# Services setup: Google Play Billing, ads, privacy

This build contains real Google Play Billing code, but it cannot take payments until the steps below
are done in Play Console. Nothing in the game fakes a purchase: until billing is configured, the store
shows a status message instead of buy buttons. Purchases with in-game coins and gems work as normal.

## 1. What is implemented

| Piece | Where | State |
|---|---|---|
| Billing backend (Unity IAP 5.5, Google Play) | `Assets/UA/Billing/GooglePlayBilling.cs` | Compiles. Not yet tested against Play (needs the console setup below). |
| Receipt check (signature, product id, package, purchase state) | same file, `Verify()` | Needs the Play license key (step 3). |
| Optional server check of the purchase token | same file, `ServerVerify()` | Off until `purchaseVerifyUrl` is set (step 4). |
| Grant, then save, then confirm | `Iap.OnVerified` in `Assets/UA/Game/Services/Monetize.cs` | Items are written to the save before the purchase is acknowledged or consumed. |
| Duplicate protection | `Store.Grant` in `Assets/UA/Core/Store.cs` | Each purchase token is recorded once. |
| Restore | Store screen and Settings "RESTORE", plus automatically at startup | Re-applies owned non-consumables only. Never re-grants coins or gems. |
| Rewarded ads | `Ads` facade only | **No ads SDK in this build.** Ad buttons stay hidden. |

How a purchase flows:

1. The player taps a price. Google Play shows its purchase sheet.
2. When Google Play reports the purchase, the receipt is verified. Nothing is granted if verification fails.
3. The items are granted and the save is written to disk.
4. Only then is the purchase confirmed with Google Play (consumed or acknowledged).

If the app is closed, offline or the save fails before step 4, the purchase stays unconfirmed. Google Play
re-delivers it on the next launch, and refunds it automatically if it is never acknowledged within 3 days.

Other cases:
- **Cancelled:** a short toast; nothing is granted.
- **Pending payment** (for example cash at a shop): the player sees "payment is being processed"; items arrive when Google Play completes the payment.
- **Failed:** an error message; nothing is granted and the player is not charged.

## 2. Play Console: app and products

1. Create the app with package name `com.rkgame.undeadapocalypse`.
2. Build a signed AAB (`powershell -ExecutionPolicy Bypass -File Tools\ua.ps1 aab`) and upload it to the
   **Internal testing** track. Play only enables billing for an app that has an uploaded build.
3. Under **Monetize with Play > Products > In-app products**, create each product below. The id must match
   exactly. Set a price, then **Activate** it.

| Product id | Type in game | What it grants | Suggested price |
|---|---|---|---|
| `gems_100` | consumable | 100 gems | $0.99 |
| `gems_500` | consumable | 550 gems | $4.99 |
| `gems_1000` | consumable | 1,200 gems | $9.99 |
| `gems_2500` | consumable | 3,000 gems | $19.99 |
| `gems_5000` | consumable | 6,500 gems | $39.99 |
| `coins_small` | consumable | 25,000 coins | $1.99 |
| `coins_large` | consumable | 120,000 coins | $7.99 |
| `coins_mega` | consumable | 400,000 coins | $19.99 |
| `pack_starter` | one-time | R4 Carbine, 20,000 coins, 200 gems | $2.99 |
| `pack_survival` | one-time | 2 shotguns, 50,000 coins, 3 damage boosts | $6.99 |
| `pack_military` | one-time | HMG, DMR, Black Ops outfit, 100,000 coins, 500 gems | $14.99 |
| `pack_legendary` | one-time | Arc Rifle, Behemoth .50, Dragon skin, Gold gloves, 1,500 gems | $29.99 |
| `bundle_heavy` | one-time | Juggernaut 10-Gauge, Titan HMG | $9.99 |
| `bundle_special` | one-time | Flamethrower, Grenade Launcher, Arc Rifle | $12.99 |
| `skins_elite` | one-time | Gold, Carbon and Toxic skins | $4.99 |
| `coin_doubler` | one-time | Doubles all coins earned | $4.99 |
| `extra_loadout_slot` | one-time | Third weapon slot | $2.99 |

All products are "In-app products" in Play Console. Consumable versus one-time is decided by the game, not
by Play. The catalog is defined in `Assets/UA/Core/Store.cs`. The prices in the table are fallbacks only:
the store shows the localized price that Google Play returns.

## 3. Purchase verification key (required)

In Play Console, open **Monetize with Play > Monetization setup > Licensing** and copy the base64 RSA public
key. This is a public key, not a secret. Then do **one** of the following:

- **Recommended:** in the Unity editor, open **Services > In-App Purchasing > Receipt Validation
  Obfuscator**, paste the key and click **Obfuscate**. This generates
  `Assets/Scripts/UnityPurchasing/generated/GooglePlayTangle.cs`, which the game finds automatically.
- **Or:** paste the key into `Assets/UA/Resources/UAServices.json` as `"googlePlayLicenseKey"`.

Without the key, the Android build disables purchases. Logcat then shows
`IAP: In-app purchases are disabled in this build: the Google Play license key is not configured.`

## 4. Optional: server-side verification

The local check proves that Google signed the receipt for this app. For stronger protection (for example
against modified clients), also check every purchase token with the Google Play Developer API from your
own server. To turn this on, set `"purchaseVerifyUrl"` in `UAServices.json` to an HTTPS endpoint.

What the game sends (POST, JSON):

```json
{ "packageName": "com.rkgame.undeadapocalypse", "productId": "gems_500", "purchaseToken": "...", "orderId": "GPA....", "consumable": true }
```

What the endpoint must answer:

- `{"valid": true}` when `purchases.products.get` returns `purchaseState == 0` for that token.
- `{"pending": true}` when the payment is still pending.
- `{"valid": false, "reason": "..."}` when the token is invalid, refunded or cancelled.

A network failure or an HTTP 5xx leaves the purchase unconfirmed, and the game retries on the next launch.
The server needs a Google Cloud service account that has been granted access in **Play Console > Users and
permissions**.

Limit: gems and coins are still held in the device save, which is signed but not server-authoritative.
Making currency fully tamper-proof needs a backend that owns the wallet. That is out of scope for this build.

## 5. Testing purchases

1. In **Play Console > Setup > License testing**, add the Google accounts of your testers.
2. Install the game from the internal testing link, not by sideloading, signed in with a tester account.
3. Test each case:
   - Normal purchase: the test card "always approves". Check that the item arrives and that a second
     launch does not grant it again.
   - "Slow test card, approves after a few minutes": the game shows "pending", and the item arrives after
     approval, even if the game was restarted in between.
   - "Slow test card, declines": the game shows "pending", then nothing is granted.
   - Cancel the purchase sheet: a short toast, nothing granted.
   - Buy a one-time pack, clear app data, start the game, then press **RESTORE PURCHASES**. The items come
     back without the bundled coins and gems.
4. Watch `adb logcat -s Unity` for lines starting with `[UA] IAP:`.

## 6. Rewarded ads (not in this build)

The `Ads` facade enforces the ad rules:
- Ads are optional and rewarded only: free coins in the menu, double coins on the result screen, revive on the death screen.
- No ads during combat.
- At most 10 ads a day, with a 20 second gap between ads.

No ads SDK is included, so these buttons are hidden. To add AdMob:

1. Import the Google Mobile Ads Unity plugin (it brings the External Dependency Manager).
2. Implement `IAdsBackend` in a new assembly, like `UA.Billing`: `Initialize()`, `RewardedReady`, `ShowRewarded()`.
   Register it from `[RuntimeInitializeOnLoadMethod]`.
3. Put the app id and rewarded unit id in `UAServices.json`. Keep `admobUseTestAds` set to true until release.
4. Complete the AdMob privacy messages (UMP consent) and the Play **Data safety** form.

## 7. Privacy policy

Play requires a privacy policy for apps with in-app purchases. Host one and set `"privacyPolicyUrl"` in
`UAServices.json`. The Settings screen links to it.
