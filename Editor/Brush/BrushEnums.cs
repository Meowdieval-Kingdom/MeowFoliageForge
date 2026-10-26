using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>What the brush paints: grass strokes on the Grass Field, or palette prefabs under the Props root.</summary>
    public enum BrushTarget { Grass, Props }

    /// <summary>Axes a random prop rotation is applied around.</summary>
    [System.Flags] public enum BrushRotationAxes { None = 0, X = 1, Y = 2, Z = 4 }

    /// <summary>Frame the random rotation turns in: world axes, or axes tilted to the ground normal under the prop.</summary>
    public enum BrushRotationMode { World, AlignToSurface }

    /// <summary>What the cursor ray found: a ground collider, a collider on a layer outside the Ground mask, only the flat fallback plane, or nothing.</summary>
    public enum BrushGround { None, Collider, WrongLayer, Plane }
}
