import 'dart:math';

import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:flutter/foundation.dart';

/// One side of a 1 vs 1 match.
@immutable
class MatchPlayer {
  const MatchPlayer({
    required this.uid,
    required this.name,
    this.score = 0,
    this.done = false,
    this.beat = 0,
  });

  final String uid;
  final String name;
  final int score;

  /// True once this player's run is over.
  final bool done;

  /// Counts up while the player is riding, so the other side can tell a
  /// player who is still there from one whose game has closed.
  final int beat;

  MatchPlayer copyWith({int? score, bool? done, int? beat}) => MatchPlayer(
        uid: uid,
        name: name,
        score: score ?? this.score,
        done: done ?? this.done,
        beat: beat ?? this.beat,
      );
}

/// A match as it stands. [guest] is null until a second player has joined.
typedef MatchSnapshot = ({MatchPlayer host, MatchPlayer? guest});

/// Why a match could not be created or joined, worded for the player.
class MatchError implements Exception {
  const MatchError(this.message);

  final String message;

  @override
  String toString() => message;
}

/// Where matches are kept: Firestore in the app, a fake in tests.
abstract class MatchBackend {
  /// Opens a match with one free seat and returns its code.
  Future<String> create(MatchPlayer host);

  /// Takes the free seat in match [code]. Throws a [MatchError] if the code
  /// is wrong, already used, or the player's own.
  Future<void> join(String code, MatchPlayer guest);

  /// The match now and whenever it changes. Null once it has been closed.
  Stream<MatchSnapshot?> watch(String code);

  /// Stores one player's side. Each player only ever writes their own.
  void update(String code, MatchPlayer player, {required bool host});

  /// Closes the match for both players.
  Future<void> close(String code);
}

/// Matches in Firestore, one document per match at `matches/{code}`.
class FirestoreMatches implements MatchBackend {
  /// A code can be handed out again once the match behind it is this old.
  static const _lifetime = Duration(minutes: 30);

  final Random _rng = Random();

  FirebaseFirestore get _db => FirebaseFirestore.instance;

  DocumentReference<Map<String, dynamic>> _doc(String code) =>
      _db.collection('matches').doc(code);

  static bool _expired(Map<String, dynamic> match) {
    final created = match['createdAt'];
    return created is Timestamp &&
        DateTime.now().difference(created.toDate()) > _lifetime;
  }

  static Map<String, Object?> _write(MatchPlayer p) => {
        'uid': p.uid,
        'name': p.name,
        'score': p.score,
        'done': p.done,
        'beat': p.beat,
      };

  static MatchPlayer? _read(Object? side) {
    if (side is! Map) return null;
    final uid = side['uid'], name = side['name'];
    final score = side['score'], beat = side['beat'];
    if (uid is! String) return null;
    return MatchPlayer(
      uid: uid,
      name: name is String ? name : 'Player',
      score: score is num ? score.toInt() : 0,
      done: side['done'] == true,
      beat: beat is num ? beat.toInt() : 0,
    );
  }

  /// Runs a Firestore step. Being refused means the rules for matches in
  /// firestore.rules have not been published in the Firebase console yet.
  static Future<T> _allowed<T>(Future<T> Function() step) async {
    try {
      return await step();
    } on FirebaseException catch (e) {
      if (e.code != 'permission-denied') rethrow;
      debugPrint('Matches refused: publish firestore.rules in the console.');
      throw const MatchError('1 VS 1 is not switched on yet.');
    }
  }

  @override
  Future<String> create(MatchPlayer host) async {
    for (var attempt = 0; attempt < 5; attempt++) {
      final code = '${100000 + _rng.nextInt(900000)}';
      final free = await _allowed(() => _db.runTransaction((tx) async {
            final match = (await tx.get(_doc(code))).data();
            if (match != null && !_expired(match)) return false;
            tx.set(_doc(code), {
              'host': _write(host),
              'createdAt': FieldValue.serverTimestamp(),
            });
            return true;
          }));
      if (free) return code;
    }
    throw const MatchError('Could not create a code. Please try again.');
  }

  @override
  Future<void> join(String code, MatchPlayer guest) async {
    // The reason comes back as a value: an exception thrown inside a
    // transaction does not reliably arrive as the same exception.
    final problem = await _allowed(() => _db.runTransaction((tx) async {
          final match = (await tx.get(_doc(code))).data();
          if (match == null || _expired(match)) {
            return 'No match found with that code.';
          }
          if (_read(match['host'])?.uid == guest.uid) {
            return 'That is your own code. Send it to a friend.';
          }
          if (match['guest'] != null) {
            return 'That match already has two players.';
          }
          tx.update(_doc(code), {'guest': _write(guest)});
          return null;
        }));
    if (problem != null) throw MatchError(problem);
  }

  @override
  Stream<MatchSnapshot?> watch(String code) => _doc(code).snapshots().map((doc) {
        final match = doc.data();
        final host = _read(match?['host']);
        return host == null
            ? null
            : (host: host, guest: _read(match?['guest']));
      });

  @override
  void update(String code, MatchPlayer player, {required bool host}) {
    _doc(code)
        .update({host ? 'host' : 'guest': _write(player)}).onError<Object>(
            (e, _) => debugPrint('Match update failed: $e'));
  }

  @override
  Future<void> close(String code) => _doc(code).delete();
}
