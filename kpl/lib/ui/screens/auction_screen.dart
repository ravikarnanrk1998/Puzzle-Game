import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../core/league.dart';
import '../../core/theme.dart';
import '../../data/teams.dart';
import '../../engine/auction.dart';
import '../../engine/career.dart';
import '../../models/franchise.dart';
import '../../models/player.dart';
import '../widgets/common.dart';
import '../widgets/crest.dart';
import '../widgets/effects.dart';
import '../widgets/player_card.dart';
import 'auction_summary_screen.dart';

enum _Phase { intro, bidding, going1, going2, result }

class AuctionScreen extends StatefulWidget {
  const AuctionScreen({super.key});

  @override
  State<AuctionScreen> createState() => _AuctionScreenState();
}

class _AuctionScreenState extends State<AuctionScreen> with TickerProviderStateMixin {
  final Career c = Career.instance;
  AuctionEngine get a => c.auction!;

  _Phase _phase = _Phase.intro;
  Timer? _timer;
  int _idle = 0;
  int _confetti = 0;
  bool _busy = false;
  final List<BidEvent> _feed = [];

  late final AnimationController _cardIn = AnimationController(vsync: this, duration: const Duration(milliseconds: 650));
  late final AnimationController _pulse = AnimationController(vsync: this, duration: const Duration(milliseconds: 420));
  late final AnimationController _stamp = AnimationController(vsync: this, duration: const Duration(milliseconds: 520));
  late final AnimationController _sheen = AnimationController(vsync: this, duration: const Duration(milliseconds: 3400))..repeat();
  late final AnimationController _stage = AnimationController(vsync: this, duration: const Duration(seconds: 8))..repeat();

  int get _speed => c.settings.auctionSpeed;
  bool get _userCanBid => a.lot != null && a.bidBlocker(a.user) == null;

  Duration get _tickDur {
    final fast = a.userPassed || (!_userCanBid && a.leader != a.userId);
    return Duration(milliseconds: ((fast ? 280 : 780) / _speed).round());
  }

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _begin());
  }

  @override
  void dispose() {
    _timer?.cancel();
    _cardIn.dispose();
    _pulse.dispose();
    _stamp.dispose();
    _sheen.dispose();
    _stage.dispose();
    super.dispose();
  }

  void _begin() {
    if (!mounted) return;
    if (a.finished) {
      _finish();
      return;
    }
    if (a.lot!.status != LotStatus.open) a.openLot();
    _startLot();
  }

  void _startLot() {
    _phase = _Phase.intro;
    _idle = 0;
    _feed.clear();
    _cardIn.forward(from: 0);
    setState(() {});
    _schedule();
  }

  void _schedule([Duration? d]) {
    _timer?.cancel();
    _timer = Timer(d ?? _tickDur, _onTick);
  }

  void _onTick() {
    if (!mounted || _busy || a.lot == null) return;
    switch (_phase) {
      case _Phase.intro:
        _phase = _Phase.bidding;
        _idle = 0;
      case _Phase.bidding:
      case _Phase.going1:
      case _Phase.going2:
        final id = a.aiTick();
        if (id != null) {
          _onBid(id);
        } else {
          _idle++;
          final grace = (!a.userPassed && _userCanBid) ? 2 : 1;
          if (_phase == _Phase.bidding && _idle >= grace) {
            _phase = _Phase.going1;
            _idle = 0;
          } else if (_phase == _Phase.going1 && _idle >= 1) {
            _phase = _Phase.going2;
            _idle = 0;
          } else if (_phase == _Phase.going2 && _idle >= 1) {
            _hammer();
            return;
          }
        }
      case _Phase.result:
        _next();
        return;
    }
    setState(() {});
    _schedule();
  }

  void _onBid(String teamId) {
    _phase = _Phase.bidding;
    _idle = 0;
    _feed.insert(0, a.bids.last);
    if (_feed.length > 5) _feed.removeLast();
    _pulse.forward(from: 0);
    if (teamId != a.userId) haptic(Buzz.light);
  }

  void _userBid() {
    if (_phase == _Phase.result || _busy || a.lot == null) return;
    if (a.userBid()) {
      _onBid(a.userId);
      haptic(Buzz.medium);
      setState(() {});
      _schedule(Duration(milliseconds: (900 / _speed).round()));
    } else {
      final why = a.bidBlocker(a.user);
      if (why != null) showToast(context, why);
    }
  }

  void _pass() {
    a.userPassed = true;
    setState(() {});
    _schedule(const Duration(milliseconds: 120));
  }

  Future<void> _hammer() async {
    _timer?.cancel();
    final waiting = a.hammer();
    if (waiting) {
      _busy = true;
      await _rtmFlow();
      _busy = false;
    }
    _showResult();
  }

  void _showResult() {
    if (!mounted) return;
    _phase = _Phase.result;
    final l = a.lastResult;
    if (l?.status == LotStatus.sold) {
      _confetti++;
      haptic(l!.soldTo == a.userId ? Buzz.heavy : Buzz.medium);
    }
    _stamp.forward(from: 0);
    setState(() {});
    _schedule(Duration(milliseconds: (1800 / _speed).round().clamp(600, 1800)));
  }

  void _next() {
    if (!mounted) return;
    a.advance();
    c.save();
    if (a.finished) {
      _finish();
      return;
    }
    a.openLot();
    _startLot();
  }

  void _finish() {
    _timer?.cancel();
    c.finishAuction();
    Navigator.of(context).pushReplacement(fadeRoute(const AuctionSummaryScreen()));
  }

  // ------------------------------------------------------------ RTM

  Future<bool?> _ask(String title, String body, String yes, String no, Color color) => showDialog<bool>(
        context: context,
        barrierDismissible: false,
        builder: (ctx) => AlertDialog(
          backgroundColor: AppColors.bg1,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20), side: BorderSide(color: color.o(0.6))),
          title: Text(title, style: T.display(28, color: color, weight: FontWeight.w700)),
          content: Text(body, style: T.ui(15, color: AppColors.textDim, height: 1.3)),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx, false), child: Text(no, style: T.label(13))),
            FilledButton(
              style: FilledButton.styleFrom(backgroundColor: color),
              onPressed: () => Navigator.pop(ctx, true),
              child: Text(yes, style: T.label(13, color: color.onColor)),
            ),
          ],
        ),
      );

  Future<void> _rtmFlow() async {
    final p = a.lot!.player;
    if (a.rtmStep == RtmStep.userDecide) {
      final leader = teamById(a.leader!);
      final use = await _ask(
        'RIGHT TO MATCH',
        '${p.name} has been sold to ${leader.name} for ${money(a.currentBid)}.\n\nUse one of your ${a.user.rtmCards} RTM cards to bring him back?',
        'USE RTM',
        'DECLINE',
        AppColors.gold,
      );
      a.userUseRtm(use == true);
      if (a.rtmStep == RtmStep.userMatch && mounted) {
        final match = await _ask(
          'FINAL RAISE',
          '${leader.name} have made a final raise to ${money(a.rtmRaisedTo)}.\n\nMatch it to sign ${p.name}?',
          'MATCH ${money(a.rtmRaisedTo)}',
          'LET HIM GO',
          AppColors.gold,
        );
        a.userMatchRaise(match == true);
      }
    } else if (a.rtmStep == RtmStep.userRaise) {
      final holder = teamById(a.rtmTeam!);
      final maxP = a.maxAllowed(a.user);
      final options = <int>{a.currentBid};
      for (final add in const [50, 100, 200, 300, 500]) {
        if (a.currentBid + add <= maxP) options.add(a.currentBid + add);
      }
      final price = await showDialog<int>(
        context: context,
        barrierDismissible: false,
        builder: (ctx) => AlertDialog(
          backgroundColor: AppColors.bg1,
          title: Text('RTM BY ${holder.id}!', style: T.display(28, color: holder.glow, weight: FontWeight.w700)),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                '${holder.name} want to match your ${money(a.currentBid)} bid for ${p.name}. Make one final raise - if they won\'t match it, he is yours.',
                style: T.ui(14.5, color: AppColors.textDim, height: 1.3),
              ),
              const SizedBox(height: 12),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  for (final o in options)
                    ActionChip(
                      backgroundColor: o == a.currentBid ? AppColors.bg3 : AppColors.gold.o(0.2),
                      label: Text(o == a.currentBid ? 'KEEP ${money(o)}' : money(o), style: T.ui(14, weight: FontWeight.w700)),
                      onPressed: () => Navigator.pop(ctx, o),
                    ),
                ],
              ),
            ],
          ),
        ),
      );
      a.userFinalRaise(price ?? a.currentBid);
    }
  }

  // ---------------------------------------------------------- sims

  Future<void> _simSet() async {
    final code = a.lot?.setCode;
    if (code == null || _busy) return;
    _timer?.cancel();
    _busy = true;
    a.aiManagesUser = true;
    final results = <AuctionLot>[];
    if (_phase != _Phase.result) {
      a.resolveInstantly();
      results.add(a.lastResult!);
    }
    a.advance();
    var guard = 0;
    while (!a.finished && a.lot!.setCode == code && guard++ < 400) {
      a.openLot();
      a.resolveInstantly();
      results.add(a.lastResult!);
      a.advance();
    }
    a.aiManagesUser = false;
    c.save();
    _busy = false;
    if (!mounted) return;
    await _showResultsSheet('SET RESULTS • AI MANAGER BID FOR YOU', results);
    if (!mounted) return;
    if (a.finished) {
      _finish();
      return;
    }
    a.openLot();
    _startLot();
  }

  Future<void> _simAll() async {
    final choice = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        backgroundColor: AppColors.bg1,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20), side: BorderSide(color: AppColors.gold.o(0.6))),
        title: Text('SIMULATE AUCTION?', style: T.display(28, color: AppColors.gold, weight: FontWeight.w700)),
        content: Text(
          'Every remaining player will be auctioned instantly.\n\nAI MANAGER bids for ${c.userTeam.id} using squad needs and your purse. '
          'WITHOUT ME skips your bids - your squad is completed at base prices.',
          style: T.ui(14.5, color: AppColors.textDim, height: 1.3),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: Text('CANCEL', style: T.label(12))),
          TextButton(onPressed: () => Navigator.pop(ctx, 'solo'), child: Text('WITHOUT ME', style: T.label(12, color: AppColors.textDim))),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: AppColors.gold),
            onPressed: () => Navigator.pop(ctx, 'ai'),
            child: Text('AI MANAGER', style: T.label(12, color: const Color(0xFF1A1205))),
          ),
        ],
      ),
    );
    if (choice == null || !mounted) return;
    _timer?.cancel();
    _busy = true;
    a.aiManagesUser = choice == 'ai';
    if (_phase != _Phase.result) a.resolveInstantly();
    a.advance();
    var guard = 0;
    while (!a.finished && guard++ < 2000) {
      a.openLot();
      a.resolveInstantly();
      a.advance();
    }
    a.aiManagesUser = false;
    _busy = false;
    _finish();
  }

  Future<void> _showResultsSheet(String title, List<AuctionLot> lots) => showDialog<void>(
        context: context,
        builder: (ctx) => Dialog(
          backgroundColor: AppColors.bg1,
          insetPadding: const EdgeInsets.symmetric(horizontal: 40, vertical: 16),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20), side: BorderSide(color: AppColors.gold.o(0.4))),
          child: SizedBox(
            width: 720,
            height: MediaQuery.of(ctx).size.height * 0.9,
            child: Column(
              children: [
                Padding(padding: const EdgeInsets.fromLTRB(12, 12, 12, 8), child: Text(title, style: T.display(24, weight: FontWeight.w700))),
                Expanded(
                  child: GridView.count(
                    crossAxisCount: 2,
                    childAspectRatio: 7.5,
                    crossAxisSpacing: 14,
                    padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
                    children: [for (final l in lots) _lotRow(l)],
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
                  child: SizedBox(width: 280, child: NeonButton(label: 'CONTINUE', height: 44, onTap: () => Navigator.pop(ctx))),
                ),
              ],
            ),
          ),
        ),
      );

  Widget _lotRow(AuctionLot l) {
    final sold = l.status == LotStatus.sold;
    final t = sold ? teamById(l.soldTo!) : null;
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        children: [
          RatingBadge(l.player.overall, size: 30),
          const SizedBox(width: 10),
          Expanded(child: Text(l.player.name, style: T.ui(14.5, weight: FontWeight.w700))),
          if (t != null) ...[
            TeamCrest(t, size: 22),
            const SizedBox(width: 6),
            Text(money(l.price), style: T.display(18, color: AppColors.gold)),
            if (l.viaRtm) ...[const SizedBox(width: 4), Pill('RTM', color: AppColors.green, fontSize: 9)],
          ] else
            Text('UNSOLD', style: T.label(11, color: AppColors.textMute)),
        ],
      ),
    );
  }

  void _openSquads() {
    showDialog<void>(
      context: context,
      builder: (ctx) => Dialog(
        backgroundColor: AppColors.bg1,
        insetPadding: const EdgeInsets.symmetric(horizontal: 30, vertical: 14),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20), side: BorderSide(color: AppColors.stroke)),
        child: SizedBox(width: 820, child: _SquadsSheet(engine: a)),
      ),
    );
  }

  // ----------------------------------------------------------- build

  @override
  Widget build(BuildContext context) {
    final lot = a.lot;
    if (lot == null) {
      return const Scaffold(backgroundColor: AppColors.bg0, body: Center(child: CircularProgressIndicator()));
    }
    final p = lot.player;
    final leaderTeam = a.leader == null ? null : teamById(a.leader!);
    final accent = leaderTeam?.glow ?? AppColors.gold;
    final result = _phase == _Phase.result ? a.lastResult : null;
    final soldTeam = result?.status == LotStatus.sold ? teamById(result!.soldTo!) : null;
    return Scaffold(
      backgroundColor: AppColors.bg0,
      body: Stack(
        children: [
          Positioned.fill(child: CustomPaint(painter: _StagePainter(_stage, accent))),
          SafeArea(
            child: Column(
              children: [
                _header(lot),
                Expanded(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(10, 0, 10, 8),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        SizedBox(width: 168, child: _teamStrip()),
                        const SizedBox(width: 8),
                        Expanded(
                          child: LayoutBuilder(builder: (context, box) {
                            final cardW = math.min(box.maxWidth * 0.9, (box.maxHeight - 34) / 1.36);
                            return Column(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                AnimatedBuilder(
                                  animation: _cardIn,
                                  builder: (context, child) {
                                    final t = Curves.easeOutBack.transform(_cardIn.value);
                                    return Opacity(
                                      opacity: _cardIn.value.clamp(0.0, 1.0),
                                      child: Transform.translate(
                                        offset: Offset(0, (1 - t) * 60),
                                        child: Transform.scale(scale: 0.85 + 0.15 * t, child: child),
                                      ),
                                    );
                                  },
                                  child: PlayerCard(player: p, width: cardW, sheen: _sheen),
                                ),
                                const SizedBox(height: 6),
                                Wrap(
                                  alignment: WrapAlignment.center,
                                  spacing: 6,
                                  runSpacing: 4,
                                  children: [
                                    Pill('BASE ${money(lot.base)}', color: AppColors.gold, fontSize: 10.5),
                                    if (p.team26 != null)
                                      Pill('2026: ${p.team26} ${money(p.price26)}', color: teamById(p.team26!).glow, fontSize: 10.5)
                                    else
                                      Pill('NEW TO THE POOL', color: AppColors.cyan, fontSize: 10.5),
                                    if (a.mega && a.rtmTeamOf[p.id] != null && franchiseRtm(a.rtmTeamOf[p.id]!) > 0)
                                      Pill('RTM: ${a.rtmTeamOf[p.id]}', color: AppColors.green, fontSize: 10.5),
                                  ],
                                ),
                              ],
                            );
                          }),
                        ),
                        const SizedBox(width: 8),
                        SizedBox(
                          width: 340,
                          child: Column(
                            children: [
                              _bidPanel(lot, leaderTeam),
                              const Spacer(),
                              _controls(lot),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
          if (result != null) Positioned.fill(child: IgnorePointer(child: _stampOverlay(result, soldTeam))),
          Positioned.fill(
            child: ConfettiBurst(
              trigger: _confetti,
              colors: soldTeam == null ? const [AppColors.gold, Colors.white] : [soldTeam.primary, soldTeam.glow, soldTeam.secondary, Colors.white, AppColors.gold],
              origin: const Alignment(0, -0.1),
            ),
          ),
        ],
      ),
    );
  }

  int franchiseRtm(String id) => a.franchises[id]?.rtmCards ?? 0;

  Widget _header(AuctionLot lot) {
    final total = a.lots.length;
    return Padding(
      padding: const EdgeInsets.fromLTRB(12, 6, 12, 4),
      child: Row(
        children: [
          CircleIconButton(icon: Icons.close_rounded, size: 38, onTap: () => Navigator.pop(context)),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(lot.setName.toUpperCase(), maxLines: 1, overflow: TextOverflow.ellipsis, style: T.display(22, weight: FontWeight.w700, height: 1)),
                Text(
                  '${a.mega ? 'MEGA' : 'MINI'} AUCTION ${c.year} • LOT ${a.index + 1}/$total • SOLD ${a.soldCount}',
                  style: T.label(10, color: AppColors.gold),
                ),
              ],
            ),
          ),
          PressScale(
            onTap: () {
              final s = c.settings;
              s.auctionSpeed = s.auctionSpeed == 1 ? 2 : (s.auctionSpeed == 2 ? 4 : 1);
              c.saveSettings();
              setState(() {});
            },
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
              decoration: BoxDecoration(borderRadius: BorderRadius.circular(20), border: Border.all(color: AppColors.stroke), color: Colors.white.o(0.06)),
              child: Text('$_speed×', style: T.display(18, weight: FontWeight.w700)),
            ),
          ),
          const SizedBox(width: 6),
          CircleIconButton(icon: Icons.groups_rounded, size: 38, onTap: _openSquads),
        ],
      ),
    );
  }

  Widget _teamStrip() {
    final teams = a.franchises.values.toList();
    return Column(
      children: [
        for (final f in teams)
          Expanded(
            child: Builder(builder: (context) {
              final lead = a.leader == f.id;
              final me = f.id == a.userId;
              final out = a.lot != null && a.bidBlocker(f) != null && !lead;
              return AnimatedContainer(
                duration: const Duration(milliseconds: 220),
                margin: const EdgeInsets.symmetric(vertical: 2),
                padding: const EdgeInsets.symmetric(horizontal: 6),
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(10),
                  gradient: LinearGradient(colors: [f.info.primary.o(lead ? 0.85 : 0.35), Colors.black.o(lead ? 0.3 : 0.45)]),
                  border: Border.all(color: lead ? f.info.glow : (me ? AppColors.gold.o(0.8) : AppColors.strokeSoft), width: lead ? 2 : 1),
                  boxShadow: lead ? [BoxShadow(color: f.info.glow.o(0.6), blurRadius: 14)] : null,
                ),
                child: Opacity(
                  opacity: out ? 0.5 : 1,
                  child: Row(
                    children: [
                      TeamCrest(f.info, size: 22),
                      const SizedBox(width: 6),
                      SizedBox(width: 40, child: Text(f.id, style: T.display(16, weight: FontWeight.w700, height: 1))),
                      Expanded(child: Text(crore(f.purse), textAlign: TextAlign.right, style: T.display(15, color: AppColors.goldLight, weight: FontWeight.w700, height: 1))),
                      const SizedBox(width: 6),
                      SizedBox(width: 30, child: Text('${f.size}/${League.maxSquad}', textAlign: TextAlign.right, style: T.ui(10, color: AppColors.textDim))),
                    ],
                  ),
                ),
              );
            }),
          ),
      ],
    );
  }

  Widget _bidPanel(AuctionLot lot, TeamInfo? leader) {
    final label = switch (_phase) {
      _Phase.intro => 'UP NEXT',
      _Phase.bidding => a.leader == null ? 'AWAITING OPENING BID' : 'CURRENT BID',
      _Phase.going1 => 'GOING ONCE…',
      _Phase.going2 => 'GOING TWICE…',
      _Phase.result => a.lastResult?.status == LotStatus.sold ? 'SOLD!' : 'UNSOLD',
    };
    final progress = switch (_phase) {
      _Phase.intro => 1.0,
      _Phase.bidding => 1.0,
      _Phase.going1 => 0.62,
      _Phase.going2 => 0.3,
      _Phase.result => 0.0,
    };
    final amount = a.currentBid == 0 ? lot.base : a.currentBid;
    final accent = leader?.glow ?? AppColors.gold;
    return Padding(
      padding: const EdgeInsets.only(top: 2),
      child: GlassCard(
        blur: true,
        padding: const EdgeInsets.fromLTRB(12, 10, 12, 10),
        borderColor: accent.o(0.6),
        glow: leader?.glow,
        child: Column(
          children: [
            Row(
              children: [
                AnimatedBuilder(
                  animation: _pulse,
                  builder: (context, child) => Transform.scale(scale: 1 + 0.18 * math.sin(_pulse.value * math.pi), child: child),
                  child: leader != null
                      ? TeamCrest(leader, size: 50, glow: true)
                      : Container(
                          width: 50,
                          height: 56,
                          alignment: Alignment.center,
                          decoration: BoxDecoration(borderRadius: BorderRadius.circular(14), color: Colors.white.o(0.06)),
                          child: const Icon(Icons.gavel_rounded, color: AppColors.gold, size: 30),
                        ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(label, style: T.label(11, color: _phase == _Phase.going2 ? AppColors.red : accent)),
                      CountUp(
                        value: amount.toDouble(),
                        duration: const Duration(milliseconds: 380),
                        builder: (_, v) => GradientText(
                          money(_snap(v.round(), amount)),
                          style: T.display(44, weight: FontWeight.w700, height: 1),
                        ),
                      ),
                      Text(
                        leader == null ? 'Base price' : leader.name,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: T.ui(13, color: AppColors.textDim),
                      ),
                    ],
                  ),
                ),
                SizedBox(
                  width: 58,
                  height: 58,
                  child: TweenAnimationBuilder<double>(
                    tween: Tween(end: progress),
                    duration: const Duration(milliseconds: 380),
                    builder: (context, v, _) => CustomPaint(painter: _RingPainter(v, _phase == _Phase.going2 ? AppColors.red : accent)),
                  ),
                ),
              ],
            ),
            if (_feed.isNotEmpty) ...[
              const SizedBox(height: 8),
              SizedBox(
                height: 24,
                child: ListView(
                  scrollDirection: Axis.horizontal,
                  children: [
                    for (var i = 0; i < _feed.length; i++)
                      Padding(
                        padding: const EdgeInsets.only(right: 6),
                        child: Opacity(
                          opacity: 1 - i * 0.16,
                          child: Pill('${_feed[i].teamId} ${money(_feed[i].amount)}', color: teamById(_feed[i].teamId).glow, fontSize: 11),
                        ),
                      ),
                  ],
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  int _snap(int animated, int target) => (animated - target).abs() < 3 ? target : (animated ~/ 5) * 5;

  Widget _controls(AuctionLot lot) {
    final blocker = a.bidBlocker(a.user);
    final canBid = _phase != _Phase.result && blocker == null;
    final me = a.user;
    final imLeader = a.leader == a.userId;
    return Container(
      padding: EdgeInsets.zero,
      child: Column(
        children: [
          Row(
            children: [
              SizedBox(
                width: 84,
                child: GhostButton(
                  label: a.userPassed ? 'OUT' : 'PASS',
                  height: 56,
                  color: AppColors.textDim,
                  onTap: (_phase == _Phase.result || a.userPassed || imLeader) ? null : _pass,
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: NeonButton(
                  label: imLeader && _phase != _Phase.result ? 'YOU LEAD' : 'BID ${money(a.nextAmount)}',
                  subtitle: imLeader && _phase != _Phase.result ? 'Waiting for rivals…' : (blocker ?? 'Max ${money(a.maxAllowed(me))}'),
                  height: 56,
                  colors: imLeader ? const [AppColors.green, Color(0xFF0F9D6B)] : [c.userTeam.glow, c.userTeam.primary],
                  // Leading keeps the button lit (tapping does nothing).
                  onTap: canBid ? _userBid : (imLeader && _phase != _Phase.result ? () {} : null),
                ),
              ),
              const SizedBox(width: 8),
              PressScale(
                onTap: _simSet,
                onLongPress: _simAll,
                child: Container(
                  width: 56,
                  height: 56,
                  decoration: BoxDecoration(borderRadius: BorderRadius.circular(14), color: Colors.white.o(0.07), border: Border.all(color: AppColors.stroke)),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const Icon(Icons.fast_forward_rounded, color: AppColors.cyan),
                      Text('SIM SET', style: T.label(8.5, color: AppColors.cyan)),
                    ],
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              _mini('PURSE', money(me.purse), AppColors.gold),
              _mini('SQUAD', '${me.size}/${League.maxSquad}', AppColors.text),
              _mini('OVERSEAS', '${me.overseasCount}/${League.maxOverseas}', AppColors.text),
              if (a.mega) _mini('RTM', '${me.rtmCards}', AppColors.green),
              _mini('NEXT', _nextNames(), AppColors.textDim),
            ],
          ),
        ],
      ),
    );
  }

  String _nextNames() {
    final i = a.index + 1;
    if (i >= a.lots.length) return 'Last lot';
    return a.lots[i].player.surname;
  }

  Widget _mini(String label, String v, Color color) => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: T.label(8.5)),
          Text(v, maxLines: 1, style: T.display(17, color: color, weight: FontWeight.w700, height: 1.05)),
        ],
      );

  Widget _stampOverlay(AuctionLot l, TeamInfo? team) {
    final sold = l.status == LotStatus.sold;
    final color = sold ? (team!.glow) : AppColors.textMute;
    return AnimatedBuilder(
      animation: _stamp,
      builder: (context, child) {
        final t = Curves.easeOutBack.transform(_stamp.value.clamp(0.0, 1.0));
        return Opacity(
          opacity: _stamp.value.clamp(0.0, 1.0),
          child: Transform.scale(scale: 2.2 - 1.2 * t, child: child),
        );
      },
      child: Center(
        child: Transform.rotate(
          angle: -0.18,
          child: Container(
            padding: const EdgeInsets.fromLTRB(22, 6, 22, 12),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(18),
              border: Border.all(color: color, width: 5),
              color: Colors.black.o(0.72),
              boxShadow: [BoxShadow(color: color.o(0.6), blurRadius: 40)],
            ),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(sold ? (l.viaRtm ? 'RTM!' : 'SOLD!') : 'UNSOLD', style: T.display(76, color: color, weight: FontWeight.w700, height: 0.95, spacing: 4)),
                if (sold) ...[
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      TeamCrest(team!, size: 34),
                      const SizedBox(width: 8),
                      Text(team.id, style: T.display(32, weight: FontWeight.w700)),
                    ],
                  ),
                  GradientText(money(l.price), style: T.display(40, weight: FontWeight.w700, height: 1)),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _RingPainter extends CustomPainter {
  _RingPainter(this.v, this.color);
  final double v;
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final c = size.center(Offset.zero);
    final r = size.width / 2 - 4;
    canvas.drawCircle(c, r, Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 5
      ..color = Colors.white.o(0.1));
    canvas.drawArc(
      Rect.fromCircle(center: c, radius: r),
      -math.pi / 2,
      math.pi * 2 * v,
      false,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 5
        ..strokeCap = StrokeCap.round
        ..color = color,
    );
    canvas.drawCircle(c, r - 8, Paint()..color = color.o(0.12));
    final icon = Icons.gavel_rounded;
    final tp = TextPainter(
      text: TextSpan(text: String.fromCharCode(icon.codePoint), style: TextStyle(fontFamily: icon.fontFamily, package: icon.fontPackage, fontSize: 22, color: color)),
      textDirection: TextDirection.ltr,
    )..layout();
    tp.paint(canvas, c - Offset(tp.width / 2, tp.height / 2));
    tp.dispose();
  }

  @override
  bool shouldRepaint(covariant _RingPainter old) => old.v != v || old.color != color;
}

/// Auction hall: LED wall, sweeping spotlights and a glowing floor.
class _StagePainter extends CustomPainter {
  _StagePainter(this.anim, this.color) : super(repaint: anim);
  final Animation<double> anim;
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final t = anim.value * math.pi * 2;
    final rect = Offset.zero & size;
    canvas.drawRect(rect, Paint()..shader = const LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [Color(0xFF0B0F33), Color(0xFF05071A), Color(0xFF020309)]).createShader(rect));
    // LED wall.
    final wall = Rect.fromLTWH(size.width * 0.06, size.height * 0.16, size.width * 0.88, size.height * 0.46);
    canvas.drawRRect(RRect.fromRectAndRadius(wall, const Radius.circular(18)), Paint()..color = color.o(0.07));
    final dot = Paint()..color = color.o(0.10);
    const step = 9.0;
    for (var y = wall.top + 6; y < wall.bottom - 4; y += step) {
      for (var x = wall.left + 6; x < wall.right - 4; x += step) {
        canvas.drawCircle(Offset(x, y), 1.2, dot);
      }
    }
    final scan = wall.top + (wall.height) * ((anim.value * 3) % 1.0);
    canvas.drawRect(Rect.fromLTWH(wall.left, scan - 18, wall.width, 36), Paint()..shader = LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [color.o(0), color.o(0.10), color.o(0)]).createShader(Rect.fromLTWH(wall.left, scan - 18, wall.width, 36)));
    // Spotlights.
    for (var i = 0; i < 2; i++) {
      final origin = Offset(size.width * (i == 0 ? 0.1 : 0.9), -10);
      final target = Offset(size.width * (0.5 + 0.18 * math.sin(t + i * 2.4)), size.height * 0.62);
      final dir = target - origin;
      final n = Offset(-dir.dy, dir.dx) / dir.distance;
      final path = Path()
        ..moveTo(origin.dx, origin.dy)
        ..lineTo(target.dx + n.dx * 110, target.dy + n.dy * 110)
        ..lineTo(target.dx - n.dx * 110, target.dy - n.dy * 110)
        ..close();
      canvas.drawPath(path, Paint()..shader = LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [Colors.white.o(0.13), color.o(0.05)]).createShader(Rect.fromPoints(origin, target)));
    }
    // Floor glow.
    final floor = Offset(size.width / 2, size.height * 0.66);
    canvas.drawOval(Rect.fromCenter(center: floor, width: size.width * 1.1, height: size.height * 0.18), Paint()..shader = RadialGradient(colors: [color.o(0.3), color.o(0)]).createShader(Rect.fromCenter(center: floor, width: size.width * 1.1, height: size.height * 0.18)));
  }

  @override
  bool shouldRepaint(covariant _StagePainter old) => old.color != color;
}

class _SquadsSheet extends StatefulWidget {
  const _SquadsSheet({required this.engine});
  final AuctionEngine engine;

  @override
  State<_SquadsSheet> createState() => _SquadsSheetState();
}

class _SquadsSheetState extends State<_SquadsSheet> {
  late String _team = widget.engine.userId;

  @override
  Widget build(BuildContext context) {
    final f = widget.engine.franchises[_team]!;
    final squad = List.of(f.squad)..sort((a, b) => b.price.compareTo(a.price));
    final mix = f.mix;
    return SizedBox(
      height: MediaQuery.of(context).size.height * 0.92,
      child: Row(
        children: [
          SizedBox(
            width: 110,
            child: ListView(
              padding: const EdgeInsets.fromLTRB(8, 8, 4, 8),
              children: [
                for (final t in widget.engine.franchises.values)
                  GestureDetector(
                    onTap: () => setState(() => _team = t.id),
                    child: Container(
                      margin: const EdgeInsets.only(bottom: 5),
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 5),
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(color: t.id == _team ? t.info.glow : AppColors.strokeSoft),
                        color: t.id == _team ? t.info.primary.o(0.4) : null,
                      ),
                      child: Row(children: [TeamCrest(t.info, size: 24), const SizedBox(width: 6), Text(t.id, style: T.display(17))]),
                    ),
                  ),
              ],
            ),
          ),
          Expanded(
            child: Column(
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(12, 12, 16, 0),
                  child: Row(
                    children: [
                      Expanded(child: Text(f.info.name.toUpperCase(), style: T.display(22, weight: FontWeight.w700))),
                      GradientText(money(f.purse), style: T.display(22, weight: FontWeight.w700)),
                      const SizedBox(width: 8),
                      CircleIconButton(icon: Icons.close_rounded, size: 34, onTap: () => Navigator.pop(context)),
                    ],
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(12, 4, 16, 8),
                  child: Wrap(
                    spacing: 6,
                    runSpacing: 4,
                    children: [
                      Pill('${f.size}/25 players', color: AppColors.text, fontSize: 10.5),
                      Pill('${f.overseasCount}/8 overseas', color: AppColors.cyan, fontSize: 10.5),
                      Pill('WK ${mix.wk}', color: roleColor(Role.wk), fontSize: 10.5),
                      Pill('BAT ${mix.bat}', color: roleColor(Role.bat), fontSize: 10.5),
                      Pill('AR ${mix.ar}', color: roleColor(Role.ar), fontSize: 10.5),
                      Pill('PACE ${mix.pace} • SPIN ${mix.spin}', color: roleColor(Role.bowl), fontSize: 10.5),
                    ],
                  ),
                ),
                Expanded(
                  child: squad.isEmpty
                      ? Center(child: Text('No players yet', style: T.ui(15, color: AppColors.textDim)))
                      : GridView.builder(
                          padding: const EdgeInsets.fromLTRB(12, 0, 16, 12),
                          gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(crossAxisCount: 2, mainAxisExtent: 36, crossAxisSpacing: 16),
                          itemCount: squad.length,
                          itemBuilder: (context, i) {
                            final x = squad[i];
                            return Row(
                              children: [
                                RatingBadge(x.player.overall, size: 28),
                                const SizedBox(width: 8),
                                RoleBadge(x.player.role, size: 9.5),
                                const SizedBox(width: 6),
                                Expanded(child: Text(x.player.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: T.ui(14, weight: FontWeight.w700))),
                                if (x.player.overseas) const Padding(padding: EdgeInsets.only(right: 6), child: Icon(Icons.flight_rounded, size: 14, color: AppColors.cyan)),
                                Text(money(x.price), style: T.display(17, color: AppColors.gold)),
                                const SizedBox(width: 6),
                                Text(x.how.label, style: T.ui(10.5, color: AppColors.textMute)),
                              ],
                            );
                          },
                        ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Exposed for summary screens.
Franchise franchiseOf(String id) => Career.instance.franchises[id]!;
