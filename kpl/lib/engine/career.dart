import 'dart:convert';
import 'dart:math' as math;

import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../core/league.dart';
import '../core/rng.dart';
import '../data/players_db.dart';
import '../data/teams.dart';
import '../models/franchise.dart';
import '../models/player.dart';
import 'auction.dart';
import 'delivery.dart';
import 'match_engine.dart';
import 'season.dart';

enum CareerMode { mega, mini }

enum Stage { retention, auction, season, finished }

/// Match lighting: follow the fixture's start time, or force day / night.
enum MatchLight {
  auto('AUTO'),
  day('DAY'),
  night('NIGHT');

  const MatchLight(this.label);
  final String label;

  bool isDay({required bool afternoon}) => switch (this) {
        MatchLight.auto => afternoon,
        MatchLight.day => true,
        MatchLight.night => false,
      };
}

class Settings {
  Difficulty difficulty = Difficulty.normal;
  bool haptics = true;
  bool timingHint = true;
  int auctionSpeed = 1;
  MatchLight matchLight = MatchLight.auto;

  Map<String, dynamic> toJson() => {
        'd': difficulty.index,
        'h': haptics,
        't': timingHint,
        'a': auctionSpeed,
        'l': matchLight.index,
      };

  void load(Map<String, dynamic> j) {
    difficulty = Difficulty.values[(j['d'] as int?) ?? 1];
    haptics = j['h'] as bool? ?? true;
    timingHint = j['t'] as bool? ?? true;
    auctionSpeed = j['a'] as int? ?? 1;
    matchLight = MatchLight.values[((j['l'] as int?) ?? 0).clamp(0, MatchLight.values.length - 1)];
  }
}

class SeasonRecord {
  SeasonRecord(this.year, this.champion, this.runnerUp, this.userPosition);
  final int year;
  final String champion;
  final String runnerUp;
  final int userPosition;

  Map<String, dynamic> toJson() => {'y': year, 'c': champion, 'r': runnerUp, 'u': userPosition};
  static SeasonRecord fromJson(Map<String, dynamic> j) =>
      SeasonRecord(j['y'] as int, j['c'] as String, j['r'] as String, j['u'] as int);
}

/// Icon players their franchise would never let go in AI retentions.
const _icons = {
  'MS Dhoni': 'CSK',
  'Virat Kohli': 'RCB',
  'Rohit Sharma': 'MI',
  'Jasprit Bumrah': 'MI',
  'Sunil Narine': 'KKR',
  'Rashid Khan': 'GT',
};

class Career extends ChangeNotifier {
  Career._();

  static final Career instance = Career._();
  static const _saveKey = 'kpl_career_v1';
  static const _settingsKey = 'kpl_settings_v1';

  final Settings settings = Settings();
  bool active = false;
  CareerMode mode = CareerMode.mega;
  int year = League.startYear;
  String userTeamId = 'CSK';
  Map<String, Franchise> franchises = {};
  AuctionEngine? auction;
  Season? season;
  Stage stage = Stage.retention;
  int seed = 1;
  List<String> auctionNotes = [];
  Map<int, String> preAuctionTeam = {};
  final List<SeasonRecord> history = [];

  /// Released players (mini auction) for the retention-day board.
  final Map<String, List<int>> released = {};

  Franchise get user => franchises[userTeamId]!;
  TeamInfo get userTeam => teamById(userTeamId);

  // ------------------------------------------------------------ setup

  /// IPL 2026 squads with the confirmed IPL 2027 trades applied.
  static Map<String, List<Contract>> preAuctionSquads() {
    final db = PlayerDb.instance;
    final map = {for (final t in kTeams) t.id: <Contract>[]};
    for (final p in db.all) {
      if (p.team26 != null) map[p.team26]!.add(Contract(p, p.price26, Acquired.retained));
    }
    for (final tr in kTrades2027) {
      final p = db.byName(tr.player);
      if (p == null) continue;
      map[tr.from]!.removeWhere((c) => c.player.id == p.id);
      map[tr.to]!.add(Contract(p, tr.fee, Acquired.traded));
    }
    return map;
  }

  void startNew({required CareerMode mode, required String teamId}) {
    this.mode = mode;
    userTeamId = teamId;
    year = League.startYear;
    seed = DateTime.now().millisecondsSinceEpoch & 0x7fffffff;
    history.clear();
    released.clear();
    auctionNotes = [];
    final squads = preAuctionSquads();
    preAuctionTeam = {
      for (final e in squads.entries)
        for (final c in e.value) c.player.id: e.key,
    };
    franchises = {for (final t in kTeams) t.id: Franchise(t)};
    if (mode == CareerMode.mini) {
      for (final e in squads.entries) {
        final f = franchises[e.key]!;
        f.squad.addAll(e.value);
        f.purse = League.purse - f.spent;
      }
    }
    auction = null;
    season = null;
    stage = Stage.retention;
    active = true;
    notifyListeners();
  }

  /// Players the given team can retain / release before the auction.
  List<Contract> preAuctionContracts(String teamId) {
    if (mode == CareerMode.mini) return List.of(franchises[teamId]!.squad);
    final squads = preAuctionSquads();
    return squads[teamId]!;
  }

  // ----------------------------------------------------- mega retention

  /// Slab cost of each retained player, in retention order.
  static List<int> retentionCosts(List<Player> ordered) {
    final out = <int>[];
    var capped = 0;
    for (final p in ordered) {
      if (p.capped) {
        out.add(League.cappedSlabs[math.min(capped, League.cappedSlabs.length - 1)]);
        capped++;
      } else {
        out.add(League.uncappedSlab);
      }
    }
    return out;
  }

  static String? retentionProblem(List<Player> ordered) {
    final capped = ordered.where((p) => p.capped).length;
    final uncapped = ordered.length - capped;
    if (ordered.length > League.maxRetain) return 'Max ${League.maxRetain} retentions';
    if (capped > League.maxCappedRetain) return 'Max ${League.maxCappedRetain} capped players';
    if (uncapped > League.maxUncappedRetain) return 'Max ${League.maxUncappedRetain} uncapped players';
    return null;
  }

  List<Player> _aiMegaRetention(String teamId) {
    final squad = preAuctionSquads()[teamId]!.map((c) => c.player).toList()
      ..sort((a, b) => marketValue(b).compareTo(marketValue(a)));
    final keep = <Player>[];
    var capped = 0, uncapped = 0;
    const thresholds = [0.55, 0.72, 0.8, 0.95, 0.95];
    for (final p in squad) {
      if (keep.length >= League.maxRetain) break;
      final icon = _icons[p.name] == teamId;
      final v = marketValue(p);
      if (p.capped) {
        if (capped >= League.maxCappedRetain) continue;
        final cost = League.cappedSlabs[capped];
        if (icon || v >= cost * thresholds[capped]) {
          keep.add(p);
          capped++;
        }
      } else {
        if (uncapped >= League.maxUncappedRetain) continue;
        if (icon || v >= League.uncappedSlab * 0.8) {
          keep.add(p);
          uncapped++;
        }
      }
    }
    return keep;
  }

  void confirmMegaRetention(List<int> userOrderedIds) {
    final db = PlayerDb.instance;
    for (final f in franchises.values) {
      final keep = f.id == userTeamId ? userOrderedIds.map(db.byId).toList() : _aiMegaRetention(f.id);
      final costs = retentionCosts(keep);
      f.squad.clear();
      for (var i = 0; i < keep.length; i++) {
        f.squad.add(Contract(keep[i], costs[i], Acquired.retained));
      }
      f.purse = League.purse - f.spent;
      f.rtmCards = League.maxRetain - keep.length;
    }
    final retainedIds = {for (final f in franchises.values) ...f.squad.map((c) => c.player.id)};
    final pool = db.all.where((p) => !retainedIds.contains(p.id)).toList();
    _startAuction(pool);
  }

  // ---------------------------------------------------- mini releases

  List<int> _aiMiniReleases(Franchise f, Rng rng) {
    final scored = f.squad.map((c) => MapEntry(c, marketValue(c.player) - c.price)).toList()
      ..sort((a, b) => a.value.compareTo(b.value));
    final out = <int>[];
    final target = rng.rangeInt(3, 7);
    for (final e in scored) {
      if (out.length >= 9) break;
      final c = e.key;
      final o = c.player.overall;
      if (_icons[c.player.name] == f.id) continue;
      // Stars are only let go when hugely overpaid (e.g. a ₹23 Cr contract
      // for a ₹5 Cr player); fringe and overpaid squad players go first.
      if (o >= 82 && e.value > -900) continue;
      if ((e.value < -450 && o < 82) || o < 62 || (out.length < target && e.value < 0 && o < 74) || e.value <= -900) {
        out.add(c.player.id);
      }
    }
    while (out.length < 3 && out.length < scored.length) {
      final next = scored.firstWhere((e) => !out.contains(e.key.player.id)).key;
      out.add(next.player.id);
    }
    while (f.size - out.length < 15 && out.isNotEmpty) {
      out.removeLast();
    }
    return out;
  }

  void confirmMiniReleases(Set<int> userReleased) {
    final rng = Rng(seed + year * 3);
    final pool = <Player>[];
    released.clear();
    for (final f in franchises.values) {
      final out = f.id == userTeamId ? userReleased.toList() : _aiMiniReleases(f, rng);
      released[f.id] = out;
      f.squad.removeWhere((c) => out.contains(c.player.id));
      f.purse = League.purse - f.spent;
      f.rtmCards = 0;
      pool.addAll(out.map(PlayerDb.instance.byId));
    }
    final contracted = {for (final f in franchises.values) ...f.squad.map((c) => c.player.id)};
    for (final p in PlayerDb.instance.all) {
      if (!contracted.contains(p.id) && !pool.any((x) => x.id == p.id)) pool.add(p);
    }
    _startAuction(pool);
  }

  // ------------------------------------------------------------ auction

  void _startAuction(List<Player> pool) {
    final rng = Rng(seed + year);
    final lots = buildAuctionLots(pool, mega: mode == CareerMode.mega, rng: rng);
    auction = AuctionEngine(
      franchises: franchises,
      lots: lots,
      mega: mode == CareerMode.mega,
      userId: userTeamId,
      rtmTeamOf: mode == CareerMode.mega
          ? {
              for (final p in pool)
                if (preAuctionTeam[p.id] != null) p.id: preAuctionTeam[p.id]!,
            }
          : {},
      seed: seed + year * 7,
    );
    stage = Stage.auction;
    save();
    notifyListeners();
  }

  void finishAuction() {
    final a = auction;
    if (a != null) auctionNotes = a.fillSquads();
    for (final f in franchises.values) {
      f.autoLineup();
    }
    season = Season.generate(year, Rng(seed + year * 13));
    stage = Stage.season;
    save();
    notifyListeners();
  }

  // ------------------------------------------------------------- season

  MatchEngine createMatch(Fixture f, {int? seedOverride}) {
    final home = TeamSheet.fromFranchise(franchises[f.home]!);
    final away = TeamSheet.fromFranchise(franchises[f.away]!);
    return MatchEngine(
      home: home,
      away: away,
      venue: teamById(f.venueId),
      seed: seedOverride ?? (seed + year * 1000 + f.no * 17),
      difficulty: settings.difficulty,
    );
  }

  void simulateFixture(Fixture f) {
    final m = createMatch(f)..simulateToEnd();
    season!.record(f, m);
  }

  void recordUserMatch(Fixture f, MatchEngine m) {
    season!.record(f, m, byUser: true);
    _checkSeasonEnd();
    save();
    notifyListeners();
  }

  /// Simulates every fixture before the user's next one. If the user's team
  /// is out of the playoffs, simulates to the end of the season.
  /// Next unplayed fixture, creating playoff fixtures once they are known.
  Fixture? _nextFixture(Season s) {
    var next = s.nextFixture;
    if (next == null) {
      s.advancePlayoffs();
      next = s.nextFixture;
    }
    return next;
  }

  /// Simulates other teams' games until the user's next match (league or
  /// playoffs) or the end of the season.
  void simulateUntilUserMatch() {
    final s = season;
    if (s == null) return;
    var guard = 0;
    while (guard++ < 300) {
      final next = _nextFixture(s);
      if (next == null) break;
      if (next.involves(userTeamId)) break;
      simulateFixture(next);
    }
    _checkSeasonEnd();
    save();
    notifyListeners();
  }

  void simulateUserFixture(Fixture f) {
    final m = createMatch(f)..simulateToEnd();
    season!.record(f, m, byUser: false);
    _checkSeasonEnd();
    save();
    notifyListeners();
  }

  void simulateRestOfSeason() {
    final s = season;
    if (s == null) return;
    var guard = 0;
    while (guard++ < 300) {
      final next = _nextFixture(s);
      if (next == null) break;
      simulateFixture(next);
    }
    _checkSeasonEnd();
    save();
    notifyListeners();
  }

  void _checkSeasonEnd() {
    final s = season;
    if (s == null) return;
    s.advancePlayoffs();
    if (s.finished && stage != Stage.finished) {
      stage = Stage.finished;
      history.add(SeasonRecord(year, s.champion!, s.runnerUp!, s.positionOf(userTeamId)));
    }
  }

  /// Rolls the career into the next year with a mini auction.
  void startNextSeason() {
    year++;
    mode = CareerMode.mini;
    for (final f in franchises.values) {
      f.purse = League.purse - f.spent;
      f.rtmCards = 0;
    }
    auction = null;
    season = null;
    released.clear();
    stage = Stage.retention;
    save();
    notifyListeners();
  }

  // -------------------------------------------------------- persistence

  Map<String, dynamic> toJson() => {
        'mode': mode.index,
        'year': year,
        'user': userTeamId,
        'seed': seed,
        'stage': stage.index,
        'fr': franchises.values.map((f) => f.toJson()).toList(),
        'auction': auction?.toJson(),
        'season': season?.toJson(),
        'notes': auctionNotes,
        'pre': {for (final e in preAuctionTeam.entries) '${e.key}': e.value},
        'hist': history.map((h) => h.toJson()).toList(),
        'rel': released,
      };

  Future<void> save() async {
    if (!active) return;
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(_saveKey, jsonEncode(toJson()));
    } catch (e) {
      debugPrint('save failed: $e');
    }
  }

  Future<bool> hasSave() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      return prefs.containsKey(_saveKey);
    } catch (_) {
      return false;
    }
  }

  Future<bool> load() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final raw = prefs.getString(_saveKey);
      if (raw == null) return false;
      final j = jsonDecode(raw) as Map<String, dynamic>;
      mode = CareerMode.values[j['mode'] as int];
      year = j['year'] as int;
      userTeamId = j['user'] as String;
      seed = j['seed'] as int;
      stage = Stage.values[j['stage'] as int];
      franchises = {
        for (final f in (j['fr'] as List).map((e) => Franchise.fromJson(e as Map<String, dynamic>))) f.id: f,
      };
      preAuctionTeam = {
        for (final e in (j['pre'] as Map<String, dynamic>).entries) int.parse(e.key): e.value as String,
      };
      auction = j['auction'] == null
          ? null
          : AuctionEngine.fromJson(j['auction'] as Map<String, dynamic>, franchises, userTeamId, seed + year * 7);
      season = j['season'] == null ? null : Season.fromJson(j['season'] as Map<String, dynamic>);
      auctionNotes = (j['notes'] as List).cast<String>();
      history
        ..clear()
        ..addAll((j['hist'] as List).map((e) => SeasonRecord.fromJson(e as Map<String, dynamic>)));
      released
        ..clear()
        ..addAll({
          for (final e in ((j['rel'] as Map<String, dynamic>?) ?? {}).entries)
            e.key: (e.value as List).cast<int>(),
        });
      active = true;
      notifyListeners();
      return true;
    } catch (e) {
      debugPrint('load failed: $e');
      return false;
    }
  }

  Future<void> deleteSave() async {
    active = false;
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.remove(_saveKey);
    } catch (_) {}
    notifyListeners();
  }

  Future<void> loadSettings() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final raw = prefs.getString(_settingsKey);
      if (raw != null) settings.load(jsonDecode(raw) as Map<String, dynamic>);
    } catch (_) {}
  }

  Future<void> saveSettings() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(_settingsKey, jsonEncode(settings.toJson()));
    } catch (_) {}
    notifyListeners();
  }
}
