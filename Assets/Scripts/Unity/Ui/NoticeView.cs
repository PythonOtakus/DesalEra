using UnityEngine;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// The one-line feedback strip, and the only part of the old text HUD kept as text.
    ///
    /// It fades after a couple of seconds rather than sitting on screen forever. A message
    /// that never disappears is read once and then becomes wallpaper, and the player stops
    /// noticing the refusals that matter.
    /// </summary>
    public sealed class NoticeView : MonoBehaviour
    {
        private const float HoldSeconds = 2.6f;
        private const float FadeSeconds = 0.8f;

        private Image _background;
        private Text _label;
        private float _shownAt;
        private bool _visible;

        public static NoticeView Create(Transform parent)
        {
            var go = new GameObject("Notice", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            var view = go.AddComponent<NoticeView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            UiFactory.FillParent(transform);

            // Sit just above the build dock so toasts never compete with the placement ghost.
            _background = UiFactory.Sprite(transform, UiSprites.Panel(), UiTheme.PanelRaised);
            UiFactory.Anchor(_background.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                             new Vector2(0f, UiTheme.BuildDockHeight + 40f), new Vector2(640f, 36f));

            Image border = UiFactory.Sprite(_background.transform, UiSprites.Outline(), UiTheme.Hairline);
            UiFactory.Stretch(border.rectTransform);
            border.raycastTarget = false;

            _label = UiFactory.Text(_background.transform, string.Empty, UiTheme.FontBody,
                                    UiTheme.TextPrimary, TextAnchor.MiddleCenter);
            _label.rectTransform.offsetMin = new Vector2(12f, 0f);
            _label.rectTransform.offsetMax = new Vector2(-12f, 0f);

            gameObject.SetActive(false);
        }

        public void Show(string message, Color tone)
        {
            if (string.IsNullOrEmpty(message)) return;

            _label.text = message;
            _label.color = tone;
            _shownAt = Time.unscaledTime;
            _visible = true;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!_visible) return;

            float age = Time.unscaledTime - _shownAt;
            if (age < HoldSeconds) return;

            float fade = Mathf.Clamp01((age - HoldSeconds) / FadeSeconds);
            var colour = _background.color;
            colour.a = Mathf.Lerp(0.97f, 0f, fade);
            _background.color = colour;

            if (fade >= 1f)
            {
                _visible = false;
                gameObject.SetActive(false);
                _background.color = UiTheme.PanelRaised;
            }
        }

        /// <summary>Clears immediately, for a mode change that invalidates the message.</summary>
        public void Clear()
        {
            _visible = false;
            gameObject.SetActive(false);
        }
    }
}