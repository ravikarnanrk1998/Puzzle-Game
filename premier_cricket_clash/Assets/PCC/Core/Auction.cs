using System;
using System.Collections.Generic;
using System.Linq;

namespace PCC.Core
{
    /// <summary>Mega-auction rules. Money is in lakh (100 lakh = 1 crore).</summary>
    public static class AuctionRules
    {
        public const int Purse = 12000;
        public const int MaxSquad = 25;
        public const int MinSquad = 18;
        public const int MaxOverseas = 8;
        public const int MinPrice = 30;

        /// <summary>Retention slab cost of the 1st, 2nd... capped player.</summary>
        public static readonly int[] CappedSlabs = { 1800, 1400, 1100, 1800, 1400 };
        public const int UncappedSlab = 400;
        public const int MaxRetain = 6;
        public const int MaxCappedRetain = 5;
        public const int MaxUncappedRetain = 2;

        /// <summary>"₹25.20 Cr" / "₹75 L".</summary>
        public static string Money(int lakh)
        {
            if (lakh == 0) return "₹0";
            if (Math.Abs(lakh) >= 100)
            {
                double cr = lakh / 100.0;
                return "₹" + (lakh % 100 == 0 ? cr.ToString("F0") : cr.ToString("F2")) + " Cr";
            }
            return "₹" + lakh + " L";
        }

        public static int BidIncrement(int current) => current < 100 ? 5 : current < 200 ? 10 : current < 500 ? 20 : 25;

        public static int NextBid(int current) => current + BidIncrement(current);
    }

    public static class PlayerValue
    {
        /// <summary>Capped (international) player. Overseas players are all capped in this league.</summary>
        public static bool Capped(this Player p) => p.CappedFlag ?? (p.Overseas || p.Overall >= 70);

        /// <summary>What the market thinks a player is worth, in lakh (top-heavy like real auctions).</summary>
        public static int MarketValue(this Player p)
        {
            int o = p.Overall;
            double v = 30 * Math.Exp(0.13 * (o - 58)) * (1 + Math.Max(0, o - 78) * 0.04);
            v *= p.Overseas ? 0.7 : (p.Capped() ? 1.15 : 0.9);
            if (p.Role == Role.Ar) v *= 1.08;
            if (p.Role == Role.Wk) v *= 1.05;
            if (p.Role == Role.Bowl && p.IsPace) v *= 1.03;
            return Math.Max(30, (int)Math.Round(v));
        }

        /// <summary>Auction base price in lakh.</summary>
        public static int BasePrice(this Player p)
        {
            int o = p.Overall;
            if (p.Capped())
            {
                if (o >= 80) return 200;
                if (o >= 76) return 150;
                if (o >= 73) return 125;
                if (o >= 70) return 100;
                if (o >= 66) return 75;
                return 50;
            }
            if (o >= 76) return 50;
            if (o >= 70) return 40;
            return 30;
        }
    }

    public enum Acquired { Retained, Auction, Rtm, Supplementary }

    public sealed class Contract
    {
        public Contract(Player player, int price, Acquired how)
        {
            Player = player;
            Price = price;
            How = how;
        }

        public Player Player { get; }
        public int Price { get; }
        public Acquired How { get; }
    }

    public sealed class SquadMix
    {
        public int Wk, Bat, Ar, Pace, Spin, Overseas;
        public int Batters => Wk + Bat;
    }

    /// <summary>A franchise during the auction: purse, signed squad and RTM cards.</summary>
    public sealed class Franchise
    {
        public Franchise(TeamInfo info)
        {
            Info = info;
            Purse = AuctionRules.Purse;
        }

        public TeamInfo Info { get; }
        public string Id => Info.Id;
        public int Purse;
        public readonly List<Contract> Squad = new List<Contract>();
        public int RtmCards;

        public int Size => Squad.Count;
        public int OverseasCount => Squad.Count(c => c.Player.Overseas);
        public int SlotsLeft => AuctionRules.MaxSquad - Size;
        public int Spent => Squad.Sum(c => c.Price);
        public List<Player> Players => Squad.Select(c => c.Player).ToList();

        public SquadMix Mix
        {
            get
            {
                var m = new SquadMix();
                foreach (var c in Squad)
                {
                    var p = c.Player;
                    if (p.Role == Role.Wk) m.Wk++;
                    else if (p.Role == Role.Bat) m.Bat++;
                    else if (p.Role == Role.Ar) m.Ar++;
                    if (p.Bowl >= 62 && p.IsPace) m.Pace++;
                    if (p.Bowl >= 62 && p.IsSpin) m.Spin++;
                    if (p.Overseas) m.Overseas++;
                }
                return m;
            }
        }

        /// <summary>Best-XI rating (0 until the squad has 11 players).</summary>
        public int Strength => Size < 11 ? 0 : Lineup.TeamRating(Lineup.PickBestXI(Players));
    }

    public static class Retention
    {
        /// <summary>Slab cost of each retained player, in retention order.</summary>
        public static List<int> Costs(IList<Player> ordered)
        {
            var outList = new List<int>();
            int capped = 0;
            foreach (var p in ordered)
            {
                if (p.Capped())
                {
                    outList.Add(AuctionRules.CappedSlabs[Math.Min(capped, AuctionRules.CappedSlabs.Length - 1)]);
                    capped++;
                }
                else
                {
                    outList.Add(AuctionRules.UncappedSlab);
                }
            }
            return outList;
        }

        /// <summary>Rule violation for a retention list, or null when legal.</summary>
        public static string Problem(IList<Player> ordered)
        {
            int capped = ordered.Count(p => p.Capped()), uncapped = ordered.Count - capped;
            if (ordered.Count > AuctionRules.MaxRetain) return "Max " + AuctionRules.MaxRetain + " retentions";
            if (capped > AuctionRules.MaxCappedRetain) return "Max " + AuctionRules.MaxCappedRetain + " capped players";
            if (uncapped > AuctionRules.MaxUncappedRetain) return "Max " + AuctionRules.MaxUncappedRetain + " uncapped players";
            return null;
        }

        /// <summary>AI retention: keeps players worth (most of) their slab.</summary>
        public static List<Player> AiPick(IEnumerable<Player> squad)
        {
            var sorted = squad.OrderByDescending(p => p.MarketValue()).ToList();
            var keep = new List<Player>();
            int capped = 0, uncapped = 0;
            double[] thresholds = { 0.55, 0.72, 0.8, 0.95, 0.95 };
            foreach (var p in sorted)
            {
                if (keep.Count >= AuctionRules.MaxRetain) break;
                int v = p.MarketValue();
                if (p.Capped())
                {
                    if (capped >= AuctionRules.MaxCappedRetain) continue;
                    if (v >= AuctionRules.CappedSlabs[capped] * thresholds[capped])
                    {
                        keep.Add(p);
                        capped++;
                    }
                }
                else
                {
                    if (uncapped >= AuctionRules.MaxUncappedRetain) continue;
                    if (v >= AuctionRules.UncappedSlab * 0.8)
                    {
                        keep.Add(p);
                        uncapped++;
                    }
                }
            }
            return keep;
        }

        /// <summary>Applies retentions to a franchise: signs them at slab cost and grants RTM cards for the rest.</summary>
        public static void Apply(Franchise f, IList<Player> keep)
        {
            var costs = Costs(keep);
            f.Squad.Clear();
            for (int i = 0; i < keep.Count; i++) f.Squad.Add(new Contract(keep[i], costs[i], Acquired.Retained));
            f.Purse = AuctionRules.Purse - f.Spent;
            f.RtmCards = AuctionRules.MaxRetain - keep.Count;
        }
    }

    public enum LotStatus { Waiting, Open, Sold, Unsold }

    public sealed class AuctionLot
    {
        public AuctionLot(Player player, int basePrice, string setCode, string setName, bool accelerated = false)
        {
            Player = player;
            Base = basePrice;
            SetCode = setCode;
            SetName = setName;
            Accelerated = accelerated;
        }

        public Player Player { get; }
        public int Base { get; }
        public string SetCode { get; }
        public string SetName { get; }
        public bool Accelerated { get; }
        public LotStatus Status = LotStatus.Waiting;
        public string SoldTo;
        public int Price;
        public bool ViaRtm;
    }

    public readonly struct BidEvent
    {
        public BidEvent(string teamId, int amount)
        {
            TeamId = teamId;
            Amount = amount;
        }

        public string TeamId { get; }
        public int Amount { get; }
    }

    /// <summary>Right-to-Match decisions that need the user.</summary>
    public enum RtmStep { None, UserDecide, UserRaise, UserMatch }

    /// <summary>
    /// Auction sets: marquee sets first, then capped sets by speciality alternating with
    /// uncapped sets, eight players per set, shuffled within each set.
    /// </summary>
    public static class AuctionSets
    {
        static string Cat(Player p)
        {
            switch (p.Role)
            {
                case Role.Bowl: return p.IsSpin ? "SP" : "FA";
                case Role.Ar: return "AL";
                case Role.Wk: return "WK";
                default: return "BA";
            }
        }

        static readonly Dictionary<string, string> CatNames = new Dictionary<string, string>
        {
            ["BA"] = "Batters", ["AL"] = "All-rounders", ["WK"] = "Wicketkeepers", ["FA"] = "Fast Bowlers", ["SP"] = "Spinners",
        };

        public static List<AuctionLot> Build(IEnumerable<Player> pool, bool mega, Rng rng)
        {
            var lots = new List<AuctionLot>();
            var sorted = pool.OrderByDescending(p => p.Overall).ThenBy(p => p.Id).ToList();
            var used = new HashSet<int>();
            if (mega)
            {
                var marquee = sorted.Where(p => p.Capped()).Take(12).ToList();
                for (int s = 0; s * 6 < marquee.Count; s++)
                {
                    var set = marquee.Skip(s * 6).Take(6).ToList();
                    rng.Shuffle(set);
                    foreach (var p in set)
                    {
                        lots.Add(new AuctionLot(p, 200, "M" + (s + 1), "Marquee Set " + (s + 1)));
                        used.Add(p.Id);
                    }
                }
            }
            string[] order = { "BA", "AL", "WK", "FA", "SP" };
            var capped = order.ToDictionary(c => c, c => sorted.Where(p => p.Capped() && !used.Contains(p.Id) && Cat(p) == c).ToList());
            var uncapped = order.ToDictionary(c => c, c => sorted.Where(p => !p.Capped() && !used.Contains(p.Id) && Cat(p) == c).ToList());
            const int chunk = 8;
            for (int round = 0; round < 40; round++)
            {
                bool added = false;
                foreach (var (group, isCapped) in new[] { (capped, true), (uncapped, false) })
                {
                    foreach (var c in order)
                    {
                        var list = group[c];
                        int start = round * chunk;
                        if (start >= list.Count) continue;
                        var set = list.Skip(start).Take(chunk).ToList();
                        rng.Shuffle(set);
                        foreach (var p in set)
                        {
                            lots.Add(new AuctionLot(p, p.BasePrice(), (isCapped ? "" : "U") + c + (round + 1),
                                (isCapped ? "Capped " : "Uncapped ") + CatNames[c] + " " + (round + 1)));
                        }
                        added = true;
                    }
                }
                if (!added) break;
            }
            return lots;
        }
    }

    /// <summary>
    /// Live auction: AI franchises value each lot by need, purse and quality, bid in
    /// official increments, and use Right-to-Match cards on their former players.
    /// </summary>
    public sealed class AuctionEngine
    {
        public AuctionEngine(Dictionary<string, Franchise> franchises, List<AuctionLot> lots, bool mega, string userId,
            Dictionary<int, string> rtmTeamOf, int seed)
        {
            Franchises = franchises;
            Lots = lots;
            Mega = mega;
            UserId = userId;
            RtmTeamOf = rtmTeamOf;
            Rng = new Rng(seed);
        }

        public Dictionary<string, Franchise> Franchises { get; }
        public List<AuctionLot> Lots { get; }
        public bool Mega { get; }
        public string UserId { get; }

        /// <summary>Player id -> franchise holding RTM rights (his pre-auction club).</summary>
        public Dictionary<int, string> RtmTeamOf { get; }
        public Rng Rng { get; }
        public int Index;
        public bool AcceleratedAdded;

        /// <summary>When true the user's franchise bids (and decides RTMs) like an AI team.</summary>
        public bool AiManagesUser;

        public int CurrentBid;
        public string Leader;
        public readonly List<BidEvent> Bids = new List<BidEvent>();
        readonly Dictionary<string, int> _cap = new Dictionary<string, int>();
        readonly HashSet<string> _dropped = new HashSet<string>();
        public bool UserPassed;

        public RtmStep RtmStep = RtmStep.None;
        public string RtmTeam;
        public int RtmRaisedTo;

        /// <summary>Most recent hammered lot (for the SOLD / UNSOLD overlay).</summary>
        public AuctionLot LastResult;

        public Franchise User => Franchises[UserId];
        public AuctionLot Lot => Index < Lots.Count ? Lots[Index] : null;
        public bool Finished => Index >= Lots.Count;
        public int SoldCount => Lots.Count(l => l.Status == LotStatus.Sold);

        public int NextAmount => CurrentBid == 0 ? Lot.Base : AuctionRules.NextBid(CurrentBid);

        // ------------------------------------------------------------ lots

        public void OpenLot()
        {
            var l = Lot;
            if (l == null) return;
            CurrentBid = 0;
            Leader = null;
            Bids.Clear();
            _dropped.Clear();
            _cap.Clear();
            UserPassed = false;
            RtmStep = RtmStep.None;
            RtmTeam = null;
            RtmRaisedTo = 0;
            foreach (var f in Franchises.Values)
            {
                if (f.Id == UserId && !AiManagesUser) continue;
                int v = Valuation(f, l);
                if (v >= l.Base) _cap[f.Id] = v;
            }
            l.Status = LotStatus.Open;
        }

        /// <summary>Number of AI teams still interested at the next price (for UI tension).</summary>
        public int InterestedCount => _cap.Count(e => e.Value >= NextAmount && !_dropped.Contains(e.Key));

        /// <summary>Max a franchise can bid while keeping the minimum price for every mandatory squad slot.</summary>
        public int MaxAllowed(Franchise f)
        {
            int after = f.Size + 1;
            int reserve = Math.Max(0, AuctionRules.MinSquad - after) * AuctionRules.MinPrice;
            return f.Purse - reserve;
        }

        public string BidBlocker(Franchise f)
        {
            var l = Lot;
            if (l == null) return "Auction over";
            if (f.Size >= AuctionRules.MaxSquad) return "Squad full (" + AuctionRules.MaxSquad + ")";
            if (l.Player.Overseas && f.OverseasCount >= AuctionRules.MaxOverseas) return "Overseas slots full (" + AuctionRules.MaxOverseas + ")";
            if (NextAmount > MaxAllowed(f)) return "Not enough purse";
            if (Leader == f.Id) return "Highest bidder";
            return null;
        }

        public bool PlaceBid(string teamId)
        {
            var f = Franchises[teamId];
            if (BidBlocker(f) != null) return false;
            CurrentBid = NextAmount;
            Leader = teamId;
            Bids.Add(new BidEvent(teamId, CurrentBid));
            if (teamId == UserId) UserPassed = false;
            return true;
        }

        public bool UserBid() => PlaceBid(UserId);

        /// <summary>One AI decision: returns the bidding team id, or null if nobody bid.</summary>
        public string AiTick()
        {
            if (Lot == null) return null;
            int amount = NextAmount;
            var ids = new List<string>();
            var weights = new List<double>();
            foreach (var e in _cap.ToList())
            {
                string id = e.Key;
                if (id == Leader || _dropped.Contains(id)) continue;
                var f = Franchises[id];
                if (BidBlocker(f) != null || e.Value < amount)
                {
                    _dropped.Add(id);
                    continue;
                }
                bool involved = Bids.Any(b => b.TeamId == id);
                double headroom = (e.Value - amount) / (double)Math.Max(1, amount);
                ids.Add(id);
                weights.Add((involved ? 3.0 : 1.0) * (0.5 + Math.Min(2.0, headroom)));
            }
            if (ids.Count == 0) return null;
            string pick = ids[Rng.WeightedIndex(weights)];
            double closeness = amount / (double)_cap[pick];
            if (closeness > 0.85 && Rng.Chance(0.35)) return null;
            PlaceBid(pick);
            return pick;
        }

        /// <summary>True while some AI team could still bid on the current lot.</summary>
        public bool AiStillInterested
        {
            get
            {
                int amount = NextAmount;
                foreach (var e in _cap)
                {
                    if (e.Key == Leader || _dropped.Contains(e.Key)) continue;
                    if (e.Value >= amount && BidBlocker(Franchises[e.Key]) == null) return true;
                }
                return false;
            }
        }

        // -------------------------------------------------------- hammer / RTM

        bool RtmPossible(string holder, int price)
        {
            var f = Franchises[holder];
            if (f.RtmCards <= 0 || f.Size >= AuctionRules.MaxSquad) return false;
            if (Lot.Player.Overseas && f.OverseasCount >= AuctionRules.MaxOverseas) return false;
            return price <= MaxAllowed(f);
        }

        int RoundBid(int v)
        {
            int x = CurrentBid;
            while (AuctionRules.NextBid(x) <= v) x = AuctionRules.NextBid(x);
            return x;
        }

        int AiRaise(string teamId, int price)
        {
            _cap.TryGetValue(teamId, out int cap);
            if (cap <= price) return price;
            int target = price + (int)Math.Round((cap - price) * Rng.Range(0.3, 0.8));
            int limit = Math.Min(target, MaxAllowed(Franchises[teamId]));
            return Math.Max(price, RoundBid(limit));
        }

        /// <summary>Ends bidding on the lot. Returns true if the user must make an RTM decision.</summary>
        public bool Hammer(bool autoUser = false)
        {
            var l = Lot;
            if (Leader == null)
            {
                l.Status = LotStatus.Unsold;
                LastResult = l;
                return false;
            }
            RtmTeamOf.TryGetValue(l.Player.Id, out string holder);
            bool userIsAi = AiManagesUser;
            if (Mega && holder != null && holder != Leader && RtmPossible(holder, CurrentBid))
            {
                if (holder == UserId && !userIsAi)
                {
                    if (autoUser)
                    {
                        Sell(Leader, CurrentBid);
                        return false;
                    }
                    RtmTeam = holder;
                    RtmStep = RtmStep.UserDecide;
                    return true;
                }
                var hf = Franchises[holder];
                double v = Valuation(hf, l, false) * Rng.Range(0.9, 1.15);
                if (v >= CurrentBid * 0.98)
                {
                    RtmTeam = holder;
                    if (Leader == UserId && !userIsAi)
                    {
                        if (autoUser)
                        {
                            Sell(holder, CurrentBid, true);
                            return false;
                        }
                        RtmStep = RtmStep.UserRaise;
                        return true;
                    }
                    int raised = AiRaise(Leader, CurrentBid);
                    if (raised > CurrentBid)
                    {
                        if (v >= raised && RtmPossible(holder, raised)) Sell(holder, raised, true);
                        else Sell(Leader, raised);
                    }
                    else
                    {
                        Sell(holder, CurrentBid, true);
                    }
                    return false;
                }
            }
            Sell(Leader, CurrentBid);
            return false;
        }

        /// <summary>User holds the RTM card: use it at the current price or not.</summary>
        public void UserUseRtm(bool use)
        {
            if (!use)
            {
                Sell(Leader, CurrentBid);
                return;
            }
            int raised = AiRaise(Leader, CurrentBid);
            if (raised > CurrentBid)
            {
                RtmRaisedTo = raised;
                RtmStep = RtmStep.UserMatch;
            }
            else
            {
                Sell(UserId, CurrentBid, true);
            }
        }

        /// <summary>After the highest bidder raised, the user (RTM holder) matches or lets him go.</summary>
        public void UserMatchRaise(bool match)
        {
            if (match && RtmPossible(UserId, RtmRaisedTo)) Sell(UserId, RtmRaisedTo, true);
            else Sell(Leader, RtmRaisedTo);
        }

        /// <summary>User was the highest bidder and an AI used RTM: the user's final raise.</summary>
        public void UserFinalRaise(int newPrice)
        {
            int price = Math.Min(Math.Max(newPrice, CurrentBid), MaxAllowed(User));
            var hf = Franchises[RtmTeam];
            double v = Valuation(hf, Lot, false) * Rng.Range(0.95, 1.2);
            if (v >= price && RtmPossible(RtmTeam, price)) Sell(RtmTeam, price, true);
            else Sell(UserId, price);
        }

        void Sell(string teamId, int price, bool rtm = false)
        {
            var l = Lot;
            var f = Franchises[teamId];
            f.Purse -= price;
            f.Squad.Add(new Contract(l.Player, price, rtm ? Acquired.Rtm : Acquired.Auction));
            if (rtm) f.RtmCards = Math.Max(0, f.RtmCards - 1);
            l.Status = LotStatus.Sold;
            l.SoldTo = teamId;
            l.Price = price;
            l.ViaRtm = rtm;
            Leader = teamId;
            CurrentBid = price;
            RtmStep = RtmStep.None;
            LastResult = l;
        }

        /// <summary>Moves to the next lot, adding the accelerated round of unsold players once.</summary>
        public void Advance()
        {
            Index++;
            if (Index < Lots.Count || AcceleratedAdded) return;
            AcceleratedAdded = true;
            bool needs = Franchises.Values.Any(f => f.Size < AuctionRules.MinSquad + 2 && f.SlotsLeft > 0);
            if (!needs) return;
            var unsold = Lots.Where(l => l.Status == LotStatus.Unsold).OrderByDescending(l => l.Player.Overall).Take(60).ToList();
            foreach (var l in unsold) Lots.Add(new AuctionLot(l.Player, l.Base, "ACC", "Accelerated Round", true));
        }

        /// <summary>Resolves the current lot without the user bidding (skip / sim).</summary>
        public void ResolveInstantly()
        {
            if (Lot == null) return;
            if (Lot.Status != LotStatus.Open) OpenLot();
            if (AiManagesUser && !_cap.ContainsKey(UserId))
            {
                int v = Valuation(User, Lot);
                if (v >= Lot.Base) _cap[UserId] = v;
            }
            int idle = 0, guard = 0;
            while (idle < 3 && guard++ < 400)
            {
                if (AiTick() == null) idle++;
                else idle = 0;
            }
            Hammer(true);
        }

        /// <summary>Signs the best affordable unsold players for any team below the minimum squad.</summary>
        public List<string> FillSquads()
        {
            var notes = new List<string>();
            var taken = new HashSet<int>(Franchises.Values.SelectMany(f => f.Squad.Select(c => c.Player.Id)));
            var seen = new HashSet<int>();
            var available = Lots.Select(l => l.Player).Where(p => !taken.Contains(p.Id) && seen.Add(p.Id))
                .OrderByDescending(p => p.Overall).ToList();
            foreach (var f in Franchises.Values)
            {
                while (f.Size < AuctionRules.MinSquad)
                {
                    int idx = available.FindIndex(p => p.BasePrice() <= f.Purse && (!p.Overseas || f.OverseasCount < AuctionRules.MaxOverseas));
                    if (idx < 0) break;
                    var p = available[idx];
                    available.RemoveAt(idx);
                    int price = Math.Min(p.BasePrice(), f.Purse);
                    f.Purse -= price;
                    f.Squad.Add(new Contract(p, price, Acquired.Supplementary));
                    if (f.Id == UserId) notes.Add(p.Name + " signed at base price " + AuctionRules.Money(price));
                }
            }
            return notes;
        }

        // -------------------------------------------------------- AI brain

        static double NeedFactor(Franchise f, Player p)
        {
            var m = f.Mix;
            switch (p.Role)
            {
                case Role.Wk: return m.Wk == 0 ? 1.45 : (m.Wk == 1 ? 1.08 : 0.7);
                case Role.Bat: return m.Batters < 4 ? 1.25 : (m.Batters < 7 ? 1.0 : 0.72);
                case Role.Ar: return m.Ar < 3 ? 1.25 : (m.Ar < 5 ? 1.0 : 0.75);
                default:
                    if (p.IsSpin) return m.Spin < 2 ? 1.35 : (m.Spin < 4 ? 1.0 : 0.7);
                    return m.Pace < 4 ? 1.3 : (m.Pace < 7 ? 1.0 : 0.72);
            }
        }

        int Valuation(Franchise f, AuctionLot l, bool noise = true)
        {
            var p = l.Player;
            if (f.Size >= AuctionRules.MaxSquad) return 0;
            if (p.Overseas && f.OverseasCount >= AuctionRules.MaxOverseas) return 0;
            double v = p.MarketValue();
            v *= NeedFactor(f, p);
            if (p.Overseas && AuctionRules.MaxOverseas - f.OverseasCount <= 2 && p.Overall < 82) v *= 0.7;
            int slotsToTarget = Math.Max(1, 22 - f.Size);
            double perSlot = f.Purse / (double)slotsToTarget;
            v *= Math.Clamp(perSlot / 420, 0.5, 1.25);
            if (f.Size >= 23 && p.Overall < 84) v *= 0.6;
            if (noise) v *= Rng.Range(0.75, 1.25);
            if (RtmTeamOf.TryGetValue(p.Id, out string former) && former == f.Id) v *= 1.1;
            if (p.Overall >= 88) v *= 1.15;
            // Captaincy scramble: teams without an Indian star chase one hard.
            if (!p.Overseas && p.Capped() && p.Overall >= 84 && !f.Squad.Any(c => !c.Player.Overseas && c.Player.Overall >= 84)) v *= 1.3;
            if (l.Accelerated) v *= 0.85;
            if (noise)
            {
                double disinterest = p.Overall >= 86 ? 0.12 : (p.Overall >= 76 ? 0.3 : 0.45);
                if (Rng.Chance(disinterest)) return 0;
            }
            return (int)Math.Round(Math.Min(v, MaxAllowed(f)));
        }
    }

    /// <summary>Compact text form of a finished auction (for saving): "USER|ID:purse:pid,price,how;...|...".</summary>
    public static class AuctionResult
    {
        public static string Serialize(string userId, IEnumerable<Franchise> franchises)
        {
            var parts = new List<string> { userId };
            foreach (var f in franchises)
            {
                parts.Add(f.Id + ":" + f.Purse + ":" + string.Join(";", f.Squad.Select(c => c.Player.Id + "," + c.Price + "," + (int)c.How)));
            }
            return string.Join("|", parts);
        }

        /// <summary>Parses a saved auction, or returns null when the text is missing or invalid.</summary>
        public static (string UserId, Dictionary<string, Franchise> Teams)? Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            try
            {
                var parts = text.Split('|');
                var teams = new Dictionary<string, Franchise>();
                var db = PlayerDb.Instance;
                for (int i = 1; i < parts.Length; i++)
                {
                    var bits = parts[i].Split(':');
                    var f = new Franchise(Teams.ById(bits[0])) { Purse = int.Parse(bits[1]) };
                    foreach (var c in bits[2].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var v = c.Split(',');
                        f.Squad.Add(new Contract(db.ById(int.Parse(v[0])), int.Parse(v[1]), (Acquired)int.Parse(v[2])));
                    }
                    teams[f.Id] = f;
                }
                if (teams.Count != Teams.All.Count || !teams.ContainsKey(parts[0])) return null;
                return (parts[0], teams);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Saves an auction in progress so it can be resumed later. A lot that was mid-bidding is
    /// re-opened from its base price on resume.
    /// </summary>
    public static class AuctionProgress
    {
        const char F = '\t';

        public static string Serialize(AuctionEngine a)
        {
            var lines = new List<string>
            {
                "PCCA1" + F + a.UserId + F + (a.Mega ? 1 : 0) + F + a.Index + F + (a.AcceleratedAdded ? 1 : 0),
            };
            foreach (var f in a.Franchises.Values)
            {
                lines.Add("F" + F + f.Id + F + f.Purse + F + f.RtmCards + F +
                          string.Join(";", f.Squad.Select(c => c.Player.Id + "," + c.Price + "," + (int)c.How)));
            }
            foreach (var l in a.Lots)
            {
                lines.Add("L" + F + l.Player.Id + F + l.Base + F + l.SetCode + F + l.SetName + F + (l.Accelerated ? 1 : 0) + F +
                          (int)l.Status + F + (l.SoldTo ?? "") + F + l.Price + F + (l.ViaRtm ? 1 : 0));
            }
            lines.Add("R" + F + string.Join(";", a.RtmTeamOf.Select(e => e.Key + "," + e.Value)));
            return string.Join("\n", lines);
        }

        /// <summary>Rebuilds a saved auction (new random stream), or null when the text is missing or invalid.</summary>
        public static AuctionEngine Deserialize(string text, int seed)
        {
            if (string.IsNullOrEmpty(text)) return null;
            try
            {
                var db = PlayerDb.Instance;
                var lines = text.Split('\n');
                var head = lines[0].Split(F);
                if (head[0] != "PCCA1") return null;
                var franchises = new Dictionary<string, Franchise>();
                var lots = new List<AuctionLot>();
                var rtm = new Dictionary<int, string>();
                for (int i = 1; i < lines.Length; i++)
                {
                    var v = lines[i].Split(F);
                    switch (v[0])
                    {
                        case "F":
                            var f = new Franchise(Teams.ById(v[1])) { Purse = int.Parse(v[2]), RtmCards = int.Parse(v[3]) };
                            foreach (var c in v[4].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                var p = c.Split(',');
                                f.Squad.Add(new Contract(db.ById(int.Parse(p[0])), int.Parse(p[1]), (Acquired)int.Parse(p[2])));
                            }
                            franchises[f.Id] = f;
                            break;
                        case "L":
                            var lot = new AuctionLot(db.ById(int.Parse(v[1])), int.Parse(v[2]), v[3], v[4], v[5] == "1")
                            {
                                Status = (LotStatus)int.Parse(v[6]),
                                SoldTo = v[7].Length == 0 ? null : v[7],
                                Price = int.Parse(v[8]),
                                ViaRtm = v[9] == "1",
                            };
                            if (lot.Status == LotStatus.Open) lot.Status = LotStatus.Waiting;
                            lots.Add(lot);
                            break;
                        case "R":
                            foreach (var e in v[1].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                var p = e.Split(',');
                                rtm[int.Parse(p[0])] = p[1];
                            }
                            break;
                    }
                }
                if (franchises.Count != Teams.All.Count || !franchises.ContainsKey(head[1])) return null;
                return new AuctionEngine(franchises, lots, head[2] == "1", head[1], rtm, seed)
                {
                    Index = int.Parse(head[3]),
                    AcceleratedAdded = head[4] == "1",
                };
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>Sets up a full mega auction: retentions for every team, the lot list and RTM rights.</summary>
    public static class MegaAuction
    {
        public static AuctionEngine Create(string userId, IList<Player> userRetained, int seed)
        {
            var db = PlayerDb.Instance;
            var franchises = new Dictionary<string, Franchise>();
            var formerTeam = new Dictionary<int, string>();
            foreach (var t in Teams.All)
            {
                var f = new Franchise(t);
                var squad = db.Squad(t.Id);
                foreach (var p in squad) formerTeam[p.Id] = t.Id;
                Retention.Apply(f, t.Id == userId ? userRetained : Retention.AiPick(squad));
                franchises[t.Id] = f;
            }
            var retained = new HashSet<int>(franchises.Values.SelectMany(f => f.Squad.Select(c => c.Player.Id)));
            var pool = db.All.Concat(db.FreeAgents).Where(p => !retained.Contains(p.Id)).ToList();
            var lots = AuctionSets.Build(pool, true, new Rng(seed));
            var rtm = pool.Where(p => formerTeam.ContainsKey(p.Id)).ToDictionary(p => p.Id, p => formerTeam[p.Id]);
            return new AuctionEngine(franchises, lots, true, userId, rtm, seed * 7 + 1);
        }
    }
}
