using DesalEra.Game;
using UnityEngine;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// Top-left nautical glance: deck footprint, facing tick, wave height.
    /// Drawn into a small texture each refresh — cheap at 96².
    /// </summary>
    public sealed class MiniMapView : MonoBehaviour
    {
        private const int MapPx = 96;

        private RawImage _map;
        private Text _wave;
        private Texture2D _tex;
        private Color32[] _pixels;
        private GameBootstrap _world;
        private PlayerController _player;

        public static MiniMapView Create(Transform parent)
        {
            var go = new GameObject("MiniMap", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var view = go.AddComponent<MiniMapView>();
            view.Build();
            return view;
        }

        public void Bind(GameBootstrap world, PlayerController player)
        {
            _world = world;
            _player = player;
        }

        private void Build()
        {
            UiFactory.FillParent(transform);

            Image panel = UiSkins.Panel(transform, UiSkins.Kind.Minimap, out _);
            float side = UiSkins.MinimapSide;
            UiFactory.Anchor(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                             new Vector2(UiTheme.ScreenMargin, -UiTheme.ScreenMargin),
                             new Vector2(side, side));

            _tex = new Texture2D(MapPx, MapPx, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            _pixels = new Color32[MapPx * MapPx];

            var mapGo = new GameObject("Map", typeof(RectTransform));
            mapGo.transform.SetParent(panel.transform, worldPositionStays: false);
            _map = mapGo.AddComponent<RawImage>();
            _map.texture = _tex;
            _map.raycastTarget = false;
            RectTransform mr = _map.rectTransform;
            Vector4 pad = UiSkins.ContentPad(UiSkins.Kind.Minimap);
            // Leave a band at the bottom for wave text inside the glass.
            mr.anchorMin = new Vector2(pad.x, pad.y + 0.10f);
            mr.anchorMax = new Vector2(1f - pad.z, 1f - pad.w - 0.02f);
            mr.offsetMin = Vector2.zero;
            mr.offsetMax = Vector2.zero;

            UiLayoutSettings L = UiLayout.Active;
            Text north = UiFactory.Text(panel.transform, "N", L.minimapNorth, UiTheme.Accent);
            north.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            north.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            north.rectTransform.pivot = new Vector2(0.5f, 1f);
            north.rectTransform.sizeDelta = new Vector2(28f, 22f);
            north.rectTransform.anchoredPosition = new Vector2(0f, -side * pad.w * 0.55f);

            _wave = UiFactory.Text(panel.transform, "浪高 —", L.minimapWave, UiTheme.TextPrimary);
            _wave.rectTransform.anchorMin = new Vector2(pad.x, pad.y * 0.35f);
            _wave.rectTransform.anchorMax = new Vector2(1f - pad.z, pad.y + 0.10f);
            _wave.rectTransform.offsetMin = Vector2.zero;
            _wave.rectTransform.offsetMax = Vector2.zero;
        }

        public void Refresh()
        {
            if (_world == null || _tex == null) return;

            Color32 sea = new Color32(28, 42, 52, 220);
            Color32 deck = new Color32(92, 68, 42, 255);
            Color32 arrow = new Color32(240, 236, 220, 255);
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = sea;

            float half = RaftState.CellSize;
            // Map local XZ [-half-1, half+1] into texture.
            float span = half * 2f + 2f;
            void PlotLocal(float lx, float lz, Color32 c, int r = 0)
            {
                int px = Mathf.RoundToInt((lx + span * 0.5f) / span * (MapPx - 1));
                int py = Mathf.RoundToInt((lz + span * 0.5f) / span * (MapPx - 1));
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = px + dx, y = py + dy;
                    if (x < 0 || y < 0 || x >= MapPx || y >= MapPx) continue;
                    _pixels[y * MapPx + x] = c;
                }
            }

            // Deck footprint (opening hull is roughly ±half).
            for (int y = 0; y < MapPx; y++)
            for (int x = 0; x < MapPx; x++)
            {
                float lx = x / (float)(MapPx - 1) * span - span * 0.5f;
                float lz = y / (float)(MapPx - 1) * span - span * 0.5f;
                if (Mathf.Abs(lx) <= half && Mathf.Abs(lz) <= half)
                    _pixels[y * MapPx + x] = deck;
            }

            if (_player != null)
            {
                Vector3 local = _world.RaftMotion != null
                    ? _world.RaftMotion.LocalOnPlane(_player.transform.position, PlayerController.DeckSurfaceLocalY)
                    : _player.transform.position;
                Vector3 face = _player.Avatar != null ? _player.Avatar.FacingDirection : Vector3.forward;
                PlotLocal(local.x, local.z, arrow, 1);
                // Facing tick.
                PlotLocal(local.x + face.x * 0.55f, local.z + face.z * 0.55f, arrow, 0);
            }

            _tex.SetPixels32(_pixels);
            _tex.Apply(false);

            float amp = SeaWave.Amplitude;
            _wave.text = $"浪高 {amp:0.0}m";
        }

        private void OnDestroy()
        {
            if (_tex != null)
            {
                if (Application.isPlaying) Destroy(_tex);
                else DestroyImmediate(_tex);
            }
        }
    }
}
