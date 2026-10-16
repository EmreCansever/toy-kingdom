using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyKingdom
{
    /// <summary>
    /// Krallar arası ilişkiler: iki rakip kral birbirleriyle de uğraşır.
    /// Aralarındaki ilişki sınır kavgalarıyla bozulur; çok bozulursa savaşa tutuşur, sınır şehirlerini birbirinden alırlar.
    /// Oyuncu ikisini birden korkutacak kadar büyürse ortak düşmana karşı yakınlaşır, sonunda ittifak kurarlar:
    /// müttefikler aynı anda saldırabilir ve birine saldırınca öteki de sana soğur. Oyuncu altınla onları kışkırtabilir.
    /// </summary>
    public static class RivalWars
    {
        public const int None = 0, War = 1, Alliance = 2;
        public const int InciteCost = 150;

        const float WarBelow = -45f, PeaceAbove = -15f, AllyAbove = 45f, BreakAllyBelow = 15f;

        public static bool AtWar(SaveData d) => d.kingsPact == War && BothAlive(d);
        public static bool Allied(SaveData d) => d.kingsPact == Alliance && BothAlive(d);

        static bool BothAlive(SaveData d)
        {
            int n = 0;
            foreach (var r in d.rivals) if (r.alive) n++;
            return n >= 2;
        }

        /// <summary>Diplomasi ekranı için kısa durum.</summary>
        public static string Status(SaveData d)
        {
            if (!BothAlive(d)) return "";
            string rel = Mathf.RoundToInt(d.kingsRelation).ToString();
            if (AtWar(d)) return "<color=#B8382F>Krallar birbiriyle savaşta</color> (" + rel + ")";
            if (Allied(d)) return "<color=#B8382F>Krallar sana karşı müttefik</color> (" + rel + ")";
            return "Krallar arası ilişki: " + (d.kingsRelation < -20f ? "gergin" : d.kingsRelation > 20f ? "yakın" : "soğuk") + " (" + rel + ")";
        }

        public static RivalState Other(SaveData d, RivalState r)
        {
            foreach (var o in d.rivals) if (o != r && o.alive) return o;
            return null;
        }

        // ------------------------------------------------------------------ adım

        public static void Tick(SaveData d, IList<CityInfo> cities, int minutes, bool online, RivalAI.Report rep)
        {
            if (!online || minutes <= 0) return;
            if (!BothAlive(d)) { d.kingsPact = None; return; }
            RivalState a = null, b = null;
            foreach (var r in d.rivals) if (r.alive) { if (a == null) a = r; else if (b == null) b = r; }

            // ortak düşman yakınlaştırır, yoksa eski kan davası sürer
            float fear = (RivalAI.Threat(d, a) + RivalAI.Threat(d, b)) * 0.5f;
            float target = fear > 0.35f ? 30f + 40f * fear : -25f;
            d.kingsRelation += (target - d.kingsRelation) * Mathf.Min(1f, 0.025f * minutes);

            // ara sıra sınır kavgaları
            d.kingsEventTimer -= minutes;
            if (d.kingsEventTimer <= 0f)
            {
                d.kingsEventTimer = 7f + UnityEngine.Random.Range(0f, 5f);
                float hit = UnityEngine.Random.Range(6f, 15f);
                d.kingsRelation -= hit;
                if (hit > 11f && d.kingsPact != War)
                    rep.messages.Add(RivalAI.Get(a.id).name + " ile " + RivalAI.Get(b.id).name + " arasında sınır kavgası çıktı");
            }
            d.kingsRelation = Mathf.Clamp(d.kingsRelation, -100f, 100f);

            // anlaşma durumu
            switch (d.kingsPact)
            {
                case None:
                    if (d.kingsRelation < WarBelow) StartWar(d, a, b, rep);
                    else if (d.kingsRelation > AllyAbove) StartAlliance(d, a, b, rep);
                    break;
                case War:
                    if (d.kingsRelation > PeaceAbove)
                    {
                        d.kingsPact = None;
                        rep.messages.Add(RivalAI.Get(a.id).name + " ile " + RivalAI.Get(b.id).name + " ateşkes imzaladı");
                    }
                    else WarStep(d, cities, a, b, minutes, rep);
                    break;
                case Alliance:
                    if (d.kingsRelation < BreakAllyBelow)
                    {
                        d.kingsPact = None;
                        rep.messages.Add(RivalAI.Get(a.id).name + " ile " + RivalAI.Get(b.id).name + " arasındaki ittifak bozuldu");
                        Court.Reply(d, "court", "İttifak çözüldü",
                            "Sevindirici haber, efendim! Kumların sultanı ile kuzeyin kraliçesi arasındaki ittifak çatırdadı; elçileri birbirinin sarayından kovuldu. Artık sırtımızda iki kılıç birden yok.");
                    }
                    break;
            }
        }

        static void StartWar(SaveData d, RivalState a, RivalState b, RivalAI.Report rep)
        {
            d.kingsPact = War;
            d.kingsWarTimer = 4f;
            rep.messages.Add(RivalAI.Get(a.id).name + " ile " + RivalAI.Get(b.id).name + " savaşa tutuştu!");
            Court.Reply(d, "court", "Komşular birbirine girdi",
                "Müjde mi desem, kara haber mi bilemedim efendim: " + RivalAI.KingFull(a.id) + " ile " + RivalAI.KingFull(b.id) + " arasında kılıçlar çekildi. " +
                "Birbirlerinin sınır kalelerine saldırıyorlar.\n\nİkisi de yorulurken biz güçleniriz. Lakin unutmayın: savaştan galip çıkan, eskisinden de aç gözlü olur.");
        }

        static void StartAlliance(SaveData d, RivalState a, RivalState b, RivalAI.Report rep)
        {
            d.kingsPact = Alliance;
            rep.messages.Add(RivalAI.Get(a.id).name + " ile " + RivalAI.Get(b.id).name + " sana karşı ittifak kurdu!");
            Court.Reply(d, "court", "Sana karşı ittifak",
                "Kötü haber, efendim. Ordumuzun büyüklüğü iki kralı da korkuttu; " + RivalAI.KingFull(a.id) + " ile " + RivalAI.KingFull(b.id) + " el sıkıştı.\n\n" +
                "Artık aynı anda saldırabilirler; birine kılıç kaldırırsak öteki de bize düşman olur. Elçilerimiz aralarına nifak sokabilir; ama altın ister.");
        }

        static string[] Neighbors(IList<CityInfo> cities, string id)
        {
            foreach (var c in cities) if (c.id == id) return c.neighbors ?? new string[0];
            return new string[0];
        }

        static int Difficulty(IList<CityInfo> cities, string id)
        {
            foreach (var c in cities) if (c.id == id) return Mathf.Max(1, c.difficulty);
            return 1;
        }

        static string Name(IList<CityInfo> cities, string id)
        {
            foreach (var c in cities) if (c.id == id) return c.name;
            return id;
        }

        static void WarStep(SaveData d, IList<CityInfo> cities, RivalState a, RivalState b, int minutes, RivalAI.Report rep)
        {
            d.kingsWarTimer -= minutes;
            if (d.kingsWarTimer > 0f) return;
            d.kingsWarTimer = 5f + UnityEngine.Random.Range(0f, 3f);

            // güçlü olan daha sık saldırır
            float pa = Mathf.Max(1f, a.power), pb = Mathf.Max(1f, b.power);
            var atk = UnityEngine.Random.value < pa / (pa + pb) ? a : b;
            var def = atk == a ? b : a;
            var atkDef = RivalAI.Get(atk.id);
            var defDef = RivalAI.Get(def.id);

            // sınır şehri: savunanın, saldıranın bir şehrine yolla bağlı (başkent hariç) şehri
            string target = null, best = null;
            float bestScore = float.MaxValue;
            foreach (var id in RivalAI.OwnedBy(d, def.id))
            {
                if (id == def.capitalId) continue;
                bool border = false;
                foreach (var n in Neighbors(cities, id)) if (d.RivalOwner(n) == atk.id) { border = true; break; }
                if (!border) continue;
                float score = Difficulty(cities, id) + UnityEngine.Random.value;
                if (score < bestScore) { bestScore = score; best = id; }
            }
            target = best;

            if (target == null)
            {
                // sınır yok: akınlar, iki taraf da kan kaybeder
                float loss = UnityEngine.Random.Range(6f, 13f);
                atk.power = Mathf.Max(5f, atk.power - loss * 0.7f);
                def.power = Mathf.Max(5f, def.power - loss);
                if (UnityEngine.Random.value < 0.5f) rep.messages.Add(atkDef.name + " akıncıları " + defDef.name + " topraklarını yağmaladı");
                return;
            }

            float need = 18f * Difficulty(cities, target) + def.power * 0.35f;
            float strike = atk.power * UnityEngine.Random.Range(0.5f, 0.9f);
            if (strike > need)
            {
                d.State(target).owner = atk.id;
                d.State(target).unrest = 40f;
                atk.power = Mathf.Max(5f, atk.power - need * 0.6f);
                def.power = Mathf.Max(5f, def.power - need * 0.5f);
                rep.captured.Add(target);
                rep.messages.Add(atkDef.name + ", " + defDef.name + " elindeki " + Name(cities, target) + " şehrini aldı");
            }
            else
            {
                atk.power = Mathf.Max(5f, atk.power - strike * 0.4f);
                def.power = Mathf.Max(5f, def.power - strike * 0.25f);
                rep.messages.Add(defDef.name + ", " + Name(cities, target) + " önünde " + atkDef.name + " ordusunu püskürttü");
            }
        }

        // ------------------------------------------------------------------ oyuncu

        /// <summary>Elçiler aracılığıyla iki kralı birbirine düşür.</summary>
        public static string Incite(SaveData d, RivalState r)
        {
            if (!BothAlive(d)) return "Kışkırtılacak iki kral kalmadı";
            if (d.gold < InciteCost) return "Yeterli altın yok";
            d.gold -= InciteCost;
            var other = Other(d, r);
            d.kingsRelation = Mathf.Clamp(d.kingsRelation - 25f, -100f, 100f);
            if (UnityEngine.Random.value < 0.25f)
            {
                r.relation = Mathf.Clamp(r.relation - 20f, -100f, 100f);
                Reputation.Change(d, -3f, "Kışkırtıcı elçin yakalandı");
                return "Elçin " + RivalAI.Get(r.id).name + " sarayında yakalandı! Kral sana öfkeli (ilişki -20); yine de nifak tohumu ekildi";
            }
            return RivalAI.Get(r.id).name + " sarayına " + (other != null ? RivalAI.Get(other.id).name : "komşusu") + " hakkında zehirli sözler fısıldandı (krallar arası ilişki -25)";
        }

        /// <summary>Oyuncu bir krala saldırınca müttefiki de soğur.</summary>
        public static string OnPlayerAttacked(SaveData d, RivalState r)
        {
            if (!Allied(d) || r == null) return null;
            var ally = Other(d, r);
            if (ally == null) return null;
            ally.relation = Mathf.Clamp(ally.relation - 12f, -100f, 100f);
            ally.attackTimer = Mathf.Min(ally.attackTimer, 5f);
            return RivalAI.Get(ally.id).name + " müttefikine saldırdığın için sana kılıç biliyor";
        }
    }
}
