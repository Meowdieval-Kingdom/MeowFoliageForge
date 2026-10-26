using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meowdieval.FoliageForge
{
    sealed partial class TreePreview
    {
        // the single-blob viewer's meshes: the wood is a stub, so this is the leaves
        private void DrawBlobMeshes(Mesh mesh, Material[] materials)
        {
            switch (Display)
            {
                case BlobDisplay.Wireframe:
                    if (_wireMat != null) _pru.DrawMesh(mesh, Matrix4x4.identity, _wireMat, 0);
                    if (_leafWireMat != null && mesh.subMeshCount > 1) _pru.DrawMesh(mesh, Matrix4x4.identity, _leafWireMat, 1);
                    if (_leafWireMat != null && mesh.subMeshCount > 2) _pru.DrawMesh(mesh, Matrix4x4.identity, _leafWireMat, 2);
                    break;
                case BlobDisplay.Normals:
                    if (RefreshNormals(mesh) && _normalMat != null) _pru.DrawMesh(_normalMesh, Matrix4x4.identity, _normalMat, 0);
                    break;
                default:
                    for (int s = 0; s < mesh.subMeshCount && s < materials.Length; s++)
                        if (materials[s] != null) _pru.DrawMesh(mesh, Matrix4x4.identity, materials[s], s);
                    break;
            }
            // Sphere.fbx has radius 0.5
            if (ShowReference && _sphere != null && _referenceMat != null) _pru.DrawMesh(_sphere, Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * 2f), _referenceMat, 0);
        }

        // caches the mesh's vertices and normals, and a copy of its leaf submesh whose vertex colour is the normal; false when there are no leaves
        private bool RefreshNormals(Mesh mesh)
        {
            if (mesh.subMeshCount < 2) return false;
            if (_normalSource == mesh && _normalVersion == MeshVersion) return _nNormals.Count > 0;
            _normalSource = mesh;
            _normalVersion = MeshVersion;
            mesh.GetVertices(_nVerts);
            mesh.GetNormals(_nNormals);
            _normalMesh.Clear();
            if (_nNormals.Count != _nVerts.Count)
            {
                _nNormals.Clear();
                return false;
            }
            var colors = new List<Color>(_nNormals.Count);
            foreach (Vector3 n in _nNormals) colors.Add(new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
            _normalMesh.SetVertices(_nVerts);
            _normalMesh.SetColors(colors);
            _normalMesh.SetTriangles(mesh.GetTriangles(1), 0);
            return true;
        }

        // one short line per sampled vertex along its normal, at most MaxArrows of them, drawn over the preview
        private void DrawNormalArrows(Rect rect, Mesh mesh)
        {
            if (!RefreshNormals(mesh)) return;
            int step = Mathf.Max(1, _nVerts.Count / MaxArrows);
            Handles.BeginGUI();
            for (int i = 0; i < _nVerts.Count; i += step)
            {
                Vector2 a = ToScreen(rect, _nVerts[i]), b = ToScreen(rect, _nVerts[i] + _nNormals[i] * ArrowLength);
                Handles.color = new Color(_nNormals[i].x * 0.5f + 0.5f, _nNormals[i].y * 0.5f + 0.5f, _nNormals[i].z * 0.5f + 0.5f);
                Handles.DrawLine(new Vector3(a.x, a.y, 0f), new Vector3(b.x, b.y, 0f));
            }
            Handles.EndGUI();
        }
    }
}
