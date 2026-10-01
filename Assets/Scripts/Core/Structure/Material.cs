using System;

namespace CrazyAquarium.Structure
{
    /// <summary>
    /// Material presets for structural members.
    /// Densities are the numbers cited in the research brief (wood 10, steel 50,
    /// concrete 80 t/m^2) expressed as the material's own bulk density in kg/m^3.
    /// </summary>
    public enum MaterialKind
    {
        Wood,
        Plastic,
        Steel,
        Concrete
    }

    public static class MaterialProperties
    {
        /// <summary>Mass density of the material itself, kg/m^3.</summary>
        public static float DensityKgPerM3(MaterialKind kind)
        {
            switch (kind)
            {
                case MaterialKind.Wood: return 600f;
                case MaterialKind.Plastic: return 950f;
                case MaterialKind.Steel: return 7850f;
                case MaterialKind.Concrete: return 2400f;
                default: return 600f;
            }
        }

        /// <summary>Face density used by the brief: wood 10, steel 50, concrete 80 t/m^2.</summary>
        public static float FaceDensityTonsPerM2(MaterialKind kind)
        {
            switch (kind)
            {
                case MaterialKind.Wood: return 10f;
                case MaterialKind.Plastic: return 2f;
                case MaterialKind.Steel: return 50f;
                case MaterialKind.Concrete: return 80f;
                default: return 10f;
            }
        }

        /// <summary>
        /// Axial capacity per unit cross-section, in kN/m^2. This is a stress, not a
        /// total, and member capacity multiplies it by cross-section area.
        ///
        /// It has to be a stress. An earlier version stored a flat capacity per
        /// material, which meant a 0.25 m^2 column was exactly as strong as a 0.05 m^2
        /// one while weighing five times as much, so widening a column made the
        /// structure weaker. The area sweep test now guards against that regression.
        /// </summary>
        public static float AxialCapacityKnPerM2(MaterialKind kind)
        {
            switch (kind)
            {
                case MaterialKind.Wood: return 500f;
                case MaterialKind.Plastic: return 400f;
                case MaterialKind.Steel: return 4500f;
                case MaterialKind.Concrete: return 2800f;
                default: return 500f;
            }
        }

        /// <summary>
        /// Bending capacity per unit section modulus, in kN*m per m^3. Section modulus
        /// for a square section of side s is s^3/6, and s is the square root of the
        /// cross-section area, so moment capacity grows with area^1.5.
        /// </summary>
        public static float BendingCapacityKnMPerM3(MaterialKind kind)
        {
            switch (kind)
            {
                case MaterialKind.Wood: return 8500f;
                case MaterialKind.Plastic: return 6000f;
                case MaterialKind.Steel: return 41000f;
                case MaterialKind.Concrete: return 22000f;
                default: return 8500f;
            }
        }

        /// <summary>Max axial load in kN for a member of the given cross-section.</summary>
        public static float MaxAxialLoadKn(MaterialKind kind, float crossSectionAreaM2)
        {
            return AxialCapacityKnPerM2(kind) * crossSectionAreaM2;
        }

        /// <summary>Max bending moment in kN*m for a member of the given cross-section.</summary>
        public static float MaxBendingMomentKnM(MaterialKind kind, float crossSectionAreaM2)
        {
            float side = (float)System.Math.Sqrt(crossSectionAreaM2);
            float sectionModulusM3 = side * side * side / 6f;
            return BendingCapacityKnMPerM3(kind) * sectionModulusM3;
        }
    }

    /// <summary>How a member behaves once its limits are exceeded.</summary>
    public enum FailureMode
    {
        /// <summary>Deforms and keeps carrying load: buckling, sagging.</summary>
        Yield,
        /// <summary>Snaps immediately and sheds its load onto neighbours.</summary>
        Fracture
    }
}
