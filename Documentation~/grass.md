# Grass

[Back to README](../README.md)

Runtime grass, separate from the baked trees: GPU-instanced clumps of crossed cards, so thickness comes from the mesh and the instance count stays low.

`GameObject > Meowdieval > Grass Field` adds a **Grass Field** and a **Grass Profile** (`Assets/Foliage Forge/Grass.asset`). Uses the same **Foliage Wind** component as the trees.

## Grass Field

- A box (local size) that grass fills. Rays go down from the box top onto the **Ground** layers; leave units and buildings out of that mask.
- Slopes over the profile's *Slope Limit* stay bare.
- Scatter is deterministic from *Seed* and reruns when you edit; nothing is saved.
- **Ground colour**: the inspector's *Capture Ground Colour* renders the ground top-down; enable Features > Ground Map.

## Grass types

Profile > **Variants** is a list of simple, single-purpose types (default Short, Tall, Fern, Dry sprig). You build the mix; there are no pre-mixed tufts and no count limit.

Each type has: sprite (stock ones in `Textures/Flat`), density, clustering, size, card count, bend, colours and **Mix %** (0-100, default 100). Mix % multiplies density wherever types are combined (field fill, paint strokes, window preview); lowering it thins a stable subset.

- **Biome** replaces the list with the biome's types (Moss lawn / Fern / Broad blades / Mushrooms ...). A type with the same name keeps its sprite, on/off and Mix.
- **Quality** resets the density of types whose name matches the biome's.

## Rendering

- `Graphics.RenderMeshInstanced`, 1023 clumps per call, per chunk. Chunks are frustum-culled per camera.
- **Distance LOD** halves and quarters the clump count with distance (survivors widen to keep the lawn full). Clumps shrink away at the draw distance.
- Shader `Meowdieval/Foliage Forge/Grass`: alpha-to-coverage, per-blade normals, point/spot lights, wind, trample, ground tint, GPU distance thinning, shadow distance, DepthNormals pass.

## Features

Profile > Features: each is a plain uniform and off = skipped work.

Wind, Texture, Gradient, Variation, Patches, Stylized Shading, Rim, Translucency, Ground Blend, Distance Fade, Receive Shadows, Cast Shadows, Distance LOD, Frustum Culling, Additional Lights, Trample, Ground Map.

**Lush / Balanced / Light** buttons set density, draw distance and the costly features in one click.

## Trample

Add `GrassTrampler` to units or effects. The 16 nearest bend the grass (Features > Trample).

## Window (Grass mode)

The window's **Trees | Grass** switch edits a Grass Profile and previews a flat patch live (wind toggle works). The Grass Profile inspector lists every type with its on/off toggle and Mix % above the full Variants list.

## Baking batches

**Bake Prefabs / Overwrite Prefabs / Save As New** (top right in Grass mode) bake every enabled type into its own small circular batch prefab under `Grass/<profile>/` (size = the number field in the toolbar), at full Density. Mix % is **not** baked; blend batches with the palette Mix % in the [brush](brush.md). Each clump keeps its own wind, gust and thinning (origin in UV1).

- Overwrite rewrites the same assets in place.
- Save As New copies the profile and bakes the copy.
- Loading a batch from the gallery switches to the Grass tab; loading a tree switches back.

## Requirements

MSAA 4x when a profile uses alpha to coverage; additional lights per pixel when a profile uses them. The [Setup Wizard](setup.md) checks both.
