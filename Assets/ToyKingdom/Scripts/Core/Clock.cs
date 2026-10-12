using System;

namespace ToyKingdom
{
    /// <summary>
    /// Oyun saati: dünya gerçek saate (UTC) göre ilerler. Duraklatınca tüm zaman damgaları ileri kaydırılır,
    /// böylece duraklama boyunca hiçbir şey ilerlemez (ordu yürümez, kervan durur, mektup süresi işlemez).
    /// </summary>
    public static class Clock
    {
        public static void Shift(SaveData d, long delta)
        {
            if (d == null || delta <= 0) return;
            long now = DateTime.UtcNow.Ticks;
            d.lastIncomeTicks += delta;
            foreach (var r in d.rivals)
            {
                if (r.Attacking) { r.attackStartTicks += delta; r.attackArriveTicks += delta; }
                if (r.treatyUntilTicks > now) r.treatyUntilTicks += delta;
            }
            foreach (var l in d.letters)
                if (l.NeedsAnswer && l.expiresTicks > 0) l.expiresTicks += delta;
            foreach (var c in d.caravans) { c.startTicks += delta; c.arriveTicks += delta; }
            foreach (var r in d.roads) if (r.nextCaravanTicks > 0) r.nextCaravanTicks += delta;
            foreach (var c in d.cities)
            {
                if (c.feastReadyTicks > now) c.feastReadyTicks += delta;
                if (c.levyReadyTicks > now) c.levyReadyTicks += delta;
                if (c.incomeModUntil > now) c.incomeModUntil += delta;
            }
            for (int i = 0; i < d.caravanTicks.Count; i++) d.caravanTicks[i] += delta;
        }
    }
}
