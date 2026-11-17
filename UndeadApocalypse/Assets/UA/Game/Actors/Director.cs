using System.Collections.Generic;
using UA.Core;
using UnityEngine;
using UnityEngine.AI;

namespace UA.Game
{
    /// <summary>
    /// Wave and spawn director. Pools zombies per type, spawns them out of sight on the NavMesh,
    /// runs discrete waves (clear-the-wave objectives) or a continuous flow (timed / collect /
    /// defend objectives), handles elites and boss minions, assigns surround slots and recycles corpses.
    /// </summary>
    public sealed class Director : MonoBehaviour
    {
        public static Director Instance;

        public StagePlan Plan;
        public Level Level;
        public int Wave = -1, WavesTotal, SpawnedInWave, Kills, EliteAlive, ElitesSpawned;
        public bool Continuous, Paused, AllWavesDone;
        public int MaxAlive;
        public event System.Action<int> WaveStarted, WaveCleared;

        readonly Dictionary<ZombieType, List<Zombie>> _pool = new Dictionary<ZombieType, List<Zombie>>();
        readonly List<Zombie> _corpses = new List<Zombie>();
        Transform _root;
        float _spawnT, _waveDelay = 1f, _slotT;
        bool _waveActive;
        int _cursor;
        List<SpawnEntry> _queue = new List<SpawnEntry>();
        readonly Queue<(ZombieType type, Vector3 pos)> _minions = new Queue<(ZombieType, Vector3)>();
        int _eliteQueue;
        float _eliteT;

        public int AliveCount => Zombie.Alive.Count;

        public static Director Create(StagePlan plan, Level level)
        {
            var go = new GameObject("Director");
            go.transform.SetParent(level.Root.transform, false);
            var d = go.AddComponent<Director>();
            d.Plan = plan;
            d.Level = level;
            d._root = go.transform;
            d.MaxAlive = Mathf.Min(plan.MaxAlive, Quality.MaxZombies);
            if (plan.IsBoss) d.MaxAlive = Mathf.Min(d.MaxAlive, Mathf.Max(5, Quality.MaxZombies / 2));
            var o = plan.Objective;
            d.Continuous = o == ObjectiveType.Survive || o == ObjectiveType.Defend || o == ObjectiveType.Supplies || o == ObjectiveType.Rescue || o == ObjectiveType.Nests || o == ObjectiveType.Elite || o == ObjectiveType.Boss;
            d.WavesTotal = o == ObjectiveType.Extract ? Mathf.Min(plan.Waves.Count, Mathf.Max(2, plan.ObjectiveCount)) : plan.Waves.Count;
            if (o == ObjectiveType.Elite) { d._eliteQueue = plan.EliteCount; d._eliteT = 6f; }
            Instance = d;
            d.Prewarm();
            return d;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Zombie.Alive.Clear();
        }

        // --------------------------------------------------------------- pool

        void Prewarm()
        {
            var counts = new Dictionary<ZombieType, int>();
            foreach (var w in Plan.Waves)
                foreach (var e in w.Spawns)
                    counts[e.Type] = (counts.TryGetValue(e.Type, out var c) ? c : 0) + 1;
            if (Plan.Objective == ObjectiveType.Elite) counts[Plan.EliteType] = (counts.TryGetValue(Plan.EliteType, out var ec) ? ec : 0) + Plan.EliteCount;
            if (Plan.Boss == BossType.BroodMother) counts[ZombieType.Crawler] = (counts.TryGetValue(ZombieType.Crawler, out var cc) ? cc : 0) + 6;
            if (Plan.Boss == BossType.Wraith) counts[ZombieType.Hunter] = (counts.TryGetValue(ZombieType.Hunter, out var hc) ? hc : 0) + 4;
            if (Plan.Objective == ObjectiveType.Nests) counts[ZombieType.Crawler] = (counts.TryGetValue(ZombieType.Crawler, out var nc) ? nc : 0) + 3;
            foreach (var kv in counts)
            {
                int n = Mathf.Clamp(Mathf.Min(kv.Value, MaxAlive), 1, MaxAlive);
                for (int i = 0; i < n; i++) Make(kv.Key, false);
            }
        }

        Zombie Make(ZombieType t, bool elite)
        {
            if (!_pool.TryGetValue(t, out var list)) { list = new List<Zombie>(); _pool[t] = list; }
            var z = Zombie.Create(_root, t, elite, list.Count % 5 + (int)(Plan.Seed % 7));
            list.Add(z);
            return z;
        }

        Zombie Take(ZombieType t, bool elite)
        {
            if (_pool.TryGetValue(t, out var list))
                foreach (var z in list)
                    if (!z.gameObject.activeSelf && z.Elite == elite) return z;
            // Recycle the oldest corpse of this type if the pool is exhausted.
            if (_pool.TryGetValue(t, out list))
                foreach (var z in list)
                    if (z.State == ZState.Dead && z.Elite == elite) { _corpses.Remove(z); z.Despawn(); return z; }
            return Make(t, elite);
        }

        // --------------------------------------------------------------- spawning

        Vector3? FindSpawnPoint(float minDist = 14)
        {
            var pts = Level.SpawnPoints;
            if (pts.Count == 0) return null;
            var cam = PlayerRig.Cam;
            var ppos = PlayerRig.Pos;
            Vector3? best = null;
            float bestScore = float.MinValue;
            for (int k = 0; k < 8; k++)
            {
                var p = pts[Random.Range(0, pts.Count)] + new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
                float d = (p - ppos).Flat().magnitude;
                if (d < minDist) continue;
                float score = -Mathf.Abs(d - 24);
                if (cam != null)
                {
                    var dir = (p - cam.transform.position).Flat().normalized;
                    if (Vector3.Dot(cam.transform.forward.Flat().normalized, dir) > 0.5f) score -= 15;
                }
                if (score > bestScore && NavMesh.SamplePosition(p, out var hit, 2.5f, NavMesh.AllAreas)) { bestScore = score; best = hit.position; }
            }
            if (best == null)
            {
                // Fall back to the farthest spawn point.
                float far = 0;
                foreach (var p in pts)
                {
                    float d = (p - ppos).sqrMagnitude;
                    if (d > far) { far = d; best = p; }
                }
            }
            return best;
        }

        public Zombie SpawnOne(ZombieType t, bool elite, Vector3? at = null, bool aware = true)
        {
            var p = at ?? FindSpawnPoint();
            if (p == null) return null;
            var z = Take(t, elite);
            z.Spawn(p.Value, Plan, aware);
            return z;
        }

        public void SpawnMinions(ZombieType t, int n, Vector3 near)
        {
            for (int i = 0; i < n; i++) _minions.Enqueue((t, near + new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f))));
        }

        // --------------------------------------------------------------- update

        void Update()
        {
            float dt = Time.deltaTime;
            if (Paused || PlayerRig.Instance == null) return;
            RecycleCorpses();
            AssignSlots(dt);
            // Boss / nest minions first (they ignore the wave cap but respect a hard cap).
            while (_minions.Count > 0 && Zombie.Alive.Count < MaxAlive + 4)
            {
                var (t, pos) = _minions.Dequeue();
                if (NavMesh.SamplePosition(pos, out var hit, 3f, NavMesh.AllAreas)) SpawnOne(t, false, hit.position);
            }
            // Elites.
            if (_eliteQueue > 0)
            {
                _eliteT -= dt;
                if (_eliteT <= 0)
                {
                    _eliteT = 12f;
                    var z = SpawnOne(Plan.EliteType, true);
                    if (z != null)
                    {
                        _eliteQueue--;
                        ElitesSpawned++;
                        Hud.Announce("ELITE " + z.Def.Name.ToUpper(), "Eliminate the marked target", new Color(1f, 0.35f, 0.2f));
                        Sfx.EliteSting();
                    }
                }
            }
            if (AllWavesDone && !Continuous) return;
            bool waveDone = Wave >= 0 && _cursor >= _queue.Count && (Continuous || Zombie.Alive.Count == 0);
            if (Wave < 0 || waveDone)
            {
                if (_waveActive)
                {
                    _waveActive = false;
                    _waveDelay = Continuous ? 1.5f : 4f;
                    if (!Continuous)
                    {
                        WaveCleared?.Invoke(Wave);
                        if (Wave + 1 >= WavesTotal)
                        {
                            AllWavesDone = true;
                            return;
                        }
                    }
                }
                _waveDelay -= dt;
                if (_waveDelay > 0) return;
                _waveActive = true;
                Wave++;
                int wi = Continuous ? Wave % Plan.Waves.Count : Wave;
                var wave = Plan.Waves[Mathf.Clamp(wi, 0, Plan.Waves.Count - 1)];
                _queue = new List<SpawnEntry>(wave.Spawns);
                if (Continuous && Wave >= Plan.Waves.Count)
                {
                    // Looping waves grow slowly so long objectives stay tense.
                    int extra = Mathf.Min(10, (Wave - Plan.Waves.Count + 1) * 2);
                    for (int i = 0; i < extra; i++) _queue.Add(wave.Spawns[Random.Range(0, wave.Spawns.Count)]);
                }
                _cursor = 0;
                SpawnedInWave = 0;
                _spawnT = wave.Delay;
                WaveStarted?.Invoke(Wave);
                return;
            }
            _spawnT -= dt;
            int cap = Plan.IsBoss ? Mathf.Max(3, MaxAlive / 2) : MaxAlive;
            if (_spawnT <= 0 && _cursor < _queue.Count && Zombie.Alive.Count < cap)
            {
                var w = Plan.Waves[Mathf.Clamp(Continuous ? Wave % Plan.Waves.Count : Wave, 0, Plan.Waves.Count - 1)];
                _spawnT = w.Interval * Random.Range(0.7f, 1.3f);
                var e = _queue[_cursor++];
                SpawnOne(e.Type, e.Elite);
                SpawnedInWave++;
            }
        }

        public int RemainingInWave => Mathf.Max(0, _queue.Count - _cursor) + Zombie.Alive.Count;

        void RecycleCorpses()
        {
            foreach (var list in _pool.Values)
                foreach (var z in list)
                    if (z.gameObject.activeSelf && z.State == ZState.Dead && !_corpses.Contains(z)) _corpses.Add(z);
            for (int i = _corpses.Count - 1; i >= 0; i--)
            {
                var z = _corpses[i];
                if (z.ReadyToRecycle || (_corpses.Count > Quality.MaxCorpses && i == 0))
                {
                    z.Despawn();
                    _corpses.RemoveAt(i);
                }
            }
        }

        static readonly List<Zombie> Near = new List<Zombie>();

        /// <summary>Spreads nearby zombies around the player so they encircle instead of queueing.</summary>
        void AssignSlots(float dt)
        {
            _slotT -= dt;
            if (_slotT > 0) return;
            _slotT = 0.5f;
            Near.Clear();
            var p = PlayerRig.Pos;
            foreach (var z in Zombie.Alive) if ((z.transform.position - p).sqrMagnitude < 12 * 12) Near.Add(z);
            if (Near.Count < 2) return;
            Near.Sort((a, b) => Angle(a, p).CompareTo(Angle(b, p)));
            float step = 360f / Near.Count;
            float baseA = Angle(Near[0], p);
            for (int i = 0; i < Near.Count; i++) Near[i].SurroundAngle = baseA + i * step;
        }

        static float Angle(Zombie z, Vector3 p)
        {
            var d = z.transform.position - p;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        /// <summary>Kills every live zombie (used when the stage ends).</summary>
        public void ClearAll()
        {
            foreach (var list in _pool.Values)
                foreach (var z in list)
                    if (z.gameObject.activeSelf) z.Despawn();
            _corpses.Clear();
            _minions.Clear();
            _queue.Clear();
        }
    }
}
