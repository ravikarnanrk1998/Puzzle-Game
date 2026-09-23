# Publishing Bike Rush on Google Play

App ID: `com.ravik.bikerush` · Upload file: `build/app/outputs/bundle/release/app-release.aab`

## 0. Back up your signing key (do this first!)

Copy these two files somewhere safe (Google Drive, USB stick):

- `android\app\bikerush-upload.jks`: your upload key
- `android\key.properties`: holds the key's password

Every future update must be signed with this key. Never share them or put them on GitHub.
Both are already in `android/.gitignore` (`*.jks`, `key.properties`), but if you zip or copy
this project folder to someone, delete them from the copy first.

## 1. Build the upload file

```
flutter build appbundle --release
```

For every update, raise the version in `pubspec.yaml` first, e.g. `1.0.0+1` → `1.0.1+2`
(the number after `+` must always go up).

## 2. Play Console setup

1. Create a developer account at https://play.google.com/console (one-time US$25 fee).
2. **Create app** → name `Bike Rush`, type **Game**, **Free**.
3. **Settings → Payments profile**: set up a merchant account. You can't sell in-app items
   (or get paid) without one.
4. **Testing → Internal testing → Create release** → upload `app-release.aab`.
   Accept **Play App Signing** when asked.

> New personal developer accounts must run a **closed test with at least 12 testers for
> 14 days** before Google allows a Production release. Start the closed test early.

## 3. Create the in-app products

**Monetize → Products → In-app products**. You can only do this after an `.aab` has been uploaded.
The IDs must match **exactly**. The prices are only suggestions; pick your own.

| Product ID     | Name              | What it gives                       | Suggested price |
|----------------|-------------------|-------------------------------------|-----------------|
| `coins_small`  | 2,000 Coins       | +2,000 coins (can buy again)        | ₹49             |
| `coins_medium` | 6,000 Coins       | +6,000 coins (can buy again)        | ₹129            |
| `coins_large`  | 15,000 Coins      | +15,000 coins (can buy again)       | ₹249            |
| `double_coins` | Double Coins      | Coins count x2 forever (one-time)   | ₹99             |
| `bike_gold`    | Gold Rush Bike    | Premium bike, full nitro + shield   | ₹149            |
| `bike_neon`    | Neon Ghost Bike   | Premium bike, 5 lives + shield      | ₹199            |

Set every product to **Active**.

## 4. Test purchases without paying

1. **Settings → License testing**: add your Gmail address.
2. Join your internal test (use the opt-in link) and install the app from Play Store on your phone.
3. Open **GARAGE → COINS**. Purchases show "Test card, always approves", and you're not charged.

The shop only works in the version installed from Google Play (internal, closed or production track).
In Chrome, or with `flutter run`, the Coins tab says purchases are unavailable. That's expected.

## 5. Store listing

Graphics are ready in `store_listing/`:

- App icon: `icon_512.png`
- Feature graphic: `feature_graphic_1024x500.png`
- Phone screenshots: `screenshot_1_menu.png` … `screenshot_4_garage.png`

**Short description** (max 80 chars):

> Race through traffic, grab coins and fire NITRO! Easy, fast and fun.

**Full description:**

> Bike Rush is a fast, easy-to-play racing game! Tap left or right to switch lanes,
> dodge cars and trucks, collect coins and hit NITRO to smash through traffic.
>
> • Simple one-touch controls
> • Close calls and combos for big scores
> • Nitro boost, shields and extra lives
> • Unlock 7 bikes, each with its own style and perks
> • The game gets faster with every level. How far can you go?

## 6. App content (required forms)

- **Privacy policy**: Play asks for a URL. A simple page saying the game stores progress
  only on the device and purchases are handled by Google Play is enough. You can host it free
  on Google Sites or GitHub Pages.
- **Ads**: No.
- **Data safety**: no data collected or shared. Payments are handled by Google Play.
- **Content rating**: fill in the questionnaire (a racing game with no violence gets a low rating).
- **Target audience**: 13+ (choosing under-13 brings extra Families policy rules).

## Regenerating the icon and store images

```
flutter test tool/store_assets_test.dart
flutter pub run flutter_launcher_icons
```

## Notes

- Purchases are checked on the device only. That's fine for a small game. A bigger game
  should also verify purchases on its own server.
- Player progress (coins, bikes, best score) is saved on the phone. Premium bikes and Double
  Coins come back automatically after a reinstall, or through **Restore purchases** in the shop.
  Coins do not come back.
