using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ToyKingdom
{
    public enum BattleState { Playing, Won, Lost }

    /// <summary>Savaş sahnesinin beyni: alanı kurar, bölükleri yönetir, kazanma/kaybetmeyi belirler.</summary>
    public class BattleManager : MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

        [Tooltip("Haritadan gelmeden Battle sahnesini doğrudan Play ile test etmek için")]
        public CastleData testCastle;
        public string mapSceneName = "Map";

        public CastleData Castle { get; private set; }
        public BattleField Field { get; private set; }
        public CommandPoints Command { get; private set; }
        public float TimeLeft { get; private set; }
        public BattleState State { get; private set; } = BattleState.Playing;
        public UnitData SelectedUnit { get; private set; }
        /// <summary>Meydan büyüdü: yürüme ve kanat manevraları için süreye pay eklenir.</summary>
        public float TimeLimit => Castle != null ? Castle.timeLimit + 45f : 180f;
        public List<Squad> PlayerSquads { get; } = new List<Squad>();
        public List<Squad> EnemySquads { get; } = new List<Squad>();
        public Camera Cam { get; private set; }
        public CameraRig Rig { get; private set; }

        public event Action<UnitData> SelectedUnitChanged;
        public event Action ArmyChanged;
        public event Action<BattleResult> BattleEnded;
        public event Action<string> Hint;
        /// <summary>Emir verilecek seçili bölük değişti.</summary>
        public event Action<Squad> SquadSelected;
        public Squad SelectedSquad { get; private set; }

        public void SelectSquad(Squad sq)
        {
            if (sq != null && !sq.CanCommand) sq = null;
            SelectedSquad = sq;
            SquadSelected?.Invoke(sq);
        }

        /// <summary>Dokunulan noktaya en yakın, emir verilebilir oyuncu bölüğü.</summary>
        public Squad SquadAt(Vector3 world, float radius = 1.1f)
        {
            Squad best = null;
            float bd = radius;
            foreach (var s in PlayerSquads)
            {
                if (!s.CanCommand) continue;
                foreach (var m in s.Members)
                {
                    float d = Vector2.Distance(m.Ground, world);
                    if (d < bd) { bd = d; best = s; }
                }
            }
            return best;
        }

        float reinforcementTimer;
        int reinforcementsLeft;
        int deployedSquads;
        int lostSquads;
        readonly List<string> lostUnits = new List<string>();
        float endDelay = -1f;
        BattleResult pendingResult;

        // kuşatma derinliği
        public bool Capturing { get; private set; }
        public Vector3 CapturePoint { get; private set; }
        public float CaptureProgress { get; private set; }
        CastleGate captureGate;
        Transform captureRing;
        SpriteRenderer captureFill;
        float nextRally;
        readonly Dictionary<CastleGate, float> oilTimers = new Dictionary<CastleGate, float>();
        readonly HashSet<CastleGate> halfDamaged = new HashSet<CastleGate>();
        bool oilHintShown;

        void Awake()
        {
            Instance = this;
            Soldier.All.Clear();
            UIFactory.EnsureEventSystem();
            Cam = Camera.main;

            Castle = GameSession.SelectedCastle != null ? GameSession.SelectedCastle : testCastle;
            if (Castle == null || GameArt.Instance == null)
            {
                Debug.LogError("BattleManager: kale verisi ya da GameArt bulunamadı.");
                enabled = false;
                return;
            }

            // kuşatma savunması: daha büyük kale, daha sert savunucular, mayınlar ve toplar
            Castle = Siege.Harden(Castle);
            Field = gameObject.AddComponent<BattleField>();
            Field.Build(Castle);
            Siege.Build(Field, Castle);
            if (Cam != null)
            {
                Field.FitCamera(Cam, 0.18f);
                Rig = Cam.GetComponent<CameraRig>();
                if (Rig == null) Rig = Cam.gameObject.AddComponent<CameraRig>();
                Rig.bounds = Field.CameraBounds;
                Rig.minSize = 3.2f;
                Rig.maxSize = 8.6f;
            }
            foreach (var g in Field.Gates)
            {
                g.Destroyed += OnGateDestroyed;
                g.Damaged += OnGateDamaged;
                oilTimers[g] = 4f;
            }
            foreach (var t in Field.Towers) t.Destroyed += OnTowerDestroyed;

            Command = new CommandPoints(Castle.startingCommandPoints, Castle.maxCommandPoints, Castle.commandRegenPerSecond);
            TimeLeft = TimeLimit;
            reinforcementTimer = Castle.reinforcementInterval;
            reinforcementsLeft = Castle.reinforcementCount;

            SpawnGarrison();
        }

        void Start()
        {
            gameObject.AddComponent<BattleHUD>();
            gameObject.AddComponent<PathDrawer>();
            Sfx.Music("music_battle");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void SpawnGarrison()
        {
            // Sur okçuları: ön surların üstünde sabit dururlar
            if (Castle.wallArchers > 0 && Castle.wallArcherUnit != null)
            {
                var posts = Field.WallArcherPosts(Castle.wallArchers);
                if (posts.Count > 0)
                {
                    var sq = new Squad(Team.Enemy, Castle.wallArcherUnit, Field) { Strength = Castle.defenderStrength };
                    sq.SpawnAtPosts(posts, BattleField.WallTopElevation, true);
                    EnemySquads.Add(sq);
                }
            }

            // Yer savunması: her bölük kendi bölgesine dağınık yerleşir ve devriye gezer
            int zone = 0;
            foreach (var entry in Castle.garrison)
            {
                if (entry == null || entry.unit == null) continue;
                for (int i = 0; i < entry.squads; i++)
                {
                    var sq = new Squad(Team.Enemy, entry.unit, Field) { Strength = Castle.defenderStrength };
                    bool ranged = entry.unit.IsRanged;
                    var posts = Field.GuardPosts(zone++, entry.unit.squadSize, ranged);
                    sq.SpawnAtPosts(posts, 0f, false);
                    sq.SetGuardRanges(ranged ? entry.unit.attackRange : 2.3f, ranged ? 2.5f : 3.4f);
                    EnemySquads.Add(sq);
                }
            }
        }

        void SpawnReinforcement()
        {
            if (Castle.reinforcement == null) return;
            var sq = new Squad(Team.Enemy, Castle.reinforcement, Field) { Strength = Castle.defenderStrength };
            var posts = Field.GuardPosts(reinforcementsLeft, Castle.reinforcement.squadSize, false);

            // akın: takviye, oyuncunun okçularının üzerine gider
            Squad archers = null;
            if (Castle.Sortie)
            {
                float best = float.MaxValue;
                foreach (var p in PlayerSquads)
                {
                    if (p.IsWiped || p.Routed || !p.Data.IsRanged) continue;
                    float d = (p.Center() - Field.GateSpawnPoint(0)).sqrMagnitude;
                    if (d < best) { best = d; archers = p; }
                }
                if (archers != null) posts = Field.PostsNear(archers.Center(), Castle.reinforcement.squadSize, 0.9f);
            }
            // kapıdan çıkıp yerlerine yürürler
            Vector3 gate = Field.GateSpawnPoint(reinforcementsLeft);
            var start = new List<Vector3>();
            for (int i = 0; i < posts.Count; i++) start.Add(Field.Constrain(gate + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.3f)));
            sq.SpawnAtPosts(start, 0f, false);
            sq.ReassignPosts(posts);
            sq.SetGuardRanges(archers != null ? 5.5f : 4.5f, archers != null ? 12f : 7f);
            EnemySquads.Add(sq);
            Hint?.Invoke(archers != null ? "Akın! Takviyeler okçularının üzerine geliyor" : "Kapıdan takviye kuvvet çıktı!");
        }

        void Update()
        {
            if (endDelay >= 0f)
            {
                endDelay -= Time.deltaTime;
                if (endDelay < 0f) BattleEnded?.Invoke(pendingResult);
            }
            if (State != BattleState.Playing) return;

            float dt = Time.deltaTime;
            TimeLeft -= dt;
            Command.Tick(dt);

            foreach (var s in PlayerSquads) s.Tick(dt);
            foreach (var s in EnemySquads) s.Tick(dt);

            if (reinforcementsLeft > 0)
            {
                reinforcementTimer -= dt;
                if (reinforcementTimer <= 0f)
                {
                    reinforcementsLeft--;
                    reinforcementTimer = Castle.reinforcementInterval;
                    SpawnReinforcement();
                }
            }

            if (Castle.BoilingOil) UpdateOil(dt);
            if (Capturing) UpdateCapture(dt);
            if (State != BattleState.Playing) return;

            if (TimeLeft <= 0f)
            {
                TimeLeft = 0f;
                EndBattle(false);
                return;
            }

            // Ordu tükendiyse ve sahada savaşabilen kimse kalmadıysa kaybedilir
            if (deployedSquads > 0 && !AnyPlayerAlive() && !CanAffordAnything(true)) EndBattle(false);
        }

        bool AnyPlayerAlive()
        {
            foreach (var s in PlayerSquads) if (!s.IsWiped && !s.Routed) return true;
            return false;
        }

        /// <summary>Elde gönderilebilecek bölük var mı? (ignoreCommand: komuta puanını yok say)</summary>
        public bool CanAffordAnything(bool ignoreCommand)
        {
            var db = UnitDatabase.Instance;
            if (db == null) return false;
            foreach (var u in db.units)
                if (u != null && SaveSystem.Data.GetArmyCount(u.unitId) > 0 && (ignoreCommand || Command.CanAfford(u.cost)))
                    return true;
            return false;
        }

        // ------------------------------------------------------------------ oyuncu komutları

        public void SelectUnit(UnitData unit)
        {
            SelectedUnit = unit;
            SelectedUnitChanged?.Invoke(unit);
        }

        /// <summary>Seçili birlik gönderilebilir mi? Değilse nedenini döner.</summary>
        public bool CanDeploy(out string reason)
        {
            reason = null;
            if (State != BattleState.Playing) { reason = ""; return false; }
            if (SelectedUnit == null) { reason = "Önce aşağıdan bir bölük seç"; return false; }
            if (SaveSystem.Data.GetArmyCount(SelectedUnit.unitId) <= 0) { reason = SelectedUnit.displayName + " bölüğün kalmadı"; return false; }
            if (!Command.CanAfford(SelectedUnit.cost)) { reason = "Komuta puanı yetmiyor, biraz bekle"; return false; }
            return true;
        }

        public bool Deploy(List<Vector3> path)
        {
            if (!CanDeploy(out _) || path == null || path.Count < 2) return false;
            var data = SelectedUnit;
            Command.TrySpend(data.cost);
            SaveSystem.Data.AddArmy(data.unitId, -1);

            var sq = new Squad(Team.Player, data, Field);
            Sfx.Play("sfx_deploy", 0.7f);
            sq.AggroRange = 1.6f;
            sq.SpawnMembers(path[0], data.squadSize);
            sq.SetPath(path, Field.SpriteMaterial);
            PlayerSquads.Add(sq);
            deployedSquads++;
            ArmyChanged?.Invoke();

            // seçili birlik bittiyse elde kalan bir sonrakine geç
            if (SaveSystem.Data.GetArmyCount(data.unitId) <= 0)
            {
                var db = UnitDatabase.Instance;
                foreach (var u in db.units)
                    if (u != null && SaveSystem.Data.GetArmyCount(u.unitId) > 0) { SelectUnit(u); break; }
            }
            return true;
        }

        public void Retreat()
        {
            if (State == BattleState.Playing) EndBattle(false);
        }

        // ------------------------------------------------------------------ bitiş

        void OnGateDestroyed(CastleGate gate)
        {
            if (State != BattleState.Playing || Capturing) return;
            foreach (var e in EnemySquads) e.ChangeMorale(-25f);
            foreach (var p in PlayerSquads) p.ChangeMorale(20f);
            if (!Castle.Courtyard)
            {
                Hint?.Invoke("Kapı kırıldı! Kale düşüyor...");
                EndBattle(true);
                return;
            }
            BeginCapture(gate);
        }

        void OnTowerDestroyed(Tower t)
        {
            Hint?.Invoke("Bir kule yıkıldı! Askerlerinin morali yükseldi");
            foreach (var p in PlayerSquads) p.ChangeMorale(12f);
            foreach (var e in EnemySquads) e.ChangeMorale(-10f);
        }

        void OnGateDamaged(CastleGate gate, float amount)
        {
            if (State != BattleState.Playing) return;
            if (gate.Normalized < 0.5f && halfDamaged.Add(gate))
                foreach (var e in EnemySquads) e.ChangeMorale(-12f);

            // hızlı savunma: yakındaki nöbetçiler kapıya koşar
            if (!Castle.RallyDefense || Time.time < nextRally) return;
            nextRally = Time.time + 12f;
            int sent = 0;
            foreach (var e in EnemySquads)
            {
                if (sent >= 2) break;
                if (e.IsWiped || e.Mode != SquadMode.Guard || e.Engaged || e.Routed) continue;
                if ((e.Anchor - gate.FrontPoint).magnitude < 2.2f) continue;
                Vector2 g = Iso.ToGrid(gate.FrontPoint) + gate.Outward * 1.3f;
                e.ReassignPosts(Field.PostsNear(Iso.Surface(g.x, g.y), e.Alive, 1.1f));
                e.SetGuardRanges(2.6f, 3.6f);
                sent++;
            }
            if (sent > 0) Hint?.Invoke("Savunucular kapıya koşuyor!");
        }

        /// <summary>Bir bölük tamamen yok oldu: yakındakilerin morali etkilenir.</summary>
        public void OnSquadWiped(Squad sq)
        {
            Vector3 c = sq.Anchor;
            bool enemy = sq.Team == Team.Enemy;
            foreach (var p in PlayerSquads)
                if (p != sq && (p.Center() - c).magnitude < 3.5f) p.ChangeMorale(enemy ? 10f : -12f);
            foreach (var e in EnemySquads)
                if (e != sq && (e.Center() - c).magnitude < 3.5f) e.ChangeMorale(enemy ? -10f : 6f);
        }

        bool mineHint;
        public void ReportMine()
        {
            if (mineHint) return;
            mineHint = true;
            Hint?.Invoke("Mayın! Kale önünde gizli mayınlar var: toprak tümseklerinden uzak dur");
        }

        public void OnSquadRouted(Squad sq)
        {
            if (sq.Team == Team.Player) Hint?.Invoke(sq.Data.displayName + " bölüğü bozguna uğradı, geri çekiliyor!");
            else Hint?.Invoke("Düşman " + sq.Data.displayName.ToLower() + " bölüğü kaçıyor!");
        }

        // ------------------------------------------------------------------ kızgın yağ

        void UpdateOil(float dt)
        {
            foreach (var g in Field.Gates)
            {
                if (g == null || g.IsDestroyed) continue;
                float t = oilTimers.TryGetValue(g, out var v) ? v : 4f;
                t -= dt;
                if (t > 0f) { oilTimers[g] = t; continue; }

                int near = 0;
                foreach (var s in Soldier.All)
                    if (s != null && s.IsAlive && s.Team == Team.Player && !s.IsElevated && (s.Ground - g.FrontPoint).magnitude < 1.0f) near++;
                if (near < 3) { oilTimers[g] = 1f; continue; }

                oilTimers[g] = 8f;
                float dmg = 16f * Castle.defenderStrength;
                foreach (var s in Soldier.All.ToArray())
                    if (s != null && s.IsAlive && s.Team == Team.Player && !s.IsElevated && (s.Ground - g.FrontPoint).magnitude < 1.1f)
                        s.TakeDamage(dmg, DamageType.Fire);
                FX.Sparkles(g.FrontPoint + Vector3.up * 0.2f, new Color(1f, 0.55f, 0.15f), 22, 1.0f);
                FX.Sparkles(g.transform.position + Vector3.up * 1.1f, new Color(0.35f, 0.3f, 0.28f), 10, 0.5f);
                if (!oilHintShown)
                {
                    oilHintShown = true;
                    Hint?.Invoke("Kızgın yağ! Kapı önünde kümelenme, kalkanlılar daha az yanar");
                }
            }
        }

        // ------------------------------------------------------------------ avlu savaşı

        void BeginCapture(CastleGate gate)
        {
            Capturing = true;
            captureGate = gate;
            CapturePoint = gate.FrontPoint;
            CaptureProgress = 0f;
            Hint?.Invoke("Kapı kırıldı! Son direniş: kapının önünü tut");

            // son direniş: kapıdan savunucular çıkar
            UnitData unit = null;
            foreach (var g in Castle.garrison)
                if (g != null && g.unit != null && !g.unit.IsRanged && (unit == null || g.unit.maxHealth > unit.maxHealth)) unit = g.unit;
            if (unit != null)
            {
                for (int i = 0; i < Castle.FinalStandSquads; i++)
                {
                    var sq = new Squad(Team.Enemy, unit, Field) { Strength = Castle.defenderStrength * 1.1f };
                    var posts = Field.PostsNear(CapturePoint, unit.squadSize, 1.2f + i * 0.5f);
                    var start = new List<Vector3>();
                    for (int k = 0; k < posts.Count; k++) start.Add(Field.Constrain(CapturePoint + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.25f)));
                    sq.SpawnAtPosts(start, 0f, false);
                    sq.ReassignPosts(posts);
                    sq.SetGuardRanges(2.8f, 3.4f);
                    EnemySquads.Add(sq);
                }
            }
            // oyuncu bölükleri kapı önüne toplanır
            foreach (var p in PlayerSquads) if (p.Mode == SquadMode.Assault) p.Tick(0f);

            var art = GameArt.Instance;
            if (art != null && art.circle != null)
            {
                captureRing = new GameObject("CaptureZone").transform;
                captureRing.position = CapturePoint;
                var ring = captureRing.gameObject.AddComponent<SpriteRenderer>();
                ring.sprite = art.circle;
                ring.color = new Color(1f, 1f, 1f, 0.18f);
                ring.sortingOrder = -19000;
                captureRing.localScale = new Vector3(3.6f, 2.2f, 1f);
                captureFill = new GameObject("Fill").AddComponent<SpriteRenderer>();
                captureFill.transform.SetParent(captureRing, false);
                captureFill.sprite = art.circle;
                captureFill.color = new Color(0.35f, 0.6f, 1f, 0.45f);
                captureFill.sortingOrder = -18999;
                captureFill.transform.localScale = Vector3.zero;
            }
        }

        void UpdateCapture(float dt)
        {
            int players = 0, enemies = 0, enemiesLeft = 0;
            foreach (var s in Soldier.All)
            {
                if (s == null || !s.IsAlive || s.IsElevated) continue;
                if (s.Team == Team.Enemy) enemiesLeft++;
                if ((s.Ground - CapturePoint).magnitude > 1.9f) continue;
                if (s.Team == Team.Player) { if (s.Squad == null || !s.Squad.Routed) players++; }
                else enemies++;
            }

            if (players > 0 && enemies == 0) CaptureProgress += dt / Castle.CaptureTime;
            else if (players == 0 && enemies > 0) CaptureProgress -= dt / (Castle.CaptureTime * 2f);
            CaptureProgress = Mathf.Clamp01(CaptureProgress);
            if (captureFill != null) captureFill.transform.localScale = Vector3.one * CaptureProgress;

            if (CaptureProgress >= 1f || enemiesLeft == 0)
            {
                Hint?.Invoke("Kale düştü!");
                if (captureRing != null) Destroy(captureRing.gameObject);
                Capturing = false;
                EndBattle(true);
            }
        }

        void EndBattle(bool victory)
        {
            if (State != BattleState.Playing) return;
            State = victory ? BattleState.Won : BattleState.Lost;
            if (victory) { Sfx.Music("music_victory", false, "music_map"); Sfx.Coins(true); }
            else { Sfx.StopMusic(); Sfx.Alert(); }

            // Yarısından fazlası hayatta kalan bölükler orduya geri döner
            int returned = 0;
            foreach (var s in PlayerSquads)
            {
                if (s.Alive * 2 >= s.StartSize && s.Alive > 0) { SaveSystem.Data.AddArmy(s.Data.unitId, 1); returned++; }
                else { lostSquads++; lostUnits.Add(s.Data.unitId); }
                s.Cleanup();
            }

            var result = new BattleResult
            {
                cityId = Castle.cityId,
                cityName = Castle.displayName,
                victory = victory,
                squadsLost = lostSquads
            };

            if (victory)
            {
                float timeRatio = TimeLimit > 0f ? TimeLeft / TimeLimit : 1f;
                result.stars = 1 + (timeRatio > 0.35f ? 1 : 0) + (lostSquads == 0 ? 1 : 0);
                result.goldEarned = Mathf.RoundToInt(Castle.goldReward * (0.5f + 0.25f * (result.stars - 1)) * 0.6f);
                SaveSystem.Data.gold += result.goldEarned;
                SaveSystem.Data.Conquer(Castle.cityId);
                // düşman askerleri kaçar
                foreach (var sq in EnemySquads)
                    foreach (var m in sq.Members.ToArray())
                        if (m != null) m.TakeDamage(9999f, DamageType.Melee);
                PlayConquest(result);
            }

            // rakip şehrine kaybedilen savaş: kaybedilen bölüklerin yarısı esir düşer, kral fidye ister
            if (!victory && !string.IsNullOrEmpty(GameSession.SelectedRivalId) && lostUnits.Count > 0)
            {
                var data = SaveSystem.Data;
                data.pendingCaptives.Clear();
                for (int i = 0; i < lostUnits.Count; i += 2) data.pendingCaptives.Add(lostUnits[i]);
                data.pendingCaptor = GameSession.SelectedRivalId;
                data.pendingCaptiveCity = Castle.displayName;
            }

            SaveSystem.Save();
            GameSession.LastResult = result;
            pendingResult = result;
            endDelay = victory ? 1.0f + Field.TowerSprites.Count * 0.18f + 1.8f : 0.6f;
        }

        /// <summary>Fetih: kuleler sırayla maviye döner, parıltılar ve büyük "FETHEDİLDİ!" yazısı.</summary>
        void PlayConquest(BattleResult result)
        {
            var art = GameArt.Instance;
            var srs = new List<SpriteRenderer>();
            var variants = new List<int>();
            foreach (var kv in Field.TowerSprites)
            {
                if (kv.Key == null) continue;
                var tower = kv.Key.GetComponent<Tower>();
                if (tower != null && tower.IsDestroyed) continue;
                srs.Add(kv.Key);
                variants.Add(kv.Value);
            }
            // kapıdan içeri doğru: önce öndeki kuleler
            var order = new List<int>();
            for (int i = 0; i < srs.Count; i++) order.Add(i);
            order.Sort((a, b) => srs[a].transform.position.y.CompareTo(srs[b].transform.position.y));
            var sortedSr = new List<SpriteRenderer>();
            var sortedVar = new List<int>();
            foreach (var i in order) { sortedSr.Add(srs[i]); sortedVar.Add(variants[i]); }

            Hint?.Invoke("");
            FX.Run(ConquestRoutine(art, sortedSr, sortedVar, result));
        }

        System.Collections.IEnumerator ConquestRoutine(GameArt art, List<SpriteRenderer> towers, List<int> variants, BattleResult result)
        {
            yield return new WaitForSeconds(0.6f);
            bool done = false;
            FX.FlipTowers(towers, i => art != null ? art.Tower(CastleColor.Blue, variants[i]) : null,
                new Color(1f, 0.85f, 0.3f), 0.18f, () => done = true);
            while (!done) yield return null;
            foreach (var g in Field.Gates)
                if (g != null) FX.Sparkles(g.transform.position + Vector3.up * 0.6f, new Color(0.45f, 0.7f, 1f), 18, 1.1f);
            FX.Banner("FETHEDİLDİ!", UIFactory.Blue, 1.2f, result.cityName + " artık senin");
        }

        public void ReturnToMap()
        {
            GameSession.SelectedCastle = null;
            Soldier.All.Clear();
            SceneManager.LoadScene(mapSceneName);
        }
    }
}
