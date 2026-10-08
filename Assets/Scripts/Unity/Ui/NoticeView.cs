using UnityEngine;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>Top-centre temporary toast — fades after a few seconds.</summary>
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

            // Mock toast is a thin dark pill — not a riveted plate.
            _background = UiFactory.Sprite(transform, UiSprites.Panel(),
                                           new Color(0.06f, 0.05f, 0.04f, 0.82f));
            UiFactory.Anchor(_background.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                             new Vector2(0f, -UiTheme.ScreenMargin), new Vector2(520f, 36f));

            var noticeStyle = UiLayout.Active.otherBody;
            noticeStyle.alignment = TextAnchor.MiddleCenter;
            _label = UiFactory.Text(_background.transform, string.Empty, noticeStyle, UiTheme.TextPrimary);
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
            SetAlpha(1f);
        }

        private void Update()
        {
            if (!_visible) return;
            float age = Time.unscaledTime - _shownAt;
            if (age < HoldSeconds) return;
            float t = (age - HoldSeconds) / FadeSeconds;
            if (t >= 1f)
            {
                _visible = false;
                gameObject.SetActive(false);
                return;
            }
            SetAlpha(1f - t);
        }

        private void SetAlpha(float a)
        {
            if (_background != null)
            {
                Color c = _background.color;
                c.a = a;
                _background.color = c;
            }
            if (_label != null)
            {
                Color c = _label.color;
                c.a = a;
                _label.color = c;
            }
        }
    }
}
