// Dev tool: renders character poses from several angles to PNG sheets.
// Run with: flutter test test/preview --dart-define=PREVIEW_DIR=<folder>
import 'dart:io';
import 'dart:math' as math;
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kpl/game/camera.dart';
import 'package:kpl/game/character.dart';

const _dir = String.fromEnvironment('PREVIEW_DIR');

Future<void> _sheet(String name, List<(String, Pose, FigureLook, bool)> poses, {List<double> views = const [0.0, 1.5708, 3.1416, -0.785]}) async {
  const cell = 300.0;
  final w = cell * views.length, h = cell * poses.length;
  final rec = ui.PictureRecorder();
  final c = Canvas(rec, Rect.fromLTWH(0, 0, w, h));
  c.drawRect(Rect.fromLTWH(0, 0, w, h), Paint()..color = const Color(0xFF6E9A5A));
  final r = CharacterRenderer();
  for (var row = 0; row < poses.length; row++) {
    final (_, pose, look, mirror) = poses[row];
    for (var col = 0; col < views.length; col++) {
      final a = views[col];
      c.save();
      c.clipRect(Rect.fromLTWH(col * cell, row * cell, cell, cell));
      c.translate(col * cell, row * cell);
      final cam = Camera3D.lookAt(Vec3(math.sin(a) * 3.4, math.cos(a) * 3.4, 1.4), const Vec3(0, 0, 1.0), focal: 400, cx: cell / 2, cy: cell / 2);
      final actor = Actor(foot: Vec3.zero, facing: 0, pose: pose, look: look, mirror: mirror);
      // Ground grid.
      final g = Paint()..color = Colors.black.withValues(alpha: 0.15);
      for (var i = -2; i <= 2; i++) {
        c.drawLine(cam.project(Vec3(i * 0.5, -1, 0)), cam.project(Vec3(i * 0.5, 1, 0)), g);
        c.drawLine(cam.project(Vec3(-1, i * 0.5, 0)), cam.project(Vec3(1, i * 0.5, 0)), g);
      }
      c.drawLine(cam.project(Vec3.zero), cam.project(const Vec3(0, 0.8, 0)), Paint()
        ..color = Colors.red
        ..strokeWidth = 2);
      r.drawShadows(c, cam, [actor], day: true);
      r.draw(c, cam, actor);
      c.restore();
    }
  }
  final pic = rec.endRecording();
  final img = await pic.toImage(w.toInt(), h.toInt());
  final bytes = await img.toByteData(format: ui.ImageByteFormat.png);
  File('$_dir/$name.png').writeAsBytesSync(bytes!.buffer.asUint8List());
}

void main() {
  testWidgets('render character previews', (tester) async {
    if (_dir.isEmpty) return;
    final bat = batterLook(const Color(0xFF1E5BC6), const Color(0xFFD4AF37), name: 'SHARMA', number: 45);
    final field = fielderLook(const Color(0xFFF9CD05), const Color(0xFF1F3B8C), name: 'DHONI', number: 7);
    final keep = keeperLook(const Color(0xFFF9CD05), const Color(0xFF1F3B8C));
    await tester.runAsync(() async {
      await _sheet('bat1', [
        ('stance', pBatStance, bat, false),
        ('backlift', pBatBacklift, bat, false),
        ('driveC', pDriveContact, bat, false),
        ('driveF', pDriveFollow, bat, false),
        ('loft', pLoftFollow, bat, false),
      ]);
      await _sheet('bat2', [
        ('pullC', pPullContact, bat, false),
        ('pullF', pPullFollow, bat, false),
        ('cutC', pCutContact, bat, false),
        ('cutF', pCutFollow, bat, false),
        ('defend', pDefend, bat, false),
        ('sweep', pSweep, bat, false),
        ('leave', pLeave, bat, false),
      ]);
      await _sheet('bowl', [
        ('runA', pRunA, field, false),
        ('runB', pRunB, field, false),
        ('gather', pGather, field, false),
        ('load', pLoad, field, false),
        ('release', pRelease, field, false),
        ('follow', pFollow, field, false),
      ]);
      await _sheet('field', [
        ('keeper', pKeeperCrouch, keep, false),
        ('keeperUp', pKeeperUp, keep, false),
        ('slip', pSlip, field, false),
        ('ready', pFieldReady, field, false),
        ('catch', pCatch, field, false),
        ('throw', pThrow, field, false),
      ]);
      await _sheet('ump', [
        ('stand', pStand, field, false),
        ('nonstriker', pNonStriker, bat, false),
        ('umpire', pUmpire, umpireLook, false),
        ('out', pUmpOut, umpireLook, false),
        ('six', pUmpSix, umpireLook, false),
        ('wide', pUmpWide, umpireLook, false),
      ]);
    });
  });
}
