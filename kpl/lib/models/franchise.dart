import 'dart:math' as math;

import '../core/league.dart';
import '../data/players_db.dart';
import '../data/teams.dart';
import 'player.dart';

enum Acquired { retained, auction, rtm, traded, supplementary }

extension AcquiredX on Acquired {
  String get label =>
      const ['Retained', 'Auction', 'RTM', 'Traded', 'Supplementary'][index];
}

class Contract {
  Contract(this.player, this.price, this.how);

  final Player player;
  final int price;
  final Acquired how;

  Map<String, dynamic> toJson() => {'p': player.id, 'c': price, 'h': how.index};

  static Contract fromJson(Map<String, dynamic> j) => Contract(
        PlayerDb.instance.byId(j['p'] as int),
        j['c'] as int,
        Acquired.values[j['h'] as int],
      );
}

class SquadMix {
  int wk = 0, bat = 0, ar = 0, pace = 0, spin = 0, overseas = 0;
  int get batters => wk + bat;
}

class Franchise {
  Franchise(this.info, {int? purse, List<Contract>? squad, this.rtmCards = 0})
      : purse = purse ?? League.purse,
        squad = squad ?? [];

  final TeamInfo info;
  int purse;
  final List<Contract> squad;
  int rtmCards;

  /// Playing XI ids in batting order.
  List<int> xi = [];
  int? captainId;
  int? keeperId;

  String get id => info.id;
  int get size => squad.length;
  int get overseasCount => squad.where((c) => c.player.overseas).length;
  int get slotsLeft => League.maxSquad - size;
  int get overseasLeft => League.maxOverseas - overseasCount;
  List<Player> get players => squad.map((c) => c.player).toList();
  int get spent => squad.fold(0, (s, c) => s + c.price);

  bool contains(int playerId) => squad.any((c) => c.player.id == playerId);

  Contract? contractOf(int playerId) {
    for (final c in squad) {
      if (c.player.id == playerId) return c;
    }
    return null;
  }

  SquadMix get mix {
    final m = SquadMix();
    for (final c in squad) {
      final p = c.player;
      switch (p.role) {
        case Role.wk:
          m.wk++;
        case Role.bat:
          m.bat++;
        case Role.ar:
          m.ar++;
        case Role.bowl:
          break;
      }
      if (p.bowl >= 62 && p.isPace) m.pace++;
      if (p.bowl >= 62 && p.isSpin) m.spin++;
      if (p.overseas) m.overseas++;
    }
    return m;
  }

  List<Player> get xiPlayers =>
      xi.map((id) => PlayerDb.instance.byId(id)).toList();

  void autoLineup() {
    final best = pickBestXI(players);
    xi = best.map((p) => p.id).toList();
    captainId = chooseCaptain(best, info).id;
    keeperId = chooseKeeper(best).id;
  }

  /// Ensures the stored XI is still legal for the current squad.
  void ensureLineup() {
    final ids = squad.map((c) => c.player.id).toSet();
    if (xi.length != 11 || xi.any((id) => !ids.contains(id)) || lineupProblem(xiPlayers) != null) {
      autoLineup();
    }
    if (captainId == null || !xi.contains(captainId)) {
      captainId = chooseCaptain(xiPlayers, info).id;
    }
    if (keeperId == null || !xi.contains(keeperId)) {
      keeperId = chooseKeeper(xiPlayers).id;
    }
  }

  /// Rating of the best XI (0-99) - shown on summary screens.
  int get strength => squad.length < 11 ? 0 : teamRating(pickBestXI(players));
  int get battingRating => squad.length < 11 ? 0 : batRating(pickBestXI(players));
  int get bowlingRating => squad.length < 11 ? 0 : bowlRating(pickBestXI(players));

  Map<String, dynamic> toJson() => {
        'id': id,
        'purse': purse,
        'rtm': rtmCards,
        'squad': squad.map((c) => c.toJson()).toList(),
        'xi': xi,
        'cap': captainId,
        'wk': keeperId,
      };

  static Franchise fromJson(Map<String, dynamic> j) {
    final f = Franchise(
      teamById(j['id'] as String),
      purse: j['purse'] as int,
      rtmCards: j['rtm'] as int,
      squad: (j['squad'] as List)
          .map((e) => Contract.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
    f.xi = (j['xi'] as List).cast<int>();
    f.captainId = j['cap'] as int?;
    f.keeperId = j['wk'] as int?;
    return f;
  }
}

// ---------------------------------------------------------------- lineups

double _batValue(Player p) => p.bat * 0.85 + p.power * 0.15;

/// Returns a human readable rule violation for the XI, or null when legal.
String? lineupProblem(List<Player> xi) {
  if (xi.length != 11) return 'Pick exactly 11 players (${xi.length} selected)';
  final os = xi.where((p) => p.overseas).length;
  if (os > League.maxOverseasXI) return 'Max ${League.maxOverseasXI} overseas players in the XI';
  if (!xi.any((p) => p.role == Role.wk)) return 'Pick a wicketkeeper';
  return null;
}

/// Soft advice (the XI is still allowed).
String? lineupWarning(List<Player> xi) {
  final options = xi.where((p) => p.isBowlingOption).length;
  if (options < 5) return 'Only $options proper bowling options - part-timers will bowl';
  return null;
}

double xiScore(List<Player> xi) {
  if (xi.length != 11) return -1e9;
  if (xi.where((p) => p.overseas).length > League.maxOverseasXI) return -1e9;
  var s = 0.0;
  if (!xi.any((p) => p.role == Role.wk)) s -= 60;
  final bats = xi.map(_batValue).toList()..sort((a, b) => b.compareTo(a));
  const batW = [1.0, 1.0, 1.0, 0.95, 0.9, 0.8, 0.62, 0.38, 0.2, 0.1, 0.05];
  for (var i = 0; i < 11; i++) {
    s += bats[i] * batW[i];
  }
  final bowls = xi
      .where((p) => p.bowlStyle != BowlStyle.none)
      .map((p) => p.bowl.toDouble())
      .toList()
    ..sort((a, b) => b.compareTo(a));
  const bowlW = [1.0, 1.0, 1.0, 0.95, 0.9, 0.3];
  for (var i = 0; i < math.min(6, bowls.length); i++) {
    s += bowls[i] * bowlW[i];
  }
  final options = xi.where((p) => p.isBowlingOption).length;
  if (options < 5) s -= (5 - options) * 50;
  final pace = xi.where((p) => p.isPace && p.bowl >= 62).length;
  final spin = xi.where((p) => p.isSpin && p.bowl >= 62).length;
  if (pace < 2) s -= 30;
  if (spin < 1) s -= 14;
  return s;
}

/// Greedy start + swap-based local search for the strongest legal XI.
List<Player> pickBestXI(List<Player> squad) {
  if (squad.length <= 11) return orderBatting(List.of(squad));
  final pool = List.of(squad)..sort((a, b) => b.overall.compareTo(a.overall));
  final xi = <Player>[];
  var os = 0;
  for (final p in pool) {
    if (xi.length == 11) break;
    if (p.overseas && os >= League.maxOverseasXI) continue;
    xi.add(p);
    if (p.overseas) os++;
  }
  var best = xiScore(xi);
  for (var pass = 0; pass < 12; pass++) {
    var improved = false;
    for (var i = 0; i < xi.length; i++) {
      for (final cand in pool) {
        if (xi.contains(cand)) continue;
        final trial = List.of(xi)..[i] = cand;
        final sc = xiScore(trial);
        if (sc > best + 0.01) {
          xi[i] = cand;
          best = sc;
          improved = true;
        }
      }
    }
    if (!improved) break;
  }
  return orderBatting(xi);
}

/// Batting order: top order by batting skill, finishers in the middle,
/// bowlers in the tail.
List<Player> orderBatting(List<Player> xi) {
  double key(Player p) {
    var k = _batValue(p);
    if (p.role == Role.bowl) k -= 40;
    return k;
  }

  final sorted = List.of(xi)..sort((a, b) => key(b).compareTo(key(a)));
  // Move pure hitters (huge power, lower technique) from the top 3 to 5/6.
  for (var i = 0; i < math.min(3, sorted.length); i++) {
    final p = sorted[i];
    if (p.power >= 92 && p.bat < 80 && sorted.length > 6) {
      sorted.removeAt(i);
      sorted.insert(5, p);
    }
  }
  return sorted;
}

Player chooseCaptain(List<Player> xi, TeamInfo team) {
  for (final p in xi) {
    if (p.name == team.captain2026) return p;
  }
  final indians = xi.where((p) => !p.overseas).toList()
    ..sort((a, b) => b.overall.compareTo(a.overall));
  if (indians.isNotEmpty) return indians.first;
  return xi.reduce((a, b) => a.overall >= b.overall ? a : b);
}

Player chooseKeeper(List<Player> xi) {
  final keepers = xi.where((p) => p.role == Role.wk).toList()
    ..sort((a, b) => b.bat.compareTo(a.bat));
  if (keepers.isNotEmpty) return keepers.first;
  return xi.reduce((a, b) => a.bat >= b.bat ? a : b);
}

int batRating(List<Player> xi) {
  final v = xi.map(_batValue).toList()..sort((a, b) => b.compareTo(a));
  final top = v.take(7).toList();
  if (top.isEmpty) return 0;
  return (top.reduce((a, b) => a + b) / top.length).round();
}

int bowlRating(List<Player> xi) {
  final v = xi.map((p) => p.bowl.toDouble()).toList()..sort((a, b) => b.compareTo(a));
  final top = v.take(5).toList();
  if (top.isEmpty) return 0;
  return (top.reduce((a, b) => a + b) / top.length).round();
}

int teamRating(List<Player> xi) =>
    ((batRating(xi) * 0.52 + bowlRating(xi) * 0.48)).round();
