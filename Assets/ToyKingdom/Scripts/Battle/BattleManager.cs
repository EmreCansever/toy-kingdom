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
        public List<Squad> PlayerSquads { get; } = new List<Squad>();
        public List<Squad> EnemySquads { get; } = new List<Squad>();
        public Camera Cam { get; private set; }

        public event Action<UnitData> SelectedUnitChanged;
        public event Action ArmyChanged;
        public event Action<BattleResult> BattleEnded;
        public event Action<string> Hint;

        float reinforcementTimer;
        int reinforcementsLeft;
        int deployedSquads;
        int lostSquads;
        float endDelay = -1f;
        BattleResult pendingResult;

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

            Field = gameObject.AddComponent<BattleField>();
            Field.Build(Castle);
            if (Cam != null) Field.FitCamera(Cam, 0.18f);
            foreach (var g in Field.Gates) g.Destroyed += OnGateDestroyed;
            foreach (var t in Field.Towers) t.Destroyed += _ => Hint?.Invoke("Bir kule yıkıldı!");

            Command = new CommandPoints(Castle.startingCommandPoints, Castle.maxCommandPoints, Castle.commandRegenPerSecond);
            TimeLeft = Castle.timeLimit;
            reinforcementTimer = Castle.reinforcementInterval;
            reinforcementsLeft = Castle.reinforcementCount;

            SpawnGarrison();
        }

        void Start()
        {
            gameObject.AddComponent<BattleHUD>();
            gameObject.AddComponent<PathDrawer>();
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
            // kapıdan çıkıp yerlerine yürürler
            Vector3 gate = Field.GateSpawnPoint(reinforcementsLeft);
            var start = new List<Vector3>();
            for (int i = 0; i < posts.Count; i++) start.Add(Field.Constrain(gate + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.3f)));
            sq.SpawnAtPosts(start, 0f, false);
            sq.ReassignPosts(posts);
            sq.SetGuardRanges(4.5f, 7f);
            EnemySquads.Add(sq);
            Hint?.Invoke("Kapıdan takviye kuvvet çıktı!");
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

            if (TimeLeft <= 0f)
            {
                TimeLeft = 0f;
                EndBattle(false);
                return;
            }

            // Ordu tükendiyse ve sahada kimse kalmadıysa kaybedilir
            if (deployedSquads > 0 && !AnyPlayerAlive() && !CanAffordAnything(true)) EndBattle(false);
        }

        bool AnyPlayerAlive()
        {
            foreach (var s in PlayerSquads) if (!s.IsWiped) return true;
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
            if (State != BattleState.Playing) return;
            Hint?.Invoke("Kapı kırıldı! Kale düşüyor...");
            EndBattle(true);
        }

        void EndBattle(bool victory)
        {
            if (State != BattleState.Playing) return;
            State = victory ? BattleState.Won : BattleState.Lost;

            // Yarısından fazlası hayatta kalan bölükler orduya geri döner
            int returned = 0;
            foreach (var s in PlayerSquads)
            {
                if (s.Alive * 2 >= s.StartSize && s.Alive > 0) { SaveSystem.Data.AddArmy(s.Data.unitId, 1); returned++; }
                else lostSquads++;
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
                float timeRatio = Castle.timeLimit > 0f ? TimeLeft / Castle.timeLimit : 1f;
                result.stars = 1 + (timeRatio > 0.35f ? 1 : 0) + (lostSquads == 0 ? 1 : 0);
                result.goldEarned = Mathf.RoundToInt(Castle.goldReward * (0.5f + 0.25f * (result.stars - 1)));
                SaveSystem.Data.gold += result.goldEarned;
                SaveSystem.Data.Conquer(Castle.cityId);
                // düşman askerleri kaçar
                foreach (var sq in EnemySquads)
                    foreach (var m in sq.Members.ToArray())
                        if (m != null) m.TakeDamage(9999f, DamageType.Melee);
            }

            SaveSystem.Save();
            GameSession.LastResult = result;
            pendingResult = result;
            endDelay = victory ? 1.6f : 0.6f;
        }

        public void ReturnToMap()
        {
            GameSession.SelectedCastle = null;
            Soldier.All.Clear();
            SceneManager.LoadScene(mapSceneName);
        }
    }
}
