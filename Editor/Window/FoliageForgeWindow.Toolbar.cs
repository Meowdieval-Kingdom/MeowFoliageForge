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
        // row 1: tab, asset, primary action (always visible); row 2: presets for the tab, then the brush and the setup wizard
        private VisualElement BuildToolbar()
        {
            var holder = new VisualElement { style = { flexShrink = 0 } };
            var bar = new Toolbar();
            _tabTrees = TabButton("Trees", false);
            _tabGrass = TabButton("Grass", true);
            bar.Add(_tabTrees);
            bar.Add(_tabGrass);
            bar.Add(new ToolbarSpacer());

            _treeTools = new VisualElement { style = { flexDirection = FlexDirection.Row, flexShrink = 1 } };
            _treeTools.Add(GroupLabel("Recipe"));
            var field = new ObjectField { objectType = typeof(TreeRecipe), allowSceneObjects = false, value = _source, tooltip = "The saved tree recipe. Edits go to a working copy until you bake. None: an unsaved scratch tree.", style = { minWidth = 150, flexShrink = 1 } };
            _recipeField = field;
            field.RegisterValueChangedCallback(e => SetRecipe(e.newValue as TreeRecipe));
            _treeTools.Add(field);
            _treeTools.Add(new ToolbarButton(CreateRecipe) { text = "New", tooltip = "Saves a new Oak recipe asset and opens it. With no recipe picked, saves the scratch tree instead." });
            bar.Add(_treeTools);

            var row2 = new Toolbar();
            _treePresets = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _treePresets.Add(GroupLabel("Start from"));
            var presets = new ToolbarMenu { text = "Preset", tooltip = "Replace every value with a preset tree. Undo restores." };
            foreach (TreePreset p in Enum.GetValues(typeof(TreePreset)))
            {
                TreePreset preset = p;
                presets.menu.AppendAction(preset.ToString(), _ => ApplyPreset(preset, TexturedPresets));
            }
            _treePresets.Add(presets);
            var textured = new ToolbarToggle { text = "Textured", value = TexturedPresets, tooltip = "Presets load with leaf, bark and moss textures. Off: no textures and flat solid cards. Applies to the next preset you pick." };
            textured.RegisterValueChangedCallback(e => TexturedPresets = e.newValue);
            _treePresets.Add(textured);
            _treePresets.Add(new ToolbarButton(SwapTextures) { text = "Swap Textures", tooltip = "Switch the current recipe between textured and untextured, keeping everything else. Your own textures stay. Undo restores." });
            _treePresets.Add(new ToolbarButton(Reroll) { text = "Reroll Seed", tooltip = "Same settings, new random branches. Undo restores." });
            _treePresets.Add(new ToolbarButton(BakeVariants) { text = "Bake All Variants", tooltip = "Bakes every variant of the recipe (Output tab > Type & Variants) as <Name>_V1, <Name>_V2, ... plus a <Name>_Variants set for the brush." });
            row2.Add(_treePresets);

            _grassTools = new VisualElement { style = { flexDirection = FlexDirection.Row, flexShrink = 1 } };
            _grassTools.Add(GroupLabel("Profile"));
            _grassField = new ObjectField { objectType = typeof(GrassProfile), allowSceneObjects = false, value = _grass != null && EditorUtility.IsPersistent(_grass) ? _grass : null, tooltip = "The Grass Profile asset, edited in place. None: an unsaved scratch profile.", style = { minWidth = 150, flexShrink = 1 } };
            _grassField.RegisterValueChangedCallback(e => SetGrass(e.newValue as GrassProfile));
            _grassTools.Add(_grassField);
            _grassTools.Add(new ToolbarButton(CreateGrass) { text = "New", tooltip = "Saves a copy of the current profile as a new asset and opens it." });
            bar.Add(_grassTools);

            _grassPresets = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _grassPresets.Add(GroupLabel("Scatter"));
            _grassPresets.Add(new ToolbarButton(() =>
            {
                if (_grass == null) return;
                Undo.RecordObject(_grass, "Reroll Grass Seed");
                _grass.Seed = UnityEngine.Random.Range(1, 99999);
                EditorUtility.SetDirty(_grass);
                _grass.Revision++;
            }) { text = "Reroll Seed", tooltip = "Same settings, new random scatter. Undo restores." });
            const string sizeTip = "Diameter of each baked grass batch, in metres (0.5 to 32). Small batches mix into tufts with the brush.";
            _grassPresets.Add(new ToolbarSpacer());
            _grassPresets.Add(GroupLabel("Batch size (m)", sizeTip));
            var bakeSize = new FloatField { value = _bakeSize, tooltip = sizeTip, style = { width = 44 } };
            bakeSize.RegisterValueChangedCallback(e => _bakeSize = Mathf.Clamp(e.newValue, 0.5f, 32f));
            _grassPresets.Add(bakeSize);
            row2.Add(_grassPresets);
            row2.Add(new ToolbarSpacer { style = { flexGrow = 1 } });
            row2.Add(new ToolbarButton(OpenBrush) { text = "Foliage Brush", tooltip = "Paint baked trees, grass batches and grass strokes in the Scene view." });
            row2.Add(new ToolbarButton(FoliageForgeSetupWizard.Open) { text = "Setup Wizard", tooltip = "Check shaders, folders and URP settings, with one-click fixes." });
            bar.Add(new ToolbarSpacer { style = { flexGrow = 1 } });

            _bakeTools = new VisualElement { style = { flexDirection = FlexDirection.Row } };

            _bakeButton = new ToolbarButton(Bake) { text = "Bake Prefab" };
            _bakeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _bakeButton.style.color = Color.white;
            _bakeTools.Add(new ToolbarButton(SaveAsNew) { text = "Save As New...", tooltip = "Copies this recipe into a new asset and bakes it as a separate prefab. The current prefab stays untouched." });
            _bakeTools.Add(_bakeButton);
            bar.Add(_bakeTools);
            holder.Add(bar);
            holder.Add(row2);
            RefreshBakeButton();
            return holder;
        }

        private static Label GroupLabel(string text, string tooltip = null) =>
            new Label(text) { tooltip = tooltip, style = { unityTextAlign = TextAnchor.MiddleLeft, opacity = 0.75f, marginLeft = 4, marginRight = 4 } };

        // the window often sits over the Scene view tab; bring a scene view forward so the brush panel is visible
        private void OpenBrush()
        {
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Focus();
            else GetWindow<SceneView>();
            FoliageBrush.Open();
            if (_status != null) _status.text = "Foliage Brush active in the Scene view (its panel: Overlays > Foliage Brush).";
        }

        // after a bake, make sure the gallery filter is not hiding what was just baked
        private void ShowBakedKind(bool grass)
        {
            if (_galleryFilter == 0 || _galleryFilter == (grass ? 2 : 1)) return;
            _galleryFilter = 0;
            foreach (Action p in _filterPaint) p();
        }

        private VisualElement BuildViewBar()
        {
            var holder = new VisualElement { style = { flexShrink = 0 } };

            var bar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row, alignItems = Align.Center, flexWrap = Wrap.Wrap, minHeight = 44, paddingLeft = 8, paddingRight = 8,
                    paddingTop = 2, paddingBottom = 2, backgroundColor = new Color(0.2f, 0.2f, 0.2f),
                },
            };
            bar.Add(new Label("View") { style = { opacity = 0.75f, marginRight = 10, unityFontStyleAndWeight = FontStyle.Bold } });
            _modeRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginRight = 12 } };
            _modeRow.Add(ModeButton("Wireframe", PreviewMode.Wireframe, "Show the mesh edges, to judge triangle density."));
            _modeRow.Add(ModeButton("Blobs", PreviewMode.Blobs, "Edit the canopy blobs that shape where the leaves grow."));
            _modeRow.Add(ModeButton("Render", PreviewMode.Finished, "The finished, shaded tree as it bakes."));
            _modeRow.Add(ModeButton("Blob", PreviewMode.Blob, "Generate the selected blob (the first when none is selected) alone at the origin, to judge its leaf groups."));
            _modeRow.Add(ModeButton("Variants", PreviewMode.Variants, "The recipe's variants side by side, each with its own seed, size and blob count."));
            bar.Add(_modeRow);
            bar.Add(BigToggle("Ortho", _preview.Orthographic, v =>
            {
                _preview.Orthographic = v;
                _view.MarkDirtyRepaint();
            }, "Orthographic camera: no perspective, for checking the silhouette."));
            bar.Add(BigToggle("Foliage", _preview.ShowFoliage, v =>
            {
                _preview.ShowFoliage = v;
                _view.MarkDirtyRepaint();
            }, "Show the leaves in the Render and Wireframe views."));
            bar.Add(BigToggle("Ground", _preview.ShowGround, v =>
            {
                _preview.ShowGround = v;
                _view.MarkDirtyRepaint();
            }, "Show the ground plane."));
            bar.Add(BigToggle("Scale", _preview.ShowScale, v =>
            {
                _preview.ShowScale = v;
                _view.MarkDirtyRepaint();
            }, "Show a size reference as tall as the number beside it."));
            var scaleHeight = new FloatField { value = _preview.ScaleHeight, tooltip = "Height of the size reference, in units.", style = { width = 48, marginRight = 8, marginLeft = -4 } };
            scaleHeight.RegisterValueChangedCallback(e =>
            {
                _preview.ScaleHeight = Mathf.Max(0.05f, e.newValue);
                _view.MarkDirtyRepaint();
            });
            bar.Add(scaleHeight);
            bar.Add(BigToggle("Backlit", _preview.Backlit, v =>
            {
                _preview.Backlit = v;
                _view.MarkDirtyRepaint();
            }, "Light from behind, to check leaf translucency."));
            bar.Add(BigToggle("Wind", _windOn, v =>
            {
                _windOn = v;
                _windPanel.SetEnabled(v);
                ApplyWind();
            }, "Animate wind in the preview and the open scene (Direction and Strength below)."));
            holder.Add(bar);

            _blobPanel = new VisualElement
            {
                style =
                {
                    marginLeft = 8, marginRight = 8, marginTop = 6,
                    paddingLeft = 10, paddingRight = 10, paddingTop = 4, paddingBottom = 4, backgroundColor = new Color(0.22f, 0.22f, 0.22f),
                    borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                    display = InBlobPanelMode ? DisplayStyle.Flex : DisplayStyle.None,
                },
            };
            // all blobs: build the set
            VisualElement tools = BlobRow();
            tools.Add(new Label("Blobs") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginRight = 8 } });
            tools.Add(new Button(AutoBlobs) { text = "Auto Blobs", tooltip = "Replace the blobs with ones spread over the branch tips (Leaves > Blob Count)." });
            tools.Add(new Button(RandomizeBlobs) { text = "Randomize", tooltip = "Replace the blobs with a random set using the sliders on the right. Click again for another roll; Undo goes back." });
            tools.Add(RandomSlider("Count", 2, 16, _randCount, v => _randCount = Mathf.RoundToInt(v)));
            tools.Add(RandomSlider("Size", 0.3f, 2f, _randSize, v => _randSize = v));
            tools.Add(RandomSlider("Spread", 0.1f, 1.5f, _randSpread, v => _randSpread = v));
            tools.Add(RandomSlider("Flatness", 0.4f, 1.3f, _randFlat, v => _randFlat = v));
            tools.Add(new Button(AddBlob) { text = "Add Blob", tooltip = "Add a blob at the top of the trunk and select it." });
            _blobPanel.Add(tools);

            // the selected blob: only shown while one is selected
            _blobSelected = BlobRow();
            _blobSelected.style.marginTop = 4;
            _blobSelected.style.paddingTop = 4;
            _blobSelected.style.borderTopWidth = 1;
            _blobSelected.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);
            _blobSelected.Add(new Label("Selected blob") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = new Color(1f, 0.75f, 0.2f), marginRight = 8 } });
            _positionField = BlobVector("Position", "Centre of the selected blob, in tree space.", (b, v) => b.Position = v);
            _radiiField = BlobVector("Radii", "Half-size of the selected blob along its own axes.", (b, v) => b.Radii = Vector3.Max(v, Vector3.one * 0.1f));
            _rotationField = BlobVector("Rotation", "Euler angles of the selected blob. Alt + scroll in the preview turns it around the vertical axis.", (b, v) => b.Rotation = v);
            _blobSelected.Add(_positionField);
            _blobSelected.Add(_radiiField);
            _blobSelected.Add(_rotationField);
            _blobSelected.Add(new Button(ResetShape) { text = "Reset Shape", tooltip = "Undo the point edits of the selected blob and make it a smooth ellipsoid again." });
            _blobSelected.Add(new Button(DeleteBlob) { text = "Delete", tooltip = "Delete the selected blob." });
            _blobPanel.Add(_blobSelected);
            _blobPanel.Add(BuildViewerRow());
            _blobPanel.schedule.Execute(SyncBlobFields).Every(100);
            _blobPanel.Add(new Label("Drag a blob to move it. Drag a point of the selected blob to deform it (Shift: neighbours follow). Scroll resizes (Ctrl: height only, Alt: rotate). Click empty space to deselect and orbit.")
                { style = { opacity = 0.65f, marginTop = 4, flexShrink = 1, whiteSpace = WhiteSpace.Normal } });
            holder.Add(_blobPanel);

            _windPanel = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row, alignItems = Align.Center, marginLeft = 8, marginRight = 8, marginTop = 6, marginBottom = 2,
                    paddingLeft = 10, paddingRight = 10, paddingTop = 4, paddingBottom = 4, backgroundColor = new Color(0.22f, 0.22f, 0.22f),
                    borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                },
            };
            _windPanel.Add(new Label("Wind") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginRight = 12 } });
            _windPanel.Add(WindSlider("Direction", 0f, 360f, _windDirection, v =>
            {
                _windDirection = v;
                ApplyWind();
            }));
            _windPanel.Add(WindSlider("Strength", 0f, 1f, _windStrength, v =>
            {
                _windStrength = v;
                ApplyWind();
            }));
            _windPanel.SetEnabled(_windOn);
            holder.Add(_windPanel);

            holder.schedule.Execute(() =>
            {
                if (_windOn) _view?.MarkDirtyRepaint();
            }).Every(33);
            return holder;
        }

        private Button TabButton(string text, bool grass)
        {
            var button = new ToolbarButton(() => SetGrassMode(grass)) { text = text };
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            return button;
        }
    }
}
