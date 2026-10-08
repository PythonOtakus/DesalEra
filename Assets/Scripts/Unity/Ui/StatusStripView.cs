using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// Situational states, borrowed from Valheim's status strip: sheltered, resting, storm.
    ///
    /// These are kept out of the vital bars on purpose. A vital is a number that drains; a
    /// status is a fact about where the player is standing. Mixing them is how a player ends
    /// up unable to say whether the blue bar is low or merely sheltered.
    ///
    /// Chips appear and disappear rather than persisting greyed out, because the whole point
    /// of the sheltered chip is the moment it lights up. Walking under your own roof for the
    /// first time has to be legible from across the deck.
    /// </summary>
    public sealed class StatusStripView : MonoBehaviour
    {
        private sealed class Chip
        {
            public RectTransform Root;
            public Image Background;
            public Image Dot;
            public Text Label;
            public Color Tone;
        }

        private readonly Dictionary<string, Chip> _chips = new Dictionary<string, Chip>();
        private RectTransform _row;

        public static StatusStripView Create(Transform parent)
        {
            var go = new GameObject("StatusStrip", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            var view = go.AddComponent<StatusStripView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            UiFactory.FillParent(transform);

            _row = UiFactory.Container(transform, "Row");
            _row.anchorMin = new Vector2(1f, 1f);
            _row.anchorMax = new Vector2(1f, 1f);
            _row.pivot = new Vector2(1f, 1f);
            _row.sizeDelta = new Vector2(560f, 34f);
            _row.anchoredPosition = new Vector2(-UiTheme.PanelPadding, -UiTheme.PanelPadding);

            // Right-aligned, so the strip grows leftwards out of the corner instead of
            // shifting every chip across the screen when one appears.
            UiFactory.Horizontal(_row, 8f, new RectOffset(0, 0, 0, 0), grow: false);
            _row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperRight;
        }

        /// <summary>Declares a chip up front so the strip's order does not depend on events.</summary>
        public void Declare(string key, string label, Color tone)
        {
            if (_chips.ContainsKey(key)) return;

            var chip = new Chip { Tone = tone };

            RectTransform root = UiFactory.Container(_row, "Chip_" + key);
            var element = root.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 210f;
            element.preferredHeight = 32f;

            chip.Background = UiFactory.Sprite(root, UiSprites.Panel(), UiTheme.PanelRaised);
            UiFactory.Stretch(chip.Background.rectTransform);

            Image dot = UiFactory.Sprite(chip.Background.transform, UiSprites.Circle(14), tone, Image.Type.Simple);
            dot.raycastTarget = false;
            RectTransform dr = dot.rectTransform;
            dr.sizeDelta = new Vector2(14f, 14f);
            dr.anchorMin = new Vector2(0f, 0.5f);
            dr.anchorMax = new Vector2(0f, 0.5f);
            dr.pivot = new Vector2(0f, 0.5f);
            dr.anchoredPosition = new Vector2(10f, 0f);

            chip.Dot = dot;
            chip.Label = UiFactory.Text(chip.Background.transform, label, UiTheme.FontBody, UiTheme.TextPrimary, TextAnchor.MiddleLeft);
            // Text() anchors to the upper half by default; in a 32 px chip that truncates
            // the line to nothing.
            UiFactory.Stretch(chip.Label.rectTransform);
            chip.Label.rectTransform.offsetMin = new Vector2(32f, 0f);
            chip.Label.rectTransform.offsetMax = new Vector2(-10f, 0f);
            chip.Label.horizontalOverflow = HorizontalWrapMode.Overflow;

            chip.Root = root;
            chip.Root.gameObject.SetActive(false);

            _chips[key] = chip;
        }

        /// <summary>Shows or hides a declared chip. Unknown keys are ignored on purpose.</summary>
        public void Set(string key, bool active, string label = null)
        {
            if (!_chips.TryGetValue(key, out Chip chip) || chip == null) return;

            chip.Root.gameObject.SetActive(active);
            if (active && label != null) chip.Label.text = label;
        }

        /// <summary>
        /// Re-tints a live chip, for a state that changes severity rather than truth:
        /// calm storm versus one about to peak.
        /// </summary>
        public void Tone(string key, Color tone)
        {
            if (!_chips.TryGetValue(key, out Chip chip) || chip == null) return;
            chip.Dot.color = tone;
        }

        /// <summary>Repacks the row after a chip changes size, since the layout is manual.</summary>
        public void Reflow()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        }
    }
}