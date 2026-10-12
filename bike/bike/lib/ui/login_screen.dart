import 'package:flutter/material.dart';

import '../account/account_service.dart';
import 'overlays.dart' show BigButton, GameTitle;

const _bg = Color(0xFF0D1020);
const _card = Color(0xFF171C2B);

/// Where a signed-out player starts: Google, or email and password.
class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key, required this.account});

  final AccountService account;

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final TextEditingController _email = TextEditingController();
  final TextEditingController _password = TextEditingController();

  /// Creating a new account instead of signing in to an existing one.
  bool _signUp = false;
  bool _hidePassword = true;
  bool _busy = false;
  String? _message;
  bool _failed = false;

  AccountService get _account => widget.account;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  void _showError(String message) => setState(() {
        _message = message;
        _failed = true;
      });

  /// Runs a sign-in step. [action] returns null if it worked, otherwise a
  /// message for the player.
  Future<void> _run(Future<String?> Function() action, {String? done}) async {
    FocusScope.of(context).unfocus();
    setState(() {
      _busy = true;
      _message = null;
    });
    final error = await action();
    // After a successful sign-in the game has already replaced this screen.
    if (!mounted) return;
    setState(() {
      _busy = false;
      _message = error ?? done;
      _failed = error != null;
    });
  }

  void _submit() {
    final email = _email.text.trim();
    final password = _password.text;
    if (email.isEmpty || password.isEmpty) {
      return _showError('Enter your email and password.');
    }
    if (_signUp && password.length < 6) {
      return _showError('Password must be at least 6 characters.');
    }
    _run(() => _signUp
        ? _account.createAccount(email, password)
        : _account.signInWithEmail(email, password));
  }

  void _forgotPassword() {
    final email = _email.text.trim();
    if (email.isEmpty) return _showError('Enter your email first.');
    _run(
      () => _account.sendPasswordReset(email),
      done: 'Password reset link sent to $email',
    );
  }

  InputDecoration _decoration(String label, IconData icon) => InputDecoration(
        labelText: label,
        prefixIcon: Icon(icon),
        filled: true,
        fillColor: _card,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(14),
          borderSide: BorderSide.none,
        ),
      );

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: _bg,
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 380),
              child: AutofillGroup(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const GameTitle(),
                    const SizedBox(height: 8),
                    const Text(
                      'Sign in to save your coins and bikes',
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        color: Colors.white70,
                        fontSize: 15,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    const SizedBox(height: 28),
                    _GoogleButton(
                      onTap: _busy
                          ? null
                          : () => _run(_account.signInWithGoogle),
                    ),
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 18),
                      child: Row(
                        children: [
                          Expanded(child: Divider(color: Colors.white24)),
                          Padding(
                            padding: EdgeInsets.symmetric(horizontal: 12),
                            child: Text(
                              'OR',
                              style: TextStyle(
                                color: Colors.white54,
                                fontSize: 12,
                                fontWeight: FontWeight.w800,
                                letterSpacing: 1.5,
                              ),
                            ),
                          ),
                          Expanded(child: Divider(color: Colors.white24)),
                        ],
                      ),
                    ),
                    TextField(
                      controller: _email,
                      enabled: !_busy,
                      keyboardType: TextInputType.emailAddress,
                      textInputAction: TextInputAction.next,
                      autocorrect: false,
                      autofillHints: const [AutofillHints.email],
                      decoration:
                          _decoration('Email', Icons.mail_outline_rounded),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: _password,
                      enabled: !_busy,
                      obscureText: _hidePassword,
                      textInputAction: TextInputAction.done,
                      autofillHints: [
                        _signUp
                            ? AutofillHints.newPassword
                            : AutofillHints.password,
                      ],
                      onSubmitted: (_) => _submit(),
                      decoration: _decoration(
                        'Password',
                        Icons.lock_outline_rounded,
                      ).copyWith(
                        suffixIcon: IconButton(
                          tooltip:
                              _hidePassword ? 'Show password' : 'Hide password',
                          icon: Icon(_hidePassword
                              ? Icons.visibility_rounded
                              : Icons.visibility_off_rounded),
                          onPressed: () =>
                              setState(() => _hidePassword = !_hidePassword),
                        ),
                      ),
                    ),
                    if (_message != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 12),
                        child: Text(
                          _message!,
                          textAlign: TextAlign.center,
                          style: TextStyle(
                            color: _failed
                                ? const Color(0xFFFF8A80)
                                : const Color(0xFF69F0AE),
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                    const SizedBox(height: 18),
                    if (_busy)
                      const SizedBox(
                        height: 60,
                        child: Center(child: CircularProgressIndicator()),
                      )
                    else
                      BigButton(
                        label: _signUp ? 'SIGN UP' : 'SIGN IN',
                        icon: _signUp
                            ? Icons.person_add_alt_1_rounded
                            : Icons.login_rounded,
                        onTap: _submit,
                      ),
                    const SizedBox(height: 8),
                    TextButton(
                      onPressed: _busy
                          ? null
                          : () => setState(() {
                                _signUp = !_signUp;
                                _message = null;
                              }),
                      style:
                          TextButton.styleFrom(foregroundColor: Colors.white70),
                      child: Text(_signUp
                          ? 'Already have an account? Sign in'
                          : 'New rider? Create an account'),
                    ),
                    if (!_signUp)
                      TextButton(
                        onPressed: _busy ? null : _forgotPassword,
                        style: TextButton.styleFrom(
                            foregroundColor: Colors.white54),
                        child: const Text('Forgot password?'),
                      ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _GoogleButton extends StatelessWidget {
  const _GoogleButton({required this.onTap});

  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: double.infinity,
      height: 54,
      child: FilledButton(
        onPressed: onTap,
        style: FilledButton.styleFrom(
          backgroundColor: Colors.white,
          foregroundColor: Colors.black87,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(16),
          ),
        ),
        child: const Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text(
              'G',
              style: TextStyle(
                color: Color(0xFF4285F4),
                fontSize: 24,
                fontWeight: FontWeight.w900,
              ),
            ),
            SizedBox(width: 12),
            Flexible(
              child: Text(
                'Continue with Google',
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontSize: 16, fontWeight: FontWeight.w800),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Shown right after signing in, while the player's save is fetched.
class LoadingSaveScreen extends StatelessWidget {
  const LoadingSaveScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      backgroundColor: _bg,
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            CircularProgressIndicator(),
            SizedBox(height: 20),
            Text(
              'Loading your saved game…',
              style: TextStyle(
                color: Colors.white70,
                fontSize: 15,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
