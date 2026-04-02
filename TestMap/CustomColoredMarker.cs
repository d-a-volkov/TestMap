using GMap.NET;
using GMap.NET.WindowsForms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestMap
{
    public class CustomColoredMarker : GMapMarker
    {
        private readonly Color _color;

        public CustomColoredMarker(PointLatLng p, Color color)
            : base(p)
        {
            _color = color;
            Size = new System.Drawing.Size(10, 10); // Размер маркера
            Offset = new System.Drawing.Point(-Size.Width / 2, -Size.Height / 2); // Центрируем относительно точки
        }

        public override void OnRender(Graphics g)
        {
            var brush = new SolidBrush(_color);
            var pen = new Pen(Color.Black, 1); // Черная граница
            g.FillEllipse(brush, new Rectangle(Offset.X, Offset.Y, Size.Width, Size.Height));
            g.DrawEllipse(pen, new Rectangle(Offset.X, Offset.Y, Size.Width, Size.Height));
        }
    }
}
