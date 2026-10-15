import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../shop/catalog.dart';
import '../shop/player_progress.dart';
import '../versus/match_backend.dart';
import '../versus/versus_match.dart';
import 'overlays.dart' show BigButton, DiamondChip;

const _bg = Color(0xFF0D1020);
const _card = Color(0xFF171C2B);
const _gold = Color(0xFFFFC107);
const _diamond = Color(0xFF40C4FF);

/// Where a 1 vs 1 match is set up: create a code and wait for a friend, or
/// enter a friend's code. Pops with the [VersusMatch] once both are in.
///
/// Creating a code costs [Catalog.versusCost] diamonds. Joining is free.
class VersusLobbyScreen extends StatefulWidget {
  const VersusLobbyScreen({
    super.key,
    required this.matches,
    required this.progress,
    required this.uid,
    required this.name,
    required this.onGetDiamonds,
  });

  final MatchBackend matches;
  final PlayerProgress progress;

  /// The signed-in player, and the name the other player will see.
  final String uid;
  final String name;

  /// Opens the shop for a player who has run out of diamonds.
  final VoidCallback onGetDiamonds;

  @override
  State<VersusLobbyScreen> createState() => _VersusLobbyScreenState();
}

class _VersusLobbyScreenState extends State<VersusLobbyScreen> {
  final TextEditingController _code = TextEditingController();

  /// The match being set up. Once both players are in it is handed to the
  /// game, and is no longer this screen's to close.
  VersusMatch? _match;
  bool _busy = false;
  String? _error;

  /// True while this player has paid for a code nobody has used yet. They
  /// get the diamond back if they stop waiting.
  bool _paid = false;

  /// Waiting for a friend to enter the code this player created.
  bool get _waiting => _match != null && _match!.isHost;

  bool get _canPay => widget.progress.diamonds >= Catalog.versusCost;

  @override
  void dispose() {
    _code.dispose();
    // Leaving the screen has already gone through _dropMatch, which is
    // where an unused code is paid back.
    _match
      ?..removeListener(_onMatchChanged)
      ..leave()
      ..dispose();
    super.dispose();
  }

  /// Stops waiting, and gives back the diamond for a code nobody used.
  void _dropMatch() {
    _match
      ?..removeListener(_onMatchChanged)
      ..leave()
      ..dispose();
    _match = null;
    _refund();
  }

  void _refund() {
    if (_paid) widget.progress.addDiamonds(Catalog.versusCost);
    _paid = false;
  }

  void _onMatchChanged() {
    final match = _match;
    if (match == null || !match.ready || !mounted) return;
    match.removeListener(_onMatchChanged);
    _match = null;
    // The friend is in, so the diamond is spent.
    _paid = false;
    Navigator.of(context).pop(match);
  }

  /// Runs [open], which creates or joins a match, then waits for both
  /// players to be in.
  Future<void> _open(Future<VersusMatch> Function() open) async {
    FocusScope.of(context).unfocus();
    setState(() {
      _busy = true;
      _error = null;
    });
    VersusMatch? match;
    String? error;
    try {
      match = await open();
    } on MatchError catch (e) {
      error = e.message;
    } catch (e) {
      debugPrint('Could not open the match: $e');
      error = 'Could not connect. Check your internet and try again.';
    }
    if (!mounted) {
      match
        ?..leave()
        ..dispose();
      _refund();
      return;
    }
    setState(() {
      // Someone joining stays busy for the moment it takes to load the match.
      _busy = match != null && !match.isHost;
      _error = error;
      _match = match;
    });
    match?.addListener(_onMatchChanged);
    _onMatchChanged();
  }

  void _create() {
    if (!_canPay) {
      setState(() => _error =
          'You need ${Catalog.versusCost} diamond to create a code. Claim '
          'your free daily diamond on the menu, or get more in the garage.');
      return;
    }
    _open(() async {
      final match = await VersusMatch.create(widget.matches,
          uid: widget.uid, name: widget.name);
      // Paid only once the code really exists.
      _paid = widget.progress.spendDiamonds(Catalog.versusCost);
      return match;
    });
  }

  void _join() {
    final code = _code.text.trim();
    if (code.length != 6) {
      setState(() => _error = 'Enter the 6-digit code from your friend.');
      return;
    }
    _open(() => VersusMatch.join(widget.matches, code,
        uid: widget.uid, name: widget.name));
  }

  void _copyCode() {
    Clipboard.setData(ClipboardData(text: _match!.code));
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(const SnackBar(
        content: Text('Code copied'),
        behavior: SnackBarBehavior.floating,
      ));
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      onPopInvokedWithResult: (didPop, _) {
        if (didPop) _dropMatch();
      },
      child: ListenableBuilder(
        listenable: widget.progress,
        builder: (context, _) => Scaffold(
          backgroundColor: _bg,
          appBar: AppBar(
            backgroundColor: const Color(0xFF141824),
            foregroundColor: Colors.white,
            title: const Text(
              '1 VS 1',
              style: TextStyle(
                fontWeight: FontWeight.w900,
                fontStyle: FontStyle.italic,
              ),
            ),
            actions: [
              Padding(
                padding: const EdgeInsets.only(right: 12),
                child: Center(
                  child: DiamondChip(count: widget.progress.diamonds),
                ),
              ),
            ],
          ),
          body: SafeArea(
            child: Center(
              child: SingleChildScrollView(
                padding: const EdgeInsets.all(24),
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 380),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: _waiting ? _waitingForFriend() : _chooser(),
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }

  List<Widget> _waitingForFriend() {
    return [
      const Text(
        'YOUR CODE',
        style: TextStyle(
          color: Colors.white60,
          fontSize: 13,
          fontWeight: FontWeight.w800,
          letterSpacing: 2,
        ),
      ),
      const SizedBox(height: 8),
      Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          // Keeps the code centred against the copy button on its right.
          const SizedBox(width: 48),
          Flexible(
            child: FittedBox(
              fit: BoxFit.scaleDown,
              child: Text(
                _match!.code,
                style: const TextStyle(
                  color: _gold,
                  fontSize: 46,
                  fontWeight: FontWeight.w900,
                  letterSpacing: 6,
                ),
              ),
            ),
          ),
          IconButton(
            tooltip: 'Copy code',
            onPressed: _copyCode,
            icon: const Icon(Icons.copy_rounded, color: Colors.white70),
          ),
        ],
      ),
      const SizedBox(height: 12),
      const Text(
        'Send this code to your friend.\n'
        'The race starts as soon as they enter it.',
        textAlign: TextAlign.center,
        style: TextStyle(color: Colors.white70, fontSize: 15),
      ),
      const SizedBox(height: 8),
      const Text(
        'If you cancel before they join, you get your diamond back.',
        textAlign: TextAlign.center,
        style: TextStyle(color: Colors.white38, fontSize: 12),
      ),
      const SizedBox(height: 28),
      const CircularProgressIndicator(),
      const SizedBox(height: 12),
      const Text(
        'Waiting for your friend…',
        style: TextStyle(color: Colors.white54),
      ),
      const SizedBox(height: 20),
      TextButton(
        onPressed: () => setState(_dropMatch),
        style: TextButton.styleFrom(foregroundColor: Colors.white70),
        child: const Text('CANCEL'),
      ),
    ];
  }

  List<Widget> _chooser() {
    return [
      const Icon(Icons.sports_score_rounded, color: Colors.white, size: 56),
      const SizedBox(height: 8),
      const Text(
        'Race a friend at the same time.\nThe higher score wins.',
        textAlign: TextAlign.center,
        style: TextStyle(
          color: Colors.white70,
          fontSize: 15,
          fontWeight: FontWeight.w600,
        ),
      ),
      const SizedBox(height: 28),
      if (_busy)
        const SizedBox(
          height: 60,
          child: Center(child: CircularProgressIndicator()),
        )
      else
        BigButton(
          label: 'CREATE CODE',
          icon: Icons.add_rounded,
          onTap: _create,
        ),
      const SizedBox(height: 10),
      const FittedBox(
        fit: BoxFit.scaleDown,
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              'Costs ${Catalog.versusCost}',
              style: TextStyle(
                color: Colors.white70,
                fontSize: 13,
                fontWeight: FontWeight.w700,
              ),
            ),
            SizedBox(width: 4),
            Icon(Icons.diamond, color: _diamond, size: 16),
            Text(
              '  •  Joining is free',
              style: TextStyle(color: Colors.white54, fontSize: 13),
            ),
          ],
        ),
      ),
      if (!_canPay)
        TextButton.icon(
          onPressed: widget.onGetDiamonds,
          icon: const Icon(Icons.diamond, size: 18),
          label: const Text('GET DIAMONDS'),
          style: TextButton.styleFrom(foregroundColor: _diamond),
        ),
      const Padding(
        padding: EdgeInsets.symmetric(vertical: 22),
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
        controller: _code,
        enabled: !_busy,
        keyboardType: TextInputType.number,
        textInputAction: TextInputAction.done,
        textAlign: TextAlign.center,
        inputFormatters: [
          FilteringTextInputFormatter.digitsOnly,
          LengthLimitingTextInputFormatter(6),
        ],
        style: const TextStyle(
          fontSize: 28,
          fontWeight: FontWeight.w900,
          letterSpacing: 6,
        ),
        decoration: InputDecoration(
          hintText: 'ENTER CODE',
          hintStyle: const TextStyle(
            color: Colors.white30,
            fontSize: 18,
            letterSpacing: 2,
          ),
          filled: true,
          fillColor: _card,
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(14),
            borderSide: BorderSide.none,
          ),
        ),
        onSubmitted: (_) => _join(),
      ),
      const SizedBox(height: 16),
      if (!_busy)
        BigButton(
          label: 'JOIN',
          icon: Icons.login_rounded,
          onTap: _join,
          colors: const [Color(0xFF7C4DFF), Color(0xFF304FFE)],
        ),
      if (_error != null)
        Padding(
          padding: const EdgeInsets.only(top: 16),
          child: Text(
            _error!,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: Color(0xFFFF8A80),
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
    ];
  }
}
