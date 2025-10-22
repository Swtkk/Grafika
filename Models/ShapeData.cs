using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Graf.Models
{
    [Serializable]
    public class ShapeData
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        //public string FillColor { get; set; }
        //public string StrokeColor { get; set; } 
        //public double StrokeThickness { get; set; }

        public ShapeData()
        {
            X = 0;
            Y = 0;
            Width = 0;
            Height = 0;
            //FillColor = "#FFFFFF";
            //StrokeColor = "#000000"; 
            //StrokeThickness = 5;
        }
    }

    [Serializable]
    public class LineData : ShapeData
    {
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
    }

    [Serializable]
    public class RectangleData : ShapeData
    {

    }

    [Serializable]
    public class CircleData : ShapeData
    {
        public double Diameter { get; set; }
    }

    [Serializable]
    [XmlInclude(typeof(LineData))]
    [XmlInclude(typeof(RectangleData))]
    [XmlInclude(typeof(CircleData))]
    public class CanvasData
    {
        public List<ShapeData> Shapes { get; set; } = new List<ShapeData>();
    }
}
