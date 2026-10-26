using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Meowdieval.FoliageForge
{
    /// <summary>One line of the setup check: what it tests, whether it passes, and an optional one-click fix.</summary>
    sealed class SetupCheck
    {
        public string Name, Detail, FixLabel;
        public bool Ok;
        public SetupSeverity Severity;
        /// <summary>Current and target value text, or null for rows without a value.</summary>
        public string Current, Target;
        /// <summary>Asset the fix edits that other systems also use; the wizard confirms before touching it.</summary>
        public string SharedAsset;
        public Action Fix;
    }

    /// <summary>Checks that the project and open scene are ready for Foliage Forge. Menu: Meowdieval > Foliage Forge > Foliage Forge Validate.</summary>
    static class FoliageForgeValidate
    {
        const string GrassShaderPath = "Packages/com.meowdieval.foliageforge/Shaders/FoliageForgeGrass.shader";

        public static List<SetupCheck> Run()
        {
            var list = new List<SetupCheck>();

            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
            bool urp = FoliageForgeProjectSettings.IsUrp(rp);
            list.Add(new SetupCheck { Name = "Universal Render Pipeline active", Severity = SetupSeverity.Required, Ok = urp, Detail = urp ? rp.name : "Assign a URP asset in Project Settings > Graphics (and Quality)." });

            bool trees = TreeBaker.LoadShader() != null && TreeBaker.LoadBarkShader() != null;
            list.Add(new SetupCheck
            {
                Name = "Tree shaders imported", Severity = SetupSeverity.Required, Ok = trees, Detail = trees ? "FoliageForge and FoliageForgeBark" : "Shader Graph files did not import. Check the Console, then reimport.",
                FixLabel = "Reimport", Fix = () => AssetDatabase.ImportAsset(TreeBaker.ShaderPath, ImportAssetOptions.ForceUpdate),
            });

            bool grassShader = AssetDatabase.LoadAssetAtPath<Shader>(GrassShaderPath) != null;
            list.Add(new SetupCheck
            {
                Name = "Grass shader imported", Severity = SetupSeverity.Required, Ok = grassShader, Detail = grassShader ? "Meowdieval/Foliage Forge/Grass" : "Shader failed to compile. Check the Console.",
                FixLabel = "Reimport", Fix = () => AssetDatabase.ImportAsset(GrassShaderPath, ImportAssetOptions.ForceUpdate),
            });

            bool folder = AssetDatabase.IsValidFolder(TreeBaker.OutputFolder);
            list.Add(new SetupCheck
            {
                Name = "Bake output folder", Severity = SetupSeverity.Recommended, Ok = folder, Detail = TreeBaker.OutputFolder + (folder ? string.Empty : " does not exist yet."),
                FixLabel = "Create", Fix = () => TreeBaker.EnsurePath(TreeBaker.OutputFolder),
            });

            bool profile = AssetDatabase.FindAssets("t:GrassProfile").Length > 0;
            list.Add(new SetupCheck
            {
                Name = "Grass profile asset", Severity = SetupSeverity.Recommended, Ok = profile, Detail = profile ? "Found in the project." : "No Grass Profile yet.",
                FixLabel = "Create", Fix = () => GrassFieldEditor.FindOrCreateProfile(),
            });

            // the manager prefab carries the Foliage Wind, so one row covers both
            var manager = Object.FindFirstObjectByType<FoliageForgeManager>();
            bool wind = Object.FindFirstObjectByType<FoliageWind>() != null;
            list.Add(new SetupCheck
            {
                Name = "Foliage Forge Manager (with Foliage Wind) in the open scene", Severity = SetupSeverity.Recommended, Ok = manager != null && wind,
                Detail = manager == null ? "Adds the Meowdieval Foliage Forge prefab: paints trees and grass, and includes the Foliage Wind." : wind ? "Paint trees and grass with it; trees and grass share its wind." : "The manager has no Foliage Wind, so nothing sways.",
                FixLabel = manager == null ? "Create" : "Add Wind", Fix = () =>
                {
                    if (manager == null)
                    {
                        FoliageForgeManagerEditor.CreateInScene(null);
                        return;
                    }
                    var go = new GameObject("Foliage Wind");
                    go.AddComponent<FoliageWind>();
                    go.transform.SetParent(manager.transform, false);
                    Undo.RegisterCreatedObjectUndo(go, "Add Foliage Wind");
                },
            });

            FoliageForgeProjectSettings.AddChecks(list);
            return list;
        }

        [MenuItem("Meowdieval/Foliage Forge/Foliage Forge Validate", false, 2)]
        public static void ValidateMenu()
        {
            List<SetupCheck> checks = Run();
            int failed = 0;
            foreach (SetupCheck c in checks)
            {
                if (c.Ok) continue;
                failed++;
                string msg = $"[Foliage Forge] {c.Severity}: {c.Name}: {c.Detail}" + (c.Current != null ? $" ({c.Current} -> {c.Target})" : string.Empty);
                if (c.Severity == SetupSeverity.Optional) Debug.Log(msg);
                else Debug.LogWarning(msg);
            }
            Debug.Log(failed == 0 ? $"[Foliage Forge] Validate: all {checks.Count} checks passed." : $"[Foliage Forge] Validate: {checks.Count - failed}/{checks.Count} passed. Open Foliage Forge Setup Wizard to fix.");
        }
    }

    /// <summary>Checklist window with one-click fixes. Opens once per project after the package is first imported; nothing changes until a Fix is clicked.</summary>
    public sealed class FoliageForgeSetupWizard : EditorWindow
    {
        List<SetupCheck> _checks = new List<SetupCheck>();
        Vector2 _scroll;
        bool _showPassed;

        [MenuItem("Meowdieval/Foliage Forge/Foliage Forge Setup Wizard", false, 3)]
        public static void Open()
        {
            var w = GetWindow<FoliageForgeSetupWizard>(true, "Foliage Forge Setup", true);
            w.minSize = new Vector2(520f, 480f);
            w.Refresh();
            w.Show();
        }

        private void OnEnable() => Refresh();
        private void OnFocus() => Refresh();
        private void OnHierarchyChange() => Refresh();
        private void OnProjectChange() => Refresh();

        private void Refresh()
        {
            _checks = FoliageForgeValidate.Run();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Foliage Forge Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Trees are made and baked in the Foliage Forge Editor. Grass and placed trees are painted with the Foliage Forge Manager. Fix anything below marked as missing.", MessageType.None);

            RenderPipelineAsset current = FoliageForgeProjectSettings.CurrentPipeline();
            EditorGUILayout.LabelField($"Quality level: {QualitySettings.names[QualitySettings.GetQualityLevel()]}, pipeline asset: {(current != null ? current.name : "none")}", EditorStyles.miniLabel);
            EditorGUI.BeginChangeCheck();
            FoliageForgeProjectSettings.AllQualityLevels = EditorGUILayout.ToggleLeft("Check and fix the URP assets of all quality levels", FoliageForgeProjectSettings.AllQualityLevels);
            if (EditorGUI.EndChangeCheck()) Refresh();

            // failing rows grouped by severity; passing rows folded into one line
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            int failing = 0;
            foreach (SetupSeverity severity in Enum.GetValues(typeof(SetupSeverity)))
            {
                List<SetupCheck> rows = _checks.Where(c => !c.Ok && c.Severity == severity).ToList();
                if (rows.Count == 0) continue;
                failing += rows.Count;
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"{severity} ({rows.Count} to fix)", EditorStyles.boldLabel);
                foreach (SetupCheck c in rows) DrawRow(c);
            }
            if (failing == 0) EditorGUILayout.HelpBox("Everything is set up. Nothing to fix.", MessageType.Info);
            List<SetupCheck> passed = _checks.Where(c => c.Ok).ToList();
            if (passed.Count > 0)
            {
                EditorGUILayout.Space(4);
                _showPassed = EditorGUILayout.Foldout(_showPassed, $"{passed.Count} checks passed", true, EditorStyles.foldoutHeader);
                if (_showPassed) foreach (SetupCheck c in passed) DrawRow(c);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox("Left as they are on purpose: rendering path (Forward / Forward+ / Deferred), GPU Resident Drawer, HDR and render scale. Foliage Forge works with any of them.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_checks.Any(c => !c.Ok && c.Fix != null && c.Severity == SetupSeverity.Required)))
                    if (GUILayout.Button("Fix All Required", GUILayout.Height(26)))
                        ApplyFixes(_checks.Where(c => !c.Ok && c.Fix != null && c.Severity == SetupSeverity.Required));
                using (new EditorGUI.DisabledScope(!_checks.Any(c => !c.Ok && c.Fix != null && c.Severity != SetupSeverity.Optional)))
                    if (GUILayout.Button(new GUIContent("Fix All Recommended", "Fixes Required and Recommended rows. Optional rows only change through their own Fix button."), GUILayout.Height(26)))
                        ApplyFixes(_checks.Where(c => !c.Ok && c.Fix != null && c.Severity != SetupSeverity.Optional));
                if (GUILayout.Button("Open Foliage Forge Editor", GUILayout.Height(26))) FoliageForgeWindow.Open();
            }
            EditorGUILayout.Space(4);
        }

        // icon plus explicit Passed / To fix text, so the state never rests on colour alone
        private void DrawRow(SetupCheck c)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                string icon = c.Ok ? "TestPassed" : c.Severity == SetupSeverity.Required ? "TestFailed" : c.Severity == SetupSeverity.Recommended ? "console.warnicon.sml" : "console.infoicon.sml";
                GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.Width(22), GUILayout.Height(22));
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField($"{c.Name}   [{(c.Ok ? "Passed" : "To fix")}, {c.Severity}]", EditorStyles.boldLabel);
                    if (c.Current != null) EditorGUILayout.LabelField(c.Ok ? c.Current : $"{c.Current} -> {c.Target}", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(c.Detail, EditorStyles.wordWrappedMiniLabel);
                }
                if (!c.Ok && c.Fix != null && GUILayout.Button(c.FixLabel, GUILayout.ExpandWidth(false), GUILayout.MinWidth(80), GUILayout.Height(24)))
                    ApplyFixes(new[] { c });
            }
        }

        // one confirmation per click for anything that edits an asset the rest of the project shares
        private void ApplyFixes(IEnumerable<SetupCheck> rows)
        {
            List<SetupCheck> list = rows.ToList();
            if (list.Count == 0) return;
            string shared = string.Join(", ", list.Where(c => c.SharedAsset != null).Select(c => c.SharedAsset).Distinct());
            if (shared.Length > 0 && !EditorUtility.DisplayDialog("Foliage Forge", $"This changes {shared}, which the rest of the project uses too. Each change is logged to the Console and can be undone with Edit > Undo.", "Apply", "Cancel"))
                GUIUtility.ExitGUI();
            foreach (SetupCheck c in list) c.Fix();
            Refresh();
            GUIUtility.ExitGUI();
        }
    }

    /// <summary>Opens the wizard the first time the package loads in a project (that is, right after it is imported). It only opens the window; it changes nothing.</summary>
    static class FoliageForgeSetupAutoOpen
    {
        const string Key = "FoliageForge.SetupWizardShown";

        [InitializeOnLoadMethod]
        private static void Init()
        {
            if (Application.isBatchMode || EditorUserSettings.GetConfigValue(Key) == "1") return;
            EditorApplication.update += WaitForEditor;
        }

        // wait out compiling and importing, so the window does not open on top of the import progress bar
        private static void WaitForEditor()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= WaitForEditor;
            EditorUserSettings.SetConfigValue(Key, "1");
            FoliageForgeSetupWizard.Open();
        }
    }
}
