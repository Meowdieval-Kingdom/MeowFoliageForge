using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Meowdieval.FoliageForge
{
    /// <summary>Shows the leaf texture's opaque part with a dot at the anchor; click or drag to move where the leaf attaches to the branch.</summary>
    public sealed class AnchorPicker : VisualElement
    {
        const float Box = 150f;
        readonly SerializedObject _so;
        readonly SerializedProperty _anchor, _texture, _columns, _rows;
        readonly Image _image = new Image { scaleMode = ScaleMode.StretchToFill };
        readonly VisualElement _dot = new VisualElement();
        Texture2D _shown;
        Vector2Int _shownGrid;

        public AnchorPicker(SerializedObject so)
        {
            _so = so;
            _anchor = so.FindProperty($"{nameof(TreeRecipe.Leaves)}.{nameof(LeafSettings.Anchor)}");
            _texture = so.FindProperty($"{nameof(TreeRecipe.Look)}.{nameof(LookSettings.LeafTexture)}");
            _columns = so.FindProperty($"{nameof(TreeRecipe.LeafShapes)}.{nameof(LeafShapeSettings.Columns)}");
            _rows = so.FindProperty($"{nameof(TreeRecipe.LeafShapes)}.{nameof(LeafShapeSettings.Rows)}");
            tooltip = "Click or drag to choose which point of the leaf sits on the branch.";
            style.marginTop = 2;
            style.marginBottom = 4;
            style.marginLeft = 4;
            style.width = Box;
            style.height = Box;
            style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;

            _dot.pickingMode = PickingMode.Ignore;
            _dot.style.position = Position.Absolute;
            _dot.style.width = 12;
            _dot.style.height = 12;
            _dot.style.borderTopLeftRadius = _dot.style.borderTopRightRadius = _dot.style.borderBottomLeftRadius = _dot.style.borderBottomRightRadius = 6;
            _dot.style.backgroundColor = new Color(1f, 0.25f, 0.2f);
            _dot.style.borderTopWidth = _dot.style.borderBottomWidth = _dot.style.borderLeftWidth = _dot.style.borderRightWidth = 2;
            _dot.style.borderTopColor = _dot.style.borderBottomColor = _dot.style.borderLeftColor = _dot.style.borderRightColor = Color.white;
            _image.Add(_dot);
            Add(_image);

            _image.RegisterCallback<PointerDownEvent>(e =>
            {
                _image.CapturePointer(e.pointerId);
                Set(e.localPosition);
            });
            _image.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (_image.HasPointerCapture(e.pointerId)) Set(e.localPosition);
            });
            _image.RegisterCallback<PointerUpEvent>(e => _image.ReleasePointer(e.pointerId));
            schedule.Execute(Refresh).Every(250);
            Refresh();
        }

        private void Set(Vector2 local)
        {
            float w = Mathf.Max(_image.resolvedStyle.width, 1f), h = Mathf.Max(_image.resolvedStyle.height, 1f);
            _so.Update();
            _anchor.vector2Value = new Vector2(Mathf.Clamp01(local.x / w), Mathf.Clamp01(1f - local.y / h));
            _so.ApplyModifiedProperties();
            Refresh();
        }

        private void Refresh()
        {
            if (_so.targetObject == null) return;
            _so.Update();
            var tex = _texture.objectReferenceValue as Texture2D;
            var grid = new Vector2Int(_columns.intValue, _rows.intValue);
            if (tex != _shown || grid != _shownGrid || _image.image == null)
            {
                _shown = tex;
                _shownGrid = grid;
                Texture2D drawn = tex != null ? tex : LeafTexture.Temporary();
                Rect content = LeafBounds.Cells(tex, grid.x, grid.y)[0]; // a sprite sheet shows its first leaf; the anchor applies to every cell
                float aspect = content.height / Mathf.Max(content.width, 1e-3f);
                _image.image = drawn;
                _image.uv = content;
                _image.style.width = aspect > 1f ? Box / aspect : Box;
                _image.style.height = aspect > 1f ? Box : Box * aspect;
            }
            Vector2 a = _anchor.vector2Value;
            _dot.style.left = a.x * _image.resolvedStyle.width - 6f;
            _dot.style.top = (1f - a.y) * _image.resolvedStyle.height - 6f;
        }
    }
}
