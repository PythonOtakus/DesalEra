using UnityEngine;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// Active <see cref="UiLayoutSettings"/> for the running HUD. Bound by
    /// <see cref="UiRoot"/>; falls back to Resources or an in-memory default.
    /// </summary>
    public static class UiLayout
    {
        public const string ResourcesPath = "UiLayoutSettings";

        private static UiLayoutSettings _active;
        private static UiLayoutSettings _fallback;

        public static UiLayoutSettings Active
        {
            get
            {
                if (_active != null) return _active;
                _active = Resources.Load<UiLayoutSettings>(ResourcesPath);
                if (_active != null) return _active;
                if (_fallback == null)
                {
                    _fallback = ScriptableObject.CreateInstance<UiLayoutSettings>();
                    _fallback.name = "UiLayoutSettings (default)";
                    _fallback.hideFlags = HideFlags.HideAndDontSave;
                }
                return _fallback;
            }
        }

        public static void Bind(UiLayoutSettings settings)
        {
            _active = settings;
        }

        public static void ClearBind()
        {
            _active = null;
        }
    }
}
