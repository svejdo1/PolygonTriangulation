/*******************************************************************************
 * Copyright 2011 Mario Zechner <badlogicgames@gmail.com>, Nathan Sweet <nathan.sweet@gmail.com>
 * Original source https://github.com/libgdx/libgdx/blob/master/gdx/src/com/badlogic/gdx/math/EarClippingTriangulator.java
 * Port to C# - Ondrej Svejdar
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 ******************************************************************************/

using System;
using System.Collections.Generic;

namespace Barbar.PolygonTriangulation
{
    /// <summary>
    /// A simple implementation of the ear cutting algorithm to triangulate simple polygons without holes. For more information:
    /// <ul>
    ///   <li><a href="http://cgm.cs.mcgill.ca/~godfried/teaching/cg-projects/97/Ian/algorithm2.html">http://cgm.cs.mcgill.ca/~godfried/teaching/cg-projects/97/Ian/algorithm2.html</a></li>
    ///   <li><a href="http://www.geometrictools.com/Documentation/TriangulationByEarClipping.pdf">http://www.geometrictools.com/Documentation/TriangulationByEarClipping.pdf</a></li>
    /// </ul>
    /// If the input polygon is not simple (self-intersects), there will be output but it is of unspecified quality (garbage in, garbage out).
    /// </summary>
    /// <remarks></remarks>

    public class EarClippingTriangulator
    {
        private readonly List<short> indices = new List<short>();
        private float[] vertices;
        private int vertexCount;
        private readonly List<VertexType> vertexTypes = new List<VertexType>();
        private readonly List<short> triangles = new List<short>();


        /// <summary>
        /// <see cref="ComputeTriangles(float[], int, int)"/> 
        /// </summary>
        /// <param name="vertices"></param>
        /// <returns></returns>
        public IList<short> ComputeTriangles(float[] vertices)
        {
            return ComputeTriangles(vertices, 0, vertices.Length);
        }

        /// <summary>
        /// Triangulates the given (convex or concave) simple polygon to a list of triangle vertices.
        /// </summary>
        /// <param name="vertices">vertices pairs describing vertices of the polygon, in either clockwise or counterclockwise order.</param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns>triples of triangle indices in clockwise order. Note the returned array is reused for later calls to the same method.</returns>
        public IList<short> ComputeTriangles(float[] vertices, int offset, int count)
        {
            this.vertices = vertices;
            int vertexCount = this.vertexCount = count / 2;
            int vertexOffset = offset / 2;

            indices.Clear();
            indices.Capacity = vertexCount;
            for (var i = 0; i < vertexCount; i++)
            {
                indices.Add(0);
            }

            if (AreVerticesClockwise(vertices, offset, count))
            {
                for (short i = 0; i < vertexCount; i++)
                {
                    indices[i] = (short)(vertexOffset + i);
                }
            }
            else
            {
                for (int i = 0, n = vertexCount - 1; i < vertexCount; i++)
                {
                    // Reversed.
                    indices[i] = (short)(vertexOffset + n - i); 
                }
            }

            vertexTypes.Clear();
            vertexTypes.Capacity = vertexCount;
            for (int i = 0, n = vertexCount; i < n; ++i)
                vertexTypes.Add(ClassifyVertex(i));

            // A polygon with n vertices has a triangulation of n-2 triangles.
            triangles.Clear();
            triangles.Capacity = Math.Max(0, vertexCount - 2) * 3;
            Triangulate();
            return triangles;
        }

        private void Triangulate()
        {
            while (vertexCount > 3)
            {
                int earTipIndex = FindEarTip();
                CutEarTip(earTipIndex);

                // The type of the two vertices adjacent to the clipped vertex may have changed.
                int previousIndex = PreviousIndex(earTipIndex);
                int nextIndex = earTipIndex == vertexCount ? 0 : earTipIndex;
                vertexTypes[previousIndex] = ClassifyVertex(previousIndex);
                vertexTypes[nextIndex] = ClassifyVertex(nextIndex);
            }

            if (vertexCount == 3)
            {
                triangles.Add(indices[0]);
                triangles.Add(indices[1]);
                triangles.Add(indices[2]);
            }
        }

        private VertexType ClassifyVertex(int index)
        {
            int previous = indices[PreviousIndex(index)] * 2;
            int current = indices[index] * 2;
            int next = indices[NextIndex(index)] * 2;
            return ComputeSpannedAreaSign(vertices[previous], vertices[previous + 1], vertices[current], vertices[current + 1],
                vertices[next], vertices[next + 1]);
        }

        private int FindEarTip()
        {
            for (int i = 0; i < vertexCount; i++)
            {
                if (IsEarTip(i))
                {
                    return i;
                }
            }

            // Desperate mode: if no vertex is an ear tip, we are dealing with a degenerate polygon (e.g. nearly collinear).
            // Note that the input was not necessarily degenerate, but we could have made it so by clipping some valid ears.

            // Idea taken from Martin Held, "FIST: Fast industrial-strength triangulation of polygons", Algorithmica (1998),
            // http://citeseerx.ist.psu.edu/viewdoc/summary?doi=10.1.1.115.291

            // Return a convex or tangential vertex if one exists.
            for (int i = 0; i < vertexCount; i++)
            {
                if (vertexTypes[i] != VertexType.Concave)
                {
                    return i;
                }
            }

            // If all vertices are concave, just return the first one.
            return 0; 
        }

        private bool IsEarTip(int earTipIndex)
        {
            if (vertexTypes[earTipIndex] == VertexType.Concave)
            {
                return false;
            }

            int previousIndex = PreviousIndex(earTipIndex);
            int nextIndex = NextIndex(earTipIndex);
            int p1 = indices[previousIndex] * 2;
            int p2 = indices[earTipIndex] * 2;
            int p3 = indices[nextIndex] * 2;
            float p1x = vertices[p1], p1y = vertices[p1 + 1];
            float p2x = vertices[p2], p2y = vertices[p2 + 1];
            float p3x = vertices[p3], p3y = vertices[p3 + 1];

            // Check if any point is inside the triangle formed by previous, current and next vertices.
            // Only consider vertices that are not part of this triangle, or else we'll always find one inside.
            for (int i = NextIndex(nextIndex); i != previousIndex; i = NextIndex(i))
            {
                // Concave vertices can obviously be inside the candidate ear, but so can tangential vertices
                // if they coincide with one of the triangle's vertices.
                if (vertexTypes[i] != VertexType.Convex)
                {
                    int v = indices[i] * 2;
                    float vx = vertices[v];
                    float vy = vertices[v + 1];
                    // Because the polygon has clockwise winding order, the area sign will be positive if the point is strictly inside.
                    // It will be 0 on the edge, which we want to include as well.
                    // note: check the edge defined by p1->p3 first since this fails _far_ more then the other 2 checks.
                    if (ComputeSpannedAreaSign(p3x, p3y, p1x, p1y, vx, vy) >= 0)
                    {
                        if (ComputeSpannedAreaSign(p1x, p1y, p2x, p2y, vx, vy) >= 0)
                        {
                            if (ComputeSpannedAreaSign(p2x, p2y, p3x, p3y, vx, vy) >= 0)
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            return true;
        }

        private void CutEarTip(int earTipIndex)
        {
            triangles.Add(indices[PreviousIndex(earTipIndex)]);
            triangles.Add(indices[earTipIndex]);
            triangles.Add(indices[NextIndex(earTipIndex)]);

            indices.RemoveAt(earTipIndex);
            vertexTypes.RemoveAt(earTipIndex);
            vertexCount--;
        }

        private int PreviousIndex(int index)
        {
            return (index == 0 ? vertexCount : index) - 1;
        }

        private int NextIndex(int index)
        {
            return (index + 1) % vertexCount;
        }

        static private bool AreVerticesClockwise(float[] vertices, int offset, int count)
        {
            if (count <= 2) return false;
            float area = 0, p1x, p1y, p2x, p2y;
            for (int i = offset, n = offset + count - 3; i < n; i += 2)
            {
                p1x = vertices[i];
                p1y = vertices[i + 1];
                p2x = vertices[i + 2];
                p2y = vertices[i + 3];
                area += p1x * p2y - p2x * p1y;
            }
            p1x = vertices[offset + count - 2];
            p1y = vertices[offset + count - 1];
            p2x = vertices[offset];
            p2y = vertices[offset + 1];
            return area + p1x * p2y - p2x * p1y < 0;
        }

        static private VertexType ComputeSpannedAreaSign(float p1x, float p1y, float p2x, float p2y, float p3x, float p3y)
        {
            float area = p1x * (p3y - p2y);
            area += p2x * (p1y - p3y);
            area += p3x * (p2y - p1y);
            return (VertexType)Math.Sign(area);
        }
    }
}