using System.Collections.Generic;
using UA.Core;
using UnityEngine;

namespace UA.Game
{
    /// <summary>Everything the result screen needs after a stage.</summary>
    public sealed class StageResult
    {
        public StagePlan Plan;
        public bool Cleared;
        public int Kills, Headshots, Stars;
        public float Accuracy, Time;
        public StageReward Reward;
        public long DailyCoins, DailyGems, RushCoins, RushGems, CampaignCoins, CampaignGems;
        public int LevelUps;
        public bool Doubled;
    }

    /// <summary>
    /// Runs one stage: spawns the player, hazards, loot, objective props, the Director and the HUD;
    /// drives the objective, events and rewards; ends with a <see cref="StageResult"/>.
    /// </summary>
    public sealed class StageRunner : MonoBehaviour
    {
        public static StageRunner Instance;

        public StagePlan Plan;
        public Level Level;
        public Director Director;
        public PlayerRig Player;
        public Hud Hud;
        public int Kills, Headshots, EliteKills, BossKills, ExplosiveKills, Rescued, NestsDestroyed, SuppliesFound;
        public readonly long[] KillsByType = new long[9];
        public readonly long[] KillsByClass = new long[6];
        public long CoinsEarned;
        public float Elapsed, ObjTimer;
        public bool Ended, Paused, Revived;
        public string ObjectiveLine = "", ObjectiveSub = "";
        public Vector3? MarkerTarget;
        public Icon MarkerIcon = Icon.Pin;
        public ObjectiveThing HoldTarget;
        public StageResult Result;
        public int Round = 1;   // boss rush round

        readonly List<ObjectiveThing> _things = new List<ObjectiveThing>();
        ObjectiveThing _flare;
        Boss _boss;
        float _bossSpawnT = -1, _lightningT, _supplyT = -1, _endT;
        bool _won;
        Transform _dynamic;

        public static StageRunner Begin(StagePlan plan, Level level, int bossRushRound = 1)
        {
            var go = new GameObject("StageRunner");
            go.transform.SetParent(level.Root.transform, false);
            var s = go.AddComponent<StageRunner>();
            Instance = s;
            s.Plan = plan;
            s.Level = level;
            s.Round = bossRushRound;
            s.Setup();
            return s;
        }

        void OnDestroy()
        {
            Zombie.Killed -= OnKill;
            Boss.Defeated -= OnBossDown;
            if (Instance == this) Instance = null;
            if (Hud != null) Destroy(Hud.gameObject);
        }

        void Setup()
        {
            _dynamic = X.Child(Level.Root.transform, "Dynamic").transform;
            Hazards.Root = _dynamic;
            var r = new Rng(Rng.Hash((long)Plan.Seed, 2024));
            Player = PlayerRig.Create(Level.Root.transform, Level.Start, Level.StartYaw);
            Player.SetFlashlight(Level.Atmosphere.Darkness >= 0.45f);
            Player.Guns.OnHit = (kill, head) => Hud?.OnHit(kill, head);
            // Hazards and loot.
            foreach (var h in Plan.Hazards) Hazards.Spawn((HazardKind)h.Kind, Level.OnNav(Level.SpotPos(h), 2), r);
            foreach (var l in Plan.Loot) Pickup.Create((LootKind)l.Kind, Level.OnNav(Level.SpotPos(l), 2) + Vector3.up * 0.02f, _dynamic, false, l.Kind == (int)LootKind.Coins ? (int)(80 * Plan.RewardMul) : 0);
            // Objective props.
            switch (Plan.Objective)
            {
                case ObjectiveType.Rescue:
                    foreach (var s in Plan.ObjectiveSpots) _things.Add(ObjectiveThing.Create(ObjKind.Survivor, Level.OnNav(Level.SpotPos(s), 2), _dynamic, Plan));
                    break;
                case ObjectiveType.Nests:
                    foreach (var s in Plan.ObjectiveSpots) _things.Add(ObjectiveThing.Create(ObjKind.Nest, Level.OnNav(Level.SpotPos(s), 2), _dynamic, Plan));
                    break;
                case ObjectiveType.Supplies:
                    foreach (var s in Plan.ObjectiveSpots) Pickup.Create(LootKind.Health, Level.OnNav(Level.SpotPos(s), 2) + Vector3.up * 0.02f, _dynamic, true);
                    break;
                case ObjectiveType.Defend:
                    _things.Add(ObjectiveThing.Create(ObjKind.Beacon, Level.OnNav(Level.SpotPos(Plan.ObjectiveSpots[0]), 2), _dynamic, Plan));
                    ObjTimer = Plan.ObjectiveTime;
                    break;
                case ObjectiveType.Extract:
                    _flare = ObjectiveThing.Create(ObjKind.Flare, Level.OnNav(Level.SpotPos(Plan.ObjectiveSpots[0]), 2), _dynamic, Plan);
                    _things.Add(_flare);
                    break;
                case ObjectiveType.Survive:
                    ObjTimer = Plan.ObjectiveTime;
                    break;
                case ObjectiveType.Boss:
                    _bossSpawnT = 3.5f;
                    break;
            }
            Director = Director.Create(Plan, Level);
            Director.WaveStarted += w =>
            {
                if (Plan.Objective == ObjectiveType.Eliminate || Plan.Objective == ObjectiveType.Extract)
                {
                    Hud.Announce("WAVE " + (w + 1), w + 1 == Director.WavesTotal ? "Final wave" : "They are coming", Pal.Accent);
                    Sfx.WaveStart();
                }
            };
            Zombie.Killed += OnKill;
            Boss.Defeated += OnBossDown;
            Hud = Hud.Create();
            Player.Hurted += (a, f, k) => { if (Hud != null) Hud.OnHurt(a, f, k); };
            Player.Died += OnPlayerDied;
            // Atmosphere: weather particles, ambience and music.
            Fx.SetWeather(Plan.Weather, Plan.Indoor, Player.WorldCam.transform);
            Sfx.Ambience(Plan.Weather, Plan.Indoor);
            Sfx.Music(Plan.IsBoss ? MusicKind.Boss : MusicKind.Combat);
            if (Plan.Event == StageEvent.SupplyDrop) _supplyT = 45f;
            _lightningT = Random.Range(5f, 12f);
            Hud.Announce(Plan.Title, Plan.ObjectiveText, Pal.Text);
            if (Plan.Event != StageEvent.None) Invoke(nameof(AnnounceEvent), 3f);
            Quality.ApplyFrameRate(true);
            Log.I("Stage " + Plan.Title + " " + Plan.Biome + " " + Plan.Objective + " meshes=" + Level.DrawMeshes + " lights=" + Level.LightCount + " colliders=" + Level.Colliders);
        }

        void AnnounceEvent() => Hud.Announce(StageGen.EventName(Plan.Event), "", Pal.Accent2);

        // --------------------------------------------------------------- update

        void Update()
        {
            float dt = Time.deltaTime;
            if (Ended)
            {
                _endT -= Time.unscaledDeltaTime;
                if (_endT <= 0 && Result == null) Finish();
                return;
            }
            if (Paused) return;
            Elapsed += dt;
            Quality.Tick(Time.unscaledDeltaTime);
            Look.Tick(dt);
            Fx.Tick(dt);
            // Weather events.
            if (Plan.Weather == Weather.Storm || Plan.Event == StageEvent.Thunderstorm)
            {
                _lightningT -= dt;
                if (_lightningT <= 0)
                {
                    _lightningT = Random.Range(7f, 18f);
                    Look.Lightning(Plan.Indoor ? 0.3f : 1f);
                    Sfx.Thunder();
                }
            }
            if (_supplyT > 0)
            {
                _supplyT -= dt;
                if (_supplyT <= 0)
                {
                    var p = Level.OnNav(PlayerRig.Pos + Quaternion.Euler(0, Random.Range(0, 360f), 0) * Vector3.forward * 8, 6);
                    SupplyDrop.Create(p, _dynamic);
                }
            }
            if (_bossSpawnT > 0)
            {
                _bossSpawnT -= dt;
                if (_bossSpawnT <= 0) SpawnBoss();
            }
            UpdateObjective(dt);
        }

        void SpawnBoss()
        {
            var at = Level.OnNav(Level.CellCenter(Plan.ExitCell), 4);
            if ((at - PlayerRig.Pos).magnitude < 12) at = Level.SpawnPoints[Random.Range(0, Level.SpawnPoints.Count)];
            _boss = Boss.Spawn(Plan.Boss, Plan, at, _dynamic);
            Hud.Announce(_boss.Def.Name, _boss.Def.Title, Pal.Bad);
        }

        void UpdateObjective(float dt)
        {
            MarkerTarget = null;
            HoldTarget = null;
            var d = Director;
            switch (Plan.Objective)
            {
                case ObjectiveType.Eliminate:
                    ObjectiveLine = "CLEAR THE INFECTED   WAVE " + Mathf.Clamp(d.Wave + 1, 1, d.WavesTotal) + "/" + d.WavesTotal;
                    ObjectiveSub = d.Wave < 0 ? "Get ready" : d.RemainingInWave + " infected remaining";
                    if (d.AllWavesDone && Zombie.Alive.Count == 0) Win();
                    else MarkNearestZombie(d.RemainingInWave <= 3);
                    break;
                case ObjectiveType.Survive:
                    ObjTimer -= dt;
                    ObjectiveLine = "SURVIVE   " + StageGen.Clock(ObjTimer);
                    ObjectiveSub = Zombie.Alive.Count + " infected nearby";
                    if (ObjTimer <= 0) Win();
                    break;
                case ObjectiveType.Rescue:
                {
                    int done = 0;
                    ObjectiveThing nearest = null;
                    float best = float.MaxValue;
                    foreach (var t in _things)
                    {
                        if (t.Done) { done++; continue; }
                        float dist = (t.transform.position - PlayerRig.Pos).Flat().magnitude;
                        if (dist < best) { best = dist; nearest = t; }
                        bool inside = dist < t.Radius;
                        if (t.TickHold(dt, inside))
                        {
                            Rescued++;
                            Hud.Announce("SURVIVOR RESCUED", (done + 1) + " / " + _things.Count, Pal.Good);
                            Sfx.LevelUp();
                        }
                        if (inside) HoldTarget = t;
                    }
                    ObjectiveLine = "RESCUE SURVIVORS   " + done + "/" + _things.Count;
                    ObjectiveSub = HoldTarget != null ? "Hold position - evacuating survivor" : "Find the survivors and stay with them";
                    if (nearest != null) { MarkerTarget = nearest.transform.position; MarkerIcon = Icon.Survivor; }
                    if (done >= _things.Count) Win();
                    break;
                }
                case ObjectiveType.Supplies:
                {
                    ObjectiveLine = "RECOVER SUPPLIES   " + SuppliesFound + "/" + Plan.ObjectiveCount;
                    ObjectiveSub = "Medical crates are marked";
                    Pickup nearest = null;
                    float best = float.MaxValue;
                    foreach (var p in Pickup.All)
                        if (p.Supply)
                        {
                            float dist = (p.transform.position - PlayerRig.Pos).sqrMagnitude;
                            if (dist < best) { best = dist; nearest = p; }
                        }
                    if (nearest != null) { MarkerTarget = nearest.transform.position; MarkerIcon = Icon.Supply; }
                    if (SuppliesFound >= Plan.ObjectiveCount) Win();
                    break;
                }
                case ObjectiveType.Nests:
                {
                    int done = 0;
                    ObjectiveThing nearest = null;
                    float best = float.MaxValue;
                    foreach (var t in _things)
                    {
                        if (t.Done) { done++; continue; }
                        float dist = (t.transform.position - PlayerRig.Pos).sqrMagnitude;
                        if (dist < best) { best = dist; nearest = t; }
                    }
                    ObjectiveLine = "DESTROY NESTS   " + done + "/" + _things.Count;
                    ObjectiveSub = "Fire and explosives deal extra damage";
                    if (nearest != null) { MarkerTarget = nearest.transform.position; MarkerIcon = Icon.Nest; }
                    if (done >= _things.Count) Win();
                    break;
                }
                case ObjectiveType.Defend:
                {
                    var b = _things[0];
                    float dist = (b.transform.position - PlayerRig.Pos).Flat().magnitude;
                    bool inside = dist < b.Radius;
                    if (inside) ObjTimer -= dt;
                    ObjectiveLine = "DEFEND THE BEACON   " + StageGen.Clock(ObjTimer);
                    ObjectiveSub = inside ? "Hold the area" : "Return to the beacon - transmission paused";
                    MarkerTarget = b.transform.position;
                    MarkerIcon = Icon.Beacon;
                    if (ObjTimer <= 0) Win();
                    break;
                }
                case ObjectiveType.Extract:
                {
                    if (!d.AllWavesDone || Zombie.Alive.Count > 0 && !_flare.Active)
                    {
                        ObjectiveLine = "SURVIVE THE HORDE   WAVE " + Mathf.Clamp(d.Wave + 1, 1, d.WavesTotal) + "/" + d.WavesTotal;
                        ObjectiveSub = d.Wave < 0 ? "Get ready" : d.RemainingInWave + " infected remaining";
                        MarkNearestZombie(d.RemainingInWave <= 3);
                        if (d.AllWavesDone && Zombie.Alive.Count == 0 && !_flare.Active) ActivateFlare();
                    }
                    else
                    {
                        if (!_flare.Active) ActivateFlare();
                        float dist = (_flare.transform.position - PlayerRig.Pos).Flat().magnitude;
                        bool inside = dist < _flare.Radius;
                        if (inside) HoldTarget = _flare;
                        if (_flare.TickHold(dt, inside)) Win();
                        ObjectiveLine = "REACH THE EXTRACTION FLARE";
                        ObjectiveSub = inside ? "Hold for pickup" : Mathf.RoundToInt(dist) + " m";
                        MarkerTarget = _flare.transform.position;
                        MarkerIcon = Icon.Pin;
                    }
                    break;
                }
                case ObjectiveType.Elite:
                {
                    int left = Plan.EliteCount - EliteKills;
                    ObjectiveLine = "ELIMINATE ELITES   " + EliteKills + "/" + Plan.EliteCount;
                    ObjectiveSub = "Elite " + Bestiary.Get(Plan.EliteType).Name + "s glow red";
                    Zombie nearest = null;
                    float best = float.MaxValue;
                    foreach (var z in Zombie.Alive)
                        if (z.Elite && z.Type == Plan.EliteType)
                        {
                            float dist = (z.transform.position - PlayerRig.Pos).sqrMagnitude;
                            if (dist < best) { best = dist; nearest = z; }
                        }
                    if (nearest != null) { MarkerTarget = nearest.transform.position; MarkerIcon = Icon.Skull; }
                    if (left <= 0) Win();
                    break;
                }
                case ObjectiveType.Boss:
                    ObjectiveLine = _boss != null ? "DEFEAT " + _boss.Def.Name : "SOMETHING BIG IS COMING";
                    ObjectiveSub = _boss != null ? "Phase " + _boss.Phase + (_boss.Type == BossType.Juggernaut ? " - shoot the glowing weak points" : "") : "";
                    if (_boss != null && _boss.IsAlive) { MarkerTarget = _boss.transform.position; MarkerIcon = Icon.Boss; }
                    break;
            }
        }

        void MarkNearestZombie(bool show)
        {
            if (!show) return;
            Zombie nearest = null;
            float best = float.MaxValue;
            foreach (var z in Zombie.Alive)
            {
                float dist = (z.transform.position - PlayerRig.Pos).sqrMagnitude;
                if (dist < best) { best = dist; nearest = z; }
            }
            if (nearest != null && best > 100) { MarkerTarget = nearest.transform.position; MarkerIcon = Icon.Skull; }
        }

        void ActivateFlare()
        {
            _flare.Activate();
            Hud.Announce("EXTRACTION READY", "Reach the red flare", Pal.Good);
            Sfx.LevelUp();
        }

        // --------------------------------------------------------------- events

        void OnKill(Zombie z, DamageInfo d)
        {
            if (Ended) return;
            Kills++;
            KillsByType[(int)z.Type]++;
            if (d.Zone == HitZone.Head) Headshots++;
            if (z.Elite) EliteKills++;
            if (d.Explosive) ExplosiveKills++;
            if (d.FromPlayer && !d.Explosive) KillsByClass[(int)d.Class]++;
            long coins = (long)(z.Def.Coins * Plan.RewardMul * PlayerRig.Stats.CoinMul * (Plan.Event == StageEvent.BloodMoon ? 2 : 1) * (z.Elite ? 3 : 1));
            CoinsEarned += coins;
            Hud?.CoinsPulse();
            // Loot drops.
            float luck = PlayerRig.Stats.LootMul;
            var at = z.transform.position + Vector3.up * 0.05f;
            float roll = Random.value;
            if (roll < 0.06f * luck) Pickup.Create(LootKind.Ammo, at, _dynamic);
            else if (roll < 0.09f * luck) Pickup.Create(LootKind.Health, at, _dynamic);
            else if (roll < 0.11f * luck || z.Elite) Pickup.Create(LootKind.Coins, at, _dynamic, false, (int)(25 * Plan.RewardMul));
        }

        void OnBossDown(Boss b)
        {
            BossKills++;
            Hud.Announce(b.Def.Name + " DEFEATED", "", Pal.Accent2);
            CoinsEarned += (long)(b.Def.Coins * Plan.RewardMul);
            Invoke(nameof(Win), 2.5f);
        }

        public void ObjectiveProgress(ObjectiveThing t)
        {
            if (t.Kind == ObjKind.Nest)
            {
                NestsDestroyed++;
                Hud.Announce("NEST DESTROYED", "", Pal.Good);
            }
        }

        /// <summary>Applies a pickup; returns true if it was used.</summary>
        public bool Collect(Pickup p)
        {
            var pl = Player;
            if (p.Supply)
            {
                SuppliesFound++;
                Sfx.Pickup();
                Hud.Announce("SUPPLIES RECOVERED", SuppliesFound + " / " + Plan.ObjectiveCount, Pal.Good);
                return true;
            }
            switch (p.Kind)
            {
                case LootKind.Ammo:
                    pl.Guns.AddAmmo(1.5f);
                    Screens.Toast("+ AMMO");
                    break;
                case LootKind.Health:
                    if (pl.Hp >= pl.MaxHp) return false;
                    pl.Heal(pl.MaxHp * 0.4f);
                    Screens.Toast("+ HEALTH");
                    break;
                case LootKind.Armor:
                    pl.AddArmor(50);
                    Screens.Toast("+ ARMOR");
                    break;
                case LootKind.Grenades:
                    if (pl.Guns.Grenades >= 6) return false;
                    pl.Guns.Grenades = Mathf.Min(6, pl.Guns.Grenades + 2);
                    Screens.Toast("+ 2 GRENADES");
                    break;
                case LootKind.Coins:
                {
                    long c = (long)(Mathf.Max(40, p.Amount) * PlayerRig.Stats.CoinMul);
                    CoinsEarned += c;
                    Sfx.Coin();
                    Hud.CoinsPulse();
                    Screens.Toast("+ " + c + " COINS");
                    return true;
                }
            }
            Sfx.Pickup();
            return true;
        }

        // --------------------------------------------------------------- pause / end

        public void Pause()
        {
            if (Ended || Paused) return;
            Paused = true;
            Time.timeScale = 0;
            AudioListener.pause = true;
            Hud.Touch.SetVisible(false);
            Screens.ShowPause();
        }

        public void Resume()
        {
            Paused = false;
            Time.timeScale = 1;
            AudioListener.pause = false;
            Hud.Touch.SetVisible(true);
        }

        void Win()
        {
            if (Ended || !Player.Alive) return;
            _won = true;
            End(1.8f);
            Hud.Announce(Plan.Mode == GameMode.BossRush ? "ROUND CLEAR" : Plan.Mode == GameMode.Campaign ? "MISSION COMPLETE" : Plan.Mode == GameMode.Daily ? "CHALLENGE COMPLETE" : "STAGE CLEAR", Plan.Title, Pal.Good);
            Sfx.StageClear();
            Player.Invulnerable = true;
            // The remaining infected collapse.
            foreach (var z in Zombie.Alive.ToArray()) z.TakeDamage(new DamageInfo { Amount = 1e6f, Point = z.transform.position + Vector3.up, Dir = Vector3.up, Zone = HitZone.Body });
        }

        public void OnPlayerDied()
        {
            if (Ended) return;
            Hud.Touch.SetVisible(false);
            Sfx.StageFail();
            Invoke(nameof(ShowRevive), 1.4f);
        }

        void ShowRevive()
        {
            if (Ended) return;
            Screens.ShowRevive(!Revived, () =>
            {
                Revived = true;
                Player.Revive();
                Hud.Touch.SetVisible(true);
                // Push the infected back a little.
                foreach (var z in Zombie.Alive.ToArray())
                    if ((z.transform.position - PlayerRig.Pos).magnitude < 5) z.TakeDamage(new DamageInfo { Amount = 1e6f, Point = z.transform.position, Dir = Vector3.up, Explosive = true });
            }, () => { _won = false; End(0.2f); });
        }

        public void Forfeit()
        {
            Resume();
            _won = false;
            End(0);
        }

        void End(float delay)
        {
            Ended = true;
            _endT = delay;
            if (Director != null) Director.Paused = true;
        }

        /// <summary>Computes and applies rewards, then shows the result screen.</summary>
        void Finish()
        {
            var d = SaveSystem.Data;
            var res = new StageResult { Plan = Plan, Cleared = _won, Kills = Kills, Headshots = Headshots, Time = Elapsed };
            res.Accuracy = Player.Guns.ShotsFired > 0 ? Player.Guns.ShotsHit / (float)Player.Guns.ShotsFired : 0;
            bool firstClear;
            switch (Plan.Mode)
            {
                case GameMode.Campaign: firstClear = Plan.CampaignMission >= d.CampaignCleared; break;
                case GameMode.Daily: firstClear = d.DailyDone != Plan.DailyDay; break;
                case GameMode.BossRush: firstClear = Round > d.BossRushBest; break;
                default: firstClear = Plan.Stage >= d.HighestStage; break;
            }
            var reward = Rewards.ForStage(Plan, _won, KillsByType, Headshots, BossKills, PlayerRig.Stats, firstClear && _won);
            res.Reward = reward;
            Wallet.Add(d, Currency.Coins, reward.Coins);
            Wallet.Add(d, Currency.Gems, reward.Gems);
            res.LevelUps = PlayerProgress.AddXp(d, reward.Xp);
            // Stats.
            var st = d.Stats;
            st.Kills += Kills;
            st.Headshots += Headshots;
            st.EliteKills += EliteKills;
            st.BossKills += BossKills;
            st.ExplosiveKills += ExplosiveKills;
            st.Rescued += Rescued;
            st.NestsDestroyed += NestsDestroyed;
            st.ShotsFired += Player.Guns.ShotsFired;
            st.ShotsHit += Player.Guns.ShotsHit;
            st.PlayTimeSeconds += (long)Elapsed;
            for (int i = 0; i < 9; i++) st.KillsByType[i] += KillsByType[i];
            for (int i = 0; i < 6; i++) st.KillsByClass[i] += KillsByClass[i];
            if (!_won) st.Deaths++;
            // Missions.
            Missions.Refresh(d, Clock.Today);
            Missions.Track(d, MissionKind.Kills, 0, Kills);
            Missions.Track(d, MissionKind.Headshots, 0, Headshots);
            for (int i = 0; i < 9; i++) Missions.Track(d, MissionKind.KillType, i, KillsByType[i]);
            for (int i = 0; i < 6; i++) Missions.Track(d, MissionKind.ClassKills, i, KillsByClass[i]);
            Missions.Track(d, MissionKind.Coins, 0, reward.Coins);
            Missions.Track(d, MissionKind.BossKills, 0, BossKills);
            Missions.Track(d, MissionKind.ExplosiveKills, 0, ExplosiveKills);
            Missions.Track(d, MissionKind.Rescues, 0, Rescued);
            if (_won)
            {
                st.StagesCleared++;
                Missions.Track(d, MissionKind.Stages, 0, 1);
                switch (Plan.Mode)
                {
                    case GameMode.Survival:
                        d.Stage = Safe.Add(Plan.Stage, 1);
                        d.HighestStage = System.Math.Max(d.HighestStage, d.Stage);
                        break;
                    case GameMode.Campaign:
                    {
                        int idx = Plan.CampaignMission;
                        var m = Campaign.All[idx];
                        int stars = Campaign.Stars(Player.Hp / Player.MaxHp, res.Accuracy, true);
                        res.Stars = stars;
                        if (d.CampaignStars.Length < Campaign.All.Length) System.Array.Resize(ref d.CampaignStars, Campaign.All.Length);
                        d.CampaignStars[idx] = System.Math.Max(d.CampaignStars[idx], stars);
                        if (firstClear)
                        {
                            res.CampaignCoins = m.Coins;
                            res.CampaignGems = m.Gems;
                            Wallet.Add(d, Currency.Coins, m.Coins);
                            Wallet.Add(d, Currency.Gems, m.Gems);
                            d.CampaignCleared = System.Math.Max(d.CampaignCleared, idx + 1);
                            st.CampaignCleared = d.CampaignCleared;
                        }
                        break;
                    }
                    case GameMode.Daily:
                        if (firstClear)
                        {
                            d.DailyDone = Plan.DailyDay;
                            st.DailyChallenges++;
                            res.DailyCoins = DailyChallenge.CompletionCoins(Plan);
                            res.DailyGems = DailyChallenge.CompletionGems;
                            Wallet.Add(d, Currency.Coins, res.DailyCoins);
                            Wallet.Add(d, Currency.Gems, res.DailyGems);
                            Missions.Track(d, MissionKind.DailyChallenge, 0, 1);
                        }
                        break;
                    case GameMode.BossRush:
                        res.RushCoins = BossRush.RoundCoins(Round);
                        res.RushGems = firstClear ? BossRush.RoundGems(Round) : 0;
                        Wallet.Add(d, Currency.Coins, res.RushCoins);
                        Wallet.Add(d, Currency.Gems, res.RushGems);
                        d.BossRushBest = System.Math.Max(d.BossRushBest, Round);
                        break;
                }
            }
            Store.TickBoosters(d);
            d.Grenades = Mathf.Clamp(Player.Guns.Grenades, 0, 9);
            SaveSystem.Save();
            CloudSave.Push();
            Result = res;
            Player.Guns.Holster();
            Hud.Touch.SetVisible(false);
            // The objective, ammo and weapon bars otherwise show through behind the result title and buttons.
            if (Hud.Canvas != null) Hud.Canvas.enabled = false;
            Screens.ShowResult(res);
            Log.I("RESULT " + (res.Cleared ? "CLEAR" : "FAIL") + " kills=" + Kills + " hs=" + Headshots + " coins=" + reward.Coins + " xp=" + reward.Xp + " t=" + Elapsed.ToString("0") + "s acc=" + res.Accuracy.ToString("0.00"));
        }

        /// <summary>Doubles the stage coin payout once (after a rewarded ad).</summary>
        public void DoubleReward()
        {
            if (Result == null || Result.Doubled) return;
            Result.Doubled = true;
            Wallet.Add(SaveSystem.Data, Currency.Coins, Result.Reward.Coins);
            SaveSystem.Save();
        }
    }
}
