# The Foliage Forge window

[Back to README](../README.md)

Open it with `Meowdieval > Foliage Forge > Foliage Forge Editor`, or double-click a Tree Recipe asset. The minimum window size is 420 x 480. For a first tree see [Getting started](getting-started.md).

## Layout

```
Toolbar     row 1: Trees | Grass, recipe / profile field, New, More, Save As New..., Bake
            row 2: presets for the tab, Foliage Brush, Setup Wizard
Settings    search, tabs, sections (left, or below the preview when narrow)
Right       View bar
            Preview
            (scrolls) stats, status line, Blobs panel, Wind panel, Prefabs gallery
```

The settings pane and the right column are separated by a draggable divider. The View bar and the preview keep their height; everything under the preview scrolls.

## Toolbar

Both rows wrap when the window is narrow.

Row 1

| Control | Does |
|---|---|
| **Trees** / **Grass** | Switches the tab. Each tab has its own asset, settings and preview. |
| **Recipe** / **Profile** field | The saved Tree Recipe or Grass Profile being edited. Empty means an unsaved scratch object. Tree edits go to a working copy until you bake; a grass profile is edited in place. |
| **New** | Trees: saves a new recipe asset and opens it (with no recipe picked, saves the scratch tree). Grass: saves a copy of the current profile as a new asset and opens it. |
| **More** | Narrow windows only (see Responsive layout). Holds everything from row 2. |
| **Save As New...** | Copies the current recipe or profile into a new asset and bakes it as a separate prefab. The original prefab is untouched. |
| **Bake Prefab** | Bakes the current recipe or profile. See Bake button below. |

Row 2, Trees tab ("Start from")

| Control | Does |
|---|---|
| **Preset** | Replaces every value with Oak, Maple, Spruce or Dead. Undo restores. |
| **Textured** | Chip. When on, the next preset you pick loads leaf, bark and moss textures; when off, no textures and flat solid cards. Remembered between sessions. |
| **Swap Textures** | Switches the current recipe between textured and untextured, keeping everything else. Your own textures stay. |
| **Reroll Seed** | Same settings, new random branches. |
| **Bake All Variants** | Bakes every variant as `<Name>_V1`, `<Name>_V2`, ... plus a `<Name>_Variants` set. Needs variants first (Output tab, Type and variants). See [Variants and LODs](variants-and-lods.md). |

Row 2, Grass tab ("Scatter")

| Control | Does |
|---|---|
| **Reroll Seed** | Same settings, new random scatter. |
| **Batch size (m)** | Diameter of each baked grass batch, 0.5 to 32 m. |

Row 2, both tabs

| Control | Does |
|---|---|
| **Foliage Brush** | Focuses a Scene view and activates the brush. See [Foliage Brush](brush.md). |
| **Setup Wizard** | Opens the wizard. See [Setup and validation](setup.md). |

Grass biome and quality presets are not on the toolbar. They are on the Types tab of the grass settings.

## Responsive layout

| Window width | Layout |
|---|---|
| 900 px or more | Settings left, preview and gallery right. |
| Below 900 px | Stacked: preview and gallery on top, settings below. |
| Below 700 px | Row 2 is hidden. Its actions move into the **More** menu in row 1 (presets, Textured, Swap Textures, Reroll Seed, Bake All Variants, Foliage Brush, Setup Wizard). **Batch size (m)** is only in row 2, so widen the window to change it. |

## Settings pane

The pane shows the inspector of the current recipe (Trees) or profile (Grass).

- **Scratch banner.** With no asset picked, a banner above the settings reads "Scratch tree, not saved yet" (or "Scratch grass profile"). **Save...** saves it as an asset. Bake also asks where to save it.
- **Search field.** Filters every tab by field name or tooltip. While a query is active, matching sections open, Advanced settings that match are shown, and each tab button shows its match count. The editor jumps to the first tab with a match.
- **Tabs.** Trees: **Shape**, **Foliage**, **Look**, **Output**. Grass: **Types**, **Features**, **Appearance**, **Interaction**, **Draw and placement**. The selected tab is remembered.
- **Advanced.** Sections with rarely used settings have an **Advanced** chip. It reveals those settings, and on the Trees tabs also whole advanced sections (for example Trunk detail, Branch detail). Each section has its own chip.
- **Foldouts and summaries.** Tree sections are foldouts. A collapsed section shows a one-line summary of its values in the header. Open/closed and Advanced states are remembered until Unity closes.
- Fields that do not apply to the current choices are hidden. For example the Blobs fields only show with the Blobs leaf layout, and the Stylized fields only with Stylized Shading on.

Field reference: [Trees](trees.md), [Leaves, look and shaders](look-and-shaders.md), [Grass](grass.md).

## View bar and preview modes

The View bar sits above the preview and wraps. Mode chips are on the Trees tab only.

| Chip | Does |
|---|---|
| **Wireframe** | Mesh edges, wood and leaf cards, to judge triangle density. |
| **Blobs** | Wood plus the editable canopy blobs. Opens the Blobs panel. |
| **Render** | The finished, shaded tree as it bakes. |
| **Blob** | The selected blob (the first when none is selected) alone at the origin, to judge its leaf groups. |
| **Variants** | The recipe's variants side by side. |
| **Auto Rotate** | Slowly turns the camera around the tree. Dragging takes over. The angle and the flag are kept when you switch prefabs. |
| **Ortho** | Orthographic camera, for checking the silhouette. |
| **Foliage** | Shows the leaves in Render and Wireframe. Off leaves the wood. |
| **Ground** | Ground plane. |
| **Scale** | A size reference capsule; the number beside it is its height in units. |
| **Backlit** | A low sun behind the tree, to check translucency. |
| **Wind** | Animates wind in the preview and the open scene. Direction and Strength are in the Wind panel below the preview, enabled while Wind is on. |

Turning Wind off restores the scene's wind values.

## Preview controls

| Input | Does |
|---|---|
| Drag | Orbit |
| Scroll | Zoom |
| Double-click | Reset angle and zoom |

In **Blobs** mode:

| Input | Does |
|---|---|
| Drag a blob | Move it (and select it) |
| Drag a point of the selected blob | Deform it. Shift: neighbouring points follow. |
| Scroll | Resize the selected blob. Ctrl: height only. Alt: rotate it around the vertical axis. |
| Delete | Remove the selected blob |
| Click empty space | Deselect, then orbit |

The Blobs panel has **Auto Blobs**, **Randomize** (with Count, Size, Spread and Flatness sliders), **Add Blob**, and for the selected blob Position, Radii, Rotation, **Reset Shape** and **Delete**. See [Trees](trees.md) for blob design.

## Stats and status

Under the preview, chips show Triangles, Cards, Vertices, Draw calls, Mesh size and tree Size for a tree, or Preview clumps, Triangles, clumps per m2, Features on and Draw distance for grass. Triangles turn to a warning colour above `Budget.MaxTriangles`; Cards warn when some were capped. The status line below reports the last bake or load and any budget warning.

The grass preview is a flat patch. Trample, ground map, additional lights and shadows only show in the scene.

## Bake button

The button changes with the state of the saved asset:

| Label | Meaning |
|---|---|
| **Bake Prefab** | No prefab exists for this recipe yet. |
| **Overwrite Prefab  ●** | The working copy differs from the saved recipe. Baking writes it into the recipe and overwrites that prefab, nothing else. |
| **Prefab Up To Date** | The prefab matches the recipe. |
| **Bake Prefabs** / **Overwrite Prefabs** | Grass: one batch prefab per enabled grass type, in `<output folder>/Grass/<profile name>`. Edits since the last bake are not tracked, so Overwrite shows whenever batches exist. |

## Prefabs gallery

Lists every baked tree and grass batch in the output folder (default `Assets/Foliage Forge`).

| Control | Does |
|---|---|
| **All** / **Trees** / **Grass** | Filter. The title shows the shown and total count. |
| **Folder...** | Chooses the output folder (inside Assets). |
| **Show** | Pings the output folder in the Project window. |
| **Small** / **Medium** | Thumbnail size. Tiles wrap to the available width. |
| Click a tile | Picks it (frame, bold name) and pings it in the Project. |
| Double-click, or **Load** | Opens its recipe or grass profile here, switching tab. |
| **Delete** | Moves the picked prefab to the trash. Its recipe or profile is kept. |

A blue tile with a dot is what the editor is showing now.

### Thumbnails

A 128 px thumbnail is rendered when you bake and saved beside the prefab as `<name>_Thumb.png`. The gallery loads that file. Prefabs baked without one fall back to Unity's asset preview, which can draw shader-graph foliage pink; bake them again to get a proper thumbnail.
