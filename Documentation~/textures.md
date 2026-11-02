# Foliage Forge texture list

[Back to README](../README.md)

All textures are **monochrome** (grayscale). The recipe colours (Leaf Bottom/Top, Bark Bottom/Top, Moss Color) tint them, so one texture serves every colour scheme.

## Where to assign

| Texture | Field | Where |
|---|---|---|
| Leaf card | Leaf Texture | Look > Leaf color |
| Fill | Fill Texture (Advanced) | Look > Patches and fill |
| Bark | Bark Texture | Look > Bark and moss |
| Moss breakup | Moss Texture (Advanced) | Look > Bark and moss |

Empty slots use a generated placeholder (leaf) or stay plain (bark, moss).

## Rules

- Grayscale PNG, 512 or 1024 px, power of two. Average brightness 0.6-0.9: the texture is multiplied with the colour, near-black darkens everything.
- Bark and moss textures must tile in both directions (mapped by the mesh UVs: u around the trunk, v along it).
- Leaf textures: white or light-grey leaves on a transparent background (alpha = cutout, cut-off at 0.5). Fill the tile; the card is a plain quad. Cards are fitted to the opaque part, so padding does not shrink the leaf. A sprite sheet is split with Foliage > Cards > Leaf Shapes (Columns, Rows).
- Moss texture is only a breakup mask: white = moss reaches here first, black = moss stays away. Soft cloudy noise works for every tree.

## Preset stand-ins

Applying a preset swaps pack textures but keeps your own. Presets can also load with no textures at all (the window's **Textured** toggle, `ApplyPreset(preset, false)`): no leaf, bark or moss texture and flat solid cards.

| Slot | Source | Preset assignment |
|---|---|---|
| Moss mask | `Textures/Noise` Perlin_01 | every preset |
| Bark | `Textures/Noise` | Vein_01 (default), Vein_03 (Spruce), Cracks_01 (Dead) |
| Leaf card | `Textures/Shaded` | sprite_0085 (default), sprite_0084 (Spruce), sprite_0086 (Maple), none (Dead) |

`Textures/Flat` has the same sprites without baked shading (number minus 51). Swap them in the Look tab; presets keep your own texture.

## Art direction per preset

| Preset | Leaf card (alpha) | Bark (tiling) | Moss mask |
|---|---|---|---|
| Oak | cluster of 5-7 broad lobed leaves | deep vertical furrows, rough ridges | blotchy clouds |
| Bush | dense cluster of small round leaves, optionally a few berries as lighter dots | thin twigs, fine fibres | light speckle |
| Maple, Poplar, Hedge, Spruce | leaf cards from the Shaded sprites; bark from the noise pack | | light speckle |
| Dead | none (no leaves) | cracked, peeling grey wood with visible grain | sparse patches |

## Shared extras

- 1 moss breakup mask (tileable cloud noise) covers all presets.
- A second leaf card per preset (different leaf arrangement) lets you vary trees later; the generator uses one texture per recipe today.
- Autumn, dry or snowy looks need no new textures: change Leaf Bottom/Top, Color Variation and Gradient Jitter, or use Patches and fill.
