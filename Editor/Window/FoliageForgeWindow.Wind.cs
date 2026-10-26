using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Meowdieval.FoliageForge
{
    public sealed partial class FoliageForgeWindow
    {
        private static VisualElement BigToggle(string text, bool value, Action<bool> onChange, string tooltip)
        {
            var accent = new Color(0.2f, 0.4f, 0.62f);
            var idle = new Color(0.3f, 0.3f, 0.3f);
            var button = new Button { text = text, tooltip = tooltip };
            button.style.height = 32;
            button.style.minWidth = 72;
            button.style.marginTop = button.style.marginBottom = 2;
            button.style.marginRight = 8;
            button.style.marginLeft = 0;
            button.style.fontSize = 13;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 4;
            void Paint()
            {
                button.style.backgroundColor = value ? accent : idle;
                button.style.color = value ? Color.white : new Color(0.8f, 0.8f, 0.8f);
            }
            button.clicked += () =>
            {
                value = !value;
                Paint();
                onChange(value);
            };
            Paint();
            return button;
        }

        private static Slider WindSlider(string label, float min, float max, float value, Action<float> onChange)
        {
            var slider = new Slider(label, min, max) { value = value, showInputField = true, style = { flexGrow = 1, flexBasis = 0, minWidth = 120, marginRight = 10 } };
            slider.labelElement.style.minWidth = 64;
            slider.labelElement.style.width = 64;
            slider.RegisterValueChangedCallback(e => onChange(e.newValue));
            return slider;
        }

        private void ApplyWind()
        {
            if (_windOn) FoliageWind.SetGlobals(_windDirection, _windStrength, 1.2f, 0.35f, 0.7f);
            else ReleaseWind();
            _view?.MarkDirtyRepaint();
        }

        private static void ReleaseWind()
        {
            FoliageWind.SetGlobals(0f, 0f, 0f, 0f, 0f);
            foreach (FoliageWind w in FindObjectsByType<FoliageWind>(FindObjectsSortMode.None)) w.Reapply();
        }
    }
}
