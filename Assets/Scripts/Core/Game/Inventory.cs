using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrazyAquarium.Game
{
    public enum ResourceKind
    {
        /// <summary>Salvaged wood. Cheap structure and fuel.</summary>
        Scrap,

        /// <summary>Drift salvage. General purpose building stock.</summary>
        Plank,

        /// <summary>Metal off wrecks. Heavy, strong, buoyant only as a sealed drum.</summary>
        Metal,

        /// <summary>Sealed containers. The only source of clean water.</summary>
        Water,

        /// <summary>Preserved rations from flooded stores.</summary>
        Food
    }

    /// <summary>
    /// Player inventory. Flat counts per resource, with costs defined per building
    /// piece so the economy lives in one place.
    ///
    /// Engine-free so the economy can be balanced headlessly. Sourcing rates and costs
    /// are the numbers most likely to be tuned, and they change constantly early on.
    /// </summary>
    public sealed class Inventory
    {
        private readonly Dictionary<ResourceKind, int> _counts = new Dictionary<ResourceKind, int>();

        public event Action<ResourceKind, int> Changed;

        public int Get(ResourceKind kind) => _counts.TryGetValue(kind, out int n) ? n : 0;

        public void Add(ResourceKind kind, int amount)
        {
            if (amount <= 0) return;
            int next = Get(kind) + amount;
            _counts[kind] = next;
            Changed?.Invoke(kind, next);
        }

        public bool Has(ResourceKind kind, int amount) => Get(kind) >= amount;

        /// <summary>
        /// Spends resources if affordable. Returns false and changes nothing when the
        /// cost is not met, so a failed build never partially charges the player.
        /// </summary>
        public bool TrySpend(IReadOnlyDictionary<ResourceKind, int> cost)
        {
            if (cost == null) return true;

            foreach (KeyValuePair<ResourceKind, int> entry in cost)
            {
                if (Get(entry.Key) < entry.Value) return false;
            }

            foreach (KeyValuePair<ResourceKind, int> entry in cost)
            {
                _counts[entry.Key] = Get(entry.Key) - entry.Value;
                Changed?.Invoke(entry.Key, _counts[entry.Key]);
            }
            return true;
        }

        /// <summary>
        /// Returns a fraction of a cost, for dismantle salvage. A partial refund is
        /// deliberate: full refunds make demolition a free undo, which removes any
        /// cost to experimenting with the build.
        /// </summary>
        public int RefundFraction(IReadOnlyDictionary<ResourceKind, int> cost, float fraction = 0.5f)
        {
            int recovered = 0;
            if (cost == null) return 0;

            foreach (KeyValuePair<ResourceKind, int> entry in cost)
            {
                int amount = Mathf.FloorToInt(entry.Value * fraction);
                if (amount <= 0) continue;
                Add(entry.Key, amount);
                recovered += amount;
            }
            return recovered;
        }

        public int Total => TotalCount();

        private int TotalCount()
        {
            int sum = 0;
            foreach (KeyValuePair<ResourceKind, int> entry in _counts) sum += entry.Value;
            return sum;
        }

        public void Clear()
        {
            _counts.Clear();
            Changed?.Invoke(ResourceKind.Scrap, 0);
        }

        /// <summary>One-line inventory summary for a HUD readout.</summary>
        public string Describe()
        {
            return $"scrap{Get(ResourceKind.Scrap)} plank{Get(ResourceKind.Plank)} " +
                   $"metal{Get(ResourceKind.Metal)} water{Get(ResourceKind.Water)} food{Get(ResourceKind.Food)}";
        }
    }
}
