# Setup and validation

[Back to README](../README.md)

`Meowdieval > Foliage Forge >`:

- **Foliage Forge Validate** runs every check and writes the failing ones to the Console (`[Foliage Forge] <Severity>: <name>: <detail>`, a warning for Required and Recommended, a log line for Optional), then a pass count.
- **Foliage Forge Setup Wizard** runs the same checks in a window with one-click fixes. It opens by itself once per project, the first time the package loads after import. It never changes anything until you click a **Fix**.

## Wizard layout

The wizard is a UI Toolkit window (minimum 420 x 400 px).

- **Header**: the current quality level and its pipeline asset, and a toggle **Check and fix the URP assets of all quality levels**. Without it, pipeline rows cover the current quality level's URP asset only.
- **Rows**: failing rows are grouped under `<Severity> (n to fix)`. Each row is a card with a severity pill, the check name, the current and target value (`current -> target`) and a one-line reason. A **Fix** button on the row applies that fix alone; pipeline rows read `Fix in <asset names>`.
- **Passed**: passing rows are folded into a `n checks passed` foldout, each with a **Passed** pill. When nothing fails, the list says so.
- **Footer**: **Fix All Required**, **Fix All Recommended** (fixes Required and Recommended rows; Optional rows change only through their own Fix button) and **Open Foliage Forge Editor**. Fix buttons are disabled when there is nothing to fix.
- A note lists what is left alone on purpose: rendering path, GPU Resident Drawer, HDR and render scale.

The pill always carries text (Passed / Required / Recommended / Optional), so the state does not rest on colour alone.

## Row severity

| Severity | Meaning | Rows |
|---|---|---|
| **Required** | A shader or feature breaks | Universal Render Pipeline active; tree and grass shaders imported (Fix: Reimport); main light per pixel; LOD Cross Fade; MSAA 4x when a Grass Profile uses alpha to coverage |
| **Recommended** | Works, but worse | Bake output folder; Grass Profile asset; Foliage Forge Manager with Foliage Wind in the open scene; SRP Batcher; Depth Texture; main light shadows; the `Ground` layer and the manager's Ground mask; MSAA (when no profile uses alpha to coverage); additional lights per pixel when a profile uses them |
| **Optional** | Polish | Shadow distance 30-150 m (fix sets 50 m); soft shadows; additional lights per pixel when no profile uses them |

## What fixes do

Fixes that edit a shared asset (the URP asset, Tags and Layers) ask once per click, log `[Foliage Forge] Set X: old -> new` to the Console, and are undoable. Rendering path, GPU Resident Drawer, HDR and render scale are never touched.

## Troubleshooting

| Symptom | Check |
|---|---|
| Shaders look wrong or missing | Run Validate; check the URP asset is the active pipeline |
| Nothing paints | Ground mask and colliders, see [Brush](brush.md#ground) |
| Grass grows on trees | Placed-prop layers are inside the Ground mask |
| LODs pop instead of fading | LOD Cross Fade off in the URP asset |
| No sway | No **Foliage Wind** component in the scene |
| Translucency invisible | Needs a directional main light, sun behind the tree |
