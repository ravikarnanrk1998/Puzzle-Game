import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../core/theme.dart';
import '../../data/teams.dart';
import '../../engine/career.dart';
import '../../models/player.dart';
import 'backgrounds.dart';

enum Buzz { light, medium, heavy, selection }

void haptic([Buzz type = Buzz.selection]) {
  if (!Career.instance.settings.haptics) return;
  switch (type) {
    case Buzz.light:
      HapticFeedback.lightImpact();
    case Buzz.medium:
      HapticFeedback.mediumImpact();
    case Buzz.heavy:
      HapticFeedback.heavyImpact();
    case Buzz.selection:
      HapticFeedback.selectionClick();
  }
}

/// Scales its child down while pressed - gives every tappable a tactile feel.
class PressScale extends StatefulWidget {
  const PressScale({super.key, required this.child, required this.onTap, this.enabled = true, this.onLongPress});

  final Widget child;
  final VoidCallback? onTap;
  final VoidCallback? onLongPress;
  final bool enabled;

  @override
  State<PressScale> createState() => _PressScaleState();
}

class _PressScaleState extends State<PressScale> {
  bool _down = false;

  @override
  Widget build(BuildContext context) {
    final active = widget.enabled && widget.onTap != null;
    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTapDown: active ? (_) => setState(() => _down = true) : null,
      onTapCancel: active ? () => setState(() => _down = false) : null,
      onTapUp: active ? (_) => setState(() => _down = false) : null,
      onTap: active
          ? () {
              haptic();
              widget.onTap!();
            }
          : null,
      onLongPress: widget.onLongPress,
      child: AnimatedScale(
        scale: _down ? 0.955 : 1.0,
        duration: const Duration(milliseconds: 90),
        curve: Curves.easeOut,
        child: widget.child,
      ),
    );
  }
}

class GlassCard extends StatelessWidget {
  const GlassCard({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(14),
    this.margin,
    this.radius = 18,
    this.blur = false,
    this.color,
    this.borderColor,
    this.gradient,
    this.glow,
    this.onTap,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;
  final EdgeInsetsGeometry? margin;
  final double radius;
  final bool blur;
  final Color? color;
  final Color? borderColor;
  final Gradient? gradient;
  final Color? glow;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    Widget content = Container(
      padding: padding,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(radius),
        color: gradient == null ? (color ?? AppColors.glass) : null,
        gradient: gradient,
        border: Border.all(color: borderColor ?? AppColors.stroke),
        boxShadow: glow == null
            ? null
            : [BoxShadow(color: glow!.o(0.32), blurRadius: 26, spreadRadius: -6)],
      ),
      child: child,
    );
    if (blur) {
      content = ClipRRect(
        borderRadius: BorderRadius.circular(radius),
        child: BackdropFilter(filter: ui.ImageFilter.blur(sigmaX: 16, sigmaY: 16), child: content),
      );
    }
    if (onTap != null) content = PressScale(onTap: onTap, child: content);
    if (margin != null) content = Padding(padding: margin!, child: content);
    return content;
  }
}

class NeonButton extends StatelessWidget {
  const NeonButton({
    super.key,
    required this.label,
    required this.onTap,
    this.icon,
    this.colors = const [AppColors.gold, AppColors.goldDeep],
    this.height = 54,
    this.fontSize = 24,
    this.textColor,
    this.subtitle,
    this.expand = true,
    this.radius = 14,
  });

  final String label;
  final VoidCallback? onTap;
  final IconData? icon;
  final List<Color> colors;
  final double height;
  final double fontSize;
  final Color? textColor;
  final String? subtitle;
  final bool expand;
  final double radius;

  @override
  Widget build(BuildContext context) {
    final enabled = onTap != null;
    final fg = textColor ?? (enabled ? colors.first.onColor : AppColors.textMute);
    final body = Container(
      height: height,
      width: expand ? double.infinity : null,
      padding: const EdgeInsets.symmetric(horizontal: 18),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(radius),
        gradient: enabled
            ? LinearGradient(colors: colors, begin: Alignment.topLeft, end: Alignment.bottomRight)
            : const LinearGradient(colors: [Color(0xFF262B4A), Color(0xFF1A1E38)]),
        border: Border.all(color: Colors.white.o(enabled ? 0.35 : 0.08)),
        boxShadow: enabled
            ? [
                BoxShadow(color: colors.first.o(0.45), blurRadius: 22, spreadRadius: -4, offset: const Offset(0, 6)),
              ]
            : null,
      ),
      child: Stack(
        children: [
          // Top gloss.
          Positioned.fill(
            child: IgnorePointer(
              child: DecoratedBox(
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(radius),
                  gradient: LinearGradient(
                    begin: Alignment.topCenter,
                    end: Alignment.center,
                    colors: [Colors.white.o(enabled ? 0.28 : 0.04), Colors.white.o(0)],
                  ),
                ),
              ),
            ),
          ),
          Center(
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (icon != null) ...[
                  Icon(icon, color: fg, size: fontSize * 0.95),
                  const SizedBox(width: 8),
                ],
                Flexible(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        label,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: T.display(fontSize, color: fg, weight: FontWeight.w700, spacing: 1.2, height: 1.0),
                      ),
                      if (subtitle != null)
                        Text(subtitle!, maxLines: 1, style: T.ui(11.5, color: fg.o(0.8), weight: FontWeight.w700)),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
    return PressScale(onTap: onTap, enabled: enabled, child: body);
  }
}

class GhostButton extends StatelessWidget {
  const GhostButton({super.key, required this.label, required this.onTap, this.icon, this.color = AppColors.text, this.height = 46, this.expand = true});

  final String label;
  final VoidCallback? onTap;
  final IconData? icon;
  final Color color;
  final double height;
  final bool expand;

  @override
  Widget build(BuildContext context) {
    final enabled = onTap != null;
    return PressScale(
      onTap: onTap,
      enabled: enabled,
      child: Container(
        height: height,
        width: expand ? double.infinity : null,
        padding: const EdgeInsets.symmetric(horizontal: 14),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(13),
          color: Colors.white.o(0.06),
          border: Border.all(color: color.o(enabled ? 0.45 : 0.12)),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            if (icon != null) ...[
              Icon(icon, size: 18, color: color.o(enabled ? 1 : 0.35)),
              const SizedBox(width: 6),
            ],
            Flexible(
              child: Text(
                label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: T.display(19, color: color.o(enabled ? 1 : 0.35), weight: FontWeight.w600, spacing: 1),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class GradientText extends StatelessWidget {
  const GradientText(this.text, {super.key, required this.style, this.gradient = AppGradients.gold, this.textAlign, this.maxLines});

  final String text;
  final TextStyle style;
  final Gradient gradient;
  final TextAlign? textAlign;
  final int? maxLines;

  @override
  Widget build(BuildContext context) {
    return ShaderMask(
      blendMode: BlendMode.srcIn,
      shaderCallback: (r) => gradient.createShader(Rect.fromLTWH(0, 0, r.width, r.height)),
      child: Text(text, style: style.copyWith(color: Colors.white), textAlign: textAlign, maxLines: maxLines),
    );
  }
}

class Pill extends StatelessWidget {
  const Pill(this.text, {super.key, this.color = AppColors.cyan, this.filled = false, this.fontSize = 11.5, this.icon});

  final String text;
  final Color color;
  final bool filled;
  final double fontSize;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: EdgeInsets.symmetric(horizontal: fontSize * 0.65, vertical: fontSize * 0.22),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(40),
        color: filled ? color : color.o(0.14),
        border: Border.all(color: color.o(filled ? 0.0 : 0.5)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (icon != null) ...[
            Icon(icon, size: fontSize + 1, color: filled ? color.onColor : color),
            const SizedBox(width: 3),
          ],
          Text(
            text,
            style: T.ui(fontSize, color: filled ? color.onColor : color, weight: FontWeight.w700, spacing: 0.8),
          ),
        ],
      ),
    );
  }
}

Color roleColor(Role r) => switch (r) {
      Role.bat => const Color(0xFF39D0FF),
      Role.wk => const Color(0xFFB88CFF),
      Role.ar => const Color(0xFF3DF2A0),
      Role.bowl => const Color(0xFFFF7A59),
    };

class RoleBadge extends StatelessWidget {
  const RoleBadge(this.role, {super.key, this.size = 11});
  final Role role;
  final double size;

  @override
  Widget build(BuildContext context) => Pill(role.code, color: roleColor(role), fontSize: size);
}

class CountryChip extends StatelessWidget {
  const CountryChip(this.code, {super.key, this.size = 11});
  final String code;
  final double size;

  @override
  Widget build(BuildContext context) {
    final c = kCountryColors[code] ?? AppColors.textMute;
    return Container(
      padding: EdgeInsets.symmetric(horizontal: size * 0.55, vertical: size * 0.15),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(5),
        gradient: LinearGradient(colors: [c.lighten(0.15), c.darken(0.2)]),
        border: Border.all(color: Colors.white.o(0.25)),
      ),
      child: Text(code, style: T.ui(size, color: Colors.white, weight: FontWeight.w700, spacing: 0.8)),
    );
  }
}

/// Overall rating in a small tier-coloured hexagon-ish badge.
class RatingBadge extends StatelessWidget {
  const RatingBadge(this.value, {super.key, this.size = 34});
  final int value;
  final double size;

  @override
  Widget build(BuildContext context) {
    final c = tierColor(value);
    return Container(
      width: size,
      height: size,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(size * 0.3),
        gradient: LinearGradient(colors: [c.lighten(0.25), c.darken(0.25)], begin: Alignment.topLeft, end: Alignment.bottomRight),
        boxShadow: [BoxShadow(color: c.o(0.45), blurRadius: 10, spreadRadius: -2)],
      ),
      child: Text('$value', style: T.display(size * 0.62, color: c.onColor, weight: FontWeight.w700, height: 1.05)),
    );
  }
}

Color tierColor(int overall) {
  if (overall >= 88) return const Color(0xFFB78CFF);
  if (overall >= 80) return AppColors.gold;
  if (overall >= 72) return const Color(0xFFCFD8E6);
  return const Color(0xFFD08A57);
}

class CircleIconButton extends StatelessWidget {
  const CircleIconButton({super.key, required this.icon, required this.onTap, this.size = 42, this.color = AppColors.text});
  final IconData icon;
  final VoidCallback? onTap;
  final double size;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return PressScale(
      onTap: onTap,
      child: Container(
        width: size,
        height: size,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          color: Colors.white.o(0.08),
          border: Border.all(color: AppColors.stroke),
        ),
        child: Icon(icon, size: size * 0.48, color: color),
      ),
    );
  }
}

class TopBar extends StatelessWidget {
  const TopBar({super.key, required this.title, this.subtitle, this.onBack, this.actions = const [], this.accent = AppColors.gold});

  final String title;
  final String? subtitle;
  final VoidCallback? onBack;
  final List<Widget> actions;
  final Color accent;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(12, 6, 12, 4),
      child: Row(
        children: [
          if (onBack != null) ...[
            CircleIconButton(icon: Icons.arrow_back_ios_new_rounded, size: 38, onTap: onBack),
            const SizedBox(width: 10),
          ],
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(title, maxLines: 1, overflow: TextOverflow.ellipsis, style: T.display(26, weight: FontWeight.w700, spacing: 1.2)),
                if (subtitle != null)
                  Text(subtitle!, maxLines: 1, overflow: TextOverflow.ellipsis, style: T.label(11.5, color: accent)),
              ],
            ),
          ),
          ...actions,
        ],
      ),
    );
  }
}

/// Standard animated page background + safe area.
class ScreenFrame extends StatelessWidget {
  const ScreenFrame({super.key, required this.child, this.colors, this.bottom, this.grid = true});

  final Widget child;
  final List<Color>? colors;
  final Widget? bottom;
  final bool grid;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.bg0,
      body: Stack(
        children: [
          Positioned.fill(child: AuroraBackground(colors: colors, grid: grid)),
          SafeArea(child: child),
        ],
      ),
      bottomNavigationBar: bottom,
    );
  }
}

class SectionTitle extends StatelessWidget {
  const SectionTitle(this.text, {super.key, this.trailing, this.color = AppColors.gold});
  final String text;
  final Widget? trailing;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 10, bottom: 8),
      child: Row(
        children: [
          Container(width: 4, height: 18, decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(3))),
          const SizedBox(width: 8),
          Expanded(child: Text(text.toUpperCase(), style: T.label(13, color: AppColors.text))),
          ?trailing,
        ],
      ),
    );
  }
}

/// Animated number - used for purses, scores and bids.
class CountUp extends StatelessWidget {
  const CountUp({super.key, required this.value, required this.builder, this.duration = const Duration(milliseconds: 650)});

  final double value;
  final Widget Function(BuildContext, double) builder;
  final Duration duration;

  @override
  Widget build(BuildContext context) {
    return TweenAnimationBuilder<double>(
      tween: Tween(end: value),
      duration: duration,
      curve: Curves.easeOutCubic,
      builder: (c, v, _) => builder(c, v),
    );
  }
}

class StatBar extends StatelessWidget {
  const StatBar({super.key, required this.label, required this.value, this.color = AppColors.cyan, this.max = 99});
  final String label;
  final int value;
  final Color color;
  final int max;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        SizedBox(width: 44, child: Text(label, style: T.label(11))),
        Expanded(
          child: ClipRRect(
            borderRadius: BorderRadius.circular(6),
            child: Stack(
              children: [
                Container(height: 8, color: Colors.white.o(0.08)),
                FractionallySizedBox(
                  widthFactor: (value / max).clamp(0.0, 1.0),
                  child: Container(
                    height: 8,
                    decoration: BoxDecoration(gradient: LinearGradient(colors: [color.darken(0.2), color])),
                  ),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(width: 8),
        SizedBox(width: 26, child: Text('$value', textAlign: TextAlign.right, style: T.display(18, height: 1))),
      ],
    );
  }
}

Route<R> fadeRoute<R>(Widget page) => PageRouteBuilder<R>(
      transitionDuration: const Duration(milliseconds: 420),
      reverseTransitionDuration: const Duration(milliseconds: 300),
      pageBuilder: (_, _, _) => page,
      transitionsBuilder: (_, anim, _, child) {
        final curved = CurvedAnimation(parent: anim, curve: Curves.easeOutCubic);
        return FadeTransition(
          opacity: curved,
          child: ScaleTransition(scale: Tween(begin: 1.04, end: 1.0).animate(curved), child: child),
        );
      },
    );

void showToast(BuildContext context, String text, {Color color = AppColors.bg3}) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(text), backgroundColor: color, duration: const Duration(milliseconds: 1800)));
}
