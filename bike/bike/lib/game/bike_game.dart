import 'dart:math';

import 'package:flutter/material.dart';

import '../shop/catalog.dart';

enum GamePhase { menu, playing, paused, crashing, gameOver }

enum VehicleKind { car, truck }

enum PickupKind { coin, nitro, shield, heart }

enum SceneryKind { tree, bush, flowers }

class Vehicle {
  Vehicle({
    required this.lane,
    required this.y,
    required this.speed,
    required this.color,
    this.kind = VehicleKind.car,
  }) : x = lane.toDouble();

  /// Lane the vehicle is in, or is moving towards during a lane change.
  int lane;

  /// Continuous lane position; glides towards [lane] when changing lanes.
  double x;
  double y;

  /// Forward speed in world units per second.
  double speed;
  final Color color;
  final VehicleKind kind;

  /// Set once the vehicle has gone past the player (used for close calls).
  bool passed = false;

  /// Seconds left of blinking before the lane change starts.
  double signal = 0;
  int signalDir = 0;

  double get width => kind == VehicleKind.truck ? 54 : 44;
  double get height => kind == VehicleKind.truck ? 130 : 82;
  bool get indicating => signalDir != 0;
}

class Pickup {
  Pickup(this.kind, this.lane, this.y);

  final PickupKind kind;
  final int lane;
  double y;
}

class Particle {
  Particle(this.x, this.y, this.vx, this.vy, this.life, this.size, this.color)
      : maxLife = life;

  double x, y, vx, vy, life;
  final double maxLife, size;
  final Color color;

  /// 1 when spawned, 0 when it dies.
  double get progress => life / maxLife;
}

class FloatingText {
  FloatingText(this.text, this.x, this.y, this.color,
      {this.size = 20, this.life = 1.1})
      : maxLife = life;

  final String text;
  double x, y, life;
  final double maxLife, size;
  final Color color;
}

class Scenery {
  Scenery(this.kind, this.x, this.y, this.scale);

  final SceneryKind kind;
  final double x, scale;
  double y;
}

/// All game state and rules. Everything is in "world units": the road area is
/// [worldWidth] wide and the painter scales it to fit the screen.
class BikeGame extends ChangeNotifier {
  BikeGame({Random? random}) : _rng = random ?? Random();

  static const double worldWidth = 400;
  static const double roadLeft = 50;
  static const double roadWidth = 300;
  static const double roadRight = roadLeft + roadWidth;
  static const int laneCount = 4;
  static const double laneWidth = roadWidth / laneCount;
  static const double playerWidth = 30;
  static const double playerHeight = 62;
  static const int startLives = 3;
  static const int maxLives = 5;
  static const double levelDistance = 700;
  static const double minBoost = 0.3;
  static const int reviveCost = 250;

  static const List<Color> carColors = [
    Color(0xFFEF5350),
    Color(0xFF42A5F5),
    Color(0xFFFFEE58),
    Color(0xFFAB47BC),
    Color(0xFF26A69A),
    Color(0xFFFF7043),
    Color(0xFFECEFF1),
    Color(0xFF5C6BC0),
    Color(0xFF8D6E63),
  ];

  static double laneCenter(num lane) => roadLeft + laneWidth * (lane + 0.5);

  final Random _rng;

  /// Called when the bike hits something; `heavy` is true for a real crash.
  void Function(bool heavy)? onImpact;

  /// Called when a run ends (including after a used continue).
  VoidCallback? onGameOver;

  // Viewport, in world units.
  double scale = 1;
  double viewWidth = worldWidth;
  double viewHeight = 800;
  double offsetX = 0;

  GamePhase phase = GamePhase.menu;
  BikeSkin bike = Catalog.bikes.first;

  double time = 0;
  double playTime = 0;
  double speed = 260;
  double scroll = 0;
  double distance = 0;
  double score = 0;
  int coins = 0;
  int level = 1;
  int lives = startLives;
  double nitro = 0;
  bool boosting = false;
  double shieldTimer = 0;
  double invincibleTimer = 0;
  double shake = 0;
  int combo = 0;
  double comboTimer = 0;
  double levelBannerTimer = 0;
  double crashTimer = 0;
  double crashSpin = 0;

  /// Whether the one continue per run has been used.
  bool revived = false;

  /// Coins already handed to the wallet this run (before a continue).
  int coinsBanked = 0;

  int targetLane = 1;
  double playerLane = 1.5;
  double tilt = 0;

  final List<Vehicle> vehicles = [];
  final List<Pickup> pickups = [];
  final List<Particle> particles = [];
  final List<FloatingText> texts = [];
  final List<Scenery> scenery = [];

  double _nextTraffic = 0;
  double _nextCoins = 0;
  double _nextPowerUp = 0;
  double _nextScenery = 0;
  double _exhaustTimer = 0;

  Color get bikeColor => bike.color;
  bool get canRevive => phase == GamePhase.gameOver && !revived;
  double get playerX => laneCenter(playerLane);
  double get playerY => viewHeight - 175;
  int get scoreValue => score.floor();
  int get speedKmh => (speed * 0.36).round();
  bool get isRunning => phase == GamePhase.playing;
  bool get canBoost => isRunning && !boosting && nitro >= minBoost;
  double get _baseSpeed => min(360 + (level - 1) * 40.0, 780);

  void resize(Size size) {
    if (size.isEmpty) return;
    scale = min(size.width / worldWidth, size.height / 700);
    viewWidth = size.width / scale;
    viewHeight = size.height / scale;
    offsetX = (viewWidth - worldWidth) / 2;
    if (scenery.isEmpty) {
      for (var y = viewHeight; y > -60; y -= 70 + _rng.nextDouble() * 80) {
        _spawnScenery(y);
      }
    }
  }

  void startGame() {
    phase = GamePhase.playing;
    playTime = 0;
    speed = 300;
    distance = 0;
    score = 0;
    coins = 0;
    level = 1;
    lives = bike.startLives;
    nitro = bike.startNitro;
    boosting = false;
    shieldTimer = bike.startShield;
    invincibleTimer = 0;
    shake = 0;
    combo = 0;
    comboTimer = 0;
    levelBannerTimer = 0;
    crashTimer = 0;
    crashSpin = 0;
    revived = false;
    coinsBanked = 0;
    targetLane = 1;
    playerLane = 1;
    tilt = 0;
    vehicles.clear();
    pickups.clear();
    particles.clear();
    texts.clear();
    _nextTraffic = 600;
    _nextCoins = 250;
    _nextPowerUp = 2200;
    _addText('GO!', worldWidth / 2, viewHeight * 0.4, const Color(0xFFFFEB3B),
        size: 56, life: 1.0);
    notifyListeners();
  }

  void steer(int dir) {
    if (!isRunning) return;
    targetLane = max(0, min(laneCount - 1, targetLane + dir));
  }

  void activateBoost() {
    if (!canBoost) return;
    boosting = true;
    shake = max(shake, 4);
    _addText('NITRO!', playerX, playerY - 80, const Color(0xFFFF9100),
        size: 30);
    notifyListeners();
  }

  /// Continues a finished run once: one life, a clear road and a moment
  /// of invincibility. The caller charges [reviveCost] coins.
  void revive() {
    if (!canRevive) return;
    revived = true;
    phase = GamePhase.playing;
    lives = 1;
    invincibleTimer = 3;
    crashSpin = 0;
    tilt = 0;
    speed = _baseSpeed * 0.6;
    vehicles.clear();
    _addText('KEEP GOING!', worldWidth / 2, viewHeight * 0.4,
        const Color(0xFF69F0AE), size: 40);
    notifyListeners();
  }

  void pause() {
    if (phase != GamePhase.playing) return;
    phase = GamePhase.paused;
    notifyListeners();
  }

  void resume() {
    if (phase != GamePhase.paused) return;
    phase = GamePhase.playing;
    notifyListeners();
  }

  void togglePause() => phase == GamePhase.paused ? resume() : pause();

  void goToMenu() {
    phase = GamePhase.menu;
    vehicles.clear();
    pickups.clear();
    particles.clear();
    texts.clear();
    boosting = false;
    shieldTimer = 0;
    invincibleTimer = 0;
    shake = 0;
    crashSpin = 0;
    notifyListeners();
  }

  void update(double dt) {
    if (dt <= 0) return;
    switch (phase) {
      case GamePhase.menu:
        time += dt;
        speed = 260;
        playerLane = 1.5 + sin(time * 0.9) * 0.35;
        tilt = cos(time * 0.9) * 0.12;
        _scrollWorld(dt);
        _spawnExhaust(dt);
        _updateEffects(dt);
      case GamePhase.playing:
        time += dt;
        _updatePlaying(dt);
      case GamePhase.crashing:
        time += dt;
        _updateCrash(dt);
      case GamePhase.paused:
      case GamePhase.gameOver:
        return;
    }
    notifyListeners();
  }

  void _updatePlaying(double dt) {
    playTime += dt;

    final target = _baseSpeed * (boosting ? 1.6 : 1.0);
    speed += (target - speed) * min(1.0, dt * (boosting ? 3.0 : 1.2));

    if (boosting) {
      nitro -= dt / 3.5;
      if (nitro <= 0) {
        nitro = 0;
        boosting = false;
      }
    }
    shieldTimer = max(0.0, shieldTimer - dt);
    invincibleTimer = max(0.0, invincibleTimer - dt);
    levelBannerTimer = max(0.0, levelBannerTimer - dt);
    if (comboTimer > 0) {
      comboTimer -= dt;
      if (comboTimer <= 0) combo = 0;
    }

    final meters = speed * dt / 10;
    distance += meters;
    score += meters * (boosting ? 2 : 1);
    final newLevel = 1 + distance ~/ levelDistance;
    if (newLevel > level) {
      level = newLevel;
      levelBannerTimer = 2.2;
      score += 100;
    }

    final diff = targetLane - playerLane;
    playerLane += diff * min(1.0, dt * 12);
    if (diff.abs() < 0.002) playerLane = targetLane.toDouble();
    tilt = (diff * 0.5).clamp(-0.4, 0.4);

    _scrollWorld(dt);
    _spawnTraffic(dt);
    _spawnPickups(dt);
    _updateVehicles(dt);
    _updatePickups(dt);
    _checkCollisions();
    _spawnExhaust(dt);
    _updateEffects(dt);
  }

  void _updateCrash(double dt) {
    crashTimer -= dt;
    crashSpin += dt * 10 * max(0.0, crashTimer);
    speed *= pow(0.15, dt).toDouble();
    _scrollWorld(dt);
    _updateVehicles(dt);
    _updatePickups(dt);
    _spawnExhaust(dt);
    _updateEffects(dt);
    if (crashTimer <= 0) _endGame();
  }

  void _endGame() {
    phase = GamePhase.gameOver;
    boosting = false;
    onGameOver?.call();
  }

  // ---------------------------------------------------------------- world

  void _scrollWorld(double dt) {
    final d = speed * dt;
    scroll = (scroll + d) % 960;
    for (final s in scenery) {
      s.y += d;
    }
    scenery.removeWhere((s) => s.y > viewHeight + 80);
    _nextScenery -= d;
    if (_nextScenery <= 0) {
      _nextScenery = 70 + _rng.nextDouble() * 80;
      _spawnScenery(-60);
    }
  }

  void _spawnScenery(double y) {
    // Wide screens have more grass on the sides, so fill it with more stuff.
    final perSide = 1 + (offsetX / 140).floor();
    for (final left in [true, false]) {
      for (var i = 0; i < perSide; i++) {
        if (_rng.nextDouble() < 0.35) continue;
        final double lo, hi;
        if (left) {
          lo = -offsetX + 16;
          hi = roadLeft - 28;
        } else {
          lo = roadRight + 28;
          hi = worldWidth + offsetX - 16;
        }
        final x = hi <= lo ? lo : lo + _rng.nextDouble() * (hi - lo);
        final roll = _rng.nextDouble();
        final kind = roll < 0.5
            ? SceneryKind.tree
            : roll < 0.8
                ? SceneryKind.bush
                : SceneryKind.flowers;
        scenery.add(Scenery(kind, x, y + _rng.nextDouble() * 40,
            0.75 + _rng.nextDouble() * 0.5));
      }
    }
  }

  void _spawnTraffic(double dt) {
    _nextTraffic -= speed * dt;
    if (_nextTraffic > 0) return;
    _nextTraffic =
        max(340.0, 620 - (level - 1) * 35.0) + _rng.nextDouble() * 180;

    final busyTop = <int>{};
    final busyBand = <int>{};
    for (final v in vehicles) {
      final lanes = {v.lane, v.x.round()};
      if (v.y - v.height / 2 < 90) busyTop.addAll(lanes);
      if (v.y < 340) busyBand.addAll(lanes);
    }
    final free = [
      for (var i = 0; i < laneCount; i++)
        if (!busyTop.contains(i)) i,
    ]..shuffle(_rng);
    final wanted =
        _rng.nextDouble() < min(0.15 + level * 0.07, 0.55) ? 2 : 1;

    // Always leave at least one lane open near the top of the screen.
    final chosen = <int>[];
    for (final lane in free) {
      if (chosen.length >= wanted) break;
      if ({...busyBand, ...chosen, lane}.length < laneCount) chosen.add(lane);
    }

    for (final lane in chosen) {
      final truck = _rng.nextDouble() < 0.18;
      final factor = truck
          ? 0.30 + _rng.nextDouble() * 0.12
          : 0.36 + _rng.nextDouble() * 0.24;
      final v = Vehicle(
        lane: lane,
        y: 0,
        speed: _baseSpeed * factor,
        color: carColors[_rng.nextInt(carColors.length)],
        kind: truck ? VehicleKind.truck : VehicleKind.car,
      );
      v.y = -v.height / 2 - 10;
      vehicles.add(v);
    }
  }

  int _freeLane() {
    final busy = {
      for (final v in vehicles)
        if (v.y < 260) v.x.round(),
    };
    final free = [
      for (var i = 0; i < laneCount; i++)
        if (!busy.contains(i)) i,
    ];
    return free.isEmpty
        ? _rng.nextInt(laneCount)
        : free[_rng.nextInt(free.length)];
  }

  void _spawnPickups(double dt) {
    final d = speed * dt;
    _nextCoins -= d;
    if (_nextCoins <= 0) {
      _nextCoins = 500 + _rng.nextDouble() * 500;
      final lane = _freeLane();
      final n = 4 + _rng.nextInt(4);
      for (var i = 0; i < n; i++) {
        pickups.add(Pickup(PickupKind.coin, lane, -30.0 - i * 46));
      }
    }
    _nextPowerUp -= d;
    if (_nextPowerUp <= 0) {
      _nextPowerUp = 2600 + _rng.nextDouble() * 2400;
      final roll = _rng.nextDouble();
      final PickupKind kind;
      if (lives < startLives && roll < 0.3) {
        kind = PickupKind.heart;
      } else if (roll < 0.65) {
        kind = PickupKind.nitro;
      } else {
        kind = PickupKind.shield;
      }
      pickups.add(Pickup(kind, _freeLane(), -40));
    }
  }

  bool _laneClear(int lane, Vehicle self) {
    for (final o in vehicles) {
      if (identical(o, self)) continue;
      if (o.lane != lane && o.x.round() != lane) continue;
      if ((o.y - self.y).abs() < (o.height + self.height) / 2 + 60) {
        return false;
      }
    }
    return true;
  }

  void _updateVehicles(double dt) {
    final changeChance = level >= 2 ? min(0.08 + level * 0.04, 0.35) : 0.0;
    for (final v in vehicles) {
      // Cars in the top part of the screen sometimes change lanes, after
      // blinking for a second so the player can react.
      if (v.kind == VehicleKind.car &&
          !v.indicating &&
          v.y > 30 &&
          v.y < viewHeight * 0.4 &&
          _rng.nextDouble() < dt * changeChance) {
        final dir = _rng.nextBool() ? 1 : -1;
        final target = v.lane + dir;
        if (target >= 0 && target < laneCount && _laneClear(target, v)) {
          v.signalDir = dir;
          v.signal = 1.0;
        }
      }
      if (v.signal > 0) {
        v.signal -= dt;
        if (v.signal <= 0) {
          final target = v.lane + v.signalDir;
          if (_laneClear(target, v)) {
            v.lane = target;
          } else {
            v.signalDir = 0;
          }
        }
      } else if (v.x != v.lane) {
        final d = v.lane - v.x;
        final step = 1.6 * dt;
        if (d.abs() <= step) {
          v.x = v.lane.toDouble();
          v.signalDir = 0;
        } else {
          v.x += step * d.sign;
        }
      }
      v.y += (speed - v.speed) * dt;
    }

    // A car catching up with a slower one in its lane slows down behind it.
    for (final a in vehicles) {
      for (final b in vehicles) {
        if (identical(a, b) || a.y >= b.y || (a.x - b.x).abs() > 0.8) continue;
        final gap = (b.y - b.height / 2) - (a.y + a.height / 2);
        if (gap < 40 && b.speed > a.speed) b.speed = a.speed;
      }
    }

    for (final v in vehicles) {
      if (!v.passed && v.y - v.height / 2 > playerY + playerHeight / 2) {
        v.passed = true;
        if (isRunning && invincibleTimer <= 0 && !boosting) {
          final dx = (laneCenter(v.x) - playerX).abs();
          if (dx < laneWidth * 1.1) _closeCall();
        }
      }
    }
    vehicles.removeWhere(
        (v) => v.y - v.height / 2 > viewHeight + 20 || v.y < -700);
  }

  void _closeCall() {
    combo = min(combo + 1, 10);
    comboTimer = 3;
    final points = 20 * combo;
    score += points;
    nitro = min(1.0, nitro + 0.08);
    _addText(
      combo > 1 ? 'CLOSE CALL x$combo +$points' : 'CLOSE CALL +$points',
      playerX,
      playerY - 60,
      const Color(0xFF18FFFF),
      size: 18,
    );
  }

  void _updatePickups(double dt) {
    final d = speed * dt;
    for (final p in pickups) {
      p.y += d;
    }
    if (isRunning) {
      final px = playerX;
      pickups.removeWhere((p) {
        final hit = (laneCenter(p.lane) - px).abs() < 36 &&
            (p.y - playerY).abs() < 46;
        if (hit) _collect(p);
        return hit;
      });
    }
    pickups.removeWhere((p) => p.y > viewHeight + 40);
  }

  void _collect(Pickup p) {
    final x = laneCenter(p.lane);
    switch (p.kind) {
      case PickupKind.coin:
        coins++;
        score += 10;
        nitro = min(1.0, nitro + 0.05);
        _burst(x, p.y, const Color(0xFFFFD54F), 8, 120);
        _addText('+10', x, p.y - 20, const Color(0xFFFFD54F),
            size: 16, life: 0.6);
      case PickupKind.nitro:
        nitro = min(1.0, nitro + 0.5);
        _burst(x, p.y, const Color(0xFF448AFF), 18, 200);
        _addText('NITRO +50%', x, p.y - 30, const Color(0xFF82B1FF));
      case PickupKind.shield:
        shieldTimer = 8;
        _burst(x, p.y, const Color(0xFF1DE9B6), 18, 200);
        _addText('SHIELD!', x, p.y - 30, const Color(0xFF1DE9B6));
      case PickupKind.heart:
        lives = min(maxLives, lives + 1);
        _burst(x, p.y, const Color(0xFFFF4081), 18, 200);
        _addText('+1 LIFE', x, p.y - 30, const Color(0xFFFF80AB));
    }
  }

  void _checkCollisions() {
    final pr = Rect.fromCenter(
      center: Offset(playerX, playerY),
      width: playerWidth - 8,
      height: playerHeight - 14,
    );
    for (final v in List.of(vehicles)) {
      final vr = Rect.fromCenter(
        center: Offset(laneCenter(v.x), v.y),
        width: v.width - 6,
        height: v.height - 10,
      );
      if (!pr.overlaps(vr)) continue;
      if (boosting || shieldTimer > 0) {
        _smash(v, vr.center);
        if (!boosting) {
          shieldTimer = 0;
          _addText('SHIELD USED', playerX, playerY - 90,
              const Color(0xFF1DE9B6), size: 18);
        }
        continue;
      }
      if (invincibleTimer > 0) continue;
      _crashInto(v, vr.center);
      break;
    }
  }

  void _smash(Vehicle v, Offset at) {
    vehicles.remove(v);
    score += 50;
    shake = max(shake, 8);
    _burst(at.dx, at.dy, v.color, 22, 320);
    _burst(at.dx, at.dy, const Color(0xFFFFAB00), 12, 260);
    _addText('SMASH +50', at.dx, at.dy, const Color(0xFFFF9100), size: 22);
    onImpact?.call(false);
  }

  void _crashInto(Vehicle v, Offset at) {
    vehicles.remove(v);
    lives--;
    combo = 0;
    comboTimer = 0;
    shake = 16;
    _burst(at.dx, at.dy, v.color, 26, 340);
    _burst(playerX, playerY, const Color(0xFFFFD740), 16, 300);
    onImpact?.call(true);
    if (lives <= 0) {
      phase = GamePhase.crashing;
      crashTimer = 1.4;
      boosting = false;
      _burst(playerX, playerY, bikeColor, 30, 380);
      _addText('CRASH!', worldWidth / 2, viewHeight * 0.4,
          const Color(0xFFFF5252), size: 48, life: 1.4);
    } else {
      invincibleTimer = 2.0;
      speed *= 0.7;
      _addText('OUCH!', playerX, playerY - 70, const Color(0xFFFF5252),
          size: 28);
    }
  }

  // -------------------------------------------------------------- effects

  void _spawnExhaust(double dt) {
    _exhaustTimer -= dt;
    if (_exhaustTimer > 0) return;
    final crashed = phase == GamePhase.crashing;
    _exhaustTimer = boosting ? 0.012 : (crashed ? 0.03 : 0.07);
    final x = playerX + (_rng.nextDouble() - 0.5) * 6;
    final y = playerY + 30;
    if (boosting) {
      particles.add(Particle(
        x,
        y + 16,
        (_rng.nextDouble() - 0.5) * 60,
        380 + _rng.nextDouble() * 200,
        0.3,
        5 + _rng.nextDouble() * 5,
        _rng.nextBool() ? const Color(0xFFFF6D00) : const Color(0xFFFFD600),
      ));
    } else if (crashed) {
      particles.add(Particle(
        playerX,
        playerY,
        (_rng.nextDouble() - 0.5) * 80,
        -40 - _rng.nextDouble() * 60,
        0.9,
        8 + _rng.nextDouble() * 8,
        const Color(0xFF616161),
      ));
    } else {
      particles.add(Particle(
        x,
        y,
        (_rng.nextDouble() - 0.5) * 30,
        140 + _rng.nextDouble() * 60,
        0.45,
        3 + _rng.nextDouble() * 3,
        const Color(0xFFB0BEC5),
      ));
    }
  }

  void _burst(double x, double y, Color color, int count, double power) {
    for (var i = 0; i < count; i++) {
      final a = _rng.nextDouble() * pi * 2;
      final s = power * (0.3 + _rng.nextDouble() * 0.7);
      particles.add(Particle(
        x,
        y,
        cos(a) * s,
        sin(a) * s + speed * 0.3,
        0.5 + _rng.nextDouble() * 0.5,
        2.5 + _rng.nextDouble() * 4,
        color,
      ));
    }
  }

  void _updateEffects(double dt) {
    final drag = pow(0.08, dt).toDouble();
    for (final p in particles) {
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      p.vx *= drag;
      p.vy *= drag;
      p.life -= dt;
    }
    particles.removeWhere((p) => p.life <= 0);
    if (particles.length > 400) {
      particles.removeRange(0, particles.length - 400);
    }
    for (final t in texts) {
      t.y -= 45 * dt;
      t.life -= dt;
    }
    texts.removeWhere((t) => t.life <= 0);
    shake = max(0.0, shake - dt * 40);
  }

  void _addText(String text, double x, double y, Color color,
      {double size = 20, double life = 1.1}) {
    texts.add(FloatingText(text, x.clamp(90.0, 310.0), y, color,
        size: size, life: life));
  }
}
