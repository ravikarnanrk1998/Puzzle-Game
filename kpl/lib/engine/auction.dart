import 'dart:math' as math;

import '../core/league.dart';
import '../core/rng.dart';
import '../data/players_db.dart';
import '../models/franchise.dart';
import '../models/player.dart';

enum LotStatus { waiting, open, sold, unsold }

class AuctionLot {
  AuctionLot({
    required this.player,
    required this.base,
    required this.setCode,
    required this.setName,
    this.accelerated = false,
  });

  final Player player;
  final int base;
  final String setCode;
  final String setName;
  final bool accelerated;
  LotStatus status = LotStatus.waiting;
  String? soldTo;
  int price = 0;
  bool viaRtm = false;

  Map<String, dynamic> toJson() => {
        'p': player.id,
        'b': base,
        'c': setCode,
        'n': setName,
        'a': accelerated,
        's': status.index,
        't': soldTo,
        'pr': price,
        'r': viaRtm,
      };

  static AuctionLot fromJson(Map<String, dynamic> j) {
    final l = AuctionLot(
      player: PlayerDb.instance.byId(j['p'] as int),
      base: j['b'] as int,
      setCode: j['c'] as String,
      setName: j['n'] as String,
      accelerated: j['a'] as bool,
    );
    l.status = LotStatus.values[j['s'] as int];
    l.soldTo = j['t'] as String?;
    l.price = j['pr'] as int;
    l.viaRtm = j['r'] as bool;
    return l;
  }
}

class BidEvent {
  const BidEvent(this.teamId, this.amount);
  final String teamId;
  final int amount;
}

enum RtmStep { none, userDecide, userRaise, userMatch }

String _catOf(Player p) {
  switch (p.role) {
    case Role.bowl:
      return p.isSpin ? 'SP' : 'FA';
    case Role.ar:
      return 'AL';
    case Role.wk:
      return 'WK';
    case Role.bat:
      return 'BA';
  }
}

const _catNames = {
  'BA': 'Batters',
  'AL': 'All-rounders',
  'WK': 'Wicketkeepers',
  'FA': 'Fast Bowlers',
  'SP': 'Spinners',
};

/// Builds auction sets the way the IPL runs them: marquee sets first (mega),
/// then capped sets by speciality alternating with uncapped sets.
List<AuctionLot> buildAuctionLots(List<Player> pool, {required bool mega, required Rng rng}) {
  final lots = <AuctionLot>[];
  final sorted = List.of(pool)..sort((a, b) => b.overall.compareTo(a.overall));
  final used = <int>{};
  if (mega) {
    final marquee = sorted.where((p) => p.capped).take(12).toList();
    for (var s = 0; s * 6 < marquee.length; s++) {
      final set = marquee.sublist(s * 6, math.min(marquee.length, s * 6 + 6));
      rng.shuffle(set);
      for (final p in set) {
        lots.add(AuctionLot(player: p, base: 200, setCode: 'M${s + 1}', setName: 'Marquee Set ${s + 1}'));
        used.add(p.id);
      }
    }
  }
  const order = ['BA', 'AL', 'WK', 'FA', 'SP'];
  final capped = {
    for (final c in order) c: sorted.where((p) => p.capped && !used.contains(p.id) && _catOf(p) == c).toList(),
  };
  final uncapped = {
    for (final c in order) c: sorted.where((p) => !p.capped && !used.contains(p.id) && _catOf(p) == c).toList(),
  };
  const chunk = 8;
  for (var round = 0; round < 40; round++) {
    var added = false;
    for (final group in [capped, uncapped]) {
      final isCapped = identical(group, capped);
      for (final c in order) {
        final list = group[c]!;
        final start = round * chunk;
        if (start >= list.length) continue;
        final set = list.sublist(start, math.min(list.length, start + chunk));
        rng.shuffle(set);
        for (final p in set) {
          lots.add(AuctionLot(
            player: p,
            base: basePriceFor(p),
            setCode: '${isCapped ? '' : 'U'}$c${round + 1}',
            setName: '${isCapped ? 'Capped' : 'Uncapped'} ${_catNames[c]} ${round + 1}',
          ));
        }
        added = true;
      }
    }
    if (!added) break;
  }
  return lots;
}

class AuctionEngine {
  AuctionEngine({
    required this.franchises,
    required this.lots,
    required this.mega,
    required this.userId,
    required this.rtmTeamOf,
    required int seed,
    this.index = 0,
    this.acceleratedAdded = false,
  }) : rng = Rng(seed);

  final Map<String, Franchise> franchises;
  final List<AuctionLot> lots;
  final bool mega;
  final String userId;

  /// Player id -> franchise holding RTM rights (pre-auction club).
  final Map<int, String> rtmTeamOf;
  final Rng rng;
  int index;
  bool acceleratedAdded;

  /// When true the user's franchise bids (and decides RTMs) like an AI team -
  /// used by "AI manager" simulations.
  bool aiManagesUser = false;

  int currentBid = 0;
  String? leader;
  final List<BidEvent> bids = [];
  final Map<String, int> _cap = {};
  final Set<String> _dropped = {};
  bool userPassed = false;

  RtmStep rtmStep = RtmStep.none;
  String? rtmTeam;
  int rtmRaisedTo = 0;

  /// Info about the most recent hammer for the SOLD / UNSOLD overlay.
  AuctionLot? lastResult;

  Franchise get user => franchises[userId]!;
  AuctionLot? get lot => index < lots.length ? lots[index] : null;
  bool get finished => index >= lots.length;
  int get soldCount => lots.where((l) => l.status == LotStatus.sold).length;

  int get nextAmount {
    final l = lot!;
    return currentBid == 0 ? l.base : nextBidAmount(currentBid);
  }

  // ------------------------------------------------------------ lots

  void openLot() {
    final l = lot;
    if (l == null) return;
    currentBid = 0;
    leader = null;
    bids.clear();
    _dropped.clear();
    _cap.clear();
    userPassed = false;
    rtmStep = RtmStep.none;
    rtmTeam = null;
    rtmRaisedTo = 0;
    for (final f in franchises.values) {
      if (f.id == userId && !aiManagesUser) continue;
      final v = _valuation(f, l);
      if (v >= l.base) _cap[f.id] = v;
    }
    l.status = LotStatus.open;
  }

  /// Adds the user's AI-manager valuation to an already open lot.
  void enableAiManagerForLot() {
    final l = lot;
    if (l == null || _cap.containsKey(userId)) return;
    final v = _valuation(user, l);
    if (v >= l.base) _cap[userId] = v;
  }

  /// Number of AI teams interested at the current price (for UI tension).
  int get interestedCount => _cap.entries.where((e) => e.value >= nextAmount && !_dropped.contains(e.key)).length;

  /// Max a franchise can bid while keeping ₹30L for every remaining
  /// mandatory squad slot.
  int maxAllowed(Franchise f) {
    final after = f.size + 1;
    final reserve = math.max(0, League.minSquad - after) * League.minPrice;
    return f.purse - reserve;
  }

  String? bidBlocker(Franchise f) {
    final l = lot;
    if (l == null) return 'Auction over';
    if (f.size >= League.maxSquad) return 'Squad full (${League.maxSquad})';
    if (l.player.overseas && f.overseasCount >= League.maxOverseas) {
      return 'Overseas slots full (${League.maxOverseas})';
    }
    if (nextAmount > maxAllowed(f)) return 'Not enough purse';
    if (leader == f.id) return 'Highest bidder';
    return null;
  }

  bool placeBid(String teamId) {
    final f = franchises[teamId]!;
    if (bidBlocker(f) != null) return false;
    currentBid = nextAmount;
    leader = teamId;
    bids.add(BidEvent(teamId, currentBid));
    if (teamId == userId) userPassed = false;
    return true;
  }

  bool userBid() => placeBid(userId);

  /// One AI decision. Returns the bidding team id, or null if nobody bid.
  String? aiTick() {
    if (lot == null) return null;
    final amount = nextAmount;
    final ids = <String>[];
    final weights = <double>[];
    for (final e in _cap.entries) {
      final id = e.key;
      if (id == leader || _dropped.contains(id)) continue;
      final f = franchises[id]!;
      if (bidBlocker(f) != null || e.value < amount) {
        _dropped.add(id);
        continue;
      }
      final involved = bids.any((b) => b.teamId == id);
      final headroom = (e.value - amount) / math.max(1, amount);
      ids.add(id);
      weights.add((involved ? 3.0 : 1.0) * (0.5 + math.min(2.0, headroom)));
    }
    if (ids.isEmpty) return null;
    final id = ids[rng.weightedIndex(weights)];
    final closeness = amount / _cap[id]!;
    if (closeness > 0.85 && rng.chance(0.35)) return null;
    placeBid(id);
    return id;
  }

  /// True while some AI team could still bid on the current lot.
  bool get aiStillInterested {
    final amount = nextAmount;
    for (final e in _cap.entries) {
      if (e.key == leader || _dropped.contains(e.key)) continue;
      if (e.value >= amount && bidBlocker(franchises[e.key]!) == null) return true;
    }
    return false;
  }

  // -------------------------------------------------------- hammer/RTM

  bool _rtmPossible(String holder, int price) {
    final f = franchises[holder]!;
    if (f.rtmCards <= 0) return false;
    if (f.size >= League.maxSquad) return false;
    if (lot!.player.overseas && f.overseasCount >= League.maxOverseas) return false;
    return price <= maxAllowed(f);
  }

  int _roundBid(int v) {
    var x = currentBid;
    while (nextBidAmount(x) <= v) {
      x = nextBidAmount(x);
    }
    return x;
  }

  int _aiRaise(String teamId, int price) {
    final cap = _cap[teamId] ?? 0;
    if (cap <= price) return price;
    final target = price + ((cap - price) * rng.range(0.3, 0.8)).round();
    final limit = math.min(target, maxAllowed(franchises[teamId]!));
    return math.max(price, _roundBid(limit));
  }

  /// Ends bidding on the lot. Returns true if the user must decide on RTM.
  bool hammer({bool autoUser = false}) {
    final l = lot!;
    if (leader == null) {
      l.status = LotStatus.unsold;
      lastResult = l;
      return false;
    }
    final holder = rtmTeamOf[l.player.id];
    final userIsAi = aiManagesUser;
    if (mega && holder != null && holder != leader && _rtmPossible(holder, currentBid)) {
      if (holder == userId && !userIsAi) {
        if (autoUser) {
          _sell(leader!, currentBid);
          return false;
        }
        rtmTeam = holder;
        rtmStep = RtmStep.userDecide;
        return true;
      }
      final hf = franchises[holder]!;
      final v = _valuation(hf, l, noise: false) * rng.range(0.9, 1.15);
      if (v >= currentBid * 0.98) {
        rtmTeam = holder;
        if (leader == userId && !userIsAi) {
          if (autoUser) {
            _sell(holder, currentBid, rtm: true);
            return false;
          }
          rtmStep = RtmStep.userRaise;
          return true;
        }
        final raised = _aiRaise(leader!, currentBid);
        if (raised > currentBid) {
          if (v >= raised && _rtmPossible(holder, raised)) {
            _sell(holder, raised, rtm: true);
          } else {
            _sell(leader!, raised);
          }
        } else {
          _sell(holder, currentBid, rtm: true);
        }
        return false;
      }
    }
    _sell(leader!, currentBid);
    return false;
  }

  /// User holds RTM: decide whether to use it at the current price.
  void userUseRtm(bool use) {
    if (!use) {
      _sell(leader!, currentBid);
      return;
    }
    final raised = _aiRaise(leader!, currentBid);
    if (raised > currentBid) {
      rtmRaisedTo = raised;
      rtmStep = RtmStep.userMatch;
    } else {
      _sell(userId, currentBid, rtm: true);
    }
  }

  /// After the highest bidder raised, the user (RTM holder) matches or not.
  void userMatchRaise(bool match) {
    if (match && _rtmPossible(userId, rtmRaisedTo)) {
      _sell(userId, rtmRaisedTo, rtm: true);
    } else {
      _sell(leader!, rtmRaisedTo);
    }
  }

  /// User was the highest bidder and an AI used RTM: final raise.
  void userFinalRaise(int newPrice) {
    final price = math.min(math.max(newPrice, currentBid), maxAllowed(user));
    final hf = franchises[rtmTeam!]!;
    final v = _valuation(hf, lot!, noise: false) * rng.range(0.95, 1.2);
    if (v >= price && _rtmPossible(rtmTeam!, price)) {
      _sell(rtmTeam!, price, rtm: true);
    } else {
      _sell(userId, price);
    }
  }

  void _sell(String teamId, int price, {bool rtm = false}) {
    final l = lot!;
    final f = franchises[teamId]!;
    f.purse -= price;
    f.squad.add(Contract(l.player, price, rtm ? Acquired.rtm : Acquired.auction));
    if (rtm) f.rtmCards = math.max(0, f.rtmCards - 1);
    l.status = LotStatus.sold;
    l.soldTo = teamId;
    l.price = price;
    l.viaRtm = rtm;
    leader = teamId;
    currentBid = price;
    rtmStep = RtmStep.none;
    lastResult = l;
  }

  /// Moves to the next lot (adding the accelerated round when needed).
  void advance() {
    index++;
    if (index >= lots.length && !acceleratedAdded) {
      acceleratedAdded = true;
      final needs = franchises.values.any((f) => f.size < League.minSquad + 2 && f.slotsLeft > 0);
      if (needs) {
        final unsold = lots.where((l) => l.status == LotStatus.unsold).toList()
          ..sort((a, b) => b.player.overall.compareTo(a.player.overall));
        for (final l in unsold.take(60)) {
          lots.add(AuctionLot(
            player: l.player,
            base: l.base,
            setCode: 'ACC',
            setName: 'Accelerated Round',
            accelerated: true,
          ));
        }
      }
    }
  }

  /// Resolves the current lot without the user bidding (sim / skip).
  void resolveInstantly() {
    if (lot == null) return;
    if (lot!.status != LotStatus.open) openLot();
    if (aiManagesUser) enableAiManagerForLot();
    var idle = 0;
    var guard = 0;
    while (idle < 3 && guard++ < 400) {
      if (aiTick() == null) {
        idle++;
      } else {
        idle = 0;
      }
    }
    hammer(autoUser: true);
  }

  /// Signs cheapest remaining players for any team below the minimum squad.
  List<String> fillSquads() {
    final notes = <String>[];
    final taken = <int>{for (final f in franchises.values) ...f.squad.map((c) => c.player.id)};
    final available = <Player>[
      for (final l in lots)
        if (!taken.contains(l.player.id)) l.player,
    ];
    final seen = <int>{};
    available.retainWhere((p) => seen.add(p.id));
    available.sort((a, b) => b.overall.compareTo(a.overall));
    for (final f in franchises.values) {
      while (f.size < League.minSquad) {
        final idx = available.indexWhere((p) =>
            basePriceFor(p) <= f.purse && (!p.overseas || f.overseasCount < League.maxOverseas));
        if (idx < 0) break;
        final p = available.removeAt(idx);
        final price = math.min(basePriceFor(p), f.purse);
        f.purse -= price;
        f.squad.add(Contract(p, price, Acquired.supplementary));
        if (f.id == userId) notes.add('${p.name} signed at base price ${money(price)}');
      }
    }
    return notes;
  }

  // -------------------------------------------------------- AI brain

  double _needFactor(Franchise f, Player p) {
    final m = f.mix;
    switch (p.role) {
      case Role.wk:
        return m.wk == 0 ? 1.45 : (m.wk == 1 ? 1.08 : 0.7);
      case Role.bat:
        return m.batters < 4 ? 1.25 : (m.batters < 7 ? 1.0 : 0.72);
      case Role.ar:
        return m.ar < 3 ? 1.25 : (m.ar < 5 ? 1.0 : 0.75);
      case Role.bowl:
        if (p.isSpin) return m.spin < 2 ? 1.35 : (m.spin < 4 ? 1.0 : 0.7);
        return m.pace < 4 ? 1.3 : (m.pace < 7 ? 1.0 : 0.72);
    }
  }

  int _valuation(Franchise f, AuctionLot l, {bool noise = true}) {
    final p = l.player;
    if (f.size >= League.maxSquad) return 0;
    if (p.overseas && f.overseasCount >= League.maxOverseas) return 0;
    var v = marketValue(p).toDouble();
    v *= _needFactor(f, p);
    if (p.overseas) {
      final left = League.maxOverseas - f.overseasCount;
      if (left <= 2 && p.overall < 82) v *= 0.7;
    }
    final slotsToTarget = math.max(1, 22 - f.size);
    final perSlot = f.purse / slotsToTarget;
    v *= (perSlot / 420).clamp(0.5, 1.25);
    if (f.size >= 23 && p.overall < 84) v *= 0.6;
    if (noise) v *= rng.range(0.75, 1.25);
    if (p.team26 == f.id) v *= 1.1;
    if (p.overall >= 88) v *= 1.15;
    // Captaincy scramble: teams without an Indian star chase one hard.
    if (!p.overseas && p.capped && p.overall >= 84 &&
        !f.squad.any((c) => !c.player.overseas && c.player.overall >= 84)) {
      v *= 1.3;
    }
    if (l.accelerated) v *= 0.85;
    if (noise) {
      final disinterest = p.overall >= 86 ? 0.12 : (p.overall >= 76 ? 0.3 : 0.45);
      if (rng.chance(disinterest)) return 0;
    }
    return math.min(v, maxAllowed(f).toDouble()).round();
  }

  // ---------------------------------------------------------- persist

  Map<String, dynamic> toJson() => {
        'lots': lots.map((l) => l.toJson()).toList(),
        'i': index,
        'acc': acceleratedAdded,
        'mega': mega,
        'rtm': {for (final e in rtmTeamOf.entries) '${e.key}': e.value},
      };

  static AuctionEngine fromJson(Map<String, dynamic> j, Map<String, Franchise> franchises, String userId, int seed) {
    final lots = (j['lots'] as List).map((e) => AuctionLot.fromJson(e as Map<String, dynamic>)).toList();
    final e = AuctionEngine(
      franchises: franchises,
      lots: lots,
      mega: j['mega'] as bool,
      userId: userId,
      rtmTeamOf: {
        for (final x in (j['rtm'] as Map<String, dynamic>).entries) int.parse(x.key): x.value as String,
      },
      seed: seed,
      index: j['i'] as int,
      acceleratedAdded: j['acc'] as bool,
    );
    // A lot that was mid-bidding when saved is re-opened from scratch.
    if (e.lot != null && e.lot!.status == LotStatus.open) e.lot!.status = LotStatus.waiting;
    return e;
  }
}
