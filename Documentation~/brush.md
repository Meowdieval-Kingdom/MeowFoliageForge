# Foliage Brush

[Back to README](../README.md)

Paint trees, grass and any prefab into the scene.

Open it via `Meowdieval > Foliage Forge > Foliage Brush`, the manager's *Open Foliage Brush* button, or the Tools overlay. Nothing needs to be selected: it paints into the selected Foliage Forge (or Grass Field), else the first one in the scene, and offers *Create Foliage Forge* when there is none. The panel lives in the scene view (Overlays menu: *Foliage Brush*).

## Controls

| Input | Action |
|---|---|
| Left-drag | Paint or erase |
| Shift | Inverts paint and erase |
| Shift + scroll | Brush size |
| Alt | Orbit |
| Esc, or *Off* | Leave the tool |

Each stroke is one undo step. Modes: *Paint / Erase / Off*, and targets *Grass paint* (strokes on the Grass Field, scattered on mouse release) or *Prefabs* (instances under Props with density, tree / grass spacing, random yaw and scale).

## Palette

Thumbnails of every baked tree and grass batch (refreshed when the project changes); click to toggle. *+ Selected* adds other prefabs from the Project window.

Each ticked tile shows a **Mix %** field (0-100, default 100). The brush picks entries by weighted random, so 50 / 30 / 20 places them in that proportion, and 0 never places it. The line above the thumbnails sums up the effective mix (e.g. `Short 50% / Tall 30% / Fern 20%`).

Ticking several grass batches paints them mixed; *Grass Spacing* controls how tightly they overlap.

Tree Variant Sets (see [Variants](variants-and-lods.md)) count as one entry each and pick a weighted prefab per placement.

## Grass paint mix

In *Grass paint* the panel lists the Grass Profile's types with an on/off box and a Mix % slider, plus the resulting clump share. These write to the profile (undoable), so every stroke and the fill of each field using that profile re-blend. Strokes do not remember the mix they were painted with.

## Ground

The Ground mask (shared by the manager and its Grass Field) needs colliders. The panel and the cursor label say why nothing happens:

- empty palette,
- a collider on a layer outside the mask (*Add layer* fixes it),
- no collider at all: props land on a flat plane at the manager's height (shown in yellow); grass needs a collider.

New Foliage Forges include the `Ground` layer. Keep the layers of placed props out of the mask, or grass grows on trees (the inspector warns).
