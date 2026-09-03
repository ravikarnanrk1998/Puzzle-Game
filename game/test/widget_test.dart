import 'package:flutter_test/flutter_test.dart';
import 'package:game/main.dart';

void main() {
  testWidgets('Puzzle game smoke test', (WidgetTester tester) async {
    // Build our app and trigger a frame.
    await tester.pumpWidget(const PuzzleApp());
    await tester.pumpAndSettle();

    // Verify that our main menu is present.
    expect(find.text('PUZZLE HUB'), findsOneWidget);

    // Verify that both game options are present.
    expect(find.text('Sliding Puzzle'), findsOneWidget);
    expect(find.text('Block Puzzle'), findsOneWidget);
  });
}
