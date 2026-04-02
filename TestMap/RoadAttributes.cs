using GMap.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestMap
{
    public class RoadAttributes
    {
        public long Id { get; set; }
        public long Source { get; set; } // Узел начала (пока будет -1, если не вычисляем)
        public long Target { get; set; } // Узел конца (пока будет -1, если не вычисляем)
        public double Reverse { get; set; }
        public bool OneWay { get; set; }
        public string Type { get; set; }
        public int Priority { get; set; }
        public double MaxSpeedForward { get; set; }
        public double MaxSpeedBackward { get; set; }
        public double Length { get; set; }
        public List<PointLatLng> Geometry { get; set; }
    }
}
