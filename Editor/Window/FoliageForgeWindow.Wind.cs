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
        // labelled slider that grows with its row (USS .ff-slider); label never narrower than 110px
        private static Slider WindSlider(string label, float min, float max, float value, Action<float> onChange)
        {
            var slider = new Slider(label, min, max) { value = value, showInputField = true };
            slider.AddToClassList("ff-slider");
            slider.labelElement.AddToClassList("ff-input-label");
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
