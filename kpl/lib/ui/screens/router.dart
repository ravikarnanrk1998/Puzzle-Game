import 'package:flutter/widgets.dart';

import '../../engine/career.dart';
import 'auction_screen.dart';
import 'champion_screen.dart';
import 'retention_screen.dart';
import 'season_hub_screen.dart';

/// Screen that resumes the career at its current stage.
Widget screenForStage(Career c) => switch (c.stage) {
      Stage.retention => const RetentionScreen(),
      Stage.auction => const AuctionScreen(),
      Stage.season => const SeasonHubScreen(),
      Stage.finished => const ChampionScreen(),
    };
