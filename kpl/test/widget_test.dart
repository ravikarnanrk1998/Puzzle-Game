import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kpl/main.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  testWidgets('splash leads to the main menu', (tester) async {
    SharedPreferences.setMockInitialValues({});
    tester.view.physicalSize = const Size(2400, 1080);
    tester.view.devicePixelRatio = 2.625;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(const KplApp());
    await tester.pump(const Duration(milliseconds: 2800));
    expect(find.text('TAP TO START'), findsOneWidget);

    await tester.tap(find.text('TAP TO START'));
    await tester.pump(const Duration(milliseconds: 500));
    await tester.pump(const Duration(milliseconds: 500));
    expect(find.textContaining('MEGA AUCTION'), findsWidgets);
    expect(find.textContaining('MINI AUCTION'), findsWidgets);
  });
}
