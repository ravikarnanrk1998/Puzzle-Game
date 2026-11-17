using System;
using UnityEngine;

namespace UA.Game
{
    /// <summary>
    /// Console-provided service settings, read from Assets/UA/Resources/UAServices.json (see docs/SERVICES_SETUP.md).
    /// Nothing here is secret: the Google Play license key is a public key and AdMob ids ship inside every app.
    /// Empty values leave the matching feature disabled; the game never fakes a working service.
    /// </summary>
    [Serializable]
    public sealed class ServicesConfig
    {
        /// <summary>Play Console > Monetize with Play > Monetization setup > Licensing (base64 RSA public key).</summary>
        public string googlePlayLicenseKey = "";

        /// <summary>Optional HTTPS endpoint that checks purchase tokens with the Google Play Developer API.</summary>
        public string purchaseVerifyUrl = "";

        /// <summary>AdMob app id (ca-app-pub-...~...) and rewarded ad unit id (ca-app-pub-.../...).</summary>
        public string admobAppIdAndroid = "";
        public string admobRewardedUnitAndroid = "";

        /// <summary>Use Google's public test ad unit instead of the real one (development builds).</summary>
        public bool admobUseTestAds = true;

        public string privacyPolicyUrl = "";
        public string supportEmail = "";

        static ServicesConfig _current;

        public static ServicesConfig Current
        {
            get
            {
                if (_current != null) return _current;
                var ta = Resources.Load<TextAsset>("UAServices");
                try { _current = ta != null ? JsonUtility.FromJson<ServicesConfig>(ta.text) : null; }
                catch (Exception e) { Debug.LogWarning("[UA] UAServices.json is invalid: " + e.Message); }
                _current ??= new ServicesConfig();
                return _current;
            }
        }

        public bool HasLicenseKey => !string.IsNullOrWhiteSpace(googlePlayLicenseKey);
    }
}
