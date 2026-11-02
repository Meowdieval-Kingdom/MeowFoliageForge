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
        private VisualElement BuildGallery()
        {
            var box = new VisualElement();
            box.AddToClassList("ff-gallery");
            VisualElement head = FoliageUi.WrapRow();
            _galleryTitle = new Label("Prefabs");
            _galleryTitle.AddToClassList("ff-title");
            _folderLabel = new Label();
            _folderLabel.AddToClassList("ff-muted");
            _folderLabel.AddToClassList("ff-gallery__folder");
            head.Add(_galleryTitle);
            head.Add(FilterButton("All", 0));
            head.Add(FilterButton("Trees", 1));
            head.Add(FilterButton("Grass", 2));
            head.Add(_folderLabel);
            head.Add(SmallButton("Folder...", ChooseOutputFolder, "Choose the folder bakes go into (inside Assets)."));
            head.Add(SmallButton("Show", () =>
            {
                Object folder = AssetDatabase.LoadAssetAtPath<Object>(TreeBaker.OutputFolder);
                if (folder != null) EditorGUIUtility.PingObject(folder);
            }, "Highlight the output folder in the Project window."));
            box.Add(head);

            // thumbnail size toggle: tiles wrap and the size is a class on the tile container, so no refresh is needed
            _tiles = new VisualElement();
            _tiles.AddToClassList("ff-gallery__tiles");
            Button small = null, medium = null;
            void PaintSize()
            {
                FoliageUi.SetChipOn(small, _smallThumbs);
                FoliageUi.SetChipOn(medium, !_smallThumbs);
                _tiles.EnableInClassList("ff-gallery__tiles--small", _smallThumbs);
            }
            small = FoliageUi.ChipButton("Small", _smallThumbs, _ => { _smallThumbs = true; PaintSize(); }, "Small thumbnails.", false);
            medium = FoliageUi.ChipButton("Medium", !_smallThumbs, _ => { _smallThumbs = false; PaintSize(); }, "Medium thumbnails.", false);
            VisualElement sizeRow = FoliageUi.WrapRow();
            sizeRow.Add(FoliageUi.GroupLabel("Thumbnails"));
            sizeRow.Add(small);
            sizeRow.Add(medium);
            box.Add(sizeRow);
            PaintSize();
            box.Add(_tiles);

            _loadButton = SmallButton("Load Selected Prefab", LoadPicked, "Open the picked prefab's recipe or grass profile here (switches tab). Double-clicking a tile does the same.");
            _loadButton.SetEnabled(false);
            _deleteButton = SmallButton("Delete", DeletePicked, "Move the picked prefab to the OS trash. Its recipe / grass profile is kept.");
            _deleteButton.SetEnabled(false);
            VisualElement actions = FoliageUi.WrapRow();
            actions.Add(_loadButton);
            actions.Add(_deleteButton);
            box.Add(actions);
            return box;
        }

        Button _deleteButton;

        private void DeletePicked()
        {
            if (_picked == null) return;
            string path = AssetDatabase.GetAssetPath(_picked);
            if (!EditorUtility.DisplayDialog("Delete prefab", $"Move {path} to the trash?\nThe recipe / grass profile is kept.", "Delete", "Cancel")) return;
            bool ok = AssetDatabase.MoveAssetToTrash(path);
            _picked = null;
            if (_status != null) _status.text = ok ? $"Deleted {path}" : $"Could not delete {path}";
            RefreshGallery();
        }

        private Button FilterButton(string text, int filter)
        {
            Button button = FoliageUi.ChipButton(text, _galleryFilter == filter, _ =>
            {
                _galleryFilter = filter;
                foreach (Action p in _filterPaint) p();
                RefreshGallery();
            }, $"Show {(filter == 0 ? "all baked prefabs" : filter == 1 ? "baked trees only" : "grass batches only")}.", false);
            _filterPaint.Add(() => FoliageUi.SetChipOn(button, _galleryFilter == filter));
            return button;
        }

        private void ChooseOutputFolder()
        {
            string picked = EditorUtility.OpenFolderPanel("Prefab output folder", Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(picked)) return;
            picked = picked.Replace('\\', '/');
            string data = Application.dataPath;
            if (picked != data && !picked.StartsWith(data + "/"))
            {
                if (_status != null) _status.text = "Pick a folder inside this project's Assets folder.";
                return;
            }
            TreeBaker.OutputFolder = "Assets" + picked.Substring(data.Length);
            RefreshGallery();
        }

        private void RefreshGallery()
        {
            if (_tiles == null) return;
            _tiles.Clear();
            _folderLabel.text = TreeBaker.OutputFolder;
            GameObject[] prefabs = TreeBaker.BakedPrefabs();
            _galleryTitle.text = $"Prefabs ({prefabs.Length})";
            if (_picked == null || System.Array.IndexOf(prefabs, _picked) < 0) _picked = null;
            _loadButton.text = _picked != null ? $"Load \"{_picked.name}\"" : "Load Selected Prefab";
            _loadButton.SetEnabled(_picked != null);
            _deleteButton.SetEnabled(_picked != null);
            var images = new System.Collections.Generic.List<(Image image, GameObject go)>();
            foreach (GameObject go in prefabs)
            {
                bool isGrass = go.TryGetComponent(out GrassBatch gb);
                if ((_galleryFilter == 1 && isGrass) || (_galleryFilter == 2 && !isGrass)) continue;
                bool current = isGrass ? _grassMode && gb.Profile == _grass : !_grassMode && _recipe != null && _recipe.name == go.name;
                var tile = new VisualElement
                {
                    tooltip = AssetDatabase.GetAssetPath(go) + "\nClick: pick it (framed, bold name). Double-click or Load below: open it here." + (current ? "\nBlue with ●: what the editor is showing now." : string.Empty),
                };
                tile.AddToClassList("ff-tile");
                tile.EnableInClassList("ff-tile--current", current);
                tile.EnableInClassList("ff-tile--picked", go == _picked);
                var image = new Image { scaleMode = ScaleMode.ScaleToFit };
                image.AddToClassList("ff-thumb");
                tile.Add(image);
                var nameLabel = new Label(current ? "● " + go.name : go.name);
                nameLabel.AddToClassList("ff-tile__name");
                nameLabel.EnableInClassList("ff-tile__name--picked", go == _picked);
                tile.Add(nameLabel);
                GameObject prefab = go;
                tile.RegisterCallback<MouseDownEvent>(e =>
                {
                    if (e.button != 0) return;
                    _picked = prefab;
                    if (e.clickCount == 2)
                    {
                        LoadPicked();
                        return;
                    }
                    EditorGUIUtility.PingObject(prefab);
                    RefreshGallery();
                });
                _tiles.Add(tile);
                images.Add((image, go));
            }
            if (_galleryFilter != 0) _galleryTitle.text = $"Prefabs ({images.Count}/{prefabs.Length})";
            if (images.Count == 0)
            {
                string empty = prefabs.Length == 0 ? "Nothing baked yet. Bake saves trees and grass batches into the folder above."
                    : _galleryFilter == 1 ? "No baked trees yet. Bake one in the Trees tab." : "No grass batches yet. Bake them in the Grass tab.";
                var emptyLabel = new Label(empty);
                emptyLabel.AddToClassList("ff-muted");
                emptyLabel.AddToClassList("ff-empty");
                _tiles.Add(emptyLabel);
                return;
            }
            // thumbnails render asynchronously; keep asking until each one is ready
            int tries = 0;
            IVisualElementScheduledItem poll = null;
            poll = _tiles.schedule.Execute(() =>
            {
                bool pending = false;
                foreach (var (image, go) in images)
                {
                    if (image.image != null) continue;
                    Texture2D preview = PrefabThumbnail.Get(go);
                    if (preview != null) image.image = preview;
                    else pending = true;
                }
                if (!pending || ++tries > 40) poll.Pause();
            }).Every(150);
        }

        // the recipe carries the whole tree type (layout, blobs, look), so loading it loads the type too
        private void LoadPicked()
        {
            if (_picked == null) return;
            if (_picked.TryGetComponent(out GrassBatch batch))
            {
                if (batch.Profile == null)
                {
                    _status.text = $"{_picked.name} has no grass profile any more.";
                    return;
                }
                if (!_grassMode) SetGrassMode(true);
                SetGrass(batch.Profile);
                _status.text = $"Loaded {AssetDatabase.GetAssetPath(batch.Profile)}";
                RefreshGallery();
                return;
            }
            TreeRecipe recipe = TreeBaker.RecipeFor(_picked);
            if (recipe == null)
            {
                if (_status != null) _status.text = $"No recipe named {_picked.name} found in the project.";
                return;
            }
            if (_grassMode) SetGrassMode(false);
            SetRecipe(recipe);
            if (InBlobPanelMode && recipe.Leaves.Layout != LeafLayout.Blobs) SetMode(PreviewMode.Finished);
            if (_status != null) _status.text = $"Loaded {AssetDatabase.GetAssetPath(recipe)}";
        }
    }
}
