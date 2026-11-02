# Foliage Brush

[Back to README](../README.md)

Paint trees, grass and any prefab into the scene.

Open it via `Meowdieval > Foliage Forge > Foliage Brush`, the manager's *Open Foliage Brush* button, the Foliage Brush tool in the Tools overlay, or a Grass Field's *Paint With Foliage Brush* button. Nothing needs to be selected: it paints into the selected Foliage Forge (or Grass Field), else the first one in the scene. With none, the panel shows **Create Foliage Forge**. The panel is the **Foliage Brush** overlay in the scene view (Overlays menu).

## Panel

The overlay can be resized: drag it wider (260 to 720 px). Label columns are at least 110 px, about 40% of the width, and wrap instead of clipping. The header shows the target name and a **Select** button.

| Control | Action |
|---|---|
| **Paint** / **Erase** / **Off** | Mode. Paint and Erase start the tool; Off leaves it |
| **Grass paint** / **Prefabs** | Target: grass strokes on the Grass Field, or palette prefabs under the Props root |
| **Radius** | Brush radius in metres |
| **Ground** | Layer mask the brush and grass land on |

### Grass paint

| Control | Meaning |
|---|---|
| **Density** | Density of each dab as a multiple of the profile's own density |
| Grass types list | One row per Grass Profile type: on/off box and **Mix %** slider (0-100), plus a `Clump share:` summary |

Strokes are scattered when you release the mouse. The type rows edit the Grass Profile (undoable), so every stroke and the fill of each field using that profile re-blend. Strokes do not remember the mix they were painted with.

### Prefabs

| Control | Meaning |
|---|---|
| **Density** | Share of free spots filled per dab |
| **Tree spacing** | Minimum distance between trees |
| **Grass spacing** | Minimum distance between grass batches; low values overlap batches into a tuft |
| **Use variants** | Swap a placed tree for a weighted random variant of its Tree Variant Set; erasing a tree also erases its sibling variants. See [Variants](variants-and-lods.md) |
| **Cluster** | Group same-prefab props around a few random centres per dab |
| **Cluster radius** | Shown with Cluster: rough size of one group in metres |
| **Edge bias** | Prefer spots near the brush rim and near ledges or water edges |
| **Edge strength**, **Edge size** | Shown with Edge bias: strength 0-1; probe distance in metres |
| **Scale** | Random scale range, min and max |
| **Random rotation** | Turn each prop by a random amount |
| **Rotation mode**, **Axes**, **Amount** | Shown with Random rotation. Mode: World, or Align to surface (stand on the ground normal first). Axes: X, Y, Z flags. Amount: min and max degrees |

## Palette

Under *Palette (click to toggle)*: baked prefabs grouped as **Trees**, **Grass batches** and **Other prefabs** (prefabs you added that are not baked), each with a count. Thumbnails are the saved bake-time PNGs; a prefab without one shows the Unity preview. The list refreshes when the project changes.

- Click a tile to add it to or remove it from the palette; ticked tiles are highlighted.
- **+ Selected** adds the prefabs selected in the Project window. **Bake...** opens the Foliage Forge Editor.
- **Tile size**: **Small**, **Medium** (default), **Large** set the thumbnail size (40 / 56 / 80 px). The choice is remembered in EditorPrefs (`FoliageForge.BrushTileSize`).

Each ticked tile shows a **Mix %** field (0-100, default 100) under its name. The brush picks entries by weighted random, so 50 / 30 / 20 places them in that proportion, and 0 never places it. The `Mix:` line above the thumbnails sums up the effective mix (for example `Short 50% / Tall 30% / Fern 20%`).

Ticking several grass batches paints them mixed; **Grass spacing** controls how tightly they overlap. Tree Variant Sets count as one entry each and pick a weighted prefab per placement.

## Controls in the scene view

| Input | Action |
|---|---|
| Left-drag | Paint or erase |
| Shift | Inverts paint and erase |
| Shift + scroll | Brush radius |
| Alt | Orbit |
| Esc, or *Off* | Leave the tool |

Each stroke is one undo step.

## Ground

The Ground mask (shared by the manager and its Grass Field) needs colliders. The panel's message box and the cursor label say why nothing happens:

- empty palette,
- a collider on a layer outside the mask (the **Add layer '<name>' to Ground** button fixes it),
- no collider at all: props land on a flat plane at the manager's height (shown in yellow); grass needs a collider.

A footer line shows prop count, grass strokes and clump count.

New Foliage Forges include the `Ground` layer. Keep the layers of placed props out of the mask, or grass grows on trees (the inspector warns).
