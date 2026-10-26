using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meowdieval.FoliageForge
{
    sealed partial class TreePreview
    {
        // the finished tree in the bottom right corner, same camera, so the blobs can be judged against the real result
        private void DrawInset(Rect rect, Mesh mesh, Material[] materials)
        {
            float size = Mathf.Clamp(Mathf.Min(rect.width, rect.height) * 0.45f, 150f, 330f);
            var inset = new Rect(rect.xMax - size - 8f, rect.yMax - size - 8f, size, size);
            if (inset.width > rect.width * 0.5f || inset.height > rect.height * 0.5f) return;
            _pru.BeginPreview(inset, GUIStyle.none);
            if (ShowGround && _groundMat != null)
            {
                float d = Mathf.Max(0.5f, mesh.bounds.extents.magnitude) * 1.7f;
                _pru.DrawMesh(_ground, Matrix4x4.TRS(new Vector3(0f, -0.02f, 0f), Quaternion.identity, new Vector3(d, 0.02f, d)), _groundMat, 0);
            }
            for (int s = 0; s < mesh.subMeshCount && s < materials.Length; s++)
                if (materials[s] != null) _pru.DrawMesh(mesh, Matrix4x4.identity, materials[s], s);
            _pru.Render(true);
            GUI.DrawTexture(inset, _pru.EndPreview(), ScaleMode.StretchToFill, false);
            EditorGUI.DrawRect(new Rect(inset.x - 1f, inset.y - 1f, inset.width + 2f, 1f), Color.black);
            EditorGUI.DrawRect(new Rect(inset.x - 1f, inset.yMax, inset.width + 2f, 1f), Color.black);
            EditorGUI.DrawRect(new Rect(inset.x - 1f, inset.y, 1f, inset.height), Color.black);
            EditorGUI.DrawRect(new Rect(inset.xMax, inset.y, 1f, inset.height), Color.black);
        }

        /// <summary>Orbit view of a flat grass patch centred on the origin.</summary>
        public void DrawGrass(Rect rect, GrassPatch patch)
        {
            _lastRect = rect;
            HandleInput(rect, null);
            if (Event.current.type != EventType.Repaint) return;

            float half = patch.Size * 0.5f;
            Camera cam = _pru.camera;
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            _pru.lights[0].transform.rotation = Backlit ? Quaternion.Euler(8f, _yaw + 180f, 0f) : Quaternion.Euler(50f, -30f, 0f);
            Vector3 centre = new Vector3(0f, 0.3f, 0f);
            float radius = half * 1.2f;
            cam.orthographic = Orthographic;
            cam.transform.rotation = rot;
            if (Orthographic)
            {
                cam.orthographicSize = radius * 0.8f / _zoom;
                cam.transform.position = centre - rot * Vector3.forward * (radius * 4f);
            }
            else
            {
                cam.fieldOfView = 30f;
                cam.transform.position = centre - rot * Vector3.forward * (radius * 2.2f / _zoom);
            }

            _pru.BeginPreview(rect, GUIStyle.none);
            if (ShowGround && _groundMat != null)
                _pru.DrawMesh(_ground, Matrix4x4.TRS(new Vector3(0f, -0.02f, 0f), Quaternion.identity, new Vector3(patch.Size * 1.1f, 0.02f, patch.Size * 1.1f)), _groundMat, 0);
            if (ShowScale && _scaleMat != null)
            {
                float h = ScaleHeight, w = h * 0.28f;
                _pru.DrawMesh(_capsule, Matrix4x4.TRS(new Vector3(0f, h * 0.5f, 0f), Quaternion.identity, new Vector3(w, h * 0.5f, w)), _scaleMat, 0);
            }
            for (int v = 0; v < patch.Meshes.Length; v++)
            {
                if (patch.Meshes[v] == null || patch.Materials[v] == null || patch.Clumps[v] == null) continue;
                foreach (Matrix4x4 m in patch.Clumps[v]) _pru.DrawMesh(patch.Meshes[v], m, patch.Materials[v], 0);
            }
            _pru.Render(true);
            GUI.DrawTexture(rect, _pru.EndPreview(), ScaleMode.StretchToFill, false);
        }
    }
}
