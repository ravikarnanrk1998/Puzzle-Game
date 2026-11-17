#if UA_IAP
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UA.Core;
using UA.Game;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;
using Catalog = UA.Core.Store;

namespace UA.Billing
{
    /// <summary>
    /// Google Play Billing through Unity IAP 5 (StoreController, two-step pending -> confirm flow).
    ///
    /// Purchase contract:
    ///  1. OnPurchasePending: the Google Play receipt is signature-checked with the app's Play license key
    ///     (CrossPlatformValidator), its product id / package / purchase state are checked, and, when a
    ///     verification server is configured, the purchase token is checked server-side with Google.
    ///  2. Only a verified purchase reaches Iap.OnVerified, which grants the items and writes the save.
    ///  3. ConfirmPurchase (consume / acknowledge) is called only after that save succeeded. Anything that
    ///     fails before this point leaves the purchase unconfirmed: Google Play re-delivers it on the next
    ///     launch, and refunds it automatically if it is never acknowledged.
    /// Deferred (e.g. cash) payments, cancellations and failures grant nothing.
    /// </summary>
    public sealed class GooglePlayBilling : IIapBackend
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            // Google Play only (plus Unity's test store inside the editor). Desktop players never get a backend,
            // so Unity's fake store can never hand out paid items in a non-Android build.
            if (Application.platform != RuntimePlatform.Android && !Application.isEditor) return;
            Iap.Backend = new GooglePlayBilling();
        }

        enum Verdict { Valid, Pending, Retry, Invalid }

        [Serializable] sealed class VerifyRequest { public string packageName, productId, purchaseToken, orderId; public bool consumable; }
        [Serializable] sealed class VerifyResponse { public bool valid, pending; public string reason; }

        StoreController _store;
        CrossPlatformValidator _validator;
        bool _connecting, _connected, _productsReady;
        string _status = "Connecting to Google Play...";
        readonly Dictionary<string, Action<PurchaseOutcome, string>> _callbacks = new Dictionary<string, Action<PurchaseOutcome, string>>();
        readonly HashSet<string> _verifying = new HashSet<string>();

        public bool Ready => _connected && _productsReady && (_validator != null || Application.isEditor);
        public string Status => _status;

        // ------------------------------------------------------------------ connection

        public async void Connect()
        {
            if (_connecting || _connected) return;
            _connecting = true;
            if (!Application.isEditor)
            {
                var key = LicenseKey(out string source);
                if (key == null)
                {
                    _status = "In-app purchases are disabled in this build: the Google Play license key is not configured.";
                    Log.W("IAP: " + _status);
                    _connecting = false;
                    return;
                }
                try
                {
                    _validator = new CrossPlatformValidator(key, Application.identifier);
                    Log.I("IAP: receipt validation key from " + source);
                }
                catch (Exception e)
                {
                    _status = "In-app purchases are disabled: the Google Play license key is invalid.";
                    Log.E("IAP validator: " + e.Message);
                    _connecting = false;
                    return;
                }
            }

            _store = UnityIAPServices.StoreController();
            // Subscribe to everything before Connect: unconfirmed purchases from an earlier session can arrive immediately.
            _store.OnStoreConnected += OnConnected;
            _store.OnStoreDisconnected += OnDisconnected;
            _store.OnProductsFetched += OnProductsFetched;
            _store.OnProductsFetchFailed += OnProductsFetchFailed;
            _store.OnPurchasePending += OnPurchasePending;
            _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
            _store.OnPurchaseFailed += OnPurchaseFailed;
            _store.OnPurchaseDeferred += OnPurchaseDeferred;
            _store.OnPurchasesFetched += OnPurchasesFetched;
            _store.OnPurchasesFetchFailed += f => Log.W("IAP: purchases fetch failed: " + f.FailureReason + " " + f.Message);
            try
            {
                await _store.Connect();
            }
            catch (Exception e)
            {
                _status = "Google Play Billing is unavailable on this device.";
                Log.W("IAP connect: " + e.Message);
            }
            _connecting = false;
        }

        void OnConnected()
        {
            _connected = true;
            _status = "Loading store...";
            Log.I("IAP: store connected");
            var defs = new List<ProductDefinition>();
            foreach (var p in Catalog.Products)
                defs.Add(new ProductDefinition(p.Id, p.Kind == ProductKind.Consumable ? ProductType.Consumable : ProductType.NonConsumable));
            _store.FetchProducts(defs);
        }

        void OnDisconnected(StoreConnectionFailureDescription f)
        {
            _connected = false;
            _status = "Google Play is not available right now. Check that you are signed in to the Play Store and online.";
            Log.W("IAP: store disconnected: " + f.Message);
            Iap.NotifyChanged();
        }

        void OnProductsFetched(List<Product> products)
        {
            _productsReady = true;
            _status = "Google Play Billing connected";
            var available = new HashSet<string>(products.Where(p => p != null && p.availableToPurchase).Select(p => p.definition.id));
            var missing = Catalog.Products.Where(p => !available.Contains(p.Id)).Select(p => p.Id).ToArray();
            Log.I("IAP: " + available.Count + " products available" + (missing.Length > 0 ? ", not set up in Play Console: " + string.Join(", ", missing) : ""));
            // Re-apply owned non-consumables and re-deliver any purchase that was paid but not yet confirmed.
            _store.FetchPurchases();
            Iap.NotifyChanged();
        }

        void OnProductsFetchFailed(ProductFetchFailed f)
        {
            Log.W("IAP: product fetch failed: " + f.FailureReason + " (" + (f.FailedFetchProducts?.Count ?? 0) + " products)");
            if (_store.GetProducts().Any(p => p.availableToPurchase))
            {
                _productsReady = true;
                _status = "Google Play Billing connected";
            }
            else _status = "The store could not be loaded from Google Play. Please try again later.";
            Iap.NotifyChanged();
        }

        // ------------------------------------------------------------------ IIapBackend

        public string Price(string productId)
        {
            var p = _store?.GetProductById(productId);
            return p != null && p.availableToPurchase ? p.metadata?.localizedPriceString : null;
        }

        public void Buy(string productId, Action<PurchaseOutcome, string> done)
        {
            var product = _store?.GetProductById(productId);
            if (product == null || !product.availableToPurchase)
            {
                done?.Invoke(PurchaseOutcome.Failed, "This item is not available on Google Play right now.");
                return;
            }
            if (_callbacks.ContainsKey(productId))
            {
                done?.Invoke(PurchaseOutcome.Failed, "A purchase of this item is already in progress.");
                return;
            }
            _callbacks[productId] = done;
            Log.I("IAP: purchase started " + productId);
            _store.PurchaseProduct(product);
        }

        public void Restore(Action<bool, string> done)
        {
            if (_store == null || !_connected) { done?.Invoke(false, "Google Play is not connected"); return; }
            _store.RestoreTransactions((ok, err) =>
            {
                Log.I("IAP: restore " + (ok ? "ok" : "failed: " + err));
                if (ok) _store.FetchPurchases();
                done?.Invoke(ok, err ?? "");
            });
        }

        // ------------------------------------------------------------------ purchase events

        void OnPurchasePending(PendingOrder order)
        {
            string id = ProductId(order);
            if (id == null) { Log.W("IAP: pending order without a product"); return; }
            string key = id + "|" + order.Info?.TransactionID;
            if (!_verifying.Add(key)) return;   // already being verified (duplicate delivery)
            Verify(order, id, (verdict, txKey, msg) =>
            {
                _verifying.Remove(key);
                switch (verdict)
                {
                    case Verdict.Valid:
                    {
                        var r = Iap.OnVerified(id, txKey, false);
                        if (r == GrantResult.Granted || r == GrantResult.AlreadyProcessed)
                        {
                            _store.ConfirmPurchase(order);
                            Finish(id, PurchaseOutcome.Success, null);
                        }
                        else if (r == GrantResult.SaveFailed)
                            Finish(id, PurchaseOutcome.Failed, "Your purchase could not be saved on this device. It will be delivered automatically the next time the game starts.");
                        else
                            Finish(id, PurchaseOutcome.Failed, "This item is not recognised by this version of the game. Please update the game; your purchase will then be delivered.");
                        break;
                    }
                    case Verdict.Pending:
                        Finish(id, PurchaseOutcome.Pending, msg);
                        break;
                    case Verdict.Retry:
                        Finish(id, PurchaseOutcome.Failed, msg);
                        break;
                    default:
                        Log.W("IAP: receipt rejected for " + id + ": " + msg);
                        Finish(id, PurchaseOutcome.Failed, "The purchase could not be verified, so nothing was added. Google Play automatically refunds purchases that are not completed.");
                        break;
                }
            });
        }

        void OnPurchaseConfirmed(Order order)
        {
            switch (order)
            {
                case ConfirmedOrder c: Log.I("IAP: confirmed " + ProductId(c)); break;
                // The items are already granted and saved; Google Play re-delivers the order and it is confirmed again.
                case FailedOrder f: Log.W("IAP: confirmation failed for " + ProductId(f) + ": " + f.FailureReason + " " + f.Details); break;
            }
        }

        void OnPurchaseFailed(FailedOrder f)
        {
            string id = ProductId(f);
            Log.I("IAP: purchase failed " + id + ": " + f.FailureReason + " " + f.Details);
            switch (f.FailureReason)
            {
                case PurchaseFailureReason.UserCancelled:
                case PurchaseFailureReason.OrderCancelled:
                    Finish(id, PurchaseOutcome.Cancelled, null);
                    break;
                case PurchaseFailureReason.ExistingPurchasePending:
                    Finish(id, PurchaseOutcome.Pending, "A previous payment for this item is still being processed by Google Play.");
                    break;
                case PurchaseFailureReason.DuplicateTransaction:
                    // Already owned on this Google account: re-apply it instead of charging again.
                    _store.FetchPurchases();
                    Finish(id, PurchaseOutcome.Failed, "You already own this item. It has been restored to your account.");
                    break;
                case PurchaseFailureReason.ProductUnavailable:
                    Finish(id, PurchaseOutcome.Failed, "This item is not available on Google Play right now.");
                    break;
                case PurchaseFailureReason.StoreNotConnected:
                case PurchaseFailureReason.PurchasingUnavailable:
                    Finish(id, PurchaseOutcome.Failed, "Google Play purchasing is not available. Check that you are signed in to the Play Store and online.");
                    break;
                default:
                    Finish(id, PurchaseOutcome.Failed, null);
                    break;
            }
        }

        void OnPurchaseDeferred(DeferredOrder d)
        {
            string id = ProductId(d);
            Log.I("IAP: purchase deferred " + id);
            Finish(id, PurchaseOutcome.Pending, null);
        }

        void OnPurchasesFetched(Orders orders)
        {
            // Pending orders are re-delivered through OnPurchasePending by Unity IAP. Confirmed non-consumables
            // are re-applied here (reinstall, new device, restore) without any currency.
            int restored = 0;
            foreach (var c in orders.ConfirmedOrders)
            {
                string id = ProductId(c);
                var def = id != null ? Catalog.Product(id) : null;
                if (def == null || def.Kind != ProductKind.NonConsumable) continue;
                Verify(c, id, (verdict, txKey, msg) =>
                {
                    if (verdict != Verdict.Valid) { Log.W("IAP: owned item " + id + " not re-applied: " + verdict + " " + msg); return; }
                    if (Iap.OnVerified(id, txKey, true) == GrantResult.Granted) restored++;
                });
            }
            Log.I("IAP: purchases fetched: " + orders.ConfirmedOrders.Count + " owned, " + orders.PendingOrders.Count + " pending, " + orders.DeferredOrders.Count + " deferred" + (restored > 0 ? ", re-applied " + restored : ""));
        }

        void Finish(string id, PurchaseOutcome outcome, string msg)
        {
            if (id != null && _callbacks.TryGetValue(id, out var cb))
            {
                _callbacks.Remove(id);
                cb?.Invoke(outcome, msg);
            }
            else if (outcome == PurchaseOutcome.Pending && !string.IsNullOrEmpty(msg)) Screens.Toast(msg);
            Iap.NotifyChanged();
        }

        static string ProductId(Order o)
        {
            try { return o?.CartOrdered?.Items()?.FirstOrDefault()?.Product?.definition?.id; }
            catch { return null; }
        }

        // ------------------------------------------------------------------ verification

        void Verify(Order order, string id, Action<Verdict, string, string> done)
        {
#if UNITY_EDITOR
            // Unity's editor test store has no signed receipts. This branch is compiled only into the editor.
            done(Verdict.Valid, "editor:" + (string.IsNullOrEmpty(order.Info?.TransactionID) ? Guid.NewGuid().ToString("N") : order.Info.TransactionID), null);
#else
            GooglePlayReceipt g;
            try
            {
                string receipt = order.Info?.Receipt;
                if (string.IsNullOrEmpty(receipt)) { done(Verdict.Invalid, null, "empty receipt"); return; }
                g = _validator.Validate(receipt).OfType<GooglePlayReceipt>().FirstOrDefault(r => r.productID == id);
            }
            catch (IAPSecurityException e) { done(Verdict.Invalid, null, "signature check failed: " + e.Message); return; }
            catch (Exception e) { done(Verdict.Invalid, null, e.GetType().Name + ": " + e.Message); return; }
            if (g == null) { done(Verdict.Invalid, null, "receipt has no Google Play purchase of " + id); return; }
            if (g.packageName != Application.identifier) { done(Verdict.Invalid, null, "receipt is for another app: " + g.packageName); return; }
            if (g.purchaseState == GooglePurchaseState.Deferred) { done(Verdict.Pending, null, "Your payment is still being processed by Google Play. The items will be added when it completes."); return; }
            if (g.purchaseState != GooglePurchaseState.Purchased) { done(Verdict.Invalid, null, "purchase state " + g.purchaseState); return; }
            string tx = "gp:" + ShortHash(g.purchaseToken);
            string url = ServicesConfig.Current.purchaseVerifyUrl;
            if (string.IsNullOrWhiteSpace(url)) { done(Verdict.Valid, tx, null); return; }
            var def = Catalog.Product(id);
            ServerVerify(url.Trim(), id, g, def != null && def.Kind == ProductKind.Consumable, tx, done);
#endif
        }

        /// <summary>
        /// Optional server check (see docs/SERVICES_SETUP.md): POSTs the purchase token to an HTTPS endpoint that
        /// calls the Google Play Developer API and answers {"valid":true} / {"valid":false,"reason":"..."} / {"pending":true}.
        /// A network or server error leaves the purchase unconfirmed so it is retried on the next launch.
        /// </summary>
        static void ServerVerify(string url, string productId, GooglePlayReceipt g, bool consumable, string tx, Action<Verdict, string, string> done)
        {
            var body = JsonUtility.ToJson(new VerifyRequest { packageName = g.packageName, productId = productId, purchaseToken = g.purchaseToken, orderId = g.orderID, consumable = consumable });
            var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 20,
            };
            req.SetRequestHeader("Content-Type", "application/json");
            req.SendWebRequest().completed += _ =>
            {
                try
                {
                    bool reachable = req.result == UnityWebRequest.Result.Success || (req.result == UnityWebRequest.Result.ProtocolError && req.responseCode < 500);
                    if (!reachable)
                    {
                        Log.W("IAP: verification server unreachable: " + req.error);
                        done(Verdict.Retry, null, "The purchase could not be verified right now. Nothing was charged twice: it will be delivered automatically when you are back online.");
                        return;
                    }
                    VerifyResponse resp = null;
                    try { resp = JsonUtility.FromJson<VerifyResponse>(req.downloadHandler.text); } catch { }
                    if (resp != null && resp.valid) done(Verdict.Valid, tx, null);
                    else if (resp != null && resp.pending) done(Verdict.Pending, null, "Your payment is still being processed by Google Play.");
                    else done(Verdict.Invalid, null, "server rejected the purchase: " + (resp?.reason ?? ("HTTP " + req.responseCode)));
                }
                finally { req.Dispose(); }
            };
        }

        static string ShortHash(string s)
        {
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes(s ?? ""));
                var sb = new StringBuilder(32);
                for (int i = 0; i < 16; i++) sb.Append(h[i].ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>
        /// The Play license (public) key: from the Receipt Validation Obfuscator's GooglePlayTangle when it was
        /// generated, otherwise from UAServices.json. Returns null when neither is configured.
        /// </summary>
        static byte[] LicenseKey(out string source)
        {
            source = null;
            try
            {
                var t = Type.GetType("UnityEngine.Purchasing.Security.GooglePlayTangle, Assembly-CSharp");
                if (t != null)
                {
                    var populated = t.GetField("IsPopulated", BindingFlags.Public | BindingFlags.Static);
                    var data = t.GetMethod("Data", BindingFlags.Public | BindingFlags.Static);
                    if (populated != null && (bool)populated.GetValue(null) && data != null && data.Invoke(null, null) is byte[] b && b.Length > 0)
                    {
                        source = "GooglePlayTangle";
                        return b;
                    }
                }
            }
            catch (Exception e) { Log.W("IAP: GooglePlayTangle lookup failed: " + e.Message); }
            var cfg = ServicesConfig.Current;
            if (cfg.HasLicenseKey)
            {
                try
                {
                    source = "UAServices.json";
                    return Convert.FromBase64String(cfg.googlePlayLicenseKey.Trim());
                }
                catch (FormatException) { Log.E("IAP: googlePlayLicenseKey in UAServices.json is not valid base64"); }
            }
            return null;
        }
    }
}
#endif
