using System.Collections.Generic;
using UnityEngine;

namespace MKMTools.SimpleBarGraph.Data
{
    public interface IBarGraphData
    {
        public IDictionary<string, ICollection<IBar>> BarSets { get; }
        public Vector2 GetMinMaxValues();
    }
}