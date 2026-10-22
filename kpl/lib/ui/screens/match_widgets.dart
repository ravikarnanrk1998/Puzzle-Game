import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../core/theme.dart';
import '../../engine/delivery.dart';
import '../../engine/match_engine.dart';
import '../widgets/common.dart';
import '../widgets/crest.dart';

/// Broadcast "score bug" (top-left).
class ScoreBug extends StatelessWidget {
  const ScoreBug({super.key, required this.engine});
  final MatchEngine engine;

  @override
  Widget build(BuildContext context) {
    if (!engine.started) return const SizedBox.shrink();
    final inn = engine.current;
    final t = inn.batting.team;
    final target = inn.target;
    return Container(
      padding: const EdgeInsets.fromLTRB(6, 4, 12, 5),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(12),
        gradient: LinearGradient(colors: [t.primary.o(0.95), const Color(0xF0080B22)], stops: const [0.0, 0.42]),
        border: Border.all(color: t.glow.o(0.65)),
        boxShadow: [BoxShadow(color: Colors.black.o(0.45), blurRadius: 12)],
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          TeamCrest(t, size: 28),
          const SizedBox(width: 8),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.end,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(t.id, style: T.display(19, weight: FontWeight.w700, height: 1)),
                  const SizedBox(width: 6),
                  Text(inn.scoreText, style: T.display(27, weight: FontWeight.w700, height: 0.95, color: AppColors.goldLight)),
                  const SizedBox(width: 6),
                  Text('(${inn.overs})', style: T.ui(12.5, color: AppColors.textDim, weight: FontWeight.w700)),
                  if (inn.superOver) ...[const SizedBox(width: 6), Pill('SUPER OVER', color: AppColors.pink, fontSize: 8.5, filled: true)],
                ],
              ),
              Text(
                target != null
                    ? 'TARGET $target • NEED ${inn.runsNeeded} OFF ${inn.ballsLeft}'
                    : 'CRR ${inn.runRate.toStringAsFixed(2)}',
                style: T.label(9, color: target != null ? AppColors.gold : AppColors.textDim),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class BallChip extends StatelessWidget {
  const BallChip(this.r, {super.key, this.size = 22});
  final BallResult r;
  final double size;

  @override
  Widget build(BuildContext context) {
    final (Color bg, Color fg) = r.wicket
        ? (AppColors.red, Colors.white)
        : r.isSix
            ? (AppColors.violet, Colors.white)
            : r.isFour
                ? (AppColors.cyan, const Color(0xFF041018))
                : (r.extra == ExtraType.wide || r.extra == ExtraType.noBall)
                    ? (AppColors.gold, const Color(0xFF1A1205))
                    : (Colors.white.o(r.total == 0 ? 0.14 : 0.24), Colors.white);
    final label = r.chip;
    return Container(
      height: size,
      constraints: BoxConstraints(minWidth: size),
      padding: EdgeInsets.symmetric(horizontal: label.length > 2 ? 4 : 0),
      alignment: Alignment.center,
      margin: const EdgeInsets.only(right: 4),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(size / 2)),
      child: Text(label, style: T.display(size * 0.72, color: fg, weight: FontWeight.w700, height: 1.1)),
    );
  }
}

/// "J BUMRAH to S YADAV" + balls of the current over (top-centre).
class MatchupStrip extends StatelessWidget {
  const MatchupStrip({super.key, required this.engine});
  final MatchEngine engine;

  @override
  Widget build(BuildContext context) {
    if (!engine.started || engine.finished) return const SizedBox.shrink();
    final inn = engine.current;
    final bowlerId = inn.bowlerId ?? inn.lastOverBowler;
    final bowler = bowlerId == null ? null : inn.bowling.byId(bowlerId);
    final striker = inn.batting.byId(inn.striker);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 5),
      decoration: BoxDecoration(
        color: Colors.black.o(0.55),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.stroke),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            bowler == null ? striker.shortName.toUpperCase() : '${bowler.shortName.toUpperCase()}  to  ${striker.shortName.toUpperCase()}',
            style: T.display(16, weight: FontWeight.w700, height: 1),
          ),
          if (inn.freeHit) ...[const SizedBox(width: 8), Pill('FREE HIT', color: AppColors.gold, fontSize: 9, filled: true)],
          const SizedBox(width: 10),
          for (final b in inn.recentOver) BallChip(b.result, size: 19),
        ],
      ),
    );
  }
}

/// Runs / balls panel of the striker (bottom-right, like TV).
class BatterPanel extends StatelessWidget {
  const BatterPanel({super.key, required this.engine});
  final MatchEngine engine;

  @override
  Widget build(BuildContext context) {
    if (!engine.started || engine.finished) return const SizedBox.shrink();
    final inn = engine.current;
    Widget row(int id, bool strike) {
      final p = inn.batting.byId(id);
      final c = inn.bat[id]!;
      return Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          SizedBox(
            width: 14,
            child: strike ? const Icon(Icons.play_arrow_rounded, size: 14, color: AppColors.gold) : null,
          ),
          SizedBox(
            width: 92,
            child: Text(p.shortName, maxLines: 1, overflow: TextOverflow.ellipsis, style: T.ui(12.5, weight: FontWeight.w700, color: strike ? AppColors.text : AppColors.textDim)),
          ),
          SizedBox(width: 30, child: Text('${c.runs}', textAlign: TextAlign.right, style: T.display(18, weight: FontWeight.w700, height: 1))),
          SizedBox(width: 30, child: Text('${c.balls}', textAlign: TextAlign.right, style: T.ui(12, color: AppColors.textDim))),
        ],
      );
    }

    final bowlerId = inn.bowlerId ?? inn.lastOverBowler;
    final bc = bowlerId == null ? null : inn.bowl[bowlerId];
    return Container(
      padding: const EdgeInsets.fromLTRB(8, 6, 10, 6),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(12),
        color: const Color(0xE6070A1E),
        border: Border.all(color: inn.batting.team.glow.o(0.5)),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(mainAxisSize: MainAxisSize.min, children: [
            const SizedBox(width: 106),
            SizedBox(width: 30, child: Text('R', textAlign: TextAlign.right, style: T.label(8.5))),
            SizedBox(width: 30, child: Text('B', textAlign: TextAlign.right, style: T.label(8.5))),
          ]),
          row(inn.striker, true),
          row(inn.nonStriker, false),
          if (bowlerId != null) ...[
            const SizedBox(height: 3),
            Text('${inn.bowling.byId(bowlerId).shortName}  ${bc?.figures ?? '0-0-0-0'}', style: T.ui(11, color: AppColors.pink, weight: FontWeight.w700)),
          ],
        ],
      ),
    );
  }
}

class TimingReadout extends StatelessWidget {
  const TimingReadout({super.key, required this.grade});
  final TimingGrade? grade;

  @override
  Widget build(BuildContext context) {
    final g = grade;
    final color = switch (g) {
      TimingGrade.perfect => AppColors.green,
      TimingGrade.good => AppColors.cyan,
      TimingGrade.early || TimingGrade.late => AppColors.gold,
      null => AppColors.textMute,
      _ => AppColors.red,
    };
    return AnimatedSwitcher(
      duration: const Duration(milliseconds: 200),
      transitionBuilder: (c, a) => ScaleTransition(scale: a, child: c),
      child: Container(
        key: ValueKey(g),
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
        decoration: BoxDecoration(color: Colors.black.o(0.5), borderRadius: BorderRadius.circular(10), border: Border.all(color: color.o(0.6))),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('TIMING', style: T.label(8)),
            Text(g?.label ?? '—', style: T.display(18, color: color, weight: FontWeight.w700, height: 1)),
          ],
        ),
      ),
    );
  }
}

/// Animated "swipe to play" helper (bottom-left while batting).
class SwipeHint extends StatefulWidget {
  const SwipeHint({super.key, required this.active, required this.loft, required this.onLoft});
  final bool active;
  final bool loft;
  final VoidCallback onLoft;

  @override
  State<SwipeHint> createState() => _SwipeHintState();
}

class _SwipeHintState extends State<SwipeHint> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(vsync: this, duration: const Duration(milliseconds: 1400))..repeat();

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        Container(
          width: 170,
          height: 82,
          padding: const EdgeInsets.fromLTRB(10, 6, 10, 6),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(16),
            color: Colors.black.o(0.5),
            border: Border.all(color: (widget.active ? AppColors.cyan : AppColors.stroke).o(0.8), width: widget.active ? 1.6 : 1),
            boxShadow: widget.active ? [BoxShadow(color: AppColors.cyan.o(0.35), blurRadius: 16)] : null,
          ),
          child: Column(
            children: [
              Text(widget.active ? 'SWIPE NOW!' : 'SWIPE TO PLAY SHOT', style: T.display(16, weight: FontWeight.w700, color: widget.active ? AppColors.cyan : AppColors.text)),
              Expanded(
                child: AnimatedBuilder(
                  animation: _c,
                  builder: (context, _) => CustomPaint(painter: _SwipePainter(_c.value), size: Size.infinite),
                ),
              ),
              Text('TAP = DEFEND', style: T.label(8.5)),
            ],
          ),
        ),
        const SizedBox(width: 8),
        PressScale(
          onTap: widget.onLoft,
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 160),
            width: 58,
            height: 58,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              gradient: widget.loft ? AppGradients.gold : null,
              color: widget.loft ? null : Colors.black.o(0.5),
              border: Border.all(color: widget.loft ? Colors.white : AppColors.stroke, width: widget.loft ? 2 : 1),
              boxShadow: widget.loft ? [BoxShadow(color: AppColors.gold.o(0.6), blurRadius: 16)] : null,
            ),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Icon(Icons.north_rounded, size: 18, color: widget.loft ? const Color(0xFF1A1205) : AppColors.text),
                Text('LOFT', style: T.display(14, color: widget.loft ? const Color(0xFF1A1205) : AppColors.text, weight: FontWeight.w700, height: 1)),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _SwipePainter extends CustomPainter {
  _SwipePainter(this.t);
  final double t;

  @override
  void paint(Canvas canvas, Size size) {
    final c = size.center(Offset.zero);
    final chev = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2.6
      ..strokeCap = StrokeCap.round;
    for (var i = 0; i < 3; i++) {
      final a = ((t * 3 - i) % 3) / 3;
      chev.color = AppColors.cyan.o(0.25 + 0.75 * (1 - a));
      final dx = 14.0 + i * 11;
      for (final s in [-1.0, 1.0]) {
        canvas.drawPath(
          Path()
            ..moveTo(c.dx + s * dx, c.dy - 7)
            ..lineTo(c.dx + s * (dx + 7), c.dy)
            ..lineTo(c.dx + s * dx, c.dy + 7),
          chev,
        );
      }
    }
    final hx = c.dx + math.sin(t * math.pi * 2) * 12;
    canvas.drawCircle(Offset(hx, c.dy), 9, Paint()..color = Colors.white.o(0.9));
    canvas.drawCircle(Offset(hx, c.dy), 15, Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.5
      ..color = Colors.white.o(0.4));
  }

  @override
  bool shouldRepaint(covariant _SwipePainter old) => old.t != t;
}

/// Dropdown-style delivery variation selector (bowling).
class VariationSelector extends StatefulWidget {
  const VariationSelector({super.key, required this.options, required this.selected, required this.onSelect, required this.summary, required this.enabled});

  final List<String> options;
  final int selected;
  final ValueChanged<int> onSelect;
  final String summary;
  final bool enabled;

  @override
  State<VariationSelector> createState() => _VariationSelectorState();
}

class _VariationSelectorState extends State<VariationSelector> {
  bool _open = false;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (_open)
          Container(
            width: 170,
            margin: const EdgeInsets.only(bottom: 6),
            padding: const EdgeInsets.all(4),
            decoration: BoxDecoration(color: const Color(0xF0080B22), borderRadius: BorderRadius.circular(12), border: Border.all(color: AppColors.gold.o(0.5))),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                for (var i = 0; i < widget.options.length; i++)
                  PressScale(
                    onTap: () {
                      widget.onSelect(i);
                      setState(() => _open = false);
                    },
                    child: Container(
                      width: double.infinity,
                      margin: const EdgeInsets.symmetric(vertical: 2),
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(8),
                        color: i == widget.selected ? AppColors.gold : Colors.white.o(0.05),
                      ),
                      child: Text(widget.options[i], style: T.display(16, color: i == widget.selected ? const Color(0xFF1A1205) : AppColors.text, weight: FontWeight.w700, height: 1)),
                    ),
                  ),
              ],
            ),
          ),
        PressScale(
          onTap: widget.enabled ? () => setState(() => _open = !_open) : null,
          child: Container(
            width: 170,
            padding: const EdgeInsets.fromLTRB(10, 6, 8, 6),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(12),
              color: Colors.black.o(0.55),
              border: Border.all(color: AppColors.gold.o(0.7)),
            ),
            child: Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('DELIVERY', style: T.label(8.5, color: AppColors.gold)),
                      Text(widget.options[widget.selected], style: T.display(18, weight: FontWeight.w700, height: 1)),
                      Text(widget.summary, maxLines: 1, overflow: TextOverflow.ellipsis, style: T.ui(10.5, color: AppColors.textDim)),
                    ],
                  ),
                ),
                Icon(_open ? Icons.expand_more_rounded : Icons.expand_less_rounded, color: AppColors.gold),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

/// BOWL button that turns into the accuracy meter.
class BowlControls extends StatelessWidget {
  const BowlControls({super.key, required this.meterOn, required this.meter, required this.enabled, required this.autoBowl, required this.onBowl, required this.onAuto});

  final bool meterOn;
  final double meter;
  final bool enabled;
  final bool autoBowl;
  final VoidCallback onBowl;
  final VoidCallback onAuto;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        PressScale(
          onTap: onAuto,
          child: Container(
            width: 52,
            height: 52,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: autoBowl ? AppColors.green.o(0.3) : Colors.black.o(0.5),
              border: Border.all(color: autoBowl ? AppColors.green : AppColors.stroke),
            ),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Icon(Icons.smart_toy_rounded, size: 18, color: autoBowl ? AppColors.green : AppColors.textDim),
                Text('AUTO', style: T.label(8, color: autoBowl ? AppColors.green : AppColors.textDim)),
              ],
            ),
          ),
        ),
        const SizedBox(width: 10),
        SizedBox(
          width: 190,
          child: meterOn
              ? GestureDetector(
                  behavior: HitTestBehavior.opaque,
                  onTapDown: (_) => onBowl(),
                  child: _Meter(value: meter),
                )
              : NeonButton(
                  label: autoBowl ? 'AUTO' : 'BOWL',
                  subtitle: autoBowl ? 'AI picks deliveries' : 'Then stop the meter',
                  icon: Icons.sports_baseball_rounded,
                  height: 56,
                  colors: const [AppColors.pink, AppColors.violet],
                  onTap: enabled && !autoBowl ? onBowl : null,
                ),
        ),
      ],
    );
  }
}

class _Meter extends StatelessWidget {
  const _Meter({required this.value});
  final double value;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 56,
      decoration: BoxDecoration(borderRadius: BorderRadius.circular(14), color: const Color(0xF00B0F2E), border: Border.all(color: AppColors.gold)),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
      child: Column(
        children: [
          Text('TAP TO RELEASE', style: T.label(9, color: AppColors.gold)),
          const SizedBox(height: 4),
          Expanded(
            child: LayoutBuilder(builder: (context, c) {
              final w = c.maxWidth;
              return Stack(
                clipBehavior: Clip.none,
                children: [
                  Container(
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(6),
                      gradient: const LinearGradient(colors: [AppColors.red, AppColors.gold, AppColors.green, AppColors.gold, AppColors.red], stops: [0, 0.3, 0.5, 0.7, 1]),
                    ),
                  ),
                  Positioned(
                    left: (value * w - 3).clamp(0.0, w - 6),
                    top: -4,
                    bottom: -4,
                    child: Container(width: 6, decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(3), boxShadow: const [BoxShadow(color: Colors.white, blurRadius: 8)])),
                  ),
                ],
              );
            }),
          ),
        ],
      ),
    );
  }
}

/// Big animated text (FOUR!, SIX!, OUT!).
class ResultBanner extends StatelessWidget {
  const ResultBanner({super.key, required this.text, required this.color, this.sub});
  final String text;
  final Color color;
  final String? sub;

  @override
  Widget build(BuildContext context) {
    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0, end: 1),
      duration: const Duration(milliseconds: 520),
      curve: Curves.easeOutBack,
      builder: (context, v, child) => Opacity(
        opacity: v.clamp(0.0, 1.0),
        child: Transform.scale(scale: 0.4 + 0.6 * v, child: child),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          GradientText(
            text,
            gradient: LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [Colors.white, color, color.darken(0.3)]),
            style: T.display(text.length > 7 ? 58 : 88, weight: FontWeight.w700, height: 0.95, spacing: 3, shadows: [Shadow(color: color.o(0.8), blurRadius: 30)]),
          ),
          if (sub != null)
            Container(
              margin: const EdgeInsets.only(top: 2),
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 5),
              decoration: BoxDecoration(color: Colors.black.o(0.65), borderRadius: BorderRadius.circular(20), border: Border.all(color: color.o(0.6))),
              child: Text(sub!, textAlign: TextAlign.center, style: T.ui(14, weight: FontWeight.w700)),
            ),
        ],
      ),
    );
  }
}
