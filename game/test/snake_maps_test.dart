import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:game/snake_game.dart';
import 'package:game/store/store_service.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Every open cell must be reachable, or food could spawn somewhere the
/// snake can never get to. Movement wraps around the board edges.
int _reachableCells(Set<Point<int>> walls, Point<int> start) {
  final seen = {start};
  final queue = [start];
  while (queue.isNotEmpty) {
    final p = queue.removeLast();
    for (final (dx, dy) in [(1, 0), (-1, 0), (0, 1), (0, -1)]) {
      final n = Point(
        (p.x + dx + kSnakeCols) % kSnakeCols,
        (p.y + dy + kSnakeRows) % kSnakeRows,
      );
      if (!walls.contains(n) && seen.add(n)) queue.add(n);
    }
  }
  return seen.length;
}

void main() {
  for (final map in kSnakeMaps) {
    group(map.name, () {
      test('walls are on the board', () {
        for (final w in map.walls) {
          expect(w.x, inInclusiveRange(0, kSnakeCols - 1));
          expect(w.y, inInclusiveRange(0, kSnakeRows - 1));
        }
      });

      test('all open cells are connected', () {
        final open = kSnakeCols * kSnakeRows - map.walls.length;
        final start = [
          for (int y = 0; y < kSnakeRows; y++)
            for (int x = 0; x < kSnakeCols; x++) Point(x, y),
        ].firstWhere((p) => !map.walls.contains(p));
        expect(_reachableCells(map.walls, start), open);
      });

      test('snake starts with room to move', () {
        // Mirrors _placeSnakeSafely: first row whose cells 1-4 are open;
        // the snake's head is at x=4, heading right.
        final row = List.generate(kSnakeRows, (y) => y).firstWhere(
          (y) => [1, 2, 3, 4].every((x) => !map.walls.contains(Point(x, y))),
        );
        for (int x = 5; x <= 7; x++) {
          expect(map.walls.contains(Point(x, row)), isFalse);
        }
      });
    });
  }

  test('premium maps are the ones that were marked coming soon', () {
    expect(kSnakeMaps.where((m) => m.premium).map((m) => m.id), [
      'outri',
      'meq',
      'beltling',
      'echo',
      'seek',
      'frontier',
      'frenzy',
      'alleys',
    ]);
    for (final m in kSnakeMaps.where((m) => m.premium)) {
      expect(m.walls, isNotEmpty, reason: '${m.name} needs a layout');
    }
  });

  group('turns', () {
    const up = SnakeDirection.up;
    const down = SnakeDirection.down;
    const left = SnakeDirection.left;
    const right = SnakeDirection.right;

    test('two quick swipes both apply, one per move', () {
      final turns = SnakeTurns()
        ..add(up, right)
        ..add(left, right); // doubling back: fine once heading up
      var heading = right;
      heading = turns.next(heading);
      expect(heading, up);
      heading = turns.next(heading);
      expect(heading, left);
      expect(turns.next(heading), left);
    });

    test('reversing straight back is ignored', () {
      final turns = SnakeTurns()..add(left, right);
      expect(turns.next(right), right);

      turns
        ..add(up, right)
        ..add(down, right); // reverses the queued up
      expect(turns.next(right), up);
      expect(turns.next(up), up);
    });

    test('repeats are ignored and at most two turns wait', () {
      final turns = SnakeTurns()
        ..add(right, right)
        ..add(up, right)
        ..add(up, right)
        ..add(left, right)
        ..add(down, right);
      expect(turns.next(right), up);
      expect(turns.next(up), left);
      expect(turns.next(left), left);
    });

    test('clear drops waiting turns', () {
      final turns = SnakeTurns()..add(up, right);
      turns.clear();
      expect(turns.next(right), right);
    });
  });

  group('map select', () {
    setUp(() async {
      SharedPreferences.setMockInitialValues({});
      await StoreService.instance.loadLocal();
    });

    Future<void> showMapSelect(WidgetTester tester) async {
      // A phone-sized screen, so every map tile is on screen.
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 2.625;
      addTearDown(tester.view.reset);
      await tester.pumpWidget(const MaterialApp(home: SnakeMapSelectPage()));
    }

    testWidgets('a free map opens without asking for diamonds', (
      tester,
    ) async {
      await showMapSelect(tester);
      await tester.tap(find.text('CLASSIC'));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 500));

      expect(find.textContaining('Unlock'), findsNothing);
      expect(find.byType(SnakeGamePage), findsOneWidget);

      // Let the game run a few moves, then dispose it to stop its timer.
      await tester.pump(const Duration(seconds: 1));
      await tester.pumpWidget(const SizedBox());
    });

    testWidgets('a locked map asks to unlock first', (tester) async {
      await showMapSelect(tester);
      await tester.tap(find.text('OUTRI'));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 300));

      expect(find.text('Unlock Outri map'), findsOneWidget);
      expect(find.byType(SnakeGamePage), findsNothing);
    });
  });
}
