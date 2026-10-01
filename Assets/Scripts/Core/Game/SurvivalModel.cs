using UnityEngine;

namespace CrazyAquarium.Game
{
    public enum Vital
    {
        Food,
        Water,
        Health,
        Stamina
    }

    /// <summary>
    /// Survival values for the player, and the rules that move them.
    ///
    /// Deliberately engine-free apart from Mathf, so the whole thing is testable in a
    /// plain dotnet process. Balance work on survival is the most likely thing to be
    /// iterated repeatedly, and paying the editor's memory cost per experiment makes
    /// that impractical on a 15 GB machine.
    ///
    /// The design intent is pressure, not attrition. Losing is a real possibility
    /// that recovery can walk back from, which is the gap the original design brief
    /// left open. See docs/plan.md P0-2.
    /// </summary>
    public sealed class SurvivalModel
    {
        public const float MaxValue = 100f;

        /// <summary>Hunger points drained per minute at rest.</summary>
        public float FoodDrainPerMinute = 0.9f;

        /// <summary>Thirst points drained per minute at rest.</summary>
        public float WaterDrainPerMinute = 1.4f;

        /// <summary>Stamina points recovered per minute while idle.</summary>
        public float StaminaRecoveryPerMinute = 6f;

        /// <summary>Stamina points drained per minute while exerting.</summary>
        public float StaminaDrainPerMinute = 9f;

        /// <summary>Health lost per minute once food or water hits zero.</summary>
        public float StarvationDamagePerMinute = 2.5f;

        /// <summary>Health restored per minute when both food and water are topped up.</summary>
        public float HealPerMinute = 4f;

        /// <summary>
        /// Multiplier applied to food and water drain while doing heavy work. Keeps
        /// exertion meaningful without letting a player starve while exploring.
        /// </summary>
        public float ExertionDrainMultiplier = 2.2f;

        public float Food { get; private set; } = MaxValue;
        public float Water { get; private set; } = MaxValue;
        public float Health { get; private set; } = MaxValue;
        public float Stamina { get; private set; } = MaxValue;

        /// <summary>True once health has reached zero.</summary>
        public bool IsDead { get; private set; }

        /// <summary>True while food or water is at zero and health is being lost.</summary>
        public bool IsInCrisis => Food <= 0f || Water <= 0f;

        /// <summary>Total minutes simulated, for a HUD readout and for tests.</summary>
        public float ElapsedMinutes { get; private set; }

        public float Get(Vital vital)
        {
            switch (vital)
            {
                case Vital.Food: return Food;
                case Vital.Water: return Water;
                case Vital.Health: return Health;
                default: return Stamina;
            }
        }

        public void Set(Vital vital, float value)
        {
            float clamped = Mathf.Clamp(value, 0f, MaxValue);
            switch (vital)
            {
                case Vital.Food: Food = clamped; break;
                case Vital.Water: Water = clamped; break;
                case Vital.Health: Health = clamped; break;
                case Vital.Stamina: Stamina = clamped; break;
            }
        }

        public void Add(Vital vital, float amount) => Set(vital, Get(vital) + amount);

        /// <summary>
        /// Advances the simulation by <paramref name="minutes"/>.
        ///
        /// Food and water drain independently, so a player can be fine on one and
        /// still die of the other. That separation is the point: it forces a choice
        /// about what to fetch rather than a single "supplies" number to top up.
        /// </summary>
        public void Advance(float minutes, bool exerting = false)
        {
            if (minutes <= 0f || IsDead) return;

            float scale = exerting ? ExertionDrainMultiplier : 1f;
            Food -= FoodDrainPerMinute * minutes * scale;
            Water -= WaterDrainPerMinute * minutes * scale;

            // Stamina recovers when idle and drains under exertion, so pushing hard
            // costs something and standing still is not punished.
            Stamina += (exerting ? -StaminaDrainPerMinute : StaminaRecoveryPerMinute) * minutes;

            // Health only bleeds when a food or water reservoir is empty, and faster
            // the further past empty it has fallen. A slow trickle is survivable, a
            // long neglect is not.
            float crisisSeverity = 0f;
            if (Food <= 0f) crisisSeverity = Mathf.Max(crisisSeverity, 1f);
            if (Water <= 0f) crisisSeverity = Mathf.Max(crisisSeverity, 1f);
            if (Food < 0f) crisisSeverity = Mathf.Max(crisisSeverity, -Food / 20f);
            if (Water < 0f) crisisSeverity = Mathf.Max(crisisSeverity, -Water / 20f);

            if (crisisSeverity > 0f)
            {
                Health -= StarvationDamagePerMinute * minutes * crisisSeverity;
            }

            // Reserves are allowed to go negative so a HUD can show how far past
            // empty they are, which is the signal that recovery matters right now.
            Food = Mathf.Clamp(Food, -50f, MaxValue);
            Water = Mathf.Clamp(Water, -50f, MaxValue);
            Stamina = Mathf.Clamp(Stamina, 0f, MaxValue);

            if (Health <= 0f)
            {
                Health = 0f;
                IsDead = true;
            }

            ElapsedMinutes += minutes;
        }

        /// <summary>
        /// Restores health when both reservoirs are above a threshold. The design brief
        /// lists health as one of five linked indicators but never says what heals it,
        /// and without a recovery path a health bar is only a countdown.
        /// </summary>
        public void TryHeal(float minutes)
        {
            if (IsDead || minutes <= 0f) return;
            if (Food <= 0f || Water <= 0f) return;

            Health = Mathf.Min(MaxValue, Health + HealPerMinute * minutes);
        }

        /// <summary>Health a single emergency ration buys.</summary>
        public const float EmergencyRationHeal = 18f;

        /// <summary>
        /// Emergency food. Buys health at an inflated cost, which is what makes it a
        /// stopgap rather than the sustainable option.
        /// </summary>
        public void ConsumeRation()
        {
            if (IsDead) return;
            Add(Vital.Food, 12f);
            Add(Vital.Water, 6f);
            Health = Mathf.Min(MaxValue, Health + EmergencyRationHeal);
        }

        public void Reset()
        {
            Food = MaxValue;
            Water = MaxValue;
            Health = MaxValue;
            Stamina = MaxValue;
            IsDead = false;
            ElapsedMinutes = 0f;
        }

        /// <summary>One-line summary for an on-screen readout.</summary>
        public string Describe()
        {
            string state = IsDead ? " DEAD" : IsInCrisis ? " CRISIS" : string.Empty;
            return $"F{Food:F0} W{Water:F0} H{Health:F0} S{Stamina:F0} t{ElapsedMinutes:F0}min{state}";
        }
    }
}
