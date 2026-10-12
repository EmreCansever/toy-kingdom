using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>Olay günlüğü: krallıkta olan her şey, en yenisi üstte. "Önemli" sekmesi bilgi haberlerini gizler.</summary>
    public class LogPanel : MonoBehaviour
    {
        GameObject root;
        RectTransform list;
        Button allTab, importantTab;
        bool onlyImportant;
        int page;
        float refresh;

        const int PerPage = 11;

        public bool IsOpen => root != null && root.activeSelf;

        public static LogPanel Create(Transform canvas)
        {
            var dim = UIFactory.Panel(canvas, "Log", UIFactory.Dim);
            UIFactory.Stretch(dim.rectTransform);
            var p = dim.gameObject.AddComponent<LogPanel>();
            p.root = dim.gameObject;
            var box = UIFactory.Window(dim.transform, "Box", new Vector2(1440f, 960f), "Olay Günlüğü", UIFactory.Hex("4A5568"), out _);
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440f, 960f));
            var header = box.transform.Find("Border/Header");
            var close = UIFactory.Button(header, "Close", "Kapat", UIFactory.Hex("F4EBDD"), 30f);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 2f), new Vector2(170f, 68f));
            close.onClick.AddListener(p.Hide);

            p.allTab = UIFactory.Button(header, "All", "Tümü", UIFactory.Hex("F4EBDD"), 26f);
            UIFactory.Place((RectTransform)p.allTab.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-420f, 2f), new Vector2(170f, 64f));
            p.allTab.onClick.AddListener(() => { p.onlyImportant = false; p.page = 0; p.Rebuild(); });
            p.importantTab = UIFactory.Button(header, "Important", "Önemli", UIFactory.Hex("F4EBDD"), 26f);
            UIFactory.Place((RectTransform)p.importantTab.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-232f, 2f), new Vector2(170f, 64f));
            p.importantTab.onClick.AddListener(() => { p.onlyImportant = true; p.page = 0; p.Rebuild(); });

            p.list = UIFactory.Rect("List", box.transform);
            UIFactory.Stretch(p.list, 30f, 30f, 30f, 120f);

            // pencere dışına dokunmak da kapatır
            var back = dim.gameObject.AddComponent<Button>();
            back.transition = Selectable.Transition.None;
            back.onClick.AddListener(p.Hide);
            p.root.SetActive(false);
            return p;
        }

        public void Open()
        {
            page = 0;
            root.SetActive(true);
            Rebuild();
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        void Update()
        {
            if (!IsOpen) return;
            refresh += Time.deltaTime;
            if (refresh > 5f) { refresh = 0f; Rebuild(); } // "x dk önce" yazıları güncel kalsın
        }

        public void Rebuild()
        {
            for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
            SetTab(allTab, !onlyImportant);
            SetTab(importantTab, onlyImportant);

            var entries = new List<LogEntry>();
            var log = SaveSystem.Data.log;
            for (int i = log.Count - 1; i >= 0; i--)
                if (!onlyImportant || log[i].level > 0) entries.Add(log[i]);

            if (entries.Count == 0)
            {
                var e = UIFactory.Text(list, "Empty", "Henüz kaydedilmiş bir olay yok.", 30f, UIFactory.Brown, TextAlignmentOptions.Center);
                UIFactory.Stretch(e.rectTransform);
                return;
            }

            int pages = (entries.Count + PerPage - 1) / PerPage;
            page = Mathf.Clamp(page, 0, pages - 1);
            long now = DateTime.UtcNow.Ticks;
            for (int i = page * PerPage, row = 0; i < Mathf.Min(entries.Count, (page + 1) * PerPage); i++, row++)
            {
                var en = entries[i];
                var bg = UIFactory.Rounded(list, "Row", en.level == 2 ? UIFactory.Hex("F8DCD6") : row % 2 == 0 ? UIFactory.ParchmentDark : UIFactory.Parchment, 14f);
                UIFactory.Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -row * 66f), new Vector2(1380f, 60f));
                bg.raycastTarget = false;
                var dot = UIFactory.Rounded(bg.transform, "Dot", en.level == 2 ? UIFactory.Red : en.level == 1 ? UIFactory.Gold : UIFactory.Hex("9AA0AA"), 8f);
                UIFactory.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(16f, 16f));
                dot.raycastTarget = false;
                var when = UIFactory.Text(bg.transform, "When", Ago(now - en.ticks), 22f, UIFactory.Brown, TextAlignmentOptions.Left);
                UIFactory.Place(when.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(170f, 50f));
                var txt = UIFactory.Text(bg.transform, "Text", en.text.Replace("\n", "  "), 25f, en.level == 2 ? UIFactory.Hex("8E2A20") : UIFactory.Ink,
                    TextAlignmentOptions.Left, en.level == 2);
                UIFactory.Place(txt.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(220f, 0f), new Vector2(1145f, 54f));
                txt.enableAutoSizing = true; txt.fontSizeMin = 16f; txt.fontSizeMax = 25f;
            }

            if (pages > 1)
            {
                var prev = UIFactory.Button(list, "Prev", "Daha yeni", UIFactory.Hex("8C8577"), 24f);
                UIFactory.Place((RectTransform)prev.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(220f, 64f));
                prev.interactable = page > 0;
                prev.onClick.AddListener(() => { page--; Rebuild(); });
                var next = UIFactory.Button(list, "Next", "Daha eski", UIFactory.Hex("8C8577"), 24f);
                UIFactory.Place((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(220f, 64f));
                next.interactable = page < pages - 1;
                next.onClick.AddListener(() => { page++; Rebuild(); });
                var pt = UIFactory.Text(list, "Page", (page + 1) + " / " + pages, 26f, UIFactory.BrownDark, TextAlignmentOptions.Center, true);
                UIFactory.Place(pt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(200f, 50f));
            }
        }

        static void SetTab(Button b, bool on)
        {
            var style = b.GetComponent<TKButton>();
            if (style != null) style.SetColor(on ? UIFactory.Blue : UIFactory.Hex("F4EBDD"));
        }

        static string Ago(long ticks)
        {
            long s = Math.Max(0L, ticks / TimeSpan.TicksPerSecond);
            if (s < 60) return "az önce";
            if (s < 3600) return (s / 60) + " dk önce";
            if (s < 86400) return (s / 3600) + " sa önce";
            return (s / 86400) + " gün önce";
        }
    }
}
