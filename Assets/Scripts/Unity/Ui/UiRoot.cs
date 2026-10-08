using System.Collections.Generic;
using DesalEra.Unity.Inspect;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// Canvas / 字库 / 屏幕注册 / 常驻 HUD，以及布局配置（Play 工作副本与保存）。
    /// 界面不进场景文件；Play 后选 Hierarchy 的 <c>GameEntry/Ui</c> 微调布局。
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

        [LabelText("布局配置资源", "Resources/UiLayoutSettings")]
        [SerializeField] private UiLayoutSettings settings;

        private UiLayoutSettings _working;

        public VitalRowView Vitals { get; private set; }
        public StatusStripView Status { get; private set; }
        public BuildBarView BuildBar { get; private set; }
        public ResourceStripView ResourceStrip { get; private set; }
        public MiniMapView MiniMap { get; private set; }
        public NoticeView Notice { get; private set; }
        public BuildReticleView Reticle { get; private set; }

        /// <summary>HUD 当前布局（Play = 工作副本）。</summary>
        public UiLayoutSettings Working => Application.isPlaying && _working != null ? _working : settings;

        /// <summary>Fired after always-on HUD panels are rebuilt (layout Apply).</summary>
        public event System.Action HudRebuilt;

        /// <summary>
        /// RebuildHud 前处于打开状态的 Overlay id；订阅方在 HudRebuilt 里据此重新打开，
        /// 避免改布局参数时详情 / 背包被关掉。
        /// </summary>
        public string OverlayToReopen { get; private set; }

        /// <summary>Anything that must draw over the HUD, e.g. a modal panel.</summary>
        public RectTransform Overlay => _overlay;

        public Canvas Canvas => _canvas;

        private bool CanSaveLayout =>
            Application.isPlaying && settings != null && _working != null && _working != settings;

        [OnInspectorInit]
        private void InspectorInit() => EnsureSettings();

        [InfoBox("Play 中可改下方参数。「保存设置」写入内存资源，退出 Play 时落盘（避免保存时甲板贴图被资源刷新打黑）。", InfoBoxType.None)]
        [ShowInInspector]
        [InlineEditor]
        [LabelText("布局参数")]
        [OnValueChanged(nameof(ApplyLayoutIfPlaying))]
        private UiLayoutSettings LayoutForInspect => Working;

        [Button("保存设置", SaveAssets = true)]
        [EnableIf(nameof(CanSaveLayout))]
        private void SaveLayoutDuringPlay()
        {
            if (!CanSaveLayout) return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(_working, prettyPrint: true), settings);
            Debug.Log("[UiLayout] 已保存设置（退出 Play 时写入磁盘）→ " + settings.name);
        }

        private void ApplyLayoutIfPlaying()
        {
            if (!Application.isPlaying) return;
            EnsureSettings();
            UiLayout.Bind(Working);
            RebuildHud();
        }

        public void Initialise()
        {
            EnsureSettings();
            if (Application.isPlaying)
            {
                if (_working != null) Destroy(_working);
                _working = Instantiate(settings);
                _working.name = settings.name + " (Play)";
                _working.hideFlags = HideFlags.DontSave;
            }
            UiLayout.Bind(Working);

            BuildCanvas();
            BuildLayers();
            BuildHud();
        }

        private void EnsureSettings()
        {
            if (settings != null) return;
            settings = Resources.Load<UiLayoutSettings>(UiLayout.ResourcesPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<UiLayoutSettings>();
                settings.name = "UiLayoutSettings (runtime)";
                Debug.LogWarning(
                    "[UiLayout] 未找到 Resources/UiLayoutSettings，已使用内存默认值。" +
                    "请通过菜单 DesalEra/UI/创建布局配置资源 创建。");
            }
        }

        /// <summary>
        /// 按当前布局重建常驻 HUD，并重建 Overlay（详情 / 背包）以应用新内边距与字号。
        /// 若重建前有 Overlay 打开，通过 <see cref="OverlayToReopen"/> 让订阅方重新打开。
        /// </summary>
        public void RebuildHud()
        {
            OverlayToReopen = FindOpenOverlayId();
            ClearOverlayScreens();

            if (_hud == null) return;
            for (int i = _hud.childCount - 1; i >= 0; i--)
            {
                Transform child = _hud.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            MiniMap = null;
            Vitals = null;
            Status = null;
            BuildBar = null;
            ResourceStrip = null;
            Notice = null;
            Reticle = null;

            BuildHud();
            HudRebuilt?.Invoke();
            OverlayToReopen = null;
        }

        private string FindOpenOverlayId()
        {
            foreach (KeyValuePair<string, GameObject> entry in _screens)
            {
                if (entry.Value != null && entry.Value.activeSelf)
                    return entry.Key;
            }
            return null;
        }

        private void ClearOverlayScreens()
        {
            foreach (KeyValuePair<string, GameObject> entry in _screens)
            {
                if (entry.Value == null) continue;
                if (Application.isPlaying) Destroy(entry.Value);
                else DestroyImmediate(entry.Value);
            }
            _screens.Clear();
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
            MiniMap = MiniMapView.Create(_hud);
            Vitals = VitalRowView.Create(_hud);
            Status = StatusStripView.Create(_hud);
            BuildBar = BuildBarView.Create(_hud);
            ResourceStrip = ResourceStripView.Create(_hud);
            Notice = NoticeView.Create(_hud);
            Reticle = BuildReticleView.Create(_hud);
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
            if (_working != null)
            {
                if (Application.isPlaying) Destroy(_working);
                else DestroyImmediate(_working);
                _working = null;
            }
            UiLayout.ClearBind();

            UiSprites.Release();
            UiFrame.Release();
            UiSkins.Release();

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