import 'package:flutter/material.dart';

import '../../core/league.dart';
import '../../core/theme.dart';
import '../../data/players_db.dart';
import '../../engine/career.dart';
import '../../models/franchise.dart';
import '../../models/player.dart';
import '../widgets/common.dart';
import '../widgets/crest.dart';

/// Playing XI, batting order, captain and keeper management.
class SquadPanel extends StatefulWidget {
  const SquadPanel({super.key, this.onChanged});

  final VoidCallback? onChanged;

  @override
  State<SquadPanel> createState() => _SquadPanelState();
}

class _SquadPanelState extends State<SquadPanel> {
  final Career c = Career.instance;
  late List<int> _xi;
  int? _captain;
  int? _keeper;
  bool _dirty = false;

  @override
  void initState() {
    super.initState();
    c.user.ensureLineup();
    _xi = List.of(c.user.xi);
    _captain = c.user.captainId;
    _keeper = c.user.keeperId;
  }

  List<Player> get _xiPlayers => _xi.map(PlayerDb.instance.byId).toList();

  List<Player> get _bench => c.user.players.where((p) => !_xi.contains(p.id)).toList()
    ..sort((a, b) => b.overall.compareTo(a.overall));

  void _toggle(Player p) {
    setState(() {
      if (_xi.contains(p.id)) {
        _xi.remove(p.id);
      } else if (_xi.length < 11) {
        _xi.add(p.id);
      } else {
        showToast(context, 'Your XI is full - remove a player first');
        return;
      }
      _dirty = true;
    });
  }

  void _auto() {
    final best = pickBestXI(c.user.players);
    setState(() {
      _xi = best.map((p) => p.id).toList();
      _captain = chooseCaptain(best, c.userTeam).id;
      _keeper = chooseKeeper(best).id;
      _dirty = true;
    });
  }

  void _save() {
    final xi = _xiPlayers;
    final problem = lineupProblem(xi);
    if (problem != null) {
      showToast(context, problem, color: AppColors.red.darken(0.3));
      return;
    }
    final f = c.user;
    f.xi = List.of(_xi);
    f.captainId = (_captain != null && _xi.contains(_captain)) ? _captain : chooseCaptain(xi, c.userTeam).id;
    final k = _keeper == null ? null : PlayerDb.instance.byId(_keeper!);
    f.keeperId = (k != null && _xi.contains(k.id) && k.role == Role.wk) ? k.id : chooseKeeper(xi).id;
    c.save();
    setState(() => _dirty = false);
    widget.onChanged?.call();
    showToast(context, 'Playing XI saved', color: const Color(0xFF0E7A52));
  }

  @override
  Widget build(BuildContext context) {
    final xi = _xiPlayers;
    final os = xi.where((p) => p.overseas).length;
    final problem = lineupProblem(xi);
    final warn = problem == null ? lineupWarning(xi) : null;
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(12, 0, 12, 6),
          child: GlassCard(
            padding: const EdgeInsets.fromLTRB(12, 6, 8, 6),
            borderColor: problem == null ? AppColors.green.o(0.5) : AppColors.red.o(0.6),
            child: Row(
              children: [
                _chip('XI', '${xi.length}/11', xi.length == 11),
                _chip('OVERSEAS', '$os/${League.maxOverseasXI}', os <= League.maxOverseasXI),
                _chip('RATING', '${xi.length == 11 ? teamRating(xi) : '-'}', true),
                Expanded(
                  child: problem != null || warn != null
                      ? Row(
                          children: [
                            Icon(problem != null ? Icons.error_rounded : Icons.info_rounded, size: 16, color: problem != null ? AppColors.red : AppColors.gold),
                            const SizedBox(width: 6),
                            Expanded(
                              child: Text(problem ?? warn!, maxLines: 2, overflow: TextOverflow.ellipsis, style: T.ui(12, color: problem != null ? AppColors.red : AppColors.goldLight)),
                            ),
                          ],
                        )
                      : const SizedBox.shrink(),
                ),
                const SizedBox(width: 8),
                GhostButton(label: 'AUTO', icon: Icons.auto_awesome_rounded, expand: false, height: 38, color: AppColors.cyan, onTap: _auto),
                const SizedBox(width: 8),
                SizedBox(
                  width: 92,
                  child: NeonButton(label: 'SAVE', height: 38, fontSize: 19, onTap: _dirty ? _save : null),
                ),
              ],
            ),
          ),
        ),
        Expanded(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Padding(
                      padding: const EdgeInsets.fromLTRB(16, 0, 12, 4),
                      child: Text('PLAYING XI • HOLD & DRAG FOR BATTING ORDER • TAP C / WK', style: T.label(10)),
                    ),
                    Expanded(
                      child: ReorderableListView.builder(
                        padding: const EdgeInsets.fromLTRB(12, 0, 6, 12),
                        buildDefaultDragHandles: false,
                        itemCount: xi.length,
                        onReorder: (from, to) {
                          setState(() {
                            if (to > from) to--;
                            final id = _xi.removeAt(from);
                            _xi.insert(to, id);
                            _dirty = true;
                          });
                        },
                        itemBuilder: (context, i) {
                          final p = xi[i];
                          return ReorderableDelayedDragStartListener(
                            key: ValueKey(p.id),
                            index: i,
                            child: _row(p, i + 1),
                          );
                        },
                      ),
                    ),
                  ],
                ),
              ),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Padding(
                      padding: const EdgeInsets.fromLTRB(10, 0, 12, 4),
                      child: Text('BENCH • TAP + TO ADD', style: T.label(10)),
                    ),
                    Expanded(
                      child: ListView(
                        padding: const EdgeInsets.fromLTRB(6, 0, 12, 12),
                        children: [for (final p in _bench) _row(p, null)],
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }

  Widget _chip(String label, String v, bool ok) => Padding(
        padding: const EdgeInsets.only(right: 14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(label, style: T.label(9)),
            Text(v, style: T.display(21, weight: FontWeight.w700, color: ok ? AppColors.text : AppColors.red, height: 1.05)),
          ],
        ),
      );

  Widget _row(Player p, int? order) {
    final inXi = order != null;
    final isCap = p.id == _captain;
    final isWk = p.id == _keeper;
    return Container(
      margin: const EdgeInsets.only(bottom: 6),
      padding: const EdgeInsets.fromLTRB(6, 6, 6, 6),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(14),
        color: inXi ? c.userTeam.primary.o(0.28) : const Color(0xB30B1030),
        border: Border.all(color: inXi ? c.userTeam.glow.o(0.45) : AppColors.strokeSoft),
      ),
      child: Row(
        children: [
          SizedBox(
            width: 26,
            child: inXi
                ? Text('$order', textAlign: TextAlign.center, style: T.display(20, color: AppColors.gold, weight: FontWeight.w700))
                : const Icon(Icons.chair_rounded, size: 16, color: AppColors.textMute),
          ),
          PlayerAvatar(p, size: 38, team: c.userTeam),
          const SizedBox(width: 8),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(p.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: T.ui(14.5, weight: FontWeight.w700)),
                Row(
                  children: [
                    RoleBadge(p.role, size: 9),
                    const SizedBox(width: 4),
                    if (p.overseas) ...[
                      const Icon(Icons.flight_rounded, size: 13, color: AppColors.cyan),
                      const SizedBox(width: 4),
                    ],
                    Flexible(
                      child: Text('BAT ${p.bat} • BOWL ${p.bowl}', maxLines: 1, style: T.ui(11, color: AppColors.textDim)),
                    ),
                  ],
                ),
              ],
            ),
          ),
          if (inXi) ...[
            _tag('C', isCap, AppColors.gold, () => setState(() {
                  _captain = p.id;
                  _dirty = true;
                })),
            if (p.role == Role.wk)
              _tag('WK', isWk, roleColor(Role.wk), () => setState(() {
                    _keeper = p.id;
                    _dirty = true;
                  })),
          ],
          const SizedBox(width: 4),
          RatingBadge(p.overall, size: 30),
          IconButton(
            visualDensity: VisualDensity.compact,
            onPressed: () => _toggle(p),
            icon: Icon(inXi ? Icons.remove_circle_rounded : Icons.add_circle_rounded, color: inXi ? AppColors.red : AppColors.green),
          ),
        ],
      ),
    );
  }

  Widget _tag(String t, bool on, Color color, VoidCallback onTap) => GestureDetector(
        onTap: onTap,
        child: Container(
          margin: const EdgeInsets.only(right: 4),
          padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(6),
            color: on ? color : Colors.transparent,
            border: Border.all(color: color.o(on ? 1 : 0.4)),
          ),
          child: Text(t, style: T.label(10, color: on ? color.onColor : color.o(0.7))),
        ),
      );
}
