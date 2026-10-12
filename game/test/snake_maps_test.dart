import 'dart:math';

import 'package:flutter_test/flutter_test.dart';
import 'package:game/snake_game.dart';

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
}
