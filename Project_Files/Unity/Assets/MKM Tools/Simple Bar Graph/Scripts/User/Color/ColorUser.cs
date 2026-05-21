using MKMTools.SimpleBarGraph.Data;
using UnityEngine;

namespace MKMTools.SimpleBarGraph.User.Color
{
    public abstract class ColorUser : MonoBehaviour
    {
        public abstract UnityEngine.Color GetColor(IBar bar, Vector2 minMax);
    }
}