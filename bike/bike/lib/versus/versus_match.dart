import 'dart:async';

import 'package:flutter/foundation.dart';

import 'match_backend.dart';

enum VersusOutcome { win, lose, draw }

/// One 1 vs 1 match, seen from this player's side.
///
/// Both players ride at the same time, each on their own road, and the
/// higher score wins. A player's run ends when they crash, or as soon as
/// they have beaten an opponent who is already out.
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

  /// How often a riding player sends their score.
  static const sendEvery = Duration(seconds: 3);

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

  StreamSubscription<MatchSnapshot?>? _sub;
  Timer? _clock;
  Duration _sinceSent = Duration.zero;
  Duration _silence = Duration.zero;

  int get myScore => _me.score;
  bool get myDone => _me.done;

  /// True once both players are in and the run can start.
  bool get ready => opponent != null;

  /// How the match ended for this player, or null while it is still open.
  VersusOutcome? get outcome {
    final other = opponent;
    if (other == null) return null;
    final mine = _me.score, theirs = other.score;
    if (_me.done && theirs > mine) return VersusOutcome.lose;
    if (opponentLeft) return VersusOutcome.win;
    if (!other.done) return null;
    if (mine > theirs) return VersusOutcome.win;
    // Level with an opponent who is out: still riding means still a chance.
    return _me.done ? VersusOutcome.draw : null;
  }

  /// Call when this player's run begins.
  void start() {
    _silence = Duration.zero;
    _clock ??= Timer.periodic(_second, (_) => _tick());
  }

  /// The score of the run in progress. It is sent every [sendEvery].
  void report(int score) {
    if (!_me.done) _me = _me.copyWith(score: score);
  }

  /// Call when this player's run is over.
  void finish(int score) {
    if (_me.done) return;
    _me = _me.copyWith(score: score, done: true);
    _send();
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
    _me = _me.copyWith(beat: _me.beat + 1);
    _backend.update(code, _me, host: isHost);
  }

  void _tick() {
    if (!_me.done && (_sinceSent += _second) >= sendEvery) _send();
    final other = opponent;
    if (other == null || other.done || opponentLeft) return;
    if ((_silence += _second) >= silenceLimit) {
      opponentLeft = true;
      notifyListeners();
    }
  }

  void _onSnapshot(MatchSnapshot? match) {
    final other = isHost ? match?.guest : match?.host;
    if (other == null) {
      // The match was closed. An opponent who had finished keeps their
      // score; one who had not has walked away.
      if (opponent != null && !opponent!.done) opponentLeft = true;
    } else {
      if (other.beat != opponent?.beat) _silence = Duration.zero;
      opponent = other;
    }
    notifyListeners();
  }

  void _stop() {
    _sub?.cancel();
    _sub = null;
    _clock?.cancel();
    _clock = null;
  }

  @override
  void dispose() {
    _stop();
    super.dispose();
  }
}
