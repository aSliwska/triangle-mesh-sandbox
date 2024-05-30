using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Globalization;
using System.Linq;
using Microsoft.SqlServer.Server;

namespace TriangleMeshNamespace
{
    public partial class UserDefinedFunctions
    {
        /////////////////////////// PUBLIC FUNCTIONS ///////////////////////////

        [Microsoft.SqlServer.Server.SqlFunction(IsDeterministic = true, IsPrecise = false, DataAccess = DataAccessKind.None)]
        public static double GetDistance(Point3D a, Point3D b)
        {
            return vectorLength(a.getVectorTo(b));
        }

        [Microsoft.SqlServer.Server.SqlFunction(IsDeterministic = true, IsPrecise = false, DataAccess = DataAccessKind.None)]
        public static double GetTriangleArea(Point3D a, Point3D b, Point3D c)
        {
            return vectorLength(crossProduct(a.getVectorTo(b), a.getVectorTo(c))) * 0.5;
        }

        [Microsoft.SqlServer.Server.SqlFunction(IsDeterministic = true, IsPrecise = false, DataAccess = DataAccessKind.None)]
        public static bool CheckIfPointInTriangle(Point3D p, Point3D a, Point3D b, Point3D c)
        {
            Point3D AB = a.getVectorTo(b);
            Point3D AC = a.getVectorTo(c);
            Point3D planePerpendicularVectorN = crossProduct(AB, AC);
            Point3D AP = a.getVectorTo(p);
            double nLength = vectorLength(planePerpendicularVectorN);

            return isPointOnPlane(planePerpendicularVectorN, AP, nLength) && isPointWithinTriangleBounds(AB, AC, AP, planePerpendicularVectorN, nLength);
        }

        // edges and corners don't cause collisions
        // if ray ends or begins on the triangle, it IS counted as a collision
        [Microsoft.SqlServer.Server.SqlFunction(IsDeterministic = true, IsPrecise = true, DataAccess = DataAccessKind.None)]
        public static bool DoesRayIntersect(Point3D a, Point3D b, Point3D c, Point3D rayOrigin, Point3D farPointOnRay)
        {
            int temp = volumeSign(a, b, rayOrigin, farPointOnRay);
            return (volumeSign(a, b, c, rayOrigin) != volumeSign(a, b, c, farPointOnRay))
                   && (temp == volumeSign(b, c, rayOrigin, farPointOnRay))
                   && (temp == volumeSign(c, a, rayOrigin, farPointOnRay));
        }

        // for calculating entire volume
        [Microsoft.SqlServer.Server.SqlFunction(IsDeterministic = true, IsPrecise = false, DataAccess = DataAccessKind.None)]
        public static double CalculateMeshVolume(string meshString)
        {
            // id,0.5,-0.4,1 # id,0.5,-0.4,1 # id,0.5,-0.4,1 ;
            List<List<(int id, Point3D point)>> mesh = unpackMesh(meshString);

            List<List<Point3D>> orderedMesh = orderVertices(ref mesh);

            Point3D hookPoint = orderedMesh[0][0];
            double volume = 0;

            foreach (List<Point3D> triangle in orderedMesh) 
            { 
                volume += signedTetrahedronVolume(triangle[0], triangle[1], triangle[2], hookPoint);
            }

            return Math.Abs(volume);
        }

        [Microsoft.SqlServer.Server.SqlFunction(IsDeterministic = true, IsPrecise = true, DataAccess = DataAccessKind.None)]
        public static Point3D CreatePoint3D(double x, double y, double z)
        {
            return new Point3D(x, y, z);
        }

        // in a manifold mesh all edges are connected to 2 triangles and all vertices are inside closed fans
        [Microsoft.SqlServer.Server.SqlFunction(IsDeterministic = false, IsPrecise = true, DataAccess = DataAccessKind.None)]
        public static bool IsMeshIsManifold(SqlString trianglesString)
        {
            List<List<int>> triangles = unpackTriangles(trianglesString.Value);

            return areAllEdgesConnectedToTwoTriangles(triangles) && doAllVerticesFormClosedFans(getAdjacencyList(triangles));
        }

        


        /////////////////////////// HELPER FUNCTIONS ///////////////////////////

        private static double dotProduct(Point3D vecU, Point3D vecV)
        {
            return vecU.X * vecV.X + vecU.Y * vecV.Y + vecU.Z * vecV.Z;
        }

        private static Point3D crossProduct(Point3D vecU, Point3D vecV)
        {
            return new Point3D(vecU.Y * vecV.Z - vecU.Z * vecV.Y, vecU.Z * vecV.X - vecU.X * vecV.Z, vecU.X * vecV.Y - vecU.Y * vecV.X);
        }

        // vector's euclidean norm
        private static double vectorLength(Point3D vec) 
        {
            return Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y + vec.Z * vec.Z);
        }

        private static int volumeSign(Point3D a, Point3D b, Point3D c, Point3D d)
        {
            return Math.Sign(dotProduct(crossProduct(a.getVectorTo(b), a.getVectorTo(c)), a.getVectorTo(d)));
        }

        private static bool isPointOnPlane(Point3D planePerpendicularVectorN, Point3D vectorToPointAP, double nLength)
        {
            double planeThickness = 0.0001;
            double distanceFromPlane = Math.Abs(dotProduct(planePerpendicularVectorN, vectorToPointAP)) / nLength;

            return distanceFromPlane <= planeThickness;
        }

        // check if projection of the point onto the plane (barycentric coordinates) is within the triangle
        private static bool isPointWithinTriangleBounds(Point3D triangleVectorAB, Point3D triangleVectorAC, Point3D vectorToPointAP, Point3D trianglePlanePerpendicularVectorN, double nLength)
        {
            double nSquared = nLength * nLength;

            double gamma = dotProduct(crossProduct(triangleVectorAB, vectorToPointAP), trianglePlanePerpendicularVectorN) / nSquared;
            if ((gamma < 0) || (gamma > 1))
            {
                return false;
            }

            double beta = dotProduct(crossProduct(vectorToPointAP, triangleVectorAC), trianglePlanePerpendicularVectorN) / nSquared;
            if ((beta < 0) || (beta > 1))
            {
                return false;
            }

            double alpha = 1.0 - gamma - beta;
            if ((alpha < 0) || (alpha > 1))
            {
                return false;
            }

            return true;
        }

        private static List<List<int>> unpackTriangles(string trianglesString)
        {
            List<List<int>> triangles = new List<List<int>>();

            foreach (string triangle in trianglesString.Split(';'))
            {
                triangles.Add(new List<int>(triangle.Split('-').Select(id => Convert.ToInt32(id))));
            }

            return triangles;
        }

        private static bool areAllEdgesConnectedToTwoTriangles(List<List<int>> triangles)
        {
            Dictionary<string, int> edgeCount = new Dictionary<string, int>();
            int value;

            foreach (List<int> triangle in triangles)
            {
                // point ids in triangles are always in an ascending order
                foreach (string key in new List<string> { triangle[0] + " " + triangle[1], triangle[1] + " " + triangle[2], triangle[0] + " " + triangle[2] })
                {
                    edgeCount.TryGetValue(key, out value);
                    if (value > 1)
                    {
                        return false;
                    }
                    edgeCount[key] = value + 1;
                }
            }

            foreach (int count in edgeCount.Values)
            {
                if (count != 2)
                {
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<int, Dictionary<int, bool>> getAdjacencyList(List<List<int>> triangles)
        {
            // turns out a HashSet can leak on abort and so cannot be used with CLR
            // that's why a Dictionary<int, bool> with all bools set to false is used here
            Dictionary<int, Dictionary<int, bool>> adjacencyList = new Dictionary<int, Dictionary<int, bool>>();

            foreach (List<int> triangle in triangles)
            {
                // key is triangle[i], the other two are added to Dictionary<int, bool> (pseudo hash set)
                for (int i = 0; i < 3; i++)
                {
                    if (adjacencyList.ContainsKey(triangle[i]))
                    {
                        if (!adjacencyList[triangle[i]].ContainsKey(triangle[(i + 1) % 3]))
                        {
                            adjacencyList[triangle[i]].Add(triangle[(i + 1) % 3], false);
                        }

                        if (!adjacencyList[triangle[i]].ContainsKey(triangle[(i + 2) % 3]))
                        {
                            adjacencyList[triangle[i]].Add(triangle[(i + 2) % 3], false);
                        }
                    }
                    else
                    {
                        adjacencyList.Add(triangle[i], new Dictionary<int, bool>
                        {
                            { triangle[(i + 1) % 3], false },
                            { triangle[(i + 2) % 3], false }
                        });
                    }
                }
            }

            return adjacencyList;
        }

        private static bool doAllVerticesFormClosedFans(Dictionary<int, Dictionary<int, bool>> adjacencyList)
        {
            foreach (Dictionary<int, bool> vertexNeighbours in adjacencyList.Values)
            {
                LinkedList<int> leftVertices = new LinkedList<int>(vertexNeighbours.Keys);
                int first = leftVertices.First.Value;
                leftVertices.Remove(first);

                int current = first;
                bool noNeighbourFlag;
                while (leftVertices.Count > 0)
                {
                    noNeighbourFlag = true;

                    foreach (int leftVertex in leftVertices)
                    {
                        if (adjacencyList[leftVertex].Keys.Contains(current))
                        {
                            current = leftVertex;
                            leftVertices.Remove(current);
                            noNeighbourFlag = false;
                            break;
                        }
                    }

                    if (noNeighbourFlag)
                    {
                        return false;
                    }
                }

                if (!adjacencyList[current].Keys.Contains(first))
                {
                    return false;
                }
            }

            return true;
        }

        private static List<List<(int id, Point3D point)>> unpackMesh(string meshString)
        {
            CultureInfo pointCulture = new CultureInfo("en")
            {
                NumberFormat = { NumberDecimalSeparator = "." }
            };

            List<List<(int id, Point3D point)>> mesh = new List<List<(int id, Point3D point)>>();

            foreach (string triangleString in meshString.Split(';'))
            {
                List<(int id, Point3D point)> triangle = new List<(int id, Point3D point)>();

                foreach (string pointString in triangleString.Split('#'))
                {
                    string[] pointData = pointString.Split(',');

                    triangle.Add((Convert.ToInt32(pointData[0]), new Point3D(
                        Convert.ToDouble(pointData[1], pointCulture),
                        Convert.ToDouble(pointData[2], pointCulture),
                        Convert.ToDouble(pointData[3], pointCulture)
                    )));
                }

                mesh.Add(triangle);
            }

            return mesh;
        }

        private static List<List<Point3D>> orderVertices(ref List<List<(int pId, Point3D coords)>> mesh)
        {
            Dictionary<(int pId1, int pId2), List<int>> edgeMapToTriangleIds = new Dictionary<(int pId1, int pId2), List<int>>();
            Dictionary<int, List<Point3D>> orderedMesh = new Dictionary<int, List<Point3D>>();
            Queue<(int pId1, int pId2)> edgeQueue = new Queue<(int pId1, int pId2)>();

            // create edge map
            for (int tId = 0; tId < mesh.Count; tId++)
            {
                (int, int)[] edges =
                [
                    (mesh[tId][0].pId, mesh[tId][1].pId),
                    (mesh[tId][1].pId, mesh[tId][2].pId),
                    (mesh[tId][0].pId, mesh[tId][2].pId),
                ];

                foreach (var edge in edges)
                {
                    if (!edgeMapToTriangleIds.ContainsKey(edge))
                    {
                        edgeMapToTriangleIds.Add(edge, new List<int>());
                    }
                    edgeMapToTriangleIds[edge].Add(tId);
                }
            }

            // add first triangle from mesh to orderedMesh and its edges to edgeQueue
            orderedMesh.Add(0, mesh[0].Select(p => p.coords).ToList());
            edgeQueue.Enqueue((mesh[0][0].pId, mesh[0][1].pId));
            edgeQueue.Enqueue((mesh[0][1].pId, mesh[0][2].pId));
            edgeQueue.Enqueue((mesh[0][2].pId, mesh[0][0].pId));

            while (edgeQueue.Count > 0)
            {
                // pop edge from edgeQueue
                (int pId1, int pId2) curEdge = edgeQueue.Dequeue();
                (int pId1, int pId2) key = (curEdge.pId1 < curEdge.pId2) ? (curEdge.pId1, curEdge.pId2) : (curEdge.pId2, curEdge.pId1);

                // get triangles adjacent to edge from edgeMap
                List<int> tIds = edgeMapToTriangleIds[key];

                foreach (int tId in tIds)
                {
                    // check if each of those triangles has already been corrected
                    if (!orderedMesh.ContainsKey(tId))
                    {
                        List<(int pId, Point3D coords)> triangle = mesh[tId];

                        List<(int pId1, int pId2)> edges = [
                            (triangle[0].pId, triangle[1].pId),
                            (triangle[1].pId, triangle[2].pId),
                            (triangle[2].pId, triangle[0].pId)
                        ];

                        // flip the order of vertices if its the same as the one from queue
                        if (edges.Contains(curEdge))
                        {
                            triangle.Reverse();
                            edges.Remove(curEdge);

                            // add the other edges to queue (but reversed)
                            edgeQueue.Enqueue((edges[0].pId2, edges[0].pId1));
                            edgeQueue.Enqueue((edges[1].pId2, edges[1].pId1));
                        }
                        else
                        {
                            edges.Remove((curEdge.pId2, curEdge.pId1));

                            // add the other edges to queue
                            edgeQueue.Enqueue(edges[0]);
                            edgeQueue.Enqueue(edges[1]);
                        }

                        // add triangle to orderedMesh
                        orderedMesh.Add(tId, triangle.Select(p => p.coords).ToList());
                    }
                }
            }

            return orderedMesh.Values.ToList();
        }

        private static double signedTetrahedronVolume(Point3D a, Point3D b, Point3D c, Point3D d)
        {
            return dotProduct(crossProduct(a.getVectorTo(b), a.getVectorTo(c)), a.getVectorTo(d)) / 6.0;
        }
    }
}


