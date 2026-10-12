import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/foundation.dart';

/// Background music for a run. One track, looped; pausing keeps its place
/// so the next run carries on from there.
class GameMusic {
  static const _track = 'music/background-music.mp3';

  final AudioPlayer _player = AudioPlayer();
  bool _wanted = false;
  bool _playing = false;
  bool _loaded = false;
  bool _busy = false;

  /// Plays while [on] is true and pauses otherwise. Cheap to call every
  /// frame with the same value.
  void setPlaying(bool on) {
    if (on == _wanted) return;
    _wanted = on;
    _sync();
  }

  // The player is asynchronous, so a pause can arrive while the track is
  // still loading. Keep going until the player matches what was asked last.
  Future<void> _sync() async {
    if (_busy) return;
    _busy = true;
    try {
      while (_playing != _wanted) {
        final on = _wanted;
        if (!on) {
          await _player.pause();
        } else if (_loaded) {
          await _player.resume();
        } else {
          await _player.setReleaseMode(ReleaseMode.loop);
          await _player.play(AssetSource(_track));
          _loaded = true;
        }
        _playing = on;
      }
    } catch (e) {
      debugPrint('Music failed: $e');
    }
    _busy = false;
  }
}
