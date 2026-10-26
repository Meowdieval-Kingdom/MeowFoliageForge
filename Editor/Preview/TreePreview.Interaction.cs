using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meowdieval.FoliageForge
{
    sealed partial class TreePreview
    {
        private Vector2 ToScreen(Rect rect, Vector3 world)
        {
            Vector3 v = _pru.camera.WorldToViewportPoint(world);
            return new Vector2(rect.x + v.x * rect.width, rect.y + (1f - v.y) * rect.height);
        }

        // wireframe and points of the selected blob's cage, drawn over the preview
        private void DrawCageOverlay(Rect rect, CanopyBlob blob)
        {
            Handles.BeginGUI();
            Handles.color = new Color(1f, 0.85f, 0.35f, 0.9f);
            var screen = new Vector2[BlobCage.PointCount];
            for (int i = 0; i < screen.Length; i++) screen[i] = ToScreen(rect, BlobCage.World(blob, i));
            int[] tris = BlobCage.Triangles;
            for (int t = 0; t < tris.Length; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    Vector2 a = screen[tris[t + k]], b = screen[tris[t + (k + 1) % 3]];
                    Handles.DrawLine(new Vector3(a.x, a.y, 0f), new Vector3(b.x, b.y, 0f));
                }
            for (int i = 0; i < screen.Length; i++)
            {
                Handles.color = i == _dragPoint ? Color.white : new Color(1f, 0.6f, 0.1f);
                Handles.DrawSolidDisc(new Vector3(screen[i].x, screen[i].y, 0f), Vector3.forward, i == _dragPoint ? 5.5f : 4f);
            }
            Handles.EndGUI();
        }

        private Ray MouseRay(Rect rect, Vector2 mouse) =>
            _pru.camera.ViewportPointToRay(new Vector3((mouse.x - rect.x) / rect.width, 1f - (mouse.y - rect.y) / rect.height, 0f));

        // distance along the ray to the blob's ellipsoid, or -1
        private float Hit(Ray ray, CanopyBlob blob)
        {
            blob.Radii += Vector3.one * BlobPad;
            Quaternion inv = Quaternion.Inverse(BlobCage.Rot(blob));
            Vector3 o = Vector3.Scale(inv * (ray.origin - blob.Position), Inverse(blob.Radii));
            Vector3 d = Vector3.Scale(inv * ray.direction, Inverse(blob.Radii));
            float a = Vector3.Dot(d, d), bq = Vector3.Dot(o, d), c = Vector3.Dot(o, o) - 1f;
            float disc = bq * bq - a * c;
            if (disc < 0f || a < 1e-9f) return -1f;
            float t = (-bq - Mathf.Sqrt(disc)) / a;
            return t > 0f ? t : (-bq + Mathf.Sqrt(disc)) / a;
        }

        private static Vector3 Inverse(Vector3 v) => new Vector3(1f / Mathf.Max(v.x, 1e-3f), 1f / Mathf.Max(v.y, 1e-3f), 1f / Mathf.Max(v.z, 1e-3f));

        // point where the ray meets the plane through `point` facing the camera
        private Vector3 PlaneHit(Ray ray, Vector3 point)
        {
            var plane = new Plane(-_pru.camera.transform.forward, point);
            return plane.Raycast(ray, out float t) ? ray.GetPoint(t) : point;
        }

        private void HandleInput(Rect rect, List<CanopyBlob> blobs)
        {
            Event e = Event.current;
            bool editing = Mode == PreviewMode.Blobs && blobs != null;
            if (e.type == EventType.MouseUp && (_dragging || _dragPoint >= 0))
            {
                _dragging = false;
                _dragPoint = -1;
                e.Use();
                return;
            }
            if (!rect.Contains(e.mousePosition) || IgnoreRect.Contains(e.mousePosition)) return;

            if (editing)
            {
                switch (e.type)
                {
                    case EventType.MouseDown when e.button == 0:
                    {
                        Ray ray = MouseRay(rect, e.mousePosition);
                        if (Selected >= 0 && Selected < blobs.Count)
                        {
                            // a cage point of the selected blob wins over the blob body
                            int near = -1;
                            float nearDist = 10f;
                            for (int i = 0; i < BlobCage.PointCount; i++)
                            {
                                float d = Vector2.Distance(ToScreen(rect, BlobCage.World(blobs[Selected], i)), e.mousePosition);
                                if (d < nearDist)
                                {
                                    nearDist = d;
                                    near = i;
                                }
                            }
                            if (near >= 0)
                            {
                                _dragPoint = near;
                                Vector3 at = BlobCage.World(blobs[Selected], near);
                                _dragOffset = at - PlaneHit(ray, at);
                                BeforeBlobEdit?.Invoke();
                                e.Use();
                                return;
                            }
                        }
                        int hit = -1;
                        float best = float.MaxValue;
                        for (int i = 0; i < blobs.Count; i++)
                        {
                            float t = Hit(ray, blobs[i]);
                            if (t > 0f && t < best)
                            {
                                best = t;
                                hit = i;
                            }
                        }
                        Selected = hit;
                        if (hit >= 0)
                        {
                            _dragging = true;
                            _dragOffset = blobs[hit].Position - PlaneHit(ray, blobs[hit].Position);
                            BeforeBlobEdit?.Invoke();
                            e.Use();
                            return;
                        }
                        break;
                    }
                    case EventType.MouseDrag when _dragPoint >= 0 && Selected >= 0 && Selected < blobs.Count:
                    {
                        // move the point in the camera plane; Shift drags its neighbours half as far
                        CanopyBlob blob = BlobCage.EnsureDeform(blobs[Selected]);
                        Vector3 oldWorld = BlobCage.World(blob, _dragPoint);
                        Vector3 target = PlaneHit(MouseRay(rect, e.mousePosition), oldWorld) + _dragOffset;
                        Vector3 delta = target - oldWorld;
                        Vector3 local = Quaternion.Inverse(BlobCage.Rot(blob)) * delta; // Deform lives in the blob's own frame
                        blob.Deform[_dragPoint] += local;
                        if (e.shift)
                            foreach (int n in BlobCage.NeighboursOf(_dragPoint)) blob.Deform[n] += local * 0.5f;
                        blobs[Selected] = blob;
                        AfterBlobEdit?.Invoke();
                        e.Use();
                        return;
                    }
                    case EventType.MouseDrag when _dragging && Selected >= 0 && Selected < blobs.Count:
                    {
                        CanopyBlob blob = blobs[Selected];
                        blob.Position = PlaneHit(MouseRay(rect, e.mousePosition), blob.Position) + _dragOffset;
                        blobs[Selected] = blob;
                        AfterBlobEdit?.Invoke();
                        e.Use();
                        return;
                    }
                    case EventType.ScrollWheel when Selected >= 0 && Selected < blobs.Count:
                    {
                        BeforeBlobEdit?.Invoke();
                        CanopyBlob blob = blobs[Selected];
                        if (e.alt)
                        {
                            blob.Rotation.y = Mathf.Repeat(blob.Rotation.y + e.delta.y * 4f, 360f); // Alt + scroll turns the blob
                            blobs[Selected] = blob;
                            AfterBlobEdit?.Invoke();
                            e.Use();
                            return;
                        }
                        float f = Mathf.Clamp(1f - e.delta.y * 0.03f, 0.5f, 2f);
                        Vector3 scale = e.control ? new Vector3(1f, f, 1f) : Vector3.one * f;
                        blob.Radii = Vector3.Max(Vector3.Scale(blob.Radii, scale), Vector3.one * 0.1f);
                        if (BlobCage.IsDeformed(blob))
                        {
                            blob.Deform = (Vector3[])blob.Deform.Clone();
                            for (int i = 0; i < blob.Deform.Length; i++) blob.Deform[i] = Vector3.Scale(blob.Deform[i], scale);
                        }
                        blobs[Selected] = blob;
                        AfterBlobEdit?.Invoke();
                        e.Use();
                        return;
                    }
                    case EventType.KeyDown when e.keyCode == KeyCode.Delete && Selected >= 0 && Selected < blobs.Count:
                        BeforeBlobEdit?.Invoke();
                        blobs.RemoveAt(Selected);
                        Selected = -1;
                        AfterBlobEdit?.Invoke();
                        e.Use();
                        return;
                }
            }

            switch (e.type)
            {
                case EventType.MouseDrag when e.button == 0:
                    _yaw += e.delta.x * 0.5f;
                    _pitch = Mathf.Clamp(_pitch + e.delta.y * 0.5f, -10f, 89f);
                    e.Use();
                    break;
                case EventType.ScrollWheel:
                    _zoom = Mathf.Clamp(_zoom * (1f - e.delta.y * 0.03f), 0.3f, 4f);
                    e.Use();
                    break;
                case EventType.MouseDown when e.clickCount == 2:
                    _yaw = DefaultYaw;
                    _pitch = DefaultPitch;
                    _zoom = 1f;
                    e.Use();
                    break;
            }
        }
    }
}
