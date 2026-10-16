import 'dart:math';

import 'package:flutter/material.dart';

/// The cartoon portraits a player can pick for their profile.
enum Avatar {
  kid('Kid'),
  boy('Boy'),
  girl('Girl'),
  man('Man'),
  woman('Woman'),
  oldMan('Old man'),
  oldWoman('Old woman');

  const Avatar(this.label);

  final String label;

  /// The avatar saved as [id], or the kid until one has been picked.
  static Avatar of(String? id) =>
      values.firstWhere((a) => a.name == id, orElse: () => kid);
}

/// [avatar] as a round cartoon portrait, [size] across.
class AvatarImage extends StatelessWidget {
  const AvatarImage({super.key, required this.avatar, this.size = 48});

  final Avatar avatar;
  final double size;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: avatar.label,
      image: true,
      child: SizedBox.square(
        dimension: size,
        child: CustomPaint(painter: _AvatarPainter(avatar)),
      ),
    );
  }
}

/// The colours of one avatar.
typedef _Look = ({Color background, Color skin, Color hair, Color shirt});

const Map<Avatar, _Look> _looks = {
  Avatar.kid: (
    background: Color(0xFF4FC3F7),
    skin: Color(0xFFFFD3B0),
    hair: Color(0xFF8D5524),
    shirt: Color(0xFFFFEB3B),
  ),
  Avatar.boy: (
    background: Color(0xFFFFB74D),
    skin: Color(0xFFE0A877),
    hair: Color(0xFF2B1B12),
    shirt: Color(0xFF1E88E5),
  ),
  Avatar.girl: (
    background: Color(0xFFBA68C8),
    skin: Color(0xFFC68A5E),
    hair: Color(0xFF3B2314),
    shirt: Color(0xFFF06292),
  ),
  Avatar.man: (
    background: Color(0xFF4DB6AC),
    skin: Color(0xFFD49A6A),
    hair: Color(0xFF3E2516),
    shirt: Color(0xFF283593),
  ),
  Avatar.woman: (
    background: Color(0xFFFFD54F),
    skin: Color(0xFFF2C29B),
    hair: Color(0xFF6D3A1F),
    shirt: Color(0xFFE53935),
  ),
  Avatar.oldMan: (
    background: Color(0xFF81C784),
    skin: Color(0xFFE8B98F),
    hair: Color(0xFFE0E0E0),
    shirt: Color(0xFF795548),
  ),
  Avatar.oldWoman: (
    background: Color(0xFFF48FB1),
    skin: Color(0xFFB97A4E),
    hair: Color(0xFFEEEEEE),
    shirt: Color(0xFF7E57C2),
  ),
};

/// Draws an avatar on a 100 × 100 grid, scaled to the size it is given.
class _AvatarPainter extends CustomPainter {
  _AvatarPainter(this.avatar);

  final Avatar avatar;

  static const _ink = Color(0xFF3E2723);
  static const _blush = Color(0x55FF5A7A);

  _Look get _look => _looks[avatar]!;
  bool get _young =>
      avatar == Avatar.kid || avatar == Avatar.boy || avatar == Avatar.girl;
  bool get _old => avatar == Avatar.oldMan || avatar == Avatar.oldWoman;

  /// The middle of the head. The young ones have bigger heads.
  Offset get _head => switch (avatar) {
        Avatar.kid => const Offset(50, 47),
        Avatar.boy || Avatar.girl => const Offset(50, 46),
        _ => const Offset(50, 44),
      };

  /// Half the head's width.
  double get _r => switch (avatar) {
        Avatar.kid => 22,
        Avatar.boy || Avatar.girl => 20.5,
        _ => 19.5,
      };

  Color get _shade => Color.lerp(_look.skin, const Color(0xFF3E2723), 0.2)!;

  static Paint _fill(Color color) => Paint()..color = color;

  static Paint _line(Color color, double width) => Paint()
    ..color = color
    ..style = PaintingStyle.stroke
    ..strokeWidth = width
    ..strokeCap = StrokeCap.round
    ..strokeJoin = StrokeJoin.round;

  @override
  void paint(Canvas canvas, Size size) {
    final look = _look;
    final c = _head;
    final r = _r;
    canvas.save();
    canvas.scale(size.width / 100, size.height / 100);
    canvas.clipPath(Path()..addOval(const Rect.fromLTWH(0, 0, 100, 100)));
    canvas.drawRect(
        const Rect.fromLTWH(0, 0, 100, 100), _fill(look.background));
    canvas.drawCircle(
        const Offset(32, 26), 46, _fill(const Color(0x1FFFFFFF)));

    _backHair(canvas, c, r);

    // Neck and shoulders.
    final shoulders = _young
        ? const Rect.fromLTRB(24, 76, 76, 124)
        : const Rect.fromLTRB(16, 74, 84, 124);
    canvas.drawRect(
        Rect.fromLTRB(c.dx - 6, c.dy + r - 6, c.dx + 6, shoulders.top + 6),
        _fill(_shade));
    canvas.drawRRect(
        RRect.fromRectAndRadius(shoulders, const Radius.circular(24)),
        _fill(look.shirt));
    canvas.drawOval(
        Rect.fromCenter(
            center: Offset(c.dx, shoulders.top + 1), width: 13, height: 9),
        _fill(_shade));
    _clothes(canvas, shoulders);

    // Ears, then the face over their inner half.
    for (final side in const [-1.0, 1.0]) {
      canvas.drawCircle(c + Offset(side * r, 3), 4.6, _fill(look.skin));
      canvas.drawCircle(c + Offset(side * r, 3), 2.2, _fill(_shade));
    }
    canvas.drawOval(
        Rect.fromCenter(center: c, width: r * 2, height: r * 2.1),
        _fill(look.skin));

    _face(canvas, c, r);
    _frontHair(canvas, c, r);
    _extras(canvas, c, r);
    canvas.restore();
  }

  /// Hair that falls behind the head: pigtails, long hair or a bun.
  void _backHair(Canvas canvas, Offset c, double r) {
    final hair = _fill(_look.hair);
    switch (avatar) {
      case Avatar.girl:
        for (final side in const [-1.0, 1.0]) {
          canvas.drawCircle(c + Offset(side * (r + 5), -r * 0.2), 7.5, hair);
        }
      case Avatar.woman:
        canvas.drawRRect(
          RRect.fromLTRBAndCorners(
            c.dx - r - 5,
            c.dy - r - 2,
            c.dx + r + 5,
            c.dy + r + 16,
            topLeft: Radius.circular(r + 5),
            topRight: Radius.circular(r + 5),
            bottomLeft: const Radius.circular(6),
            bottomRight: const Radius.circular(6),
          ),
          hair,
        );
      case Avatar.oldWoman:
        canvas.drawCircle(c + Offset(0, -r - 3), 8.5, hair);
        canvas.drawCircle(c + Offset(-2.5, -r - 5.5), 2.5,
            _fill(const Color(0x66FFFFFF)));
      default:
    }
  }

  void _clothes(Canvas canvas, Rect s) {
    final top = s.top;
    switch (avatar) {
      case Avatar.kid:
        canvas.drawRect(Rect.fromLTWH(s.left, top + 12, s.width, 5),
            _fill(const Color(0x99FFFFFF)));
      case Avatar.man:
        // A white shirt collar.
        final collar = _fill(Colors.white);
        for (final side in const [-1.0, 1.0]) {
          canvas.drawPath(
            Path()
              ..moveTo(50 + side * 8, top - 0.5)
              ..lineTo(50 + side * 0.5, top + 7)
              ..lineTo(50 + side * 5, top + 10)
              ..close(),
            collar,
          );
        }
      case Avatar.woman:
        canvas.drawPath(
          Path()
            ..moveTo(44, top)
            ..lineTo(56, top)
            ..lineTo(50, top + 10)
            ..close(),
          _fill(_shade),
        );
      case Avatar.oldMan:
        // A cardigan, buttoned down the middle.
        canvas.drawLine(Offset(50, top + 4), Offset(50, s.bottom),
            _line(Color.lerp(_look.shirt, Colors.black, 0.35)!, 1.6));
        for (final y in [top + 11, top + 19]) {
          canvas.drawCircle(Offset(53.5, y), 1.6, _fill(const Color(0xFFFFE0B2)));
        }
      case Avatar.oldWoman:
        // A string of pearls.
        for (var i = -3; i <= 3; i++) {
          canvas.drawCircle(Offset(50 + i * 3.6, top + 5 + i * i * 0.45), 1.8,
              _fill(Colors.white));
        }
      default:
    }
  }

  void _face(Canvas canvas, Offset c, double r) {
    final look = _look;
    final eyeY = c.dy + 1;
    final eyeDx = r * 0.38;
    final eyeW = _young ? 5.2 : 4.4;
    final eyeH = _young ? 6.4 : 5.0;
    final brow = _old
        ? _line(look.hair, 2.8)
        : _line(Color.lerp(look.hair, Colors.black, 0.2)!,
            avatar == Avatar.man ? 2.2 : 1.7);
    for (final side in const [-1.0, 1.0]) {
      final e = Offset(c.dx + side * eyeDx, eyeY);
      canvas.drawOval(
          Rect.fromCenter(center: e, width: eyeW, height: eyeH), _fill(_ink));
      canvas.drawCircle(e + const Offset(0.9, -1.2), _young ? 1.3 : 1.0,
          _fill(Colors.white));
      canvas.drawPath(
        Path()
          ..moveTo(e.dx - 3.6, eyeY - 6)
          ..quadraticBezierTo(e.dx, eyeY - 8.4, e.dx + 3.6, eyeY - 6),
        brow,
      );
    }

    if (_young || avatar == Avatar.woman || avatar == Avatar.oldWoman) {
      for (final side in const [-1.0, 1.0]) {
        canvas.drawCircle(
            Offset(c.dx + side * r * 0.6, c.dy + 8), 3.6, _fill(_blush));
      }
    }

    canvas.drawOval(
        Rect.fromCenter(center: c + const Offset(0, 6.5), width: 3.6, height: 2.6),
        _fill(_shade));

    final mouthY = c.dy + (avatar == Avatar.oldMan ? 13.5 : 11);
    switch (avatar) {
      case Avatar.kid || Avatar.girl:
        // A big open smile.
        canvas.drawPath(
          Path()
            ..moveTo(c.dx - 5.5, mouthY)
            ..quadraticBezierTo(c.dx, mouthY + 15, c.dx + 5.5, mouthY)
            ..close(),
          _fill(const Color(0xFF8E2A2A)),
        );
      case Avatar.woman:
        canvas.drawPath(
          Path()
            ..moveTo(c.dx - 5.5, mouthY + 0.5)
            ..quadraticBezierTo(c.dx, mouthY + 5.5, c.dx + 5.5, mouthY + 0.5),
          _line(const Color(0xFFD32F2F), 2.8),
        );
      default:
        canvas.drawPath(
          Path()
            ..moveTo(c.dx - 5.5, mouthY + 0.5)
            ..quadraticBezierTo(c.dx, mouthY + 5, c.dx + 5.5, mouthY + 0.5),
          _line(_ink, 2),
        );
    }
  }

  /// A cap of hair over the top of the head, from temple to temple, with
  /// [fringe] drawn back from the right temple to the left one.
  Path _cap(Offset c, double r, void Function(Path path) fringe,
      {double temples = -1}) {
    final path = Path()
      ..moveTo(c.dx - r - 1.2, c.dy + temples)
      ..arcToPoint(Offset(c.dx + r + 1.2, c.dy + temples),
          radius: Radius.elliptical(r + 1.2, r * 1.2 + temples.abs()));
    fringe(path);
    return path..close();
  }

  void _frontHair(Canvas canvas, Offset c, double r) {
    final hair = _fill(_look.hair);
    switch (avatar) {
      case Avatar.kid:
        canvas.drawPath(
          _cap(c, r, (p) {
            p
              ..quadraticBezierTo(
                  c.dx + r * 0.6, c.dy - r * 0.55, c.dx, c.dy - r * 0.5)
              ..quadraticBezierTo(
                  c.dx - r * 0.6, c.dy - r * 0.55, c.dx - r - 1.2, c.dy - 1);
          }),
          hair,
        );
        // A curl on top.
        canvas.drawPath(
          Path()
            ..moveTo(c.dx - 1, c.dy - r * 1.1)
            ..cubicTo(c.dx - 7, c.dy - r - 10, c.dx + 7, c.dy - r - 11,
                c.dx + 2.5, c.dy - r - 4),
          _line(_look.hair, 3),
        );
      case Avatar.boy:
        canvas.drawPath(
          _cap(c, r, (p) {
            for (final (x, y) in const [
              (0.78, -0.42),
              (0.48, -0.72),
              (0.22, -0.48),
              (-0.08, -0.76),
              (-0.36, -0.5),
              (-0.64, -0.72),
              (-0.86, -0.38),
            ]) {
              p.lineTo(c.dx + r * x, c.dy + r * y);
            }
            p.lineTo(c.dx - r - 1.2, c.dy - 1);
          }),
          hair,
        );
        // Spikes standing up all over the top, drawn on their own so they
        // can't cut holes in the cap.
        final spikes = Path();
        for (final a in const [-2.55, -2.05, -1.57, -1.09, -0.6]) {
          final dir = Offset(cos(a), sin(a) * 1.15);
          final across = Offset(-sin(a), cos(a)) * 4.6;
          final base = c + dir * (r - 1);
          final tip = c + dir * (r + 7);
          spikes
            ..moveTo(base.dx + across.dx, base.dy + across.dy)
            ..lineTo(tip.dx, tip.dy)
            ..lineTo(base.dx - across.dx, base.dy - across.dy)
            ..close();
        }
        canvas.drawPath(spikes, hair);
      case Avatar.girl:
        canvas.drawPath(
          _cap(c, r, (p) {
            p.quadraticBezierTo(
                c.dx, c.dy - r * 0.75, c.dx - r - 1.2, c.dy - 1);
          }),
          hair,
        );
      case Avatar.man:
        canvas.drawPath(
          _cap(c, r, (p) {
            p
              ..quadraticBezierTo(c.dx + r * 0.9, c.dy - r * 0.8,
                  c.dx + r * 0.35, c.dy - r * 0.74)
              ..quadraticBezierTo(
                  c.dx - r * 0.35, c.dy - r * 0.62, c.dx - r - 1.2, c.dy + 1);
          }),
          hair,
        );
        // The parting.
        canvas.drawLine(
            Offset(c.dx + r * 0.35, c.dy - r * 0.76),
            Offset(c.dx + r * 0.22, c.dy - r * 1.08),
            _line(Color.lerp(_look.hair, Colors.white, 0.25)!, 1.2));
      case Avatar.woman:
        canvas.drawPath(
          _cap(c, r, temples: 4, (p) {
            p
              ..quadraticBezierTo(c.dx + r * 0.3, c.dy - r * 0.75,
                  c.dx - r * 0.5, c.dy - r * 0.82)
              ..quadraticBezierTo(
                  c.dx - r * 0.85, c.dy - r * 0.5, c.dx - r - 1.2, c.dy + 4);
          }),
          hair,
        );
      case Avatar.oldWoman:
        canvas.drawPath(
          _cap(c, r, (p) {
            p
              ..quadraticBezierTo(
                  c.dx + r * 0.7, c.dy - r * 0.35, c.dx + r * 0.3, c.dy - r * 0.62)
              ..quadraticBezierTo(
                  c.dx, c.dy - r * 0.4, c.dx - r * 0.3, c.dy - r * 0.62)
              ..quadraticBezierTo(
                  c.dx - r * 0.7, c.dy - r * 0.35, c.dx - r - 1.2, c.dy - 1);
          }),
          hair,
        );
      case Avatar.oldMan:
        // Bald on top, with a shine, and grey hair over the ears.
        canvas.drawOval(
            Rect.fromCenter(
                center: c + Offset(-6, -r * 0.72), width: 9, height: 4.5),
            _fill(const Color(0x59FFFFFF)));
        for (final side in const [-1.0, 1.0]) {
          canvas.drawOval(
              Rect.fromCenter(
                  center: c + Offset(side * (r - 1.5), -3),
                  width: 8,
                  height: 13),
              hair);
        }
        final wrinkle = _line(_shade, 1.2);
        for (final dy in const [-9.0, -12.5]) {
          canvas.drawPath(
            Path()
              ..moveTo(c.dx - 6, c.dy + dy)
              ..quadraticBezierTo(c.dx, c.dy + dy - 1.6, c.dx + 6, c.dy + dy),
            wrinkle,
          );
        }
    }
  }

  /// Glasses, beards, moustaches, bows and earrings.
  void _extras(Canvas canvas, Offset c, double r) {
    final eyeY = c.dy + 1;
    final eyeDx = r * 0.38;
    switch (avatar) {
      case Avatar.girl:
        for (final side in const [-1.0, 1.0]) {
          final bow = c + Offset(side * (r + 1), -r * 0.5);
          canvas.drawCircle(bow, 3.4, _fill(const Color(0xFFFF4081)));
          canvas.drawCircle(bow, 1.3, _fill(const Color(0xFFFFCDD2)));
        }
      case Avatar.woman:
        for (final side in const [-1.0, 1.0]) {
          canvas.drawCircle(c + Offset(side * r, 9), 2.2,
              _fill(const Color(0xFFFFC107)));
        }
      case Avatar.man:
        // A short beard round the jaw, and a moustache.
        final beard = _fill(_look.hair);
        canvas.drawPath(
          Path()
            ..moveTo(c.dx - r + 0.3, c.dy + 2)
            ..quadraticBezierTo(
                c.dx - r + 1, c.dy + r * 1.18, c.dx, c.dy + r * 1.14)
            ..quadraticBezierTo(
                c.dx + r - 1, c.dy + r * 1.18, c.dx + r - 0.3, c.dy + 2)
            ..lineTo(c.dx + r - 4, c.dy + 3)
            ..quadraticBezierTo(c.dx + 9, c.dy + 15, c.dx, c.dy + 17.5)
            ..quadraticBezierTo(c.dx - 9, c.dy + 15, c.dx - r + 4, c.dy + 3)
            ..close(),
          beard,
        );
        for (final side in const [-1.0, 1.0]) {
          canvas.drawOval(
              Rect.fromCenter(
                  center: c + Offset(side * 3.4, 9.4), width: 7.5, height: 3),
              beard);
        }
      case Avatar.oldMan || Avatar.oldWoman:
        final frame = _line(
            avatar == Avatar.oldMan
                ? const Color(0xFF5D4037)
                : const Color(0xFFC2185B),
            1.6);
        for (final side in const [-1.0, 1.0]) {
          canvas.drawCircle(Offset(c.dx + side * eyeDx, eyeY), 5.6, frame);
        }
        canvas.drawLine(Offset(c.dx - eyeDx + 5.6, eyeY),
            Offset(c.dx + eyeDx - 5.6, eyeY), frame);
        if (avatar == Avatar.oldMan) {
          // A white moustache.
          for (final side in const [-1.0, 1.0]) {
            canvas.drawOval(
                Rect.fromCenter(
                    center: c + Offset(side * 4, 9.8), width: 9.5, height: 4.6),
                _fill(_look.hair));
          }
        }
      default:
    }
  }

  @override
  bool shouldRepaint(_AvatarPainter oldDelegate) =>
      oldDelegate.avatar != avatar;
}
