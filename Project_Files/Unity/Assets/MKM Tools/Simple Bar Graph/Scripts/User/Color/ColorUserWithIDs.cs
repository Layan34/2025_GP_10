using System.Collections.Generic;
using MKMTools.SimpleBarGraph.Data;
using UnityEngine;

namespace MKMTools.SimpleBarGraph.User.Color
{
    public class ColorUserWithIDs : ColorUser
    {
        [SerializeField] private List<UnityEngine.Color> colors;

        public override UnityEngine.Color GetColor(IBar bar, Vector2 minMax)
        {
            return colors[bar.ID];
        }
    }
}