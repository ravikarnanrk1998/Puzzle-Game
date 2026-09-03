import 'dart:io';

void main() {
  List<String> files = [
    'lib/block_puzzle.dart',
    'lib/main.dart',
    'lib/sliding_puzzle.dart',
  ];

  for (String path in files) {
    File file = File(path);
    if (!file.existsSync()) continue;

    String content = file.readAsStringSync();

    // Fix withOpacity
    content = content.replaceAllMapped(
      RegExp(r'\.withOpacity\((.*?)\)'),
      (match) => '.withValues(alpha: ${match[1]})',
    );

    // Fix Color.value -> .toARGB32()
    content = content.replaceAll('c?.value', 'c?.toARGB32()');

    // Fix curly braces in if/for - easier to do this manually in sed or just replace the specific lines.
    content = content.replaceAll(
      'if (shapeDef == null || !_canPlaceShape(shapeDef, startRow, startCol))\n      return;',
      'if (shapeDef == null || !_canPlaceShape(shapeDef, startRow, startCol)) { return; }',
    );
    content = content.replaceAll(
      'if (!isReady)\n      return const Scaffold(',
      'if (!isReady) { return const Scaffold(',
    );
    content = content.replaceAll(
      'for (int c = 0; c < boardSize; c++) isClearing[r][c] = true;',
      '{ for (int c = 0; c < boardSize; c++) isClearing[r][c] = true; }',
    );

    file.writeAsStringSync(content);
  }
}
