#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyKingdom.EditorTools
{
    /// <summary>
    /// Menü: ToyKingdom > Örnek içeriği oluştur
    /// Grafik paketlerinden GameArt, birlikler, kaleler, izometrik dünya haritası (Map) ve savaş (Battle) sahnelerini kurar.
    /// Üretilen her şey Assets/ToyKingdom/Generated altına yazılır ve her çalıştırmada yeniden oluşturulur.
    /// </summary>
    public static class SampleContentBuilder
    {
        const string ArtRoot = "Assets/ToyKingdom/Art";
        const string Med = ArtRoot + "/Medieval/";
        const string Cru = ArtRoot + "/Crusader/";
        const string LayoutPath = "Assets/ToyKingdom/Data/WorldLayout.json";
        const string Root = "Assets/ToyKingdom/Generated";
        const string HelperDir = Root + "/Helpers";
        const string DataDir = Root + "/Data";
        const string SceneDir = Root + "/Scenes";
        const string ResDir = Root + "/Resources";

        // ------------------------------------------------------------------ menü

        [MenuItem("ToyKingdom/Örnek içeriği oluştur")]
        public static void Build()
        {
            if (!EditorUtility.DisplayDialog("Toy Kingdom",
                    Root + " klasörü silinip yeniden oluşturulacak. Devam edilsin mi?", "Oluştur", "Vazgeç"))
                return;

            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                EditorUtility.DisplayDialog("Toy Kingdom", "Önce Window > TextMeshPro > Import TMP Essential Resources ile TextMeshPro dosyalarını içe aktar.", "Tamam");
                return;
            }
            if (!AssetDatabase.IsValidFolder(ArtRoot + "/Medieval") || !AssetDatabase.IsValidFolder(ArtRoot + "/Crusader") || !File.Exists(LayoutPath))
            {
                EditorUtility.DisplayDialog("Toy Kingdom", "Grafik klasörleri (Art/Medieval, Art/Crusader) veya Data/WorldLayout.json bulunamadı.", "Tamam");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            AssetDatabase.DeleteAsset(Root);
            foreach (var d in new[] { HelperDir, DataDir, SceneDir, ResDir }) EnsureFolder(d);

            try
            {
                EditorUtility.DisplayProgressBar("Toy Kingdom", "Grafik ayarları", 0.05f);
                ApplyImportSettings();

                EditorUtility.DisplayProgressBar("Toy Kingdom", "Animasyonlar", 0.3f);
                var knight = BuildAnimSet();
                var art = BuildGameArt(knight);

                EditorUtility.DisplayProgressBar("Toy Kingdom", "Birlikler ve kaleler", 0.5f);
                var units = BuildUnits(knight);
                var db = ScriptableObject.CreateInstance<UnitDatabase>();
                db.units = new List<UnitData> { units["infantry"], units["shield"], units["archer"], units["climber"], units["knight"] };
                AssetDatabase.CreateAsset(db, ResDir + "/UnitDatabase.asset");
                var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
                var castles = BuildCastles(units, layout);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayProgressBar("Toy Kingdom", "Savaş sahnesi", 0.65f);
                string battlePath = BuildBattleScene(castles["city_01"]);

                EditorUtility.DisplayProgressBar("Toy Kingdom", "Dünya haritası", 0.8f);
                string mapPath = BuildMapScene(art, castles, layout);

                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(mapPath, true),
                    new EditorBuildSettingsScene(battlePath, true)
                };

                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(mapPath);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            SaveSystem.ResetSave();
            EditorUtility.DisplayDialog("Toy Kingdom", "Hazır! Map sahnesi açıldı. Play'e bas, haritayı sürükle, bir şehre dokun ve saldır.", "Harika");
        }

        [MenuItem("ToyKingdom/Kaydı sıfırla")]
        public static void ResetSaveMenu()
        {
            SaveSystem.ResetSave();
            Debug.Log("Toy Kingdom: kayıt sıfırlandı (" + Application.persistentDataPath + ")");
        }

        // ------------------------------------------------------------------ içe aktarma

        static void ApplyImportSettings()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot });
            var changed = new List<string>();
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (imp == null) continue;
                if (ArtImportRules.Apply(imp, p)) changed.Add(p);
            }
            if (changed.Count == 0) return;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var p in changed)
                {
                    var imp = (TextureImporter)AssetImporter.GetAtPath(p);
                    ArtImportRules.Apply(imp, p);
                    imp.SaveAndReimport();
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
        }

        static Sprite S(string rel)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(Med + rel);
            if (s == null) Debug.LogWarning("Toy Kingdom: sprite bulunamadı: " + Med + rel);
            return s;
        }

        static Sprite[] Range(string pattern, int from, int to)
        {
            var list = new List<Sprite>();
            for (int i = from; i <= to; i++)
            {
                var s = S(string.Format(pattern, i));
                if (s != null) list.Add(s);
            }
            return list.ToArray();
        }

        static Sprite[] Folder(string rel)
        {
            var list = new List<Sprite>();
            foreach (var g in AssetDatabase.FindAssets("t:Sprite", new[] { Med + rel }))
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g));
                if (s != null) list.Add(s);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list.ToArray();
        }

        // ------------------------------------------------------------------ grafikler

        static CharacterAnimSet BuildAnimSet()
        {
            var set = ScriptableObject.CreateInstance<CharacterAnimSet>();
            set.idle = Clip("idle", 14f, true);
            set.walk = Clip("walk", 22f, true);
            set.attack = Clip("attack", 26f, false);
            set.death = Clip("death", 14f, false);
            set.attackHitPoint = 0.55f;
            AssetDatabase.CreateAsset(set, DataDir + "/Anim_Knight.asset");
            return set;
        }

        static AnimClip Clip(string anim, float fps, bool loop)
        {
            int frames = 0;
            while (AssetDatabase.LoadAssetAtPath<Sprite>(Cru + anim + "/crusader_" + anim + "_0_" + frames.ToString("00") + ".png") != null) frames++;
            var arr = new Sprite[frames * 8];
            for (int d = 0; d < 8; d++)
                for (int f = 0; f < frames; f++)
                    arr[d * frames + f] = AssetDatabase.LoadAssetAtPath<Sprite>(Cru + anim + "/crusader_" + anim + "_" + d + "_" + f.ToString("00") + ".png");
            if (frames == 0) Debug.LogError("Toy Kingdom: animasyon kareleri bulunamadı: " + anim);
            return new AnimClip { framesPerDirection = frames, fps = fps, loop = loop, frames = arr };
        }

        static GameArt BuildGameArt(CharacterAnimSet knight)
        {
            var a = ScriptableObject.CreateInstance<GameArt>();
            a.spring = Bio("Spring", "", "Enviroument/Spring/grass.png",
                Range("Enviroument/Spring/grass_detail({0}).png", 1, 9),
                Range("Enviroument/Spring/trees/tree({0}).png", 1, 4),
                Concat(Range("Enviroument/Spring/stones/stone({0}).png", 1, 4), Range("Enviroument/Spring/stones/stone_ground ({0}).png", 1, 4)));
            a.desert = Bio("Desert", "_desert", "Enviroument/Desert/sand.png",
                Range("Enviroument/Desert/sand_detail({0}).png", 1, 9),
                Range("Enviroument/Desert/cactuses/cactus({0}).png", 1, 5),
                Range("Enviroument/Desert/stones/stone_deser({0}).png", 1, 4));
            a.winter = Bio("Winter", "_winter", "Enviroument/Winter/snow.png",
                Range("Enviroument/Winter/snow_detail({0}).png", 1, 9),
                Range("Enviroument/Winter/trees/tree({0}).png", 1, 4),
                Range("Enviroument/Spring/stones/stone({0}).png", 1, 4));
            a.fieldTiles = new[] { S("Buildings/wheat/wheat(1).png"), S("Buildings/wheat/wheat(2).png"), S("Buildings/corn/corn(1).png"), S("Buildings/corn/corn(2).png") };
            a.houses = Folder("Buildings/house");
            a.mills = Folder("Buildings/mill");
            a.smoke = S("Buildings/smoke.png");

            a.wallsAlongU = new[] { S("Castle walls/castle_wall(1).png"), S("Castle walls/castle_wall(1).png"), S("Castle walls/castle_wall(3).png") };
            a.wallsAlongV = new[] { S("Castle walls/castle_wall(2).png"), S("Castle walls/castle_wall(2).png"), S("Castle walls/castle_wall(4).png") };
            a.wallCross = S("Castle walls/castle_wall(10).png");
            a.gateLeft = S("Castle walls/castle_wall(5).png");
            a.gateRight = S("Castle walls/castle_wall(6).png");
            a.towersBlue = Range("Towers/Blue towers/castle_tower_blue({0}).png", 1, 9);
            a.towersGreen = Range("Towers/Green towers/castle_tower_green({0}).png", 1, 9);
            a.towersRed = Range("Towers/Red tower/castle_tower_red({0}).png", 1, 9);
            a.towersWood = Range("Towers/Wood towers/castle_tower_wood({0}).png", 1, 9);

            a.knight = knight;

            a.white = MakeSprite("white", 4, 4, 4f, new Vector2(0.5f, 0.5f), (u, v) => Color.white);
            a.shadow = MakeSprite("shadow", 64, 32, 64f, new Vector2(0.5f, 0.5f), (u, v) =>
            {
                float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                return new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 2.2f));
            });
            a.arrow = MakeSprite("arrow", 40, 8, 100f, new Vector2(0.5f, 0.5f), (u, v) =>
            {
                if (u > 0.78f && Mathf.Abs(v - 0.5f) < (1f - u) * 2.2f) return new Color(0.45f, 0.45f, 0.5f);
                if (u < 0.15f && Mathf.Abs(v - 0.5f) < 0.45f) return new Color(0.9f, 0.9f, 0.85f);
                if (Mathf.Abs(v - 0.5f) < 0.16f) return new Color(0.45f, 0.28f, 0.1f);
                return Color.clear;
            });
            a.tileHighlight = MakeSprite("tile_highlight", 90, 52, 100f, new Vector2(0.5f, 0.5f), (u, v) =>
                Mathf.Abs(u - 0.5f) * 2f + Mathf.Abs(v - 0.5f) * 2f <= 0.96f ? Color.white : Color.clear);
            a.ladder = MakeSprite("ladder", 26, 96, 100f, new Vector2(0.5f, 0f), (u, v) =>
            {
                Color wood = new Color(0.55f, 0.33f, 0.14f), dark = new Color(0.25f, 0.14f, 0.05f);
                bool rail = (u > 0.08f && u < 0.28f) || (u > 0.72f && u < 0.92f);
                bool rung = u >= 0.08f && u <= 0.92f && Mathf.Repeat(v * 9f, 1f) < 0.16f;
                bool edge = (u > 0.04f && u < 0.08f) || (u > 0.92f && u < 0.96f);
                if (rail || rung) return wood;
                if (edge) return dark;
                return Color.clear;
            });
            a.circle = MakeSprite("circle", 64, 64, 64f, new Vector2(0.5f, 0.5f), (u, v) =>
            {
                float dx = u - 0.5f, dy = v - 0.5f;
                return dx * dx + dy * dy <= 0.235f ? Color.white : Color.clear;
            });

            a.star = MakeSprite("star", 48, 48, 48f, new Vector2(0.5f, 0.5f), (u, v) =>
            {
                float dx = u - 0.5f, dy = v - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                float edge = 0.22f + 0.24f * Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 2f)), 6f);
                if (r > edge) return Color.clear;
                return new Color(1f, 1f, 1f, Mathf.Clamp01((edge - r) * 30f));
            });

            AssetDatabase.CreateAsset(a, ResDir + "/GameArt.asset");
            return a;
        }

        static Sprite[] Concat(Sprite[] a, Sprite[] b)
        {
            var l = new List<Sprite>(a); l.AddRange(b); return l.ToArray();
        }

        static BiomeArt Bio(string folder, string sfx, string ground, Sprite[] details, Sprite[] trees, Sprite[] stones)
        {
            // kenar maskesi (NE=1, SE=2, SW=4, NW=8) -> paket dosya numarası (üç bölgede de aynı)
            int[] roadFile = { 0, 14, 12, 9, 13, 3, 11, 5, 15, 10, 2, 6, 8, 7, 4, 1 };
            int[] riverFile = { 0, 14, 12, 9, 13, 2, 11, 5, 15, 10, 3, 6, 8, 7, 4, 1 };
            var b = new BiomeArt { ground = S(ground), details = details, trees = trees, stones = stones };
            for (int m = 1; m < 16; m++)
            {
                b.roadByMask[m] = S("Roads/" + folder + "/road" + sfx + "(" + roadFile[m] + ").png");
                b.riverByMask[m] = S("Rivers/" + folder + "/river" + sfx + "(" + riverFile[m] + ").png");
            }
            b.bridgeRoadNWSE = S("Rivers/" + folder + "/river" + sfx + "(16).png");
            b.bridgeRoadNESW = S("Rivers/" + folder + "/river" + sfx + "(17).png");
            return b;
        }

        static Sprite MakeSprite(string name, int w, int h, float ppu, Vector2 pivot, Func<float, float, Color> pixel)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = pixel((x + 0.5f) / w, (y + 0.5f) / h);
            tex.SetPixels(px);
            tex.Apply();
            string path = HelperDir + "/" + name + ".png";
            File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.spriteMode = (int)SpriteImportMode.Single;
            s.spritePixelsPerUnit = ppu;
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = pivot;
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.alphaIsTransparency = true;
            s.mipmapEnabled = false;
            s.filterMode = FilterMode.Bilinear;
            imp.SetTextureSettings(s);
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ------------------------------------------------------------------ veri

        static Dictionary<string, UnitData> BuildUnits(CharacterAnimSet knight)
        {
            var icon = knight.idle != null && knight.idle.IsValid ? knight.idle.Get(1, 0) : null;
            var d = new Dictionary<string, UnitData>();

            d["infantry"] = Unit("infantry", "Piyade", "Ucuz ve kalabalık. Savunucuları sayıyla boğar.", icon, knight,
                new Color(0.86f, 0.93f, 1f), 1f, 9, 3, 60, 45f, 1.05f, 7f, 1.1f, 0.40f, 3f, 0f, 0f);
            d["shield"] = Unit("shield", "Kalkanlı", "Okları kalkanıyla karşılar. Kulelerin önünden geçmek için ideal.", icon, knight,
                new Color(0.68f, 0.8f, 1f), 1.05f, 6, 4, 90, 80f, 0.85f, 6f, 1.2f, 0.40f, 2f, 0.3f, 0.65f);
            d["knight"] = Unit("knight", "Ağır şövalye", "Yavaş ama kapıları paramparça eder.", icon, knight,
                new Color(1f, 0.88f, 0.6f), 1.35f, 3, 5, 140, 220f, 0.8f, 18f, 1.4f, 0.5f, 22f, 0.35f, 0.35f);
            d["knight"].towerDamage = 10f;

            d["archer"] = Unit("archer", "Okçu", "Uzaktan ok atar: savunucuları, sur okçularını ve kuleleri yıpratır.", icon, knight,
                new Color(0.72f, 1f, 0.7f), 0.95f, 6, 3, 80, 30f, 1.0f, 6f, 1.5f, 3.2f, 2f, 0f, 0f);
            d["archer"].role = UnitRole.Ranged;
            d["archer"].towerDamage = 5f;
            EditorUtility.SetDirty(d["archer"]);

            d["climber"] = Unit("climber", "Merdivenci", "Merdiven dayayıp kulelere tırmanır ve onları yıkar.", icon, knight,
                new Color(1f, 0.8f, 0.62f), 1f, 5, 4, 110, 55f, 1.05f, 5f, 1.1f, 0.4f, 3f, 0.1f, 0.2f);
            d["climber"].role = UnitRole.Climber;
            d["climber"].towerDamage = 14f;
            EditorUtility.SetDirty(d["climber"]);
            return d;
        }

        static UnitData Unit(string id, string name, string desc, Sprite icon, CharacterAnimSet anim, Color tint, float scale,
            int squad, int cost, int price, float hp, float speed, float dmg, float interval, float range, float gate,
            float meleeArmor, float arrowArmor)
        {
            var u = ScriptableObject.CreateInstance<UnitData>();
            u.unitId = id; u.displayName = name; u.description = desc; u.icon = icon; u.anim = anim;
            u.tint = tint; u.scale = scale; u.squadSize = squad; u.cost = cost; u.goldPrice = price;
            u.maxHealth = hp; u.moveSpeed = speed; u.damage = dmg; u.attackInterval = interval; u.attackRange = range;
            u.gateDamage = gate; u.towerDamage = gate; u.meleeArmor = meleeArmor; u.arrowArmor = arrowArmor;
            AssetDatabase.CreateAsset(u, DataDir + "/Unit_" + id + ".asset");
            return u;
        }

        static TowerData Tower(string id, float range, float interval, float dmg, int arrows, float hp)
        {
            var t = ScriptableObject.CreateInstance<TowerData>();
            t.maxHealth = hp;
            t.towerId = id; t.displayName = "Okçu kulesi"; t.range = range; t.fireInterval = interval;
            t.damage = dmg; t.arrowsPerShot = arrows; t.projectileSpeed = 7f; t.arcHeight = 0.8f;
            AssetDatabase.CreateAsset(t, DataDir + "/Tower_" + id + ".asset");
            return t;
        }

        /// <summary>
        /// Zorluk tablosu. Her şehir WorldLayout.json'daki zorluk derecesine göre bu tablodan bir kale alır;
        /// aynı derecedeki kaleler sıralarına göre biraz daha güçlenir, bölge de küçük farklar ekler.
        /// </summary>
        static Dictionary<string, CastleData> BuildCastles(Dictionary<string, UnitData> u, Layout layout)
        {
            var light = Tower("archer_light", 3.8f, 1.6f, 5f, 1, 110f);
            var normal = Tower("archer", 4.2f, 1.3f, 6f, 1, 170f);
            var heavy = Tower("archer_heavy", 4.6f, 1.1f, 7f, 2, 250f);
            var d = new Dictionary<string, CastleData>();
            var seenPerTier = new int[7];

            foreach (var city in layout.cities)
            {
                if (city.home) continue;
                int diff = Mathf.Clamp(city.difficulty, 1, 5);
                int k = seenPerTier[diff]++;            // bu derecede kaçıncı kale
                float step = 1f + 0.08f * k;            // aynı derecede kademeli güçlenme
                Biome biome = city.biome == "D" ? Biome.Desert : city.biome == "W" ? Biome.Winter : Biome.Spring;
                CastleData c;
                switch (diff)
                {
                    case 1:
                        c = Castle(city.id, city.name, 1, CastleColor.Wood, 5, 5, false, false, false, 260f * step, light,
                            k == 0 ? G(u["infantry"], 1) : G(u["infantry"], 1, u["archer"], 1),
                            null, 0f, 0, 150f, 6, 10, 0.55f, 300 + 30 * k, 5 + k);
                        c.wallArchers = 2;
                        break;
                    case 2:
                        c = Castle(city.id, city.name, 2, CastleColor.Green, 5, 5, false, false, true, 380f * step, light,
                            k % 2 == 0 ? G(u["infantry"], 1, u["archer"], 1) : G(u["infantry"], 1, u["shield"], 1),
                            u["infantry"], 40f, 1, 150f, 6, 10, 0.6f, 450 + 40 * k, 8 + k);
                        c.wallArchers = 3;
                        break;
                    case 3:
                        c = Castle(city.id, city.name, 3, k % 2 == 0 ? CastleColor.Green : CastleColor.Red, 6, 6, true, k >= 3, true, 540f * step, normal,
                            G(u["infantry"], 2, u["shield"], 1, u["archer"], 1),
                            k % 2 == 0 ? u["infantry"] : u["shield"], 35f, 2, 165f, 7, 12, 0.65f, 700 + 50 * k, 12 + k);
                        c.wallArchers = 4;
                        break;
                    case 4:
                        c = Castle(city.id, city.name, 4, CastleColor.Red, 7, 7, true, true, true, 720f * step, k >= 2 ? heavy : normal,
                            G(u["infantry"], 2, u["shield"], 2, u["archer"], 2),
                            u["shield"], 35f, 2, 180f, 8, 14, 0.7f, 1000 + 60 * k, 17 + k);
                        c.wallArchers = 5;
                        break;
                    default:
                        c = Castle(city.id, city.name, 5, CastleColor.Red, 7, 7, true, true, true, 900f * step, heavy,
                            G(u["infantry"], 3, u["shield"], 2, u["archer"], 2, u["knight"], 1),
                            u["knight"], 40f, 3, 200f, 8, 15, 0.75f, 1500 + 80 * k, 25 + 2 * k);
                        c.wallArchers = 6;
                        break;
                }
                c.biome = biome;
                // bölge farkları: çöl kaleleri daha çok okçu, kış kaleleri daha sağlam kapı
                if (biome == Biome.Desert) c.wallArchers += 1;
                if (biome == Biome.Winter) c.gateHealth *= 1.15f;
                c.wallArcherUnit = u["archer"];
                c.description = Describe(c);
                EditorUtility.SetDirty(c);
                d[city.id] = c;
            }
            return d;
        }

        static string Describe(CastleData c)
        {
            string where = c.biome == Biome.Desert ? "Çölün ortasında" : c.biome == Biome.Winter ? "Karlı dağların eteğinde" : "Yeşil vadide";
            string size = c.width >= 7 ? "dev bir kale" : c.width >= 6 ? "taş bir kale" : c.keep ? "küçük bir hisar" : "ahşap kuleli küçük bir kale";
            var notes = new List<string>();
            if (c.midTowers) notes.Add("sur ortalarında ek kuleler var");
            if (c.secondGate) notes.Add("iki kapısı var, saldırıyı bölmek işe yarayabilir");
            if (c.reinforcement != null) notes.Add("savaş sırasında kapıdan takviye " + c.reinforcement.displayName.ToLower() + " çıkar");
            if (c.biome == Biome.Desert) notes.Add("surlarda fazladan okçu bekler");
            if (c.biome == Biome.Winter) notes.Add("kapısı buzla güçlendirilmiş");
            string s = where + " " + size + ".";
            if (notes.Count > 0) s += " " + char.ToUpper(notes[0][0]) + notes[0].Substring(1) + (notes.Count > 1 ? ", " + string.Join(", ", notes.GetRange(1, notes.Count - 1)) : "") + ".";
            return s;
        }

        static List<GarrisonEntry> G(params object[] pairs)
        {
            var list = new List<GarrisonEntry>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                list.Add(new GarrisonEntry { unit = (UnitData)pairs[i], squads = (int)pairs[i + 1] });
            return list;
        }

        static CastleData Castle(string id, string name, int diff, CastleColor color, int w, int dep,
            bool mid, bool second, bool keep, float gateHp, TowerData tower, List<GarrisonEntry> garrison,
            UnitData reinf, float reinfInterval, int reinfCount, float time, int cpStart, int cpMax, float regen,
            int reward, int income)
        {
            var c = ScriptableObject.CreateInstance<CastleData>();
            c.cityId = id; c.displayName = name; c.difficulty = diff; c.color = color;
            c.width = w; c.depth = dep; c.midTowers = mid; c.secondGate = second; c.keep = keep;
            c.gateHealth = gateHp; c.towerData = tower; c.garrison = garrison;
            c.reinforcement = reinf; c.reinforcementInterval = reinfInterval; c.reinforcementCount = reinfCount;
            c.timeLimit = time; c.startingCommandPoints = cpStart; c.maxCommandPoints = cpMax; c.commandRegenPerSecond = regen;
            c.goldReward = reward; c.passiveIncomePerMinute = income;
            c.defenderStrength = diff <= 1 ? 0.6f : diff == 2 ? 0.75f : diff == 3 ? 0.9f : diff == 4 ? 1f : 1.1f;
            AssetDatabase.CreateAsset(c, DataDir + "/Castle_" + id + ".asset");
            return c;
        }

        // ------------------------------------------------------------------ savaş sahnesi

        static string BuildBattleScene(CastleData test)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCamera(new Color(0.55f, 0.74f, 0.42f), 5f);
            var go = new GameObject("BattleManager");
            var bm = go.AddComponent<BattleManager>();
            bm.testCastle = test;
            EditorSceneManager.MarkSceneDirty(scene);
            string path = SceneDir + "/Battle.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        static Camera MakeCamera(Color bg, float size)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = size;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            go.AddComponent<AudioListener>();
            return cam;
        }

        // ------------------------------------------------------------------ harita sahnesi

        [Serializable] class LTile { public int u, v, m, var; public string t; }
        [Serializable] class LProp { public int u, v, var; public string k; public float ox, oy; }
        [Serializable] class LCity { public string id, name, biome; public int u, v, size, difficulty; public bool home; public int[] door; }
        [Serializable] class LLink { public string a, b; public int[] cells; }
        [Serializable] class Layout { public int size; public string[] biomeRows; public LTile[] tiles; public LProp[] props; public LCity[] cities; public LLink[] links; }

        static Biome BiomeAt(Layout l, int u, int v)
        {
            if (l.biomeRows == null || v < 0 || v >= l.biomeRows.Length || u < 0 || u >= l.biomeRows[v].Length) return Biome.Spring;
            char c = l.biomeRows[v][u];
            return c == 'D' ? Biome.Desert : c == 'W' ? Biome.Winter : Biome.Spring;
        }

        static string BuildMapScene(GameArt art, Dictionary<string, CastleData> castles, Layout layout)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = MakeCamera(new Color(0.62f, 0.83f, 0.94f), 3f);
            int n = layout.size;

            var world = new GameObject("World").transform;
            var ground = new GameObject("Ground").transform; ground.SetParent(world, false);
            var props = new GameObject("Props").transform; props.SetParent(world, false);
            var cityRoot = new GameObject("Cities").transform; cityRoot.SetParent(world, false);

            var tiles = new Dictionary<Vector2Int, LTile>();
            foreach (var t in layout.tiles) tiles[new Vector2Int(t.u, t.v)] = t;

            for (int u = 0; u < n; u++)
                for (int v = 0; v < n; v++)
                {
                    var bio = art.Biome(BiomeAt(layout, u, v));
                    Sprite s = bio.ground;
                    if (tiles.TryGetValue(new Vector2Int(u, v), out var t))
                    {
                        switch (t.t)
                        {
                            case "road": s = bio.Road(t.m); break;
                            case "river": s = bio.River(t.m); break;
                            case "bridgeNWSE": s = bio.bridgeRoadNWSE; break;
                            case "bridgeNESW": s = bio.bridgeRoadNESW; break;
                            case "field": s = art.fieldTiles[Mathf.Abs(t.var) % art.fieldTiles.Length]; break;
                        }
                    }
                    Sr(ground, "G", s, Iso.CellBase(u, v), Iso.GroundOrder(u, v));
                }

            foreach (var p in layout.props)
            {
                Vector3 pos = Iso.Surface(p.u + p.ox, p.v + p.oy);
                var bio = art.Biome(BiomeAt(layout, p.u, p.v));
                switch (p.k)
                {
                    case "tree": Sr(props, "Tree", bio.Pick(bio.trees, p.var), pos + Vector3.down * 0.04f, Iso.Order(pos, 3)); break;
                    case "stone": Sr(props, "Stone", bio.Pick(bio.stones, p.var), pos + Vector3.down * 0.08f, Iso.Order(pos, 3)); break;
                    case "detail": Sr(props, "Detail", bio.Pick(bio.details, p.var), pos + Vector3.down * 0.04f, Iso.Order(pos, -2)); break;
                    case "house": Sr(props, "House", art.houses[p.var % art.houses.Length], pos + Vector3.down * 0.14f, Iso.Order(pos, 3)); break;
                    case "mill": Sr(props, "Mill", art.mills[p.var % art.mills.Length], pos + Vector3.down * 0.14f, Iso.Order(pos, 3)); break;
                }
            }

            var cityById = new Dictionary<string, MapCity>();
            foreach (var c in layout.cities)
            {
                castles.TryGetValue(c.id, out var data);
                cityById[c.id] = BuildCity(cityRoot, art, c, data);
            }

            foreach (var l in layout.links)
            {
                if (!cityById.TryGetValue(l.a, out var a) || !cityById.TryGetValue(l.b, out var b)) continue;
                var pts = new List<Vector3>();
                for (int i = 0; i + 1 < l.cells.Length; i += 2) pts.Add(Iso.Surface(l.cells[i], l.cells[i + 1]));
                var forward = pts.GetRange(1, pts.Count - 1).ToArray();
                pts.Reverse();
                var backward = pts.GetRange(1, pts.Count - 1).ToArray();
                a.links.Add(new RoadLink { to = b, points = forward });
                b.links.Add(new RoadLink { to = a, points = backward });
            }

            // kahraman
            var heroGo = new GameObject("Hero");
            var shadow = Sr(heroGo.transform, "Shadow", art.shadow, Vector3.zero, -2);
            shadow.color = new Color(0f, 0f, 0f, 0.3f);
            shadow.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            var body = Sr(heroGo.transform, "Body", null, Vector3.zero, 0);
            body.transform.localScale = new Vector3(1f, 1f, 1f);
            body.color = new Color(0.86f, 0.93f, 1f);
            var anim = body.gameObject.AddComponent<SpriteAnimator>();
            anim.target = body;
            anim.set = art.knight;
            if (art.knight != null && art.knight.idle != null && art.knight.idle.IsValid) body.sprite = art.knight.idle.Get(0, 0);
            var hero = heroGo.AddComponent<MapHero>();
            hero.anim = anim;
            heroGo.AddComponent<YSort>().bias = 8;

            // kamera ve kontrol
            var rig = cam.gameObject.AddComponent<CameraRig>();
            float halfW = n * Iso.HalfW;
            rig.bounds = new Rect(-halfW * 0.85f, 0.5f, halfW * 1.7f, n * 2f * Iso.HalfH + 1.2f);
            rig.minSize = 2.0f;
            rig.maxSize = 5.5f;

            var ctrlGo = new GameObject("MapController");
            var ctrl = ctrlGo.AddComponent<MapController>();
            ctrl.rig = rig;
            ctrl.hero = hero;

            EditorSceneManager.MarkSceneDirty(scene);
            string path = SceneDir + "/Map.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        static MapCity BuildCity(Transform parent, GameArt art, LCity c, CastleData data)
        {
            var root = new GameObject("City_" + c.name);
            root.transform.SetParent(parent, false);
            float cu = c.u + (c.size - 1) * 0.5f, cv = c.v + (c.size - 1) * 0.5f;
            root.transform.position = Iso.Surface(cu, cv);

            var city = root.AddComponent<MapCity>();
            city.castle = c.home ? null : data;
            city.isHomeCity = c.home;
            city.homeName = c.name;
            city.homeId = c.id;
            city.doorPoint = Iso.Surface(c.door[0], c.door[1]);
            CastleColor color = c.home ? CastleColor.Blue : data != null ? data.color : CastleColor.Red;

            int s = c.size;
            for (int i = 0; i < s; i++)
                for (int j = 0; j < s; j++)
                {
                    int u = c.u + i, v = c.v + j;
                    bool corner = (i == 0 || i == s - 1) && (j == 0 || j == s - 1);
                    if (corner || s == 2)
                    {
                        int variant = (u * 7 + v * 3) % 9;
                        var t = Sr(root.transform, "Tower", art.Tower(color, variant), Iso.BlockBase(u, v), Iso.BlockOrder(u, v, 1));
                        city.towers.Add(t);
                        city.towerVariants.Add(variant);
                    }
                    else if (i == 0 || i == s - 1)
                        Sr(root.transform, "Wall", art.wallsAlongV[0], Iso.BlockBase(u, v), Iso.BlockOrder(u, v, 1));
                    else if (j == 0 || j == s - 1)
                        Sr(root.transform, "Wall", art.wallsAlongU[0], Iso.BlockBase(u, v), Iso.BlockOrder(u, v, 1));
                    else
                    {
                        Sr(root.transform, "Keep", art.wallCross, Iso.BlockBase(u, v), Iso.BlockOrder(u, v, 1));
                        var t = Sr(root.transform, "KeepTower", art.Tower(color, 4), Iso.BlockBase(u, v, 1), Iso.BlockOrder(u, v, 2));
                        city.towers.Add(t);
                        city.towerVariants.Add(4);
                    }
                }

            // dokunma alanı
            var col = root.AddComponent<BoxCollider2D>();
            float w = (s + 0.6f) * Iso.HalfW * 2f;
            float h = s * Iso.HalfH * 2f + 1.6f;
            col.size = new Vector2(w, h);
            col.offset = new Vector2(0f, h * 0.5f - s * Iso.HalfH - 0.3f);

            float top = s >= 3 ? 1.75f : 1.2f;
            city.label = WorldText(root.transform, "Label", c.name, 4f, new Vector3(0f, top + 0.34f, 0f));
            city.subLabel = WorldText(root.transform, "SubLabel", "", 2.6f, new Vector3(0f, top, 0f));
            return city;
        }

        static TextMeshPro WorldText(Transform parent, string name, string text, float size, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var t = go.AddComponent<TextMeshPro>();
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.fontStyle = FontStyles.Bold;
            t.rectTransform.sizeDelta = new Vector2(6f, 0.6f);
            t.color = new Color(0.25f, 0.14f, 0.02f);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 31000;
            return t;
        }

        static SpriteRenderer Sr(Transform parent, string name, Sprite s, Vector3 pos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos; // dünya konumu
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            return sr;
        }

        static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
