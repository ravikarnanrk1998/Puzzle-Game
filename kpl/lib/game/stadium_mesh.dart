import 'dart:math' as math;
import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';

import '../core/league.dart';
import '../core/theme.dart';
import '../data/teams.dart';
import '../ui/widgets/crest.dart';
import '../ui/widgets/paint_utils.dart';
import 'camera.dart';

/// Floodlight tower angles (also used by the ground shader lighting).
const kTowerAngles = [0.6632, 2.2340, 3.8048, 5.3756];

/// Sun direction (towards the sun) for afternoon games - matches the shader.
const kSun = Vec3(0.42, -0.55, 0.72);

/// What the giant screens show.
enum ScreenEvent { none, four, six, out }

/// Ring-shaped quad grid (stands, walls, roof...).
class _Grid {
  _Grid(this.cols, this.rows, this.world, {this.uv, this.colors});

  final int cols, rows;
  final Float32List world;
  final Float32List? uv;
  final Int32List? colors;
  late final Float32List baseUv = Float32List.fromList(uv ?? Float32List(0));
  late final Float32List proj = Float32List(world.length ~/ 3 * 2);
  late final Float32List depth = Float32List(world.length ~/ 3);
  late final Uint16List idx = Uint16List(cols * rows * 6);
  int get nv => world.length ~/ 3;
}

/// Independent quads (boards, sight screens, towers, flags).
class _Quads {
  _Quads(this.world, this.colors);

  final Float32List world;
  final Int32List colors;
  late final Float32List proj = Float32List(world.length ~/ 3 * 2);
  late final Float32List depth = Float32List(world.length ~/ 3);
  late final Uint16List idx = Uint16List(world.length ~/ 12 * 6);
  int get quads => world.length ~/ 12;
}

class _Building {
  _Building(this.cx, this.cy, this.w, this.d, this.h, this.yaw, this.facade, this.tint);
  final double cx, cy, w, d, h, yaw;
  final int facade;
  final double tint;
}

class _Palm {
  _Palm(this.x, this.y, this.h, this.lean, this.seed);
  final double x, y, h, lean;
  final int seed;
}

/// Real-time 3D stadium: geometry is built once and re-projected every frame
/// so cameras can move freely. [day] switches between an afternoon look
/// (sky, sunlit stands, white roof) and a floodlit night.
class StadiumMesh {
  StadiumMesh({required this.home, required this.away, this.seed = 7, this.day = false}) {
    _build();
  }

  final TeamInfo home;
  final TeamInfo away;
  final int seed;
  final bool day;

  double get rx => home.boundary.toDouble();
  double get ry => rx + 4;

  late final _Grid _upper, _lower, _roof, _roofEdge, _backWall, _fascia, _fasciaLed, _wall, _boards;
  late final _Quads _towers, _screens, _ribs, _flags;
  late final List<_Building> _city;
  late final List<_Palm> _palms;
  final List<Vec3> _screenPos = [];

  ui.Image? _crowd, _cheer, _boardImg, _bannerImg;
  final Map<ScreenEvent, ui.Image> _screenImg = {};
  final List<ui.Image> _facadeImg = [];
  Paint? _crowdPaint, _cheerPaint, _boardPaint, _bannerPaint;
  final Map<ScreenEvent, Paint> _screenPaint = {};
  final List<Paint> _facadePaint = [];
  final _rng = math.Random(3);

  Color get _trim => home.secondary.luma < 0.2 ? home.glow : home.secondary;

  // ------------------------------------------------------------ build

  void _ringPoint(List<double> out, double phi, double extra, double z) {
    out
      ..add(math.cos(phi) * (rx + extra))
      ..add(kPitchMid + math.sin(phi) * (ry + extra))
      ..add(z);
  }

  _Grid _ring({
    required int cols,
    required int rows,
    required double e0,
    required double e1,
    required double z0,
    required double z1,
    double? texW,
    double? texH,
    double tileM = 40,
    Color Function(double phi, double f)? color,
  }) {
    final w = <double>[];
    final uv = <double>[];
    final col = <int>[];
    final meanR = rx + (e0 + e1) / 2;
    for (var j = 0; j <= rows; j++) {
      final f = j / rows;
      final e = e0 + (e1 - e0) * f;
      final z = z0 + (z1 - z0) * f;
      for (var i = 0; i <= cols; i++) {
        final phi = -math.pi / 2 + i / cols * math.pi * 2;
        _ringPoint(w, phi, e, z);
        if (texW != null) {
          // Negative u: the rings are seen from the inside, so text reads correctly.
          uv
            ..add(-(phi + math.pi / 2) * meanR / tileM * texW)
            ..add(texH! * (1 - f));
        }
        if (color != null) col.add(color(phi, f).toARGB32());
      }
    }
    return _Grid(
      cols,
      rows,
      Float32List.fromList(w),
      uv: texW != null ? Float32List.fromList(uv) : null,
      colors: color != null ? Int32List.fromList(col) : null,
    );
  }

  /// Brightness of the stands (multiplied with the crowd texture).
  Color _standLight(double phi, double f, {required bool upper}) {
    double k;
    if (day) {
      final sx = kSun.x, sy = kSun.y;
      final sl = math.sqrt(sx * sx + sy * sy);
      final facing = -(math.cos(phi) * sx + math.sin(phi) * sy) / sl;
      k = 0.8 + 0.2 * facing.clamp(0.0, 1.0);
      // The roof shades the top of the upper tier; the upper tier shades the
      // back of the lower one.
      final shadeStart = upper ? 0.5 : 0.62;
      final shade = ((f - shadeStart) / (1 - shadeStart)).clamp(0.0, 1.0);
      k *= 1 - shade * (upper ? 0.4 : 0.3);
    } else {
      k = 0.95 - f * 0.12;
    }
    final v = (k * 255).round().clamp(0, 255);
    return Color.fromARGB(255, v, v, v);
  }

  void _build() {
    const cols = 240;
    _upper = _ring(cols: cols, rows: 6, e0: 23, e1: 34.7, z0: 11.4, z1: 19.5, texW: 1024, texH: 448, color: (p, f) => _standLight(p, f, upper: true));
    _lower = _ring(cols: cols, rows: 6, e0: 9.2, e1: 22.3, z0: 1.5, z1: 8.5, texW: 1024, texH: 448, color: (p, f) => _standLight(p, f, upper: false));
    // Cantilever roof: from the back of the upper tier out over the seats.
    _roof = _ring(
      cols: cols,
      rows: 2,
      e0: 36.5,
      e1: 26,
      z0: 21,
      z1: 24.2,
      color: (p, f) => day
          ? Color.lerp(const Color(0xFF8E96A6), const Color(0xFFDDE2EA), f)!
          : Color.lerp(const Color(0xFF080A16), const Color(0xFF1A1E36), f)!,
    );
    _roofEdge = _ring(
      cols: cols,
      rows: 1,
      e0: 26,
      e1: 26,
      z0: 24.1,
      z1: 25.7,
      color: (p, f) => day ? Color.lerp(const Color(0xFFC9CED8), Colors.white, f)! : Color.lerp(home.glow.darken(0.3), home.glow, f)!,
    );
    _backWall = _ring(
      cols: cols,
      rows: 1,
      e0: 34.8,
      e1: 36.5,
      z0: 19.4,
      z1: 21.1,
      color: (p, f) => day ? const Color(0xFF5C6474) : const Color(0xFF0C0F22),
    );
    _fascia = _ring(cols: cols, rows: 1, e0: 22.4, e1: 22.4, z0: 8.4, z1: 11.4, color: (p, f) => day ? const Color(0xFF1C2238) : const Color(0xFF080A1A));
    _fasciaLed = _ring(cols: cols, rows: 1, e0: 22.32, e1: 22.32, z0: 9.1, z1: 10.7, texW: 2048, texH: 96, tileM: 32);
    _wall = _ring(
      cols: cols,
      rows: 1,
      e0: 9.0,
      e1: 9.0,
      z0: 0,
      z1: 1.55,
      color: (p, f) => Color.lerp(day ? const Color(0xFF26304A) : const Color(0xFF0E1230), _trim, f)!,
    );
    _boards = _ring(cols: cols, rows: 1, e0: 3, e1: 3, z0: 0, z1: 1.2, texW: 2048, texH: 96, tileM: 38);

    final tw = <double>[], tc = <int>[];
    void quad(Vec3 a, Vec3 b, Vec3 c, Vec3 d, Color c0, [Color? c1]) {
      for (final v in [a, b, c, d]) {
        tw
          ..add(v.x)
          ..add(v.y)
          ..add(v.z);
      }
      final top = (c1 ?? c0).toARGB32(), bot = c0.toARGB32();
      tc.addAll([bot, bot, top, top]);
    }

    // Floodlight towers (mast + head panel facing the centre).
    final mast0 = day ? const Color(0xFF6D7486) : const Color(0xFF0C0E1C);
    final mast1 = day ? const Color(0xFFB4BAC6) : const Color(0xFF1A1D32);
    for (final phi in kTowerAngles) {
      final cx = math.cos(phi) * (rx + 42), cy = kPitchMid + math.sin(phi) * (ry + 42);
      final lx = -math.sin(phi), ly = math.cos(phi);
      quad(Vec3(cx - lx * 1.3, cy - ly * 1.3, 0), Vec3(cx + lx * 1.3, cy + ly * 1.3, 0), Vec3(cx + lx * 0.5, cy + ly * 0.5, 50.5),
          Vec3(cx - lx * 0.5, cy - ly * 0.5, 50.5), mast0, mast1);
      quad(Vec3(cx - lx * 5.6, cy - ly * 5.6, 50.5), Vec3(cx + lx * 5.6, cy + ly * 5.6, 50.5), Vec3(cx + lx * 5.6, cy + ly * 5.6, 56.8),
          Vec3(cx - lx * 5.6, cy - ly * 5.6, 56.8), day ? const Color(0xFF3A4152) : const Color(0xFF1B1E30), day ? const Color(0xFF8C93A3) : const Color(0xFF2A2E48));
    }
    _towers = _Quads(Float32List.fromList(tw), Int32List.fromList(tc));

    // Sight screens at both ends.
    tw.clear();
    tc.clear();
    for (final sgn in [1.0, -1.0]) {
      final y = kPitchMid + sgn * (ry + 3.8);
      quad(Vec3(-7.5, y, 0), Vec3(7.5, y, 0), Vec3(7.5, y, 7.2), Vec3(-7.5, y, 7.2), const Color(0xFF04060C), const Color(0xFF0A0D18));
      _screenPos.add(Vec3(0, kPitchMid + sgn * (ry + 22.6), 0));
    }
    _screens = _Quads(Float32List.fromList(tw), Int32List.fromList(tc));

    // Roof ribs underneath the canopy.
    tw.clear();
    tc.clear();
    const ribs = 64;
    for (var i = 0; i < ribs; i++) {
      final phi = -math.pi / 2 + (i + 0.5) / ribs * math.pi * 2;
      final px = -math.sin(phi) * 0.35, py = math.cos(phi) * 0.35;
      Vec3 at(double e, double z, double s) => Vec3(math.cos(phi) * (rx + e) + px * s, kPitchMid + math.sin(phi) * (ry + e) + py * s, z);
      final c = day ? const Color(0xFF6F7788) : const Color(0xFF05060E);
      quad(at(36.3, 20.92, -1), at(36.3, 20.92, 1), at(26.2, 24.08, 1), at(26.2, 24.08, -1), c);
    }
    _ribs = _Quads(Float32List.fromList(tw), Int32List.fromList(tc));

    // Team flags along the roof edge (positions animated every frame).
    tw.clear();
    tc.clear();
    const flags = 56;
    final flagCols = [home.primary, _trim, Colors.white, home.primary.lighten(0.15)];
    for (var i = 0; i < flags; i++) {
      final c = flagCols[i % flagCols.length];
      // Pole + two flag panels (filled in by _waveFlags).
      quad(Vec3.zero, Vec3.zero, Vec3.zero, Vec3.zero, const Color(0xFFDDE1E8));
      quad(Vec3.zero, Vec3.zero, Vec3.zero, Vec3.zero, c.darken(0.08), c);
      quad(Vec3.zero, Vec3.zero, Vec3.zero, Vec3.zero, c.darken(0.2), c.darken(0.1));
    }
    _flags = _Quads(Float32List.fromList(tw), Int32List.fromList(tc));

    // City skyline and palms beyond the stadium.
    final rng = math.Random(seed * 31 + home.id.hashCode);
    _city = [];
    for (var i = 0; i < 46; i++) {
      final phi = rng.nextDouble() * math.pi * 2;
      final r = rx + 115 + rng.nextDouble() * 150;
      final h = 34 + math.pow(rng.nextDouble(), 1.6) * 120;
      _city.add(_Building(
        math.cos(phi) * r,
        kPitchMid + math.sin(phi) * (r + 4),
        16 + rng.nextDouble() * 26,
        14 + rng.nextDouble() * 20,
        h.toDouble(),
        phi + (rng.nextDouble() - 0.5) * 0.5,
        rng.nextInt(3),
        0.82 + rng.nextDouble() * 0.18,
      ));
    }
    _palms = [];
    for (var g = 0; g < 9; g++) {
      final phi = rng.nextDouble() * math.pi * 2;
      final n = 2 + rng.nextInt(3);
      for (var k = 0; k < n; k++) {
        final p = phi + (rng.nextDouble() - 0.5) * 0.12;
        final e = 40 + rng.nextDouble() * 9;
        _palms.add(_Palm(
          math.cos(p) * (rx + e),
          kPitchMid + math.sin(p) * (ry + e),
          27 + rng.nextDouble() * 9,
          (rng.nextDouble() - 0.5) * 0.25,
          rng.nextInt(1 << 20),
        ));
      }
    }
  }

  // --------------------------------------------------------- textures

  /// Generates the crowd, board, banner, screen and facade textures.
  Future<void> prepare() async {
    if (_crowd != null) return;
    final results = await Future.wait([
      _crowdTexture(false),
      _crowdTexture(true),
      _boardTexture(),
      _bannerTexture(),
      _screenTexture(ScreenEvent.none),
      _screenTexture(ScreenEvent.four),
      _screenTexture(ScreenEvent.six),
      _screenTexture(ScreenEvent.out),
      _facadeTexture(0),
      _facadeTexture(1),
      _facadeTexture(2),
    ]);
    _crowd = results[0];
    _cheer = results[1];
    _boardImg = results[2];
    _bannerImg = results[3];
    _screenImg[ScreenEvent.none] = results[4];
    _screenImg[ScreenEvent.four] = results[5];
    _screenImg[ScreenEvent.six] = results[6];
    _screenImg[ScreenEvent.out] = results[7];
    _facadeImg.addAll(results.sublist(8));
    Paint tex(ui.Image img, {TileMode y = TileMode.clamp}) => Paint()
      ..shader = ImageShader(img, TileMode.repeated, y, Float64List.fromList(_identity), filterQuality: FilterQuality.medium);
    _crowdPaint = tex(_crowd!);
    _cheerPaint = tex(_cheer!);
    _boardPaint = tex(_boardImg!);
    _bannerPaint = tex(_bannerImg!);
    for (final e in _screenImg.entries) {
      _screenPaint[e.key] = tex(e.value);
    }
    for (final f in _facadeImg) {
      _facadePaint.add(tex(f, y: TileMode.repeated));
    }
  }

  static const _identity = <double>[1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];

  Future<ui.Image> _render(int w, int h, void Function(Canvas c) draw) async {
    final rec = ui.PictureRecorder();
    final c = Canvas(rec, Rect.fromLTWH(0, 0, w.toDouble(), h.toDouble()));
    draw(c);
    final pic = rec.endRecording();
    final img = await pic.toImage(w, h);
    pic.dispose();
    return img;
  }

  Future<ui.Image> _crowdTexture(bool cheer) => _render(1024, 448, (c) {
        const w = 1024.0, h = 448.0, rows = 14;
        const rh = h / rows;
        final rng = math.Random(seed);
        final palette = <Color>[
          home.primary,
          home.primary,
          home.primary,
          home.primary.lighten(0.25),
          _trim,
          _trim,
          Colors.white,
          away.primary.lighten(0.1),
          away.primary,
          const Color(0xFFFF5F6D),
          const Color(0xFF7CF5FF),
          const Color(0xFFFFD166),
        ];
        const skins = [Color(0xFF8A5A3B), Color(0xFF6E4630), Color(0xFFA0704C), Color(0xFF5A3A26), Color(0xFFB98563)];
        final seatTop = day ? home.primary.lighten(0.45).darken(0.1) : const Color(0xFF1B2046);
        final seatBottom = day ? home.primary.darken(0.15) : const Color(0xFF0B0E25);
        for (var r = 0; r < rows; r++) {
          final y0 = r * rh;
          final band = Rect.fromLTWH(0, y0, w, rh);
          c.drawRect(
            band,
            Paint()
              ..shader = LinearGradient(
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
                colors: [seatTop, seatBottom],
              ).createShader(band),
          );
          c.drawRect(Rect.fromLTWH(0, y0 + rh - 2, w, 2), Paint()..color = day ? const Color(0xFFB9BFCB) : const Color(0xFF2C3468));
          var x = rng.nextDouble() * 8;
          while (x < w) {
            final aisle = (x % 256) < 16;
            if (aisle) {
              x += 16;
              continue;
            }
            final empty = rng.nextDouble() < (day ? 0.05 : 0.07);
            final col = palette[rng.nextInt(palette.length)];
            final skin = skins[rng.nextInt(skins.length)];
            final jit = (rng.nextDouble() - 0.5) * 3;
            final armsUp = rng.nextDouble() < 0.55;
            final flag = rng.nextDouble() < 0.05;
            final phone = !day && rng.nextDouble() < 0.03;
            final cap = day && rng.nextDouble() < 0.25;
            final flagCol = rng.nextBool() ? home.primary : _trim;
            for (final dx in const [0.0, -w, w]) {
              final cx = x + dx;
              if (cx < -14 || cx > w + 14) continue;
              if (empty) {
                c.drawRRect(
                  RRect.fromRectAndRadius(Rect.fromCenter(center: Offset(cx, y0 + rh * 0.6), width: 11, height: 11), const Radius.circular(3)),
                  Paint()..color = day ? home.primary.lighten(0.2) : const Color(0xFF283066),
                );
                continue;
              }
              final hy = y0 + rh * 0.32 + jit;
              final body = RRect.fromRectAndRadius(
                Rect.fromCenter(center: Offset(cx, y0 + rh * 0.68 + jit), width: 12, height: 16),
                const Radius.circular(4.5),
              );
              c.drawRRect(
                body,
                Paint()
                  ..shader = LinearGradient(
                    begin: Alignment.topCenter,
                    end: Alignment.bottomCenter,
                    colors: [col.lighten(day ? 0.3 : 0.22), col.darken(day ? 0.25 : 0.4)],
                  ).createShader(body.outerRect),
              );
              if (cheer && armsUp) {
                final arm = Paint()
                  ..color = skin
                  ..strokeWidth = 2.6
                  ..strokeCap = StrokeCap.round;
                c.drawLine(Offset(cx - 4.5, y0 + rh * 0.56 + jit), Offset(cx - 7.5, hy - 9), arm);
                c.drawLine(Offset(cx + 4.5, y0 + rh * 0.56 + jit), Offset(cx + 7.5, hy - 9), arm);
              }
              c.drawCircle(Offset(cx, hy), 4.4, Paint()..color = skin);
              c.drawArc(Rect.fromCircle(center: Offset(cx, hy), radius: 4.6), math.pi * 1.05, math.pi * 0.9, true,
                  Paint()..color = cap ? col.darken(0.1) : const Color(0xFF17100C));
              if (flag) {
                final top = hy - (cheer ? 18 : 12);
                c.drawLine(Offset(cx + 5, hy + 4), Offset(cx + 5, top), Paint()
                  ..color = const Color(0xFFDDDDDD)
                  ..strokeWidth = 1.2);
                final wave = cheer ? 3.0 : 0.0;
                c.drawPath(
                  Path()
                    ..moveTo(cx + 5, top)
                    ..quadraticBezierTo(cx + 10, top - wave, cx + 16, top + 1)
                    ..lineTo(cx + 16, top + 8)
                    ..quadraticBezierTo(cx + 10, top + 7 - wave, cx + 5, top + 7)
                    ..close(),
                  Paint()..color = flagCol,
                );
              }
              if (phone) {
                c.drawCircle(Offset(cx + 3, hy - 2), 3.2, Paint()
                  ..color = Colors.white.o(0.9)
                  ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 1.5));
              }
            }
            x += 13 + rng.nextDouble() * 5;
          }
        }
        // Stair aisles.
        for (var ax = 0.0; ax < w; ax += 256) {
          final r = Rect.fromLTWH(ax, 0, 16, h);
          c.drawRect(r, Paint()..color = day ? const Color(0xFFC7CCD6) : const Color(0xFF1E2448));
          for (var y = 0.0; y < h; y += rh / 2) {
            c.drawLine(Offset(ax, y), Offset(ax + 16, y), Paint()
              ..color = day ? const Color(0xFF9AA1AF) : const Color(0xFF2D3563)
              ..strokeWidth = 1.2);
          }
        }
      });

  void _crest(Canvas c, TeamInfo t, Offset center, double h) {
    final w = h / 1.12;
    c.save();
    c.translate(center.dx - w / 2, center.dy - h / 2);
    CrestPainter(t).paint(c, Size(w, h));
    c.restore();
  }

  Future<ui.Image> _boardTexture() => _render(2048, 96, (c) {
        const h = 96.0;
        void panel(double x, List<Color> bg, void Function(Rect r) content) {
          final r = Rect.fromLTWH(x, 0, 512, h);
          c.drawRect(r, Paint()..shader = LinearGradient(colors: bg).createShader(r));
          c.drawRect(Rect.fromLTWH(x, h - 6, 512, 6), Paint()..color = Colors.black.o(0.25));
          content(r);
        }

        final homeText = home.primary.luma > 0.6 ? const Color(0xFF14151E) : Colors.white;
        panel(0, [home.primary.darken(0.15), home.primary, home.primary.darken(0.2)], (r) {
          _crest(c, home, Offset(r.left + 52, h / 2), 80);
          paintText(c, home.name.toUpperCase(), Offset(r.left + 98, h / 2 + 2), T.display(home.name.length > 20 ? 38 : 46, color: homeText, weight: FontWeight.w700, spacing: 1),
              anchor: Alignment.centerLeft);
        });
        panel(512, const [Color(0xFF0B0F33), Color(0xFF1B1F5A), Color(0xFF0B0F33)], (r) {
          paintText(c, League.name, Offset(r.left + 200, h / 2 + 4), T.display(64, color: AppColors.gold, weight: FontWeight.w700, spacing: 2), anchor: Alignment.centerRight);
          paintText(c, '${League.startYear}', Offset(r.left + 214, h / 2 + 4), T.display(64, color: AppColors.cyan, weight: FontWeight.w700, spacing: 6), anchor: Alignment.centerLeft);
        });
        final trimText = _trim.luma > 0.6 ? const Color(0xFF14151E) : Colors.white;
        panel(1024, [_trim.darken(0.1), _trim, _trim.darken(0.15)], (r) {
          paintText(c, 'FOUR  •  SIX  •  WICKET', r.center + const Offset(0, 3), T.display(48, color: trimText, weight: FontWeight.w700, spacing: 2));
        });
        final awayText = away.primary.luma > 0.6 ? const Color(0xFF14151E) : Colors.white;
        panel(1536, [away.primary.darken(0.15), away.primary, away.primary.darken(0.2)], (r) {
          _crest(c, away, Offset(r.left + 52, h / 2), 80);
          paintText(c, away.name.toUpperCase(), Offset(r.left + 98, h / 2 + 2), T.display(away.name.length > 20 ? 38 : 46, color: awayText, weight: FontWeight.w700, spacing: 1),
              anchor: Alignment.centerLeft);
        });
      });

  Future<ui.Image> _bannerTexture() => _render(2048, 96, (c) {
        const h = 96.0;
        final r = const Rect.fromLTWH(0, 0, 2048, h);
        c.drawRect(r, Paint()..shader = LinearGradient(colors: [home.primary.darken(0.25), home.primary, home.primary.darken(0.25)]).createShader(r));
        final text = home.primary.luma > 0.6 ? const Color(0xFF14151E) : Colors.white;
        for (var i = 0; i < 4; i++) {
          final x = i * 512.0;
          _crest(c, home, Offset(x + 50, h / 2), 76);
          paintText(c, home.name.toUpperCase(), Offset(x + 96, h / 2 + 3), T.display(home.name.length > 20 ? 36 : 44, color: text, weight: FontWeight.w700, spacing: 1),
              anchor: Alignment.centerLeft);
        }
        c.drawRect(const Rect.fromLTWH(0, 0, 2048, 5), Paint()..color = _trim);
        c.drawRect(const Rect.fromLTWH(0, h - 5, 2048, 5), Paint()..color = _trim);
      });

  Future<ui.Image> _screenTexture(ScreenEvent e) => _render(1024, 288, (c) {
        const w = 1024.0, h = 288.0;
        final r = const Rect.fromLTWH(0, 0, w, h);
        c.drawRect(r, Paint()..color = const Color(0xFF05060C));
        final inner = r.deflate(10);
        c.drawRect(
          inner,
          Paint()
            ..shader = RadialGradient(radius: 1.0, colors: [home.primary.lighten(0.15), home.primary.darken(0.45)]).createShader(inner),
        );
        // LED pixel grid.
        final grid = Paint()..color = Colors.black.o(0.18);
        for (var x = inner.left; x < inner.right; x += 6) {
          c.drawLine(Offset(x, inner.top), Offset(x, inner.bottom), grid);
        }
        switch (e) {
          case ScreenEvent.none:
            _crest(c, home, Offset(w * 0.5, h * 0.42), 170);
            paintText(c, home.name.toUpperCase(), Offset(w * 0.5, h * 0.86), T.display(46, color: Colors.white, weight: FontWeight.w700, spacing: 3));
            paintText(c, '${League.name} ${League.startYear}', Offset(w * 0.14, h * 0.18), T.display(34, color: AppColors.gold, weight: FontWeight.w700));
            paintText(c, '${home.id} v ${away.id}', Offset(w * 0.86, h * 0.18), T.display(34, color: Colors.white, weight: FontWeight.w700));
          case ScreenEvent.four:
          case ScreenEvent.six:
          case ScreenEvent.out:
            final (label, color) = switch (e) {
              ScreenEvent.four => ('FOUR!', AppColors.cyan),
              ScreenEvent.six => ('SIX!', const Color(0xFFC77DFF)),
              _ => ('OUT!', AppColors.red),
            };
            c.drawRect(inner, Paint()..color = Colors.black.o(0.45));
            paintText(c, label, Offset(w * 0.5, h * 0.52), T.display(210, color: color, weight: FontWeight.w700, spacing: 8, shadows: [Shadow(color: color, blurRadius: 30)]));
            _crest(c, home, Offset(w * 0.1, h * 0.5), 110);
            _crest(c, home, Offset(w * 0.9, h * 0.5), 110);
        }
      });

  /// Building facades: glass, concrete and white towers (lit windows at night).
  Future<ui.Image> _facadeTexture(int kind) => _render(256, 256, (c) {
        const s = 256.0;
        final rng = math.Random(kind * 97 + 5);
        final base = day
            ? switch (kind) {
                0 => const Color(0xFF6F8FB0),
                1 => const Color(0xFFD9CDB4),
                _ => const Color(0xFFE6E9EE),
              }
            : const Color(0xFF151827);
        c.drawRect(const Rect.fromLTWH(0, 0, s, s), Paint()..color = base);
        const floors = 11, bays = 8;
        const fh = s / floors, bw = s / bays;
        for (var f = 0; f < floors; f++) {
          for (var b = 0; b < bays; b++) {
            final r = Rect.fromLTWH(b * bw + 3, f * fh + 4, bw - 6, fh - 7);
            Color win;
            if (day) {
              win = switch (kind) {
                0 => Color.lerp(const Color(0xFF2E4D70), const Color(0xFFA9C7E6), rng.nextDouble() * 0.6)!,
                1 => const Color(0xFF3B4555),
                _ => Color.lerp(const Color(0xFF516378), const Color(0xFF8FA5BD), rng.nextDouble())!,
              };
            } else {
              win = rng.nextDouble() < 0.42 ? Color.lerp(const Color(0xFFFFD27A), const Color(0xFFFFF3D0), rng.nextDouble())! : const Color(0xFF0B0D16);
            }
            c.drawRect(r, Paint()..color = win);
          }
        }
      });

  void dispose() {
    for (final img in [_crowd, _cheer, _boardImg, _bannerImg, ..._screenImg.values, ..._facadeImg]) {
      img?.dispose();
    }
  }

  // ------------------------------------------------------------ draw

  static const _near = 0.6;

  int _cullGrid(_Grid g, Camera3D cam, Size size) {
    final w = g.world;
    final n = g.nv;
    for (var i = 0; i < n; i++) {
      g.depth[i] = cam.projectInto(w[i * 3], w[i * 3 + 1], w[i * 3 + 2], g.proj, i);
    }
    var k = 0;
    final c1 = g.cols + 1;
    final p = g.proj, d = g.depth;
    final maxX = size.width + 80, maxY = size.height + 80;
    for (var r = 0; r < g.rows; r++) {
      for (var c = 0; c < g.cols; c++) {
        final v00 = r * c1 + c, v10 = v00 + 1, v01 = v00 + c1, v11 = v01 + 1;
        if (d[v00] < _near || d[v10] < _near || d[v01] < _near || d[v11] < _near) continue;
        final x0 = p[v00 * 2], x1 = p[v10 * 2], x2 = p[v01 * 2], x3 = p[v11 * 2];
        if ((x0 < -80 && x1 < -80 && x2 < -80 && x3 < -80) || (x0 > maxX && x1 > maxX && x2 > maxX && x3 > maxX)) continue;
        final y0 = p[v00 * 2 + 1], y1 = p[v10 * 2 + 1], y2 = p[v01 * 2 + 1], y3 = p[v11 * 2 + 1];
        if ((y0 < -80 && y1 < -80 && y2 < -80 && y3 < -80) || (y0 > maxY && y1 > maxY && y2 > maxY && y3 > maxY)) continue;
        g.idx[k++] = v00;
        g.idx[k++] = v10;
        g.idx[k++] = v11;
        g.idx[k++] = v00;
        g.idx[k++] = v11;
        g.idx[k++] = v01;
      }
    }
    return k;
  }

  void _drawGrid(Canvas canvas, _Grid g, Camera3D cam, Size size, {Paint? texPaint, Color? fallback}) {
    final k = _cullGrid(g, cam, size);
    if (k == 0) return;
    final idx = Uint16List.sublistView(g.idx, 0, k);
    if (g.uv != null && texPaint != null) {
      final v = ui.Vertices.raw(ui.VertexMode.triangles, g.proj, textureCoordinates: g.uv, colors: g.colors, indices: idx);
      canvas.drawVertices(v, BlendMode.modulate, texPaint);
      v.dispose();
    } else if (g.colors != null) {
      final v = ui.Vertices.raw(ui.VertexMode.triangles, g.proj, colors: g.colors, indices: idx);
      canvas.drawVertices(v, BlendMode.dst, Paint());
      v.dispose();
    } else {
      final v = ui.Vertices.raw(ui.VertexMode.triangles, g.proj, indices: idx);
      canvas.drawVertices(v, BlendMode.srcOver, Paint()..color = fallback ?? const Color(0xFF151A36));
      v.dispose();
    }
  }

  void _drawQuads(Canvas canvas, _Quads q, Camera3D cam, Size size) {
    final w = q.world;
    final n = w.length ~/ 3;
    for (var i = 0; i < n; i++) {
      q.depth[i] = cam.projectInto(w[i * 3], w[i * 3 + 1], w[i * 3 + 2], q.proj, i);
    }
    var k = 0;
    for (var i = 0; i < q.quads; i++) {
      final b = i * 4;
      if (q.depth[b] < _near || q.depth[b + 1] < _near || q.depth[b + 2] < _near || q.depth[b + 3] < _near) continue;
      q.idx[k++] = b;
      q.idx[k++] = b + 1;
      q.idx[k++] = b + 2;
      q.idx[k++] = b;
      q.idx[k++] = b + 2;
      q.idx[k++] = b + 3;
    }
    if (k == 0) return;
    final v = ui.Vertices.raw(ui.VertexMode.triangles, q.proj, colors: q.colors, indices: Uint16List.sublistView(q.idx, 0, k));
    canvas.drawVertices(v, BlendMode.dst, Paint());
    v.dispose();
  }

  void _scroll(_Grid g, double px) {
    final uv = g.uv!, base = g.baseUv;
    for (var i = 0; i < uv.length; i += 2) {
      uv[i] = base[i] + px;
    }
  }

  void _waveFlags(double time) {
    final w = _flags.world;
    final n = _flags.quads ~/ 3;
    var o = 0;
    void put(double x, double y, double z) {
      w[o++] = x;
      w[o++] = y;
      w[o++] = z;
    }

    for (var i = 0; i < n; i++) {
      final phi = -math.pi / 2 + (i + 0.5) / n * math.pi * 2;
      final ox = math.cos(phi), oy = math.sin(phi);
      final tx = -oy, ty = ox; // tangent
      final bx = ox * (rx + 25.8), by = kPitchMid + oy * (ry + 25.8);
      const z0 = 25.6, z1 = 29.6;
      // Pole.
      put(bx - tx * 0.07, by - ty * 0.07, z0);
      put(bx + tx * 0.07, by + ty * 0.07, z0);
      put(bx + tx * 0.07, by + ty * 0.07, z1);
      put(bx - tx * 0.07, by - ty * 0.07, z1);
      // Flag in two panels, rippling in the breeze.
      final ph = time * 3.2 + i * 0.9;
      double off(double u) => math.sin(ph - u * 3) * 0.35 * u;
      Vec3 at(double u, double v) => Vec3(bx + tx * 2.5 * u + ox * off(u), by + ty * 2.5 * u + oy * off(u), z1 - 0.05 - v * 1.5 - u * u * 0.15);
      for (final (u0, u1) in const [(0.0, 0.5), (0.5, 1.0)]) {
        for (final p in [at(u0, 1), at(u1, 1), at(u1, 0), at(u0, 0)]) {
          put(p.x, p.y, p.z);
        }
      }
    }
  }

  /// Draws the city, stands, roof, screens, boards and floodlights for [cam].
  void paint(Canvas canvas, Size size, Camera3D cam, {double time = 0, double hype = 0, ScreenEvent event = ScreenEvent.none}) {
    _drawCity(canvas, cam, size);
    _drawPalms(canvas, cam, size, time);
    _drawQuads(canvas, _towers, cam, size);
    _drawGrid(canvas, _roof, cam, size);
    _drawQuads(canvas, _ribs, cam, size);
    _drawGrid(canvas, _backWall, cam, size);
    final crowd = hype > 0.55 && ((time * 4).floor().isEven) ? (_cheerPaint ?? _crowdPaint) : _crowdPaint;
    _drawGrid(canvas, _upper, cam, size, texPaint: crowd, fallback: day ? home.primary.lighten(0.2) : const Color(0xFF1A2050));
    _drawGrid(canvas, _roofEdge, cam, size);
    _waveFlags(time);
    _drawQuads(canvas, _flags, cam, size);
    _drawGrid(canvas, _fascia, cam, size);
    _scroll(_fasciaLed, -time * (14 + hype * 30));
    _drawGrid(canvas, _fasciaLed, cam, size, texPaint: _bannerPaint, fallback: home.primary);
    _drawBigScreens(canvas, cam, size, event);
    _drawGrid(canvas, _lower, cam, size, texPaint: crowd, fallback: day ? home.primary.lighten(0.1) : const Color(0xFF1E2458));
    _drawGrid(canvas, _wall, cam, size);
    _drawQuads(canvas, _screens, cam, size);
    _scroll(_boards, time * (22 + hype * 60));
    _drawGrid(canvas, _boards, cam, size, texPaint: _boardPaint, fallback: home.primary);
    if (!day) {
      _flashes(canvas, size, time, hype);
      _lights(canvas, cam, size, time);
    }
  }

  void _drawBigScreens(Canvas canvas, Camera3D cam, Size size, ScreenEvent event) {
    final paint = _screenPaint[event] ?? _screenPaint[ScreenEvent.none];
    for (final c in _screenPos) {
      final sgn = c.y > kPitchMid ? 1.0 : -1.0;
      const hw = 13.0, z0 = 11.8, z1 = 19.6;
      final corners = [Vec3(-hw * sgn, c.y, z1), Vec3(hw * sgn, c.y, z1), Vec3(hw * sgn, c.y, z0), Vec3(-hw * sgn, c.y, z0)];
      if (corners.any((v) => cam.depth(v) < _near)) continue;
      final pts = corners.map(cam.project).toList();
      // Frame.
      final frame = Path()..addPolygon(pts, true);
      canvas.drawPath(frame, Paint()..color = const Color(0xFF06070D));
      if (paint == null) continue;
      final pos = Float32List.fromList([for (final p in pts) ...[p.dx, p.dy]]);
      final uv = Float32List.fromList([0, 0, 1024, 0, 1024, 288, 0, 288]);
      final v = ui.Vertices.raw(ui.VertexMode.triangles, pos, textureCoordinates: uv, indices: Uint16List.fromList([0, 1, 2, 0, 2, 3]));
      canvas.drawVertices(v, BlendMode.srcOver, paint);
      v.dispose();
      // Support legs under the screen.
      for (final x in [-hw * 0.7, hw * 0.7]) {
        final a = Vec3(x, c.y, z0), b = Vec3(x, c.y, z0 - 1.4);
        if (cam.depth(b) < _near) continue;
        canvas.drawLine(cam.project(a), cam.project(b), Paint()
          ..color = const Color(0xFF2A2F3D)
          ..strokeWidth = math.max(1.0, cam.scaleAt(a) * 0.5));
      }
    }
  }

  void _drawCity(Canvas canvas, Camera3D cam, Size size) {
    if (_facadePaint.length < 3) return;
    final order = List.of(_city)..sort((a, b) => cam.depthXYZ(b.cx, b.cy, b.h / 2).compareTo(cam.depthXYZ(a.cx, a.cy, a.h / 2)));
    final batches = [<double>[], <double>[], <double>[]];
    final uvs = [<double>[], <double>[], <double>[]];
    final cols = [<int>[], <int>[], <int>[]];
    const pxPerM = 256 / 26.0;
    final sun = kSun;
    for (final b in order) {
      final cy = math.cos(b.yaw), sy = math.sin(b.yaw);
      final hx = b.w / 2, hy = b.d / 2;
      final base = [
        Vec3(b.cx + cy * hx - sy * hy, b.cy + sy * hx + cy * hy, 0),
        Vec3(b.cx - cy * hx - sy * hy, b.cy - sy * hx + cy * hy, 0),
        Vec3(b.cx - cy * hx + sy * hy, b.cy - sy * hx - cy * hy, 0),
        Vec3(b.cx + cy * hx + sy * hy, b.cy + sy * hx - cy * hy, 0),
      ];
      final top = [for (final p in base) p.withZ(b.h)];
      if (cam.depthXYZ(b.cx, b.cy, b.h) < 5) continue;
      for (var f = 0; f < 4; f++) {
        final a = base[f], c = base[(f + 1) % 4];
        final mx = (a.x + c.x) / 2, my = (a.y + c.y) / 2;
        final nx = mx - b.cx, ny = my - b.cy;
        if (nx * (cam.pos.x - mx) + ny * (cam.pos.y - my) <= 0) continue;
        final pts = [top[f], top[(f + 1) % 4], c, a];
        if (pts.any((v) => cam.depth(v) < 2)) continue;
        final nl = math.sqrt(nx * nx + ny * ny);
        final lit = day ? (0.7 + 0.3 * ((nx * sun.x + ny * sun.y) / nl).clamp(0.0, 1.0)) * b.tint : 0.9;
        final g = (lit * 255).round().clamp(0, 255);
        final col = Color.fromARGB(255, g, g, g).toARGB32();
        final width = (a - c).length;
        final list = batches[b.facade], uv = uvs[b.facade];
        final start = list.length ~/ 2;
        for (final p in pts) {
          final s = cam.project(p);
          list
            ..add(s.dx)
            ..add(s.dy);
        }
        final u0 = (f * 37.0 + b.cx) * pxPerM;
        uv.addAll([u0, 0, u0 + width * pxPerM, 0, u0 + width * pxPerM, b.h * pxPerM, u0, b.h * pxPerM]);
        cols[b.facade].addAll([col, col, col, col]);
        assert(start >= 0);
      }
    }
    for (var k = 0; k < 3; k++) {
      final pos = batches[k];
      if (pos.isEmpty) continue;
      final quads = pos.length ~/ 8;
      final idx = Uint16List(quads * 6);
      for (var q = 0; q < quads; q++) {
        idx.setAll(q * 6, [q * 4, q * 4 + 1, q * 4 + 2, q * 4, q * 4 + 2, q * 4 + 3]);
      }
      final v = ui.Vertices.raw(ui.VertexMode.triangles, Float32List.fromList(pos),
          textureCoordinates: Float32List.fromList(uvs[k]), colors: Int32List.fromList(cols[k]), indices: idx);
      canvas.drawVertices(v, BlendMode.modulate, _facadePaint[k]);
      v.dispose();
    }
    if (!day) {
      // Aviation lights on the tallest towers.
      final blink = Paint()..color = const Color(0xFFFF3B30);
      for (final b in _city) {
        if (b.h < 90) continue;
        final p = Vec3(b.cx, b.cy, b.h + 1);
        if (cam.depth(p) < 5) continue;
        canvas.drawCircle(cam.project(p), 1.6, blink);
      }
    }
  }

  void _drawPalms(Canvas canvas, Camera3D cam, Size size, double time) {
    final trunkCol = day ? const Color(0xFF7A5C3E) : const Color(0xFF0A0C12);
    final leafA = day ? const Color(0xFF2F6E2C) : const Color(0xFF080C0E);
    final leafB = day ? const Color(0xFF4C9440) : const Color(0xFF0D1316);
    for (final p in _palms) {
      final base = Vec3(p.x, p.y, 8);
      final topW = Vec3(p.x + p.lean * p.h * 0.3, p.y, p.h);
      if (cam.depth(topW) < 4 || cam.depth(base) < 4) continue;
      final b = cam.project(base), t = cam.project(topW);
      if (t.dx < -200 || t.dx > size.width + 200) continue;
      final s = cam.scaleAt(topW);
      if (s < 0.4) continue;
      final mid = Offset.lerp(b, t, 0.55)! + Offset(p.lean * s * 2, 0);
      canvas.drawPath(
        Path()
          ..moveTo(b.dx, b.dy)
          ..quadraticBezierTo(mid.dx, mid.dy, t.dx, t.dy),
        Paint()
          ..style = PaintingStyle.stroke
          ..strokeCap = StrokeCap.round
          ..strokeWidth = math.max(1.2, 0.55 * s)
          ..color = trunkCol,
      );
      final rng = math.Random(p.seed);
      final sway = math.sin(time * 1.3 + p.seed) * 0.08;
      for (var i = 0; i < 9; i++) {
        final a = i / 9 * math.pi * 2 + rng.nextDouble() * 0.4 + sway;
        final len = (4.2 + rng.nextDouble() * 1.8) * s;
        final dx = math.cos(a) * len;
        final dropY = (1.2 + rng.nextDouble()) * s;
        final up = math.sin(a) * len * 0.25;
        final end = t + Offset(dx, dropY - up * 0.2);
        final ctrl = t + Offset(dx * 0.55, -len * 0.28 - up);
        final leaf = Path()
          ..moveTo(t.dx, t.dy)
          ..quadraticBezierTo(ctrl.dx, ctrl.dy - 0.3 * s, end.dx, end.dy)
          ..quadraticBezierTo(ctrl.dx, ctrl.dy + 0.35 * s, t.dx, t.dy + 0.2 * s)
          ..close();
        canvas.drawPath(leaf, Paint()..color = i.isEven ? leafA : leafB);
      }
    }
  }

  void _flashes(Canvas canvas, Size size, double time, double hype) {
    final g = _lower;
    final count = (3 + hype * 22).round();
    final p = Paint()..maskFilter = const MaskFilter.blur(BlurStyle.normal, 2);
    for (var i = 0; i < count; i++) {
      final v = _rng.nextInt(g.nv);
      if (g.depth[v] < _near) continue;
      final x = g.proj[v * 2] + (_rng.nextDouble() - 0.5) * 30, y = g.proj[v * 2 + 1] + (_rng.nextDouble() - 0.5) * 10;
      if (x < 0 || x > size.width || y < 0 || y > size.height) continue;
      p.color = Colors.white.o(0.5 + _rng.nextDouble() * 0.5);
      canvas.drawCircle(Offset(x, y), 1.4 + hype * 1.6, p);
    }
  }

  void _lights(Canvas canvas, Camera3D cam, Size size, double time) {
    for (final phi in kTowerAngles) {
      final head = Vec3(math.cos(phi) * (rx + 42), kPitchMid + math.sin(phi) * (ry + 42), 53.6);
      if (cam.depth(head) < 2) continue;
      final p = cam.project(head);
      if (p.dx < -400 || p.dx > size.width + 400 || p.dy < -400 || p.dy > size.height + 300) continue;
      final s = cam.scaleAt(head);
      final r = math.max(26.0, 32 * s);
      canvas.drawCircle(
        p,
        r,
        Paint()
          ..blendMode = BlendMode.plus
          ..shader = RadialGradient(colors: [const Color(0xFFFFF4D6).o(0.55), const Color(0xFFFFF4D6).o(0)]).createShader(Rect.fromCircle(center: p, radius: r)),
      );
      final lx = -math.sin(phi), ly = math.cos(phi);
      final lamp = Paint()..color = const Color(0xFFFFFDF2);
      for (var i = 0; i < 6; i++) {
        for (var j = 0; j < 3; j++) {
          final off = (i - 2.5) * 1.6;
          final q = cam.project(Vec3(head.x + lx * off, head.y + ly * off, 51.6 + j * 2));
          canvas.drawCircle(q, math.max(0.9, 0.55 * s), lamp);
        }
      }
      // Anamorphic lens flare streak.
      final streak = Rect.fromCenter(center: p, width: size.width * 0.5, height: math.max(2, r * 0.08));
      canvas.drawRect(
        streak,
        Paint()
          ..blendMode = BlendMode.plus
          ..shader = LinearGradient(colors: [Colors.transparent, const Color(0xFF9FD8FF).o(0.22), Colors.transparent]).createShader(streak),
      );
    }
  }
}
