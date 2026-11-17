# Graphics upgrade (November 2026)

This pass changes only visuals: lighting, materials, textures, post-processing and set dressing.
It does not change gameplay values, AI, weapons data, the economy or saves.

The project generates everything procedurally (meshes, textures, materials) from code. "Upgrading assets"
therefore means changing generators in `Assets/UA/Game/Render` and `Assets/UA/Game/World`; there are no
imported model or texture files to replace.

A zip checkpoint taken before the pass is at
`..\UndeadApocalypse_backups\pre-graphics-upgrade-2026-11-05.zip`. Git is not installed on the build machine.

## Rendering fixes (affect every scene)

- **Viewmodel lighting:** the "Viewmodel" rendering layer was never actually defined. Hand-edited
  `TagManager.asset` entries are ignored by the editor, and URP drops undefined layer bits, so no light
  reached the first-person weapon. The build step `EnsureRenderingLayers()` now adds the layer through the
  editor API, and the weapon-only lights also sit on the Viewmodel GameObject layer.
- **NaN pixels:** Unity's `Vector3.normalized` returns zero below 1e-5. Tiny parts (sight dots, reticles)
  got zero normals, which rendered as a black pixel with a large bloom halo. `MeshBuilder.Nrm()` fixes this.
- **Bloom clamp:** bloom is clamped to 24, so one over-bright pixel can no longer flood the screen.
- **Weapon materials:** realistic albedo for steel, polymer and gloves (they were near-black). Metal
  smoothness is capped so close lights cannot overflow the HDR buffer.

## Lighting and post-processing (every biome)

- **Brighter scenes:** ambient light lifted about 3x in linear terms, and more ceiling fixtures are lit
  (light chance +25%). Indoor fog is 40% thinner, and indoor bounce light is slightly cool.
- **Grade:** ACES tonemapping with gentle split toning, cool shadows and warm highlights (kept close to
  neutral so nothing turns purple), contrast 16, post exposure +0.8 EV base.
- **Cleaner image:**
  - Vignette 0.22.
  - Film grain on Ultra only, at 0.08.
  - Chromatic aberration 0.05 (Ultra only).
  - Bloom threshold 0.95, intensity 0.75.
  - Fake light-cone alpha halved.
- **Reflection probes:** box-projected probes on a 12-16 m grid (up to 5x5 on Ultra, 3x3 on Medium) at
  256 px (Ultra) or 128 px (Medium). Before, Medium had one 64 px probe for the whole level. Reflection
  intensity is 0.9 indoors and 0.7 outdoors.
- **SSAO:** now on Medium as well as Ultra. Medium uses a downsampled, low-sample version.
- **Flashlight:** narrower cone aimed slightly higher, so the near floor no longer blows out.
- **Viewmodel light rig:** stronger key and rim lights that only light the weapon and hands.

## Underground metro (the reference screenshot)

- **Walls:** glazed blue-grey subway tile, about 40 x 20 cm in a running bond, glossy with bevelled
  edges and a navy band, and much less grime.
- **Platform floor:** grey stone tiles with a wet gloss film, so the fixtures reflect in it.
- **Ceiling:** concrete with deep beams over every cell boundary (coffered look).
- **Lighting:** continuous rows of strip fixtures over the concourse, the platform and the tracks. Real
  lights are spread evenly across those rows within each preset's light budget.
- **Track:** procedural stone ballast (new `Tex.Ballast`), concrete sleepers, and rails with foot, web
  and polished head.
- **Platform edge:** wide yellow tactile strip.
- **Tunnel mouths:** concrete portals at both ends of the tracks, with signal lamps.
- **Train:**
  - Silver cars with a blue livery band and red pinstripe.
  - Framed windows onto a warm-lit interior, and glazed doors.
  - Roof equipment, bogies, gangways.
  - Cab ends with windscreens and head and tail lights.
  - The collider is identical to the old train.
- **New dressing:**
  - Potted plants and fire-extinguisher cabinets on the back wall.
  - Benches on the platform.
  - Brighter ad boards.

## Other biomes

- Paler floors (hospital linoleum, mall marble, factory concrete) are darker so they no longer clip.
- Wooden crate stacks are capped at two crates, so they never reach into ceiling lamps.
- Arena floodlights are toned down.

## Second round (darkness on devices, zombies, weapons, characters)

**Why levels were dark on phones, and the fixes:**
- **HDR on every preset.** Low used to render without HDR, through the LDR grade, and looked far darker
  than Medium on the same device. Every preset now renders HDR (R11G11B10 costs the same bandwidth as
  RGBA8). Low also gets one small reflection probe.
- **Lights spread over the whole level.** Ceiling-fixture lights were handed out first-come in cell
  order, so once a preset's light budget (16/22/28) ran out, the rest of the map got no lights at all.
  Fixture lights are now collected and spread evenly over the whole level. When the budget drops some,
  the ones placed reach further.
- **Minimum bounce light.** No stage drops below a minimum ambient level. Blackout events keep a lower one.
- **Smaller presets get stronger lights.** Low and Medium lights are a little brighter and longer-ranged
  to make up for having fewer of them.

**Zombies, after the type sheet:**
- **All types:** adult (bulkier) builds, much more blood on clothes (soaked stains, spatter, drips),
  grimier skin, and hair on most walkers. Hitboxes are unchanged.
- **Explosive:** orange hi-vis vest with strapped charges and a blinking detonator.
- **Toxic:** shirtless, with glowing green veins (emissive vein map) and glowing pustules.
- **Hunter:** dark hood and cloak, blue eyes.
- **Mutant:** bigger and more muscular.

**Environments:**
- Indoor arenas (boss rooms) have a coffered concrete ceiling and wet sealed concrete floors.
- Arena edges are dressed with crates, drums, wrecked cars and wall signage.

**Pickups:**
- Health: red medic case with white crosses.
- Armor: plate carrier with pouches.
- Grenades: crate of frag grenades.

**New weapons** (appended to the weapon list, so saved weapon indices are unchanged):

| Weapon | Type | Price | Unlocks at |
|---|---|---|---|
| Viper SMG | fast compact submachine gun | 7,500 coins | level 4 |
| Hellfire RPG | straight-flying rocket with a smoke trail, 7 m blast | 700 gems | level 24 |

**Characters:** the six outfit slots became six characters: Soldier, Special Forces, Survivor, Hunter,
Engineer and Medic. Prices, ownership and the equipped choice carry over from the old outfits.
- **New Characters tab:** shows a turning full-body 3D preview of each character holding their signature
  weapon. Hands are placed on the weapon with two-bone IK.
- **Human models:** characters use human skin and faces (eyes, brows, lips, optional stubble).
- **In game:** the character still decides the first-person sleeves.

## Load time

Procedural texture mip chains are now built on worker threads (`Pix.Upload`). Level build time on
desktop dropped from about 5.0 s to 3.0 s at Ultra and from 1.55 s to 1.05 s at Medium.

## Not done in this pass

- **No HIGH preset.** The game keeps its three presets (Low, Medium, Ultra). Adding a fourth would change
  the saved setting index.
- **No baked GI or light probes.** Levels are generated at runtime, so there is nothing to bake. Lighting
  is realtime (Forward+) plus probes captured at load.
- **Zombie animations are unchanged.** Builds, gear and textures were upgraded (see the second round), but
  the bodies are still procedural rather than sculpted or scanned. Matching the detail of an AI concept
  sheet exactly would need imported character art made by an artist or a 3D asset pack.
- **The reference screenshot is an AI-generated paint-over.** It shows effects URP mobile does not provide
  out of the box, such as true screen-space mirror reflections. The wet look here comes from reflection
  probes and specular highlights.
- **HUD and menus:** layout and style already match the reference. The menu background now shows the
  upgraded metro.
