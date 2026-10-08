using UnityEngine;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// Centre-screen build aim: green when the ghost is legal, red with a short reason
    /// when it is not. Keeps the player's eyes on the placement point instead of the dock.
    /// </summary>
    public sealed class BuildReticleView : MonoBehaviour
    {
        private Image _cross;
        private Text _caption;
        private CanvasGroup _group;

        public static BuildReticleView Create(Transform parent)
        {
            var go = new GameObject("BuildReticle", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var view = go.AddComponent<BuildReticleView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            UiFactory.FillParent(transform);
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _cross = UiFactory.Sprite(transform, UiSprites.Crosshair(36), UiTheme.Good, Image.Type.Simple);
            _cross.raycastTarget = false;
            UiFactory.Anchor(_cross.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(0f, 18f), new Vector2(36f, 36f));

            Image captionBg = UiFactory.Sprite(transform, UiSprites.Panel(),
                                               new Color(0.08f, 0.06f, 0.04f, 0.78f));
            captionBg.raycastTarget = false;
            UiFactory.Anchor(captionBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(0f, -8f), new Vector2(200f, 26f));

            _caption = UiFactory.Text(captionBg.transform, string.Empty, UiTheme.FontSmall, UiTheme.TextPrimary,
                                      TextAnchor.MiddleCenter);
            UiFactory.Stretch(_caption.rectTransform);
            _caption.rectTransform.offsetMin = new Vector2(6f, 0f);
            _caption.rectTransform.offsetMax = new Vector2(-6f, 0f);
            _caption.horizontalOverflow = HorizontalWrapMode.Overflow;

            Hide();
        }

        public void Show(bool allowed, string reason)
        {
            _group.alpha = 1f;
            _cross.color = allowed ? UiTheme.Good : UiTheme.Bad;
            _caption.color = allowed ? UiTheme.Good : UiTheme.Bad;
            _caption.text = allowed ? "可放置" : (string.IsNullOrEmpty(reason) ? "不可放置" : reason);
        }

        public void Hide()
        {
            _group.alpha = 0f;
        }
    }
}
