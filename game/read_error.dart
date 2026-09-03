import 'dart:convert';
import 'dart:io';

void main() {
  final file = File('test_output.json');
  final lines = file.readAsLinesSync();
  for (var line in lines) {
    if (line.isEmpty) continue;
    final map = jsonDecode(line);
    if (map['error'] != null) {
      print('ERROR: ${map['error']}');
    }
    if (map['stackTrace'] != null) {
      print('STACKTRACE: ${map['stackTrace']}');
    }
  }
}
