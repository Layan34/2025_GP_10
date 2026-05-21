using MKMTools.SimpleBarGraph.Data;
using UnityEngine;

namespace MKMTools.SimpleBarGraph.User.Color
{
    public class ColorUserGradient : ColorUser
    {
        [SerializeField] private UnityEngine.Color bottom;
        [SerializeField] private UnityEngine.Color top;

        public override UnityEngine.Color GetColor(IBar bar, Vector2 minMax)
        {
            float lerpValue = (bar.Value - minMax.x) / (minMax.y - minMax.x);
            return UnityEngine.Color.Lerp(bottom, top, lerpValue);
        }
    }
}