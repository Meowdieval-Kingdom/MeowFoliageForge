# Leaves, look and shaders

[Back to README](../README.md)

![Oak backlit](images/oak-backlit.png)

## Shader Graphs

- `FoliageForge` (leaf cards): height gradient (bottom to top colour), per-card flutter, scrolling leaf texture, global wind.
- `FoliageForgeBark` (trunk and branches): bark texture, height gradient, moss.
- Both share `Shaders/FoliageForge.hlsl`; the graphs call it through Custom Function nodes. Wind values: `_FF_WindDir` (xyz dir, w strength) and `_FF_WindParams` (speed, gust frequency, gust contrast).
- Grass uses a hand-written shader, see [Grass](grass.md).

The recipe's **Look** settings are written into the tool-made materials on every bake and live in the preview. Assign your own material in Look to opt out.

## Mesh layout

- Submesh 0 = wood, submesh 1 = leaf cards. Two draw calls per tree.
- Vertex colour: **R** height 0..1, **G** sway weight, **B** 1 on leaf cards (crown-interior AO when Renormalize is on), **A** per-branch random.
- UV1 on leaf cards: xyz pivot (object space), w flutter phase.
- UV2/UV3: card axes, written (+32 bytes per vertex) only when Billboard is not Off.

## Leaves

- Monochrome cutout texture, dark-to-light gradient (**Bias**) with per-card **Jitter**, per-branch **Color Variation** (0-1, default 0.5; warm yellow to cool green plus brightness).
- **Cards fit the texture**: the generator measures the opaque part of the leaf texture (alpha > 0.5) and maps the card to it, so transparent padding does not shrink the leaf. Card size is the longer side of that part.
- **Roll** / **Roll Jitter**: 0 keeps the texture top up, 180 hangs it, jitter 180 allows any rotation.
- **Anchor**: which texture point sits on the branch (default bottom middle = the stem). Use (0.5, 0.5) for star-shaped sprays. A small picture of the leaf under the fields shows a red dot; click or drag in it.
- **Billboard**: Off keeps fixed cards (default; fine with orthographic cameras). ViewAligned turns every card to the camera, YAxis only around the vertical axis.

## Bark

Monochrome texture on the mesh UVs (u around, v along the wood, so it moves with the sway), height gradient, **moss** on upward-facing surfaces (Moss Amount, Sharpness, Tiling, optional breakup mask).

## Stylized shading (leaf shader)

Gated by **Look > Stylized Shading** (off by default; sliders keep their values while off):

- **Shadow** Color / Amount / Softness: three-tone banding toward a coloured shadow.
- **Rim** Color / Strength / Power: fresnel edge, stronger on the lit side.
- **Hue Shift**: warm light, cool shadow.
- **Tree Variation**: per-tree tint from the object position.

## Translucency and wrap

- **Translucency** (amount, colour, focus): light through the leaves toward the camera, `pow(dot(view, -(L + N*0.3)), focus)`, strongest when the sun is behind the tree. Unshadowed emission, so it also lights leaves shaded by the crown above. Needs a directional main light. Use the **Backlit** button in the preview to see it; with a high sun it barely shows.
- **Wrap**: adds the part of `N.L * 0.5 + 0.5` that plain Lambert lacks as unshadowed emission from the main light, so shaded sides stay soft. 0 = off.

## Textures

See [Textures](textures.md) for rules and the per-preset list. Presets can load without any texture (the **Textured** toggle, `ApplyPreset(preset, false)`): flat solid cards.

## Performance notes

- Cards are fixed quads with blended normals; billboarding is optional.
- Renormalize and AO baking cost nothing at runtime.
- Keep the Budget caps in mind: Intersecting and Card Shape multiply triangles.
