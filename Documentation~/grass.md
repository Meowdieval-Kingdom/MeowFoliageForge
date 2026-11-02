# Grass

[Back to README](../README.md)

Runtime grass, separate from the baked trees: GPU-instanced clumps of crossed cards, so thickness comes from the mesh and the instance count stays low.

`GameObject > Meowdieval > Grass Field` adds a **Grass Field** and a **Grass Profile** (`Assets/Foliage Forge/Grass.asset`, reused if one exists). Uses the same **Foliage Wind** component as the trees.

## Grass Field

- A box (*Size*, local) that grass fills. Rays go down from the box top onto the **Ground** layers; leave units and buildings out of that mask.
- Slopes over the profile's *Slope Limit* stay bare.
- Scatter is deterministic from the profile's *Seed* and reruns when you edit; nothing is saved except brush strokes.
- Inspector buttons: **Rescatter**, **Capture Ground Colour** (renders the ground top-down into `Assets/Foliage Forge/<field>_GroundMap.png` and assigns it; enable Features > Colour > Ground Map), **Paint With Foliage Brush**, **Clear Paint (n strokes)**.
- The inspector shows live clump, chunk and draw-call counts and the triangles per clump of each enabled type.

**Grass GPU Field** is the compute-shader variant. It does not raycast: no slope limit, ground layers or brush strokes. Ground comes from its height texture or the flat plane at the object's Y. It switches itself off on devices without compute shaders.

## Grass Profile inspector

The profile inspector (also shown in the window's Grass mode) is built with UI Toolkit.

- **Search** field on top filters settings by name or tooltip across all tabs. While searching, each tab label shows its match count, the view jumps to a tab with matches if the current one has none, and Advanced settings and collapsed sections are included.
- **Tabs**: Types, Features, Appearance, Interaction, Draw and placement. The selected tab and the search text persist for the editor session.
- Sections that depend on a feature show `(off in Features)` in their title while that feature is off.
- **Advanced**: a section with rarely used settings has an *Advanced* chip in its header. It is remembered per section.
- Any serialized field not placed on a tab appears in an **Other** section at the bottom of Draw and placement.

Where each setting lives is given as **Where: Tab > Section**; *(Advanced)* marks settings behind the section's chip.

## Types tab

### Look
**Where: Types > Look**

- **Biome** chips: Meadow, Forest, Tundra, Desert, Alien, Savanna, Swamp, Autumn, Ashlands. With *Add types only* off, a biome replaces the grass types, colours and wind feel; a type from the same biome ingredient keeps its sprite, on/off and Mix. With it on, the biome only adds its grass types and changes nothing else.
- **Quality** chips: **Lush**, **Balanced**, **Light** set density (x1 / x0.55 / x0.25 of the biome's), draw distance (70 / 50 / 30 m) and the costly features in one click. Hand-made types keep their density.

### Grass types
**Where: Types > Grass types**

A list of simple, single-purpose types (default Short, Tall, Fern, Dry sprig). You build the mix; there is no pre-mixed tuft and no count limit. Each row has:

| Control | Meaning |
|---|---|
| Enabled checkbox | Off keeps the settings but scatters none |
| Mix % (0-100, default 100) | Multiplies Density wherever types are combined (field fill, paint strokes, window preview); lowering it thins a stable subset |
| Density | Clumps per square metre |

**Add type** appends a type. A strip below lists triangles per clump at mesh LOD 0 / 1 / 2 for each enabled type.

### Type details
**Where: Types > Type details** (collapsed; *Show* / *Hide*)

One foldout per type with **Move up**, **Move down**, **Remove**, then:

| Group | Fields |
|---|---|
| Identity | Name, Source Id (biome ingredient, used by Quality and biome apply), Texture (cutout sprite; stock ones in `Textures/Flat`) |
| Scatter | Clustering (0 even, 1 patches only), Cluster Scale |
| Size | Height, Width (min, max metres) |
| Clump mesh | Cards, Segments, Bend, Spread |
| Colour | Bottom, Top, Tip, Tip Start, Tip Hue Variation, Wind Weight |

## Features tab

Plain uniform switches; off = skipped work. Each group title shows how many are on (for example `Motion 3/6`). Settings that belong to a switch sit under it once it is on.

| Where | Features |
|---|---|
| Features > Motion | Wind, Wind Texture, Gusts, Trample, Trample Map, Perspective Correction (shows *Perspective Strength* when on) |
| Features > Colour | Texture, Gradient, Variation, Patches, Ground Blend, Ground Map, Trail Tint |
| Features > Lighting | Stylized Shading, Rim, Translucency, Receive Shadows, Cast Shadows, Additional Lights, Light Ramp (shows the ramp texture when on) |
| Features > Performance | Distance Fade, Distance Lod, Alpha To Coverage, Frustum Culling, Mesh Lod, Cpu Thinning |

Notes: Light Ramp stays off until a ramp texture is assigned. Trail Tint reads the trample map, so it needs Trample Map. Cpu Thinning needs Distance Lod. Trample Map replaces the 16-nearest Trample list.

## Appearance tab

| Where | Settings |
|---|---|
| Appearance > Colour | Color Variation, Root Darkening; *(Advanced)* Gradient Bias, Alpha Cutoff |
| Appearance > Patches (Patches) | Patch Color, Patch Amount, Patch Scale; *(Advanced)* Patch Blur, Patch Offset |
| Appearance > Shadow | Shadow Color, Shadow Strength; *(Advanced)* Shadow Softness, Shadow Distance |
| Appearance > Light | Light Contribution; *(Advanced)* Normal Up, Additional Light Strength |
| Appearance > Rim (Rim) | Rim Color, Rim Strength; *(Advanced)* Rim Power |
| Appearance > Translucency (Translucency) | Translucency Color, Translucency Strength |
| Appearance > Ground (Ground Blend) | Ground Color, Ground Blend Height, Ground Map Strength |
| Appearance > Gusts (Gusts) | Gust Color, Gust Intensity, Gust Speed; *(Advanced)* Gust Frequency |
| Appearance > Flutter (Wind) | Flutter |

Wind sway and entropy come from **Foliage Wind**, not the profile.

## Interaction tab

Add `GrassTrampler` to units or effects. Without Trample Map the 16 nearest bend the grass; with it, every trampler in range is stamped into a camera-following map that fades, so footprints recover.

| Where | Settings |
|---|---|
| Interaction > Trample | Trample Strength |
| Interaction > Trample map (Trample Map) | Trample Window Size, Trample Recovery Seconds; *(Advanced)* Trample Map Resolution |
| Interaction > Trail tint (Trail Tint) | Trail Top Color, Trail Bottom Color, Trail Strength |

## Draw and placement tab

Rarely changed.

| Where | Settings |
|---|---|
| Draw and placement > Shader and range | Shader, Draw Distance; *(Advanced)* Chunk Size, Lod Mid, Lod Far, Lod Min Share, Max Instances |
| Draw and placement > Placement | Slope Limit, Normal Align; *(Advanced)* Seed |
| Draw and placement > Other | Any field not placed above |

## Rendering

- `Graphics.RenderMeshInstanced`, 1023 clumps per call, per chunk. Chunks are frustum-culled per camera.
- **Distance Lod** halves and quarters the clump count with distance (survivors widen to keep the lawn full). Clumps shrink away at the draw distance.
- Shader `Meowdieval/Foliage Forge/Grass`: alpha-to-coverage, per-blade normals, point/spot lights, wind, trample, ground tint, GPU distance thinning, shadow distance, DepthNormals pass.

## Window (Grass mode)

The window's **Trees | Grass** switch edits a Grass Profile with this inspector and previews a flat patch live (wind toggle works).

## Baking batches

**Bake Prefabs / Overwrite Prefabs / Save As New** (top right in Grass mode) bake every enabled type into its own small circular batch prefab under `Grass/<profile>/` (size = the number field in the toolbar), at full Density. Mix % is **not** baked; blend batches with the palette Mix % in the [brush](brush.md). Each clump keeps its own wind, gust and thinning (origin in UV1).

- Overwrite rewrites the same assets in place.
- Save As New copies the profile and bakes the copy.
- Loading a batch from the gallery switches to the Grass tab; loading a tree switches back.

## Requirements

MSAA 4x when a profile uses alpha to coverage; additional lights per pixel when a profile uses them. The [Setup Wizard](setup.md) checks both.
