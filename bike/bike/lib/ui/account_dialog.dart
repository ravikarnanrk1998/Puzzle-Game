import 'package:flutter/material.dart';

import '../account/account_service.dart';

const _danger = Color(0xFFFF5252);

enum _Choice { logOut, delete }

/// The popup behind the account button on the menu: log out, or delete the
/// account.
Future<void> showAccountDialog(
    BuildContext context, AccountService account) async {
  final choice = await showDialog<_Choice>(
    context: context,
    builder: (context) => AlertDialog(
      title: Text(
        account.name ?? 'Account',
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w800),
      ),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          FilledButton.icon(
            onPressed: () => Navigator.of(context).pop(_Choice.logOut),
            icon: const Icon(Icons.logout_rounded),
            label: const Text('LOG OUT'),
          ),
          const SizedBox(height: 10),
          OutlinedButton.icon(
            onPressed: () => Navigator.of(context).pop(_Choice.delete),
            style: OutlinedButton.styleFrom(
              foregroundColor: _danger,
              side: const BorderSide(color: _danger),
            ),
            icon: const Icon(Icons.delete_forever_rounded),
            label: const Text('DELETE ACCOUNT'),
          ),
        ],
      ),
    ),
  );
  if (choice == _Choice.logOut) {
    await account.signOut();
  } else if (choice == _Choice.delete && context.mounted) {
    await showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (_) => _DeleteAccountDialog(account: account),
    );
  }
}

/// Asks before deleting, then deletes the account and everything saved
/// with it.
class _DeleteAccountDialog extends StatefulWidget {
  const _DeleteAccountDialog({required this.account});

  final AccountService account;

  @override
  State<_DeleteAccountDialog> createState() => _DeleteAccountDialogState();
}

class _DeleteAccountDialogState extends State<_DeleteAccountDialog> {
  final TextEditingController _password = TextEditingController();
  bool _busy = false;
  String? _error;

  AccountService get _account => widget.account;

  @override
  void dispose() {
    _password.dispose();
    super.dispose();
  }

  Future<void> _delete() async {
    if (_account.usesPassword && _password.text.isEmpty) {
      setState(() => _error = 'Enter your password to confirm.');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    final messenger = ScaffoldMessenger.of(context);
    final error = await _account.deleteAccount(password: _password.text);
    if (!mounted) return;
    if (error != null) {
      setState(() {
        _busy = false;
        _error = error;
      });
      return;
    }
    // The game has gone back to the login screen behind this dialog.
    Navigator.of(context).pop();
    messenger.showSnackBar(const SnackBar(
      content: Text('Your account and all its data have been deleted.'),
      behavior: SnackBarBehavior.floating,
    ));
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: !_busy,
      child: AlertDialog(
        scrollable: true,
        title: const Text('Delete account?'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'This deletes ${_account.name ?? 'your account'} and all of '
              'its data for good: coins, bikes, upgrades and best score.\n\n'
              'This cannot be undone.',
            ),
            const SizedBox(height: 14),
            if (_account.usesPassword)
              TextField(
                controller: _password,
                enabled: !_busy,
                obscureText: true,
                decoration: const InputDecoration(labelText: 'Password'),
                onSubmitted: (_) => _delete(),
              )
            else
              const Text(
                'You will be asked to pick your Google account to confirm.',
                style: TextStyle(color: Colors.white60, fontSize: 13),
              ),
            if (_error != null)
              Padding(
                padding: const EdgeInsets.only(top: 12),
                child: Text(
                  _error!,
                  style: const TextStyle(
                      color: _danger, fontWeight: FontWeight.w600),
                ),
              ),
          ],
        ),
        actions: _busy
            ? const [
                Padding(
                  padding: EdgeInsets.all(8),
                  child: SizedBox(
                    width: 28,
                    height: 28,
                    child: CircularProgressIndicator(strokeWidth: 3),
                  ),
                ),
              ]
            : [
                TextButton(
                  onPressed: () => Navigator.of(context).pop(),
                  child: const Text('CANCEL'),
                ),
                FilledButton(
                  onPressed: _delete,
                  style: FilledButton.styleFrom(
                    backgroundColor: _danger,
                    foregroundColor: Colors.white,
                  ),
                  child: const Text('DELETE'),
                ),
              ],
      ),
    );
  }
}
