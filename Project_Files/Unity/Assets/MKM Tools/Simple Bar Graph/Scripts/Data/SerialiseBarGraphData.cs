using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MKMTools.SimpleBarGraph.Data
{
    [Serializable]
    public class SerialiseBarGraphData : IBarGraphData
    {
        [Serializable]
        private class BarSet
        {
            public string bottomValue;
            public List<SerialiseBar> bars;
        }

        [SerializeField] private List<BarSet> barSets;
        private IDictionary<string, ICollection<IBar>> _barSets;

        IDictionary<string, ICollection<IBar>> IBarGraphData.BarSets
        {
            get
            {
                _barSets ??= barSets.ToDictionary(
                    x => x.bottomValue,
                    x => (ICollection<IBar>)x.bars.Cast<IBar>().ToList()
                );

                return _barSets;
            }
        }

        Vector2 IBarGraphData.GetMinMaxValues()
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            foreach (var barValue in _barSets.Values.SelectMany(x => x).Select(x => x.Value))
            {
                if (barValue < min) min = barValue;
                if (barValue > max) max = barValue;
            }

            return new Vector2(min, max);
        }
    }
}