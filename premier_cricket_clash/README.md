# Premier Cricket Clash 2027 (Unity 6 / URP)

A landscape-only 3D T20 cricket game for Android with 10 original franchises and procedural stadiums, kits and crests. The player database uses real cricketers' names and squads (`Assets/PCC/Core/RealPlayers.cs`). This is a Google Play policy risk. Set `PlayerDb.UseRealNames = false` to switch to the fully generated fictional players instead. Players can rename anyone in **EDIT NAMES**.

## What's in it

| Area | Details |
| --- | --- |
| Batting | Two control schemes, chosen in **Settings › Batting controls**:<br>• **Swipe**: swipe in the shot direction as the ball arrives (a fast swipe or the LOFT toggle goes aerial), tap to defend.<br>• **Buttons**: aim with the stick, then press **DEFEND / GROUND / LOFT** on time. |
| Bowling | Delivery dropdown (stock, slower, in-/out-swinger; spinners get stock, quicker, mystery), drag the target on the pitch map (yorker to bouncer), BOWL, then stop the accuracy meter. AUTO bowls for you. |
| Match | Toss, bowler picker, field settings, run-outs, free hits, super overs, Player of the Match, full scorecard, and pause to sim an over, an innings or the whole match. |
| Graphics | URP with sun shadows or a floodlit night, ACES tonemapping, colour grading, an animated crowd and flags, big screens, LED boards and a city skyline. **Settings › Graphics** picks LOW / MEDIUM (default) / HIGH. HIGH adds soft shadows, bloom, SMAA and floodlight spot lights. Matches run at 30 FPS unless **Frame rate › 60 SMOOTH** is chosen. Menus run at 30 FPS, and the stadium behind non-main menus is a frozen snapshot, to keep phones cool. |
| Mega auction | Pick a franchise, retain up to 6 players at slab prices (unused slots become Right-to-Match cards), then a live auction of about 260 lots: marquee, capped and uncapped sets, a ₹120 Cr purse, overseas and squad limits, AI teams that bid by need, RTM decisions, and an accelerated round. Controls: BID / PASS / SKIP LOT / SKIP SET / SIM REST at 1x/2x/4x speed. The results are saved, and quick matches can use the auction squads. |
| Screens | Main menu over a live orbiting stadium, quick match setup (teams, 2/5/10/20 overs, 3:30 PM day or 7:30 PM night, level, auction or original squads), settings, how to play, and a result page with the scorecard. |

The engine-independent rules (`Assets/PCC/Core`) are unit-tested with plain .NET:

```powershell
cd Tools\CoreTests; dotnet run -c Release
```

## One-time install

1. Install **Unity Hub** (https://unity.com/download) and sign in. A free Personal licence is fine.
2. In Hub › Installs › **Install Editor**, choose the newest **Unity 6 LTS (6000.x)** and tick **Android Build Support** together with **OpenJDK** and **Android SDK & NDK Tools**.

## Create the project, build and run

From this folder:

```powershell
powershell -ExecutionPolicy Bypass -File Tools\pcc.ps1 all
```

That command:

1. opens the folder once so Unity creates `ProjectSettings` and `Packages`;
2. adds URP, using the version bundled with your editor;
3. runs **PCC › 1. Setup Project**, which creates the base materials, boot scene, app icon, landscape-only player settings, IL2CPP + ARM64, and tunes URP;
4. builds `Builds\PremierCricketClash2027.apk`;
5. installs and launches it on the connected phone or emulator.

Individual steps are `init`, `setup`, `apk`, `aab` and `run`. You can also open the folder in Unity Hub (**Add › Add project from disk**) and use the **PCC** menu.

## Google Play release

1. Create an upload key once and keep it safe; you need it for every future update:
   ```powershell
   & "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe" -genkeypair -v -keystore pcc-upload.keystore -alias pcc -keyalg RSA -keysize 2048 -validity 10000
   ```
2. Build the signed bundle:
   ```powershell
   $env:PCC_KEYSTORE = "$PWD\pcc-upload.keystore"; $env:PCC_KEYSTORE_PASS = "..."; $env:PCC_KEY_ALIAS = "pcc"; $env:PCC_KEY_PASS = "..."
   powershell -ExecutionPolicy Bypass -File Tools\pcc.ps1 aab
   ```
3. Upload `Builds\PremierCricketClash2027.aab` in Play Console. Raise `PlayerSettings.Android.bundleVersionCode` for each new upload.
4. The package name is `com.rkgame.premiercricketclase` (set in `Assets/PCC/Editor/BuildTools.cs`). It can never change after the first upload. The `Signing\` folder already holds the upload key, and `Tools\pcc.ps1 aab` signs with it automatically.

## Code map

```
Assets/PCC/Core        Pure C# rules: players, teams, lineups, deliveries, ball-by-ball match engine
Assets/PCC/Game
  Boot/                GameApp (entry point, canvas, navigation), Settings, procedural Sfx
  Characters/          Pose library, IK skeleton, procedural cricketer meshes
  Render/              Stadium builder, procedural textures, materials, text baking, lighting / post-FX
  Match/               MatchDirector (ball cycle, cameras, actors), ball physics paths, props
  UI/                  HUD, scorecard, menus, widgets (joystick, touch layer, confetti)
Assets/PCC/Editor      Setup + Android build automation
Assets/PCC/Bootstrap   Adds URP to a fresh project
Tools/pcc.ps1          Command-line setup / build / run
Tools/CoreTests        .NET console tests for the rules engine
```

Fonts: Teko and Rajdhani, SIL Open Font License.
