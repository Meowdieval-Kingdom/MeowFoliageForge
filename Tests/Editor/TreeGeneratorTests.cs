using NUnit.Framework;
using UnityEngine;

namespace Meowdieval.FoliageForge.Tests
{
    public class TreeGeneratorTests
    {
        TreeRecipe _recipe;

        [SetUp]
        public void SetUp()
        {
            _recipe = ScriptableObject.CreateInstance<TreeRecipe>();
            _recipe.ApplyPreset(TreePreset.Oak);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_recipe);

        [Test]
        public void Build_SameSeed_ProducesIdenticalMesh()
        {
            var a = new Mesh();
            var b = new Mesh();
            TreeGenerator.Build(_recipe, a);
            TreeGenerator.Build(_recipe, b);
            CollectionAssert.AreEqual(a.vertices, b.vertices);
            CollectionAssert.AreEqual(a.triangles, b.triangles);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        [Test]
        public void Build_DifferentSeed_ChangesMesh()
        {
            var a = new Mesh();
            var b = new Mesh();
            TreeGenerator.Build(_recipe, a);
            _recipe.Seed += 1;
            TreeGenerator.Build(_recipe, b);
            CollectionAssert.AreNotEqual(a.vertices, b.vertices);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        [Test]
        public void Build_CardBudget_IsNeverExceeded()
        {
            _recipe.Leaves.CardsPerBranch = 60;
            _recipe.Budget.MaxCards = 100;
            var mesh = new Mesh();
            TreeStats stats = TreeGenerator.Build(_recipe, mesh);
            Assert.LessOrEqual(stats.Cards, 100);
            Assert.Greater(stats.CardsDropped, 0);
            Object.DestroyImmediate(mesh);
        }

        [TestCase(TreePreset.Oak)]
        [TestCase(TreePreset.Spruce)]
        [TestCase(TreePreset.Maple)]
        [TestCase(TreePreset.Dead)]
        public void Build_EveryPreset_ProducesValidTwoSubmeshMesh(TreePreset preset)
        {
            _recipe.ApplyPreset(preset);
            var mesh = new Mesh();
            TreeStats stats = TreeGenerator.Build(_recipe, mesh);
            Assert.AreEqual(2, mesh.subMeshCount);
            Assert.Greater(stats.Triangles, 0);
            foreach (Vector3 v in mesh.vertices) Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z));
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Build_VertexColorHeight_SpansZeroToOne()
        {
            var mesh = new Mesh();
            TreeGenerator.Build(_recipe, mesh);
            Color32[] colors = mesh.colors32;
            byte min = 255, max = 0;
            foreach (Color32 c in colors)
            {
                if (c.r < min) min = c.r;
                if (c.r > max) max = c.r;
            }
            Assert.AreEqual(0, min);
            Assert.AreEqual(255, max);
            Object.DestroyImmediate(mesh);
        }
    
        [Test]
        public void TriangleBudget_IsRespectedWhenFitting()
        {
            var bare = new Mesh();
            int wood = TreeGenerator.Build(_recipe, bare).Triangles - bare.GetTriangles(1).Length / 3; // only cards are trimmed, so budgets sit above the wood
            Object.DestroyImmediate(bare);
            foreach (int budget in new[] { wood + 300, wood + 500, wood + 700 })
            {
                _recipe.Budget.MaxTriangles = budget;
                _recipe.Budget.FitCardsToTriangles = true;
                var mesh = new Mesh();
                TreeStats stats = TreeGenerator.Build(_recipe, mesh);
                Assert.LessOrEqual(stats.Triangles, budget, "over budget at " + budget);
                Assert.Greater(stats.CardsDropped, 0, "Oak should need trimming at " + budget);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void SwayWeight_IsOneFunctionOfHeight()
        {
            var mesh = new Mesh();
            TreeGenerator.Build(_recipe, mesh);
            Color32[] colors = mesh.colors32;
            foreach (Color32 c in colors)
            {
                int expected = Mathf.RoundToInt(Mathf.Pow(c.r / 255f, 1.6f) * 255f);
                Assert.LessOrEqual(Mathf.Abs(c.g - expected), 3, "branch and trunk must share one sway curve or branches detach");
            }
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Renormalize_ChangesNormalsAndBakesAo_WithoutChangingTopology()
        {
            _recipe.Leaves.Layout = LeafLayout.Clusters; // blob groups take their normals from the blob surface and skip Renormalize
            _recipe.Renormalize.Enabled = false;
            var off = new Mesh();
            TreeStats a = TreeGenerator.Build(_recipe, off);
            _recipe.Renormalize.Enabled = true;
            var on = new Mesh();
            TreeStats b = TreeGenerator.Build(_recipe, on);
            Assert.AreEqual(a.Vertices, b.Vertices);
            Assert.AreEqual(a.Triangles, b.Triangles);

            Color32[] cols = on.colors32;
            Vector3[] nor = on.normals;
            byte minAo = 255;
            int leaves = 0;
            for (int i = 0; i < cols.Length; i++)
            {
                Assert.AreEqual(1f, nor[i].magnitude, 0.01f, "normals must stay unit length");
                if (cols[i].b == 0) continue;
                leaves++;
                Assert.GreaterOrEqual(cols[i].b, 128, "leaf flag must stay above 0.5");
                minAo = (byte)Mathf.Min(minAo, cols[i].b);
            }
            Assert.Greater(leaves, 0);
            Assert.Less(minAo, 255, "some leaves must be occluded");
            Object.DestroyImmediate(off);
            Object.DestroyImmediate(on);
        }

        [Test]
        public void Billboard_WritesUnitAxesOnLeavesOnly_AndOffWritesNone()
        {
            _recipe.Leaves.Billboard = BillboardMode.Off;
            var off = new Mesh();
            TreeGenerator.Build(_recipe, off);
            var none = new System.Collections.Generic.List<Vector4>();
            off.GetUVs(3, none);
            Assert.AreEqual(0, none.Count, "no billboard data when Off");

            _recipe.Leaves.Billboard = BillboardMode.ViewAligned;
            var on = new Mesh();
            TreeStats stats = TreeGenerator.Build(_recipe, on);
            var right = new System.Collections.Generic.List<Vector4>();
            var up = new System.Collections.Generic.List<Vector4>();
            on.GetUVs(2, right);
            on.GetUVs(3, up);
            Color32[] cols = on.colors32;
            Assert.AreEqual(on.vertexCount, right.Count);
            Assert.AreEqual(on.vertexCount, up.Count);
            int leaves = 0;
            for (int i = 0; i < cols.Length; i++)
            {
                bool leaf = cols[i].b > 127;
                if (leaf)
                {
                    leaves++;
                    Assert.AreEqual(1f, ((Vector3)right[i]).magnitude, 0.01f);
                    Assert.AreEqual(1f, ((Vector3)up[i]).magnitude, 0.01f);
                }
                else Assert.AreEqual(0f, ((Vector3)right[i]).sqrMagnitude, 1e-6f, "wood must carry zero axes");
            }
            Assert.Greater(leaves, 0);
            Assert.AreEqual(off.vertexCount, on.vertexCount, "billboard data must not change topology");
            Object.DestroyImmediate(off);
            Object.DestroyImmediate(on);
        }

        [Test]
        public void Presets_ResetLookNumbers_ButKeepTextures()
        {
            var tex = new Texture2D(2, 2);
            _recipe.Look.BarkTexture = tex;
            _recipe.Look.MossAmount = 1f;
            _recipe.ApplyPreset(TreePreset.Spruce);
            Assert.AreEqual(tex, _recipe.Look.BarkTexture);
            Assert.AreEqual(0f, _recipe.Look.MossAmount, "spruce has no moss");
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void UntexturedPresets_HaveNoTextures_AndSetTexturedRoundTrips()
        {
            foreach (TreePreset preset in System.Enum.GetValues(typeof(TreePreset)))
            {
                _recipe.ApplyPreset(preset, false);
                Assert.IsNull(_recipe.Look.LeafTexture, preset.ToString());
                Assert.IsNull(_recipe.Look.BarkTexture, preset.ToString());
                Assert.IsNull(_recipe.Look.MossTexture, preset.ToString());
                Assert.IsTrue(_recipe.Look.SolidCards, preset.ToString());
            }
            _recipe.ApplyPreset(TreePreset.Maple, false);
            _recipe.SetTextured(true);
            Assert.IsNotNull(_recipe.Look.LeafTexture);
            Assert.IsNotNull(_recipe.Look.BarkTexture);
            Assert.IsFalse(_recipe.Look.SolidCards);
            _recipe.SetTextured(false);
            Assert.IsNull(_recipe.Look.LeafTexture);
            Assert.IsNull(_recipe.Look.BarkTexture);
            Assert.IsTrue(_recipe.Look.SolidCards);
        }

        [Test]
        public void Cards_MapToTheOpaquePartOfTheLeafTexture()
        {
            _recipe.ApplyPreset(TreePreset.Maple);
            Rect r = LeafBounds.Of(_recipe.Look.LeafTexture);
            var mesh = new Mesh();
            TreeGenerator.Build(_recipe, mesh);
            var uvs = new System.Collections.Generic.List<Vector2>();
            mesh.GetUVs(0, uvs);
            Color32[] cols = mesh.colors32;
            int leaves = 0;
            for (int i = 0; i < uvs.Count; i++)
            {
                if (cols[i].b <= 127) continue;
                leaves++;
                Assert.GreaterOrEqual(uvs[i].x, r.xMin - 1e-4f);
                Assert.LessOrEqual(uvs[i].x, r.xMax + 1e-4f);
                Assert.GreaterOrEqual(uvs[i].y, r.yMin - 1e-4f);
                Assert.LessOrEqual(uvs[i].y, r.yMax + 1e-4f);
            }
            Assert.Greater(leaves, 0);
        }

        [Test]
        public void Presets_AssignPackageLeafSprites_ExceptDead_AndKeepUserLeaf()
        {
            foreach (TreePreset preset in System.Enum.GetValues(typeof(TreePreset)))
            {
                _recipe.ApplyPreset(preset);
                if (preset == TreePreset.Dead) Assert.IsNull(_recipe.Look.LeafTexture);
                else Assert.IsNotNull(_recipe.Look.LeafTexture, preset + " has no leaf sprite");
            }
            var mine = new Texture2D(2, 2);
            _recipe.Look.LeafTexture = mine;
            _recipe.ApplyPreset(TreePreset.Oak);
            Assert.AreEqual(mine, _recipe.Look.LeafTexture);
            Object.DestroyImmediate(mine);
        }

        [Test]
        public void ShapedCards_AreGrids_AndStayInsideTheTriangleBudget()
        {
            _recipe.LeafShapes.Fold = 30f;
            _recipe.LeafShapes.Curl = 60f;
            _recipe.Budget.MaxTriangles = 3000;
            var mesh = new Mesh();
            TreeStats stats = TreeGenerator.Build(_recipe, mesh);
            Assert.LessOrEqual(stats.Triangles, 3000);
            Assert.Greater(stats.Cards, 0);
            // fold forces 2 splits across, curl 3 along: 3 x 4 vertices and 12 triangles per card
            Assert.AreEqual(stats.Cards * 12, mesh.GetTriangles(1).Length / 3);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Blobs_PlacementBias_GathersGroupsOnTopOrUnderneath()
        {
            float MeanHeight(float bias)
            {
                SingleBlob(60, 1, 1f);
                _recipe.Leaves.PlacementBias = bias;
                var mesh = new Mesh();
                TreeGenerator.Build(_recipe, mesh);
                LeafVertices(mesh, out var leaf, out var centres);
                float sum = 0f;
                foreach (int i in leaf) sum += centres[i].y;
                Object.DestroyImmediate(mesh);
                return sum / leaf.Count;
            }
            float down = MeanHeight(-1f), even = MeanHeight(0f), up = MeanHeight(1f);
            Assert.Less(down, even - 0.2f);
            Assert.Greater(up, even + 0.2f);
        }

        [Test]
        public void SpriteSheet_FindsEveryNonEmptyCell_AndCardsUseThem()
        {
            // 2 x 2 sheet, bottom-right cell empty
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var px = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    bool right = x >= 32, top = y >= 32;
                    bool leaf = (x % 32) >= 8 && (x % 32) < 24 && (y % 32) >= 4 && (y % 32) < 28;
                    px[y * 64 + x] = new Color32(255, 255, 255, (byte)(leaf && (top || !right) ? 255 : 0));
                }
            tex.SetPixels32(px);
            tex.Apply();
            var cells = LeafBounds.Cells(tex, 2, 2);
            Assert.AreEqual(3, cells.Count);
            foreach (Rect c in cells)
            {
                Assert.AreEqual(0.25f, c.width, 0.02f);
                Assert.AreEqual(0.375f, c.height, 0.02f);
                Assert.AreEqual(Mathf.FloorToInt(c.xMin * 2f), Mathf.FloorToInt((c.xMax - 1e-4f) * 2f), "cell stays inside its column");
            }

            _recipe.Look.LeafTexture = tex;
            _recipe.LeafShapes.Columns = 2;
            _recipe.LeafShapes.Rows = 2;
            _recipe.Leaves.Layout = LeafLayout.Clusters;
            var mesh = new Mesh();
            TreeGenerator.Build(_recipe, mesh);
            var uvs = new System.Collections.Generic.List<Vector2>();
            mesh.GetUVs(0, uvs);
            Color32[] cols = mesh.colors32;
            var used = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < uvs.Count; i++)
            {
                if (cols[i].b <= 127) continue;
                Assert.IsFalse(uvs[i].x > 0.5f + 1e-4f && uvs[i].y < 0.5f - 1e-4f, "no card uses the empty cell");
                used.Add((uvs[i].x > 0.5f ? 1 : 0) + (uvs[i].y > 0.5f ? 2 : 0));
            }
            Assert.AreEqual(3, used.Count);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void Presets_KeepTheSpriteSheetGrid_OfYourOwnLeafTexture()
        {
            var mine = new Texture2D(2, 2);
            _recipe.Look.LeafTexture = mine;
            _recipe.LeafShapes.Columns = 4;
            _recipe.LeafShapes.Rows = 2;
            _recipe.LeafShapes.Curl = 50f;
            _recipe.ApplyPreset(TreePreset.Maple);
            Assert.AreEqual(4, _recipe.LeafShapes.Columns);
            Assert.AreEqual(2, _recipe.LeafShapes.Rows);
            Assert.AreEqual(0f, _recipe.LeafShapes.Curl, "shape numbers come from the preset");
            Object.DestroyImmediate(mine);
        }

        [Test]
        public void StylizedSettings_ChangeTheTree_AndStayDeterministic()
        {
            var plain = new Mesh();
            _recipe.Trunk.RootLobes = 0;
            _recipe.Trunk.Gnarl = 0f;
            _recipe.Trunk.Twist = 0f;
            _recipe.Leaves.Envelope = CrownShape.None;
            _recipe.Leaves.Clumps = 0;
            TreeGenerator.Build(_recipe, plain);

            _recipe.Trunk.RootLobes = 5;
            _recipe.Trunk.Gnarl = 0.2f;
            _recipe.Trunk.Twist = 90f;
            _recipe.Leaves.Envelope = CrownShape.Sphere;
            _recipe.Leaves.EnvelopeStrength = 1f;
            _recipe.Leaves.Clumps = 6;
            var a = new Mesh();
            var b = new Mesh();
            TreeGenerator.Build(_recipe, a);
            TreeGenerator.Build(_recipe, b);
            CollectionAssert.AreEqual(a.vertices, b.vertices);
            CollectionAssert.AreEqual(a.normals, b.normals);
            CollectionAssert.AreNotEqual(plain.vertices, a.vertices);
            foreach (Vector3 n in a.normals) Assert.AreEqual(1f, n.magnitude, 1e-3f);
            Object.DestroyImmediate(plain);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        // Oak with every structure feature off and one branch level, so branch counts are exact
        private void PlainOak()
        {
            TrunkSettings t = _recipe.Trunk;
            t.Splits = 0;
            t.Roots = 0;
            t.NoiseBend = 0f;
            BranchSettings b = _recipe.Branches;
            b.Levels = 1;
            b.NoiseBend = 0f;
            b.LengthNoise = 0f;
            b.Asymmetry = 0f;
            b.Broken = 0f;
        }

        [Test]
        public void NoiseSplitsRootsAndBroken_ChangeTheTree_AndStayDeterministic()
        {
            PlainOak();
            var plain = new Mesh();
            TreeGenerator.Build(_recipe, plain);

            _recipe.Trunk.NoiseBend = 0.2f;
            _recipe.Trunk.Splits = 2;
            _recipe.Trunk.Braid = 0.5f;
            _recipe.Trunk.Roots = 4;
            _recipe.Branches.NoiseBend = 0.3f;
            _recipe.Branches.LengthNoise = 0.5f;
            _recipe.Branches.Asymmetry = 0.5f;
            _recipe.Branches.Broken = 0.3f;
            var a = new Mesh();
            var b = new Mesh();
            TreeGenerator.Build(_recipe, a);
            TreeGenerator.Build(_recipe, b);
            CollectionAssert.AreEqual(a.vertices, b.vertices);
            CollectionAssert.AreNotEqual(plain.vertices, a.vertices);
            foreach (Vector3 v in a.vertices) Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z));
            Object.DestroyImmediate(plain);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        [Test]
        public void SplitsAndRoots_AddOneBranchEach()
        {
            PlainOak();
            var mesh = new Mesh();
            int plain = TreeGenerator.Build(_recipe, mesh).Branches;
            _recipe.Trunk.Splits = 2;
            Assert.AreEqual(plain + 3, TreeGenerator.Build(_recipe, mesh).Branches, "two splits give three stems");
            _recipe.Trunk.Roots = 6;
            Assert.AreEqual(plain + 9, TreeGenerator.Build(_recipe, mesh).Branches);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Roots_StayOnTheGround()
        {
            PlainOak();
            _recipe.Trunk.Roots = 8;
            _recipe.Trunk.RootRadius = 1f;
            _recipe.Trunk.RootWander = 1f;
            var mesh = new Mesh();
            TreeGenerator.Build(_recipe, mesh);
            Vector3[] v = mesh.vertices;
            float lowest = 0f;
            foreach (int i in mesh.GetTriangles(0)) lowest = Mathf.Min(lowest, v[i].y);
            // roots are half buried: at most half their thickness (plus gnarl) below 0
            Assert.GreaterOrEqual(lowest, -_recipe.Trunk.BaseRadius * 0.6f);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Broken_EveryBranch_LeavesOnlyTheCrownTop()
        {
            PlainOak();
            _recipe.Branches.Broken = 1f;
            _recipe.Leaves.Intersecting = false; // a crossed pair would count as two cards
            _recipe.Leaves.Layout = LeafLayout.Clusters; // blobs ignore the branches, so this check needs the branch layout
            _recipe.Leaves.CardsPerBranch = 10;
            var mesh = new Mesh();
            TreeStats stats = TreeGenerator.Build(_recipe, mesh);
            Assert.AreEqual(10, stats.Cards, "only the trunk top keeps leaves");
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Fronds_StayInsideTheCardBudget_AndFollowTheBranches()
        {
            _recipe.ApplyPreset(TreePreset.Spruce);
            Assert.AreEqual(LeafLayout.Fronds, _recipe.Leaves.Layout);
            _recipe.Budget.MaxCards = 40;
            var mesh = new Mesh();
            TreeStats stats = TreeGenerator.Build(_recipe, mesh);
            Assert.LessOrEqual(stats.Cards, 40);
            Assert.Greater(stats.Cards, 0);
            Assert.Greater(stats.CardsDropped, 0);
            foreach (Vector3 n in mesh.normals) Assert.AreEqual(1f, n.magnitude, 1e-3f);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void TipClumpsAndShelves_ChangeTheLeaves_ButNotTheWood()
        {
            PlainOak();
            _recipe.Leaves.Layout = LeafLayout.Clusters; // clumps, shells and shelves belong to the branch layouts; blob groups ignore them
            var plain = new Mesh();
            TreeGenerator.Build(_recipe, plain);
            _recipe.Leaves.TipClumps = true;
            _recipe.Leaves.Shell = 0.6f;
            _recipe.Leaves.FlattenBottom = 0.8f;
            _recipe.Leaves.ClumpHeight = 0.6f;
            var shaped = new Mesh();
            TreeGenerator.Build(_recipe, shaped);
            CollectionAssert.AreEqual(plain.GetTriangles(0), shaped.GetTriangles(0));
            CollectionAssert.AreNotEqual(plain.vertices, shaped.vertices);
            Object.DestroyImmediate(plain);
            Object.DestroyImmediate(shaped);
        }

        [Test]
        public void GroupAxes_FanQuadsEvenly_AndStayOrthonormal()
        {
            TreeGenerator.GroupAxes(Vector3.right, Vector3.forward, 0, 3, out Vector3 r0, out Vector3 n0);
            Assert.AreEqual(0f, Vector3.Distance(Vector3.right, r0), 1e-5f);
            Assert.AreEqual(0f, Vector3.Distance(Vector3.forward, n0), 1e-5f);
            TreeGenerator.GroupAxes(Vector3.right, Vector3.forward, 1, 2, out Vector3 r1, out Vector3 n1);
            Assert.AreEqual(0f, Vector3.Distance(Vector3.forward, r1), 1e-5f, "a pair is crossed at 90 degrees");
            TreeGenerator.GroupAxes(Vector3.right, Vector3.forward, 1, 3, out Vector3 r2, out Vector3 n2);
            Assert.AreEqual(60f, Vector3.Angle(Vector3.right, r2), 1e-3f, "three quads sit 60 degrees apart");
            Assert.AreEqual(0f, Vector3.Dot(r2, n2), 1e-5f);
            Assert.AreEqual(1f, r2.magnitude, 1e-5f);
        }

        [Test]
        public void PullToTip_OnlyUsesTipsInReach()
        {
            var tips = new System.Collections.Generic.List<Vector3> { new Vector3(1f, 0f, 0f), new Vector3(50f, 0f, 0f) };
            Vector3 p = new Vector3(0f, 1f, 0f);
            Assert.AreEqual(p, TreeGenerator.PullToTip(p, Vector3.zero, 2f, tips, 0f, 0), "no bias, no move");
            Assert.AreEqual(p, TreeGenerator.PullToTip(p, Vector3.zero, 0.5f, tips, 1f, 0), "no tip in reach, no move");
            Vector3 pulled = TreeGenerator.PullToTip(p, Vector3.zero, 2f, tips, 1f, 3);
            Assert.Less(Vector3.Distance(pulled, tips[0]), Vector3.Distance(p, tips[0]));
            Assert.AreEqual(tips[0].x * 0.75f, pulled.x, 1e-5f, "the far tip is ignored");
        }

        [Test]
        public void CardsPerCluster_MultipliesCards_AndZeroKeepsTheIntersectingBehaviour()
        {
            PlainOak();
            _recipe.Leaves.Layout = LeafLayout.Clusters; // blobs use Group Quads instead
            _recipe.Budget.MaxCards = 100000;
            _recipe.Budget.MaxTriangles = 1000000;
            var mesh = new Mesh();
            _recipe.Leaves.Intersecting = false;
            int single = TreeGenerator.Build(_recipe, mesh).Cards;
            _recipe.Leaves.CardsPerCluster = 3;
            int triple = TreeGenerator.Build(_recipe, mesh).Cards;
            _recipe.Leaves.CardsPerCluster = 0;
            _recipe.Leaves.Intersecting = true;
            int crossed = TreeGenerator.Build(_recipe, mesh).Cards;
            Object.DestroyImmediate(mesh);
            Assert.AreEqual(single * 3, triple);
            Assert.AreEqual(single * 2, crossed);
        }

        [Test]
        public void BranchTipBias_MovesBlobCards_ButNotTheWood()
        {
            PlainOak();
            var plain = new Mesh();
            TreeGenerator.Build(_recipe, plain);
            _recipe.Leaves.BranchTipBias = 1f;
            var biased = new Mesh();
            TreeGenerator.Build(_recipe, biased);
            CollectionAssert.AreEqual(plain.GetTriangles(0), biased.GetTriangles(0));
            CollectionAssert.AreNotEqual(plain.vertices, biased.vertices);
            Object.DestroyImmediate(plain);
            Object.DestroyImmediate(biased);
        }

        // one plain blob with small cards, so every group sits well inside it
        private CanopyBlob SingleBlob(int groups, int quads, float smoothness)
        {
            var blob = new CanopyBlob { Position = new Vector3(0f, 3f, 0f), Radii = new Vector3(1.2f, 1f, 1.1f), Rotation = new Vector3(10f, 30f, 5f) };
            LeafSettings l = _recipe.Leaves;
            l.Layout = LeafLayout.Blobs;
            l.CardsPerBlob = groups;
            l.GroupQuads = quads;
            l.Smoothness = smoothness;
            l.CardSize = new Vector2(0.3f, 0.3f);
            l.Aspect = 1f;
            l.NormalOffset = 0f;
            l.BranchTipBias = 0f;
            l.Anchor = new Vector2(0.5f, 0.5f);
            _recipe.Blobs = new System.Collections.Generic.List<CanopyBlob> { blob };
            _recipe.Budget.MaxCards = 100000;
            _recipe.Budget.MaxTriangles = 1000000;
            return blob;
        }

        // vertices of the leaf cards, with the card centre (the group anchor) each belongs to in uv1.xyz
        private static void LeafVertices(Mesh mesh, out System.Collections.Generic.List<int> leaf, out System.Collections.Generic.List<Vector4> centres)
        {
            centres = new System.Collections.Generic.List<Vector4>();
            mesh.GetUVs(1, centres);
            Color32[] cols = mesh.colors32;
            leaf = new System.Collections.Generic.List<int>();
            for (int i = 0; i < cols.Length; i++) if (cols[i].b > 127) leaf.Add(i);
        }

        [Test]
        public void Blobs_SameSeed_ProducesIdenticalVerticesAndNormals()
        {
            SingleBlob(14, 3, 1f);
            var a = new Mesh();
            var b = new Mesh();
            TreeGenerator.Build(_recipe, a);
            TreeGenerator.Build(_recipe, b);
            CollectionAssert.AreEqual(a.vertices, b.vertices);
            CollectionAssert.AreEqual(a.normals, b.normals);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Blobs_CardCount_IsGroupsTimesGroupQuads(int quads)
        {
            SingleBlob(10, quads, 1f);
            var mesh = new Mesh();
            TreeStats stats = TreeGenerator.Build(_recipe, mesh);
            Assert.AreEqual(10 * quads, stats.Cards);
            Assert.AreEqual(0, stats.CardsDropped);
            Assert.AreEqual(10 * quads * 2, mesh.GetTriangles(1).Length / 3);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Blobs_CardBudget_CountsQuads()
        {
            SingleBlob(40, 3, 1f);
            _recipe.Budget.MaxCards = 60;
            var mesh = new Mesh();
            TreeStats stats = TreeGenerator.Build(_recipe, mesh);
            Assert.LessOrEqual(stats.Cards, 60);
            Assert.AreEqual(0, stats.Cards % 3, "whole groups only");
            Assert.Greater(stats.CardsDropped, 0);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Blobs_FullSmoothness_EveryVertexUsesTheSurfaceNormalAtTheAnchor()
        {
            CanopyBlob blob = SingleBlob(16, 3, 1f);
            var mesh = new Mesh();
            TreeGenerator.Build(_recipe, mesh);
            LeafVertices(mesh, out var leaf, out var centres);
            Vector3[] nor = mesh.normals;
            Assert.Greater(leaf.Count, 0);
            foreach (int i in leaf)
            {
                Vector3 expected = TreeGenerator.BlobSurfaceNormal(blob, (Vector3)centres[i] - blob.Position);
                Assert.LessOrEqual(Vector3.Angle(expected, nor[i]), 5f);
            }
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Blobs_FullSmoothness_NormalsPointAwayFromTheBlobCentre()
        {
            CanopyBlob blob = SingleBlob(16, 3, 1f);
            var mesh = new Mesh();
            TreeGenerator.Build(_recipe, mesh);
            LeafVertices(mesh, out var leaf, out _);
            Vector3[] pos = mesh.vertices, nor = mesh.normals;
            foreach (int i in leaf) Assert.Greater(Vector3.Dot(nor[i], pos[i] - blob.Position), 0f);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Blobs_ZeroSmoothness_KeepsEachQuadsOwnNormals()
        {
            SingleBlob(12, 3, 1f);
            var smooth = new Mesh();
            TreeGenerator.Build(_recipe, smooth);
            _recipe.Leaves.Smoothness = 0f;
            var flat = new Mesh();
            TreeGenerator.Build(_recipe, flat);
            CollectionAssert.AreEqual(smooth.vertices, flat.vertices, "smoothness only shades");
            CollectionAssert.AreNotEqual(smooth.normals, flat.normals);
            foreach (Vector3 n in flat.normals) Assert.AreEqual(1f, n.magnitude, 1e-3f);
            Object.DestroyImmediate(smooth);
            Object.DestroyImmediate(flat);
        }

        [Test]
        public void Blobs_IgnoreRenormalizeAndPuffiness()
        {
            SingleBlob(12, 3, 1f);
            _recipe.Renormalize.Enabled = false;
            _recipe.Leaves.Puffiness = 0f;
            var off = new Mesh();
            TreeGenerator.Build(_recipe, off);
            _recipe.Renormalize.Enabled = true;
            _recipe.Leaves.Puffiness = 1f;
            var on = new Mesh();
            TreeGenerator.Build(_recipe, on);
            CollectionAssert.AreEqual(off.normals, on.normals);
            Object.DestroyImmediate(off);
            Object.DestroyImmediate(on);
        }

        [Test]
        public void Blobs_DeformedBlob_KeepsAnchorsWithinGroupDepthOfTheCage()
        {
            CanopyBlob blob = SingleBlob(20, 3, 1f);
            blob = BlobCage.EnsureDeform(blob);
            blob.Deform[0] = Vector3.up * 0.7f;
            blob.Deform[9] = Vector3.right * 0.6f;
            blob.Deform[13] = Vector3.back * 0.5f;
            _recipe.Blobs = new System.Collections.Generic.List<CanopyBlob> { blob };
            _recipe.Leaves.GroupVolume = 0f;
            float depth = _recipe.Leaves.GroupDepth * 0.3f; // every card is 0.3 m
            var mesh = new Mesh();
            TreeGenerator.Build(_recipe, mesh);
            LeafVertices(mesh, out var leaf, out var centres);
            Assert.Greater(leaf.Count, 0);
            foreach (int i in leaf)
            {
                Vector3 off = (Vector3)centres[i] - blob.Position;
                float gap = BlobCage.Extent(blob, off.normalized) - off.magnitude;
                Assert.AreEqual(depth, gap, 1e-3f, "anchor sits GroupDepth below the cage surface");
            }
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void BlobSurfaceNormal_PlainBlobFollowsTheEllipsoid_AndPointsOutward()
        {
            var flat = new CanopyBlob { Position = Vector3.zero, Radii = new Vector3(2f, 1f, 2f) };
            Assert.AreEqual(0f, Vector3.Distance(Vector3.up, TreeGenerator.BlobSurfaceNormal(flat, Vector3.up)), 1e-4f);
            Assert.AreEqual(0f, Vector3.Distance(Vector3.right, TreeGenerator.BlobSurfaceNormal(flat, Vector3.right)), 1e-4f);
            Vector3 diag = new Vector3(1f, 1f, 0f).normalized;
            Vector3 n = TreeGenerator.BlobSurfaceNormal(flat, diag);
            Assert.Greater(n.y, n.x, "the squashed axis gets the steeper gradient");
            Assert.Greater(Vector3.Dot(n, diag), 0f);

            CanopyBlob bumped = BlobCage.EnsureDeform(flat);
            bumped.Deform[0] = Vector3.up * 1f;
            Vector3 top = TreeGenerator.BlobSurfaceNormal(bumped, Vector3.up);
            Assert.AreEqual(1f, top.magnitude, 1e-4f);
            Assert.Greater(top.y, 0.5f);
        }

        [Test]
        public void OtherLayouts_IgnoreTheBlobOnlySettings()
        {
            foreach (TreePreset preset in new[] { TreePreset.Spruce, TreePreset.Oak })
            {
                _recipe.ApplyPreset(preset);
                _recipe.Leaves.Layout = preset == TreePreset.Spruce ? LeafLayout.Fronds : LeafLayout.Clusters;
                var a = new Mesh();
                TreeGenerator.Build(_recipe, a);
                _recipe.Leaves.GroupQuads = 6;
                _recipe.Leaves.GroupDepth = 0.4f;
                _recipe.Leaves.GroupJitter = 1f;
                _recipe.Leaves.Smoothness = 0f;
                _recipe.Leaves.CardsPerBlob = 7;
                var b = new Mesh();
                TreeGenerator.Build(_recipe, b);
                CollectionAssert.AreEqual(a.vertices, b.vertices, preset.ToString());
                CollectionAssert.AreEqual(a.normals, b.normals, preset.ToString());
                CollectionAssert.AreEqual(a.triangles, b.triangles, preset.ToString());
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }
    }
}
