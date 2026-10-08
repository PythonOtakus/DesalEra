using System;
using UnityEngine;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// The themed button. Every clickable thing in the game is one of these.
    ///
    /// It exists as a component rather than as a helper that hands back a Button because a
    /// build slot needs a title, a cost line, an icon and an affordability state, and
    /// carrying those as four parallel references in every screen is how the four drift
    /// apart.
    ///
    /// State is expressed by tinting rather than by swapping sprites, so a slot the player
    /// cannot afford still shows what it would have cost.
    /// </summary>
    public sealed class UiButton : MonoBehaviour
    {
        private Image _background;
        private Image _border;
        private Image _icon;
        private Text _title;
        private Text _subtitle;
        private Text _badge;
        private Button _button;

        private bool _selected;
        private bool _interactive = true;
        private Vector2 _size;

        public string Title { get; private set; }
        public string Subtitle { get; private set; }
        public bool Selected => _selected;
        public bool Interactive => _interactive;

        public event Action Clicked;

        public static UiButton Create(Transform parent, string title, Vector2 size,
                                      string subtitle = null, Sprite icon = null)
        {
            var root = new GameObject("UiButton", typeof(RectTransform));
            root.transform.SetParent(parent, worldPositionStays: false);

            var button = root.AddComponent<UiButton>();
            button.Build(title, subtitle, icon, size, card: false, hotkey: 0);
            return button;
        }

        /// <summary>
        /// Square palette cell: icon dominates, name and cost sit under it, hotkey badge
        /// in the corner. Used by the bottom build dock.
        /// </summary>
        public static UiButton CreateCard(Transform parent, string title, string subtitle,
                                          Sprite icon, int hotkey)
        {
            var root = new GameObject("UiCard", typeof(RectTransform));
            root.transform.SetParent(parent, worldPositionStays: false);

            var button = root.AddComponent<UiButton>();
            float side = UiTheme.BuildSlot;
            button.Build(title, subtitle, icon, new Vector2(side, side + 28f), card: true, hotkey: hotkey);
            return button;
        }

        private void Build(string title, string subtitle, Sprite icon, Vector2 size,
                           bool card, int hotkey)
        {
            _size = size;
            Title = title;
            Subtitle = subtitle;

            var rect = (RectTransform)transform;
            rect.sizeDelta = size;

            _background = UiFactory.Sprite(transform, UiSprites.Panel(), UiTheme.PanelRaised);
            UiFactory.Stretch(_background.rectTransform);

            _border = UiFactory.Sprite(transform, UiSprites.Outline(), UiTheme.Hairline);
            UiFactory.Stretch(_border.rectTransform);
            _border.raycastTarget = false;

            _button = gameObject.AddComponent<Button>();
            _button.targetGraphic = _background;
            _button.transition = Selectable.Transition.None;
            _button.navigation = new Navigation { mode = Navigation.Mode.None };
            _button.onClick.AddListener(() => Clicked?.Invoke());

            var layout = gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = size.x;
            layout.preferredHeight = size.y;
            layout.minWidth = size.x;
            layout.minHeight = size.y;

            if (card) BuildCardContents(title, subtitle, icon, hotkey);
            else BuildRowContents(title, subtitle, icon, size);

            Refresh();
        }

        private void BuildRowContents(string title, string subtitle, Sprite icon, Vector2 size)
        {
            float textLeft = UiTheme.PanelPadding;

            if (icon != null)
            {
                _icon = UiFactory.Sprite(transform, icon, Color.white);
                _icon.preserveAspect = true;
                _icon.raycastTarget = false;

                float side = Mathf.Min(size.y * 0.54f, size.x * 0.34f);
                RectTransform iconRect = _icon.rectTransform;
                iconRect.sizeDelta = new Vector2(side, side);
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(UiTheme.PanelPadding, 0f);

                textLeft += side + UiTheme.RowGap;
            }

            bool twoLines = !string.IsNullOrEmpty(subtitle);

            // Explicit bands — the old lowerHalf flag put both labels in the upper half,
            // so WOOD sat on top of "300 kg/m³ · 500 kN/m²".
            _title = UiFactory.Text(transform, title, UiTheme.FontBody, UiTheme.TextPrimary,
                                   TextAnchor.MiddleLeft, textLeft, false);
            _title.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (twoLines)
            {
                RectTransform tr = _title.rectTransform;
                tr.anchorMin = new Vector2(0f, 0.48f);
                tr.anchorMax = Vector2.one;
                tr.offsetMin = new Vector2(textLeft, 2f);
                tr.offsetMax = new Vector2(-UiTheme.PanelPadding, -4f);

                _subtitle = UiFactory.Text(transform, subtitle, UiTheme.FontBody - 2, UiTheme.TextMuted,
                                           TextAnchor.MiddleLeft, textLeft, true);
                _subtitle.horizontalOverflow = HorizontalWrapMode.Overflow;
                RectTransform sr = _subtitle.rectTransform;
                sr.anchorMin = Vector2.zero;
                sr.anchorMax = new Vector2(1f, 0.48f);
                sr.offsetMin = new Vector2(textLeft, 4f);
                sr.offsetMax = new Vector2(-UiTheme.PanelPadding, -2f);
            }
        }

        private void BuildCardContents(string title, string subtitle, Sprite icon, int hotkey)
        {
            if (icon != null)
            {
                _icon = UiFactory.Sprite(transform, icon, Color.white);
                _icon.preserveAspect = true;
                _icon.raycastTarget = false;

                RectTransform iconRect = _icon.rectTransform;
                iconRect.anchorMin = new Vector2(0.5f, 1f);
                iconRect.anchorMax = new Vector2(0.5f, 1f);
                iconRect.pivot = new Vector2(0.5f, 1f);
                iconRect.sizeDelta = new Vector2(44f, 44f);
                iconRect.anchoredPosition = new Vector2(0f, -14f);
            }

            _title = UiFactory.Text(transform, title, UiTheme.FontBody, UiTheme.TextPrimary,
                                    TextAnchor.MiddleCenter);
            _title.rectTransform.anchorMin = new Vector2(0f, 0f);
            _title.rectTransform.anchorMax = new Vector2(1f, 0f);
            _title.rectTransform.pivot = new Vector2(0.5f, 0f);
            _title.rectTransform.sizeDelta = new Vector2(-8f, 22f);
            _title.rectTransform.anchoredPosition = new Vector2(0f, 24f);
            _title.horizontalOverflow = HorizontalWrapMode.Overflow;

            if (!string.IsNullOrEmpty(subtitle))
            {
                _subtitle = UiFactory.Text(transform, subtitle, UiTheme.FontSmall, UiTheme.TextMuted,
                                           TextAnchor.MiddleCenter);
                _subtitle.rectTransform.anchorMin = new Vector2(0f, 0f);
                _subtitle.rectTransform.anchorMax = new Vector2(1f, 0f);
                _subtitle.rectTransform.pivot = new Vector2(0.5f, 0f);
                _subtitle.rectTransform.sizeDelta = new Vector2(-8f, 18f);
                _subtitle.rectTransform.anchoredPosition = new Vector2(0f, 5f);
                _subtitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            if (hotkey > 0)
            {
                RectTransform badge = UiFactory.Container(transform, "Badge");
                badge.anchorMin = new Vector2(0f, 1f);
                badge.anchorMax = new Vector2(0f, 1f);
                badge.pivot = new Vector2(0f, 1f);
                badge.sizeDelta = new Vector2(22f, 18f);
                badge.anchoredPosition = new Vector2(6f, -6f);

                Image badgeBg = UiFactory.Sprite(badge, UiSprites.Panel(), UiTheme.Well);
                UiFactory.Stretch(badgeBg.rectTransform);
                badgeBg.raycastTarget = false;

                _badge = UiFactory.Text(badge, hotkey.ToString(), 12, UiTheme.TextMuted,
                                        TextAnchor.MiddleCenter);
                UiFactory.Stretch(_badge.rectTransform);
                _badge.rectTransform.offsetMin = Vector2.zero;
                _badge.rectTransform.offsetMax = Vector2.zero;
            }
        }

        public void SetTitle(string title)
        {
            Title = title;
            if (_title != null) _title.text = title;
        }

        public void SetSubtitle(string subtitle)
        {
            Subtitle = subtitle;
            if (_subtitle != null) _subtitle.text = subtitle;
        }

        /// <summary>Marks the slot as the current choice.</summary>
        public void SetSelected(bool selected)
        {
            _selected = selected;
            Refresh();
        }

        /// <summary>
        /// Whether the action is available. Kept apart from selection on purpose: a slot
        /// can be both selected and unaffordable, and the player still has to be able to
        /// read what it would have cost.
        /// </summary>
        public void SetInteractive(bool interactive)
        {
            _interactive = interactive;
            if (_button != null) _button.interactable = interactive;
            Refresh();
        }

        /// <summary>Colours the cost line by whether it can actually be paid.</summary>
        public void SetSubtitleTone(Color tone)
        {
            if (_subtitle != null) _subtitle.color = tone;
        }

        private void Refresh()
        {
            if (_background == null) return;

            if (!_interactive)
            {
                _background.color = UiTheme.Well;
                if (_title != null) _title.color = UiTheme.TextDisabled;
                if (_subtitle != null) _subtitle.color = UiTheme.TextDisabled;
                if (_icon != null)
                {
                    var c = _icon.color;
                    c.a = 0.35f;
                    _icon.color = c;
                }
                if (_border != null) _border.color = new Color(0f, 0f, 0f, 0f);
                return;
            }

            _background.color = _selected ? UiTheme.AccentDim : UiTheme.PanelRaised;
            if (_title != null) _title.color = UiTheme.TextPrimary;
            if (_subtitle != null) _subtitle.color = UiTheme.TextMuted;
            if (_icon != null)
            {
                var c = _icon.color;
                c.a = 1f;
                _icon.color = c;
            }
            if (_border != null) _border.color = _selected ? UiTheme.Accent : UiTheme.Hairline;
        }

        public Vector2 Size => _size;
    }
}
