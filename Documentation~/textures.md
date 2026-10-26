# Foliage Forge texture list

All textures are **monochrome** (grayscale). The recipe colours (Bottom/Top, Moss Color) tint them, so one texture serves every colour scheme.

## Rules

- Grayscale PNG, 512 or 1024 px, power of two. Average brightness 0.6–0.9: the texture is multiplied with the colour, near-black darkens everything.
- Bark and moss textures must tile in both directions (mapped by the mesh UVs: u around the trunk, v along it).
- Leaf textures: white or light-grey leaves on a transparent background (alpha = cutout, cut-off at 0.5). Fill the tile; the card is a plain quad.
- Moss texture is only a breakup mask: white = moss reaches here first, black = moss stays away. Soft cloudy noise works for every tree.
- Assign them in the recipe under **Look**. Empty slots use a generated placeholder (leaf) or stay plain (bark, moss).

For now the presets use stand-ins from `Textures/Noise` in this package: Perlin_01 as moss mask everywhere, and per preset Vein_01 (Oak), Vein_03 (Pine), Streak_02 (Willow), Gabor_03 (Bush), Cracks_01 (Dead) as bark. Applying a preset swaps pack textures but keeps your own. Leaf cards come from `Textures/Shaded` (Oak sprite_0085, Pine 0099, Willow 0080, Bush 0090, Maple 0087, Spruce 0098, Poplar 0083, Acacia 0092, Hedge 0094, Dead none); `Textures/Flat` has the same sprites without baked shading (number minus 51). Swap them in the recipe's Look; presets keep your own texture. Presets can also load with no textures at all (the Textured toggle in the window, `ApplyPreset(preset, false)`): no leaf, bark or moss texture and flat solid cards.

## Per preset

| Preset | Leaf card (alpha) | Bark (tiling) | Moss mask |
|---|---|---|---|
| Oak | cluster of 5–7 broad lobed leaves | deep vertical furrows, rough ridges | blotchy clouds |
| Bush | dense cluster of small round leaves, optionally a few berries as lighter dots | thin twigs, fine fibres | light speckle |
| Maple, Poplar, Acacia, Hedge, Spruce | same leaf cards as above (Shaded sprites); bark from the noise pack | light speckle |
| Dead | none (no leaves) | cracked, peeling grey wood with visible grain | sparse patches |

## Shared extras

- 1 moss breakup mask (tileable cloud noise) covers all presets.
- A second leaf card per preset (different leaf arrangement) lets you vary trees later; the generator uses one texture per recipe today.
- Autumn, dry or snowy looks need no new textures: change Bottom/Top colours, Color Variation and Gradient Jitter.
