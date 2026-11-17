using System;
using System.Collections.Generic;
using UA.Core;
using UnityEngine;

namespace UA.Game
{
    public enum PurchaseOutcome { Success, Pending, Cancelled, Failed, Unavailable, AlreadyOwned }

    /// <summary>Result of applying a verified purchase. The backend confirms (consumes/acknowledges) with Google Play only on Granted or AlreadyProcessed.</summary>
    public enum GrantResult { Granted, AlreadyProcessed, UnknownProduct, SaveFailed }

    /// <summary>Implemented by the Google Play Billing (Unity IAP) backend when the package is installed.</summary>
    public interface IIapBackend
    {
        /// <summary>Connects to the store; called once after the save and UI are ready.</summary>
        void Connect();
        bool Ready { get; }
        string Status { get; }
        string Price(string productId);
        void Buy(string productId, Action<PurchaseOutcome, string> done);
        void Restore(Action<bool, string> done);
    }

    /// <summary>
    /// In-app purchase facade. Items are granted ONLY from <see cref="OnVerified"/>, which the billing
    /// backend calls after Google Play reports a completed purchase that passed receipt validation.
    /// Without a configured backend the store shows "unavailable" and nothing can be bought for money.
    /// </summary>
    public static class Iap
    {
        public static IIapBackend Backend;
        static readonly List<Action> Changed = new List<Action>();

        public static bool Available => Backend != null && Backend.Ready;
        public static string Status => Backend == null ? "Google Play Billing is not set up in this build." : Backend.Status;

        public static void OnChanged(Action a) { if (!Changed.Contains(a)) Changed.Add(a); }
        public static void RemoveChanged(Action a) => Changed.Remove(a);
        public static void NotifyChanged() { foreach (var a in Changed.ToArray()) { try { a(); } catch (Exception e) { Debug.LogException(e); } } }

        public static string Price(ProductDef p)
        {
            if (Available)
            {
                var s = Backend.Price(p.Id);
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return p.FallbackPrice;
        }

        public static void Buy(ProductDef p, Action<PurchaseOutcome> done = null)
        {
            if (!Available)
            {
                Screens.Message("STORE UNAVAILABLE", Status);
                done?.Invoke(PurchaseOutcome.Unavailable);
                return;
            }
            if (Store.IsOwned(SaveSystem.Data, p)) { Screens.Toast("Already owned"); done?.Invoke(PurchaseOutcome.AlreadyOwned); return; }
            Backend.Buy(p.Id, (outcome, msg) =>
            {
                switch (outcome)
                {
                    case PurchaseOutcome.Pending: Screens.Message("PURCHASE PENDING", "Your payment is being processed by Google Play. The items will be added as soon as it completes."); break;
                    case PurchaseOutcome.Cancelled: Screens.Toast("Purchase cancelled"); break;
                    case PurchaseOutcome.Failed: Screens.Message("PURCHASE FAILED", string.IsNullOrEmpty(msg) ? "The purchase could not be completed. You have not been charged." : msg); break;
                }
                done?.Invoke(outcome);
            });
        }

        public static void Restore()
        {
            if (!Available) { Screens.Message("STORE UNAVAILABLE", Status); return; }
            Backend.Restore((ok, msg) => Screens.Toast(ok ? "Restore complete: owned items re-applied" : "Restore failed: " + msg));
        }

        /// <summary>
        /// Called by the backend for a purchase that passed receipt verification (fresh or restored).
        /// Idempotent per transaction. The grant is written to disk before this returns; the backend must
        /// confirm the transaction with Google Play only when the result is Granted or AlreadyProcessed,
        /// so a failed save leaves the purchase unconfirmed and Google Play re-delivers it.
        /// </summary>
        public static GrantResult OnVerified(string productId, string transactionId, bool restore)
        {
            var d = SaveSystem.Data;
            var p = Store.Product(productId);
            if (p == null) { Log.W("Unknown product " + productId); return GrantResult.UnknownProduct; }
            bool granted = Store.Grant(d, productId, transactionId, restore);
            if (!SaveSystem.Save())
            {
                Log.E("IAP grant for " + productId + " could not be saved; leaving the purchase unconfirmed");
                return GrantResult.SaveFailed;
            }
            if (granted)
            {
                CloudSave.Push();
                if (!restore)
                {
                    Sfx.Buy();
                    Screens.Message("PURCHASE COMPLETE", p.Title + " has been added to your account. Thank you!");
                }
                Log.I("IAP granted " + productId + " tx=" + transactionId + (restore ? " (restore)" : ""));
            }
            Screens.RefreshTopBar();
            NotifyChanged();
            return granted ? GrantResult.Granted : GrantResult.AlreadyProcessed;
        }
    }

    /// <summary>Implemented by the Google Mobile Ads backend when the plugin is installed.</summary>
    public interface IAdsBackend
    {
        /// <summary>Initialises the SDK and starts loading a rewarded ad; called once after boot.</summary>
        void Initialize();
        bool RewardedReady { get; }
        void ShowRewarded(string placement, Action<bool> done);
    }

    /// <summary>
    /// Optional rewarded ads only (never forced, never during combat). Limits: 10 rewarded ads per
    /// day and 5 free-coin ads per day; double-reward once per stage; one revive per stage.
    /// </summary>
    public static class Ads
    {
        public static IAdsBackend Backend;
        public const int DailyLimit = 10, FreeCoinsPerDay = 5;
        static float _lastShown = -999;

        public static bool Configured => Backend != null;

        public static bool RewardedReady
        {
            get
            {
                if (Backend == null || !Backend.RewardedReady) return false;
                var d = SaveSystem.Data;
                if (d.AdDay != Clock.Today) { d.AdDay = Clock.Today; d.AdsToday = 0; }
                return d.AdsToday < DailyLimit && Time.unscaledTime - _lastShown > 20;
            }
        }

        public static int FreeCoinsLeft
        {
            get
            {
                var d = SaveSystem.Data;
                if (d.AdDay != Clock.Today) return FreeCoinsPerDay;
                return Mathf.Max(0, FreeCoinsPerDay - d.AdsToday);
            }
        }

        public static void ShowRewarded(string placement, Action<bool> done)
        {
            if (!RewardedReady) { done?.Invoke(false); return; }
            bool wasPaused = AudioListener.pause;
            AudioListener.pause = true;
            Backend.ShowRewarded(placement, ok =>
            {
                AudioListener.pause = wasPaused;
                _lastShown = Time.unscaledTime;
                if (ok)
                {
                    var d = SaveSystem.Data;
                    if (d.AdDay != Clock.Today) { d.AdDay = Clock.Today; d.AdsToday = 0; }
                    d.AdsToday++;
                    SaveSystem.MarkDirty();
                }
                done?.Invoke(ok);
            });
        }

        public static void GrantFreeCoins()
        {
            Wallet.Add(SaveSystem.Data, Currency.Coins, 500);
            SaveSystem.MarkDirty();
            Screens.Toast("+500 COINS");
        }
    }

    /// <summary>Implemented by a cloud backend (e.g. Firebase) when configured.</summary>
    public interface ICloudBackend
    {
        bool Ready { get; }
        string Status { get; }
        void Upload(string payload, Action<bool, string> done);
        void Download(Action<bool, string, string> done);   // ok, payload, error
    }

    /// <summary>
    /// Cloud save with offline play and conflict handling. The device save is always the source of truth
    /// for play; on sync the copy with more progress wins, entitlements (purchases) are unioned, and
    /// the losing copy is kept as a local backup.
    /// </summary>
    public static class CloudSave
    {
        public static ICloudBackend Backend;
        static float _lastPush = -999;

        public static bool Configured => Backend != null;
        public static string Status => Backend == null ? "Not configured in this build (saves stay on this device)" : Backend.Status;

        public static void Push()
        {
            if (Backend == null || !Backend.Ready || Time.unscaledTime - _lastPush < 20) return;
            _lastPush = Time.unscaledTime;
            Backend.Upload(SaveSystem.Encode(SaveSystem.Data), (ok, err) => { if (!ok) Log.W("Cloud upload failed: " + err); });
        }

        public static void SyncNow(Action<string> report)
        {
            if (Backend == null || !Backend.Ready) { report?.Invoke(Status); return; }
            Backend.Download((ok, payload, err) =>
            {
                if (!ok) { report?.Invoke("Sync failed: " + err); return; }
                var local = SaveSystem.Data;
                var remote = string.IsNullOrEmpty(payload) ? null : SaveSystem.Decode(payload, out _);
                if (remote != null && Score(remote) > Score(local) + 1e-3)
                {
                    // The cloud copy has more progress: take it, but keep purchases from both copies.
                    foreach (var id in local.OwnedProducts) if (!remote.OwnedProducts.Contains(id)) remote.OwnedProducts.Add(id);
                    foreach (var tx in local.ProcessedTx) if (!remote.ProcessedTx.Contains(tx)) remote.ProcessedTx.Add(tx);
                    SaveSystem.Replace(remote);
                    foreach (var id in remote.OwnedProducts) Store.Grant(SaveSystem.Data, id, null, true);
                    SaveSystem.Save();
                    report?.Invoke("Cloud progress restored");
                    Screens.Home();
                    return;
                }
                _lastPush = -999;
                Push();
                report?.Invoke("Progress uploaded");
            });
        }

        /// <summary>Progress score used to resolve conflicts (higher wins).</summary>
        public static double Score(SaveData d) => d.HighestStage * 1000.0 + d.Level * 100.0 + d.CampaignCleared * 50.0 + d.Stats.Kills * 0.01 + d.Revision * 1e-6;
    }
}
