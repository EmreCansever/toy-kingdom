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
        MapTradeLayer trade;
        float incomeTimer;
        float countdownTimer;

        void Start()
        {
            UIFactory.EnsureEventSystem();
            maxOfflineHours = Mathf.Min(maxOfflineHours, 2f); // yokken en fazla 2 saatlik gelir
            cities = FindObjectsByType<MapCity>(FindObjectsSortMode.None);
            Sfx.Music("music_map");
            ui = gameObject.AddComponent<MapUI>();
            ui.Build(this);

            // kamera karo alanının dışına taşmasın
            float n = rig.bounds.width / (2f * Iso.HalfW * 0.85f);
            rig.useDiamond = true;
            rig.diamondCenter = new Vector2(0f, n * Iso.HalfH + Iso.GroundTop);
            rig.diamondHalfW = n * Iso.HalfW * 0.82f;
            rig.diamondHalfH = n * Iso.HalfH * 0.82f;
            EnsureCitiesReachable();

            RivalAI.Setup(SaveSystem.Data, Infos());
            trade = MapTradeLayer.Create(cities);
            var offlineReport = Simulate(false);
            RefreshAll();
            CityNameplates.Create(cities);
            coach = Coach.Create();
            if (!GameSession.TitleShown) TitleScreen.Show(this, ui, cities);
            ui.BuildRivals();

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
                    // gururlu kral sınırına dayanan fatihi uyarır
                    if (c != null)
                        foreach (var l in c.links)
                        {
                            var owner = l.to != null ? l.to.RivalId : null;
                            var rs = owner != null ? SaveSystem.Data.Rival(owner) : null;
                            if (rs != null && rs.alive && RivalAI.Get(owner).trait == RivalAI.KingTrait.Proud) { Court.BorderThreat(SaveSystem.Data, rs, c.DisplayName); break; }
                        }
                    ui.Toast(r.cityName + " artık senin! +" + r.goldEarned + " altın" + (needGov ? "\nŞehre dokun ve bir vali ata" : ""));
                }
                else ui.Toast(r.cityName + " savunmayı tuttu");
                GameSession.LastResult = null;
            }
            else if (offlineReport.gold > 0) ui.Toast("Yokken krallığın " + offlineReport.gold + " altın kazandı", MapUI.Level.Info);
            foreach (var m in offlineReport.messages) ui.Toast(m);
            if (!string.IsNullOrEmpty(SaveSystem.Data.pendingMessage))
            {
                FX.Banner("ZAFER!", UIFactory.Gold, 2f, SaveSystem.Data.pendingMessage);
                ui.Toast(SaveSystem.Data.pendingMessage);
                SaveSystem.Data.pendingMessage = "";
                SaveSystem.Save();
            }
            HandleRivalReport(lastRivalReport, false);
            lastRivalReport = new RivalAI.Report();
            HandleCourt(false);
            HandleTrade();
            HandleReputation();
            HandleTutorial();
            HandleEndgame();
            if (SaveSystem.Data.gameOver == Endgame.Won || SaveSystem.Data.gameOver == Endgame.Lost) ShowEnd();
            SyncArmies();
        }

        string endMsg;
        bool endedNow;

        /// <summary>Darbe uyarıları ve oyun sonu ekranı.</summary>
        void HandleEndgame()
        {
            if (endMsg != null) { ui.Toast(endMsg); endMsg = null; }
            if (!endedNow) return;
            endedNow = false;
            SaveSystem.Save();
            ShowEnd();
        }

        void ShowEnd() => ui.ShowEnd(OwnedCount(), CityCount, NewGame, () =>
        {
            SaveSystem.Data.gameOver = Endgame.WonContinuing;
            SaveSystem.Save();
        });

        /// <summary>Kaydı silip haritayı baştan kurar.</summary>
        public void NewGame()
        {
            SaveSystem.ResetSave();
            GameSession.LastResult = null;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>Vezirin görevleri: tamamlanan görevin ödülü ve sıradaki görev.</summary>
        void HandleTutorial()
        {
            string done = Tutorial.Check(SaveSystem.Data);
            if (done != null)
            {
                FX.Banner("GÖREV TAMAM", UIFactory.Gold, 1.2f, done.Replace("Görev tamamlandı: ", ""));
                Sfx.Coins(true);
                ui.Toast(done);
                SaveSystem.Save();
                HandleCourt(true);
            }
            ui.RefreshQuest();
        }

        /// <summary>İtibar değişimlerini haber olarak gösterir (küçükler alt şeritte, büyükler üstte).</summary>
        void HandleReputation()
        {
            var list = Reputation.Take();
            if (list.Count == 0) return;
            foreach (var kv in list)
            {
                string sign = kv.Key > 0f ? "+" : "";
                string text = Mathf.Abs(kv.Key) >= 10f && kv.Value.StartsWith("Artık") ? kv.Value : "İtibar " + sign + Mathf.RoundToInt(kv.Key) + ": " + kv.Value;
                ui.Toast(text, Mathf.Abs(kv.Key) >= 5f ? MapUI.Level.Important : MapUI.Level.Info);
            }
            SaveSystem.Save();
            ui.RefreshTop();
        }

        /// <summary>Ticaretten gelenler: kervan kazançları ve eşkıya haberleri.</summary>
        void HandleTrade()
        {
            var rep = Trade.Take();
            if (!rep.Any) return;
            foreach (var m in rep.messages) ui.Toast(m);
            foreach (var m in rep.info) ui.Toast(m, MapUI.Level.Info);
            foreach (var g in rep.gains) if (trade != null) trade.ShowGain(g.Key, g.Value);
            if (rep.gains.Count > 0) Sfx.Coins();
            if (rep.messages.Count > 0) SaveSystem.Save();
            ui.RefreshTop();
        }

        /// <summary>Şehir bilgileri kimliğe göre (ticaret paneli için).</summary>
        public IDictionary<string, CityInfo> InfoDict()
        {
            var d = new Dictionary<string, CityInfo>();
            foreach (var c in cities) d[c.CityId] = c.Info;
            return d;
        }

        /// <summary>
        /// Köşedeki şehirler (başkent) de ekranın ortasına alınabilsin: kamera sınırları her kaleyi kapsayacak kadar genişletilir.
        /// </summary>
        void EnsureCitiesReachable()
        {
            var cam = rig.GetComponent<Camera>();
            if (cam == null) return;
            float h = cam.orthographicSize, w = h * cam.aspect;
            foreach (var c in cities)
            {
                Vector2 p = c.transform.position + Vector3.up * 0.3f;
                float dx = p.x - rig.diamondCenter.x, dy = p.y - rig.diamondCenter.y;
                float k = Mathf.Abs(dx) / rig.diamondHalfW + Mathf.Abs(dy) / rig.diamondHalfH;
                if (k > 1f) { rig.diamondHalfW *= k; rig.diamondHalfH *= k; }
                var b = rig.bounds;
                if (p.y - h < b.yMin) b.yMin = p.y - h;
                if (p.y + h > b.yMax) b.yMax = p.y + h;
                if (p.x - w < b.xMin) b.xMin = p.x - w;
                if (p.x + w > b.xMax) b.xMax = p.x + w;
                rig.bounds = b;
            }
        }

        /// <summary>Mektup cevabı (arayüzden).</summary>
        public string AnswerLetter(Letter l, int option)
        {
            string res = Court.Resolve(SaveSystem.Data, Infos(), l, option);
            HandleReputation();
            if (l.resolved && !string.IsNullOrEmpty(res)) { SaveSystem.Data.AddLog(l.title + ": " + res, 1); SaveSystem.Data.SetFlag(Tutorial.FlagAnswered); }
            SaveSystem.Save();
            HandleCourt(true);
            ui.RefreshTop();
            return res;
        }

        /// <summary>Diplomasi panelindeki eylemler.</summary>
        public string Diplomacy(string action, RivalState r)
        {
            var data = SaveSystem.Data;
            string res;
            switch (action)
            {
                case "gift": res = Court.SendGift(data, r); data.SetFlag(Tutorial.FlagDiplomacy); break;
                case "peace": res = Court.ProposePeace(data, r); data.SetFlag(Tutorial.FlagDiplomacy); break;
                case "tribute": res = Court.DemandTribute(data, r); break;
                case "incite": res = RivalWars.Incite(data, r); break;
                default: res = Court.DeclareWar(data, r); break;
            }
            ui.Toast(res);
            SaveSystem.Save();
            HandleCourt(true);
            HandleReputation();
            ui.RefreshTop();
            return res;
        }

        /// <summary>Saraydan gelenleri işler: yeni mektupları açar, sonuçları gösterir.</summary>
        public void HandleCourt(bool live)
        {
            var rep = Court.Take();
            if (!rep.Any) { ui.RefreshLetters(); return; }
            foreach (var m in rep.messages) ui.Toast(m);
            foreach (var l in rep.newLetters) SaveSystem.Data.AddLog("Mektup geldi: " + l.title, l.NeedsAnswer ? 1 : 0);
            if (rep.newLetters.Count > 0) Sfx.Play("sfx_letter", 0.8f, 0.03f, 1f);
            foreach (var id in rep.cityChanged)
            {
                var c = Find(id);
                if (c != null && c.IsConquered) c.PlayConquest();
            }
            if (rep.attacksStarted.Count > 0)
            {
                var r = SaveSystem.Data.Rival(rep.attacksStarted[0]);
                var target = r != null ? Find(r.attackTarget) : null;
                if (target != null) rig.FocusOn(target.doorPoint);
                FX.Banner("SALDIRI!", UIFactory.Red, 1.4f, r != null ? RivalAI.Get(r.id).name + " ordusu yolda" : null);
            }
            SaveSystem.Save();
            SyncArmies();
            RefreshAll();
            ui.RefreshRivals();
            ui.RefreshLetters();
            // cevap bekleyen yeni mektup varsa hemen aç
            foreach (var l in rep.newLetters)
                if (l.NeedsAnswer || l.kind == LetterKind.Debt) { ui.ShowLetter(l); break; }
        }

        List<CityInfo> Infos()
        {
            var infos = new List<CityInfo>();
            foreach (var c in cities) infos.Add(c.Info);
            return infos;
        }

        RivalAI.Report lastRivalReport = new RivalAI.Report();
        readonly Dictionary<string, MapRivalArmy> armies = new Dictionary<string, MapRivalArmy>();

        /// <summary>Yoldaki rakip ordularını haritada gösterir.</summary>
        void SyncArmies()
        {
            foreach (var r in SaveSystem.Data.rivals)
            {
                bool has = armies.TryGetValue(r.id, out var a) && a != null;
                if (r.Attacking && !has)
                {
                    var from = Find(r.attackFrom);
                    var to = Find(r.attackTarget);
                    if (from == null || to == null) continue;
                    var route = FindRoute(from, to);
                    if (route == null) continue;
                    route.Insert(0, from.doorPoint);
                    armies[r.id] = MapRivalArmy.Create(r, route);
                }
            }
        }

        void HandleRivalReport(RivalAI.Report rep, bool live)
        {
            if (rep == null || !rep.Any) return;
            foreach (var id in rep.captured)
            {
                var c = Find(id);
                if (c == null) continue;
                string owner = c.RivalId;
                if (owner != null) c.PlayCapturedByRival(owner);
            }
            foreach (var id in rep.defended)
            {
                var c = Find(id);
                if (c != null) c.PlayDefended();
            }
            if (live && rep.attacksStarted.Count > 0)
            {
                var r = SaveSystem.Data.Rival(rep.attacksStarted[0]);
                var target = r != null ? Find(r.attackTarget) : null;
                if (target != null) rig.FocusOn(target.doorPoint);
                FX.Banner("SALDIRI!", UIFactory.Red, 1.4f, r != null ? RivalAI.Get(r.id).name + " ordusu yolda" : null);
            }
            foreach (var m in rep.messages) ui.Toast(m);
            SyncArmies();
            RefreshAll();
            ui.RefreshRivals();
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

        /// <summary>Bu şehir, krallığın en yakın şehrinden yol üzerinden kaç şehir ötede (1 = komşu, saldırılabilir). Yol yoksa -1.</summary>
        public int RoadHops(MapCity target)
        {
            var dist = new Dictionary<MapCity, int>();
            var q = new Queue<MapCity>();
            foreach (var c in cities) if (c.IsConquered) { dist[c] = 0; q.Enqueue(c); }
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c == target) return dist[c];
                // fethedilmemiş şehirlerin ötesine de bakılır ama her biri bir adım sayılır
                foreach (var l in c.links)
                {
                    if (l.to == null || dist.ContainsKey(l.to)) continue;
                    dist[l.to] = dist[c] + 1;
                    q.Enqueue(l.to);
                }
            }
            return dist.TryGetValue(target, out int d) ? d : -1;
        }

        Coach coach;

        static Vector3 CityPoint(MapCity c) => Vector3.Lerp(c.doorPoint, c.LabelAnchor, 0.35f);

        /// <summary>Öğretici adımına göre dokunulacak yeri gösterir (görev kartı gizlenmişse göstermez).</summary>
        void UpdateCoach()
        {
            if (coach == null) return;
            var d = SaveSystem.Data;
            var step = Tutorial.Current(d);
            if (step == null || ui.Paused || SettingsPanel.Get(SettingsPanel.QuestKey, false) || hero.IsMoving) { coach.Hide(); return; }
            var panel = ui.CityPanelView;
            bool modal = ui.IsModalOpen;
            switch (step.title)
            {
                case "İlk fetih":
                {
                    if (ui.AttackOpen)
                    {
                        if (ui.AttackButton != null) coach.PointRect(ui.AttackButton, "Saldır'a bas: savaş başlasın");
                        else coach.Hide();
                        return;
                    }
                    if (modal) { coach.Hide(); return; }
                    MapCity best = null;
                    foreach (var c in cities)
                        if (CanAttack(c) && !c.IsRebel && (best == null || c.Difficulty < best.Difficulty)) best = c;
                    if (best != null) coach.PointWorld(CityPoint(best), "Bu şehre dokun: kahramanın oraya yürür");
                    else coach.Hide();
                    return;
                }
                case "Bir vali ata":
                    if (panel != null) { if (panel.HireButton != null) coach.PointRect(panel.HireButton, "Bir aday seç ve vali yap"); else coach.Hide(); return; }
                    PointOwned(modal, c => !d.State(c.CityId).hasGovernor, "Fethettiğin şehre dokun");
                    return;
                case "Valinin gönlünü kazan":
                    if (panel != null) { if (panel.GiftButton != null) coach.PointRect(panel.GiftButton, "Hediye gönder: sadakat artar"); else coach.Hide(); return; }
                    PointOwned(modal, c => d.State(c.CityId).hasGovernor, "Valili şehrine dokun");
                    return;
                case "Halkı yatıştır":
                    if (panel != null) { if (panel.FeastButtonRect != null) coach.PointRect(panel.FeastButtonRect, "Ziyafet ver ya da vergiyi düşür"); else coach.Hide(); return; }
                    PointOwned(modal, c => true, "Şehrine dokun: halkın huzurunu gör");
                    return;
                case "Ordunu büyüt": PointButton(modal, "ArmyButton", "Ordu'ya dokun ve bir bölük al"); return;
                case "Yola devriye koy": PointButton(modal, "TradeButton", "Ticaret'e dokun, bir yol seç ve + ile devriye koy"); return;
                case "Vezirin mektubunu cevapla": PointButton(modal, "LettersButton", "Mektuplar'ı aç ve vezire cevap ver"); return;
                case "Komşu krallarla tanış": PointButton(modal, "DiplomacyButton", "Diplomasi'den bir krala hediye gönder"); return;
                default: coach.Hide(); return;
            }
        }

        void PointOwned(bool modal, Func<MapCity, bool> want, string msg)
        {
            if (modal) { coach.Hide(); return; }
            foreach (var c in cities)
                if (c.IsConquered && !c.isHomeCity && want(c)) { coach.PointWorld(CityPoint(c), msg); return; }
            coach.Hide();
        }

        void PointButton(bool modal, string name, string msg)
        {
            var rt = modal ? null : ui.FindUI(name);
            if (rt != null) coach.PointRect(rt, msg); else coach.Hide();
        }

        long lastFrameTicks;

        void Update()
        {
            // bir pencere açıkken ya da duraklatınca krallıkta zaman durur
            long nowTicks = DateTime.UtcNow.Ticks;
            bool paused = ui.IsModalOpen || ui.Paused || TitleScreen.IsOpen;
            if (paused && lastFrameTicks > 0) Clock.Shift(SaveSystem.Data, nowTicks - lastFrameTicks);
            lastFrameTicks = nowTicks;
            if (!paused) incomeTimer += Time.deltaTime;
            if (incomeTimer >= 5f)
            {
                incomeTimer = 0f;
                var rep = Simulate(true);
                HandleRivalReport(lastRivalReport, true);
                lastRivalReport = new RivalAI.Report();
                HandleCourt(true);
                HandleTrade();
                HandleReputation();
                HandleTutorial();
                HandleEndgame();
                if (rep.gold > 0 || rep.messages.Count > 0)
                {
                    foreach (var id in rep.newRebels)
                    {
                        var c = Find(id);
                        if (c != null) { c.PlayRebellion(); rig.FocusOn(c.doorPoint); }
                    }
                    foreach (var id in rep.urgent)
                    {
                        var c = Find(id);
                        if (c == null) continue;
                        rig.FocusOn(c.doorPoint);
                        FX.Shake(c.transform, 0.06f, 0.5f);
                        FX.Banner("İSYAN EŞİĞİ", UIFactory.Red, 1.4f, c.DisplayName + " için " + (int)KingdomSim.RevoltGrace + " dakikan var");
                        break;
                    }
                    foreach (var m in rep.messages) ui.Toast(m);
                    RefreshAll();
                }
            }

            // saldırı geri sayımı her saniye güncellensin
            countdownTimer += Time.deltaTime;
            if (countdownTimer >= 1f)
            {
                countdownTimer = 0f;
                foreach (var r in SaveSystem.Data.rivals)
                    if (r.Attacking) { var t = Find(r.attackTarget); if (t != null) t.RefreshVisual(false); }
                foreach (var c in cities)
                    if (!c.isHomeCity && c.IsConquered && c.State.revoltTimer >= 0f) c.RefreshVisual(false);
            }

            if (TitleScreen.IsOpen) { coach?.Hide(); rig.inputEnabled = false; rig.TryConsumeTap(out _); return; }
            UpdateCoach();
            rig.inputEnabled = !ui.IsModalOpen;
            if (!rig.TryConsumeTap(out Vector3 world)) return;
            if (hero.IsMoving) return;

            var hits = Physics2D.OverlapPointAll(world);
            foreach (var h in hits)
            {
                var road = h.GetComponent<MapRoadTap>();
                if (road != null) { ui.ShowTrade(road.key); return; }
            }
            MapCity city = null;
            foreach (var h in hits) { city = h.GetComponentInParent<MapCity>(); if (city != null) break; }
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
            // saldırılamayan şehre yürümenin anlamı yok: sadece bilgisini göster
            if (!CanAttack(city))
            {
                ShowCity(city);
                return;
            }
            var route = FindRoute(hero.CurrentCity, city);
            if (route == null)
            {
                ui.Hint("Oraya giden bir yol yok");
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
                ui.Hint("Ordun yok! Önce Ordu menüsünden bölük al");
                return;
            }
            GameSession.SelectedCastle = BattleCastle(city);
            GameSession.SelectedRivalId = city.RivalId;
            var rival = city.RivalId != null ? SaveSystem.Data.Rival(city.RivalId) : null;
            if (rival != null && rival.AtPeace) ui.Toast(Court.DeclareWar(SaveSystem.Data, rival));
            string allyMsg = RivalWars.OnPlayerAttacked(SaveSystem.Data, rival);
            if (allyMsg != null) SaveSystem.Data.AddLog(allyMsg, 1);
            SaveSystem.Save();
            SceneManager.LoadScene(battleSceneName);
        }

        /// <summary>Saldırılacak kale: asi şehirde isyancıların, rakip şehrinde rakip krallığın güçlendirilmiş kopyası.</summary>
        CastleData BattleCastle(MapCity city)
        {
            if (city.castle == null) return null;
            var db = UnitDatabase.Instance;
            var inf = db != null ? db.Get("infantry") : null;
            var kn = db != null ? db.Get("knight") : null;
            CastleData c = city.castle;
            int tier = 0; // tarafsız ve asi şehirler normal zorlukta
            if (city.IsRebel) c = KingdomSim.RebelCastle(city.castle, city.State, inf, kn);
            else
            {
                var rival = city.RivalId != null ? SaveSystem.Data.Rival(city.RivalId) : null;
                if (rival != null)
                {
                    c = RivalAI.RivalCastle(city.castle, rival, city.State, inf, kn);
                    tier = rival.capitalId == city.CityId ? 2 : 1;
                }
            }
            return Siege.Harden(c, tier);
        }

        /// <summary>Geçen her tam dakika için krallık simülasyonunu ilerletir (gelir, huzursuzluk, sadakat, isyan).</summary>
        KingdomSim.TickReport Simulate(bool online)
        {
            var data = SaveSystem.Data;
            long now = DateTime.UtcNow.Ticks;
            if (data.gameOver == Endgame.Lost) { data.lastIncomeTicks = now; return new KingdomSim.TickReport(); } // taht kaybedildi: zaman durdu
            if (data.lastIncomeTicks <= 0 || data.lastIncomeTicks > now) data.lastIncomeTicks = now;
            long minutes = (now - data.lastIncomeTicks) / TimeSpan.TicksPerMinute;
            if (minutes <= 0)
            {
                // dakika dolmasa da yoldaki saldırıların varışı kontrol edilir
                var inf = Infos();
                RivalAI.Advance(data, inf, 0, online, lastRivalReport);
                Court.Tick(data, inf, 0, online);
                Trade.Tick(data, inf, 0, online);
                if (Endgame.Tick(data, 0, online, out string m0)) endedNow = true;
                if (m0 != null) endMsg = m0;
                if (lastRivalReport.Any) SaveSystem.Save();
                return new KingdomSim.TickReport();
            }

            // oyun kapalıyken dünya durur: sadece çok az pasif gelir gelir (savaş dönüşü gibi kısa aralar hariç)
            if (!online && minutes > BattleGapMinutes) return Away(data, now);

            long cap = (long)(maxOfflineHours * 60f);
            int run = (int)Math.Min(minutes, cap);
            var infos = Infos();

            var rep = KingdomSim.Advance(data, infos, run, online);
            RivalAI.Advance(data, infos, run, online, lastRivalReport);
            Court.Tick(data, infos, run, online);
            Trade.Tick(data, infos, run, online);
            if (Endgame.Tick(data, run, online, out string m1)) endedNow = true;
            if (m1 != null) endMsg = m1;
            data.lastIncomeTicks = minutes > cap ? now : data.lastIncomeTicks + minutes * TimeSpan.TicksPerMinute;
            SaveSystem.Save();
            return rep;
        }

        const int BattleGapMinutes = 6;
        const float AwayIncomeShare = 0.05f;
        const int AwayCapMinutes = 240;

        /// <summary>Uzun aradan dönüş: geçen süre için gelirin yalnızca %5'i (en fazla 4 saatlik), dünya kaldığı yerden devam eder.</summary>
        KingdomSim.TickReport Away(SaveData data, long now)
        {
            long delta = now - data.lastIncomeTicks;
            int mins = (int)Math.Min(delta / TimeSpan.TicksPerMinute, AwayCapMinutes);
            int gain = Mathf.Max(0, Mathf.RoundToInt(IncomePerMinute() * AwayIncomeShare * mins));
            data.gold += gain;
            Clock.Shift(data, delta); // mektuplar, saldırılar, kervanlar, bekleme süreleri: hepsi durdu
            data.lastIncomeTicks = now;
            lastFrameTicks = now;
            SaveSystem.Save();
            var rep = new KingdomSim.TickReport();
            rep.gold = gain;
            return rep;
        }

        long backgroundTicks;

        /// <summary>Telefonda uygulama arka plana atılınca da dünya durur.</summary>
        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                backgroundTicks = DateTime.UtcNow.Ticks;
                SaveSystem.Save();
                return;
            }
            if (backgroundTicks <= 0) return;
            long gap = DateTime.UtcNow.Ticks - backgroundTicks;
            backgroundTicks = 0;
            if (gap < BattleGapMinutes * TimeSpan.TicksPerMinute) return;
            var rep = Simulate(false);
            if (rep.gold > 0) ui.Toast("Yokken krallığın " + rep.gold + " altın kazandı", MapUI.Level.Info);
            RefreshAll();
        }

        public void RefreshAll()
        {
            if (ui != null) HandleReputation();
            foreach (var c in cities) { c.roadHops = c.IsConquered ? 0 : RoadHops(c); c.RefreshVisual(CanAttack(c)); }
            if (ui != null) ui.RefreshTop();
        }

        public int IncomePerMinute()
        {
            int n = 0;
            if (cities != null) foreach (var c in cities) if (c.IsConquered) n += c.Income;
            return n;
        }

        public int OwnedCount()
        {
            int n = 0;
            foreach (var c in cities) if (c.IsConquered) n++;
            return n;
        }

        public int CityCount => cities != null ? cities.Length : 0;

        void OnApplicationQuit() => SaveSystem.Save();
    }
}
