using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>Harita arayüzü (koddan kurulur): altın, krallık, ordu düğmesi, bildirim, saldırı paneli, dükkân.</summary>
    public class MapUI : MonoBehaviour
    {
        MapController map;
        TMP_Text goldText;
        TMP_Text kingdomText;
        TMP_Text incomeText;
        TMP_Text repText;
        Image questCard;
        TMP_Text questTitle, questBody;
        bool questCollapsed;
        Transform canvasT;
        Image repDot;
        TMP_Text toastText;
        GameObject toastBox;
        AttackPanel attack;
        ArmyShopUI shop;
        CityPanel cityPanel;
        LettersUI letters;
        DiplomacyPanel diplomacy;
        TradePanel tradePanel;
        EndPanel endPanel;
        SettingsPanel settings;
        GameObject pausedChip;
        TMP_Text pauseLabel;
        /// <summary>Oyuncu duraklat düğmesine bastı.</summary>
        public bool Paused { get; private set; }
        TMP_Text letterBadge;
        GameObject letterBadgeRoot;
        Coroutine toastRoutine;

        public bool IsModalOpen => (attack != null && attack.IsOpen) || (shop != null && shop.IsOpen) || (cityPanel != null && cityPanel.IsOpen) ||
                                   (letters != null && letters.IsOpen) || (diplomacy != null && diplomacy.IsOpen) || (tradePanel != null && tradePanel.IsOpen) || (logPanel != null && logPanel.IsOpen) || (endPanel != null && endPanel.IsOpen) || (settings != null && settings.IsOpen);

        public void Build(MapController controller)
        {
            map = controller;
            var canvas = UIFactory.Canvas("MapUI", 10);
            var t = canvas.transform;
            canvasRoot = t;
            canvasT = t;

            // sol üst: altın
            var goldBox = UIFactory.Chip(t, "Gold", new Vector2(300f, 84f));
            UIFactory.Place(goldBox.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(300f, 84f));
            var coin = UIFactory.Panel(goldBox.transform, "Coin", UIFactory.Gold, GameArt.Instance != null ? GameArt.Instance.circle : null);
            UIFactory.Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46f, 0f), new Vector2(52f, 52f));
            var coinIn = UIFactory.Panel(coin.transform, "Inner", UIFactory.Hex("FFD36E"), GameArt.Instance != null ? GameArt.Instance.circle : null);
            UIFactory.Stretch(coinIn.rectTransform, 9f);
            goldText = UIFactory.Text(goldBox.transform, "Value", "0", 40f, Color.white, TextAlignmentOptions.Left, true);
            UIFactory.Place(goldText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(86f, 0f), new Vector2(200f, 70f));
            incomeText = UIFactory.Text(t, "Income", "", 26f, new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.Left);
            UIFactory.Place(incomeText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -110f), new Vector2(620f, 40f));
            incomeText.outlineWidth = 0.22f;
            incomeText.outlineColor = new Color32(20, 24, 36, 220);

            // sol üst: itibar (dokununca etkileri anlatır)
            var rep = UIFactory.Chip(t, "Reputation", new Vector2(380f, 54f));
            UIFactory.Place(rep.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -154f), new Vector2(380f, 54f));
            repDot = UIFactory.Rounded(rep.transform, "Dot", Color.white, 9f);
            UIFactory.Place(repDot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(18f, 18f));
            repDot.raycastTarget = false;
            repText = UIFactory.Text(rep.transform, "Value", "", 26f, Color.white, TextAlignmentOptions.Left, true);
            UIFactory.Stretch(repText.rectTransform, 52f, 0f, 16f, 0f);
            repText.textWrappingMode = TextWrappingModes.NoWrap;
            var repBtn = rep.gameObject.AddComponent<Button>();
            repBtn.transition = Selectable.Transition.None;
            repBtn.onClick.AddListener(() => Hint("İtibar " + Mathf.RoundToInt(SaveSystem.Data.reputation) + ": " + Reputation.Effects()));

            // sol: vezirin görevi (dokununca küçülür/büyür)
            questCard = UIFactory.Chip(t, "Quest", new Vector2(560f, 160f), 22f);
            questCard.rectTransform.anchorMin = questCard.rectTransform.anchorMax = new Vector2(0f, 1f);
            questCard.rectTransform.pivot = new Vector2(0f, 1f);
            questCard.rectTransform.anchoredPosition = new Vector2(24f, -218f);
            questTitle = UIFactory.Text(questCard.transform, "Title", "", 26f, UIFactory.Hex("FFD36E"), TextAlignmentOptions.TopLeft, true);
            UIFactory.Place(questTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -12f), new Vector2(516f, 36f));
            questTitle.textWrappingMode = TextWrappingModes.NoWrap;
            questBody = UIFactory.Text(questCard.transform, "Body", "", 21f, new Color(1f, 1f, 1f, 0.88f), TextAlignmentOptions.TopLeft);
            UIFactory.Place(questBody.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -50f), new Vector2(516f, 100f));
            questBody.enableAutoSizing = true; questBody.fontSizeMin = 15f; questBody.fontSizeMax = 21f;
            var qb = questCard.gameObject.AddComponent<Button>();
            qb.transition = Selectable.Transition.None;
            qb.onClick.AddListener(() => { questCollapsed = !questCollapsed; RefreshQuest(); });

            // sol alt: yakınlaştır / uzaklaştır (parmakla iki parmak da olur)
            var zin = UIFactory.Button(t, "ZoomIn", "+", UIFactory.Hex("4A5568"), 44f);
            UIFactory.Place((RectTransform)zin.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 104f), new Vector2(88f, 84f));
            zin.onClick.AddListener(() => map.rig.ZoomBy(-0.9f));
            var zout = UIFactory.Button(t, "ZoomOut", "–", UIFactory.Hex("4A5568"), 44f);
            UIFactory.Place((RectTransform)zout.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(124f, 104f), new Vector2(88f, 84f));
            zout.onClick.AddListener(() => map.rig.ZoomBy(0.9f));

            // sol alt: krallık
            var kBox = UIFactory.Chip(t, "Kingdom", new Vector2(360f, 64f));
            UIFactory.Place(kBox.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 22f), new Vector2(360f, 64f));
            var crown = UIFactory.Rounded(kBox.transform, "Dot", UIFactory.Hex("5AA2FF"), 8f);
            UIFactory.Place(crown.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(16f, 16f));
            kingdomText = UIFactory.Text(kBox.transform, "Value", "", 28f, Color.white, TextAlignmentOptions.Left, true);
            UIFactory.Stretch(kingdomText.rectTransform, 52f, 0f, 16f, 0f);

            // sağ üst: ordu
            var armyBtn = UIFactory.Button(t, "ArmyButton", "Ordu", UIFactory.Gold, 38f);
            UIFactory.Place((RectTransform)armyBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -20f), new Vector2(230f, 92f));

            // bildirim
            toastBox = UIFactory.Chip(t, "Toast", new Vector2(1000f, 96f), 28f).gameObject;
            UIFactory.Place((RectTransform)toastBox.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(1000f, 96f));
            toastText = UIFactory.Text(toastBox.transform, "Text", "", 30f, Color.white, TextAlignmentOptions.Center, true);
            toastText.enableAutoSizing = true;
            toastText.fontSizeMin = 20f;
            toastText.fontSizeMax = 30f;
            UIFactory.Stretch(toastText.rectTransform, 30f, 8f, 30f, 8f);
            toastBox.GetComponent<Image>().raycastTarget = false;
            toastBox.SetActive(false);

            // ipucu (ilk açılışta)
            attack = AttackPanel.Create(t);
            shop = ArmyShopUI.Create(t, this);
            cityPanel = CityPanel.Create(t, () => map.RefreshAll(), Toast);

            // sağ: saray (alt kenar telefonlarda hareket çubuğuyla çakışmasın diye üstte)
            var dipBtn = UIFactory.Button(t, "DiplomacyButton", "Diplomasi", UIFactory.Hex("5B3A8C"), 32f);
            UIFactory.Place((RectTransform)dipBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -268f), new Vector2(240f, 84f));
            var letBtn = UIFactory.Button(t, "LettersButton", "Mektuplar", UIFactory.Hex("8A5A2B"), 32f);
            UIFactory.Place((RectTransform)letBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -364f), new Vector2(240f, 84f));
            var badge = UIFactory.Rounded(letBtn.transform, "Badge", UIFactory.Red, 20f);
            UIFactory.Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-12f, -6f), new Vector2(42f, 42f));
            badge.raycastTarget = false;
            letterBadge = UIFactory.Text(badge.transform, "N", "0", 24f, Color.white, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(letterBadge.rectTransform);
            letterBadgeRoot = badge.gameObject;

            var tradeBtn = UIFactory.Button(t, "TradeButton", "Ticaret", UIFactory.Hex("2E7D5B"), 32f);
            UIFactory.Place((RectTransform)tradeBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -460f), new Vector2(240f, 84f));
            tradePanel = TradePanel.Create(t, map.InfoDict, () => map.RefreshAll(), Toast);
            tradeBtn.onClick.AddListener(() => tradePanel.Open());
            var logBtn = UIFactory.Button(t, "LogButton", "Günlük", UIFactory.Hex("4A5568"), 32f);
            UIFactory.Place((RectTransform)logBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -556f), new Vector2(240f, 84f));
            var lb = UIFactory.Rounded(logBtn.transform, "Badge", UIFactory.Red, 20f);
            UIFactory.Place(lb.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-12f, -6f), new Vector2(42f, 42f));
            lb.raycastTarget = false;
            logBadge = UIFactory.Text(lb.transform, "N", "0", 22f, Color.white, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(logBadge.rectTransform);
            logBadgeRoot = lb.gameObject;
            logBadgeRoot.SetActive(false);
            logPanel = LogPanel.Create(t);
            logBtn.onClick.AddListener(OpenLog);

            endPanel = EndPanel.Create(t, () => endNewGame?.Invoke(), () => endContinue?.Invoke());

            // ayarlar ve duraklatma
            var setBtn = UIFactory.Button(t, "SettingsButton", "Ayarlar", UIFactory.Hex("6B7280"), 32f);
            UIFactory.Place((RectTransform)setBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -652f), new Vector2(240f, 84f));
            settings = SettingsPanel.Create(t, () => map.NewGame(), () => { RefreshQuest(); Sfx.ApplySettings(); });
            setBtn.onClick.AddListener(settings.Open);

            var pauseBtn = UIFactory.Button(t, "PauseButton", "II", UIFactory.Hex("4A5568"), 36f);
            UIFactory.Place((RectTransform)pauseBtn.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(338f, -20f), new Vector2(96f, 84f));
            pauseLabel = pauseBtn.GetComponentInChildren<TMP_Text>();
            pauseBtn.onClick.AddListener(() =>
            {
                Paused = !Paused;
                pauseLabel.text = Paused ? ">" : "II";
                pausedChip.SetActive(Paused);
            });
            var pc = UIFactory.Chip(t, "Paused", new Vector2(520f, 72f));
            UIFactory.Place(pc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(520f, 72f));
            pc.raycastTarget = false;
            var pt = UIFactory.Text(pc.transform, "T", "OYUN DURAKLATILDI", 34f, UIFactory.Hex("FFD36E"), TextAlignmentOptions.Center, true);
            UIFactory.Stretch(pt.rectTransform);
            pausedChip = pc.gameObject;
            pausedChip.SetActive(false);

            feed = UIFactory.Rect("Feed", t);
            UIFactory.Place(feed, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(1200f, 180f));
            toastBox.transform.SetAsLastSibling();

            letters = LettersUI.Create(t, (l, i) => map.AnswerLetter(l, i), Toast);
            diplomacy = DiplomacyPanel.Create(t, (a, r) => map.Diplomacy(a, r));
            letBtn.onClick.AddListener(letters.OpenInbox);
            dipBtn.onClick.AddListener(diplomacy.Open);
            armyBtn.onClick.AddListener(shop.Open);

            RefreshTop();
        }

        public void RefreshTop()
        {
            if (goldText != null) goldText.text = SaveSystem.Data.gold.ToString("N0");
            if (incomeText != null && map != null)
            {
                int inc = map.IncomePerMinute();
                int up = Mathf.RoundToInt(KingdomSim.Upkeep(SaveSystem.Data));
                int net = inc - up;
                incomeText.text = "<color=" + (net < 0 ? "#FF7A6B" : "#BFF2A6") + ">" + (net >= 0 ? "+" : "") + net + " / dk</color>  (gelir " + inc + ", gider " + up + ")";
            }
            if (goldText != null) goldText.color = SaveSystem.Data.gold < 0 ? UIFactory.Hex("FF7A6B") : Color.white;
            if (repText != null)
            {
                float r = SaveSystem.Data.reputation;
                repText.text = "İtibar  " + Reputation.Name(r) + "  " + Mathf.RoundToInt(r);
                repDot.color = Reputation.Color(r);
            }
            RefreshRivals();
            if (kingdomText != null && map != null) kingdomText.text = "Krallık  " + map.OwnedCount() + " / " + map.CityCount + " şehir";
        }

        /// <summary>Vezirin görev kartını günceller; görevin anlattığı düğme hafifçe nabız atar.</summary>
        public void RefreshQuest()
        {
            if (questCard == null) return;
            var d = SaveSystem.Data;
            var s = Tutorial.Current(d);
            if (SettingsPanel.Get(SettingsPanel.QuestKey, false)) { questCard.gameObject.SetActive(false); pulseTarget = null; return; }
            if (s == null)
            {
                // öğretici bitti: büyük hedef
                pulseTarget = null;
                bool goal = d.gameOver == Endgame.None;
                questCard.gameObject.SetActive(goal);
                if (!goal) return;
                questTitle.text = "Hedef  ·  Rakip krallıkları diz çöktür (" + Endgame.Fallen(d) + "/" + d.rivals.Count + ")";
                questBody.text = "Başkenti düşen kral sarayını taşır ve zayıflar; son şehrini de alırsan öteki krala sığınır ve orduları birleşir.\n<color=#FF9A8A>Başkentini ve tahtını koru: hazine " +
                                 Endgame.DebtLimit + " altının altında ya da itibarın dipte kalırsa saray darbesi olur.</color>";
                questBody.gameObject.SetActive(!questCollapsed);
                questCard.rectTransform.sizeDelta = new Vector2(560f, questCollapsed ? 58f : 160f);
                return;
            }
            questCard.gameObject.SetActive(true);
            questTitle.text = "Görev " + (d.tutorialStep + 1) + "/" + Tutorial.Steps.Length + "  ·  " + s.title + (questCollapsed ? "" : "");
            questBody.text = s.how + "\n<color=#FFD36E>Ödül: " + s.reward + " altın</color>";
            questBody.gameObject.SetActive(!questCollapsed);
            questCard.rectTransform.sizeDelta = new Vector2(560f, questCollapsed ? 58f : 160f);
            var target = s.highlight != null && canvasT != null ? canvasT.Find(s.highlight) : null;
            if (pulseTarget != target && pulseTarget != null) pulseTarget.localScale = Vector3.one;
            pulseTarget = target;
        }

        Transform pulseTarget;
        Action endNewGame, endContinue;

        public void ShowEnd(int owned, int total, Action newGame, Action keepPlaying)
        {
            endNewGame = newGame;
            endContinue = keepPlaying;
            endPanel.Show(SaveSystem.Data, owned, total);
        }

        void Update()
        {
            if (pulseTarget != null) pulseTarget.localScale = Vector3.one * (1f + 0.05f * (0.5f + 0.5f * Mathf.Sin(Time.time * 5f)));
        }

        public void ShowAttack(MapCity city, CastleData castle, bool canAttack, Action onAttack) => attack.Show(city, castle, canAttack, onAttack);

        public void ShowCity(MapCity city) => cityPanel.Show(city);

        public void ShowTrade(string roadKey) => tradePanel.Open(roadKey);

        public void ShowLetter(Letter l)
        {
            if (letters == null) return;
            // başka bir pencere açıksa zorla açma: rozet yeterli
            if ((attack != null && attack.IsOpen) || (shop != null && shop.IsOpen) || (cityPanel != null && cityPanel.IsOpen) || (diplomacy != null && diplomacy.IsOpen) || (tradePanel != null && tradePanel.IsOpen) || (logPanel != null && logPanel.IsOpen) || letters.IsOpen)
            {
                RefreshLetters();
                return;
            }
            letters.Show(l);
        }

        public void RefreshLetters()
        {
            if (letterBadge == null) return;
            int n = 0;
            foreach (var l in SaveSystem.Data.letters) if (l.NeedsAnswer || !l.read) n++;
            letterBadgeRoot.SetActive(n > 0);
            letterBadge.text = n.ToString();
        }

        // ------------------------------------------------------------------ rakipler

        readonly System.Collections.Generic.List<TMP_Text> rivalTexts = new System.Collections.Generic.List<TMP_Text>();
        readonly System.Collections.Generic.List<Image> rivalDots = new System.Collections.Generic.List<Image>();
        Transform canvasRoot;

        /// <summary>Sağ üstte, Ordu düğmesinin altında rakip krallıkların özeti.</summary>
        public void BuildRivals()
        {
            if (canvasRoot == null) return;
            var data = SaveSystem.Data;
            for (int i = 0; i < data.rivals.Count; i++)
            {
                var chip = UIFactory.Chip(canvasRoot, "Rival" + i, new Vector2(500f, 56f));
                UIFactory.Place(chip.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -128f - i * 66f), new Vector2(500f, 56f));
                chip.raycastTarget = false;
                var dot = UIFactory.Rounded(chip.transform, "Dot", RivalAI.Get(data.rivals[i].id).color, 9f);
                UIFactory.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(18f, 18f));
                dot.raycastTarget = false;
                var t = UIFactory.Text(chip.transform, "Text", "", 23f, Color.white, TextAlignmentOptions.Left, true);
                UIFactory.Stretch(t.rectTransform, 50f, 0f, 16f, 0f);
                t.enableAutoSizing = true; t.fontSizeMin = 16f; t.fontSizeMax = 23f;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                rivalTexts.Add(t);
                rivalDots.Add(dot);
            }
            RefreshRivals();
        }

        public void RefreshRivals()
        {
            var data = SaveSystem.Data;
            for (int i = 0; i < rivalTexts.Count && i < data.rivals.Count; i++)
            {
                var r = data.rivals[i];
                var def = RivalAI.Get(r.id);
                if (!r.alive)
                {
                    rivalTexts[i].text = "<s>" + def.name + "</s>  yıkıldı";
                    rivalTexts[i].color = new Color(1f, 1f, 1f, 0.5f);
                    continue;
                }
                int count = RivalAI.OwnedBy(data, r.id).Count;
                rivalTexts[i].text = def.name + "   " + count + " şehir  ·  güç " + Mathf.RoundToInt(r.power) + (r.Attacking ? "  ·  <color=#FF7A6B>saldırıda</color>" : "");
            }
        }

        // ------------------------------------------------------------------ bildirimler

        /// <summary>Bilgi: küçük, altta belirip kaybolur. Önemli: üstte sırayla. Kritik: kırmızı, sıranın önüne geçer, daha uzun kalır.</summary>
        public enum Level { Info, Important, Critical }

        class Note { public string text; public Level level; }
        readonly System.Collections.Generic.List<Note> notes = new System.Collections.Generic.List<Note>();
        RectTransform feed;
        readonly System.Collections.Generic.List<GameObject> feedItems = new System.Collections.Generic.List<GameObject>();
        LogPanel logPanel;
        TMP_Text logBadge;
        GameObject logBadgeRoot;
        int unseen;

        /// <summary>Krallık haberi: önemine göre gösterilir ve günlüğe yazılır.</summary>
        public void Toast(string message) => Toast(message, Classify(message));

        public void Toast(string message, Level level) => Show(message, level, true);

        /// <summary>Oyuncunun kendi eylemine kısa geri bildirim ("altın yetmiyor" gibi): günlüğe yazılmaz.</summary>
        public void Hint(string message) => Show(message, Level.Important, false);

        static readonly string[] NoLog = { "Yeterli altın", "Orduda bu", "Ordun yok", "Oraya giden", "Ordu kapasitesi" };

        void Show(string message, Level level, bool log)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (log)
                foreach (var p in NoLog) if (message.StartsWith(p)) { log = false; break; }
            if (log)
            {
                SaveSystem.Data.AddLog(message, (int)level);
                if (level != Level.Info) { unseen++; RefreshLogBadge(); }
                if (logPanel != null && logPanel.IsOpen) logPanel.Rebuild();
            }
            if (level == Level.Info) { Feed(message); return; }
            if (notes.Exists(n => n.text == message)) return;
            var note = new Note { text = message, level = level };
            if (level == Level.Critical)
            {
                // kritik haber sıradakilerin önüne geçer (gösterilmekte olanı kesmez)
                int at = toastRoutine != null ? 1 : 0;
                while (at < notes.Count && notes[at].level == Level.Critical) at++;
                notes.Insert(Mathf.Min(at, notes.Count), note);
            }
            else notes.Add(note);
            if (toastRoutine == null) toastRoutine = StartCoroutine(ToastRoutine());
        }

        /// <summary>Haberin önemi: isyan, saldırı, şehir kaybı ve borç kritiktir.</summary>
        static Level Classify(string m)
        {
            string l = m.ToLowerInvariant();
            if (l.Contains("geçti") || l.Contains("püskürt")) return Level.Important;
            foreach (var k in new[] { "isyan", "ayaklan", "düştü", "saldırı", "yürüyor", "savaş ilan", "borçta", "hazine boş", "darbe" })
                if (l.Contains(k)) return Level.Critical;
            return Level.Important;
        }

        IEnumerator ToastRoutine()
        {
            var bg = toastBox.GetComponent<Image>();
            while (notes.Count > 0)
            {
                var n = notes[0];
                bool critical = n.level == Level.Critical;
                if (critical) Sfx.Alert();
                toastText.text = n.text;
                bg.color = critical ? new Color(0.64f, 0.15f, 0.12f, 0.95f) : UIFactory.Glass;
                toastBox.transform.SetAsLastSibling(); // pencerelerin üstünde görünsün
                toastBox.SetActive(true);
                FX.Punch(toastBox.transform, critical ? 0.1f : 0.06f, 0.25f);
                yield return new WaitForSeconds(critical ? 4f : notes.Count > 1 ? 2.2f : 3f);
                notes.Remove(n);
            }
            bg.raycastTarget = false;
            toastBox.SetActive(false);
            toastRoutine = null;
        }

        /// <summary>Altta, ortada küçük haber şeridi (en fazla üç satır, kendiliğinden kaybolur).</summary>
        void Feed(string message)
        {
            if (feed == null) return;
            var chip = UIFactory.Chip(feed, "Info", new Vector2(600f, 48f), 24f);
            chip.raycastTarget = false;
            chip.color = new Color(0.09f, 0.11f, 0.17f, 0.72f);
            var t = UIFactory.Text(chip.transform, "T", message, 23f, new Color(1f, 1f, 1f, 0.92f), TextAlignmentOptions.Center);
            UIFactory.Stretch(t.rectTransform, 22f, 0f, 22f, 0f);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            float w = Mathf.Clamp(t.GetPreferredValues(message).x + 48f, 200f, 1100f);
            chip.rectTransform.anchorMin = chip.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            chip.rectTransform.pivot = new Vector2(0.5f, 0f);
            chip.rectTransform.sizeDelta = new Vector2(w, 48f);
            feedItems.Add(chip.gameObject);
            while (feedItems.Count > 3) { if (feedItems[0] != null) Destroy(feedItems[0]); feedItems.RemoveAt(0); }
            LayoutFeed();
            StartCoroutine(FadeFeed(chip.gameObject, chip, t));
        }

        void LayoutFeed()
        {
            feedItems.RemoveAll(g => g == null);
            for (int i = 0; i < feedItems.Count; i++)
                ((RectTransform)feedItems[i].transform).anchoredPosition = new Vector2(0f, (feedItems.Count - 1 - i) * 56f);
        }

        IEnumerator FadeFeed(GameObject go, Image bg, TMP_Text t)
        {
            yield return new WaitForSeconds(4f);
            float a = 1f;
            while (a > 0f && go != null)
            {
                a -= Time.deltaTime * 2f;
                bg.color = new Color(bg.color.r, bg.color.g, bg.color.b, 0.72f * a);
                t.alpha = a;
                yield return null;
            }
            if (go != null) { feedItems.Remove(go); Destroy(go); LayoutFeed(); }
        }

        void RefreshLogBadge()
        {
            if (logBadgeRoot == null) return;
            logBadgeRoot.SetActive(unseen > 0);
            logBadge.text = unseen > 9 ? "9+" : unseen.ToString();
        }

        void OpenLog()
        {
            unseen = 0;
            RefreshLogBadge();
            logPanel.Open();
        }
    }
}
