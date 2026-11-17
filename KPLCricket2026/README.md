# KPL Cricket 2026 (Unity 6 · URP · Android)

A 3D T20 franchise-cricket game for Android phones (landscape). You lead one of ten original KPL franchises through quick matches, a full KPL season (league stage → Qualifier 1 → Eliminator → Qualifier 2 → Final), 50 challenge scenarios and practice nets. You bat, bowl and field yourself.

All teams, players, crests, sponsors and grounds are fictional. Nothing uses real IPL / BCCI branding or real player names.

## Features

| Area | What's in the build |
| --- | --- |
| **Batting** | There are two control schemes, chosen in *Settings › Batting controls*.<br>**Stick + buttons:** aim with the stick (centred = the natural shot for that ball), then press GROUND, LOFT, DEFEND or LEAVE as the ball arrives. The buttons fire on touch-down, so timing is exact.<br>**Swipe:** swipe in the shot direction; a long or fast swipe goes in the air, and a tap defends.<br>Timing is graded PERFECT / GOOD / EARLY / LATE. Shots include the cover drive, straight drive, pull, hook, cut, sweep, slog sweep, scoop, lofted drive, flick and defence. With *Timing assist* on, a bounce marker and timing meter show when to play. |
| **Bowling** | Pace bowlers have stock, outswinger, inswinger, yorker, bouncer and slower ball. Spinners have stock turn, flighted, arm ball, wrong'un (googly / doosra) and yorker. You drag on the pitch to set line and length, press BOWL, then stop the accuracy meter. AUTO bowls a smart delivery for you, and FIELD switches between attacking, balanced and defensive fields. |
| **Fielding** | Real fielding positions follow T20 rules (powerplay, middle overs, death) and are mirrored for left-handers. Fielders chase, dive, catch, pick up and throw, and run-outs, stumpings and overthrows all happen. When your side fields, a TAP TO CATCH prompt appears for catches. |
| **Match engine** | Each delivery is a physics model: swing, spin and seam, bounce, and a ball flight with air drag, bounces and rolling friction. The intercept, catch, throw and running decisions are worked out from that flight. Matches include wides, no-balls with free hits, byes, leg byes, LBW, bowling quotas, super overs, a boundary-count tie-break and Player of the Match. AI v AI uses the same engine, and the T20 averages are about 137/8 at 7.9 an over (see `Tools/CoreTests`). |
| **Modes** | **Quick match:** 2, 5, 10 or 20 overs, day or night, four difficulty levels.<br>**KPL Tournament:** Express (9 games) or Full (14 games, two-group format), with a points table and NRR, fixtures, Gold Bat / Gold Ball / most-sixes stats and playoffs. You can play or simulate any match.<br>**Challenges:** 50 levels in 5 chapters, rated 1–3 stars.<br>**Nets:** practice with no pressure. |
| **Progression** | Coins for wins, boundaries and wickets, plus playoff and title bonuses and challenge stars. The shop sells 6 bats (timing window and power), 4 spike sets (bowling accuracy), 4 fielding programmes (catching) and 3 coaches with 5 levels each. You can train individual players (5 levels), claim 7-day daily rewards, earn 26 achievements with coin rewards, and track career stats. |
| **Squad** | 16-player squads with role, ratings, batting hand and bowling type. You pick the XI (maximum 4 overseas), set the batting order and choose the captain. |
| **Graphics** | Four profiles: LOW, MEDIUM, HIGH and ULTRA. Each profile has its own URP asset, covering render scale, MSAA, shadow resolution and distance, soft shadows, HDR, extra lights and SSAO on ULTRA. Texture sizes, floodlight spot lights, bloom, vignette and the skyline also change with the profile. A default is auto-detected from the device's RAM, CPU and GPU, and you can change it in Settings. You can also choose 30 or 60 FPS. |
| **World** | Every ground is procedural. Each has a striped outfield with a grass detail texture, and a clay pitch with cracks, footmarks and creases. Around it are LED stumps, a padded rope and scrolling LED boards. The two-tier stands have an animated crowd and cantilever roofs or corner towers, and there are big screens with live scores, floodlight towers that glow at night, a spidercam, team flags, trees and a city skyline. Day and night lighting use ACES tonemapping, bloom and colour grading. |
| **Characters** | Skinned 22-bone cricketers are built in code. Each has a kit texture with name, number and crest, a helmet with grille or a cap, pads, gloves and a shaped bat. Animation uses IK legs and arms, with procedural batting shots generated from the actual delivery, a full bowling action, run cycles, throws, dives, keeper takes, umpire signals and celebrations. |
| **Audio** | All sound is synthesised at start-up: bat cracks and edges, bounce, stumps, glove, crowd ambience and swells, cheers, groans, applause, dhol drums, a stadium horn, UI sounds and an original menu music loop. There are separate music and effects volumes. |
| **Save** | The save is a JsonUtility file, obfuscated and signed with HMAC-SHA256. It is written atomically (temp file → main file) and the previous save is kept as a backup, so a corrupt or edited file falls back to the backup. It saves automatically after every result, purchase and settings change, and when the app is paused or quit. |

## Requirements

- Unity **6000.6.6f1** (Unity 6) with Android Build Support (OpenJDK, Android SDK and NDK). Any newer 6000.x should work.
- Windows. The scripts use PowerShell. `dotnet` 8 or newer is needed only for the core tests.

## Set up, build and run

From the project folder:

```powershell
powershell -ExecutionPolicy Bypass -File Tools\kpl.ps1 setup     # URP tiers, materials, player settings, icon, scene
powershell -ExecutionPolicy Bypass -File Tools\kpl.ps1 apk       # Builds\KPLCricket2026.apk (ARM64)
powershell -ExecutionPolicy Bypass -File Tools\kpl.ps1 install   # adb install to the connected phone / emulator
powershell -ExecutionPolicy Bypass -File Tools\kpl.ps1 aab       # creates the upload key on first use, then a signed Builds\KPLCricket2026.aab
powershell -ExecutionPolicy Bypass -File Tools\kpl.ps1 win       # Windows test build (Builds\Windows)
powershell -ExecutionPolicy Bypass -File Tools\kpl.ps1 test      # core rules / balance / tournament / economy tests
```

You can also open the folder in Unity Hub and use the **KPL** menu (Setup, Make Icon, Build APK, Build AAB, Build Windows).

**Signing.** The first `aab` run creates `Signing\kpl-upload.keystore` with a random password, stored in `Signing\keystore.properties` and `Signing\KEYSTORE-INFO.txt`. Back this folder up somewhere safe: every future Play Store update must be signed with the same key. The folder is git-ignored.

**Versioning.** Raise `VersionCode` and `Version` in `Assets/KPL/Editor/BuildTools.cs` before each Play upload.

## Automated testing

The app has a built-in autopilot that plays the game with simulated touches:

```powershell
# Windows test build
Builds\Windows\KPLCricket2026.exe -kplAuto -kplShots C:\shots -kplPlan all
# Android
powershell -ExecutionPolicy Bypass -File Tools\kpl.ps1 run      # launches with -kplAuto
powershell -ExecutionPolicy Bypass -File Tools\kpl.ps1 shots    # pulls the screenshots to Builds\shots_android
```

The `all` plan does the following:

1. Visits every menu screen.
2. Buys a bat.
3. Plays a 2-over day match (bat first) and a 2-over night match (bowl first). Batting uses simulated timing; bowling uses delivery choice plus the meter, with catch prompts.
4. Simulates a full KPL season, then plays challenge 5.
5. Cycles all four graphics profiles.
6. Checks a save/load round-trip.

It logs FPS, memory and an error count (`[KPL] AUTOPILOT DONE errors=N`).

## Code map

```
Assets/KPL/Core        Engine-free rules (unit-tested): ball physics, batting, fielding/running, AI, match state,
                       tournament, challenges, economy, achievements, save data
Assets/KPL/Game/App    GameApp (boot, navigation), SaveSystem, Quality (graphics tiers), Audio (synth), Progression, AutoPilot
Assets/KPL/Game/Render Stadium builder, procedural textures, materials, meshes, text baking, lighting/post FX
Assets/KPL/Game/Characters  Skinned body + kit, gear, IK rig, poses, batting/bowling motion, Actor behaviours
Assets/KPL/Game/Match  MatchDirector (live match flow), CameraRig, ball view, effects, mode setups
Assets/KPL/Game/UI     UI kit + sprites/icons, crests, HUD and controls, popups, scorecard, all screens
Assets/KPL/Editor      Setup (URP tiers, materials, player settings, icon) and build automation
Tools/kpl.ps1          Command-line setup/build/install/run
Tools/CoreTests        .NET console tests for the rules engine
```

Fonts are Barlow and Barlow Condensed (SIL Open Font License, `Assets/KPL/Resources/Fonts/OFL-Barlow.txt`).
