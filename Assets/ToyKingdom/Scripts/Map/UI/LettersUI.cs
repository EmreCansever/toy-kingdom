using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>Mektup penceresi ve gelen kutusu (koddan kurulur).</summary>
    public class LettersUI : MonoBehaviour
    {
        GameObject popupRoot, inboxRoot;
        RectTransform popupBody, inboxList;
        Letter current;
        TMP_Text timer;
        Func<Letter, int, string> answer;
        Action<string> toast;

        static readonly Color Paper = UIFactory.Hex("F7ECD2");
        static readonly Color PaperDark = UIFactory.Hex("E9D8B0");

        public bool IsOpen => (popupRoot != null && popupRoot.activeSelf) || (inboxRoot != null && inboxRoot.activeSelf);

        public static LettersUI Create(Transform canvas, Func<Letter, int, string> answer, Action<string> toast)
        {
            var go = new GameObject("Letters", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            UIFactory.Stretch((RectTransform)go.transform);
            var ui = go.AddComponent<LettersUI>();
            ui.answer = answer;
            ui.toast = toast;

            // gelen kutusu
            var dim = UIFactory.Panel(go.transform, "Inbox", UIFactory.Dim);
            UIFactory.Stretch(dim.rectTransform);
            ui.inboxRoot = dim.gameObject;
            var box = UIFactory.Window(dim.transform, "Box", new Vector2(1180f, 900f), "Mektuplar", UIFactory.Hex("8A5A2B"), out _);
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 900f));
            var close = UIFactory.Button(box.transform.Find("Border/Header"), "Close", "Kapat", UIFactory.Hex("F4EBDD"), 30f);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 2f), new Vector2(170f, 68f));
            close.onClick.AddListener(() => ui.inboxRoot.SetActive(false));
            ui.inboxList = UIFactory.Rect("List", box.transform);
            UIFactory.Stretch(ui.inboxList, 34f, 30f, 34f, 120f);
            ui.inboxRoot.SetActive(false);

            // tek mektup
            var pdim = UIFactory.Panel(go.transform, "Letter", UIFactory.Dim);
            UIFactory.Stretch(pdim.rectTransform);
            ui.popupRoot = pdim.gameObject;
            ui.popupRoot.SetActive(false);
            return ui;
        }

        void Update()
        {
            if (current != null && popupRoot.activeSelf && timer != null)
            {
                if (current.NeedsAnswer && current.expiresTicks > 0)
                {
                    long left = Math.Max(0L, current.expiresTicks - DateTime.UtcNow.Ticks) / TimeSpan.TicksPerSecond;
                    timer.text = "Cevap süresi  " + (left / 60) + ":" + (left % 60).ToString("00");
                    timer.color = left < 30 ? UIFactory.Red : UIFactory.Brown;
                }
                else if (!current.NeedsAnswer && current.options.Count > 0 && !string.IsNullOrEmpty(timer.text) && timer.text.StartsWith("Cevap"))
                    Show(current); // süre dolup otomatik çözüldüyse sonucu göster
            }
        }

        // ------------------------------------------------------------------ gelen kutusu

        public void OpenInbox()
        {
            popupRoot.SetActive(false);
            inboxRoot.SetActive(true);
            for (int i = inboxList.childCount - 1; i >= 0; i--) Destroy(inboxList.GetChild(i).gameObject);
            var list = new List<Letter>(SaveSystem.Data.letters);
            // cevap bekleyenler önce, sonra en yeniler
            list.Sort((a, b) =>
            {
                int pa = a.NeedsAnswer ? 0 : 1, pb = b.NeedsAnswer ? 0 : 1;
                if (pa != pb) return pa.CompareTo(pb);
                return b.createdTicks.CompareTo(a.createdTicks);
            });
            if (list.Count == 0)
            {
                var t = UIFactory.Text(inboxList, "Empty", "Henüz mektup yok.", 30f, UIFactory.Muted, TextAlignmentOptions.Center);
                UIFactory.Stretch(t.rectTransform);
                return;
            }
            for (int i = 0; i < list.Count && i < 7; i++)
            {
                var l = list[i];
                var row = UIFactory.Rounded(inboxList, "Row", l.NeedsAnswer ? UIFactory.Hex("FFE9C7") : UIFactory.ParchmentDark, 18f);
                UIFactory.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -i * 104f), new Vector2(1112f, 94f));
                var seal = UIFactory.Panel(row.transform, "Seal", SealColor(l), GameArt.Instance != null ? GameArt.Instance.circle : null);
                UIFactory.Place(seal.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(54f, 54f));
                var t1 = UIFactory.Text(row.transform, "Title", l.title, 28f, UIFactory.BrownDark, TextAlignmentOptions.Left, true);
                UIFactory.Place(t1.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(92f, -8f), new Vector2(700f, 40f));
                var t2 = UIFactory.Text(row.transform, "From", Court.Sender(l), 21f, UIFactory.Muted, TextAlignmentOptions.Left);
                UIFactory.Place(t2.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(92f, -50f), new Vector2(700f, 34f));
                string status = l.NeedsAnswer ? "Cevap bekliyor" : l.resolved ? "Cevaplandı" : l.read ? "Okundu" : "Yeni";
                var t3 = UIFactory.Text(row.transform, "Status", status, 24f, l.NeedsAnswer ? UIFactory.Red : UIFactory.Brown, TextAlignmentOptions.Right, true);
                UIFactory.Place(t3.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(280f, 40f));
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = row;
                var letter = l;
                btn.onClick.AddListener(() => Show(letter));
            }
        }

        static Color SealColor(Letter l) => l.from == "court" ? UIFactory.Hex("8A2BE2") : RivalAI.Get(l.from).color;

        // ------------------------------------------------------------------ mektup

        public void Show(Letter l)
        {
            Sfx.Play("sfx_page", 0.7f, 0.05f, 0.3f);
            current = l;
            l.read = true;
            inboxRoot.SetActive(false);
            popupRoot.SetActive(true);
            if (popupBody != null) Destroy(popupBody.gameObject);

            bool hasResult = !l.NeedsAnswer && !string.IsNullOrEmpty(l.result);
            float h = l.NeedsAnswer ? 560f + 108f * l.options.Count + 110f : hasResult ? 980f : 820f;
            h = Mathf.Min(Mathf.Max(h, 700f), 1040f);
            var frame = UIFactory.Frame(popupRoot.transform, "Paper", new Vector2(1180f, h));
            UIFactory.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, h));
            frame.transform.Find("Border/Inner").GetComponent<Image>().color = Paper;
            popupBody = frame.rectTransform;
            var b = frame.transform;

            // gönderen: portre ya da mühür
            if (l.from != "court")
            {
                var p = KingPortrait.Create(b, l.from, 150f);
                UIFactory.Place(p, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(150f, 150f));
            }
            else
            {
                var seal = UIFactory.Panel(b, "Seal", SealColor(l), GameArt.Instance != null ? GameArt.Instance.circle : null);
                UIFactory.Place(seal.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -40f), new Vector2(120f, 120f));
                var st = UIFactory.Text(seal.transform, "V", "V", 64f, Color.white, TextAlignmentOptions.Center, true);
                UIFactory.Stretch(st.rectTransform);
            }
            var from = UIFactory.Text(b, "From", Court.Sender(l), 26f, UIFactory.Muted, TextAlignmentOptions.Left);
            UIFactory.Place(from.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(220f, -40f), new Vector2(620f, 36f));
            var title = UIFactory.Text(b, "Title", l.title, 46f, UIFactory.BrownDark, TextAlignmentOptions.Left, true);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(220f, -80f), new Vector2(620f, 64f));
            timer = UIFactory.Text(b, "Timer", "", 26f, UIFactory.Brown, TextAlignmentOptions.Right, true);
            UIFactory.Place(timer.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -44f), new Vector2(320f, 36f));

            // ince süs çizgisi ve mum mühür
            var line = UIFactory.Panel(b, "Rule", UIFactory.Hex("B9925A"));
            UIFactory.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -184f), new Vector2(1080f, 3f));
            line.raycastTarget = false;
            var wax = UIFactory.Panel(b, "Wax", UIFactory.Darken(SealColor(l), 0.8f), GameArt.Instance != null ? GameArt.Instance.circle : null);
            if (l.NeedsAnswer) UIFactory.Place(wax.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-110f, -122f), new Vector2(96f, 96f));
            else UIFactory.Place(wax.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-110f, 200f), new Vector2(118f, 118f));
            wax.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            wax.raycastTarget = false;
            var waxIn = UIFactory.Panel(wax.transform, "In", SealColor(l), GameArt.Instance != null ? GameArt.Instance.circle : null);
            UIFactory.Stretch(waxIn.rectTransform, 12f);
            waxIn.raycastTarget = false;
            var waxT = UIFactory.Text(waxIn.transform, "L", LetterStyle.SealLetter(l.from), 54f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center, true);
            UIFactory.Stretch(waxT.rectTransform);

            float bodyH = l.NeedsAnswer ? 340f : hasResult ? 400f : 470f;
            var body = UIFactory.Text(b, "Body", l.body, 27f, UIFactory.Hex("3B2A1A"), TextAlignmentOptions.TopLeft);
            UIFactory.Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -200f), new Vector2(l.NeedsAnswer ? 1080f : 940f, bodyH));
            body.enableAutoSizing = true; body.fontSizeMin = 17f; body.fontSizeMax = 27f;
            body.fontStyle = FontStyles.Italic;
            body.lineSpacing = 2f;

            if (l.NeedsAnswer)
            {
                var data = SaveSystem.Data;
                for (int i = 0; i < l.options.Count; i++)
                {
                    var o = l.options[i];
                    float y = -560f - i * 108f;
                    Color col = i == 0 ? UIFactory.Blue : i == l.options.Count - 1 ? UIFactory.Hex("8C8577") : UIFactory.Gold;
                    var btn = UIFactory.Button(b, "Opt" + i, o.label, col, 28f);
                    UIFactory.Place((RectTransform)btn.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, y), new Vector2(470f, 92f));
                    btn.interactable = o.cost <= 0 || data.gold >= o.cost;
                    var hint = UIFactory.Text(b, "Hint" + i, o.hint + (i == l.defaultOption ? "  <i>(cevap vermezsen bu olur)</i>" : ""), 22f, UIFactory.Brown, TextAlignmentOptions.Left);
                    UIFactory.Place(hint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(545f, y - 4f), new Vector2(590f, 88f));
                    hint.enableAutoSizing = true; hint.fontSizeMin = 16f; hint.fontSizeMax = 22f;
                    int idx = i;
                    btn.onClick.AddListener(() =>
                    {
                        string res = answer?.Invoke(current, idx);
                        if (!string.IsNullOrEmpty(res) && current.NeedsAnswer) { toast?.Invoke(res); return; } // ör. altın yetmedi
                        Show(current);
                    });
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(l.result))
                {
                    string chosen = l.chosen >= 0 && l.chosen < l.options.Count ? "Cevabın: " + l.options[l.chosen].label + "\n" : "";
                    // sonuç mektubun altında kendi kutusunda: mektup metninin üstüne binmez
                    var box = UIFactory.Rounded(b, "ResultBox", UIFactory.Hex("EADBB8"), 14f);
                    UIFactory.Place(box.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -620f), new Vector2(920f, 190f));
                    box.raycastTarget = false;
                    var res = UIFactory.Text(box.transform, "Result", chosen + "<b>" + l.result + "</b>", 28f, UIFactory.BrownDark, TextAlignmentOptions.TopLeft);
                    UIFactory.Stretch(res.rectTransform, 18f);
                    res.enableAutoSizing = true; res.fontSizeMin = 16f; res.fontSizeMax = 28f;
                }
                var ok = UIFactory.Button(b, "Ok", "Tamam", UIFactory.Blue, 32f);
                UIFactory.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-150f, 40f), new Vector2(260f, 90f));
                ok.onClick.AddListener(() => { popupRoot.SetActive(false); current = null; });
                var all = UIFactory.Button(b, "All", "Tüm mektuplar", UIFactory.Hex("8C8577"), 28f);
                UIFactory.Place((RectTransform)all.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(150f, 40f), new Vector2(300f, 90f));
                all.onClick.AddListener(OpenInbox);
            }
            if (l.NeedsAnswer)
            {
                // kapatmak cevabı erteler (süre işlemeye devam eder)
                var later = UIFactory.Button(b, "Later", "Sonra karar ver", UIFactory.Hex("8C8577"), 24f);
                UIFactory.Place((RectTransform)later.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 30f), new Vector2(280f, 72f));
                later.onClick.AddListener(() => { popupRoot.SetActive(false); current = null; });
            }
            FX.Punch(frame.transform, 0.04f, 0.2f);
        }
    }
}
