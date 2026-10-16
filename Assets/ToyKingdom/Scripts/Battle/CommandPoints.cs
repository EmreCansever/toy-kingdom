using UnityEngine;

namespace ToyKingdom
{
    /// <summary>Savaş sırasında dolan, birlik sürmek için harcanan enerji.</summary>
    public class CommandPoints
    {
        public float Current { get; private set; }
        public int Max { get; }
        readonly float regenPerSecond;

        public CommandPoints(int start, int max, float regenPerSecond)
        {
            Max = Mathf.Max(1, max);
            Current = Mathf.Clamp(start, 0, Max);
            this.regenPerSecond = regenPerSecond;
        }

        public void Tick(float deltaTime) =>
            Current = Mathf.Min(Max, Current + regenPerSecond * deltaTime);

        public bool CanAfford(int cost) => Current >= cost;

        /// <summary>Savaş sandığından komuta puanı (en fazla doluluk sınırına kadar).</summary>
        public void Add(float n) => Current = Mathf.Min(Max, Current + n);

        public bool TrySpend(int cost)
        {
            if (!CanAfford(cost)) return false;
            Current -= cost;
            return true;
        }
    }
}
