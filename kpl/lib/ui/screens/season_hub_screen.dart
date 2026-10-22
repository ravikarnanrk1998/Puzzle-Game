import 'package:flutter/material.dart';

import '../../core/league.dart';
import '../../core/theme.dart';
import '../../data/players_db.dart';
import '../../data/teams.dart';
import '../../engine/career.dart';
import '../../engine/season.dart';
import '../widgets/common.dart';
import '../widgets/crest.dart';
import 'champion_screen.dart';
import 'match_screen.dart';
import 'squad_screen.dart';

class SeasonHubScreen extends StatefulWidget {
  const SeasonHubScreen({super.key});

  @override
  State<SeasonHubScreen> createState() => _SeasonHubScreenState();
}

class _SeasonHubScreenState extends State<SeasonHubScreen> {
  final Career c = Career.instance;
  int _tab = 0;
  bool _myFixturesOnly = true;
  bool _busy = false;

  Season get s => c.season!;

  static const _tabs = <(String, IconData)>[
    ('HOME', Icons.home_rounded),
    ('TABLE', Icons.leaderboard_rounded),
    ('FIXTURES', Icons.calendar_month_rounded),
    ('STATS', Icons.insights_rounded),
    ('SQUAD', Icons.groups_rounded),
  ];

  Future<void> _play() async {
    if (_busy) return;
    setState(() => _busy = true);
    c.simulateUntilUserMatch();
    setState(() => _busy = false);
    final f = s.nextFixture;
    if (f == null || !f.involves(c.userTeamId)) {
      _afterMatch();
      return;
    }
    final engine = c.createMatch(f);
    await Navigator.of(context).push(fadeRoute(MatchScreen(
      engine: engine,
      userTeamId: c.userTeamId,
      title: '${f.title.toUpperCase()} • ${League.name} ${c.year}',
      day: c.settings.matchLight.isDay(afternoon: f.afternoon),
      onFinished: (m) => c.recordUserMatch(f, m),
    )));
    if (mounted) _afterMatch();
  }

  void _sim() {
    if (_busy) return;
    c.simulateUntilUserMatch();
    final f = s.nextFixture;
    if (f != null && f.involves(c.userTeamId)) {
      c.simulateUserFixture(f);
      final r = f.result!;
      showToast(context, '${f.title}: ${r.text}', color: r.winner == c.userTeamId ? const Color(0xFF0E7A52) : AppColors.red.darken(0.35));
    }
    _afterMatch();
  }

  void _simRest() {
    if (s.isEliminated(c.userTeamId)) {
      c.simulateRestOfSeason();
    } else {
      // Still alive: play on once the user's next (playoff) match is known.
      c.simulateUntilUserMatch();
    }
    _afterMatch();
  }

  void _afterMatch() {
    setState(() {});
    if (c.stage == Stage.finished) {
      Navigator.of(context).pushReplacement(fadeRoute(const ChampionScreen()));
    }
  }

  @override
  Widget build(BuildContext context) {
    final team = c.userTeam;
    final table = s.standings();
    final pos = table.indexWhere((t) => t.teamId == team.id) + 1;
    final st = table[pos - 1];
    return ScreenFrame(
      colors: [team.glow, team.primary, AppColors.violet, team.secondary],
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Navigation rail.
          Container(
            width: 84,
            margin: const EdgeInsets.fromLTRB(8, 8, 0, 8),
            padding: const EdgeInsets.symmetric(vertical: 8),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(18),
              color: const Color(0xCC070A1E),
              border: Border.all(color: AppColors.strokeSoft),
            ),
            child: Column(
              children: [
                CircleIconButton(icon: Icons.arrow_back_ios_new_rounded, size: 36, onTap: () => Navigator.of(context).popUntil((r) => r.isFirst)),
                const SizedBox(height: 6),
                for (final (i, (label, icon)) in _tabs.indexed)
                  Expanded(
                    child: PressScale(
                      onTap: () => setState(() => _tab = i),
                      child: AnimatedContainer(
                        duration: const Duration(milliseconds: 180),
                        margin: const EdgeInsets.symmetric(horizontal: 6, vertical: 3),
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(14),
                          gradient: _tab == i ? AppGradients.goldFlat : null,
                          color: _tab == i ? null : Colors.white.o(0.04),
                        ),
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Icon(icon, size: 20, color: _tab == i ? const Color(0xFF1A1205) : AppColors.textDim),
                            const SizedBox(height: 2),
                            Text(label, style: T.label(9, color: _tab == i ? const Color(0xFF1A1205) : AppColors.textDim)),
                          ],
                        ),
                      ),
                    ),
                  ),
              ],
            ),
          ),
          Expanded(
            child: Column(
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(12, 8, 12, 4),
                  child: Row(
                    children: [
                      TeamCrest(team, size: 38, glow: true),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text('${League.name} ${c.year}  •  ${team.name.toUpperCase()}', maxLines: 1, overflow: TextOverflow.ellipsis, style: T.display(24, weight: FontWeight.w700, height: 1)),
                            Text('${ordinal(pos)} • ${st.pts} PTS • NRR ${st.nrr >= 0 ? '+' : ''}${st.nrr.toStringAsFixed(2)} • ${st.w}W ${st.l}L',
                                style: T.label(10.5, color: team.glow)),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
                Expanded(
                  child: switch (_tab) {
                    0 => _home(table),
                    1 => _table(table),
                    2 => _fixtures(),
                    3 => _stats(),
                    _ => SquadPanel(onChanged: () => setState(() {})),
                  },
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  // ------------------------------------------------------------- home

  Widget _home(List<Standing> table) {
    final next = s.nextFixtureFor(c.userTeamId);
    final out = s.leagueDone && s.isEliminated(c.userTeamId);
    final played = s.fixtures.where((f) => f.played && f.involves(c.userTeamId)).toList();
    final last = played.isEmpty ? null : played.last;
    final orange = s.leaders((x) => x.runs.toDouble(), n: 1);
    final purple = s.leaders((x) => x.wickets.toDouble(), n: 1);
    return Padding(
      padding: const EdgeInsets.fromLTRB(12, 4, 12, 10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Expanded(flex: 11, child: next != null && !out ? _nextCard(next) : _seasonOverCard()),
          const SizedBox(width: 12),
          Expanded(
            flex: 9,
            child: ListView(
              padding: EdgeInsets.zero,
              children: [
                if (last != null) ...[
                  _resultTile(last),
                  const SizedBox(height: 8),
                ],
                GlassCard(
                  padding: const EdgeInsets.fromLTRB(10, 6, 10, 6),
                  color: const Color(0xB30B1030),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('STANDINGS', style: T.label(10.5, color: AppColors.gold)),
                      for (var i = 0; i < 5; i++) _miniRow(i + 1, table[i]),
                    ],
                  ),
                ),
                const SizedBox(height: 8),
                Row(
                  children: [
                    Expanded(child: _capCard('ORANGE CAP', const Color(0xFFFF9F1C), orange.isEmpty ? null : orange.first.key, orange.isEmpty ? '-' : '${orange.first.value.runs} runs')),
                    const SizedBox(width: 8),
                    Expanded(child: _capCard('PURPLE CAP', const Color(0xFFB57BFF), purple.isEmpty ? null : purple.first.key, purple.isEmpty ? '-' : '${purple.first.value.wickets} wkts')),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _nextCard(Fixture f) {
    final home = teamById(f.home), away = teamById(f.away);
    final venue = teamById(f.venueId);
    final hs = c.franchises[f.home]!.strength, as = c.franchises[f.away]!.strength;
    final pending = s.fixtures.takeWhile((x) => x != f).where((x) => !x.played).length;
    final day = c.settings.matchLight.isDay(afternoon: f.afternoon);
    return GlassCard(
      blur: true,
      padding: const EdgeInsets.all(14),
      borderColor: AppColors.gold.o(0.5),
      glow: AppColors.gold,
      gradient: LinearGradient(
        begin: Alignment.topLeft,
        end: Alignment.bottomRight,
        colors: [home.primary.o(0.45), const Color(0xE6070A1E), away.primary.o(0.45)],
      ),
      child: Column(
        children: [
          Row(
            children: [
              Pill(f.kind.isPlayoff ? f.kind.label.toUpperCase() : 'MATCH ${f.no} OF 70', color: f.kind.isPlayoff ? AppColors.pink : AppColors.gold, fontSize: 11),
              const Spacer(),
              Icon(day ? Icons.wb_sunny_rounded : Icons.nightlight_round, size: 15, color: day ? AppColors.gold : AppColors.cyan),
              const SizedBox(width: 4),
              Text(f.startTime, style: T.label(10.5)),
            ],
          ),
          Expanded(
            child: Row(
              children: [
                Expanded(child: _side(home, hs)),
                GradientText('VS', gradient: AppGradients.neon, style: T.display(50, weight: FontWeight.w700)),
                Expanded(child: _side(away, as)),
              ],
            ),
          ),
          Text('${venue.venue}, ${venue.city}', textAlign: TextAlign.center, style: T.ui(12.5, color: AppColors.textDim)),
          if (pending > 0)
            Text('$pending other match${pending == 1 ? '' : 'es'} will be simulated first', style: T.ui(11.5, color: AppColors.textMute)),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                flex: 3,
                child: NeonButton(label: 'PLAY MATCH', icon: Icons.sports_cricket_rounded, height: 48, onTap: _busy ? null : _play),
              ),
              const SizedBox(width: 10),
              Expanded(
                flex: 2,
                child: GhostButton(label: 'SIMULATE', icon: Icons.fast_forward_rounded, height: 48, onTap: _busy ? null : _sim),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _side(TeamInfo t, int strength) => Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          TeamCrest(t, size: 76, glow: t.id == c.userTeamId),
          const SizedBox(height: 6),
          Text(t.id, style: T.display(26, weight: FontWeight.w700, height: 1)),
          Text('RATING $strength', style: T.label(9.5)),
        ],
      );

  Widget _seasonOverCard() {
    final elim = s.isEliminated(c.userTeamId);
    return GlassCard(
      blur: true,
      padding: const EdgeInsets.all(16),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(elim ? Icons.sentiment_dissatisfied_rounded : Icons.hourglass_bottom_rounded, color: AppColors.gold, size: 44),
          const SizedBox(height: 8),
          Text(elim ? 'KNOCKED OUT' : 'WAITING FOR RESULTS', style: T.display(30, weight: FontWeight.w700)),
          Text(elim ? 'Your campaign is over. Watch the rest of the season unfold.' : 'Simulate the other league matches to see if you make the playoffs.',
              textAlign: TextAlign.center, style: T.ui(13.5, color: AppColors.textDim)),
          const SizedBox(height: 14),
          NeonButton(label: elim ? 'SIMULATE TO THE FINAL' : 'SIMULATE OTHER MATCHES', icon: Icons.fast_forward_rounded, height: 48, onTap: _simRest),
        ],
      ),
    );
  }

  Widget _resultTile(Fixture f) {
    final r = f.result!;
    final won = r.winner == c.userTeamId;
    final first = teamById(r.firstBat), second = teamById(f.opponentOf(r.firstBat));
    return GlassCard(
      padding: const EdgeInsets.all(10),
      color: (won ? AppColors.green : AppColors.red).o(0.1),
      borderColor: (won ? AppColors.green : AppColors.red).o(0.5),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('LAST RESULT', style: T.label(10, color: won ? AppColors.green : AppColors.red)),
          const SizedBox(height: 4),
          Row(
            children: [
              TeamCrest(first, size: 24),
              const SizedBox(width: 6),
              Text('${first.id} ${r.r1}/${r.w1}', style: T.display(19, weight: FontWeight.w700)),
              Text(' (${oversText(r.b1)})', style: T.ui(11, color: AppColors.textDim)),
              const Spacer(),
              Text('${second.id} ${r.r2}/${r.w2}', style: T.display(19, weight: FontWeight.w700)),
              Text(' (${oversText(r.b2)})', style: T.ui(11, color: AppColors.textDim)),
              const SizedBox(width: 6),
              TeamCrest(second, size: 24),
            ],
          ),
          const SizedBox(height: 4),
          Text(r.text, style: T.ui(13.5, weight: FontWeight.w700, color: won ? AppColors.green : AppColors.red)),
          Text('Player of the Match: ${PlayerDb.instance.byId(r.potmId).name}', style: T.ui(11.5, color: AppColors.textDim)),
        ],
      ),
    );
  }

  Widget _miniRow(int pos, Standing st) {
    final t = teamById(st.teamId);
    final me = st.teamId == c.userTeamId;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        children: [
          SizedBox(width: 20, child: Text('$pos', style: T.display(17, color: pos <= 4 ? AppColors.gold : AppColors.textDim))),
          TeamCrest(t, size: 22),
          const SizedBox(width: 8),
          Expanded(child: Text(t.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: T.ui(13.5, weight: me ? FontWeight.w700 : FontWeight.w600, color: me ? AppColors.gold : AppColors.text))),
          Text('${st.p}', style: T.ui(12.5, color: AppColors.textDim)),
          const SizedBox(width: 12),
          SizedBox(width: 28, child: Text('${st.pts}', textAlign: TextAlign.right, style: T.display(18, weight: FontWeight.w700))),
        ],
      ),
    );
  }

  Widget _capCard(String title, Color color, int? playerId, String value) {
    final p = playerId == null ? null : PlayerDb.instance.byId(playerId);
    final owner = p == null ? null : _ownerOf(p.id);
    return GlassCard(
      padding: const EdgeInsets.all(10),
      borderColor: color.o(0.6),
      color: color.o(0.1),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: T.label(10, color: color)),
          const SizedBox(height: 4),
          if (p != null)
            Row(
              children: [
                PlayerAvatar(p, size: 30, team: owner),
                const SizedBox(width: 6),
                Expanded(child: Text(p.shortName, maxLines: 1, overflow: TextOverflow.ellipsis, style: T.ui(13, weight: FontWeight.w700, height: 1.1))),
              ],
            )
          else
            Text('Season not started', style: T.ui(12, color: AppColors.textDim)),
          Text(value, style: T.display(21, color: color, weight: FontWeight.w700, height: 1.1)),
        ],
      ),
    );
  }

  TeamInfo? _ownerOf(int pid) {
    for (final f in c.franchises.values) {
      if (f.contains(pid)) return f.info;
    }
    return null;
  }

  // ------------------------------------------------------------ table

  Widget _table(List<Standing> table) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(12, 2, 12, 12),
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
          child: Row(
            children: [
              SizedBox(width: 26, child: Text('#', style: T.label(10))),
              const SizedBox(width: 30),
              Expanded(child: Text('TEAM', style: T.label(10))),
              SizedBox(width: 90, child: Text('FORM', style: T.label(10))),
              for (final h in const ['P', 'W', 'L', 'NRR', 'PTS'])
                SizedBox(width: h == 'NRR' ? 64 : 44, child: Text(h, textAlign: TextAlign.center, style: T.label(10))),
            ],
          ),
        ),
        for (var i = 0; i < table.length; i++) ...[
          if (i == 4)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 2),
              child: Row(children: [
                Expanded(child: Container(height: 1, color: AppColors.gold.o(0.5))),
                Padding(padding: const EdgeInsets.symmetric(horizontal: 8), child: Text('PLAYOFF LINE', style: T.label(9, color: AppColors.gold))),
                Expanded(child: Container(height: 1, color: AppColors.gold.o(0.5))),
              ]),
            ),
          _tableRow(i + 1, table[i]),
        ],
      ],
    );
  }

  Widget _tableRow(int pos, Standing st) {
    final t = teamById(st.teamId);
    final me = st.teamId == c.userTeamId;
    return GlassCard(
      margin: const EdgeInsets.only(bottom: 4),
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 5),
      gradient: LinearGradient(colors: [t.primary.o(me ? 0.5 : 0.25), const Color(0xD9070A1E)]),
      borderColor: me ? AppColors.gold : AppColors.strokeSoft,
      child: Row(
        children: [
          SizedBox(width: 26, child: Text('$pos', style: T.display(19, color: pos <= 4 ? AppColors.gold : AppColors.textDim))),
          TeamCrest(t, size: 24),
          const SizedBox(width: 6),
          Expanded(
            child: Text(t.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: T.ui(14, weight: FontWeight.w700, color: me ? AppColors.gold : AppColors.text)),
          ),
          SizedBox(
            width: 90,
            child: Row(
              children: [
                for (final r in st.form.reversed.take(5).toList().reversed)
                  Container(
                    width: 14,
                    height: 14,
                    margin: const EdgeInsets.only(right: 3),
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color: r == 'W' ? AppColors.green : (r == 'L' ? AppColors.red : AppColors.textMute),
                    ),
                    child: Text(r, style: T.ui(8.5, color: Colors.white, weight: FontWeight.w700)),
                  ),
              ],
            ),
          ),
          for (final v in [st.p, st.w, st.l]) SizedBox(width: 44, child: Text('$v', textAlign: TextAlign.center, style: T.ui(14))),
          SizedBox(
            width: 64,
            child: Text('${st.nrr >= 0 ? '+' : ''}${st.nrr.toStringAsFixed(2)}', textAlign: TextAlign.center, style: T.ui(13, color: st.nrr >= 0 ? AppColors.green : AppColors.red)),
          ),
          SizedBox(width: 44, child: Text('${st.pts}', textAlign: TextAlign.center, style: T.display(21, weight: FontWeight.w700))),
        ],
      ),
    );
  }

  // --------------------------------------------------------- fixtures

  Widget _fixtures() {
    final list = s.fixtures.where((f) => !_myFixturesOnly || f.involves(c.userTeamId)).toList();
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 0, 12, 0),
          child: Row(
            children: [
              Text(_myFixturesOnly ? 'MY FIXTURES' : 'ALL ${s.fixtures.length} FIXTURES', style: T.label(11)),
              const Spacer(),
              Text('MINE ONLY', style: T.label(10)),
              Switch(value: _myFixturesOnly, activeThumbColor: AppColors.gold, onChanged: (v) => setState(() => _myFixturesOnly = v)),
            ],
          ),
        ),
        Expanded(
          child: GridView.builder(
            padding: const EdgeInsets.fromLTRB(12, 0, 12, 12),
            gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(crossAxisCount: 2, mainAxisExtent: 48, mainAxisSpacing: 6, crossAxisSpacing: 8),
            itemCount: list.length,
            itemBuilder: (context, i) {
              final f = list[i];
              final h = teamById(f.home), a = teamById(f.away);
              final r = f.result;
              final mine = f.involves(c.userTeamId);
              return GlassCard(
                padding: const EdgeInsets.fromLTRB(10, 4, 10, 4),
                color: const Color(0xB30B1030),
                borderColor: f.kind.isPlayoff ? AppColors.pink.o(0.6) : (mine ? AppColors.gold.o(0.35) : AppColors.strokeSoft),
                child: Row(
                  children: [
                    SizedBox(
                      width: 44,
                      child: Text(f.kind.isPlayoff ? f.kind.label.toUpperCase() : 'M${f.no}', maxLines: 2, style: T.label(9.5, color: f.kind.isPlayoff ? AppColors.pink : AppColors.textDim)),
                    ),
                    TeamCrest(h, size: 22),
                    const SizedBox(width: 4),
                    Text(h.id, style: T.display(17, weight: r?.winner == h.id ? FontWeight.w700 : FontWeight.w500, color: r != null && r.winner != h.id ? AppColors.textMute : AppColors.text)),
                    Text('  v  ', style: T.ui(11, color: AppColors.textMute)),
                    Text(a.id, style: T.display(17, weight: r?.winner == a.id ? FontWeight.w700 : FontWeight.w500, color: r != null && r.winner != a.id ? AppColors.textMute : AppColors.text)),
                    const SizedBox(width: 4),
                    TeamCrest(a, size: 22),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        r == null ? '${teamById(f.venueId).venueShort} • ${f.startTime.replaceAll(' IST', '')}' : r.text,
                        maxLines: 2,
                        textAlign: TextAlign.right,
                        style: T.ui(11, color: r == null ? AppColors.textMute : AppColors.textDim, height: 1.1),
                      ),
                    ),
                  ],
                ),
              );
            },
          ),
        ),
      ],
    );
  }

  // ------------------------------------------------------------ stats

  Widget _stats() {
    final db = PlayerDb.instance;
    Widget board(
      String title,
      Color color,
      List<MapEntry<int, PlayerSeasonStats>> rows,
      String Function(PlayerSeasonStats) value,
      String Function(PlayerSeasonStats) sub,
    ) {
      return GlassCard(
        margin: const EdgeInsets.only(bottom: 10),
        padding: const EdgeInsets.all(12),
        borderColor: color.o(0.5),
        color: const Color(0xB30B1030),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: T.display(21, color: color, weight: FontWeight.w700)),
            const SizedBox(height: 4),
            if (rows.isEmpty) Text('No matches played yet', style: T.ui(13, color: AppColors.textDim)),
            for (var i = 0; i < rows.length; i++)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 3),
                child: Row(
                  children: [
                    SizedBox(width: 22, child: Text('${i + 1}', style: T.display(16, color: i == 0 ? color : AppColors.textDim))),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(db.byId(rows[i].key).name, maxLines: 1, overflow: TextOverflow.ellipsis, style: T.ui(13.5, weight: FontWeight.w700)),
                          Text('${_ownerOf(rows[i].key)?.id ?? '-'} • ${sub(rows[i].value)}', maxLines: 1, style: T.ui(10.5, color: AppColors.textDim)),
                        ],
                      ),
                    ),
                    Text(value(rows[i].value), style: T.display(20, weight: FontWeight.w700, color: i == 0 ? color : AppColors.text)),
                  ],
                ),
              ),
          ],
        ),
      );
    }

    final runs = s.leaders((x) => x.runs.toDouble());
    final wkts = s.leaders((x) => x.wickets * 1000.0 - x.economy);
    final sixes = s.leaders((x) => x.sixes.toDouble(), n: 5);
    final mvp = s.leaders((x) => x.mvp, n: 5);
    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(12, 2, 12, 12),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              board('ORANGE CAP', const Color(0xFFFF9F1C), runs, (v) => '${v.runs}', (v) => 'Avg ${v.average.toStringAsFixed(1)} • SR ${v.strikeRate.toStringAsFixed(0)} • HS ${v.high}${v.highNotOut ? '*' : ''}'),
              board('MOST SIXES', AppColors.gold, sixes, (v) => '${v.sixes}', (v) => '${v.runs} runs'),
            ]),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              board('PURPLE CAP', const Color(0xFFB57BFF), wkts, (v) => '${v.wickets}', (v) => 'Econ ${v.economy.toStringAsFixed(2)} • Best ${v.bestW}/${v.bestR}'),
              board('MOST VALUABLE PLAYER', AppColors.cyan, mvp, (v) => v.mvp.toStringAsFixed(0), (v) => '${v.runs} runs • ${v.wickets} wkts'),
            ]),
          ),
        ],
      ),
    );
  }
}
