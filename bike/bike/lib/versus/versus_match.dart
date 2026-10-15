import 'dart:async';

import 'package:flutter/foundation.dart';

import 'match_backend.dart';

enum VersusOutcome { win, lose, draw }

/// One 1 vs 1 match, seen from this player's side.
///
/// Both players ride at the same time, each on their own road, and the
/// higher score wins. A player's run ends when they crash, as soon as they
/// have beaten an opponent who is already out, or when their connection is
/// lost, which loses them the match.
///
/// Each phone judges the match from what it has heard, but the result both
/// players see is the first one to reach the server. A phone that was
/// offline can't overturn it with scores it sends once it is back.
class VersusMatch extends ChangeNotifier {
  VersusMatch._(this._backend, this.code, this._me, {required this.isHost}) {
    _sub = _backend.watch(code).listen(
          _onSnapshot,
          onError: (Object e) => debugPrint('Match unavailable: $e'),
        );
  }

  /// Opens a match and waits for a friend to enter its [code].
  static Future<VersusMatch> create(
    MatchBackend backend, {
    required String uid,
    required String name,
  }) async {
    final me = MatchPlayer(uid: uid, name: name);
    final code = await backend.create(me);
    return VersusMatch._(backend, code, me, isHost: true);
  }

  /// Joins the match a friend created. Throws a [MatchError] if [code] is
  /// wrong or the match is already full.
  static Future<VersusMatch> join(
    MatchBackend backend,
    String code, {
    required String uid,
    required String name,
  }) async {
    final me = MatchPlayer(uid: uid, name: name);
    await backend.join(code, me);
    return VersusMatch._(backend, code, me, isHost: false);
  }

  /// How often a player sends their score, and with it that they are still
  /// there.
  static const sendEvery = Duration(seconds: 3);

  /// While something this player sent has gone unanswered this long, their
  /// connection is shown as weak.
  static const weakAfter = Duration(seconds: 3);

  /// A riding player whose connection has been gone this long has lost the
  /// match. They find out before their opponent stops waiting for them.
  static const lostAfter = Duration(seconds: 8);

  /// An opponent who has sent nothing for this long has left the match:
  /// closed the game, or lost their connection.
  static const silenceLimit = Duration(seconds: 12);

  static const _second = Duration(seconds: 1);

  final MatchBackend _backend;
  final String code;
  final bool isHost;

  MatchPlayer _me;

  /// The other player. Null until they have joined.
  MatchPlayer? opponent;

  /// True if the other player went away without finishing their run.
  bool opponentLeft = false;

  /// True if this player's connection was lost during their run.
  bool connectionLost = false;

  /// True while what this player sends is not getting through.
  bool weakConnection = false;

  StreamSubscription<MatchSnapshot?>? _sub;
  Timer? _clock;
  bool _disposed = false;

  /// Time since [start], counted by [_clock].
  Duration _time = Duration.zero;
  Duration _sinceSent = Duration.zero;
  Duration _silence = Duration.zero;

  /// When each send the server has not answered yet went out, by beat,
  /// oldest first.
  final Map<int, Duration> _unanswered = {};

  /// How this phone judges the match. Fixed once it is known.
  VersusOutcome? _verdict;

  /// The result the server holds: a uid, or [drawResult].
  String? _winner;
  bool _settling = false;

  /// True once the match has been closed.
  bool _closed = false;

  int get myScore => _me.score;
  bool get myDone => _me.done;

  /// True once both players are in and the run can start.
  bool get ready => opponent != null;

  /// True if the other player lost their connection during their run.
  bool get opponentDropped => opponent?.dropped ?? false;

  /// True once this player has nothing left to ride for: the match is won,
  /// or lost, even if the result has not reached the server yet.
  bool get decided => _verdict != null || outcome != null;

  /// How the match ended for this player, or null while it is still open
  /// or its result is on its way.
  VersusOutcome? get outcome {
    final winner = _winner;
    if (winner != null) {
      if (winner == drawResult) return VersusOutcome.draw;
      return winner == _me.uid ? VersusOutcome.win : VersusOutcome.lose;
    }
    if (connectionLost) return VersusOutcome.lose;
    // Closed before it was settled: nobody is left to disagree.
    if (_closed) return _verdict;
    return null;
  }

  /// Call when this player's run begins.
  void start() {
    _silence = Duration.zero;
    _clock ??= Timer.periodic(_second, (_) => _tick());
  }

  /// The score of the run in progress. It is sent every [sendEvery].
  void report(int score) {
    if (_me.done) return;
    _me = _me.copyWith(score: score);
    if (_decide()) notifyListeners();
  }

  /// Call when this player's run is over.
  void finish(int score) {
    if (_me.done) return;
    _me = _me.copyWith(score: score, done: true);
    _send();
    _decide();
    notifyListeners();
  }

  /// Leaves the match. The host leaving closes it for both players; a guest
  /// leaving mid-run gives up with the score they have.
  void leave() {
    _stop();
    if (isHost) {
      _backend.close(code).onError<Object>(
          (e, _) => debugPrint('Could not close the match: $e'));
    } else if (!_me.done) {
      _me = _me.copyWith(done: true);
      _send();
    }
  }

  void _send() {
    _sinceSent = Duration.zero;
    final beat = _me.beat + 1;
    _me = _me.copyWith(beat: beat);
    _unanswered[beat] = _time;
    // The server answers in order: an answer covers everything before it.
    _backend.update(code, _me, host: isHost).whenComplete(
        () => _unanswered.removeWhere((sent, _) => sent <= beat));
  }

  void _tick() {
    _time += _second;
    final open = outcome == null;
    // Scores ridden offline don't pile up to arrive later: the next one
    // waits until the last has got through.
    if (open && (_sinceSent += _second) >= sendEvery && _unanswered.isEmpty) {
      _send();
    }

    final waited = _unanswered.isEmpty
        ? Duration.zero
        : _time - _unanswered.values.first;
    final weak = open && waited >= weakAfter;
    var changed = weak != weakConnection;
    weakConnection = weak;
    if (open && !_me.done && waited >= lostAfter) {
      connectionLost = true;
      _me = _me.copyWith(done: true, dropped: true);
      _send();
      changed = true;
    }

    final other = opponent;
    if (weak) {
      // Nothing heard because this phone is offline is not the other
      // player's fault.
      _silence = Duration.zero;
    } else if (other != null &&
        !other.done &&
        !opponentLeft &&
        (_silence += _second) >= silenceLimit) {
      opponentLeft = true;
      changed = true;
    }
    if (_decide()) changed = true;
    _settle();
    if (changed) notifyListeners();
  }

  void _onSnapshot(MatchSnapshot? match) {
    if (match == null) {
      // The match was closed. An opponent who had finished keeps their
      // score; one who had not has walked away.
      _closed = true;
      if (opponent != null && !opponent!.done) opponentLeft = true;
    } else {
      _winner ??= match.winner;
      final other = isHost ? match.guest : match.host;
      if (other != null) {
        if (other.beat != opponent?.beat) _silence = Duration.zero;
        opponent = other;
        if (other.dropped) opponentLeft = true;
      }
    }
    _decide();
    notifyListeners();
  }

  /// Judges the match from what this phone knows, once. Returns true if
  /// that has just happened.
  bool _decide() {
    if (_verdict != null) return false;
    _verdict = _judge();
    if (_verdict == null) return false;
    _settle();
    return true;
  }

  VersusOutcome? _judge() {
    if (connectionLost) return VersusOutcome.lose;
    final other = opponent;
    if (other == null) return null;
    if (opponentLeft) return VersusOutcome.win;
    final mine = _me.score, theirs = other.score;
    if (_me.done && theirs > mine) return VersusOutcome.lose;
    if (!other.done) return null;
    if (mine > theirs) return VersusOutcome.win;
    // Level with an opponent who is out: still riding means still a chance.
    return _me.done ? VersusOutcome.draw : null;
  }

  /// Sends this phone's verdict to the server, where the first result to
  /// arrive is the one that counts. Tried again every second until it
  /// gets there.
  void _settle() {
    final verdict = _verdict, other = opponent;
    // A player who lost their connection leaves it to the other player.
    if (verdict == null || other == null || connectionLost) return;
    if (_winner != null || _closed || _settling) return;
    _settling = true;
    final winner = switch (verdict) {
      VersusOutcome.win => _me.uid,
      VersusOutcome.lose => other.uid,
      VersusOutcome.draw => drawResult,
    };
    _backend.settle(code, winner).then((settled) {
      if (_disposed || settled == null || _winner != null) return;
      _winner = settled;
      notifyListeners();
    }, onError: (Object e) {
      debugPrint('Could not settle the match: $e');
    }).whenComplete(() => _settling = false);
  }

  void _stop() {
    _sub?.cancel();
    _sub = null;
    _clock?.cancel();
    _clock = null;
  }

  @override
  void dispose() {
    _disposed = true;
    _stop();
    super.dispose();
  }
}
