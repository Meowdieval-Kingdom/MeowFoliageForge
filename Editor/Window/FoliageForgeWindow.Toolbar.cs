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
        // row 1: tab, asset, More menu (compact widths), bake actions; row 2: presets for the tab, then the brush and the setup wizard.
        // Both rows wrap. Below CompactWidth row 2 is hidden and its actions live in the More menu.
        private VisualElement BuildToolbar()
        {
            var holder = new VisualElement();
            holder.AddToClassList("ff-toolbar");
            VisualElement row1 = FoliageUi.WrapRow();
            _tabTrees = TabButton("Trees", false);
            _tabGrass = TabButton("Grass", true);
            row1.Add(_tabTrees);
            row1.Add(_tabGrass);

            _treeTools = new VisualElement();
            _treeTools.AddToClassList("ff-toolbar__asset");
            _treeTools.Add(FoliageUi.GroupLabel("Recipe"));
            var field = new ObjectField { objectType = typeof(TreeRecipe), allowSceneObjects = false, value = _source, tooltip = "The saved tree recipe. Edits go to a working copy until you bake. None: an unsaved scratch tree." };
            _recipeField = field;
            field.RegisterValueChangedCallback(e => SetRecipe(e.newValue as TreeRecipe));
            _treeTools.Add(field);
            _treeTools.Add(SmallButton("New", CreateRecipe, "Saves a new Oak recipe asset and opens it. With no recipe picked, saves the scratch tree instead."));
            row1.Add(_treeTools);

            _grassTools = new VisualElement();
            _grassTools.AddToClassList("ff-toolbar__asset");
            _grassTools.Add(FoliageUi.GroupLabel("Profile"));
            _grassField = new ObjectField { objectType = typeof(GrassProfile), allowSceneObjects = false, value = _grass != null && EditorUtility.IsPersistent(_grass) ? _grass : null, tooltip = "The Grass Profile asset, edited in place. None: an unsaved scratch profile." };
            _grassField.RegisterValueChangedCallback(e => SetGrass(e.newValue as GrassProfile));
            _grassTools.Add(_grassField);
            _grassTools.Add(SmallButton("New", CreateGrass, "Saves a copy of the current profile as a new asset and opens it."));
            row1.Add(_grassTools);

            // second row: presets / scatter for the tab, then brush + wizard
            _secondaryRow = FoliageUi.WrapRow();
            _secondaryRow.AddToClassList("ff-hide-compact");

            _treePresets = new VisualElement();
            _treePresets.AddToClassList("ff-toolbar__group");
            _treePresets.Add(FoliageUi.GroupLabel("Start from"));
            var presets = new ToolbarMenu { text = "Preset", tooltip = "Replace every value with a preset tree. Undo restores." };
            presets.AddToClassList("ff-button");
            foreach (TreePreset p in Enum.GetValues(typeof(TreePreset)))
            {
                TreePreset preset = p;
                presets.menu.AppendAction(preset.ToString(), _ => ApplyPreset(preset, TexturedPresets));
            }
            _treePresets.Add(presets);
            Button textured = null;
            textured = FoliageUi.ChipButton("Textured", TexturedPresets, v => TexturedPresets = v, "Presets load with leaf, bark and moss textures. Off: no textures and flat solid cards. Applies to the next preset you pick.");
            _treePresets.Add(textured);
            _treePresets.Add(SmallButton("Swap Textures", SwapTextures, "Switch the current recipe between textured and untextured, keeping everything else. Your own textures stay. Undo restores."));
            _treePresets.Add(SmallButton("Reroll Seed", Reroll, "Same settings, new random branches. Undo restores."));
            _treePresets.Add(SmallButton("Bake All Variants", BakeVariants, "Bakes every variant of the recipe (Output tab > Type & Variants) as <Name>_V1, <Name>_V2, ... plus a <Name>_Variants set for the brush."));
            _secondaryRow.Add(_treePresets);

            _grassPresets = new VisualElement();
            _grassPresets.AddToClassList("ff-toolbar__group");
            _grassPresets.Add(FoliageUi.GroupLabel("Scatter"));
            _grassPresets.Add(SmallButton("Reroll Seed", RerollGrass, "Same settings, new random scatter. Undo restores."));
            const string sizeTip = "Diameter of each baked grass batch, in metres (0.5 to 32). Small batches mix into tufts with the brush.";
            _grassPresets.Add(FoliageUi.GroupLabel("Batch size (m)", sizeTip));
            var bakeSize = new FloatField { value = _bakeSize, tooltip = sizeTip };
            bakeSize.AddToClassList("ff-input-small");
            bakeSize.RegisterValueChangedCallback(e => _bakeSize = Mathf.Clamp(e.newValue, 0.5f, 32f));
            _grassPresets.Add(bakeSize);
            _secondaryRow.Add(_grassPresets);

            var spacer = new VisualElement();
            spacer.AddToClassList("ff-toolbar__spacer");
            _secondaryRow.Add(spacer);
            _secondaryRow.Add(SmallButton("Foliage Brush", OpenBrush, "Paint baked trees, grass batches and grass strokes in the Scene view."));
            _secondaryRow.Add(SmallButton("Setup Wizard", FoliageForgeSetupWizard.Open, "Check shaders, folders and URP settings, with one-click fixes."));

            // compact widths: everything from row 2 in one dropdown
            row1.Add(BuildMoreMenu(textured));

            _bakeTools = new VisualElement();
            _bakeTools.AddToClassList("ff-toolbar__group");
            _bakeTools.Add(SmallButton("Save As New...", SaveAsNew, "Copies this recipe into a new asset and bakes it as a separate prefab. The current prefab stays untouched."));
            _bakeButton = new ToolbarButton(Bake) { text = "Bake Prefab" };
            _bakeButton.AddToClassList("ff-bake");
            _bakeTools.Add(_bakeButton);
            row1.Add(_bakeTools);

            holder.Add(row1);
            holder.Add(_secondaryRow);
            RefreshBakeButton();
            return holder;
        }

        private static Button SmallButton(string text, Action click, string tooltip)
        {
            var b = new Button(click) { text = text, tooltip = tooltip };
            b.AddToClassList("ff-button");
            return b;
        }

        private ToolbarMenu BuildMoreMenu(Button texturedChip)
        {
            var more = new ToolbarMenu { text = "More", tooltip = "Presets, Foliage Brush and Setup Wizard." };
            more.AddToClassList("ff-button");
            more.AddToClassList("ff-show-compact");
            DropdownMenu menu = more.menu;
            Func<DropdownMenuAction, DropdownMenuAction.Status> treeOnly = _ => _grassMode ? DropdownMenuAction.Status.Hidden : DropdownMenuAction.Status.Normal;
            Func<DropdownMenuAction, DropdownMenuAction.Status> grassOnly = _ => _grassMode ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Hidden;
            foreach (TreePreset p in Enum.GetValues(typeof(TreePreset)))
            {
                TreePreset preset = p;
                menu.AppendAction("Preset: " + preset, _ => ApplyPreset(preset, TexturedPresets), treeOnly);
            }
            menu.AppendAction("Textured presets", _ =>
            {
                TexturedPresets = !TexturedPresets;
                FoliageUi.SetChipOn(texturedChip, TexturedPresets);
            }, a => _grassMode ? DropdownMenuAction.Status.Hidden : TexturedPresets ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            menu.AppendAction("Swap Textures", _ => SwapTextures(), treeOnly);
            menu.AppendAction("Reroll Seed", _ => Reroll(), treeOnly);
            menu.AppendAction("Bake All Variants", _ => BakeVariants(), treeOnly);
            menu.AppendAction("Reroll Seed", _ => RerollGrass(), grassOnly);
            menu.AppendSeparator();
            menu.AppendAction("Foliage Brush", _ => OpenBrush());
            menu.AppendAction("Setup Wizard", _ => FoliageForgeSetupWizard.Open());
            return more;
        }

        private void RerollGrass()
        {
            if (_grass == null) return;
            Undo.RecordObject(_grass, "Reroll Grass Seed");
            _grass.Seed = UnityEngine.Random.Range(1, 99999);
            EditorUtility.SetDirty(_grass);
            _grass.Revision++;
        }

        // bake button colour comes from USS: "create" (green), "overwrite" (orange), "current" (neutral)
        private void SetBakeLook(string state)
        {
            foreach (string s in new[] { "create", "overwrite", "current" }) _bakeButton.EnableInClassList("ff-bake--" + s, s == state);
        }

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

        private Button ModeChip(string text, PreviewMode mode, string tooltip)
        {
            Button chip = FoliageUi.ChipButton(text, _preview.Mode == mode, _ => SetMode(mode), tooltip, false);
            _modePaint.Add(() => FoliageUi.SetChipOn(chip, _preview.Mode == mode));
            return chip;
        }

        private VisualElement BuildViewBar()
        {
            VisualElement bar = FoliageUi.WrapRow();
            bar.AddToClassList("ff-viewbar");
            bar.Add(FoliageUi.GroupLabel("View"));
            _modeRow = FoliageUi.WrapRow();
            _modeRow.Add(ModeChip("Wireframe", PreviewMode.Wireframe, "Show the mesh edges, to judge triangle density."));
            _modeRow.Add(ModeChip("Blobs", PreviewMode.Blobs, "Edit the canopy blobs that shape where the leaves grow."));
            _modeRow.Add(ModeChip("Render", PreviewMode.Finished, "The finished, shaded tree as it bakes."));
            _modeRow.Add(ModeChip("Blob", PreviewMode.Blob, "Generate the selected blob (the first when none is selected) alone at the origin, to judge its leaf groups."));
            _modeRow.Add(ModeChip("Variants", PreviewMode.Variants, "The recipe's variants side by side, each with its own seed, size and blob count."));
            bar.Add(_modeRow);
            bar.Add(FoliageUi.ChipButton("Auto Rotate", _preview.AutoRotate, v => _preview.AutoRotate = v, "Slowly turn the preview around the tree. Drag to take over; the angle is kept when you switch prefabs."));
            bar.schedule.Execute(() => { if (_preview.AutoRotate) _view?.MarkDirtyRepaint(); }).Every(16); // ponytail: ticks even when off (cheap check); pause it if that ever shows in a profile
            bar.Add(FoliageUi.ChipButton("Ortho", _preview.Orthographic, v =>
            {
                _preview.Orthographic = v;
                _view.MarkDirtyRepaint();
            }, "Orthographic camera: no perspective, for checking the silhouette."));
            bar.Add(FoliageUi.ChipButton("Foliage", _preview.ShowFoliage, v =>
            {
                _preview.ShowFoliage = v;
                _view.MarkDirtyRepaint();
            }, "Show the leaves in the Render and Wireframe views."));
            bar.Add(FoliageUi.ChipButton("Ground", _preview.ShowGround, v =>
            {
                _preview.ShowGround = v;
                _view.MarkDirtyRepaint();
            }, "Show the ground plane."));
            bar.Add(FoliageUi.ChipButton("Scale", _preview.ShowScale, v =>
            {
                _preview.ShowScale = v;
                _view.MarkDirtyRepaint();
            }, "Show a size reference as tall as the number beside it."));
            var scaleHeight = new FloatField { value = _preview.ScaleHeight, tooltip = "Height of the size reference, in units." };
            scaleHeight.AddToClassList("ff-input-small");
            scaleHeight.RegisterValueChangedCallback(e =>
            {
                _preview.ScaleHeight = Mathf.Max(0.05f, e.newValue);
                _view.MarkDirtyRepaint();
            });
            bar.Add(scaleHeight);
            bar.Add(FoliageUi.ChipButton("Backlit", _preview.Backlit, v =>
            {
                _preview.Backlit = v;
                _view.MarkDirtyRepaint();
            }, "Light from behind, to check leaf translucency."));
            bar.Add(FoliageUi.ChipButton("Wind", _windOn, v =>
            {
                _windOn = v;
                _windPanel.SetEnabled(v);
                ApplyWind();
            }, "Animate wind in the preview and the open scene (Direction and Strength below)."));
            bar.schedule.Execute(() =>
            {
                if (_windOn) _view?.MarkDirtyRepaint();
            }).Every(33);
            return bar;
        }

        private VisualElement BuildBlobPanel()
        {
            _blobPanel = new VisualElement { style = { display = InBlobPanelMode ? DisplayStyle.Flex : DisplayStyle.None } };
            _blobPanel.AddToClassList("ff-panel");
            // all blobs: build the set
            VisualElement tools = BlobRow();
            tools.Add(Title("Blobs"));
            tools.Add(SmallButton("Auto Blobs", AutoBlobs, "Replace the blobs with ones spread over the branch tips (Leaves > Blob Count)."));
            tools.Add(SmallButton("Randomize", RandomizeBlobs, "Replace the blobs with a random set using the sliders on the right. Click again for another roll; Undo goes back."));
            tools.Add(SmallButton("Add Blob", AddBlob, "Add a blob at the top of the trunk and select it."));
            _blobPanel.Add(tools);
            VisualElement randoms = BlobRow();
            randoms.Add(WindSlider("Count", 2, 16, _randCount, v => _randCount = Mathf.RoundToInt(v)));
            randoms.Add(WindSlider("Size", 0.3f, 2f, _randSize, v => _randSize = v));
            randoms.Add(WindSlider("Spread", 0.1f, 1.5f, _randSpread, v => _randSpread = v));
            randoms.Add(WindSlider("Flatness", 0.4f, 1.3f, _randFlat, v => _randFlat = v));
            _blobPanel.Add(randoms);

            // the selected blob: only shown while one is selected
            _blobSelected = BlobRow();
            _blobSelected.AddToClassList("ff-divider");
            Label selectedTitle = Title("Selected blob");
            selectedTitle.AddToClassList("ff-title--selected");
            _blobSelected.Add(selectedTitle);
            _positionField = BlobVector("Position", "Centre of the selected blob, in tree space.", (b, v) => b.Position = v);
            _radiiField = BlobVector("Radii", "Half-size of the selected blob along its own axes.", (b, v) => b.Radii = Vector3.Max(v, Vector3.one * 0.1f));
            _rotationField = BlobVector("Rotation", "Euler angles of the selected blob. Alt + scroll in the preview turns it around the vertical axis.", (b, v) => b.Rotation = v);
            _blobSelected.Add(_positionField);
            _blobSelected.Add(_radiiField);
            _blobSelected.Add(_rotationField);
            _blobSelected.Add(SmallButton("Reset Shape", ResetShape, "Undo the point edits of the selected blob and make it a smooth ellipsoid again."));
            _blobSelected.Add(SmallButton("Delete", DeleteBlob, "Delete the selected blob."));
            _blobPanel.Add(_blobSelected);
            _blobPanel.Add(BuildViewerRow());
            _blobPanel.schedule.Execute(SyncBlobFields).Every(100);
            var hint = new Label("Drag a blob to move it. Drag a point of the selected blob to deform it (Shift: neighbours follow). Scroll resizes (Ctrl: height only, Alt: rotate). Click empty space to deselect and orbit.");
            hint.AddToClassList("ff-hint");
            _blobPanel.Add(hint);
            return _blobPanel;
        }

        private static Label Title(string text)
        {
            var l = new Label(text);
            l.AddToClassList("ff-title");
            return l;
        }

        private VisualElement BuildWindPanel()
        {
            _windPanel = FoliageUi.WrapRow();
            _windPanel.AddToClassList("ff-panel");
            _windPanel.Add(Title("Wind"));
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
            return _windPanel;
        }

        private Button TabButton(string text, bool grass) =>
            FoliageUi.ChipButton(text, false, _ => SetGrassMode(grass), grass ? "Edit grass profiles." : "Edit tree recipes.", false);
    }
}
