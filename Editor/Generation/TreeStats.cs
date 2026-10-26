using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meowdieval.FoliageForge
{
    /// <summary>Counts for one generated tree, shown in the window's budget bar.</summary>
    public readonly struct TreeStats
    {
        public readonly int Vertices;
        public readonly int Triangles;
        public readonly int Cards;
        public readonly int CardsDropped;
        public readonly int Branches;
        public readonly int MeshBytes;
        public readonly Vector3 Size;

        public TreeStats(int vertices, int triangles, int cards, int cardsDropped, int branches, int meshBytes, Vector3 size)
        {
            Vertices = vertices;
            Triangles = triangles;
            Cards = cards;
            CardsDropped = cardsDropped;
            Branches = branches;
            MeshBytes = meshBytes;
            Size = size;
        }
    }
}
