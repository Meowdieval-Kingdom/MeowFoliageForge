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
            var box = new VisualElement
            {
                style =
                {
                    flexShrink = 0, backgroundColor = new Color(0.17f, 0.17f, 0.17f), borderTopWidth = 1, borderTopColor = new Color(0.1f, 0.1f, 0.1f),
                    paddingLeft = 8, paddingRight = 8, paddingTop = 4, paddingBottom = 4,
                },
            };
            var head = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            _galleryTitle = new Label("Prefabs") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginRight = 10 } };
            _folderLabel = new Label { style = { opacity = 0.6f, flexGrow = 1, flexShrink = 1, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis } };
            head.Add(_galleryTitle);
            head.Add(FilterButton("All", 0));
            head.Add(FilterButton("Trees", 1));
            head.Add(FilterButton("Grass", 2));
            head.Add(_folderLabel);
            _folderLabel.style.marginLeft = 8;
            head.Add(new Button(ChooseOutputFolder) { text = "Folder...", tooltip = "Choose the folder bakes go into (inside Assets)." });
            head.Add(new Button(() =>
            {
                Object folder = AssetDatabase.LoadAssetAtPath<Object>(TreeBaker.OutputFolder);
                if (folder != null) EditorGUIUtility.PingObject(folder);
            }) { text = "Show", tooltip = "Highlight the output folder in the Project window." });
            box.Add(head);
            _tiles = new ScrollView(ScrollViewMode.Horizontal) { style = { height = 134, marginTop = 4 } };
            _tiles.contentContainer.style.flexDirection = FlexDirection.Row;
            box.Add(_tiles);
            _loadButton = new Button(LoadPicked) { text = "Load Selected Prefab", tooltip = "Open the picked prefab's recipe or grass profile here (switches tab). Double-clicking a tile does the same.", style = { marginTop = 4, alignSelf = Align.FlexStart } };
            _loadButton.SetEnabled(false);
            _deleteButton = new Button(DeletePicked) { text = "Delete", tooltip = "Move the picked prefab to the OS trash. Its recipe / grass profile is kept.", style = { marginTop = 4 } };
            _deleteButton.SetEnabled(false);
            var actions = new VisualElement { style = { flexDirection = FlexDirection.Row } };
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
            var button = new Button(() =>
            {
                _galleryFilter = filter;
                foreach (Action p in _filterPaint) p();
                RefreshGallery();
            })
                { text = text, tooltip = $"Show {(filter == 0 ? "all baked prefabs" : filter == 1 ? "baked trees only" : "grass batches only")}.", style = { marginLeft = 0, marginRight = 0 } };
            void Paint()
            {
                bool on = _galleryFilter == filter;
                button.style.backgroundColor = on ? new StyleColor(new Color(0.2f, 0.4f, 0.62f)) : new StyleColor(StyleKeyword.Null);
                button.style.unityFontStyleAndWeight = on ? FontStyle.Bold : FontStyle.Normal;
            }
            _filterPaint.Add(Paint);
            Paint();
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
                    tooltip = AssetDatabase.GetAssetPath(go) + "\nClick: pick it (white frame, bold name). Double-click or Load below: open it here." + (current ? "\nBlue with ●: what the editor is showing now." : string.Empty),
                    style =
                    {
                        width = 104, marginRight = 6, paddingTop = 4, paddingBottom = 2, alignItems = Align.Center,
                        backgroundColor = current ? new Color(0.2f, 0.4f, 0.62f) : new Color(0.23f, 0.23f, 0.23f),
                        borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                        borderTopWidth = go == _picked ? 2 : 0, borderBottomWidth = go == _picked ? 2 : 0, borderLeftWidth = go == _picked ? 2 : 0, borderRightWidth = go == _picked ? 2 : 0,
                        borderTopColor = Color.white, borderBottomColor = Color.white, borderLeftColor = Color.white, borderRightColor = Color.white,
                    },
                };
                var image = new Image { scaleMode = ScaleMode.ScaleToFit, style = { width = 96, height = 96, backgroundColor = new Color(0.14f, 0.16f, 0.15f) } };
                tile.Add(image);
                tile.Add(new Label(current ? "● " + go.name : go.name) { style = { fontSize = 11, marginTop = 2, maxWidth = 98, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis, unityFontStyleAndWeight = go == _picked ? FontStyle.Bold : FontStyle.Normal } });
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
                _tiles.Add(new Label(empty) { style = { opacity = 0.75f, marginTop = 20, marginLeft = 4 } });
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
