# Foliage Forge package layout

Namespace `Meowdieval.FoliageForge` everywhere; large types are `partial` classes split by responsibility (`Type.Part.cs`).

- `Runtime/` - `FoliageForgeManager`, `FoliageWind`, `TreeVariantSet`.
  - `Runtime/Grass/` - `GrassField`, `GrassGpuField`, `GrassGpuMath`, `GrassProfile`, `GrassScatter`, `GrassBiomes`, `GrassMeshes`, `GrassTrampler`, `GrassBatch`.
- `Shaders/` - tree Shader Graphs (`FoliageForge`, `FoliageForgeBark`) with `FoliageForge.hlsl`; grass `FoliageForgeGrass.shader` and `.hlsl`; `GrassPlacement.compute`.
- `Editor/Generation/` - `TreeGenerator` public API and `TreeStats`; `TreeGenerator.Builder.*` partials: Core (state, RNG, Build), Skeleton (trunk, roots, branches), Blobs (blob suggestion, blob leaf groups), BlobLook, Leaves (cluster layout, crown envelope, clumps), Cards (card/grid emit, winding), Fronds, Mesh (tubes, normals/AO, puff, mesh assembly).
- `Editor/Recipe/` - `TreeRecipe` (serialized fields) with partials for Presets, BlobPresets, BlobLook, Variants and AssetSnapshots; one file per settings class (`TrunkSettings`, `BranchSettings`, `LeafSettings`, ...), enums, `PresetTextures`, `ReapplyPresets`, `BlobTreeShowcase`, `TreeRecipeEditor`.
- `Editor/Window/` - `FoliageForgeWindow` partials: Toolbar, Recipe, Preview, Baking, Gallery, Grass, Blobs, Viewer, Wind.
  - `FoliageUi.cs` - shared UI helpers and the entry point that applies the style sheet.
  - `Styles/FoliageForge.uss` - the shared style sheet.
- `Editor/Brush/` - `FoliageBrush` partials: core/state, Palette, SceneGui, Placement; `BrushEnums`, `FoliageBrushTool`, `FoliageBrushOverlay`.
- `Editor/Baking/` - `TreeBaker` (bake, LODs), `TreeBaker.Materials`, `TreeBaker.Variants`, `LeafTexture`.
- `Editor/Preview/` - `TreePreview` partials: core/draw, Normals, Inset (inset and grass), Interaction (picking, cage, input); `PreviewEnums`; `PrefabThumbnail` (gallery thumbnails rendered at bake time and saved beside the prefab as `{name}_Thumb.png`).
- `Editor/Blobs/` - `BlobCage`, `BlobViewer`, `AnchorPicker`, `LeafBounds`.
- `Editor/Grass/` - `GrassProfileEditor` (grass profile inspector), `GrassEditors` (Grass Field inspector, create menu), `GrassGpuFieldEditor`, `GrassPatch`, `GrassPatchBaker`.
- `Editor/Setup/` - `FoliageForgeSetup` (validate checks, setup wizard, auto-open), `FoliageForgeSetup.uss` (wizard styles), `FoliageForgeProjectSettings` (URP and layer checks), `FoliageForgeVerify`, `FoliageForgeManagerEditor`.
- `Tests/Editor/` - unit and integration tests. `UiCoverageTests` checks that the tree and grass inspectors place every serialized field and build with the shared style sheet.

Scripts that Unity binds to assets (ScriptableObjects, editors) keep their file name and .meta, so GUIDs are stable.

## UI conventions

- Editor UI is UI Toolkit, built in code: the inspectors, the window, the brush overlay and the setup wizard.
- Every root calls `FoliageUi.Apply(root)`, which adds `Editor/Window/Styles/FoliageForge.uss` and the `ff-root` class. The sheet only reaches elements below such a root; applying twice is harmless.
- Styling uses `ff-*` classes (`ff-panel`, `ff-row`, `ff-field`, `ff-chip`, `ff-chip--on`, `ff-button`, `ff-group-label`, `ff-hint`, `ff-muted`, `ff-stat-chip`) and `--ff-*` variables (colours such as `--ff-accent`, spacing, font sizes, label column width) with a light-skin override. Helpers in `FoliageUi`: `ChipButton`, `SetChipOn`, `GroupLabel`, `WrapRow`, `Field`, `StatChip`.
- UI construction code must not use `PreviewRenderUtility` or render a `Camera`: doing so while a window or inspector builds breaks URP pipeline creation. Thumbnails are baked once by `PrefabThumbnail` and the UI only loads the PNG.
- Every serialized field must be placed in an inspector section. `TreeRecipeEditor` and `GrassProfileEditor` expose the placed paths and `UiCoverageTests` fails when a field is missing, stale or placed twice. The grass inspector also adds unplaced fields to an "Other" section as a fallback.
