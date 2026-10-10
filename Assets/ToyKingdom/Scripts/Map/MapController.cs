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

            int offline = CollectIncome();
            RefreshAll();

            MapCity start = Find(SaveSystem.Data.heroCityId);
            if (start == null) start = Array.Find(cities, c => c.isHomeCity);
            if (start != null)
            {
                hero.PlaceAt(start);
                rig.FocusOn(start.doorPoint, true);
            }

            if (GameSession.LastResult != null)
            {
                var r = GameSession.LastResult;
                ui.Toast(r.victory ? r.cityName + " artık senin! +" + r.goldEarned + " altın" : r.cityName + " savunmayı tuttu");
                GameSession.LastResult = null;
            }
            else if (offline > 0) ui.Toast("Yokken krallığın " + offline + " altın kazandı");
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
            foreach (var l in city.links) if (l.to != null && l.to.IsConquered) return true;
            return false;
        }

        void Update()
        {
            incomeTimer += Time.deltaTime;
            if (incomeTimer >= 5f)
            {
                incomeTimer = 0f;
                if (CollectIncome() > 0) ui.RefreshTop();
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
                ui.Toast(city.isHomeCity ? city.DisplayName + ": krallığının kalbi" : city.DisplayName + " dakikada " + city.Income + " altın üretiyor");
                return;
            }
            ui.ShowAttack(city, CanAttack(city), () => StartBattle(city));
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
            GameSession.SelectedCastle = city.castle;
            SaveSystem.Save();
            SceneManager.LoadScene(battleSceneName);
        }

        /// <summary>Geçen her tam dakika için fethedilen şehirlerin gelirini ekler.</summary>
        int CollectIncome()
        {
            var data = SaveSystem.Data;
            long now = DateTime.UtcNow.Ticks;
            if (data.lastIncomeTicks <= 0 || data.lastIncomeTicks > now) data.lastIncomeTicks = now;
            long minutes = (now - data.lastIncomeTicks) / TimeSpan.TicksPerMinute;
            if (minutes <= 0) return 0;

            long cap = (long)(maxOfflineHours * 60f);
            long paid = Math.Min(minutes, cap);
            int perMinute = 0;
            foreach (var c in cities) if (c.IsConquered) perMinute += c.Income;

            int earned = (int)(paid * perMinute);
            data.gold += earned;
            data.lastIncomeTicks = minutes > cap ? now : data.lastIncomeTicks + minutes * TimeSpan.TicksPerMinute;
            SaveSystem.Save();
            return earned;
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
