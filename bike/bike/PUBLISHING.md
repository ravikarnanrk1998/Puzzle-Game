# Publishing Bike Rush on Google Play

App ID: `com.ravikarnangame.bikerush` · Upload file: `build/app/outputs/bundle/release/app-release.aab`

## 0. Back up your signing key (do this first!)

Copy these two files somewhere safe (Google Drive, USB stick):

- `android\app\bikerush-upload.jks`: your upload key
- `android\key.properties`: holds the key's password

Every future update must be signed with this key. Never share them or put them on GitHub.
Both are already in `android/.gitignore` (`*.jks`, `key.properties`), but if you zip or copy
this project folder to someone, delete them from the copy first.

## 1. Build the upload file

Do the Firebase setup in section 7 first, or the game is built without sign-in.

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
| `coins_small`  | 10,000 Coins      | +10,000 coins (can buy again)       | ₹49             |
| `coins_medium` | 30,000 Coins      | +30,000 coins (can buy again)       | ₹129            |
| `coins_large`  | 80,000 Coins      | +80,000 coins (can buy again)       | ₹249            |
| `double_coins` | Double Coins      | Coins count x2 forever (one-time)   | ₹99             |
| `bike_gold`    | Gold Rush Bike    | Premium bike, full nitro + shield   | ₹149            |
| `bike_neon`    | Neon Ghost Bike   | Premium bike, 5 lives + shield      | ₹199            |
| `diamonds_1`   | 1 Diamond         | +1 diamond (can buy again)          | ₹10             |
| `diamonds_10`  | 10 Diamonds       | +10 diamonds (can buy again)        | ₹79             |
| `diamonds_20`  | 20 Diamonds       | +20 diamonds (can buy again)        | ₹149            |
| `diamonds_50`  | 50 Diamonds       | +50 diamonds (can buy again)        | ₹349            |
| `diamonds_100` | 100 Diamonds      | +100 diamonds (can buy again)       | ₹599            |

Diamonds pay for 1 VS 1 matches (see section 7). Until the five `diamonds_…` products exist
and are Active, the **DIAMONDS** tab in the garage shows "BUY" buttons that do nothing.

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

- **Privacy policy**: Play asks for a URL. A simple page saying the game keeps the player's
  email address and game progress (coins, bikes, best score) in Google Firebase so progress
  can be restored, and that purchases are handled by Google Play, is enough. You can host it
  free on Google Sites or GitHub Pages.
- **Ads**: No.
- **Data safety**: the game collects **Email address** and **User IDs** (for app functionality
  and account management). Nothing is shared with other companies. Data is encrypted in
  transit. Payments are handled by Google Play.
- **Account deletion**: because players can create an account, Play asks for a way to delete
  it, both inside the app and through a web link.
  - In the app: on the menu, tap the email at the top left → **DELETE ACCOUNT** → confirm.
    This deletes the account and its saved data (coins, bikes, best score) for good.
  - Web link: Play asks for a URL in the Data safety form. A simple page (it can be part of
    the privacy policy page) that explains the steps above and gives an email address for
    players who can't open the game is enough.
- **Content rating**: fill in the questionnaire (a racing game with no violence gets a low rating).
- **Target audience**: 13+ (choosing under-13 brings extra Families policy rules).

## 7. Sign-in and cloud save (Firebase)

Players sign in with Google, or with an email and password, and stay signed in after that.
Their coins, bikes, upgrades and best score are saved to their account in the Firebase project
`bike-rush-f46df`, so nothing is lost after a reinstall or on a new phone.

Do this once at https://console.firebase.google.com/project/bike-rush-f46df:

1. **Project settings → Your apps → Add app → Android**, package name
   `com.ravikarnangame.bikerush`.
2. On that app, **Add fingerprint** for every key the game is signed with. Google sign-in
   fails (it just says "cancelled") for a build whose key is missing here.
   - Debug key on this PC (used by `flutter run`):
     `F3:E5:78:55:A2:4E:6C:31:A3:C2:87:59:32:F4:1D:E2:AD:29:9C:AA`
   - Upload key (`bikerush-upload.jks`):
     `B0:F4:99:38:1D:2B:C1:D1:5E:15:6F:65:B8:37:A8:91:0E:DF:F9:F4`
   - Play App Signing key: **Play Console → App integrity → App signing**, copy the SHA-1 of
     the *App signing key certificate*. Installs from Play Store use this one.

   On another PC the debug key is different. To print the fingerprints again, run this in
   PowerShell from the `android` folder and read the `SHA1` lines:

   ```
   $env:JAVA_HOME = "C:\Program Files\Android\Android Studio\jbr"; .\gradlew signingReport
   ```
3. **Authentication → Sign-in method**: enable **Google** and **Email/Password**.
4. **Firestore Database → Create database** (production mode). Then open the **Rules** tab,
   paste in the contents of `firestore.rules` and press **Publish**.
5. Back in **Project settings → Your apps**, download `google-services.json` and save it as
   `android\app\google-services.json`. Download it after steps 2 and 3 so it includes the
   Google sign-in details.

Until `google-services.json` is there, the app still builds but opens straight into the game,
without sign-in, and saves on the phone only.

Each player's save is one document in Firestore at `users/<their account id>`. You can look at
it in the console, and the rules let only that player read or change it.

### 1 VS 1 matches

The **1 VS 1** button on the menu lets two signed-in players race at the same time. One taps
**CREATE CODE** and sends the 6-digit code to a friend, who types it under **ENTER CODE**.
Both then ride their own road, see each other's score, and the higher score wins.

Creating a code costs **1 diamond**; joining a friend's code is free. If the player cancels
before a friend joins, the diamond is given back. Players get diamonds two ways:

- **Free**: 1 diamond a day, with the **CLAIM DAILY +1** button on the menu, and 1 diamond
  every time a run completes 10 levels (level 10, 20, 30…).
- **Paid**: packs of 1, 10, 20, 50 and 100 in **GARAGE → DIAMONDS** (the `diamonds_…`
  products in section 3).

Matches are kept in Firestore under `matches/<code>` while they are played, so they need their
own rules. Whenever `firestore.rules` changes, paste the whole file into **Firestore Database →
Rules** again and press **Publish**. Until the match rules are published, the game says
"1 VS 1 is not switched on yet."

A player sends their score every 3 seconds, so a two-minute match is about 80 writes.
The free Firebase plan allows 20,000 writes a day.

If a player's internet stops working during their run, they see **WEAK NETWORK** after about
3 seconds. If it doesn't come back within about 8 seconds, their run ends and they lose the
match. Their opponent is given the win. The first result saved in Firestore (the `winner`
field of the match) is final, so a phone that comes back online later can't change it with
the scores it rode offline.

## Regenerating the icon and store images

```
flutter test tool/store_assets_test.dart
flutter pub run flutter_launcher_icons
```

## Notes

- Purchases are checked on the device only. That's fine for a small game. A bigger game
  should also verify purchases on its own server.
- Player progress (coins, bikes, best score) is saved on the phone and in the player's account
  (section 7). After a reinstall it all comes back, coins included, when they sign in again.
  Premium bikes and Double Coins can also be brought back through **Restore purchases** in
  the shop.
- The game can be played offline once signed in. Changes are kept on the phone and sent to
  the account when the internet is back.
