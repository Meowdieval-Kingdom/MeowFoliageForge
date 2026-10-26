using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meowdieval.FoliageForge
{

    /// <summary>
    /// Orbit preview of a tree mesh. Drag to rotate, scroll to zoom, double-click to reset.
    /// In Blobs mode a drag on a blob moves it, scroll resizes the selected blob (Ctrl: height only), Delete removes it.
    /// </summary>
    sealed partial class TreePreview : IDisposable
    {
        const float DefaultYaw = 30f, DefaultPitch = 28f;

        readonly PreviewRenderUtility _pru = new PreviewRenderUtility();
        Mesh _ground, _sphere, _capsule;
        Material _scaleMat, _groundMat, _wireMat, _leafWireMat, _blobMat, _blobSelectedMat, _referenceMat, _normalMat;
        float _yaw = DefaultYaw, _pitch = DefaultPitch, _zoom = 1f;
        /// <summary>How far the leaf cards reach past the blob volume their centres fill (half a card plus the normal offset); the blob shells are drawn this much larger.</summary>
        public float BlobPad;
        bool _dragging;
        int _dragPoint = -1;
        Vector3 _dragOffset;
        readonly Mesh _cage = new Mesh { hideFlags = HideFlags.HideAndDontSave };
        Rect _lastRect;
        List<CanopyBlob> _lastBlobs;

        // Blob mode: the leaf mesh recoloured by normal (normal * 0.5 + 0.5), and the vertex data the normal arrows sample
        readonly Mesh _normalMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
        readonly List<Vector3> _nVerts = new List<Vector3>(), _nNormals = new List<Vector3>();
        Mesh _normalSource;
        int _normalVersion = -1;
        const int MaxArrows = 300;
        const float ArrowLength = 0.15f;

        public bool Orthographic = true;
        public bool ShowGround = true;
        /// <summary>Draws the leaves in the Render and Wireframe views; off leaves just the wood.</summary>
        public bool ShowFoliage = true;
        /// <summary>Draws a capsule next to the tree to judge its size.</summary>
        public bool ShowScale = true;
        /// <summary>Height of the size reference capsule, in units.</summary>
        public float ScaleHeight = 1.8f;
        /// <summary>Puts a low sun behind the tree as seen from the camera, so translucency shows.</summary>
        public bool Backlit;
        public PreviewMode Mode = PreviewMode.Finished;
        /// <summary>Variants mode: the meshes to draw and where each stands. Null or empty draws the single mesh like Render.</summary>
        public Mesh[] VariantMeshes;
        public Vector3[] VariantOffsets;
        /// <summary>Blob mode: how the leaves are drawn.</summary>
        public BlobDisplay Display = BlobDisplay.Shaded;
        /// <summary>Blob mode overlays: the blob's cage, a radius 1 reference sphere, and per-vertex normal arrows (always on in the Normals display).</summary>
        public bool ShowCage, ShowReference, ShowNormalArrows;
        /// <summary>Blob mode: bump when the mesh was rebuilt, so the cached normal colours are redone.</summary>
        public int MeshVersion;
        /// <summary>Mouse input inside this rect (an overlaid toolbar) does not orbit.</summary>
        public Rect IgnoreRect;
        /// <summary>Index of the blob being edited, or -1.</summary>
        public int Selected = -1;
        /// <summary>Called before a blob is changed, so the owner can record undo.</summary>
        public Action BeforeBlobEdit;
        /// <summary>Called after a blob changed.</summary>
        public Action AfterBlobEdit;

        public TreePreview()
        {
            Camera cam = _pru.camera;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.17f, 0.2f, 0.19f);
            _pru.lights[0].intensity = 1.25f;
            _pru.lights[0].transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            _pru.lights[1].intensity = 0.35f;
            _pru.ambientColor = new Color(0.32f, 0.36f, 0.38f);

            _ground = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            _sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null)
            {
                _groundMat = new Material(lit) { hideFlags = HideFlags.HideAndDontSave };
                _groundMat.SetColor("_BaseColor", new Color(0.3f, 0.4f, 0.28f));
            }
            _wireMat = Flat(new Color(0.75f, 0.9f, 0.7f), false);
            _leafWireMat = Flat(new Color(0.45f, 0.85f, 0.35f), false);
            _scaleMat = Flat(new Color(0.95f, 0.55f, 0.2f), false);
            _capsule = Resources.GetBuiltinResource<Mesh>("Capsule.fbx");
            _blobMat = Flat(new Color(0.35f, 0.8f, 0.4f, 0.28f), true);
            _blobSelectedMat = Flat(new Color(1f, 0.75f, 0.2f, 0.45f), true);
            _referenceMat = Flat(new Color(0.75f, 0.8f, 0.95f, 0.1f), true);
            _normalMat = Flat(Color.white, false);
        }

        // unlit colour (optionally see-through), drawn from both sides
        private static Material Flat(Color color, bool transparent)
        {
            Shader sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null) return null;
            var m = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            m.SetInt("_Cull", 0);
            if (transparent)
            {
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.renderQueue = 3000;
            }
            return m;
        }

        public void Draw(Rect rect, Mesh mesh, Material[] materials, List<CanopyBlob> blobs)
        {
            _lastRect = rect;
            _lastBlobs = blobs;
            HandleInput(rect, blobs);
            if (Event.current.type != EventType.Repaint || mesh == null) return;

            bool row = Mode == PreviewMode.Variants && VariantMeshes != null && VariantMeshes.Length > 0 && VariantOffsets != null && VariantOffsets.Length == VariantMeshes.Length;
            Bounds b = mesh.bounds;
            if (row)
            {
                b = new Bounds(VariantMeshes[0].bounds.center + VariantOffsets[0], VariantMeshes[0].bounds.size);
                for (int v = 1; v < VariantMeshes.Length; v++) b.Encapsulate(new Bounds(VariantMeshes[v].bounds.center + VariantOffsets[v], VariantMeshes[v].bounds.size));
            }
            float radius = Mathf.Max(0.5f, b.extents.magnitude);
            Camera cam = _pru.camera;
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            _pru.lights[0].transform.rotation = Backlit ? Quaternion.Euler(8f, _yaw + 180f, 0f) : Quaternion.Euler(50f, -30f, 0f);
            cam.orthographic = Orthographic;
            cam.transform.rotation = rot;
            if (Orthographic)
            {
                cam.orthographicSize = radius * 1.05f / _zoom;
                cam.transform.position = b.center - rot * Vector3.forward * (radius * 4f);
            }
            else
            {
                cam.fieldOfView = 30f;
                cam.transform.position = b.center - rot * Vector3.forward * (radius * 2.6f / _zoom);
            }

            _pru.BeginPreview(rect, GUIStyle.none);
            bool blobView = Mode == PreviewMode.Blob;
            bool wire = Mode == PreviewMode.Wireframe || (blobView && Display == BlobDisplay.Wireframe);
            if (ShowGround && !wire && !blobView && _groundMat != null)
            {
                float d = radius * 1.7f;
                _pru.DrawMesh(_ground, Matrix4x4.TRS(new Vector3(0f, -0.02f, 0f), Quaternion.identity, new Vector3(d, 0.02f, d)), _groundMat, 0);
            }
            if (ShowScale && !blobView && _scaleMat != null)
            {
                // standing just outside the crown on its right, about as wide as a person is tall over 3.6
                float h = ScaleHeight, w = h * 0.28f;
                Vector3 at = new Vector3(b.max.x + w + 0.3f, h * 0.5f, b.center.z);
                _pru.DrawMesh(_capsule, Matrix4x4.TRS(at, Quaternion.identity, new Vector3(w, h * 0.5f, w)), _scaleMat, 0);
            }
            if (Mode == PreviewMode.Finished || Mode == PreviewMode.Variants)
            {
                for (int v = 0; v < (row ? VariantMeshes.Length : 1); v++)
                {
                    Mesh m = row ? VariantMeshes[v] : mesh;
                    Matrix4x4 at = row ? Matrix4x4.Translate(VariantOffsets[v]) : Matrix4x4.identity;
                    for (int s = 0; s < m.subMeshCount && s < materials.Length; s++)
                        if (materials[s] != null && (ShowFoliage || s == 0)) _pru.DrawMesh(m, at, materials[s], s);
                }
            }
            else if (blobView) DrawBlobMeshes(mesh, materials);
            else
            {
                // submesh 0 is the wood; leaves are left out of Blobs so the skeleton and blobs read clearly
                Material wood = wire ? _wireMat : materials.Length > 0 ? materials[0] : null;
                if (wood != null) _pru.DrawMesh(mesh, Matrix4x4.identity, wood, 0);
                // the wireframe shows the leaf cards too, so the quad layout is visible
                if (wire && ShowFoliage && _leafWireMat != null && mesh.subMeshCount > 1) _pru.DrawMesh(mesh, Matrix4x4.identity, _leafWireMat, 1);
                // the inner core shell (submesh 2, when Look.CoreMesh is on) shows with the blobs it sits in
                if (!wire && mesh.subMeshCount > 2 && materials.Length > 2 && materials[2] != null) _pru.DrawMesh(mesh, Matrix4x4.identity, materials[2], 2);
                if (!wire && blobs != null)
                    for (int i = 0; i < blobs.Count; i++)
                    {
                        Material m = i == Selected ? _blobSelectedMat : _blobMat;
                        if (m == null) continue;
                        if (i == Selected || BlobCage.IsDeformed(blobs[i]) || BlobCage.HasShape(blobs[i]))
                        {
                            // a deformed, shaped or selected blob shows its cage surface
                            BlobCage.BuildMesh(blobs[i], _cage, BlobPad);
                            _pru.DrawMesh(_cage, Matrix4x4.identity, m, 0);
                        }
                        else if (_sphere != null) _pru.DrawMesh(_sphere, Matrix4x4.TRS(blobs[i].Position, BlobCage.Rot(blobs[i]), (blobs[i].Radii + Vector3.one * BlobPad) * 2f), m, 0);
                    }
            }
            GL.wireframe = wire;
            _pru.Render(true);
            GL.wireframe = false;
            Texture tex = _pru.EndPreview();
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
            if (blobView)
            {
                if (ShowCage && blobs != null && blobs.Count > 0) DrawCageOverlay(rect, blobs[0]);
                if (ShowNormalArrows || Display == BlobDisplay.Normals) DrawNormalArrows(rect, mesh);
            }
            if (Mode == PreviewMode.Blobs && blobs != null && Selected >= 0 && Selected < blobs.Count) DrawCageOverlay(rect, blobs[Selected]);
            if (Mode == PreviewMode.Blobs) DrawInset(rect, mesh, materials);
        }

        public void Dispose()
        {
            _pru.Cleanup();
            UnityEngine.Object.DestroyImmediate(_cage);
            UnityEngine.Object.DestroyImmediate(_normalMesh);
            foreach (Material m in new[] { _scaleMat, _groundMat, _wireMat, _leafWireMat, _blobMat, _blobSelectedMat, _referenceMat, _normalMat })
                if (m != null) UnityEngine.Object.DestroyImmediate(m);
        }
    }
}
