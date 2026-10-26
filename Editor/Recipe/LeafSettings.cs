using System;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Leaf card placement and look. Cards are fixed quads, no billboarding.</summary>
    [Serializable]
    public class LeafSettings
    {
        [Tooltip("Clusters scatters cards around the end branches. Fronds lays long cards along them (conifers) and puts an upright leader on the trunk top. Hanging drops strands of cards straight down from the branches (willow); Roll 180 hangs each leaf tip down.")]
        public LeafLayout Layout = LeafLayout.Clusters;
        [Range(0, 200)] public int CardsPerBranch = 14;
        [Tooltip("Blobs layout: leaf groups per blob. 0 = automatic: enough groups to blanket each blob (see Coverage). Each group is Group Quads intersecting quads.")]
        [Range(0, 400)] public int CardsPerBlob = 60;
        [Tooltip("Blobs layout, automatic group count: layers of leaf area laid over each blob's surface. Higher = denser, fewer gaps.")]
        [Range(0.5f, 4f)] public float Coverage = 1.8f;
        [Tooltip("Blobs layout: 0 = every group sits on the blob surface (hollow shell), 1 = groups also fill the blob's interior so it reads as a solid mass.")]
        [Range(0f, 1f)] public float GroupVolume = 0.5f;
        [Tooltip("Blobs layout: pushes every group out of its blob along the blob normal, in metres. Gives the crown a fuzzy, leafy outline.")]
        [Range(0f, 0.5f)] public float NormalOffset;
        [Tooltip("Where leaves gather: -1 = underneath (south) of each blob, 0 = all around, 1 = on top. Blobs layout only.")]
        [Range(-1f, 1f)] public float PlacementBias;
        [Tooltip("Blobs layout: intersecting quads per leaf group, fanned evenly around the group's up axis (the blob surface normal).")]
        [Range(1, 6)] public int GroupQuads = 3;
        [Tooltip("Blobs layout: how far a group's anchor sits below the blob surface, as a fraction of the group size. 0 puts the anchor on the surface.")]
        [Range(0f, 0.5f)] public float GroupDepth = 0.1f;
        [Tooltip("Blobs layout: random jitter of each group's anchor along the surface, which also fuzzes the outline. 0 = a regular spread.")]
        [Range(0f, 1f)] public float GroupJitter = 0.15f;
        [Tooltip("Blobs layout: angle in degrees between a group's shared axis and the blob surface normal. 0 = quads stand on the surface like fins (edge-on from outside), 90 = the axis lies along the surface so a quad can lie flat and face outward.")]
        [Range(0f, 90f)] public float GroupTilt = 70f;
        [Tooltip("Blobs layout: random +- degrees added to Group Tilt per group. Gives a few edge-on cards for a spiky outline.")]
        [Range(0f, 90f)] public float GroupTiltJitter = 25f;
        [Tooltip("Blobs layout: 0 = every quad keeps its own normals, 1 = every vertex of a group uses the blob surface normal at the group's anchor (smooth, ball-like shading).")]
        [Range(0f, 1f)] public float Smoothness = 1f;
        [Tooltip("Blobs layout: how many blobs Auto Blobs creates, spread over the branch tips.")]
        [Range(1, 24)] public int BlobCount = 7;
        [Tooltip("Blobs layout: smallest and largest blob radius in metres that Auto Blobs may create.")]
        public Vector2 BlobSize = new Vector2(0.4f, 3f);
        [Tooltip("Blobs layout: Auto Blobs inflates every blob by this factor so neighbours overlap (1.3 = about 30-50% overlap).")]
        [Range(0.5f, 2f)] public float BlobOverlap = 1.3f;
        [Tooltip("Blobs layout: how strongly Auto Blobs spreads blob sizes, up to about 3:1 largest to smallest, with the top blob largest. 0 = keep the natural sizes.")]
        [Range(0f, 1f)] public float BlobSizeSpread = 1f;
        [Tooltip("Blobs layout: flat underside Auto Blobs gives each blob (a shelf under a rounded dome). 0 = round.")]
        [Range(0f, 1f)] public float BlobFlatBelly = 0.5f;
        [Tooltip("Blobs layout: how much narrower Auto Blobs makes one end of each blob (egg shape). 0 = both ends alike.")]
        [Range(0f, 1f)] public float BlobTaper = 0.3f;
        [Tooltip("Blobs layout: 0 = Auto Blobs makes round blobs, 1 = every blob is stretched along the direction of the branches that feed it.")]
        [Range(0f, 1f)] public float BranchFollow = 0.7f;
        [Tooltip("Hanging layout: how far each strand drops below its branch. Cards per strand follow from this and the card size.")]
        [Min(0.1f)] public float StrandLength = 1.5f;
        [Min(0.05f)] public float ClusterRadius = 0.55f;
        public Vector2 CardSize = new Vector2(0.7f, 1.1f);
        [Min(0.2f)] public float Aspect = 1f;
        [Tooltip("Where along each end branch cards start, 0 at the base and 1 at the tip. Branch layouts only; blobs have no branches.")]
        [Range(0f, 1f)] public float TipBias = 0.45f;
        [Tooltip("How much lighting follows the round crown instead of each flat card. Scales Renormalize and Puffiness too; 0 shades every card flat.")]
        [Range(0f, 1f)] public float NormalBlend = 0.85f;
        [Tooltip("Tilts cards toward the sky so a top-down camera sees them.")]
        [Range(0f, 1f)] public float UpBias = 0.35f;
        [Tooltip("0 scatters cards around the branches. 1 grows each leaf from a point on a branch, pointing along it and fanning out.")]
        [Range(0f, 1f)] public float Attach = 0f;
        [Tooltip("Where the leaf is attached, in the opaque part of the texture: (0.5, 0) = bottom middle (the stem), (0.5, 0.5) = centre.")]
        public Vector2 Anchor = new Vector2(0.5f, 0f);
        [Tooltip("Turn of the card around its normal in degrees. 0 keeps the texture's top pointing up, 180 hangs it (willow).")]
        [Range(-180f, 180f)] public float Roll = 0f;
        [Tooltip("Random spread around Roll. 180 = any rotation, 0 = every card the same way up.")]
        [Range(0f, 180f)] public float RollJitter = 180f;
        public bool CrownOnTrunkTop = true;
        [Tooltip("Also grow leaves along the level 1 branches, not only the twigs, so leaves hang below the crown.")]
        public bool OnInnerBranches;
        [Tooltip("Adds a second card through every card, turned 90 degrees around its up axis (crossed quads). Fuller from every side, but doubles the cards.")]
        public bool Intersecting = true;
        [Tooltip("Quads per leaf group, fanned evenly around the group's up axis so it reads from any angle (3 = a star of three). 0 follows Intersecting (off = 1, on = 2). Multiplies the card count.")]
        [Range(0, 6)] public int CardsPerCluster = 0;
        [Tooltip("Blobs layout: pulls cards toward the branch tips that lie inside their blob, so the leaf groups hang on the ends of the branches. 0 = off.")]
        [Range(0f, 1f)] public float BranchTipBias = 0f;
        [Tooltip("Off keeps the cards fixed. ViewAligned faces every card to the camera. YAxis only turns them around the vertical axis and keeps their tilt. Costs 32 bytes per vertex. Billboards flatten folded or curled cards.")]
        public BillboardMode Billboard = BillboardMode.Off;

        [Header("Crown Shape")]
        [Tooltip("Silhouette the leaves are pulled onto, sized to the crown. None keeps them where the branches put them.")]
        public CrownShape Envelope = CrownShape.None;
        [Tooltip("How hard leaves are pulled onto the envelope, and how far branches sticking out of it are shortened. 1 puts every leaf on the silhouette shell.")]
        [Range(0f, 1f)] public float EnvelopeStrength = 0.6f;

        [Header("Clumps")]
        [Tooltip("Groups the leaves into this many puffy clumps spread over the crown. 0 = off.")]
        [Range(0, 32)] public int Clumps;
        [Tooltip("How tightly leaves gather toward their clump's centre.")]
        [Range(0f, 1f)] public float ClumpGather = 0.35f;
        [Tooltip("Shades each clump like a ball: normals point out from the clump centre. Applied after Renormalize.")]
        [Range(0f, 1f)] public float Puffiness = 0.6f;
        [Tooltip("Each clump gets its own tint (strength = Look / Color Variation) instead of one per branch.")]
        public bool TintPerClump = true;
        [Tooltip("One clump at the tip of every end branch, with bare wood leading into it (fluffy pines, poplars). Ignores Clumps.")]
        public bool TipClumps;
        [Tooltip("0 fills each clump with cards, 1 puts them on its surface so it reads as a round puff.")]
        [Range(0f, 1f)] public float Shell;
        [Tooltip("Squashes the underside of each clump flat, for layered leaf shelves.")]
        [Range(0f, 1f)] public float FlattenBottom;
        [Tooltip("Clump height relative to its width.")]
        [Range(0.3f, 1.5f)] public float ClumpHeight = 1f;
    }
}
