using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Marks a baked grass batch (one grass type, a small circular patch) and remembers where it came from, so the window can load it back.</summary>
    [DisallowMultipleComponent]
    public sealed class GrassBatch : MonoBehaviour
    {
        public GrassProfile Profile;
        public int Variant;
    }
}
