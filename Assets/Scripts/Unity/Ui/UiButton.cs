using System;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private bool _hovered;
        private bool _subtitleVisible = true;
        private bool _plateVisible = true;
        private bool _scaleOnSelect;
        private Vector2 _size;

        public string Title { get; private set; }
        public string Subtitle { get; private set; }
        public bool Selected => _selected;
        public bool Interactive => _interactive;

        public event Action Clicked;

        public static UiButton Create(Transform parent, string title, Vector2 size,
                                      string subtitle = null, Sprite icon = null)
        {
            UiLayoutSettings L = UiLayout.Active;
            return Create(parent, title, size, subtitle, icon, L.otherBody, L.otherSmall);
        }

        public static UiButton Create(Transform parent, string title, Vector2 size,
                                      string subtitle, Sprite icon,
                                      in UiTextStyle titleStyle, in UiTextStyle subtitleStyle)
        {
            var root = new GameObject("UiButton", typeof(RectTransform));
            root.transform.SetParent(parent, worldPositionStays: false);

            var button = root.AddComponent<UiButton>();
            // 行列表不缩放选中项，避免详情面板里材料行宽窄不一。
            button.Build(title, subtitle, icon, size, card: false, hotkey: 0,
                         titleStyle, subtitleStyle, scaleOnSelect: false);
            return button;
        }

        /// <summary>
        /// Square palette cell: icon dominates, name and cost sit under it, hotkey badge
        /// in the corner. Used by the bottom build dock.
        /// </summary>
        public static UiButton CreateCard(Transform parent, string title, string subtitle,
                                          Sprite icon, int hotkey)
        {
            UiLayoutSettings L = UiLayout.Active;
            var root = new GameObject("UiCard", typeof(RectTransform));
            root.transform.SetParent(parent, worldPositionStays: false);

            var button = root.AddComponent<UiButton>();
            float side = UiTheme.BuildSlot;
            button.Build(title, subtitle, icon, new Vector2(side, side + 16f), card: true, hotkey: hotkey,
                         L.buildCardTitle, L.otherSmall, scaleOnSelect: true);
            return button;
        }

        private void Build(string title, string subtitle, Sprite icon, Vector2 size,
                           bool card, int hotkey, in UiTextStyle titleStyle, in UiTextStyle subtitleStyle,
                           bool scaleOnSelect)
        {
            _size = size;
            _scaleOnSelect = scaleOnSelect;
            Title = title;
            Subtitle = subtitle;

            var rect = (RectTransform)transform;
            rect.sizeDelta = size;

            // Inner cards are timber only — riveted metal belongs on the outer panel.
            _background = UiFactory.Sprite(transform, UiFrame.WoodTile(), Color.white, Image.Type.Tiled);
            UiFactory.Stretch(_background.rectTransform);

            _border = UiFactory.Sprite(transform, UiSprites.Outline(UiTheme.CornerRadius, 4), UiTheme.Accent);
            UiFactory.Stretch(_border.rectTransform);
            _border.raycastTarget = false;

            _button = gameObject.AddComponent<Button>();
            _button.targetGraphic = _background;
            _button.transition = Selectable.Transition.None;
            _button.navigation = new Navigation { mode = Navigation.Mode.None };
            _button.onClick.AddListener(() => Clicked?.Invoke());

            var trigger = gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => { _hovered = true; Refresh(); });
            AddTrigger(trigger, EventTriggerType.PointerExit, () => { _hovered = false; Refresh(); });

            var layout = gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = size.x;
            layout.preferredHeight = size.y;
            layout.minWidth = size.x;
            layout.minHeight = size.y;

            if (card) BuildCardContents(title, subtitle, icon, hotkey, titleStyle, subtitleStyle);
            else BuildRowContents(title, subtitle, icon, size, titleStyle, subtitleStyle);

            Refresh();
        }

        private void BuildRowContents(string title, string subtitle, Sprite icon, Vector2 size,
                                      in UiTextStyle titleStyle, in UiTextStyle subtitleStyle)
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

            _title = UiFactory.Text(transform, title, titleStyle, UiTheme.TextPrimary, textLeft);
            _title.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (twoLines)
            {
                RectTransform tr = _title.rectTransform;
                tr.anchorMin = new Vector2(0f, 0.48f);
                tr.anchorMax = Vector2.one;
                tr.offsetMin = new Vector2(textLeft, 2f);
                tr.offsetMax = new Vector2(-UiTheme.PanelPadding, -4f);

                _subtitle = UiFactory.Text(transform, subtitle, subtitleStyle, UiTheme.TextMuted, textLeft);
                _subtitle.horizontalOverflow = HorizontalWrapMode.Overflow;
                RectTransform sr = _subtitle.rectTransform;
                sr.anchorMin = Vector2.zero;
                sr.anchorMax = new Vector2(1f, 0.48f);
                sr.offsetMin = new Vector2(textLeft, 4f);
                sr.offsetMax = new Vector2(-UiTheme.PanelPadding, -2f);
            }
        }

        private void BuildCardContents(string title, string subtitle, Sprite icon, int hotkey,
                                       in UiTextStyle titleStyle, in UiTextStyle subtitleStyle)
        {
            if (icon != null)
            {
                _icon = UiFactory.Sprite(transform, icon, Color.white);
                _icon.preserveAspect = true;
                _icon.raycastTarget = false;
                RectTransform iconRect = _icon.rectTransform;
                iconRect.anchorMin = new Vector2(0.14f, 0.42f);
                iconRect.anchorMax = new Vector2(0.86f, 0.94f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
            }

            _title = UiFactory.Text(transform, title, titleStyle, UiTheme.TextPrimary);
            RectTransform tr = _title.rectTransform;
            tr.anchorMin = new Vector2(0.04f, 0.04f);
            tr.anchorMax = new Vector2(0.96f, 0.40f);
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            _title.horizontalOverflow = HorizontalWrapMode.Overflow;
            _title.verticalOverflow = VerticalWrapMode.Overflow;
            _title.resizeTextForBestFit = true;
            _title.resizeTextMinSize = 14;
            titleStyle.ApplyTo(_title);

            if (!string.IsNullOrEmpty(subtitle))
            {
                _subtitle = UiFactory.Text(transform, subtitle, subtitleStyle, UiTheme.TextMuted);
                RectTransform sr = _subtitle.rectTransform;
                sr.anchorMin = new Vector2(0.04f, 0.02f);
                sr.anchorMax = new Vector2(0.96f, 0.18f);
                sr.offsetMin = Vector2.zero;
                sr.offsetMax = Vector2.zero;
                _subtitle.horizontalOverflow = HorizontalWrapMode.Overflow;
                _subtitle.resizeTextForBestFit = true;
                _subtitle.resizeTextMinSize = 12;
                subtitleStyle.ApplyTo(_subtitle);
            }

            if (hotkey > 0)
            {
                UiTextStyle badgeStyle = UiLayout.Active.buildCardBadge;
                RectTransform badge = UiFactory.Container(transform, "Badge");
                badge.anchorMin = new Vector2(0f, 1f);
                badge.anchorMax = new Vector2(0f, 1f);
                badge.pivot = new Vector2(0f, 1f);
                badge.sizeDelta = new Vector2(24f, 20f);
                badge.anchoredPosition = new Vector2(6f, -6f);

                Image badgeBg = UiFactory.Sprite(badge, UiSprites.Panel(), UiTheme.Well);
                UiFactory.Stretch(badgeBg.rectTransform);
                badgeBg.raycastTarget = false;

                _badge = UiFactory.Text(badge, hotkey.ToString(), badgeStyle, UiTheme.TextPrimary);
                UiFactory.Stretch(_badge.rectTransform);
                _badge.rectTransform.offsetMin = Vector2.zero;
                _badge.rectTransform.offsetMax = Vector2.zero;
            }
        }

        public void SetIconColor(Color color)
        {
            if (_icon != null) _icon.color = color;
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

        /// <summary>
        /// Cost / secondary line. Hidden by default on the build dock so the palette stays
        /// icon-first; shown when selected or when the player cannot afford it.
        /// </summary>
        public void SetSubtitleVisible(bool visible)
        {
            _subtitleVisible = visible;
            if (_subtitle != null) _subtitle.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Hide the timber plate when the parent HUD skin already paints the slot wells.
        /// </summary>
        public void SetPlateVisible(bool visible)
        {
            _plateVisible = visible;
            if (_background == null) return;
            if (!visible)
            {
                _background.sprite = UiSprites.Solid();
                _background.type = Image.Type.Simple;
                _background.color = new Color(0f, 0f, 0f, 0f);
            }
            else
            {
                _background.sprite = UiFrame.WoodTile();
                _background.type = Image.Type.Tiled;
            }
            Refresh();
        }

        private void Refresh()
        {
            if (_background == null) return;

            if (!_interactive)
            {
                _background.color = _plateVisible
                    ? new Color(0.45f, 0.42f, 0.40f, 0.85f)
                    : new Color(0f, 0f, 0f, 0f);
                // Keep titles readable on dark timber (disabled grey disappears into the grain).
                if (_title != null) _title.color = new Color(UiTheme.Bad.r, UiTheme.Bad.g, UiTheme.Bad.b, 0.95f);
                if (_subtitle != null)
                {
                    _subtitle.color = UiTheme.Bad;
                    _subtitle.gameObject.SetActive(true);
                }
                if (_icon != null)
                {
                    var c = _icon.color;
                    c.a = 0.4f;
                    _icon.color = c;
                }
                if (_border != null) _border.color = new Color(UiTheme.Bad.r, UiTheme.Bad.g, UiTheme.Bad.b, 0.75f);
                transform.localScale = Vector3.one;
                return;
            }

            if (_plateVisible)
            {
                Color fill = Color.white;
                if (_selected) fill = new Color(1.08f, 1.02f, 0.88f, 1f);
                else if (_hovered) fill = new Color(1.05f, 1.03f, 0.96f, 1f);
                _background.color = fill;
            }
            else
            {
                _background.color = new Color(0f, 0f, 0f, 0f);
            }
            if (_title != null) _title.color = UiTheme.TextPrimary;
            if (_subtitle != null)
            {
                if (_subtitle.gameObject.activeSelf != _subtitleVisible)
                    _subtitle.gameObject.SetActive(_subtitleVisible);
            }
            if (_icon != null)
            {
                var c = _icon.color;
                c.a = 1f;
                _icon.color = c;
            }
            if (_border != null)
                _border.color = _selected ? UiTheme.Accent
                              : (_hovered ? new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.45f)
                                          : new Color(0f, 0f, 0f, 0f));

            transform.localScale = (_scaleOnSelect && _selected)
                ? new Vector3(1.05f, 1.05f, 1f)
                : Vector3.one;
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        public Vector2 Size => _size;
    }
}
