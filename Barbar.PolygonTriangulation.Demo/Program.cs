using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace Barbar.PolygonTriangulation.Demo
{
    static class Program
    {
        const int WIDTH = 800;
        const int HEIGHT = 600;
        const int OFFSET = 5;
        static readonly EarClippingTriangulator _triangulator = new EarClippingTriangulator();

        static float[] Polygon01()
        {
            return new float[] { OFFSET, OFFSET, WIDTH - OFFSET, 0, WIDTH - OFFSET, HEIGHT - OFFSET, OFFSET, HEIGHT - OFFSET };
        }

        static void DumpPolygon(float[] polygon, string fileName, ImageFormat format)
        {
            var triangles = _triangulator.ComputeTriangles(polygon);

            using (var image = new Bitmap(WIDTH, HEIGHT, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(image))
                {
                    var points = new PointF[3];
                    int a, b, c;
                    for (var i = 0; i < triangles.Count; i += 3)
                    {
                        a = triangles[i];
                        b = triangles[i + 1];
                        c = triangles[i + 2];

                        points[0] = new PointF(polygon[a * 2], polygon[a * 2 + 1]);
                        points[1] = new PointF(polygon[b * 2], polygon[b * 2 + 1]);
                        points[2] = new PointF(polygon[c * 2], polygon[c * 2 + 1]);
                        graphics.DrawPolygon(Pens.Green, points);
                    }
                }
                image.Save(fileName, format);
            }
        }
        
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            DumpPolygon(Polygon01(), "dump01.png", ImageFormat.Png);
        }
    }
}
