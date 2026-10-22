import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';

import '../../core/theme.dart';
import '../../data/teams.dart';
import '../../engine/career.dart';
import '../../engine/delivery.dart';
import '../../engine/match_engine.dart';
import '../../game/ball_path.dart';
import '../../game/camera.dart';
import '../../game/character.dart';
import '../../game/ground.dart';
import '../../game/scene.dart';
import '../../game/stadium_mesh.dart';
import '../../models/player.dart';
import '../widgets/common.dart';
import '../widgets/crest.dart';
import '../widgets/effects.dart';
import '../widgets/paint_utils.dart';
import 'match_result_screen.dart';
import 'match_widgets.dart';

enum _P { toss, ready, runUp, flight, carry, follow, result, overBreak, inningsBreak, done }

const _paceVariations = ['STOCK', 'SLOWER BALL', 'IN-SWINGER', 'OUT-SWINGER'];
const _spinVariations = ['STOCK', 'QUICKER', 'MYSTERY'];

class MatchScreen extends StatefulWidget {
  const MatchScreen({super.key, required this.engine, required this.userTeamId, required this.title, this.day = true, this.onFinished});

  final MatchEngine engine;
  final String userTeamId;
  final String title;

  /// Afternoon game in sunlight, or a night game under the floodlights.
  final bool day;
  final void Function(MatchEngine m)? onFinished;

  @override
  State<MatchScreen> createState() => _MatchScreenState();
}

class _MatchScreenState extends State<MatchScreen> with SingleTickerProviderStateMixin {
  MatchEngine get m => widget.engine;
  late final Ticker _ticker = createTicker(_tick);
  final _repaint = ValueNotifier<int>(0);
  late final StadiumMesh _mesh = StadiumMesh(
    home: m.venue,
    away: m.venue.id == m.home.team.id ? m.away.team : m.home.team,
    day: widget.day,
  );
  final _ground = GroundRenderer();
  final _s = SceneState();
  Size _size = const Size(900, 410);
  Camera3D? _cam;

  _P _phase = _P.toss;
  double _t = 0;
  double _clock = 0;
  double _hype = 0.25;
  Duration _last = Duration.zero;

  // Ball state.
  DeliveryPlan? _plan;
  DeliveryPath? _path;
  BallResult? _result;
  ShotPlan? _shot;
  Vec3? _ballPos;
  double _releaseClock = 0;
  double _carryStartFt = 0;
  double? _pressClock;
  ShotDir? _pressDir;
  bool _pressDefend = false;
  bool _pressLoft = false;
  bool _loft = false;
  bool _carryHit = false;
  bool _stumpsDone = false;
  TimingGrade? _grade;

  // Actors.
  double _bowlerX = -1.2;
  double _bowlerY = -10;
  Pose _bowlerPose = pRunA;
  Pose _batterPose = pBatStance;
  Pose _keeperPose = pKeeperCrouch;
  Pose _umpPose = pUmpire;
  double _nsY = 0.9;
  double _walk = 0;
  FieldSetting _field = fieldFor(phase: 0, spin: false, leftBatter: false);

  // Follow camera.
  bool _following = false;
  double _followStartClock = 0;
  Vec3 _followStart = Vec3(0, 0, 0);
  Vec3 _followEnd = Vec3(0, 0, 0);
  Vec3 _followPos = Vec3(0, 0, 0);
  Vec3 _look = Vec3(0, 0, 0);
  double _followFocal = 400;
  double _shake = 0;

  // Bowling controls.
  Vec3 _aim = const Vec3(-0.12, 14.1, 0.01);
  int _variation = 0;
  bool _meterOn = false;
  double _meterT = 0;
  bool _autoBowl = false;
  final List<DeliveryType> _recent = [];
  bool _pickingBowler = false;
  bool _draggingAim = false;

  /// 0 = wide broadcast view, 1 = close-up on the pitch while aiming.
  double _aimZoom = 0;
  double _lastAimTouch = -10;

  // Swipe.
  Offset? _swipeStart;
  double _swipeStartClock = 0;
  bool _swipeFired = false;

  // UI.
  String? _banner;
  String? _bannerSub;
  Color _bannerColor = AppColors.gold;
  int _bannerKey = 0;
  int _confetti = 0;
  String? _overToast;
  int _inningsCount = 1;
  bool _finishedCalled = false;
  bool _resultOpen = false;
  String? _speedGun;

  // Toss.
  bool? _call;
  bool _flipping = false;
  double _flipT = 0;
  bool _awaitChoice = false;
  String? _tossText;

  bool get userBatting => m.started && !m.finished && m.current.batting.team.id == widget.userTeamId;
  bool get userBowling => m.started && !m.finished && m.current.bowling.team.id == widget.userTeamId;
  TeamInfo get userTeam => m.sheetOf(widget.userTeamId).team;
  TeamInfo get oppTeam => (m.home.team.id == widget.userTeamId ? m.away : m.home).team;
  bool get _hints => Career.instance.settings.timingHint;
  double get _hand => (m.started ? m.striker : m.home.order.first).leftBat ? -1.0 : 1.0;

  @override
  void initState() {
    super.initState();
    _s.camFn = _camera;
    _mesh.prepare().then((_) {
      if (mounted) setState(() {});
    });
    GroundRenderer.load();
    _ticker.start();
  }

  @override
  void dispose() {
    _ticker.dispose();
    _repaint.dispose();
    _mesh.dispose();
    _ground.dispose();
    super.dispose();
  }

  // ========================================================== cameras

  Camera3D _baseCam(Size size) {
    if (userBatting) return Rigs.batting(size, leftHanded: m.striker.leftBat);
    return Rigs.bowling(size);
  }

  Vec3 _baseLook() => userBatting ? Rigs.battingLook(m.striker.leftBat) : Rigs.bowlingLook;
  Vec3 _basePos() => userBatting ? Rigs.battingPos(m.striker.leftBat) : Rigs.bowlingPos;

  Camera3D _camera(Size size) {
    _size = size;
    var base = _baseCam(size);
    if (!userBatting && _aimZoom > 0.001) base = Camera3D.lerp(base, Rigs.aim(size), easeInOut(_aimZoom));
    var cam = base;
    if (_following) {
      final f = Camera3D.lookAt(_followPos, _look, focal: _followFocal, cx: size.width / 2, cy: size.height * 0.5);
      cam = Camera3D.lerp(base, f, easeInOut(clamp01((_clock - _followStartClock) / 0.5)));
    }
    _cam = cam.shaken(_shake, _clock);
    return _cam!;
  }

  double get _shortSide => math.min(_size.height, _size.width * 0.6);

  void _updateFollow(double dt) {
    if (!_following) return;
    final tf = _clock - _followStartClock;
    final b = _ballPos ?? _look;
    final want = Vec3(b.x, b.y, math.min(b.z, 8) * 0.55);
    _look = Vec3.lerp(_look, want, 1 - math.exp(-dt * 3.4));
    _followPos = Vec3.lerp(_followStart, _followEnd, easeInOut(clamp01(tf / 1.3)));
    final d = _followPos.distTo(_look);
    final h = _shortSide;
    final wantF = (h * d / 42).clamp(h * 0.95, h * 2.7);
    _followFocal += (wantF - _followFocal) * (1 - math.exp(-dt * 2.4));
  }

  // =========================================================== ticker

  void _tick(Duration elapsed) {
    final dt = _last == Duration.zero ? 0.016 : ((elapsed - _last).inMicroseconds / 1e6).clamp(0.0, 0.05);
    _last = elapsed;
    _clock += dt;
    _t += dt;
    _hype = math.max(0.15, _hype - dt * 0.3);
    _shake = math.max(0, _shake - dt * 1.6);
    _s.flash = math.max(0, _s.flash - dt * 1.6);
    if (_s.farStumpsHit >= 0) _s.farStumpsHit += dt;
    if (_s.nearStumpsHit >= 0) _s.nearStumpsHit += dt;

    switch (_phase) {
      case _P.toss:
        if (_flipping) {
          _flipT += dt;
          setState(() {});
          if (_flipT > 1.6) _resolveToss();
        }
      case _P.ready:
        if (_meterOn) {
          _meterT += dt;
          setState(() {});
        }
        // Idle bat taps while waiting for the bowler.
        _batterPose = Pose.lerp(pBatStance, pBatBacklift, 0.05 + 0.05 * math.sin(_clock * 5));
        if (_banner != null && _t > 1.8) setState(() => _banner = null);
        if (!_pickingBowler && m.started && !m.finished && m.current.bowlerId != null) {
          if (userBatting && _t > 1.0) _startDelivery();
          if (userBowling && _autoBowl && _t > 0.7) _startDelivery();
        }
      case _P.runUp:
        _tickRunUp();
      case _P.flight:
        _tickFlight();
      case _P.carry:
        _tickCarry();
      case _P.follow:
        _tickFollow();
      case _P.result:
        _tickResult();
      case _P.overBreak:
        if (_t > 1.6) {
          _overToast = null;
          _enterReady();
        }
      case _P.inningsBreak:
      case _P.done:
        break;
    }
    if (_pressClock != null && _phase != _P.ready) _batterPose = _swingPose(_clock - _pressClock!);
    final aiming = userBowling && _phase == _P.ready && !_autoBowl && (_draggingAim || _clock - _lastAimTouch < 0.9);
    _aimZoom += ((aiming ? 1.0 : 0.0) - _aimZoom) * (1 - math.exp(-dt * 7));
    _updateFollow(dt);
    _s.swipe.removeWhere((p) => _clock - p.$2 > 0.5);
    _syncScene();
    _repaint.value++;
  }

  // ============================================================= toss

  void _callToss(bool heads) {
    if (_flipping) return;
    setState(() {
      _call = heads;
      _flipping = true;
      _flipT = 0;
    });
  }

  void _resolveToss() {
    _flipping = false;
    final headsUp = m.rng.chance(0.5);
    if (headsUp == _call) {
      setState(() {
        _awaitChoice = true;
        _tossText = '${headsUp ? 'HEADS' : 'TAILS'}! You won the toss';
      });
    } else {
      final bat = m.aiElectBat();
      setState(() => _tossText = '${headsUp ? 'HEADS' : 'TAILS'}! ${oppTeam.id} won the toss and chose to ${bat ? 'bat' : 'bowl'}');
      Future.delayed(const Duration(milliseconds: 1700), () {
        if (!mounted) return;
        m.setToss(oppTeam.id, bat);
        _startMatch();
      });
    }
  }

  void _choose(bool bat) {
    m.setToss(widget.userTeamId, bat);
    _startMatch();
  }

  void _startMatch() {
    _inningsCount = m.innings.length;
    _showBanner('PLAY!', AppColors.gold, sub: m.tossText);
    _enterReady();
  }

  // ======================================================= ball cycle

  void _enterReady() {
    _phase = _P.ready;
    _t = 0;
    _following = false;
    _plan = null;
    _path = null;
    _result = null;
    _shot = null;
    _ballPos = null;
    _pressClock = null;
    _pressDir = null;
    _pressDefend = false;
    _meterOn = false;
    _stumpsDone = false;
    _speedGun = null;
    _walk = 0;
    _nsY = 0.9;
    _umpPose = pUmpire;
    _keeperPose = pKeeperCrouch;
    _batterPose = pBatStance;
    _s
      ..marker = null
      ..markerAlpha = 0
      ..farStumpsHit = -1
      ..nearStumpsHit = -1
      ..ring = null
      ..screen = ScreenEvent.none;
    _s.trail.clear();
    if (m.finished) {
      _enterDone();
      return;
    }
    final pending = _pendingSim;
    if (pending != null) {
      _sim(pending);
      return;
    }
    if (m.needBowler) {
      if (userBowling && !_autoBowl) {
        _pickingBowler = true;
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (mounted) _pickBowler();
        });
        setState(() {});
        return;
      }
      m.autoBowlerIfNeeded();
    }
    _setupBall();
  }

  void _setupBall() {
    final inn = m.current;
    final bowler = m.bowler;
    _field = fieldFor(phase: inn.phase, spin: bowler.isSpin, leftBatter: m.striker.leftBat, boundary: m.venue.boundary.toDouble());
    final side = bowler.bowlStyle.leftArm ? 1.0 : -1.0;
    _bowlerX = side * (bowler.isSpin ? 0.85 : 1.2);
    _bowlerY = bowler.isSpin ? -3.6 : -6.2;
    _bowlerPose = pRunA;
    _s.stumpsColor = inn.bowling.team.primary;
    if (_variation >= (bowler.isSpin ? _spinVariations : _paceVariations).length) _variation = 0;
    setState(() {});
  }

  Future<void> _pickBowler() async {
    _pickingBowler = true;
    final suggested = m.suggestBowler();
    final opts = m.eligibleBowlers()
      ..sort((a, b) {
        if (a.id == suggested.id) return -1;
        if (b.id == suggested.id) return 1;
        return b.bowl.compareTo(a.bowl);
      });
    final inn = m.current;
    final picked = await showDialog<Player>(
      context: context,
      barrierDismissible: false,
      builder: (ctx) => Dialog(
        backgroundColor: AppColors.bg1,
        insetPadding: const EdgeInsets.symmetric(horizontal: 40, vertical: 20),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20), side: BorderSide(color: AppColors.gold.o(0.5))),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 720),
          child: Padding(
            padding: const EdgeInsets.all(14),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text('OVER ${inn.overIndex + 1} • PICK YOUR BOWLER', style: T.display(22, weight: FontWeight.w700)),
                const SizedBox(height: 8),
                Flexible(
                  child: GridView.count(
                    shrinkWrap: true,
                    crossAxisCount: 2,
                    childAspectRatio: 4.2,
                    mainAxisSpacing: 6,
                    crossAxisSpacing: 6,
                    children: [
                      for (final p in opts)
                        GlassCard(
                          padding: const EdgeInsets.fromLTRB(6, 4, 10, 4),
                          onTap: () => Navigator.pop(ctx, p),
                          borderColor: p.id == suggested.id ? AppColors.gold : AppColors.strokeSoft,
                          color: const Color(0xCC0B1030),
                          child: Row(
                            children: [
                              PlayerAvatar(p, size: 34, team: inn.bowling.team),
                              const SizedBox(width: 8),
                              Expanded(
                                child: Column(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(p.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: T.ui(13.5, weight: FontWeight.w700)),
                                    Text(p.id == suggested.id ? 'SUGGESTED • ${p.bowlStyle.code}' : p.bowlStyle.label, maxLines: 1, style: T.ui(10.5, color: p.id == suggested.id ? AppColors.gold : AppColors.textDim)),
                                  ],
                                ),
                              ),
                              Column(
                                mainAxisAlignment: MainAxisAlignment.center,
                                crossAxisAlignment: CrossAxisAlignment.end,
                                children: [
                                  Text(inn.bowl[p.id]?.figures ?? '0-0-0-0', style: T.display(17, weight: FontWeight.w700, height: 1)),
                                  Text('BOWL ${p.bowl}', style: T.label(8.5, color: p.bowl >= 70 ? AppColors.green : AppColors.textMute)),
                                ],
                              ),
                            ],
                          ),
                        ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
    if (!mounted) return;
    _pickingBowler = false;
    m.setBowler((picked ?? suggested).id);
    _setupBall();
  }

  // --------------------------------------------------- bowling (user)

  (DeliveryType, BallLine) _aimToDelivery() {
    final bowler = m.bowler;
    final lat = _aim.x * _hand;
    final line = lat < -0.45
        ? BallLine.wideOff
        : (lat < -0.06 ? BallLine.off : (lat < 0.07 ? BallLine.middle : BallLine.leg));
    final len = kPitchLength - _aim.y;
    DeliveryType t;
    if (bowler.isSpin) {
      t = len < 3.9 ? DeliveryType.flighted : (len < 7.2 ? DeliveryType.stock : DeliveryType.quicker);
      if (_variation == 1) t = DeliveryType.quicker;
      if (_variation == 2) t = DeliveryType.variation;
    } else {
      t = len < 2.0
          ? (line == BallLine.wideOff ? DeliveryType.wideYorker : DeliveryType.yorker)
          : len < 4.5
              ? DeliveryType.full
              : len < 7.2
                  ? DeliveryType.goodLength
                  : len < 9.6
                      ? DeliveryType.short
                      : DeliveryType.bouncer;
      if (_variation == 1) t = DeliveryType.slower;
    }
    return (t, line);
  }

  double? _swingOverride() {
    if (m.bowler.isSpin) return null;
    if (_variation == 2) return 0.27 * _hand;
    if (_variation == 3) return -0.27 * _hand;
    return null;
  }

  void _moveAim(Offset p) {
    final cam = _cam ?? Rigs.bowling(_size);
    final g = cam.groundAt(p);
    _lastAimTouch = _clock;
    if (g == null) return;
    setState(() => _aim = Vec3(g.x.clamp(-1.3, 1.3), g.y.clamp(kPitchLength - 12.0, kPitchLength - 0.4), 0.01));
  }

  void _onBowlButton() {
    if (_phase != _P.ready || !userBowling || _pickingBowler) return;
    if (!_meterOn) {
      setState(() {
        _meterOn = true;
        _meterT = 0;
      });
      return;
    }
    final accuracy = (1 - (_meterValue - 0.5).abs() * 2).clamp(0.0, 1.0);
    final (type, line) = _aimToDelivery();
    _plan = planUserDelivery(
      m.rng,
      m.bowler,
      type,
      line,
      accuracy,
      m.current.phase,
      spinFactor: m.venue.spinFactor,
      aimY: _aim.y,
      aimX: _aim.x,
      swing: _swingOverride(),
    );
    _meterOn = false;
    haptic(Buzz.medium);
    _startDelivery();
  }

  double get _meterValue {
    final speed = switch (m.difficulty) {
      Difficulty.easy => 3.0,
      Difficulty.normal => 4.1,
      Difficulty.hard => 5.4,
    };
    return 0.5 + 0.5 * math.sin(_meterT * speed - math.pi / 2);
  }

  // ------------------------------------------------------- delivery

  void _startDelivery() {
    final inn = m.current;
    final bowler = m.bowler;
    if (userBatting) {
      _plan = planAiDelivery(m.rng, bowler, inn.phase, spinFactor: m.venue.spinFactor, accuracyBonus: m.difficulty.aiBowlBonus);
    } else if (userBowling) {
      if (_autoBowl || _plan == null) _plan = planAiDelivery(m.rng, bowler, inn.phase, spinFactor: m.venue.spinFactor);
      _result = m.resolveUserBowling(_plan!, _recent);
      _recent.insert(0, _plan!.type);
      if (_recent.length > 6) _recent.removeLast();
    }
    _path = DeliveryPath(plan: _plan!, batterLeft: m.striker.leftBat, slowMo: m.difficulty.slowMo, keeperY: _field.keeper.y);
    _phase = _P.runUp;
    _t = 0;
    setState(() {});
  }

  double get _runUpDur => m.bowler.isSpin ? 0.8 : 1.15;

  void _tickRunUp() {
    final u = clamp01(_t / _runUpDur);
    final spin = m.bowler.isSpin;
    final startY = spin ? -3.6 : -6.2;
    const endY = 0.3;
    final side = m.bowler.bowlStyle.leftArm ? 1.0 : -1.0;
    const runEnd = 0.7;
    if (u < runEnd) {
      final k = u / runEnd;
      _bowlerY = lerpD(startY, endY - 1.0, k);
      _bowlerX = side * lerpD(spin ? 0.85 : 1.2, 0.62, k);
      _bowlerPose = Pose.lerp(pRunA, pRunB, 0.5 + 0.5 * math.sin(_t * (spin ? 9.0 : 11.5)));
    } else {
      final k = (u - runEnd) / (1 - runEnd);
      _bowlerY = lerpD(endY - 1.0, endY, k);
      _bowlerX = side * 0.55;
      _bowlerPose = samplePoses([(0, pGather), (0.45, pLoad), (0.85, pRelease), (1.0, pRelease)], k);
    }
    _nsY = lerpD(0.9, 1.7, u);
    _walk = 1.8 * u;
    if (u >= 1) {
      _phase = _P.flight;
      _t = 0;
      _releaseClock = _clock;
      final p = _path!;
      if (userBatting && _hints && p.bounces && !_plan!.wide) {
        _s
          ..marker = Vec3(p.bounceX, p.bounceY, 0.01)
          ..markerAlpha = 1
          ..markerColor = AppColors.cyan;
      }
      _speedGun = '${_plan!.speed.toStringAsFixed(1)} km/h';
      setState(() {});
    }
  }

  void _pushTrail(Vec3 b, int n) {
    _s.trail.add(b);
    while (_s.trail.length > n) {
      _s.trail.removeAt(0);
    }
  }

  void _tickFlight() {
    final p = _path!;
    final ft = _clock - _releaseClock;
    final b = p.at(ft);
    _ballPos = b;
    _pushTrail(b, 7);
    _bowlerPose = samplePoses([(0, pRelease), (0.28, pFollow)], ft);
    _bowlerY = 0.3 + math.min(ft, 0.7) * 2.0;
    _nsY = 1.7 + math.min(ft, 0.6);
    if (_s.marker != null) _s.markerAlpha = ft < p.tBounce ? 1 : clamp01(1 - (ft - p.tBounce) * 3);
    if (_pressClock == null) {
      _batterPose = Pose.lerp(pBatStance, pBatBacklift, clamp01(ft / 0.35));
      _s.ring = (userBatting && _hints) ? clamp01((p.tArrive - ft) / p.tArrive) : null;
    } else {
      _s.ring = null;
    }
    if (ft > p.tArrive - 0.2) _keeperPose = Pose.lerp(pKeeperCrouch, pKeeperUp, clamp01((ft - p.tArrive + 0.2) / 0.3));
    if (userBowling && _pressClock == null && ft >= p.tArrive - 0.1) _aiSwing(_result!);

    if (userBatting) {
      final late = p.tArrive + m.difficulty.windows.poor / 1000;
      if (_pressClock != null && ft >= p.tArrive) {
        _beginAfterContact(ft);
      } else if (_pressClock == null && ft >= late) {
        _result = m.resolveUserShot(_plan!, const ShotInput());
        _grade = null;
        _beginAfterContact(ft);
      }
    } else if (ft >= p.tArrive) {
      _beginAfterContact(ft);
    }
  }

  ShotDir _nearestDir(double angle) {
    var best = ShotDir.straight;
    var bd = 1e9;
    for (final d in ShotDir.values) {
      var diff = (d.angle - angle).abs();
      if (diff > 180) diff = 360 - diff;
      if (diff < bd) {
        bd = diff;
        best = d;
      }
    }
    return best;
  }

  void _press({ShotDir? dir, bool defend = false, bool loft = false}) {
    if (_phase != _P.flight || !userBatting || _pressClock != null) return;
    final p = _path!;
    final ft = _clock - _releaseClock;
    final err = (ft - p.tArrive) * 1000;
    _pressClock = _clock;
    _pressDir = dir;
    _pressDefend = defend;
    _pressLoft = loft;
    _result = m.resolveUserShot(_plan!, ShotInput(dir: dir, defend: defend, loft: loft, timingMs: err));
    _grade = _result!.timing ?? gradeTiming(err, m.difficulty.windows);
    haptic(Buzz.light);
    setState(() {});
  }

  void _aiSwing(BallResult r) {
    _pressClock = _clock;
    _pressLoft = r.lofted;
    if (r.extra == ExtraType.wide || (r.beaten && m.rng.chance(0.55))) {
      _pressDir = null;
      _pressDefend = false;
      return;
    }
    if (r.defended && r.runs == 0) {
      _pressDefend = true;
      return;
    }
    _pressDir = _nearestDir(r.angle ?? 0);
  }

  Pose _swingPose(double dt) {
    if (_pressDefend) return Pose.lerp(pBatBacklift, pDefend, clamp01(dt / 0.12));
    final dir = _pressDir;
    if (dir == null) return Pose.lerp(pBatBacklift, pLeave, clamp01(dt / 0.2));
    final spin = _plan?.spin ?? false;
    final (Pose contact, Pose follow) = switch (dir) {
      ShotDir.straight || ShotDir.cover => (pDriveContact, _pressLoft ? pLoftFollow : pDriveFollow),
      ShotDir.point || ShotDir.thirdMan => (pCutContact, pCutFollow),
      ShotDir.midWicket || ShotDir.squareLeg => (pPullContact, pPullFollow),
      ShotDir.fineLeg => spin ? (pSweep, pSweep) : (pPullContact, pPullFollow),
    };
    return samplePoses([(0, pBatBacklift), (0.11, contact), (0.42, follow)], dt);
  }

  bool _isHit(BallResult r) {
    if (r.extra == ExtraType.wide || r.beaten) return false;
    if (r.wicket) {
      switch (r.dismissal!) {
        case Dismissal.bowled:
        case Dismissal.lbw:
        case Dismissal.stumped:
        case Dismissal.caughtBehind:
          return false;
        default:
          return true;
      }
    }
    return true;
  }

  void _beginAfterContact(double ft) {
    final r = _result!;
    _s
      ..marker = null
      ..ring = null;
    _carryHit = _isHit(r);
    final defendedDot = r.defended && r.total == 0 && !r.wicket;
    if (_carryHit && !defendedDot) {
      _beginFollow();
      return;
    }
    _phase = _P.carry;
    _t = 0;
    _carryStartFt = ft;
  }

  void _tickCarry() {
    final r = _result!;
    final p = _path!;
    final ft = _carryStartFt + _t;
    if (_carryHit) {
      // Defended into the pitch.
      final start = p.at(_carryStartFt);
      final k = clamp01(_t / 0.6);
      _ballPos = Vec3(start.x + 0.4 * k * _hand, start.y - 3.2 * k, math.max(0, start.z * (1 - k) + 0.25 * math.sin(k * math.pi)));
    } else {
      switch (r.dismissal) {
        case Dismissal.bowled:
          if (ft < p.tStumps) {
            _ballPos = p.at(ft);
          } else if (!_stumpsDone) {
            _stumpsDone = true;
            _s.farStumpsHit = 0;
            _s.flash = 0.35;
            _shake = 0.8;
            haptic(Buzz.heavy);
          }
        case Dismissal.lbw:
          final at = p.at(math.min(ft, p.tArrive));
          _ballPos = Vec3(at.x, at.y, math.max(0, at.z - math.max(0, ft - p.tArrive) * 1.5));
        case Dismissal.stumped:
          _ballPos = p.at(ft);
          if (ft > p.tAfter * 0.5 + p.tArrive && !_stumpsDone) {
            _stumpsDone = true;
            _s.farStumpsHit = 0;
            _s.flash = 0.3;
          }
        default:
          _ballPos = p.at(ft);
      }
      if (ft > p.tArrive - 0.1) _keeperPose = pKeeperUp;
    }
    if (_ballPos != null) _pushTrail(_ballPos!, 7);
    if (_t > 0.9) _enterResult();
  }

  void _beginFollow() {
    _shot = planShot(_result!, _field, leftBatter: m.striker.leftBat, boundary: m.venue.boundary.toDouble(), rng: m.rng);
    _following = true;
    _followStartClock = _clock;
    _followStart = _basePos();
    _followPos = _followStart;
    _look = _baseLook();
    _followFocal = _baseCam(_size).focal;
    _followEnd = userBatting ? Vec3(_followStart.x * 0.4, 46, 18) : const Vec3(0, -44, 20);
    if (_result!.isSix || _result!.isFour) haptic(Buzz.medium);
    _phase = _P.follow;
    _t = 0;
    _s.trail.clear();
    setState(() {});
  }

  void _tickFollow() {
    final s = _shot!;
    final b = s.path.at(_t);
    _ballPos = b;
    _pushTrail(b, 12);
    if (_t >= s.duration + 0.3) _enterResult();
  }

  // =========================================================== result

  void _enterResult() {
    final r = _result!;
    final inn = m.current;
    final batter = m.striker;
    final bowler = m.bowler;
    String? outText;
    if (r.wicket) {
      final outId = r.outBatterId ?? inn.striker;
      final out = inn.batting.byId(outId);
      final card = inn.bat[outId]!;
      final fielder = r.fielderId == null ? null : inn.bowling.byId(r.fielderId!);
      outText = '${out.shortName} ${dismissalLine(r.dismissal!, fielder, bowler, out)} • ${card.runs} (${card.balls + (r.legal ? 1 : 0)})';
    }
    final before = inn.bat[batter.id]?.runs ?? 0;
    m.apply(r);
    final after = inn.bat[batter.id]?.runs ?? 0;
    _phase = _P.result;
    _t = 0;
    _s.screen = r.wicket ? ScreenEvent.out : (r.isSix ? ScreenEvent.six : (r.isFour ? ScreenEvent.four : ScreenEvent.none));

    if (r.wicket) {
      _showBanner(r.dismissal == Dismissal.runOut ? 'RUN OUT!' : 'OUT!', AppColors.red, sub: outText);
      if (r.dismissal != Dismissal.runOut) _umpPose = pUmpOut;
      _hype = 1;
      _shake = math.max(_shake, 0.5);
      haptic(Buzz.heavy);
    } else if (r.isSix) {
      _showBanner('SIX!', AppColors.violet, sub: '${batter.shortName} • ${(78 + m.rng.next() * 28).round()}m');
      _umpPose = pUmpSix;
      _hype = 1;
      _shake = 0.7;
      _confetti++;
      haptic(Buzz.heavy);
    } else if (r.isFour) {
      _showBanner('FOUR!', AppColors.cyan, sub: batter.shortName);
      _umpPose = pUmpFour;
      _hype = 0.85;
      haptic(Buzz.medium);
    } else if (r.extra == ExtraType.wide) {
      _showBanner('WIDE', AppColors.gold);
      _umpPose = pUmpWide;
    } else if (r.extra == ExtraType.noBall) {
      _showBanner('NO BALL', AppColors.gold, sub: 'FREE HIT NEXT BALL');
      _umpPose = pUmpNoBall;
    } else if (r.total > 0) {
      _showBanner(r.total == 1 ? '1 RUN' : '${r.total} RUNS', AppColors.green);
    } else {
      _banner = null;
    }
    if (!r.wicket && ((before < 50 && after >= 50) || (before < 100 && after >= 100))) {
      _showBanner(after >= 100 ? 'CENTURY!' : 'FIFTY!', AppColors.gold, sub: '${batter.name} • $after off ${inn.bat[batter.id]!.balls}');
      _confetti++;
      _hype = 1;
    }
    setState(() {});
  }

  void _showBanner(String text, Color color, {String? sub}) {
    _banner = text;
    _bannerSub = sub;
    _bannerColor = color;
    _bannerKey++;
  }

  void _tickResult() {
    final r = _result;
    final big = r != null && (r.wicket || r.isSix || r.isFour);
    if (_t < (big ? 2.1 : 1.15)) return;
    _banner = null;
    _proceed();
  }

  void _proceed() {
    if (m.finished) {
      _enterDone();
    } else if (m.innings.length != _inningsCount) {
      _inningsCount = m.innings.length;
      _phase = _P.inningsBreak;
      _following = false;
      _pendingSim = null;
      setState(() {});
    } else if (m.needBowler) {
      final inn = m.current;
      final last = inn.overRuns.isEmpty ? 0 : inn.overRuns.last;
      _overToast = 'END OF OVER ${inn.overIndex} • $last RUN${last == 1 ? '' : 'S'} • ${inn.batting.team.id} ${inn.scoreText}';
      _phase = _P.overBreak;
      _t = 0;
      setState(() {});
    } else {
      _enterReady();
    }
  }

  void _enterDone() {
    _phase = _P.done;
    _following = false;
    _banner = null;
    if (!_finishedCalled) {
      _finishedCalled = true;
      widget.onFinished?.call(m);
      if (m.outcome?.winnerId == widget.userTeamId) {
        _confetti++;
        _hype = 1;
      }
    }
    setState(() {});
  }

  // ============================================================ actors

  Color _trim(TeamInfo t) => t.secondary.luma < 0.2 ? AppColors.gold : t.secondary;

  Pose _runPose(double phase) => Pose.lerp(pRunA, pRunB, 0.5 + 0.5 * math.sin(phase));

  FigureLook _lookFor(Player p, TeamInfo t, {required bool batting}) => batting
      ? batterLook(t.primary, _trim(t), name: p.surname, number: p.jersey, skin: skinFor(p.name))
      : fielderLook(t.primary, _trim(t), name: p.surname, number: p.jersey, skin: skinFor(p.name));

  void _syncScene() {
    final a = _s.actors..clear();
    final inn = m.started ? (m.finished ? m.innings.last : m.current) : null;
    final batTeam = inn?.batting.team ?? m.home.team;
    final bowlTeam = inn?.bowling.team ?? m.away.team;
    final striker = inn != null ? inn.batting.byId(inn.striker) : m.home.order.first;
    final nonStriker = inn != null ? inn.batting.byId(inn.nonStriker) : m.home.order[1];
    final hand = striker.leftBat ? -1.0 : 1.0;
    final strikerPos = Vec3(0.18 * hand, 19.32, 0);
    final shot = _shot;
    final tf = _clock - _followStartClock;
    final showShot = shot != null && (_phase == _P.follow || _phase == _P.result);
    final bowlerId = inn?.bowlerId ?? inn?.lastOverBowler;
    final bowlerP = inn != null && bowlerId != null ? inn.bowling.byId(bowlerId) : null;
    final keeperId = inn?.bowling.keeperId;
    final fieldSide = inn == null
        ? m.away.order
        : inn.bowling.order.where((p) => p.id != bowlerId && p.id != keeperId).toList();

    // Fielders.
    for (var i = 0; i < _field.positions.length; i++) {
      final home = _field.positions[i];
      final toS = strikerPos - home;
      final len = math.max(1.0, toS.length);
      var p = home + toS * (_walk / len);
      var facing = math.atan2(strikerPos.x - p.x, strikerPos.y - p.y);
      var pose = pFieldReady;
      if (showShot && shot.fielder == i && shot.fielderTarget != null) {
        final arrive = shot.caught ? shot.duration * 0.97 : shot.duration * 0.9;
        final u = clamp01(tf / arrive);
        final e = u * u * (3 - 2 * u);
        final dest = shot.four ? Vec3.lerp(p, shot.fielderTarget!, 0.85) : shot.fielderTarget!;
        final moving = (dest - p).length > 1;
        if (moving) facing = math.atan2(dest.x - p.x, dest.y - p.y);
        p = Vec3.lerp(p, dest, e);
        if (u < 1 && moving) {
          pose = _runPose(_clock * 13 + i);
        } else {
          pose = shot.caught ? pCatch : pThrow;
        }
      }
      final who = fieldSide[i % fieldSide.length];
      a.add(Actor(foot: p, facing: facing, pose: pose, look: _lookFor(who, bowlTeam, batting: false)));
    }

    final hideKeeper = userBatting && !_following;
    if (!hideKeeper) {
      final keeper = keeperId != null && inn != null ? inn.bowling.byId(keeperId) : null;
      a.add(Actor(
        foot: _field.keeper,
        facing: math.pi,
        pose: _keeperPose,
        look: keeperLook(bowlTeam.primary, _trim(bowlTeam), skin: keeper == null ? null : skinFor(keeper.name)),
      ));
      for (var i = 0; i < _field.slips.length; i++) {
        final who = fieldSide[(i + _field.positions.length) % fieldSide.length];
        a.add(Actor(foot: _field.slips[i], facing: math.pi, pose: pSlip, look: _lookFor(who, bowlTeam, batting: false)));
      }
    }

    // Bowler.
    if (bowlerP != null) {
      a.add(Actor(
        foot: Vec3(_bowlerX, _bowlerY, 0),
        facing: 0,
        pose: _bowlerPose,
        mirror: bowlerP.bowlStyle.leftArm,
        look: _lookFor(bowlerP, bowlTeam, batting: false),
      ));
    }

    // Batters (running between wickets after the shot).
    final strikerLook = _lookFor(striker, batTeam, batting: true);
    final nonStrikerLook = _lookFor(nonStriker, batTeam, batting: true);
    final runs = showShot && !shot.four && !shot.six ? shot.runs : 0;
    final armSide = bowlerP != null && bowlerP.bowlStyle.leftArm ? -1.0 : 1.0;
    final runOut = showShot && shot.runOut;
    if (runs > 0 || runOut) {
      var progress = clamp01(tf / (1.1 * math.max(1, runs))) * math.max(1, runs);
      if (runOut) progress = math.min(progress, 0.85);
      final leg = progress.floor();
      final frac = progress - leg;
      double endY(bool st, int l) => (st ? l.isEven : l.isOdd) ? 19.3 : 1.0;
      for (final st in [true, false]) {
        final from = endY(st, leg);
        final to = leg >= math.max(1, runs) ? from : endY(st, leg + 1);
        final y = from + (to - from) * (leg >= math.max(1, runs) ? 0 : frac);
        final x = (st ? 0.7 : -0.7) * hand;
        final dirUp = to > from;
        final run = _runPose(_clock * 13 + (st ? 0 : 2)).copyWith(hold: BatHold.right, bat: const Vec3(0.12, 0.55, -0.82));
        a.add(Actor(foot: Vec3(x, y, 0), facing: dirUp ? 0 : math.pi, pose: run, look: st ? strikerLook : nonStrikerLook));
      }
    } else {
      a.add(Actor(
        foot: strikerPos,
        facing: striker.leftBat ? math.pi / 2 : -math.pi / 2,
        pose: _batterPose,
        mirror: striker.leftBat,
        look: strikerLook,
      ));
      a.add(Actor(foot: Vec3(2.1 * armSide, _nsY + 0.6, 0), facing: 0, pose: pNonStriker, mirror: nonStriker.leftBat, look: nonStrikerLook));
    }

    // Umpires.
    a.add(Actor(foot: Vec3(1.15 * armSide, -2.4, 0), facing: 0, pose: _umpPose, look: umpireLook.withSkin(kSkinTones[1])));
    a.add(Actor(foot: Vec3(22 * hand, 19.4, 0), facing: hand > 0 ? -math.pi / 2 : math.pi / 2, pose: pUmpire, look: umpireLook.withSkin(kSkinTones[4])));

    _s
      ..ball = _ballPos
      ..hype = _hype
      ..time = _clock
      ..leftBatter = striker.leftBat
      ..ringAt = Vec3(0.1 * hand, 19.05, 0.7)
      ..hideNearActors = hideKeeper
      ..pitchMapLabels = _aimZoom > 0.45
      ..pitchMap = userBowling && _phase == _P.ready && !_autoBowl && !_pickingBowler
      ..aim = (userBowling && _phase == _P.ready && !_autoBowl) ? _aim : null;
  }

  // ============================================================ input

  void _panStart(DragStartDetails d) {
    final p = d.localPosition;
    if (userBowling && _phase == _P.ready && !_autoBowl && !_meterOn) {
      _draggingAim = true;
      _moveAim(p);
      return;
    }
    if (userBatting) {
      _swipeStart = p;
      _swipeStartClock = _clock;
      _swipeFired = false;
      _s.swipe
        ..clear()
        ..add((p, _clock));
    }
  }

  void _panUpdate(DragUpdateDetails d) {
    final p = d.localPosition;
    if (_draggingAim) {
      _moveAim(p);
      return;
    }
    final start = _swipeStart;
    if (start == null) return;
    _s.swipe.add((p, _clock));
    if (_s.swipe.length > 26) _s.swipe.removeAt(0);
    if (_swipeFired) return;
    final v = p - start;
    if (v.distance < 26) return;
    _swipeFired = true;
    final speed = v.distance / math.max(0.016, _clock - _swipeStartClock);
    final phi = math.atan2(v.dx, -v.dy) * 180 / math.pi;
    _press(dir: _nearestDir(phi * _hand), loft: _loft || speed > 1800);
  }

  void _panEnd(DragEndDetails d) {
    _draggingAim = false;
    _swipeStart = null;
  }

  void _tapUp(TapUpDetails d) {
    if (userBatting) {
      _press(defend: true);
    } else if (userBowling && _phase == _P.ready && !_autoBowl && !_meterOn) {
      _moveAim(d.localPosition);
    }
  }

  // ============================================================== sims

  bool get _canSim => m.started && !m.finished && (_phase == _P.ready || _phase == _P.overBreak);
  bool get _simOffered => m.started && !m.finished && _phase != _P.inningsBreak && _phase != _P.done;

  /// Simulation chosen mid-ball: runs as soon as the current ball is over.
  void Function()? _pendingSim;

  void _sim(void Function() run) {
    if (!_canSim) {
      if (_simOffered) {
        _pendingSim = run;
        showToast(context, 'Simulating after this ball…');
      }
      return;
    }
    _pendingSim = null;
    run();
    _overToast = null;
    if (m.finished) {
      _enterDone();
    } else if (m.innings.length != _inningsCount) {
      _inningsCount = m.innings.length;
      _phase = _P.inningsBreak;
      setState(() {});
    } else {
      _enterReady();
    }
  }

  Future<void> _menu() async {
    final choice = await showDialog<String>(
      context: context,
      builder: (ctx) => Dialog(
        backgroundColor: AppColors.bg1,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20), side: BorderSide(color: AppColors.gold.o(0.4))),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 520),
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text('MATCH PAUSED', style: T.display(26, weight: FontWeight.w700)),
                  const SizedBox(height: 10),
                  Wrap(
                    spacing: 10,
                    runSpacing: 10,
                    alignment: WrapAlignment.center,
                    children: [
                      for (final (id, label, icon, color) in <(String, String, IconData, Color)>[
                        ('resume', 'RESUME', Icons.play_arrow_rounded, AppColors.green),
                        if (_simOffered) ('over', 'SIM OVER', Icons.skip_next_rounded, AppColors.text),
                        if (_simOffered) ('innings', 'SIM INNINGS', Icons.fast_forward_rounded, AppColors.text),
                        if (_simOffered) ('match', 'SIM MATCH', Icons.double_arrow_rounded, AppColors.gold),
                        ('card', 'SCORECARD', Icons.list_alt_rounded, AppColors.cyan),
                        ('quit', 'QUIT', Icons.exit_to_app_rounded, AppColors.red),
                      ])
                        SizedBox(width: 150, child: GhostButton(label: label, icon: icon, color: color, onTap: () => Navigator.pop(ctx, id))),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
    if (!mounted) return;
    switch (choice) {
      case 'over':
        _sim(m.simulateOver);
      case 'innings':
        _sim(m.simulateInnings);
      case 'match':
        _sim(m.simulateToEnd);
      case 'card':
        showDialog<void>(
          context: context,
          builder: (ctx) => Dialog(
            backgroundColor: AppColors.bg1,
            insetPadding: const EdgeInsets.all(16),
            child: SizedBox(width: 640, height: _size.height * 0.9, child: ScorecardView(engine: m)),
          ),
        );
      case 'quit':
        _quit();
    }
  }

  Future<void> _quit() async {
    if (widget.onFinished == null) {
      Navigator.of(context).pop();
      return;
    }
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        backgroundColor: AppColors.bg1,
        title: Text('Leave match?', style: T.display(26)),
        content: Text('Season matches must finish - the rest of this match will be simulated.', style: T.ui(14.5, color: AppColors.textDim)),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: Text('KEEP PLAYING', style: T.label(13))),
          TextButton(onPressed: () => Navigator.pop(ctx, true), child: Text('SIMULATE & EXIT', style: T.label(13, color: AppColors.gold))),
        ],
      ),
    );
    if (ok != true || !mounted) return;
    if (!m.started) m.autoToss();
    m.simulateToEnd();
    _enterDone();
    _openResult();
  }

  /// Shows the match centre; closing it also closes the match.
  Future<void> _openResult() async {
    if (_resultOpen) return;
    _resultOpen = true;
    _ticker.stop();
    await Navigator.of(context).push(fadeRoute(MatchResultScreen(engine: m, userTeamId: widget.userTeamId, title: widget.title)));
    if (mounted) Navigator.of(context).pop(true);
  }

  // ============================================================= build

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) _menu();
      },
      child: Scaffold(
        backgroundColor: Colors.black,
        body: Stack(
          children: [
            Positioned.fill(
              child: GestureDetector(
                behavior: HitTestBehavior.opaque,
                onPanStart: _panStart,
                onPanUpdate: _panUpdate,
                onPanEnd: _panEnd,
                onTapUp: _tapUp,
                child: CustomPaint(
                  painter: ScenePainter(mesh: _mesh, ground: _ground, s: _s, team: m.venue, repaint: _repaint),
                  child: const SizedBox.expand(),
                ),
              ),
            ),
            SafeArea(child: Padding(padding: const EdgeInsets.fromLTRB(6, 6, 6, 6), child: _hud())),
            Positioned.fill(
              child: ConfettiBurst(
                trigger: _confetti,
                colors: [userTeam.primary, userTeam.glow, AppColors.gold, Colors.white, AppColors.violet],
                origin: const Alignment(0, -0.3),
              ),
            ),
            if (_phase == _P.toss) Positioned.fill(child: _tossOverlay()),
            if (_phase == _P.inningsBreak) Positioned.fill(child: _inningsOverlay()),
            if (_phase == _P.done) Positioned.fill(child: _doneOverlay()),
          ],
        ),
      ),
    );
  }

  Widget _hud() {
    final started = m.started && !m.finished;
    return Stack(
      children: [
        Align(alignment: Alignment.topLeft, child: ScoreBug(engine: m)),
        Align(alignment: Alignment.topCenter, child: MatchupStrip(engine: m)),
        Align(
          alignment: Alignment.topRight,
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (_speedGun != null)
                Container(
                  margin: const EdgeInsets.only(right: 8),
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  decoration: BoxDecoration(color: Colors.black.o(0.55), borderRadius: BorderRadius.circular(8), border: Border.all(color: AppColors.gold.o(0.4))),
                  child: Text(_speedGun!, style: T.display(15, color: AppColors.goldLight)),
                ),
              CircleIconButton(icon: Icons.pause_rounded, size: 40, onTap: _menu),
            ],
          ),
        ),
        if (_banner != null)
          Align(
            alignment: const Alignment(0, -0.25),
            child: ResultBanner(key: ValueKey(_bannerKey), text: _banner!, color: _bannerColor, sub: _bannerSub),
          ),
        if (_overToast != null)
          Align(
            alignment: const Alignment(0, 0.3),
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              decoration: BoxDecoration(color: Colors.black.o(0.75), borderRadius: BorderRadius.circular(14), border: Border.all(color: AppColors.gold.o(0.5))),
              child: Text(_overToast!, style: T.display(18, weight: FontWeight.w700, color: AppColors.goldLight)),
            ),
          ),
        if (started && userBatting) ...[
          Align(
            alignment: Alignment.bottomLeft,
            child: SwipeHint(
              active: _phase == _P.flight && _pressClock == null,
              loft: _loft,
              onLoft: () => setState(() => _loft = !_loft),
            ),
          ),
          Align(
            alignment: Alignment.bottomRight,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                TimingReadout(grade: _grade),
                const SizedBox(height: 6),
                BatterPanel(engine: m),
              ],
            ),
          ),
        ],
        if (started && userBowling) ...[
          if (m.current.bowlerId != null)
            Align(
              alignment: Alignment.bottomLeft,
              child: Builder(builder: (context) {
                final (type, line) = _aimToDelivery();
                return VariationSelector(
                  options: m.bowler.isSpin ? _spinVariations : _paceVariations,
                  selected: _variation,
                  onSelect: (i) => setState(() => _variation = i),
                  summary: '${type.label} • ${line.label}',
                  enabled: _phase == _P.ready && !_autoBowl,
                );
              }),
            ),
          Align(
            alignment: Alignment.bottomRight,
            child: BowlControls(
              meterOn: _meterOn,
              meter: _meterOn ? _meterValue : 0.5,
              enabled: _phase == _P.ready && !_pickingBowler,
              autoBowl: _autoBowl,
              onBowl: _onBowlButton,
              onAuto: () => setState(() => _autoBowl = !_autoBowl),
            ),
          ),
          Align(alignment: const Alignment(1, -0.45), child: BatterPanel(engine: m)),
          if (_phase == _P.ready && !_autoBowl)
            Align(
              alignment: const Alignment(0, 0.92),
              child: IgnorePointer(
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                  decoration: BoxDecoration(color: Colors.black.o(0.55), borderRadius: BorderRadius.circular(14)),
                  child: Text('DRAG THE TARGET ON THE PITCH  •  THEN BOWL', style: T.label(10, color: AppColors.goldLight)),
                ),
              ),
            ),
        ],
      ],
    );
  }

  // ========================================================= overlays

  Widget _overlayCard({required Widget child, double maxWidth = 620}) => Container(
        color: Colors.black.o(0.6),
        alignment: Alignment.center,
        padding: const EdgeInsets.all(12),
        child: ConstrainedBox(
          constraints: BoxConstraints(maxWidth: maxWidth),
          child: GlassCard(
            blur: true,
            padding: const EdgeInsets.all(16),
            color: const Color(0xD90A0E2A),
            borderColor: AppColors.gold.o(0.5),
            child: SingleChildScrollView(child: child),
          ),
        ),
      );

  Widget _tossOverlay() {
    final flipAngle = _flipping ? _flipT * 22 : 0.0;
    return _overlayCard(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(widget.title, textAlign: TextAlign.center, style: T.label(11, color: AppColors.gold)),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(child: Column(children: [TeamCrest(m.home.team, size: 54, glow: true), Text(m.home.team.id, style: T.display(20, weight: FontWeight.w700))])),
              Transform(
                alignment: Alignment.center,
                transform: Matrix4.identity()
                  ..setEntry(3, 2, 0.002)
                  ..rotateX(flipAngle),
                child: Container(
                  width: 78,
                  height: 78,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    gradient: const RadialGradient(colors: [AppColors.goldLight, AppColors.gold, AppColors.goldDeep], stops: [0.1, 0.6, 1]),
                    boxShadow: [BoxShadow(color: AppColors.gold.o(0.6), blurRadius: 24)],
                    border: Border.all(color: const Color(0xFFB8860B), width: 4),
                  ),
                  alignment: Alignment.center,
                  child: Text(math.cos(flipAngle) >= 0 ? 'H' : 'T', style: T.display(42, color: const Color(0xFF6B4A00), weight: FontWeight.w700)),
                ),
              ),
              Expanded(child: Column(children: [TeamCrest(m.away.team, size: 54, glow: true), Text(m.away.team.id, style: T.display(20, weight: FontWeight.w700))])),
            ],
          ),
          const SizedBox(height: 6),
          Text('${m.venue.venue} • ${m.overs} overs', textAlign: TextAlign.center, style: T.ui(12.5, color: AppColors.textDim)),
          const SizedBox(height: 10),
          if (_tossText != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: Text(_tossText!, textAlign: TextAlign.center, style: T.display(21, color: AppColors.goldLight, weight: FontWeight.w700)),
            ),
          if (!_flipping && _call == null)
            Row(children: [
              Expanded(child: NeonButton(label: 'HEADS', height: 48, onTap: () => _callToss(true))),
              const SizedBox(width: 10),
              Expanded(child: NeonButton(label: 'TAILS', height: 48, colors: const [AppColors.cyan, AppColors.violet], onTap: () => _callToss(false))),
            ]),
          if (_awaitChoice)
            Row(children: [
              Expanded(child: NeonButton(label: 'BAT FIRST', icon: Icons.sports_cricket_rounded, height: 48, onTap: () => _choose(true))),
              const SizedBox(width: 10),
              Expanded(child: NeonButton(label: 'BOWL FIRST', icon: Icons.sports_baseball_rounded, height: 48, colors: const [AppColors.pink, AppColors.violet], onTap: () => _choose(false))),
            ]),
          if (_flipping) Text('The coin is in the air…', style: T.ui(14, color: AppColors.textDim)),
        ],
      ),
    );
  }

  Widget _inningsOverlay() {
    final prev = m.innings[m.innings.length - 2];
    final next = m.current;
    final so = next.superOver;
    final bats = prev.bat.values.toList()..sort((a, b) => b.runs.compareTo(a.runs));
    final bowls = prev.bowl.values.toList()..sort((a, b) => b.wickets != a.wickets ? b.wickets.compareTo(a.wickets) : a.runs.compareTo(b.runs));
    final userChasing = next.batting.team.id == widget.userTeamId;
    return _overlayCard(
      maxWidth: 700,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          Expanded(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(so ? 'SUPER OVER!' : 'INNINGS BREAK', style: T.display(28, weight: FontWeight.w700, color: so ? AppColors.pink : AppColors.gold)),
                Row(children: [
                  TeamCrest(prev.batting.team, size: 34),
                  const SizedBox(width: 8),
                  Text('${prev.batting.team.id} ${prev.scoreText}', style: T.display(32, weight: FontWeight.w700)),
                  Text('  (${prev.overs})', style: T.ui(13, color: AppColors.textDim)),
                ]),
                const SizedBox(height: 6),
                for (final b in bats.take(2)) _perfRow(prev.batting.byId(b.playerId).name, '${b.runs} (${b.balls})', AppColors.cyan),
                for (final b in bowls.take(2)) _perfRow(prev.bowling.byId(b.playerId).name, '${b.wickets}/${b.runs} (${oversText2(b.balls)})', AppColors.pink),
              ],
            ),
          ),
          const SizedBox(width: 16),
          SizedBox(
            width: 230,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text('${next.batting.team.id} NEED', style: T.label(11)),
                GradientText('${next.target}', style: T.display(64, weight: FontWeight.w700, height: 1)),
                Text(so ? 'off 1 over' : 'off ${next.maxOvers} overs', style: T.ui(13, color: AppColors.textDim)),
                const SizedBox(height: 10),
                NeonButton(
                  label: userChasing ? 'START CHASE' : 'DEFEND ${next.target! - 1}',
                  icon: Icons.play_arrow_rounded,
                  height: 50,
                  onTap: _enterReady,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _perfRow(String name, String v, Color color) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 2),
        child: Row(
          children: [
            Container(width: 4, height: 15, color: color),
            const SizedBox(width: 8),
            Expanded(child: Text(name, style: T.ui(14, weight: FontWeight.w700))),
            Text(v, style: T.display(18, weight: FontWeight.w700)),
          ],
        ),
      );

  Widget _doneOverlay() {
    final o = m.outcome!;
    final won = o.winnerId == widget.userTeamId;
    final potm = m.playerById(o.potmId);
    final winner = teamById(o.winnerId!);
    return Stack(
      children: [
        if (won) const Positioned.fill(child: FireworksLayer()),
        _overlayCard(
          maxWidth: 720,
          child: Row(
            children: [
              Expanded(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(won ? 'VICTORY!' : 'DEFEAT', style: T.display(44, weight: FontWeight.w700, color: won ? AppColors.gold : AppColors.red, height: 1)),
                    const SizedBox(height: 6),
                    TeamCrest(winner, size: 60, glow: true),
                    const SizedBox(height: 6),
                    Text(o.text, textAlign: TextAlign.center, style: T.display(22, weight: FontWeight.w700)),
                    for (final inn in m.innings.take(2))
                      Text('${inn.batting.team.id}  ${inn.scoreText} (${inn.overs})', style: T.ui(14, color: AppColors.textDim, weight: FontWeight.w700)),
                  ],
                ),
              ),
              const SizedBox(width: 16),
              SizedBox(
                width: 250,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    GlassCard(
                      padding: const EdgeInsets.all(10),
                      child: Row(
                        children: [
                          PlayerAvatar(potm, size: 44, team: m.home.order.any((p) => p.id == potm.id) ? m.home.team : m.away.team),
                          const SizedBox(width: 10),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text('PLAYER OF THE MATCH', style: T.label(9.5, color: AppColors.gold)),
                                Text(potm.name, style: T.display(20, weight: FontWeight.w700)),
                              ],
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 12),
                    NeonButton(label: 'SCORECARD', icon: Icons.list_alt_rounded, height: 50, onTap: _openResult),
                  ],
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

String oversText2(int balls) => '${balls ~/ 6}.${balls % 6}';
