import 'dart:math' as math;
import 'dart:ui' show PointMode;

import 'package:flutter/material.dart';

import '../../core/league.dart';
import '../../core/theme.dart';
import '../../data/teams.dart';
import '../../engine/match_engine.dart';
import '../../models/player.dart';
import '../widgets/common.dart';
import '../widgets/crest.dart';
import '../widgets/paint_utils.dart';

/// "c Pandya b Bumrah", "lbw b Chahal", "run out (Jadeja)".
String dismissalLine(Dismissal d, Player? fielder, Player? bowler, Player out) {
  final b = bowler?.shortName ?? '';
  final f = fielder?.shortName ?? 'sub';
  return switch (d) {
    Dismissal.bowled => 'b $b',
    Dismissal.lbw => 'lbw b $b',
    Dismissal.caught => fielder != null && bowler != null && fielder.id == bowler.id ? 'c & b $b' : 'c $f b $b',
    Dismissal.caughtBehind => 'c †$f b $b',
    Dismissal.stumped => 'st †$f b $b',
    Dismissal.runOut => 'run out ($f)',
  };
}

class MatchResultScreen extends StatefulWidget {
  const MatchResultScreen({super.key, required this.engine, required this.userTeamId, required this.title});

  final MatchEngine engine;
  final String userTeamId;
  final String title;

  @override
  State<MatchResultScreen> createState() => _MatchResultScreenState();
}

class _MatchResultScreenState extends State<MatchResultScreen> {
  bool _worm = false;

  @override
  Widget build(BuildContext context) {
    final m = widget.engine;
    final o = m.outcome!;
    final winner = teamById(o.winnerId!);
    final won = o.winnerId == widget.userTeamId;
    final potm = m.playerById(o.potmId);
    return ScreenFrame(
      colors: [winner.glow, winner.primary, AppColors.violet, winner.secondary],
      child: Column(
        children: [
          TopBar(
            title: 'MATCH CENTRE',
            subtitle: widget.title,
            onBack: () => Navigator.pop(context),
            actions: [
              SizedBox(
                width: 200,
                child: NeonButton(label: 'CONTINUE', icon: Icons.arrow_forward_rounded, height: 42, fontSize: 20, onTap: () => Navigator.pop(context)),
              ),
            ],
          ),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(12, 2, 12, 10),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  SizedBox(width: 340, child: ListView(padding: EdgeInsets.zero, children: _summary(m, o, winner, won, potm))),
                  const SizedBox(width: 12),
                  Expanded(
                    child: GlassCard(
                      padding: const EdgeInsets.only(top: 4),
                      color: const Color(0xB30B1030),
                      child: ScorecardView(engine: m),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  List<Widget> _summary(MatchEngine m, MatchOutcome o, TeamInfo winner, bool won, Player potm) {
    return [
      GlassCard(
              blur: true,
              glow: winner.glow,
              borderColor: (won ? AppColors.gold : winner.glow).o(0.6),
              padding: const EdgeInsets.all(12),
              child: Column(
                children: [
                  Row(
                    children: [
                      TeamCrest(winner, size: 46, glow: true),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(won ? 'YOU WON!' : 'RESULT', style: T.label(10.5, color: won ? AppColors.gold : AppColors.textDim)),
                            Text(o.text, style: T.display(22, weight: FontWeight.w700, height: 1.05)),
                          ],
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      for (final inn in m.innings.take(2))
                        Expanded(
                          child: Row(
                            children: [
                              TeamCrest(inn.batting.team, size: 24),
                              const SizedBox(width: 6),
                              Text('${inn.batting.team.id} ${inn.scoreText}', style: T.display(21, weight: FontWeight.w700)),
                              Text(' (${inn.overs})', style: T.ui(11.5, color: AppColors.textDim)),
                            ],
                          ),
                        ),
                    ],
                  ),
                  const Divider(color: AppColors.strokeSoft, height: 18),
                  Row(
                    children: [
                      PlayerAvatar(potm, size: 38, team: m.home.order.any((p) => p.id == potm.id) ? m.home.team : m.away.team),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text('PLAYER OF THE MATCH', style: T.label(9.5, color: AppColors.gold)),
                            Text(potm.name, style: T.ui(15.5, weight: FontWeight.w700)),
                          ],
                        ),
                      ),
                      Text(_potmLine(m, potm.id), style: T.display(17, color: AppColors.goldLight)),
                    ],
                  ),
                ],
              ),
            ),
      const SizedBox(height: 10),
      GlassCard(
        padding: const EdgeInsets.fromLTRB(10, 8, 10, 10),
        color: const Color(0xB30B1030),
        child: Column(
          children: [
            Row(
              children: [
                Text(_worm ? 'WORM' : 'MANHATTAN', style: T.label(11)),
                const Spacer(),
                GestureDetector(
                  onTap: () => setState(() => _worm = !_worm),
                  child: Pill(_worm ? 'SHOW MANHATTAN' : 'SHOW WORM', color: AppColors.cyan, fontSize: 10),
                ),
              ],
            ),
            const SizedBox(height: 6),
            SizedBox(height: 130, child: CustomPaint(painter: _ChartPainter(m, worm: _worm), size: Size.infinite)),
          ],
        ),
      ),
    ];
  }

  String _potmLine(MatchEngine m, int id) {
    final parts = <String>[];
    for (final inn in m.innings.take(2)) {
      final b = inn.bat[id];
      if (b != null && (b.balls > 0 || b.runs > 0)) parts.add('${b.runs}${b.out ? '' : '*'} (${b.balls})');
      final w = inn.bowl[id];
      if (w != null && w.balls > 0) parts.add('${w.wickets}/${w.runs}');
    }
    return parts.join(' & ');
  }
}

class ScorecardView extends StatefulWidget {
  const ScorecardView({super.key, required this.engine});
  final MatchEngine engine;

  @override
  State<ScorecardView> createState() => _ScorecardViewState();
}

class _ScorecardViewState extends State<ScorecardView> {
  int _i = 0;

  @override
  Widget build(BuildContext context) {
    final m = widget.engine;
    if (!m.started) return Center(child: Text('Match not started', style: T.ui(14, color: AppColors.textDim)));
    final inns = m.innings;
    _i = _i.clamp(0, inns.length - 1);
    final inn = inns[_i];
    final dnb = inn.batting.order.where((p) => !inn.bat.containsKey(p.id)).toList();
    return Column(
      children: [
        SizedBox(
          height: 44,
          child: ListView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 5),
            children: [
              for (var i = 0; i < inns.length; i++)
                Padding(
                  padding: const EdgeInsets.only(right: 6),
                  child: PressScale(
                    onTap: () => setState(() => _i = i),
                    child: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 12),
                      alignment: Alignment.center,
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(18),
                        color: i == _i ? inns[i].batting.team.primary.o(0.7) : Colors.white.o(0.06),
                        border: Border.all(color: i == _i ? inns[i].batting.team.glow : AppColors.strokeSoft),
                      ),
                      child: Text(
                        '${inns[i].batting.team.id} ${inns[i].superOver ? 'SUPER OVER' : '${i + 1}${i == 0 ? 'ST' : 'ND'} INNS'}',
                        style: T.display(16, weight: FontWeight.w700),
                      ),
                    ),
                  ),
                ),
            ],
          ),
        ),
        Expanded(
          child: ListView(
            padding: const EdgeInsets.fromLTRB(12, 2, 12, 12),
            children: [
              _header(['BATTER', 'R', 'B', '4s', '6s', 'SR']),
              for (final c in (inn.bat.values.toList()..sort((a, b) => a.position.compareTo(b.position))))
                _batRow(inn, c),
              _line('Extras', '${inn.extras}', '(wd ${inn.wides}, nb ${inn.noBalls}, lb ${inn.legByes}, b ${inn.byes})'),
              _line('TOTAL', inn.scoreText, '(${inn.overs} ov, RR ${inn.runRate.toStringAsFixed(2)})', bold: true),
              if (dnb.isNotEmpty)
                Padding(
                  padding: const EdgeInsets.fromLTRB(6, 6, 6, 2),
                  child: Text('Did not bat: ${dnb.map((p) => p.shortName).join(', ')}', style: T.ui(12, color: AppColors.textDim)),
                ),
              if (inn.fow.isNotEmpty)
                Padding(
                  padding: const EdgeInsets.fromLTRB(6, 6, 6, 8),
                  child: Text(
                    'Fall of wickets: ${inn.fow.map((f) => '${f.score}-${f.wicket} (${inn.batting.byId(f.playerId).surname.toLowerCase()}, ${oversText(f.balls)})').join(', ')}',
                    style: T.ui(11.5, color: AppColors.textMute, height: 1.25),
                  ),
                ),
              const SizedBox(height: 6),
              _header(['BOWLER', 'O', 'M', 'R', 'W', 'ECON']),
              for (final id in inn.bowlerOrder) _bowlRow(inn, inn.bowl[id]!),
            ],
          ),
        ),
      ],
    );
  }

  Widget _header(List<String> cols) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
        decoration: BoxDecoration(color: Colors.white.o(0.06), borderRadius: BorderRadius.circular(8)),
        child: Row(
          children: [
            Expanded(child: Text(cols.first, style: T.label(10))),
            for (final c in cols.skip(1)) SizedBox(width: c == 'ECON' || c == 'SR' ? 46 : 32, child: Text(c, textAlign: TextAlign.right, style: T.label(10))),
          ],
        ),
      );

  Widget _batRow(Innings inn, BatCard c) {
    final p = inn.batting.byId(c.playerId);
    final how = c.out
        ? dismissalLine(c.how!, c.fielderId == null ? null : inn.bowling.byId(c.fielderId!), c.bowlerId == null ? null : inn.bowling.byId(c.bowlerId!), p)
        : 'not out';
    final atCrease = !c.out && (c.playerId == inn.striker || c.playerId == inn.nonStriker) && !inn.complete;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 5),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('${p.shortName}${p.id == inn.batting.captainId ? ' (c)' : ''}${p.id == inn.batting.keeperId ? ' †' : ''}',
                    style: T.ui(14, weight: FontWeight.w700, color: c.out ? AppColors.text : AppColors.goldLight)),
                Text(atCrease ? 'batting' : how, style: T.ui(11, color: AppColors.textDim)),
              ],
            ),
          ),
          for (final v in ['${c.runs}', '${c.balls}', '${c.fours}', '${c.sixes}'])
            SizedBox(width: 32, child: Text(v, textAlign: TextAlign.right, style: v == '${c.runs}' ? T.display(18, weight: FontWeight.w700) : T.ui(13.5))),
          SizedBox(width: 46, child: Text(c.strikeRate.toStringAsFixed(1), textAlign: TextAlign.right, style: T.ui(12.5, color: AppColors.textDim))),
        ],
      ),
    );
  }

  Widget _bowlRow(Innings inn, BowlCard b) {
    final p = inn.bowling.byId(b.playerId);
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
      child: Row(
        children: [
          Expanded(child: Text(p.shortName, style: T.ui(14, weight: FontWeight.w700))),
          for (final v in [oversText(b.balls), '${b.maidens}', '${b.runs}', '${b.wickets}'])
            SizedBox(width: 32, child: Text(v, textAlign: TextAlign.right, style: v == '${b.wickets}' ? T.display(18, weight: FontWeight.w700, color: b.wickets > 0 ? AppColors.pink : AppColors.text) : T.ui(13.5))),
          SizedBox(width: 46, child: Text(b.economy.toStringAsFixed(2), textAlign: TextAlign.right, style: T.ui(12.5, color: AppColors.textDim))),
        ],
      ),
    );
  }

  Widget _line(String a, String b, String sub, {bool bold = false}) => Padding(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 5),
        child: Row(
          children: [
            Text(a, style: bold ? T.display(19, weight: FontWeight.w700) : T.ui(13.5, color: AppColors.textDim)),
            const SizedBox(width: 8),
            Expanded(child: Text(sub, style: T.ui(11.5, color: AppColors.textMute))),
            Text(b, style: bold ? T.display(22, weight: FontWeight.w700, color: AppColors.gold) : T.ui(14, weight: FontWeight.w700)),
          ],
        ),
      );
}

class _ChartPainter extends CustomPainter {
  _ChartPainter(this.m, {required this.worm});
  final MatchEngine m;
  final bool worm;

  @override
  void paint(Canvas canvas, Size size) {
    final inns = m.innings.where((i) => !i.superOver).take(2).toList();
    if (inns.isEmpty) return;
    final overs = m.overs;
    final left = 22.0, bottom = size.height - 14;
    final w = size.width - left;
    final axis = Paint()
      ..color = Colors.white.o(0.15)
      ..strokeWidth = 1;
    canvas.drawLine(Offset(left, bottom), Offset(size.width, bottom), axis);
    for (var o = 0; o <= overs; o += overs >= 10 ? 5 : 1) {
      final x = left + w * o / overs;
      paintText(canvas, '$o', Offset(x, bottom + 7), T.ui(9, color: AppColors.textMute));
    }
    if (worm) {
      final maxR = math.max(1, inns.map((i) => i.runs).reduce(math.max)).toDouble();
      for (final inn in inns) {
        final col = inn.batting.team.glow;
        final pts = <Offset>[Offset(left, bottom)];
        var cum = 0;
        for (var o = 0; o < inn.overRuns.length; o++) {
          cum += inn.overRuns[o];
          pts.add(Offset(left + w * (o + 1) / overs, bottom - (bottom - 6) * cum / maxR));
        }
        canvas.drawPoints(
          PointMode.polygon,
          pts,
          Paint()
            ..color = col
            ..strokeWidth = 2.4
            ..strokeCap = StrokeCap.round,
        );
        for (final f in inn.fow) {
          final x = left + w * (f.balls / 6) / overs;
          final y = bottom - (bottom - 6) * f.score / maxR;
          canvas.drawCircle(Offset(x, y), 3.2, Paint()..color = AppColors.red);
        }
      }
      paintText(canvas, '${maxR.round()}', Offset(10, 10), T.ui(9, color: AppColors.textMute));
      return;
    }
    final maxO = math.max(6, inns.expand((i) => i.overRuns).fold(0, math.max)).toDouble();
    final slot = w / overs;
    final bw = slot / (inns.length + 0.6);
    for (var k = 0; k < inns.length; k++) {
      final inn = inns[k];
      final col = inn.batting.team.glow;
      for (var o = 0; o < inn.overRuns.length; o++) {
        final h = (bottom - 8) * inn.overRuns[o] / maxO;
        final x = left + slot * o + bw * k + bw * 0.3;
        canvas.drawRRect(
          RRect.fromRectAndCorners(Rect.fromLTWH(x, bottom - h, bw * 0.92, h), topLeft: const Radius.circular(3), topRight: const Radius.circular(3)),
          Paint()..shader = LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [col, col.darken(0.45)]).createShader(Rect.fromLTWH(x, bottom - h, bw, h)),
        );
        final wk = inn.fow.where((f) => (f.balls - 1) ~/ 6 == o).length;
        for (var i = 0; i < wk; i++) {
          canvas.drawCircle(Offset(x + bw * 0.46, bottom - h - 5 - i * 7), 2.8, Paint()..color = AppColors.red);
        }
      }
    }
    paintText(canvas, '${maxO.round()}', Offset(10, 10), T.ui(9, color: AppColors.textMute));
  }

  @override
  bool shouldRepaint(covariant _ChartPainter old) => old.worm != worm;
}
