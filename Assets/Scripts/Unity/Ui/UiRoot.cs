using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// Owns the canvas, the font and the screen registry, and builds the always-on HUD
    /// layer. Nothing about the interface lives in the scene file.
    ///
    /// Scale is reference-resolution based rather than a raw pixels-per-unit canvas, so
    /// the layout holds from a 1280-wide window to a 4K one without every metric being
    /// re-tuned.
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        /// <summary>Reference size. Every metric in <see cref="UiTheme"/> assumes this.</summary>
        private static readonly Vector2 Reference = new Vector2(1920f, 1080f);

        private static Font _font;
        private static bool _fontIsOurs;

        /// <summary>
        /// The font every label uses. Prefer a CJK face so Chinese HUD copy actually draws;
        /// LegacyRuntime / Arial render CJK as empty boxes.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;

                _font = Font.CreateDynamicFontFromOSFont(new[]
                {
                    "Microsoft YaHei UI",
                    "Microsoft YaHei",
                    "微软雅黑",
                    "SimHei",
                    "Noto Sans SC",
                    "Source Han Sans SC",
                    "PingFang SC",
                    "Segoe UI",
                    "Arial"
                }, 20);

                if (_font != null)
                {
                    _fontIsOurs = true;
                    return _font;
                }

                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        private readonly Dictionary<string, GameObject> _screens = new Dictionary<string, GameObject>();
        private Canvas _canvas;
        private RectTransform _hud;
        private RectTransform _overlay;

        /// <summary>Surviving stats, drawn above the always-on HUD.</summary>
        public VitalRowView Vitals { get; private set; }

        /// <summary>Situational states: sheltered, resting, storm.</summary>
        public StatusStripView Status { get; private set; }

        /// <summary>The build bar along the bottom.</summary>
        public BuildBarView BuildBar { get; private set; }

        /// <summary>The single-line feedback strip.</summary>
        public NoticeView Notice { get; private set; }

        /// <summary>Anything that must draw over the HUD, e.g. a modal panel.</summary>
        public RectTransform Overlay => _overlay;

        public Canvas Canvas => _canvas;

        public void Initialise()
        {
            BuildCanvas();
            BuildLayers();
            BuildHud();
        }

        private void BuildCanvas()
        {
            var go = new GameObject("UiCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, worldPositionStays: false);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            // Neither axis wins outright: a wide window gets its extra width from the
            // layout filling out, not from the type growing.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            go.AddComponent<GraphicRaycaster>();

            EnsureEventSystem();
        }

        /// <summary>
        /// A canvas cannot receive clicks without an EventSystem, and the project uses the
        /// legacy Input class throughout, so this is the matching input module rather than
        /// the new Input System one.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<StandaloneInputModule>();
            go.transform.SetParent(null);
        }

        private void BuildLayers()
        {
            _hud = UiFactory.Container(_canvas.transform, "Hud");
            UiFactory.Stretch(_hud);

            _overlay = UiFactory.Container(_canvas.transform, "Overlay");
            UiFactory.Stretch(_overlay);
        }

        private void BuildHud()
        {
            Vitals = VitalRowView.Create(_hud);
            Status = StatusStripView.Create(_hud);
            BuildBar = BuildBarView.Create(_hud);
            Notice = NoticeView.Create(_hud);
        }

        /// <summary>
        /// Registers a screen, or returns the existing one. Screens are created on demand
        /// rather than all at startup: an inventory that costs nothing to open should not
        /// cost anything to load. They start hidden, because a panel that exists but is
        /// waiting for a keypress must not be visible while the player waits.
        /// </summary>
        public T Screen<T>(string id, System.Func<T> create) where T : Component
        {
            if (_screens.TryGetValue(id, out GameObject existing) && existing != null)
            {
                return existing.GetComponent<T>();
            }

            T screen = create();
            RectTransform rect = (RectTransform)screen.transform;
            UiFactory.Stretch(rect);
            rect.gameObject.SetActive(false);

            _screens[id] = screen.gameObject;
            return screen;
        }

        /// <summary>Shows one screen and hides the rest. Pass null for a blank overlay.</summary>
        public void ShowOnly(string id)
        {
            foreach (KeyValuePair<string, GameObject> entry in _screens)
            {
                if (entry.Value != null) entry.Value.SetActive(entry.Key == id);
            }
        }

        /// <summary>An already-created screen, or null if it was never opened.</summary>
        public T GetScreen<T>(string id) where T : Component
        {
            if (!_screens.TryGetValue(id, out GameObject existing) || existing == null) return null;
            return existing.GetComponent<T>();
        }

        public bool AnyScreenOpen()
        {
            foreach (KeyValuePair<string, GameObject> entry in _screens)
            {
                if (entry.Value != null && entry.Value.activeSelf) return true;
            }

            return false;
        }

        private void OnDestroy()
        {
            UiSprites.Release();

            // Only a font taken from an OS face is ours. A builtin belongs to the engine
            // and survives, as do the Resources-loaded textures the material library owns.
            if (_fontIsOurs && _font != null) SafeDestroy(_font);

            _font = null;
            _fontIsOurs = false;
        }

        internal static void SafeDestroy(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}