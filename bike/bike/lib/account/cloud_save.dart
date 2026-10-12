import 'dart:async';

import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:flutter/foundation.dart';

import '../shop/player_progress.dart';
import '../shop/save_data.dart';

/// One reading of a player's cloud save. [save] is null when the player has
/// none yet. [fromServer] is false for a copy kept on the device, which may
/// be out of date.
typedef CloudSnapshot = ({SaveData? save, bool fromServer});

/// Where cloud saves are kept: Firestore in the app, a fake in tests.
abstract class SaveBackend {
  /// The save of account [uid]: now, and again every time it changes (this
  /// device's own writes included).
  Stream<CloudSnapshot> watch(String uid);

  /// Applies [delta] to the save of account [uid]. Must not need a
  /// connection: offline writes are kept and sent later.
  void write(String uid, SaveDelta delta);

  /// Completes once everything written so far has reached the server.
  Future<void> flush();
}

/// Keeps [PlayerProgress] and the signed-in player's cloud save in step, so
/// coins, bikes and purchases survive a reinstall or a new phone.
///
/// The game still starts from the copy on the device. Every change made
/// here is also sent to the cloud, and every change in the cloud (another
/// device, or this one's writes) comes back in.
class CloudSave {
  CloudSave(this.progress, this._backend) {
    progress.addListener(_queuePush);
  }

  final PlayerProgress progress;
  final SaveBackend _backend;

  String? _uid;
  StreamSubscription<CloudSnapshot>? _sub;
  Completer<void> _loaded = Completer<void>()..complete();

  /// What the cloud holds, as far as this device knows. Null until the
  /// progress on this device has been joined with the account's save.
  SaveData? _cloud;
  bool _pushQueued = false;

  /// Starts syncing with the save of account [uid]. The future completes
  /// when that save is on this device, which is right away unless this is
  /// the account's first time here.
  Future<void> attach(String uid) {
    if (uid == _uid) return _loaded.future;
    _stop();
    // Progress left behind by another account is not this player's.
    if (progress.owner != null && progress.owner != uid) progress.reset();
    _uid = uid;
    _loaded = Completer<void>();
    if (progress.owner == uid) {
      _cloud = progress.data;
      _loaded.complete();
    }
    _sub = _backend.watch(uid).listen(
          _onSnapshot,
          onError: (Object e) => debugPrint('Cloud save unavailable: $e'),
        );
    return _loaded.future;
  }

  /// Stops syncing when the player signs out, and clears their progress
  /// from this device. It stays in the cloud for the next sign-in.
  void detach() {
    _stop();
    if (progress.owner != null) progress.reset();
  }

  /// Waits until the changes made so far have reached the server.
  Future<void> flush() => _backend.flush();

  void _stop() {
    _sub?.cancel();
    _sub = null;
    _uid = null;
    _cloud = null;
    if (!_loaded.isCompleted) _loaded.complete();
  }

  void _onSnapshot(CloudSnapshot snap) {
    // Joining this device's progress with the account (the first time, or
    // if the save has gone missing) needs the server's word: a copy kept on
    // the device may simply not have been downloaded yet.
    final joining = _cloud == null || snap.save == null;
    if (joining && !snap.fromServer) return;
    // Anything changed here and not sent yet goes out first, so that taking
    // on the cloud's coins below can't wipe it out.
    if (!joining) _push();
    final cloud = snap.save ?? const SaveData();
    final merged = cloud.merge(progress.data, guest: joining);
    _cloud = cloud;
    if (merged != progress.data || progress.owner != _uid) {
      progress.restore(merged, owner: _uid);
    }
    // Sends whatever this device has that the cloud is missing.
    _push();
    if (!_loaded.isCompleted) _loaded.complete();
  }

  void _queuePush() {
    if (_cloud == null || _pushQueued) return;
    _pushQueued = true;
    // Changes made together (an order marked delivered, then its coins) go
    // out as one write, so the cloud never has one without the other.
    scheduleMicrotask(_push);
  }

  void _push() {
    _pushQueued = false;
    final uid = _uid, cloud = _cloud;
    if (uid == null || cloud == null) return;
    final now = progress.data;
    final delta = now.since(cloud);
    if (delta.isEmpty) return;
    _backend.write(uid, delta);
    _cloud = now;
  }
}

/// Cloud saves in Firestore, one document per player at `users/{uid}`.
/// firestore.rules limits each document to its signed-in owner.
class FirestoreSaves implements SaveBackend {
  DocumentReference<Map<String, dynamic>> _doc(String uid) =>
      FirebaseFirestore.instance.collection('users').doc(uid);

  @override
  Stream<CloudSnapshot> watch(String uid) => _doc(uid)
      // Metadata changes say when a cached copy has been confirmed by the
      // server, even if nothing in it changed.
      .snapshots(includeMetadataChanges: true)
      .map((doc) => (
            save: _read(doc.data()),
            fromServer: !doc.metadata.isFromCache,
          ));

  static SaveData? _read(Map<String, dynamic>? doc) {
    if (doc == null) return null;
    int number(Object? v) => v is num ? v.toInt() : 0;
    List<String> strings(Object? v) =>
        v is List ? v.whereType<String>().toList() : const [];
    final selected = doc['selected'];
    return SaveData(
      wallet: number(doc['wallet']),
      best: number(doc['best']),
      doubleCoins: doc['doubleCoins'] == true,
      owned: strings(doc['owned']).toSet(),
      selected: selected is String ? selected : null,
      delivered: strings(doc['delivered']),
    );
  }

  @override
  void write(String uid, SaveDelta delta) {
    // Not awaited: offline, the write is kept on the device and the future
    // only completes once it has been sent.
    _doc(uid).set({
      if (delta.coins != 0) 'wallet': FieldValue.increment(delta.coins),
      if (delta.best != null) 'best': delta.best,
      if (delta.doubleCoins) 'doubleCoins': true,
      if (delta.bikes.isNotEmpty)
        'owned': FieldValue.arrayUnion(delta.bikes.toList()),
      if (delta.selected != null) 'selected': delta.selected,
      if (delta.orders.isNotEmpty)
        'delivered': FieldValue.arrayUnion(delta.orders),
      'updatedAt': FieldValue.serverTimestamp(),
    }, SetOptions(merge: true)).onError<Object>(
        (e, _) => debugPrint('Cloud save write failed: $e'));
  }

  @override
  Future<void> flush() => FirebaseFirestore.instance.waitForPendingWrites();
}
