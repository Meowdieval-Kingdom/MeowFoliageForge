using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Meowdieval.FoliageForge
{
    /// <summary>Severity of a <see cref="FoliageUi.StatChip"/>.</summary>
    internal enum StatKind { Neutral, Ok, Warn }

    /// <summary>Shared UI Toolkit helpers + stylesheet loader for Foliage Forge editor UI.</summary>
    internal static class FoliageUi
    {
        private const string SheetPath = "Packages/com.meowdieval.foliageforge/Editor/Window/Styles/FoliageForge.uss";
        private static StyleSheet _sheet;

        /// <summary>The shared stylesheet; null if it cannot be loaded.</summary>
        public static StyleSheet Sheet
        {
            get
            {
                if (_sheet == null) _sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SheetPath);
                return _sheet;
            }
        }

        /// <summary>Adds the stylesheet and the .ff-root class (USS variables) to a root element.</summary>
        public static void Apply(VisualElement root)
        {
            if (root == null) return;
            root.AddToClassList("ff-root");
            StyleSheet sheet = Sheet;
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
        }

        /// <summary>
        /// Chip-style button. When selfToggle is true (default, BigToggle behaviour) a click flips the state and
        /// calls onChange(newState). When false (ModeButton behaviour) a click calls onChange(true) and the caller
        /// repaints via <see cref="SetChipOn"/>. onChange may be null.
        /// </summary>
        public static Button ChipButton(string text, bool on, Action<bool> onChange, string tooltip = null, bool selfToggle = true)
        {
            var button = new Button { text = text, tooltip = tooltip };
            button.AddToClassList("ff-chip");
            SetChipOn(button, on);
            button.clicked += () =>
            {
                if (selfToggle)
                {
                    bool next = !button.ClassListContains("ff-chip--on");
                    SetChipOn(button, next);
                    onChange?.Invoke(next);
                }
                else onChange?.Invoke(true);
            };
            return button;
        }

        /// <summary>Sets the on/off look of a chip button.</summary>
        public static void SetChipOn(VisualElement chip, bool on) => chip.EnableInClassList("ff-chip--on", on);

        public static Label GroupLabel(string text, string tooltip = null)
        {
            var label = new Label(text) { tooltip = tooltip };
            label.AddToClassList("ff-group-label");
            return label;
        }

        /// <summary>A flex-wrap row (.ff-row); children are spaced with margins.</summary>
        public static VisualElement WrapRow()
        {
            var row = new VisualElement();
            row.AddToClassList("ff-row");
            return row;
        }

        /// <summary>Label column (min 110px, ~40%) + growing control (.ff-field).</summary>
        public static VisualElement Field(string label, VisualElement control, string tooltip = null)
        {
            var field = new VisualElement { tooltip = tooltip };
            field.AddToClassList("ff-field");
            var l = new Label(label);
            l.AddToClassList("ff-field__label");
            field.Add(l);
            if (control != null)
            {
                control.AddToClassList("ff-field__control");
                field.Add(control);
            }
            return field;
        }

        /// <summary>Read-only stat pill (.ff-stat-chip + --ok/--warn/--neutral).</summary>
        public static VisualElement StatChip(string label, string value, StatKind kind = StatKind.Neutral)
        {
            var chip = new VisualElement();
            chip.AddToClassList("ff-stat-chip");
            chip.AddToClassList(kind == StatKind.Ok ? "ff-stat-chip--ok" : kind == StatKind.Warn ? "ff-stat-chip--warn" : "ff-stat-chip--neutral");
            var l = new Label(label);
            l.AddToClassList("ff-stat-chip__label");
            var v = new Label(value);
            v.AddToClassList("ff-stat-chip__value");
            chip.Add(l);
            chip.Add(v);
            return chip;
        }
    }
}
