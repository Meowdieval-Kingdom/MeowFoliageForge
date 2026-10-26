# Setup and validation

[Back to README](../README.md)

`Meowdieval > Foliage Forge >`:

- **Foliage Forge Validate** checks shaders, folders, wind, manager, the URP asset and the Ground layer. Results go to the Console.
- **Foliage Forge Setup Wizard** runs the same checks with one-click fixes. It opens by itself once per project, the first time the package loads after import. It never changes anything until you click a **Fix**.

## Row severity

| Severity | Rows |
|---|---|
| **Required** (a shader or feature breaks) | Main light per pixel; LOD Cross Fade; MSAA 4x when a Grass Profile uses alpha to coverage |
| **Recommended** | SRP Batcher; Depth Texture; main light shadows; the `Ground` layer and the manager's Ground mask; additional lights per pixel when a profile uses them |
| **Optional** | Shadow distance 30-150 m; soft shadows |

Pipeline rows are checked on the current quality level's URP asset; tick **all quality levels** to cover every one.

## What fixes do

Fixes that edit a shared asset (the URP asset, Tags and Layers) ask once, log `[Foliage Forge] Set X: old -> new` to the Console, and are undoable. Rendering path, GPU Resident Drawer, HDR and render scale are never touched.

## Troubleshooting

| Symptom | Check |
|---|---|
| Shaders look wrong or missing | Run Validate; check the URP asset is the active pipeline |
| Nothing paints | Ground mask and colliders, see [Brush](brush.md#ground) |
| Grass grows on trees | Placed-prop layers are inside the Ground mask |
| LODs pop instead of fading | LOD Cross Fade off in the URP asset |
| No sway | No **Foliage Wind** component in the scene |
| Translucency invisible | Needs a directional main light, sun behind the tree |
