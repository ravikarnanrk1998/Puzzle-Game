# Freeride Fury (Unity 6 / URP)

A landscape 3D downhill mountain-bike racer for Android, in the spirit of the PS2-era downhill brawlers: race down a mountain against five rivals, throw tricks off kickers and drops, punch rivals off their bikes, and burn adrenaline for boost. All names, riders, bikes and tracks are original. Everything (terrain, trees, bikes, riders, UI, sound and music) is generated in code, so there are no imported art or audio files.

## What's in it

| Area | Details |
| --- | --- |
| Racing | You pedal automatically and gravity does the rest. Cornering has a grip limit: carry too much speed and you drift wide into the tape, so brake for hairpins and use the banked berms. Rough verges slow you, and hitting the tape hard or an obstacle at speed is a crash. |
| Jumps | Hold **JUMP** to preload and release to hop (clear logs). Hold it into a kicker lip to **POP** for extra height. There are rock drops and roller sections too. |
| Tricks | In the air the buttons become **SUPERMAN / NO HANDS / CAN-CAN**. Hold a trick for points, then let go before landing. Steer in the air to spin 360s. Two or three different tricks in one jump give a x1.5 or x2 combo. Landing sideways or mid-trick is a crash. |
| Combat | **PUNCH** a rider beside you. Each hit drains their balance bar, and at zero they are knocked off (a **TAKEDOWN**). AI riders fight back, and the aggressive ones target you. |
| Boost | Tricks, hits, takedowns and glowing orbs fill a four-bar **ADRENALINE** meter. Each full bar is one **BOOST**. |
| Mountains | Pine Ridge (forest), Red Rock Canyon (desert), Maple Hollow (autumn, golden hour), Glacier Pass (snow, low grip), Ember Peak (volcano, ash and lava glow). Each course is about 1.7 to 2.2 km and is generated from its own seed. |
| Career | 15 events, each graded bronze, silver or gold: Downhill Race (podium), Trick Attack (points target), Takedown Brawl (knock-downs) and a Championship Final. A medal unlocks the next event. |
| Garage | 6 riders with perks (speed, handling, strength, acceleration, +25% trick points), 6 bikes from a $0 hardtail to a $22,000 carbon DH bike (speed, accel, handling, strength), and 10 free frame paints. |
| Controls | Steering by left-thumb arrows (slide between them) or phone tilt (3 sensitivities), set in **Settings** or the pause menu. A keyboard also works for testing: arrows/A-D steer, Space jump, J punch, K brake, L boost, Esc pause. |
| Graphics | **Settings > Graphics** has LOW, MEDIUM (default) and HIGH. They scale render resolution, shadows, tree density, terrain detail and post-processing. **Frame rate** is 30 FPS by default, or 60 SMOOTH. |

## Build and run

Needs the Unity 6 editor that the cricket project uses (6000.6.6f1), with Android Build Support. Run these from this folder:

```powershell
powershell -ExecutionPolicy Bypass -File Tools\frf.ps1 setup   # compile + create materials, scene, icon, player settings
powershell -ExecutionPolicy Bypass -File Tools\frf.ps1 apk     # Builds\FreerideFury.apk (ARM64 test build)
powershell -ExecutionPolicy Bypass -File Tools\frf.ps1 run     # install + launch on the phone / emulator
powershell -ExecutionPolicy Bypass -File Tools\frf.ps1 win     # Builds\Windows\FreerideFury.exe desktop test player
```

`all` does setup, apk and run. You can also open the folder in Unity Hub (**Add > Add project from disk**) and use the **FRF** menu.

Automated smoke test and screenshots: launch with `-frfShots shots` (add `-frfTrack pine` for one mountain). The game walks every menu, races each mountain on autopilot, saves PNGs, logs build time, FPS and race stats, then quits.

```powershell
adb shell "am start -S -n com.rkgame.freeridefury/com.unity3d.player.UnityPlayerGameActivity -e unity '-frfShots shots'"
adb pull /storage/emulated/0/Android/data/com.rkgame.freeridefury/files/shots
```

## Google Play

The package name is `com.rkgame.freeridefury` (in `Assets/FRF/Editor/BuildTools.cs`). It can never change after the first upload. For a signed bundle, put the upload keystore in `Signing\` together with `Signing\keystore.properties` (`storeFile=`, `storePassword=`, `keyAlias=`, `keyPassword=`), or set `FRF_KEYSTORE`, `FRF_KEYSTORE_PASS`, `FRF_KEY_ALIAS` and `FRF_KEY_PASS`. Then run `Tools\frf.ps1 aab`. Raise `VersionCode` for every upload.

## Code map

```
Assets/FRF/Game
  Boot/      GameApp (entry, canvas, navigation, race flow), Settings + Profile (saves), Sfx (synth audio + music), AutoShots, Dbg
  Data/      Catalog: themes, tracks, bikes, riders, career events, prizes
  Track/     Track (course generator + track-space queries), TerrainField (heightfield), TrackScene (meshes), Props (low-poly scenery)
  Race/      Rider (physics, tricks, combat, crashes), RiderAI, RiderModel (procedural bike + IK rider), RaceDirector (race loop, camera)
  Render/    Mats + Palette, MeshKit, Tex (procedural textures), TextBaker, SceneLook (sky, light, fog, post), Fx (particles)
  UI/        Ui kit, Widgets (hold buttons, steer pad), Hud, Menus, MenuStage (3D showroom)
Assets/FRF/Editor     Setup + Android/Windows build automation
Assets/FRF/Bootstrap  Adds URP to a fresh project
Tools/frf.ps1         Command-line setup / build / run
```

Fonts: Teko and Rajdhani, SIL Open Font License.
