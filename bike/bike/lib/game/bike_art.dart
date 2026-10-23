import 'dart:math';

import 'package:flutter/material.dart';

import '../shop/catalog.dart';

// The fixed shapes, built once instead of every frame. They all face up the
// screen and are centred on the origin.

final Path _streetBody = Path()
  ..moveTo(0, -30)
  ..quadraticBezierTo(11, -24, 11, -8)
  ..lineTo(9, 18)
  ..quadraticBezierTo(0, 26, -9, 18)
  ..lineTo(-11, -8)
  ..quadraticBezierTo(-11, -24, 0, -30)
  ..close();

final Path _windshield = Path()
  ..moveTo(-7, -22)
  ..quadraticBezierTo(0, -29, 7, -22)
  ..lineTo(5, -17)
  ..lineTo(-5, -17)
  ..close();

/// Slimmer than the street bike, with a high front mudguard.
final Path _dirtBody = Path()
  ..moveTo(0, -31)
  ..quadraticBezierTo(8, -27, 8, -10)
  ..lineTo(7, 16)
  ..quadraticBezierTo(0, 25, -7, 16)
  ..lineTo(-8, -10)
  ..quadraticBezierTo(-8, -27, 0, -31)
  ..close();

/// A teardrop tank and the rear mudguard: all the paint a chopper shows.
final Path _chopperBody = Path()
  ..addOval(Rect.fromCenter(center: const Offset(0, -8), width: 16, height: 24))
  ..addRRect(RRect.fromRectAndRadius(
    Rect.fromCenter(center: const Offset(0, 19), width: 15, height: 16),
    const Radius.circular(7),
  ));

final Path _superbikeBody = Path()
  ..addPolygon(const [
    Offset(0, -32),
    Offset(9, -22),
    Offset(12, -6),
    Offset(10, 14),
    Offset(6, 25),
    Offset(-6, 25),
    Offset(-10, 14),
    Offset(-12, -6),
    Offset(-9, -22),
  ], true);

final Path _superbikeScreen = Path()
  ..addPolygon(const [Offset(0, -29), Offset(6, -21), Offset(-6, -21)], true);

final Path _sedanBody = Path()
  ..addRRect(RRect.fromRectAndRadius(
      const Rect.fromLTWH(-19, -38, 38, 76), const Radius.circular(11)));

/// Narrower at the nose.
final Path _sportsBody = Path()
  ..moveTo(0, -38)
  ..quadraticBezierTo(15, -38, 17, -26)
  ..lineTo(19, 27)
  ..quadraticBezierTo(19, 38, 8, 38)
  ..lineTo(-8, 38)
  ..quadraticBezierTo(-19, 38, -19, 27)
  ..lineTo(-17, -26)
  ..quadraticBezierTo(-15, -38, 0, -38)
  ..close();

/// Boxy, with square corners.
final Path _muscleBody = Path()
  ..addRRect(RRect.fromRectAndRadius(
      const Rect.fromLTWH(-19, -38, 38, 76), const Radius.circular(6)));

/// A low wedge, pointed at the front.
final Path _hyperBody = Path()
  ..moveTo(0, -39)
  ..quadraticBezierTo(11, -38, 15, -26)
  ..lineTo(19, 18)
  ..quadraticBezierTo(19, 37, 10, 38)
  ..lineTo(-10, 38)
  ..quadraticBezierTo(-19, 37, -19, 18)
  ..lineTo(-15, -26)
  ..quadraticBezierTo(-11, -38, 0, -39)
  ..close();

/// A monster truck's body, up between its big wheels.
final Path _monsterBody = Path()
  ..addRRect(RRect.fromRectAndRadius(
      const Rect.fromLTWH(-16, -37, 32, 72), const Radius.circular(8)));

/// A car's windscreen and rear window, for cars with a cabin [top] to
/// [bottom] and roof [half] wide either side.
Path _carGlass(double top, double bottom, double half) => Path()
  ..addPolygon([
    Offset(-half - 2, top - 10),
    Offset(half + 2, top - 10),
    Offset(half, top),
    Offset(-half, top),
  ], true)
  ..addPolygon([
    Offset(-half, bottom),
    Offset(half, bottom),
    Offset(half + 1.5, bottom + 7),
    Offset(-half - 1.5, bottom + 7),
  ], true);

final Path _sedanGlass = _carGlass(-7, 13, 12);
final Path _sportsGlass = _carGlass(-3, 11, 11);
final Path _muscleGlass = _carGlass(-5, 14, 13);
final Path _monsterGlass = _carGlass(-12, 4, 11);

const Rect _glowArea = Rect.fromLTWH(-34, -34, 68, 68);

/// Where a galaxy skin's stars are, across and down the ride (0 to 1).
const List<Offset> _stars = [
  Offset(0.2, 0.12),
  Offset(0.7, 0.2),
  Offset(0.45, 0.35),
  Offset(0.85, 0.5),
  Offset(0.15, 0.55),
  Offset(0.6, 0.68),
  Offset(0.3, 0.82),
  Offset(0.78, 0.9),
];

/// A camo skin's blobs: where they are and how big, across and down the
/// ride (0 to 1), and which of the skin's colours.
const List<(double, double, double, double, int)> _camoBlobs = [
  (0.2, 0.1, 0.55, 0.14, 0),
  (0.8, 0.22, 0.5, 0.12, 1),
  (0.35, 0.36, 0.6, 0.1, 2),
  (0.85, 0.5, 0.45, 0.15, 0),
  (0.2, 0.62, 0.55, 0.12, 1),
  (0.7, 0.75, 0.6, 0.12, 2),
  (0.25, 0.9, 0.5, 0.12, 0),
];

final Paint _shadow = Paint()..color = const Color(0x55000000);
final Paint _tyre = Paint()..color = const Color(0xFF1A1A1A);
final Paint _tread = Paint()..color = const Color(0xFF3A3A3A);
final Paint _glass = Paint()..color = const Color(0xFF1E2A38);
final Paint _roof = Paint()..color = const Color(0x26FFFFFF);
final Paint _headlight = Paint()..color = const Color(0xFFFFF9C4);
final Paint _tailLight = Paint()..color = const Color(0xFFFF1744);
final Paint _handlebar = Paint()
  ..color = const Color(0xFF424242)
  ..strokeWidth = 3.5
  ..strokeCap = StrokeCap.round;
final Paint _visor = Paint()
  ..color = const Color(0xFF212121)
  ..style = PaintingStyle.stroke
  ..strokeWidth = 3.5;
final Paint _chrome = Paint()
  ..color = const Color(0xFFCFD8DC)
  ..strokeWidth = 2.5
  ..strokeCap = StrokeCap.round;

/// The colours a ride is drawn in, with its skin on.
class _Look {
  _Look(Ride ride, this.skin)
      : color = skin?.base ?? ride.color,
        light = ride.accent;

  final Color color;
  final Color light;
  final Skin? skin;

  Color get dark => Color.lerp(color, Colors.black, 0.45)!;
  bool get patterned => skin != null && skin!.pattern != SkinPattern.none;
}

/// Draws a top-down bike or car facing up, centred on the origin, wearing
/// [skin]. Shared by the game, the garage and the app icon.
void paintRide(Canvas canvas, Ride ride,
    {Skin? skin, bool boosting = false, double time = 0}) {
  if (ride.glow) _paintGlow(canvas, ride, time);

  canvas.drawRRect(
    RRect.fromRectAndRadius(
      Rect.fromCenter(
        center: const Offset(5, 7),
        width: ride.isCar ? ride.width : ride.width - 4,
        height: ride.height,
      ),
      Radius.circular(ride.isCar ? 10 : 13),
    ),
    _shadow,
  );

  if (boosting) _paintBoost(canvas, ride, time);

  final look = _Look(ride, skin);
  switch (ride.body) {
    case RideBody.street:
      _street(canvas, look);
    case RideBody.dirt:
      _dirt(canvas, look);
    case RideBody.chopper:
      _chopper(canvas, look);
    case RideBody.superbike:
      _superbike(canvas, look);
    case RideBody.sedan:
      _sedan(canvas, look);
    case RideBody.sports:
      _sports(canvas, look);
    case RideBody.muscle:
      _muscle(canvas, look);
    case RideBody.hyper:
      _hyper(canvas, look);
    case RideBody.monster:
      _monster(canvas, look);
  }
}

/// A soft oval halo. A gradient gives the same look as a blur filter at a
/// fraction of the cost, which matters as it is drawn every frame.
void _paintGlow(Canvas canvas, Ride ride, double time) {
  final pulse = 0.55 + 0.25 * sin(time * 5);
  canvas.save();
  canvas.scale(ride.width / 30, ride.height / 62 * 78 / 46);
  canvas.drawCircle(
    Offset.zero,
    34,
    Paint()
      ..shader = RadialGradient(
        colors: [
          ride.accent.withValues(alpha: pulse),
          ride.accent.withValues(alpha: pulse * 0.5),
          ride.accent.withValues(alpha: 0),
        ],
        stops: const [0.35, 0.65, 1],
      ).createShader(_glowArea),
  );
  canvas.restore();
}

/// Nitro flames out of the back: one exhaust on a bike, two on a car.
void _paintBoost(Canvas canvas, Ride ride, double time) {
  final len = 26 + sin(time * 60) * 6;
  final y = ride.height / 2 - 5;
  final outer = Paint()..color = const Color(0xFFFF6D00);
  final inner = Paint()..color = const Color(0xFFFFF176);
  for (final x in ride.isCar ? const [-9.0, 9.0] : const [0.0]) {
    canvas.drawPath(
      Path()
        ..moveTo(x - 6, y)
        ..quadraticBezierTo(x, y + len * 1.3, x + 6, y)
        ..close(),
      outer,
    );
    canvas.drawPath(
      Path()
        ..moveTo(x - 3, y)
        ..quadraticBezierTo(x, y + len * 0.8, x + 3, y)
        ..close(),
      inner,
    );
  }
}

/// Fills [body] in the ride's colour, then lays the skin's pattern over it.
void _paintBody(Canvas canvas, Path body, _Look look) {
  canvas.drawPath(body, Paint()..color = look.color);
  final skin = look.skin;
  if (skin == null || skin.pattern == SkinPattern.none) return;
  canvas.save();
  canvas.clipPath(body);
  _paintPattern(canvas, body.getBounds(), skin);
  canvas.restore();
}

void _paintPattern(Canvas canvas, Rect r, Skin skin) {
  Paint fill(int i) => Paint()..color = skin.colors[i % skin.colors.length];
  Offset at(double x, double y) =>
      Offset(r.left + r.width * x, r.top + r.height * y);

  switch (skin.pattern) {
    case SkinPattern.none:
      return;
    case SkinPattern.stripes:
      for (final x in const [0.39, 0.61]) {
        canvas.drawRect(
          Rect.fromCenter(
              center: at(x, 0.5), width: r.width * 0.13, height: r.height),
          fill(0),
        );
      }
    case SkinPattern.flames:
      canvas.drawPath(_flamePath(r, 1), fill(0));
      canvas.drawPath(_flamePath(r, 0.6), fill(1));
    case SkinPattern.lightning:
      final bolt = Path()
        ..addPolygon([
          at(0.62, 0),
          at(0.34, 0.48),
          at(0.52, 0.48),
          at(0.38, 1),
          at(0.7, 0.4),
          at(0.5, 0.4),
          at(0.74, 0),
        ], true);
      // A shadow under the bolt keeps it clear on a light ride.
      canvas.drawPath(bolt.shift(const Offset(1.2, 1.2)), fill(1));
      canvas.drawPath(bolt, fill(0));
    case SkinPattern.tiger:
      // Claw marks reaching in from both sides, the right ones in between
      // the left ones.
      final paint = fill(0);
      for (var y = 0.06; y < 0.95; y += 0.15) {
        for (final (side, tip, dy) in const [
          (0.0, 0.38, 0.0),
          (1.0, 0.62, 0.075),
        ]) {
          canvas.drawPath(
            Path()
              ..addPolygon([
                at(side, y + dy - 0.03),
                at(tip, y + dy + 0.025),
                at(side, y + dy + 0.05),
              ], true),
            paint,
          );
        }
      }
    case SkinPattern.camo:
      for (final (x, y, w, h, color) in _camoBlobs) {
        canvas.drawOval(
          Rect.fromCenter(
              center: at(x, y), width: r.width * w, height: r.height * h),
          fill(color),
        );
      }
    case SkinPattern.checker:
      final size = r.width / 4;
      final paint = fill(0);
      final rows = (r.height / size).ceil();
      for (var row = 0; row < rows; row++) {
        for (var col = row % 2; col < 4; col += 2) {
          canvas.drawRect(
            Rect.fromLTWH(
                r.left + col * size, r.top + row * size, size, size),
            paint,
          );
        }
      }
    case SkinPattern.galaxy:
      canvas.drawOval(
        Rect.fromCenter(
            center: at(0.3, 0.3),
            width: r.width * 0.9,
            height: r.height * 0.3),
        fill(0),
      );
      canvas.drawOval(
        Rect.fromCenter(
            center: at(0.75, 0.7),
            width: r.width * 0.8,
            height: r.height * 0.28),
        fill(1),
      );
      final star = fill(2);
      for (final s in _stars) {
        canvas.drawCircle(at(s.dx, s.dy), 0.4 + r.width * 0.035, star);
      }
    case SkinPattern.chrome:
      // Bands of light and shade across it, like a polished mirror.
      for (final (y, color) in const [(0.15, 0), (0.45, 1), (0.7, 0)]) {
        canvas.drawPath(
          Path()
            ..addPolygon([
              at(0, y),
              at(1, y - 0.12),
              at(1, y - 0.04),
              at(0, y + 0.08),
            ], true),
          fill(color),
        );
      }
  }
}

/// Flames across the front of [r], licking back over it. [reach] scales
/// how far back they go.
Path _flamePath(Rect r, double reach) {
  const tongues = [0.32, 0.5, 0.64, 0.5, 0.32];
  final w = r.width / tongues.length;
  final valley = r.top + r.height * 0.14 * reach;
  final path = Path()
    ..moveTo(r.left, r.top)
    ..lineTo(r.left, valley);
  for (var i = 0; i < tongues.length; i++) {
    final x = r.left + w * i;
    final tip = r.top + r.height * tongues[i] * reach;
    final mid = (valley + tip) / 2;
    path
      ..quadraticBezierTo(x + w * 0.2, mid, x + w * 0.5, tip)
      ..quadraticBezierTo(x + w * 0.8, mid, x + w, valley);
  }
  return path
    ..lineTo(r.right, r.top)
    ..close();
}

// ---------------------------------------------------------------- bikes

void _street(Canvas canvas, _Look look) {
  _paintTyre(canvas, const Offset(0, 20), 11, 22);
  _paintTyre(canvas, const Offset(0, -22), 10, 20);
  _paintBody(canvas, _streetBody, look);
  if (!look.patterned) {
    canvas.drawRRect(
      RRect.fromRectAndRadius(
        Rect.fromCenter(center: const Offset(0, -10), width: 4, height: 30),
        const Radius.circular(2),
      ),
      Paint()..color = look.light,
    );
  }
  canvas.drawPath(_windshield, Paint()..color = const Color(0xCC90CAF9));
  canvas.drawLine(const Offset(-15, -13), const Offset(15, -13), _handlebar);
  _paintRider(canvas, look);
  _paintTailLight(canvas, 24);
}

void _dirt(Canvas canvas, _Look look) {
  _paintKnobbly(canvas, const Offset(0, 21), 10, 22);
  _paintKnobbly(canvas, const Offset(0, -23), 9, 20);
  _paintBody(canvas, _dirtBody, look);
  // The number plate on the front.
  canvas.drawOval(
    Rect.fromCenter(center: const Offset(0, -24), width: 13, height: 7),
    Paint()..color = look.light,
  );
  canvas.drawLine(const Offset(-18, -14), const Offset(18, -14), _handlebar);
  _paintRider(canvas, look, barY: -14, reach: 17);
  _paintTailLight(canvas, 23);
}

void _chopper(Canvas canvas, _Look look) {
  _paintTyre(canvas, const Offset(0, 21), 13, 20);
  _paintTyre(canvas, const Offset(0, -26), 7, 18, radius: 3.5);
  // Long front forks out to the wheel, and the exhaust pipes.
  canvas.drawLine(const Offset(-4, -14), const Offset(-3, -27), _chrome);
  canvas.drawLine(const Offset(4, -14), const Offset(3, -27), _chrome);
  canvas.drawLine(const Offset(-8, 2), const Offset(-10, 28), _chrome);
  canvas.drawLine(const Offset(8, 2), const Offset(10, 28), _chrome);
  _paintBody(canvas, _chopperBody, look);
  // A tall, wide handlebar.
  canvas.drawLine(const Offset(-14, -17), const Offset(14, -17), _handlebar);
  canvas.drawLine(const Offset(-14, -17), const Offset(-18, -22), _handlebar);
  canvas.drawLine(const Offset(14, -17), const Offset(18, -22), _handlebar);
  // Leaning back in the seat.
  _paintRider(canvas, look, seat: 8, barY: -19, reach: 17);
  _paintTailLight(canvas, 27);
}

void _superbike(Canvas canvas, _Look look) {
  _paintTyre(canvas, const Offset(0, 21), 13, 22);
  _paintTyre(canvas, const Offset(0, -22), 10, 20);
  _paintBody(canvas, _superbikeBody, look);
  // Air intakes either side of the nose.
  final intake = Paint()..color = look.light;
  canvas.drawPath(
      Path()
        ..addPolygon(
            const [Offset(-9, -14), Offset(-6, -21), Offset(-5, -12)], true),
      intake);
  canvas.drawPath(
      Path()
        ..addPolygon(
            const [Offset(9, -14), Offset(6, -21), Offset(5, -12)], true),
      intake);
  canvas.drawPath(_superbikeScreen, Paint()..color = const Color(0xCC4FC3F7));
  canvas.drawLine(const Offset(-12, -13), const Offset(12, -13), _handlebar);
  // Tucked in low behind the screen.
  _paintRider(canvas, look, seat: 2, barY: -13, reach: 12);
  for (final x in const [-3.5, 3.5]) {
    canvas.drawRRect(
      RRect.fromRectAndRadius(
        Rect.fromCenter(center: Offset(x, 24), width: 5, height: 2.5),
        const Radius.circular(1.2),
      ),
      _tailLight,
    );
  }
}

void _paintTyre(Canvas canvas, Offset center, double width, double height,
    {double radius = 5}) {
  canvas.drawRRect(
    RRect.fromRectAndRadius(
      Rect.fromCenter(center: center, width: width, height: height),
      Radius.circular(radius),
    ),
    _tyre,
  );
}

/// An off-road tyre, with knobs sticking out of its sides.
void _paintKnobbly(Canvas canvas, Offset c, double width, double height) {
  _paintTyre(canvas, c, width, height);
  for (var y = c.dy - height / 2 + 3; y < c.dy + height / 2 - 2; y += 4) {
    canvas.drawRect(Rect.fromLTWH(c.dx - width / 2 - 1.5, y, 1.5, 2), _tyre);
    canvas.drawRect(Rect.fromLTWH(c.dx + width / 2, y, 1.5, 2), _tyre);
  }
}

/// The rider: arms out to the handlebar at [barY], body and helmet. [seat]
/// sits them further back.
void _paintRider(Canvas canvas, _Look look,
    {double seat = 0, double barY = -12, double reach = 14}) {
  final dark = look.dark;
  final arm = Paint()
    ..color = dark
    ..strokeWidth = 5
    ..strokeCap = StrokeCap.round;
  canvas.drawLine(Offset(-8, seat), Offset(-reach, barY), arm);
  canvas.drawLine(Offset(8, seat), Offset(reach, barY), arm);
  canvas.drawOval(
    Rect.fromCenter(center: Offset(0, seat + 4), width: 20, height: 24),
    Paint()..color = dark,
  );
  final helmet = Offset(0, seat - 3);
  canvas.drawCircle(helmet, 8.5, Paint()..color = look.light);
  canvas.drawArc(Rect.fromCircle(center: helmet, radius: 8.5), pi * 1.2,
      pi * 0.6, false, _visor);
}

void _paintTailLight(Canvas canvas, double y) {
  canvas.drawRRect(
    RRect.fromRectAndRadius(
      Rect.fromCenter(center: Offset(0, y), width: 8, height: 3),
      const Radius.circular(1.5),
    ),
    _tailLight,
  );
}

// ----------------------------------------------------------------- cars

void _sedan(Canvas canvas, _Look look) {
  _paintWheels(canvas);
  _paintBody(canvas, _sedanBody, look);
  if (!look.patterned) {
    // Trim along both sides.
    final trim = Paint()..color = look.light;
    canvas.drawRect(const Rect.fromLTRB(-17, -26, -15.5, 28), trim);
    canvas.drawRect(const Rect.fromLTRB(15.5, -26, 17, 28), trim);
  }
  _paintMirrors(canvas, look, -12);
  canvas.drawPath(_sedanGlass, _glass);
  canvas.drawRect(const Rect.fromLTRB(-12, -7, 12, 13), _roof);
  _paintCarLights(canvas, spread: 12);
}

void _sports(Canvas canvas, _Look look) {
  _paintWheels(canvas, front: -23, rear: 25, rearWidth: 7);
  _paintBody(canvas, _sportsBody, look);
  if (!look.patterned) {
    // A pair of thin stripes up the bonnet.
    final stripe = Paint()..color = look.light;
    canvas.drawRect(const Rect.fromLTRB(-4, -37, -2, -13), stripe);
    canvas.drawRect(const Rect.fromLTRB(2, -37, 4, -13), stripe);
  }
  _paintMirrors(canvas, look, -8);
  canvas.drawPath(_sportsGlass, _glass);
  canvas.drawRect(const Rect.fromLTRB(-11, -3, 11, 11), _roof);
  // The rear wing.
  canvas.drawRRect(
    RRect.fromRectAndRadius(
        const Rect.fromLTRB(-20, 27, 20, 32), const Radius.circular(2)),
    Paint()..color = look.dark,
  );
  _paintCarLights(canvas, front: -36, spread: 10);
}

void _muscle(Canvas canvas, _Look look) {
  _paintWheels(canvas, rearWidth: 8);
  _paintBody(canvas, _muscleBody, look);
  if (!look.patterned) {
    // Racing stripes, nose to tail.
    final stripe = Paint()..color = look.light;
    canvas.drawRect(const Rect.fromLTRB(-8, -38, -3, 38), stripe);
    canvas.drawRect(const Rect.fromLTRB(3, -38, 8, 38), stripe);
  }
  // A bulging scoop on the bonnet.
  canvas.drawRRect(
    RRect.fromRectAndRadius(
        const Rect.fromLTRB(-6, -31, 6, -17), const Radius.circular(2)),
    Paint()..color = look.dark,
  );
  canvas.drawRect(const Rect.fromLTRB(-4, -31, 4, -28), _tyre);
  _paintMirrors(canvas, look, -10);
  canvas.drawPath(_muscleGlass, _glass);
  canvas.drawRect(const Rect.fromLTRB(-13, -5, 13, 14), _roof);
  for (final x in const [-7.0, 7.0]) {
    canvas.drawCircle(Offset(x, 38.5), 2, _chrome);
  }
  _paintCarLights(canvas, spread: 12);
}

void _hyper(Canvas canvas, _Look look) {
  _paintWheels(canvas, front: -22, rear: 24, rearWidth: 8);
  _paintBody(canvas, _hyperBody, look);
  // Air intakes along the sides.
  final dark = Paint()..color = look.dark;
  for (final s in const [-1.0, 1.0]) {
    canvas.drawPath(
      Path()
        ..addPolygon([
          Offset(s * 17, 2),
          Offset(s * 11, 6),
          Offset(s * 11, 16),
          Offset(s * 18, 14),
        ], true),
      dark,
    );
  }
  // A bubble canopy, with a glint on it.
  canvas.drawOval(
      Rect.fromCenter(center: const Offset(0, -4), width: 20, height: 34),
      _glass);
  canvas.drawOval(
    Rect.fromCenter(center: const Offset(-4, -11), width: 4, height: 11),
    Paint()..color = const Color(0x55FFFFFF),
  );
  canvas.drawRRect(
    RRect.fromRectAndRadius(
        const Rect.fromLTRB(-2.5, 15, 2.5, 29), const Radius.circular(1.5)),
    Paint()..color = look.light,
  );
  canvas.drawRRect(
    RRect.fromRectAndRadius(
        const Rect.fromLTRB(-21, 30, 21, 35), const Radius.circular(2)),
    dark,
  );
  _paintCarLights(canvas, front: -33, spread: 8);
}

void _monster(Canvas canvas, _Look look) {
  // Huge treaded wheels, standing well out of the sides.
  for (final x in const [-19.0, 19.0]) {
    for (final y in const [-24.0, 25.0]) {
      _paintTyre(canvas, Offset(x, y), 13, 26, radius: 4);
      for (var t = y - 10; t <= y + 10; t += 5) {
        canvas.drawRect(
            Rect.fromCenter(center: Offset(x, t), width: 13, height: 1.8),
            _tread);
      }
    }
  }
  _paintBody(canvas, _monsterBody, look);
  canvas.drawRRect(
    RRect.fromRectAndRadius(
        const Rect.fromLTRB(-15, -39, 15, -35), const Radius.circular(2)),
    Paint()..color = const Color(0xFF424242),
  );
  canvas.drawPath(_monsterGlass, _glass);
  canvas.drawRect(const Rect.fromLTRB(-11, -12, 11, 4), _roof);
  // A row of lamps along the roof.
  for (final x in const [-7.5, -2.5, 2.5, 7.5]) {
    canvas.drawCircle(Offset(x, -9), 1.8, _headlight);
  }
  // The truck bed behind the cab, with two exhaust stacks in it.
  canvas.drawRRect(
    RRect.fromRectAndRadius(
        const Rect.fromLTRB(-12, 14, 12, 30), const Radius.circular(3)),
    Paint()..color = look.dark,
  );
  for (final x in const [-7.0, 7.0]) {
    canvas.drawCircle(Offset(x, 18), 2.6, _chrome);
    canvas.drawCircle(Offset(x, 18), 1.2, _tyre);
  }
  _paintCarLights(canvas, front: -37, rear: 35, spread: 10);
}

/// A car's wheels, showing at its four corners.
void _paintWheels(Canvas canvas,
    {double front = -24, double rear = 24, double rearWidth = 6}) {
  for (final s in const [-1.0, 1.0]) {
    _paintTyre(canvas, Offset(s * 18.5, front), 6, 14, radius: 2.5);
    _paintTyre(canvas, Offset(s * (18.5 + (rearWidth - 6) / 2), rear),
        rearWidth, 14,
        radius: 2.5);
  }
}

void _paintMirrors(Canvas canvas, _Look look, double y) {
  final paint = Paint()..color = look.dark;
  for (final x in const [-23.0, 18.0]) {
    canvas.drawRRect(
      RRect.fromRectAndRadius(
          Rect.fromLTWH(x, y, 5, 6), const Radius.circular(2)),
      paint,
    );
  }
}

/// Headlights at the [front], tail lights at the [rear], [spread] out from
/// the middle.
void _paintCarLights(Canvas canvas,
    {double front = -38, double rear = 38, double spread = 11}) {
  for (final s in const [-1.0, 1.0]) {
    canvas.drawRRect(
      RRect.fromRectAndRadius(
        Rect.fromCenter(
            center: Offset(s * spread, front + 3), width: 8, height: 4),
        const Radius.circular(2),
      ),
      _headlight,
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(
        Rect.fromCenter(
            center: Offset(s * spread, rear - 2.5), width: 8, height: 3.5),
        const Radius.circular(1.5),
      ),
      _tailLight,
    );
  }
}
