using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ToyKingdom
{
    /// <summary>Harita sahnesinin beyni: şehirler, gelir, dokunma, kahramanın yolculuğu ve saldırıya geçiş.</summary>
    public class MapController : MonoBehaviour
    {
        public CameraRig rig;
        public MapHero hero;
        public string battleSceneName = "Battle";
        [Tooltip("Oyun kapalıyken en fazla kaç saatlik gelir birikir")]
        public float maxOfflineHours = 8f;

        MapCity[] cities;
        MapUI ui;
        float incomeTimer;

        void Start()
        {
            UIFactory.EnsureEventSystem();
            cities = FindObjectsByType<MapCity>(FindObjectsSortMode.None);
            ui = gameObject.AddComponent<MapUI>();
            ui.Build(this);

            var offlineReport = Simulate(false);
            RefreshAll();

            MapCity start = Find(SaveSystem.Data.heroCityId);
            if (start == null) start = Array.Find(cities, c => c.isHomeCity);
            if (start != null)
            {
                hero.PlaceAt(start);
                rig.FocusOn(start.doorPoint, true);
            }

            string anim = SaveSystem.Data.pendingConquestAnim;
            if (!string.IsNullOrEmpty(anim))
            {
                SaveSystem.Data.pendingConquestAnim = "";
                SaveSystem.Save();
                var won = Find(anim);
                if (won != null)
                {
                    rig.FocusOn(won.doorPoint, true);
                    won.PlayConquest();
                }
            }

            if (GameSession.LastResult != null)
            {
                var r = GameSession.LastResult;
                if (r.victory)
                {
                    var c = Find(r.cityId);
                    bool needGov = c != null && !c.isHomeCity && !c.State.hasGovernor;
                    ui.Toast(r.cityName + " artık senin! +" + r.goldEarned + " altın" + (needGov ? "\nŞehre dokun ve bir vali ata" : ""));
                }
                else ui.Toast(r.cityName + " savunmayı tuttu");
                GameSession.LastResult = null;
            }
            else if (offlineReport.gold > 0) ui.Toast("Yokken krallığın " + offlineReport.gold + " altın kazandı");
            foreach (var m in offlineReport.messages) ui.Toast(m);
        }

        MapCity Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var c in cities) if (c.CityId == id) return c;
            return null;
        }

        public bool CanAttack(MapCity city)
        {
            if (city.IsConquered || city.castle == null) return false;
            if (city.IsRebel) return true; // krallığın içindeki isyan her zaman bastırılabilir
            foreach (var l in city.links) if (l.to != null && l.to.IsConquered) return true;
            return false;
        }

        void Update()
        {
            incomeTimer += Time.deltaTime;
            if (incomeTimer >= 5f)
            {
                incomeTimer = 0f;
                var rep = Simulate(true);
                if (rep.gold > 0 || rep.messages.Count > 0)
                {
                    foreach (var id in rep.newRebels)
                    {
                        var c = Find(id);
                        if (c != null) { c.PlayRebellion(); rig.FocusOn(c.doorPoint); }
                    }
                    foreach (var m in rep.messages) ui.Toast(m);
                    RefreshAll();
                }
            }

            rig.inputEnabled = !ui.IsModalOpen;
            if (!rig.TryConsumeTap(out Vector3 world)) return;
            if (hero.IsMoving) return;

            Collider2D hit = Physics2D.OverlapPoint(world);
            MapCity city = hit != null ? hit.GetComponentInParent<MapCity>() : null;
            if (city != null) OnCityTapped(city);
        }

        void OnCityTapped(MapCity city)
        {
            // kendi şehrin: kahraman yürümeden yönetim paneli açılır
            if (city.IsConquered)
            {
                ui.ShowCity(city);
                return;
            }
            if (hero.CurrentCity == city)
            {
                ShowCity(city);
                return;
            }
            var route = FindRoute(hero.CurrentCity, city);
            if (route == null)
            {
                ui.Toast("Oraya giden bir yol yok");
                return;
            }
            rig.FocusOn(city.doorPoint);
            hero.Travel(route, city, () =>
            {
                SaveSystem.Data.heroCityId = city.CityId;
                SaveSystem.Save();
                ShowCity(city);
            });
        }

        void ShowCity(MapCity city)
        {
            if (city.IsConquered)
            {
                ui.ShowCity(city);
                return;
            }
            ui.ShowAttack(city, BattleCastle(city), CanAttack(city), () => StartBattle(city));
        }

        /// <summary>Şehirler arası yol noktalarını bulur (en az şehir geçen rota).</summary>
        List<Vector3> FindRoute(MapCity from, MapCity to)
        {
            if (from == null) return new List<Vector3> { to.doorPoint };
            var prev = new Dictionary<MapCity, RoadLink>();
            var prevCity = new Dictionary<MapCity, MapCity>();
            var queue = new Queue<MapCity>();
            queue.Enqueue(from);
            prevCity[from] = null;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (c == to) break;
                foreach (var l in c.links)
                {
                    if (l.to == null || prevCity.ContainsKey(l.to)) continue;
                    prevCity[l.to] = c;
                    prev[l.to] = l;
                    queue.Enqueue(l.to);
                }
            }
            if (!prevCity.ContainsKey(to)) return null;

            var chain = new List<RoadLink>();
            for (var c = to; c != from; c = prevCity[c]) chain.Add(prev[c]);
            chain.Reverse();
            var points = new List<Vector3>();
            foreach (var l in chain) if (l.points != null) points.AddRange(l.points);
            return points;
        }

        void StartBattle(MapCity city)
        {
            if (SaveSystem.Data.TotalArmy() == 0)
            {
                ui.Toast("Ordun yok! Önce Ordu menüsünden bölük al");
                return;
            }
            GameSession.SelectedCastle = BattleCastle(city);
            SaveSystem.Save();
            SceneManager.LoadScene(battleSceneName);
        }

        /// <summary>Saldırılacak kale: asi şehirde isyancıların güçlendirilmiş kopyası.</summary>
        CastleData BattleCastle(MapCity city)
        {
            if (city.castle == null || !city.IsRebel) return city.castle;
            var db = UnitDatabase.Instance;
            return KingdomSim.RebelCastle(city.castle, city.State, db != null ? db.Get("infantry") : null, db != null ? db.Get("knight") : null);
        }

        /// <summary>Geçen her tam dakika için krallık simülasyonunu ilerletir (gelir, huzursuzluk, sadakat, isyan).</summary>
        KingdomSim.TickReport Simulate(bool online)
        {
            var data = SaveSystem.Data;
            long now = DateTime.UtcNow.Ticks;
            if (data.lastIncomeTicks <= 0 || data.lastIncomeTicks > now) data.lastIncomeTicks = now;
            long minutes = (now - data.lastIncomeTicks) / TimeSpan.TicksPerMinute;
            if (minutes <= 0) return new KingdomSim.TickReport();

            long cap = (long)(maxOfflineHours * 60f);
            int run = (int)Math.Min(minutes, cap);
            var infos = new List<CityInfo>();
            foreach (var c in cities) infos.Add(c.Info);

            var rep = KingdomSim.Advance(data, infos, run, online);
            data.lastIncomeTicks = minutes > cap ? now : data.lastIncomeTicks + minutes * TimeSpan.TicksPerMinute;
            SaveSystem.Save();
            return rep;
        }

        public void RefreshAll()
        {
            foreach (var c in cities) c.RefreshVisual(CanAttack(c));
            if (ui != null) ui.RefreshTop();
        }

        public int OwnedCount()
        {
            int n = 0;
            foreach (var c in cities) if (c.IsConquered) n++;
            return n;
        }

        public int CityCount => cities != null ? cities.Length : 0;

        void OnApplicationPause(bool paused)
        {
            if (paused) SaveSystem.Save();
        }

        void OnApplicationQuit() => SaveSystem.Save();
    }
}
