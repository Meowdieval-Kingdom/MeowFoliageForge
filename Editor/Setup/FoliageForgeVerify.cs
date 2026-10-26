using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Meowdieval.FoliageForge
{
    /// <summary>
    /// Render check for the editor: bakes every preset, renders a showcase and a 400-tree forest with wind,
    /// times it, and screenshots the window. Run Verify.Run() from code, or drop
    /// Temp/FoliageForgeVerify.trigger into the project to run it after the next compile. Output goes to Library/FoliageForgeVerify.
    /// </summary>
    public static class FoliageForgeVerify
    {
        const string Folder = "Assets/__FoliageForgeVerify";
        static string _savedOutput;
        static string OutDir => Path.Combine(Directory.GetCurrentDirectory(), "Library", "FoliageForgeVerify");
        static string Trigger => Path.Combine(Directory.GetCurrentDirectory(), "Temp", "FoliageForgeVerify.trigger");

        static bool _asyncShaders;
        static int _frames;
        static FoliageForgeWindow _window;
        static List<GameObject> _prefabs;
        static Texture2D _windA;

        [InitializeOnLoadMethod]
        private static void AutoRun()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(Trigger)) return;
                File.Delete(Trigger);
                Run();
            };
        }

        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            _asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FoliageForgeVerify");
                _savedOutput = TreeBaker.OutputFolder;
                TreeBaker.OutputFolder = Folder + "/Out";
                var prefabs = new List<GameObject>();
                TreeRecipe oak = null;
                foreach (TreePreset p in System.Enum.GetValues(typeof(TreePreset)))
                {
                    var r = ScriptableObject.CreateInstance<TreeRecipe>();
                    r.ApplyPreset(p);
                    r.Look.StylizedShading = true; // screenshots show the stylized look: shadow tone, rim, patches
                    AssetDatabase.CreateAsset(r, $"{Folder}/{p}.asset");
                    prefabs.Add(TreeBaker.Bake(r));
                    if (p == TreePreset.Oak) oak = r;
                }

                // extras: billboards, and moss + strong jitter on the oak
                foreach (var extra in new[] { ("Oak_Billboard", BillboardMode.ViewAligned, 0f), ("Oak_Moss", BillboardMode.Off, 0.9f) })
                {
                    var r = ScriptableObject.CreateInstance<TreeRecipe>();
                    r.ApplyPreset(TreePreset.Oak);
                    r.Leaves.Billboard = extra.Item2;
                    r.Look.MossAmount = extra.Item3;
                    r.Look.GradientJitter = 0.6f;
                    AssetDatabase.CreateAsset(r, $"{Folder}/{extra.Item1}.asset");
                    prefabs.Add(TreeBaker.Bake(r));
                }

                var pru = new PreviewRenderUtility();
                try
                {
                    _prefabs = prefabs;
                    Showcase(pru, prefabs);
                    Forest(pru, prefabs);
                }
                finally
                {
                    pru.Cleanup();
                }
                FoliageWind.SetGlobals(0f, 0f, 1f, 0.3f, 0.5f);

                _window = EditorWindow.GetWindow<FoliageForgeWindow>();
                _window.position = new Rect(80, 80, 1280, 800);
                FoliageForgeWindow.Open(oak);
                _frames = 0;
                EditorApplication.update += Tick;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[FF verify] failed: " + e);
                Cleanup();
            }
        }

        private static void Tick()
        {
            _frames++;
            if (_frames < 120)
            {
                _window.Repaint();
                return;
            }
            if (_frames == 120)
            {
                Capture("window");
                _windA = WindFrame();
            }
            if (_frames == 160)
            {
                Texture2D b = WindFrame();
                Color32[] pa = _windA.GetPixels32(), pb = b.GetPixels32();
                int diff = 0;
                for (int i = 0; i < pa.Length; i++) if (pa[i].r != pb[i].r || pa[i].g != pb[i].g || pa[i].b != pb[i].b) diff++;
                Debug.Log($"[FF verify] wind: {diff} of {pa.Length} pixels changed between two editor frames");
                Save(_windA, "wind_a");
                Save(b, "wind_b");
                _window.position = new Rect(80, 80, 780, 480);
            }
            if (_frames < 230)
            {
                _window.Repaint();
                return;
            }
            EditorApplication.update -= Tick;
            Capture("window_small");
            Cleanup();
        }

        private static Texture2D WindFrame()
        {
            var pru = new PreviewRenderUtility();
            try
            {
                FoliageWind.SetGlobals(30f, 1f, 1f, 0.3f, 0.5f);
                Setup(pru, new Vector3(0, 2, 0), 4f);
                pru.BeginStaticPreview(new Rect(0, 0, 640, 360));
                Ground(pru, 40f);
                Draw(pru, _prefabs[0], Matrix4x4.identity);
                pru.Render(true);
                return pru.EndStaticPreview();
            }
            finally
            {
                pru.Cleanup();
            }
        }

        private static void Capture(string name)
        {
            Rect r = _window.position;
            float ppp = EditorGUIUtility.pixelsPerPoint;
            int w = (int)(r.width * ppp), h = (int)((r.height + 48f) * ppp);
            Color[] px = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(new Vector2(r.x * ppp, r.y * ppp), w, h);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            Save(tex, name);
        }

        private static void Cleanup()
        {
            ShaderUtil.allowAsyncCompilation = _asyncShaders;
            if (_savedOutput != null) TreeBaker.OutputFolder = _savedOutput;
            AssetDatabase.DeleteAsset(Folder);
            Debug.Log("[FF verify] done, output in " + OutDir);
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-ffExit") >= 0) EditorApplication.Exit(0);
        }

        private static void Setup(PreviewRenderUtility pru, Vector3 target, float size)
        {
            Camera cam = pru.camera;
            cam.orthographic = true;
            cam.orthographicSize = size;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.7f, 0.85f);
            cam.transform.rotation = Quaternion.Euler(32f, 35f, 0f);
            cam.transform.position = target - cam.transform.forward * 100f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;
            pru.lights[0].intensity = 1.3f;
            pru.lights[0].transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            pru.ambientColor = new Color(0.35f, 0.4f, 0.45f);
        }

        private static void Draw(PreviewRenderUtility pru, GameObject prefab, Matrix4x4 m)
        {
            Mesh mesh = prefab.GetComponent<MeshFilter>().sharedMesh;
            Material[] mats = prefab.GetComponent<MeshRenderer>().sharedMaterials;
            for (int s = 0; s < mesh.subMeshCount; s++) pru.DrawMesh(mesh, m, mats[s], s);
        }

        private static void Ground(PreviewRenderUtility pru, float extent)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(lit) { hideFlags = HideFlags.HideAndDontSave };
            mat.SetColor("_BaseColor", new Color(0.32f, 0.45f, 0.28f));
            pru.DrawMesh(Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"), Matrix4x4.TRS(new Vector3(0, -0.02f, 0), Quaternion.identity, new Vector3(extent, 0.02f, extent)), mat, 0);
        }

        private static void Save(Texture2D t, string name)
        {
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), t.EncodeToPNG());
            Object.DestroyImmediate(t);
            Debug.Log("[FF verify] saved " + name);
        }

        private static void Showcase(PreviewRenderUtility pru, List<GameObject> prefabs)
        {
            foreach (float strength in new[] { 0f, 1f })
            {
                FoliageWind.SetGlobals(30f, strength, 1f, 0.3f, 0.5f);
                Setup(pru, new Vector3(0, 2, 0), 12f);
                pru.BeginStaticPreview(new Rect(0, 0, 1280, 720));
                Ground(pru, 40f);
                for (int i = 0; i < prefabs.Count; i++)
                    Draw(pru, prefabs[i], Matrix4x4.Translate(new Vector3((i % 7 - 3f) * 5f, 0, -(i / 7) * 6f + 3f)));
                pru.Render(true);
                Save(pru.EndStaticPreview(), strength > 0f ? "showcase_wind" : "showcase_calm");
            }

            // sun behind the trees, facing the camera: translucency
            FoliageWind.SetGlobals(30f, 0f, 1f, 0.3f, 0.5f);
            Setup(pru, new Vector3(0, 2, 0), 12f);
            pru.lights[0].transform.rotation = Quaternion.Euler(8f, 215f, 0f);
            pru.BeginStaticPreview(new Rect(0, 0, 1280, 720));
            Ground(pru, 40f);
            for (int i = 0; i < prefabs.Count; i++)
                Draw(pru, prefabs[i], Matrix4x4.Translate(new Vector3((i % 7 - 3f) * 5f, 0, -(i / 7) * 6f + 3f)));
            pru.Render(true);
            Save(pru.EndStaticPreview(), "showcase_backlit");
        }

        private static void Forest(PreviewRenderUtility pru, List<GameObject> prefabs)
        {
            const int n = 20;
            FoliageWind.SetGlobals(30f, 1f, 1f, 0.3f, 0.5f);
            Setup(pru, Vector3.zero, 48f);
            var sw = new Stopwatch();
            Texture2D last = null;
            for (int run = 0; run < 12; run++)
            {
                if (run == 2) sw.Start();
                pru.BeginStaticPreview(new Rect(0, 0, 1280, 720));
                Ground(pru, 120f);
                for (int x = 0; x < n; x++)
                    for (int z = 0; z < n; z++)
                        Draw(pru, prefabs[(x * 7 + z * 3) % 4], Matrix4x4.TRS(new Vector3((x - n / 2) * 4.5f, 0, (z - n / 2) * 4.5f), Quaternion.Euler(0, (x * 37 + z * 91) % 360, 0), Vector3.one));
                pru.Render(true);
                if (last != null) Object.DestroyImmediate(last);
                last = pru.EndStaticPreview();
            }
            sw.Stop();
            Debug.Log($"[FF verify] forest {n * n} trees: {sw.Elapsed.TotalMilliseconds / 10:0.0} ms per frame (CPU submit + readback, 10 frames)");
            Save(last, "forest");
        }
    }
}
