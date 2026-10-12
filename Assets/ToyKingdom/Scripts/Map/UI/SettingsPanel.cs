using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>Oyun içi ayarlar: ses, görev kartı, yeni oyun (iki kez onaylı).</summary>
    public class SettingsPanel : MonoBehaviour
    {
        public const string MusicKey = "tk_music", SfxKey = "tk_sfx", QuestKey = "tk_hideQuest";

        GameObject root;
        RectTransform content;
        Action onNewGame, onChanged;
        bool confirmReset;

        public bool IsOpen => root != null && root.activeSelf;

        public static bool Get(string key, bool def) { try { return PlayerPrefs.GetInt(key, def ? 1 : 0) == 1; } catch { return def; } }
        static void Set(string key, bool v) { try { PlayerPrefs.SetInt(key, v ? 1 : 0); PlayerPrefs.Save(); } catch { } }

        public static SettingsPanel Create(Transform canvas, Action newGame, Action changed)
        {
            var dim = UIFactory.Panel(canvas, "Settings", UIFactory.Dim);
            UIFactory.Stretch(dim.rectTransform);
            var p = dim.gameObject.AddComponent<SettingsPanel>();
            p.root = dim.gameObject;
            p.onNewGame = newGame;
            p.onChanged = changed;
            var box = UIFactory.Window(dim.transform, "Box", new Vector2(980f, 820f), "Ayarlar", UIFactory.Hex("4A5568"), out _);
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 820f));
            var header = box.transform.Find("Border/Header");
            var close = UIFactory.Button(header, "Close", "Kapat", UIFactory.Hex("F4EBDD"), 30f);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 2f), new Vector2(170f, 68f));
            close.onClick.AddListener(p.Hide);
            p.content = UIFactory.Rect("Content", box.transform);
            UIFactory.Stretch(p.content, 40f, 40f, 40f, 130f);
            var back = dim.gameObject.AddComponent<Button>();
            back.transition = Selectable.Transition.None;
            back.onClick.AddListener(p.Hide);
            p.root.SetActive(false);
            return p;
        }

        public void Open()
        {
            confirmReset = false;
            root.SetActive(true);
            Rebuild();
        }

        public void Hide()
        {
            confirmReset = false;
            if (root != null) root.SetActive(false);
        }

        void Rebuild()
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            Toggle(0, "Müzik", "Arka plan müziği", MusicKey, true);
            Toggle(1, "Ses efektleri", "Savaş ve arayüz sesleri", SfxKey, true);
            Toggle(2, "Görev kartını gizle", "Sol üstteki görev / hedef kartı", QuestKey, false);

            var row = Row(3);
            Label(row, "Yeni oyun", 30f, UIFactory.BrownDark, true, new Vector2(24f, -12f));
            Label(row, confirmReset ? "Emin misin? Kaydın tamamen silinecek!" : "Krallığı sıfırdan başlat", 22f,
                confirmReset ? UIFactory.Red : UIFactory.Brown, false, new Vector2(24f, -56f));
            var b = UIFactory.Button(row, "Reset", confirmReset ? "Evet, sil" : "Yeni oyun", UIFactory.Red, 28f);
            UIFactory.Place((RectTransform)b.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(240f, 80f));
            b.onClick.AddListener(() =>
            {
                if (!confirmReset) { confirmReset = true; Rebuild(); return; }
                Hide();
                onNewGame?.Invoke();
            });

            var note = UIFactory.Text(content, "Note", "Bir pencere açıkken ya da duraklatınca krallıkta zaman durur.", 22f, UIFactory.Brown, TextAlignmentOptions.Center);
            UIFactory.Place(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(880f, 40f));
        }

        RectTransform Row(int i)
        {
            var bg = UIFactory.Rounded(content, "Row" + i, UIFactory.ParchmentDark, 18f);
            UIFactory.Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -i * 132f), new Vector2(900f, 116f));
            return bg.rectTransform;
        }

        void Toggle(int i, string title, string hint, string key, bool def)
        {
            var row = Row(i);
            Label(row, title, 30f, UIFactory.BrownDark, true, new Vector2(24f, -12f));
            Label(row, hint, 22f, UIFactory.Brown, false, new Vector2(24f, -56f));
            bool on = Get(key, def);
            var b = UIFactory.Button(row, "Toggle", on ? "Açık" : "Kapalı", on ? UIFactory.Green : UIFactory.Hex("8C8577"), 28f);
            UIFactory.Place((RectTransform)b.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(240f, 80f));
            b.onClick.AddListener(() => { Set(key, !Get(key, def)); onChanged?.Invoke(); Rebuild(); });
        }

        static void Label(RectTransform parent, string text, float size, Color color, bool bold, Vector2 pos)
        {
            var t = UIFactory.Text(parent, "T", text, size, color, TextAlignmentOptions.TopLeft, bold);
            UIFactory.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(600f, 44f));
        }
    }
}
