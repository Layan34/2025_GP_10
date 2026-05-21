using System;
using UnityEngine;

namespace MKMTools.SimpleBarGraph.Data
{
    [Serializable]
    public class SerialiseBar : IBar
    {
        [SerializeField] private int id;
        [SerializeField] private float value;

        int IBar.ID => id;

        float IBar.Value => value;
    }
}