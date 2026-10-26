# Trees

[Back to README](../README.md)

A tree is a `TreeRecipe`: a seed plus settings foldouts. Structure features below all default to **off**, so older recipes build the same mesh. They use hashes and Perlin noise instead of the seed's random stream, so turning one on does not reshuffle the rest of the tree.

| Maple | Spruce |
|---|---|
| ![Maple](images/preset-maple.png) | ![Spruce](images/preset-spruce.png) |

## Presets

- **Oak**: blob canopy filled with small cards, split stems and roots.
- **Maple**: blob canopy, multi-stem trunk.
- **Spruce**: fronds layout (conifer sprays).
- **Dead**: broken stubs and exposed roots, no leaves.

## Trunk

- **Character**: Taper Power, Twist (bark spirals), Root Lobes / Depth / Height (buttress flare at the base), Gnarl (Perlin bumps along the wood).
- **Noise**: Noise Bend and Frequency bend the wood along Perlin noise; the base stays put.
- **Split**: Splits cuts the trunk at Split Height into Splits + 1 stems that lean out by Split Angle. Braid turns them around each other (low split + braid = braided multi-stem). Branches and crown tops spread over the stems.
- **Roots**: exposed, half-buried roots that arch out of the base and curl up at the tip (Length, Radius, Wander, Curl).

## Branches

- **Noise**: Noise Bend, Frequency, and **Length Noise** (smooth length variation up the trunk).
- **Character**: **Asymmetry** grows the lean side longer. **Broken** snaps that share of level-1 branches into short jagged stubs without leaves.

## Leaf layout

Set under **Leaves > Layout**:

| Layout | Result |
|---|---|
| **Clusters** (default) | Cards scattered around each end branch. **Attach** 0 scatters, 1 grows every leaf from a branch point |
| **Fronds** | Long cards along every end branch (conifer sprays). CardsPerBranch = fronds per branch, fanned by Roll Jitter, Fold droops them, plus a crossed leader on top |
| **Hanging** | Strands of cards (Strand Length) straight down from each end branch; Roll 180 hangs leaves tip-down |
| **Blobs** | Fills hand-editable ellipsoids (see below) |

Other leaf options: **Tip Clumps** (one puff per branch tip; **Shell** pushes cards to the surface, **Clump Height** squashes, **Flatten Bottom** makes a shelf look), **On Inner Branches** (leaves on level-1 branches too), **Intersecting** (a second card through each one, turned 90 degrees; on by default, doubles the cards and the budget counts both).

### Crown shape

- **Envelope** (Sphere, Dome, Cone, Column, Umbrella) pulls leaves onto that silhouette and prunes level-1 branches to it; Strength 0..1.
- **Clumps** gathers cards into N puffy blobs (**Gather**), bends normals outward from each centre (**Puffiness**), tints per blob (**Tint Per Clump**). 0 = off.

## Canopy blobs

Layout **Blobs** fills ellipsoid volumes (recipe > Canopy Blobs) with *Cards Per Blob* cards. Shell, Flatten Bottom and Puffiness shape each blob.

In the window's **Blobs** view mode:

- **Auto Blobs** spreads *Blob Count* blobs over the branch tips. An empty list is suggested on the fly.
- Drag a blob to move it. Scroll resizes it (Ctrl: height only). Delete key or button removes it. **Add Blob** adds one.
- Select a blob to see its **cage** (26 points). Drag a point to reshape the blob (Shift: neighbours follow half as far); cards then fill the cage. **Reset Shape** returns to an ellipsoid.

## Leaf shapes

- **Sprite Sheet** (Columns/Rows): each cell is trimmed on its own, empty cells skipped, every card picks a random cell.
- **Card Shape**: cards become grids (Width/Length Segments) that **Fold** along the middle, **Curl** along the length and **Twist**, with per-card Jitter. Shaped cards cost `2 * width * length` triangles each. LOD1 halves the segments, LOD2 uses flat quads. Billboards flatten shaped cards.

## Renormalize

The **Renormalize** foldout replaces leaf normals with a smoothed density-proxy shell (Tightness, Strength) and bakes crown-interior AO into leaf vertex colour B (AOStrength). It runs at generation time and costs nothing at runtime.

## Budgets

`Budget.MaxCards` is a hard cap. The window shows triangle count against `Budget.MaxTriangles`; the budget drops cards sooner when shaped cards or Intersecting are on. `Budget.AddTrunkCollider` adds a capsule collider to the baked prefab.
