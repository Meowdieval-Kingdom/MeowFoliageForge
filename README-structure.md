# Foliage Forge package layout

Namespace `Meowdieval.FoliageForge` everywhere; large types are `partial` classes split by responsibility (`Type.Part.cs`).

- `Runtime/` - `FoliageForgeManager`, `FoliageWind`; `Runtime/Grass/` - grass field, profile, scatter, biomes, meshes, trampler, batch.
- `Editor/Generation/` - `TreeGenerator` public API and `TreeStats`; `TreeGenerator.Builder.*` partials: Core (state, RNG, Build), Skeleton (trunk, roots, branches), Blobs (blob suggestion, blob leaf groups), Leaves (cluster layout, crown envelope, clumps), Cards (card/grid emit, winding), Fronds, Mesh (tubes, normals/AO, puff, mesh assembly).
- `Editor/Recipe/` - `TreeRecipe` (serialized fields), `TreeRecipe.Presets` (preset values), one file per settings class, enums, `PresetTextures`, `TreeRecipeEditor`.
- `Editor/Window/` - `FoliageForgeWindow` partials: Toolbar, Recipe, Preview, Baking, Gallery, Grass, Blobs, Viewer, Wind.
- `Editor/Brush/` - `FoliageBrush` partials: core/state, Palette, SceneGui, Placement; enums, tool, overlay.
- `Editor/Baking/` - `TreeBaker` (bake, LODs), `TreeBaker.Materials`, `LeafTexture`.
- `Editor/Preview/` - `TreePreview` partials: core/draw, Normals, Inset (inset and grass), Interaction (picking, cage, input); enums.
- `Editor/Blobs/` - blob cage, viewer, anchor picker, leaf bounds.
- `Editor/Grass/` - grass patch, patch baker, grass inspectors.
- `Editor/Setup/` - setup wizard, project settings, verify, manager inspector.
- `Tests/Editor/` - unit and integration tests (unchanged).

Scripts that Unity binds to assets (ScriptableObjects, editors) keep their file name and .meta, so GUIDs are unchanged.
