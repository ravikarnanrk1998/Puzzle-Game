import 'dart:async';

import 'package:firebase_auth/firebase_auth.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/foundation.dart';
import 'package:google_sign_in/google_sign_in.dart';

import '../shop/player_progress.dart';
import 'cloud_save.dart';

/// Sign-in with Firebase Auth (Google, or email and password). Whoever is
/// signed in has their progress kept in the cloud by [cloud].
///
/// Firebase remembers the player on the device, so they only sign in once.
class AccountService extends ChangeNotifier {
  AccountService(this.cloud);

  /// Connects to Firebase and picks up the player who signed in last time.
  ///
  /// Returns null where Firebase is not set up (desktop and web, or Android
  /// without google-services.json). The game then runs without accounts and
  /// saves on the device only.
  static Future<AccountService?> start(PlayerProgress progress) async {
    try {
      await Firebase.initializeApp();
    } catch (e) {
      debugPrint('Firebase is not set up, so no sign-in or cloud save: $e');
      return null;
    }
    return AccountService(CloudSave(progress, FirestoreSaves())).._listen();
  }

  /// How long a player who just signed in waits for their save before the
  /// game opens anyway. A save that arrives later is still loaded.
  static const _loadTimeout = Duration(seconds: 8);

  static const _authMessages = {
    'invalid-email': 'That email address is not valid.',
    'invalid-credential': 'Wrong email or password.',
    'wrong-password': 'Wrong email or password.',
    'user-not-found': 'Wrong email or password.',
    'user-disabled': 'This account has been disabled.',
    'email-already-in-use':
        'That email already has an account. Sign in instead.',
    'weak-password': 'Password must be at least 6 characters.',
    'account-exists-with-different-credential':
        'That email is already used with another sign-in method.',
    'operation-not-allowed': 'This sign-in method is not switched on yet.',
    'network-request-failed': 'No internet connection. Please try again.',
    'too-many-requests': 'Too many attempts. Please wait and try again.',
  };

  final CloudSave cloud;

  /// Account id of the signed-in player, or null when signed out.
  String? uid;

  /// Their email address (or name), for display.
  String? name;

  /// True while the save of a player who just signed in is being fetched.
  bool loading = false;

  Future<void>? _googleReady;
  StreamSubscription<User?>? _sub;

  bool get signedIn => uid != null;

  FirebaseAuth get _auth => FirebaseAuth.instance;

  void _listen() {
    // Known right away for a returning player, so the game opens without
    // showing the login screen first.
    _onUser(_auth.currentUser);
    _sub = _auth.authStateChanges().listen(_onUser);
  }

  void _onUser(User? user) =>
      handleUser(user?.uid, name: user?.email ?? user?.displayName);

  @visibleForTesting
  void handleUser(String? newUid, {String? name}) {
    this.name = name;
    if (newUid == uid) return;
    uid = newUid;
    if (newUid == null) {
      loading = false;
      cloud.detach();
    } else {
      loading = true;
      cloud.attach(newUid).timeout(_loadTimeout, onTimeout: () {}).then((_) {
        if (uid != newUid || !loading) return;
        loading = false;
        notifyListeners();
      });
    }
    notifyListeners();
  }

  Future<String?> signInWithGoogle() => _attempt(() async {
        await (_googleReady ??= GoogleSignIn.instance.initialize());
        final account = await GoogleSignIn.instance.authenticate();
        await _auth.signInWithCredential(GoogleAuthProvider.credential(
          idToken: account.authentication.idToken,
        ));
      });

  Future<String?> signInWithEmail(String email, String password) => _attempt(
      () => _auth.signInWithEmailAndPassword(email: email, password: password));

  Future<String?> createAccount(String email, String password) =>
      _attempt(() => _auth.createUserWithEmailAndPassword(
          email: email, password: password));

  Future<String?> sendPasswordReset(String email) =>
      _attempt(() => _auth.sendPasswordResetEmail(email: email));

  Future<void> signOut() async {
    try {
      // Give coins earned a moment ago the chance to reach the server.
      // Offline, they stay queued on the device for this account.
      await cloud
          .flush()
          .timeout(const Duration(seconds: 3), onTimeout: () {});
    } catch (e) {
      debugPrint('Could not confirm the cloud save before signing out: $e');
    }
    await _auth.signOut();
    _onUser(_auth.currentUser);
    try {
      // So "Continue with Google" asks which account to use next time.
      await (_googleReady ??= GoogleSignIn.instance.initialize());
      await GoogleSignIn.instance.signOut();
    } catch (e) {
      debugPrint('Google sign-out failed: $e');
    }
  }

  /// Runs one sign-in step. Returns null if it worked, otherwise a message
  /// to show the player.
  Future<String?> _attempt(Future<void> Function() step) async {
    try {
      await step();
      // Don't wait for the auth stream to move on from the login screen.
      _onUser(_auth.currentUser);
      return null;
    } on GoogleSignInException catch (e) {
      // Note for setup: a missing SHA-1 fingerprint in the Firebase console
      // is also reported as "canceled".
      debugPrint('Google sign-in failed: $e');
      return e.code == GoogleSignInExceptionCode.canceled
          ? 'Google sign-in was cancelled.'
          : 'Google sign-in is not available right now.';
    } on FirebaseAuthException catch (e) {
      debugPrint('Sign-in failed: ${e.code} ${e.message}');
      return _authMessages[e.code] ??
          e.message ??
          'Something went wrong. Please try again.';
    } catch (e) {
      debugPrint('Sign-in failed: $e');
      return 'Something went wrong. Please try again.';
    }
  }

  @override
  void dispose() {
    _sub?.cancel();
    super.dispose();
  }
}
