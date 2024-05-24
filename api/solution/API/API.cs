using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using static TriangleMeshAPI;
using System.Globalization;
using System.Security.Cryptography;
using System.Transactions;
using System.Reflection.PortableExecutable;
using System.Linq;
using System.Linq.Expressions;

public class TriangleMeshAPI
{
    private CultureInfo pointCulture = new CultureInfo("en")
    {
        NumberFormat = { NumberDecimalSeparator = "." }
    };
    
    public class MeshObject
    {
        public MeshObject() 
        {
            this.id = -1;
        }

        public MeshObject(int id)
        {
            this.id = id;
        }

        public int id;
    }

    public class Point : MeshObject
    {
        public Point() : base()
        {
            this.x = 0;
            this.y = 0;
            this.z = 0;
        }
        public Point(int id, double x, double y, double z) : base(id)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        public override string ToString()
        {
            CultureInfo pointCulture = new CultureInfo("en")
            {
                NumberFormat = { NumberDecimalSeparator = "." }
            };
            return "(p " + id + ": [" + x.ToString(pointCulture) + ", " + y.ToString(pointCulture) + ", " + z.ToString(pointCulture) + "])";
        }

        public double x, y, z;
    }

    public class Triangle : MeshObject
    {
        public Triangle() : base()
        {
            this.a = null;
            this.b = null;
            this.c = null;
        }
        public Triangle(int id, Point a, Point b, Point c) : base(id)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }
        public override string ToString()
        {
            return "(t " + id + ": " + a + ", " + b + ", " + c + ")";
        }

        public Point a, b, c;
    }

    public class Mesh : MeshObject
    {
        public Mesh() : base()
        {
            this.triangles = null;
        }

        public Mesh(int id, List<Triangle> triangles) : base(id)
        {
            this.triangles = triangles;
        }
        public override string ToString()
        {
            return "(m " + id + ": \n\t" + String.Join(",\n\t", triangles) + "\n)";
        }

        public List<Triangle> triangles;
    }

    private string connectionString;


    /////////////////////////////////////// PUBLIC FUNCTIONS ///////////////////////////////////////


    public TriangleMeshAPI(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public List<Point> fetchPoints()
    {
        using (SqlConnection connection = new SqlConnection(this.connectionString))
        {
            connection.Open();
            return fetchPoints(connection);
        }
    }

    public List<Triangle> fetchTriangles(List<Point> points)
    {
        using (SqlConnection connection = new SqlConnection(this.connectionString))
        {
            connection.Open();
            return fetchTriangles(connection, points);
        }
    }

    public List<Mesh> fetchMeshes(List<Triangle> triangles)
    {
        using (SqlConnection connection = new SqlConnection(this.connectionString))
        {
            connection.Open();
            return fetchMeshes(connection, triangles);
        }
    }

    // returns id
    public int insertPoint(double x, double y, double z)
    {
        using (SqlConnection connection = new SqlConnection(this.connectionString))
        {
            connection.Open();

            string commandString = "INSERT INTO [Point] (coordinates) OUTPUT INSERTED.id VALUES ([dbo].[createPoint3D](@x, @y, @z))";
            SqlCommand command = new SqlCommand(commandString, connection);
            command.Parameters.Add("@x", SqlDbType.Float).Value = x;
            command.Parameters.Add("@y", SqlDbType.Float).Value = y;
            command.Parameters.Add("@z", SqlDbType.Float).Value = z;

            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    // will throw SqlException on triangle repeats
    public int insertTriangle(Point a, Point b, Point c)
    {
        return insertTriangle(a.id, b.id, c.id);
    }

    // will throw SqlException on triangle repeats
    public int insertTriangle(int aId, int bId, int cId)
    {
        using (SqlConnection connection = new SqlConnection(this.connectionString))
        {
            connection.Open();
            return insertTriangleFromPointIdList(connection, new List<int>([aId, bId, cId]));
        }
    }

    public int insertMesh(List<Triangle> triangles)
    {
        return insertMesh(triangles.Select(triangle => triangle.id).ToList());
    }

    public int insertMesh(List<int> triangleIds)
    {
        using (TransactionScope transaction = new TransactionScope())
        {
            using (SqlConnection connection = new SqlConnection(this.connectionString))
            {
                connection.Open();
            
                int id = createNewMesh(connection);

                if (triangleIds.Count != 0)
                {
                    insertTrianglesIntoMesh(connection, triangleIds, id);
                }

                transaction.Complete();
                return id;
            }
        }
    }

    // returns updated list of meshes
    public List<Mesh> mergeMeshes(List<int> meshIds, List<Triangle> allTriangles)
    {
        using (TransactionScope transaction = new TransactionScope())
        {
            using (SqlConnection connection = new SqlConnection(this.connectionString))
            {
                connection.Open();

                if (meshIds.Count > 1)
                {
                    int meshId = createNewMesh(connection);
                    List<int> triangleIds = fetchTrianglesFromMeshes(connection, meshIds);
                    deleteMeshes(connection, meshIds);
                    insertTrianglesIntoMesh(connection, triangleIds, meshId);
                }
                
                List<Mesh> meshes = fetchMeshes(connection, allTriangles);

                transaction.Complete();
                return meshes;
            }
        }
    }

    // updates lists of points and triangles and meshes
    public void deletePoints(List<int> pointIds, out List<Point> points, out List<Triangle> triangles, out List<Mesh> meshes)
    {
        using (TransactionScope transaction = new TransactionScope())
        {
            using (SqlConnection connection = new SqlConnection(this.connectionString))
            {
                connection.Open();

                if (pointIds.Count != 0)
                {
                    string commandString = "DELETE FROM [Point] WHERE id IN (" + string.Join(", ", pointIds) + ")";
                    SqlCommand command = new SqlCommand(commandString, connection);
                    command.ExecuteNonQuery();
                }

                List<Point> updatedPoints = fetchPoints(connection);
                List<Triangle> updatedTriangles = fetchTriangles(connection, updatedPoints);
                List<Mesh> updatedMeshes = fetchMeshes(connection, updatedTriangles);

                transaction.Complete();

                points = updatedPoints;
                triangles = updatedTriangles;
                meshes = updatedMeshes;
            }
        }
    }

    // updates list of triangles and meshes
    public void deleteTriangles(List<int> triangleIds, List<Point> points, out List<Triangle> triangles, out List<Mesh> meshes)
    {
        using (TransactionScope transaction = new TransactionScope())
        {
            using (SqlConnection connection = new SqlConnection(this.connectionString))
            {
                connection.Open();

                if (triangleIds.Count != 0)
                {
                    string commandString = "DELETE FROM [Triangle] WHERE id IN (" + string.Join(", ", triangleIds) + ")";
                    SqlCommand command = new SqlCommand(commandString, connection);
                    command.ExecuteNonQuery();
                }

                List<Triangle> updatedTriangles = fetchTriangles(connection, points);
                List<Mesh> updatedMeshes = fetchMeshes(connection, updatedTriangles);

                transaction.Complete();

                triangles = updatedTriangles;
                meshes = updatedMeshes;
            }
        }
    }

    public void deleteMeshes(List<int> meshIds)
    {
        using (SqlConnection connection = new SqlConnection(this.connectionString))
        {
            connection.Open();
            deleteMeshes(connection, meshIds);
        }
    }

    public double getDistanceBetweenPoints(int aId, int bId)
    {
        using (SqlConnection connection = new SqlConnection(this.connectionString))
        {
            connection.Open();

            string commandString = "SELECT [dbo].[getDistance] (" +
                "(SELECT coordinates FROM [Point] WHERE id = @aId)," +
                "(SELECT coordinates FROM [Point] WHERE id = @bId))";
            SqlCommand command = new SqlCommand(commandString, connection);
            command.Parameters.Add("@aId", SqlDbType.Int).Value = aId;
            command.Parameters.Add("@bId", SqlDbType.Int).Value = bId;

            return Convert.ToDouble(command.ExecuteScalar());
        }
    }

    public double getTriangleArea(Triangle triangle)
    {
        using (SqlConnection connection = new SqlConnection(this.connectionString))
        {
            connection.Open();

            string commandString = "SELECT [dbo].[getTriangleArea] (" +
                "(SELECT coordinates FROM [Point] WHERE id = @aId)," +
                "(SELECT coordinates FROM [Point] WHERE id = @bId)," +
                "(SELECT coordinates FROM [Point] WHERE id = @cId))";
            SqlCommand command = new SqlCommand(commandString, connection);
            command.Parameters.Add("@aId", SqlDbType.Int).Value = triangle.a.id;
            command.Parameters.Add("@bId", SqlDbType.Int).Value = triangle.b.id;
            command.Parameters.Add("@cId", SqlDbType.Int).Value = triangle.c.id;

            return Convert.ToDouble(command.ExecuteScalar());
        }
    }

    public double getMeshArea(int id)
    {
        using (SqlConnection connection = new SqlConnection(this.connectionString))
        {
            connection.Open();

            string commandString = "EXEC [getMeshArea] @meshId = " + id;
            SqlCommand command = new SqlCommand(commandString, connection);

            return Convert.ToDouble(command.ExecuteScalar());
        }
    }

    public bool isMeshManifold(int id)
    {
        using (SqlConnection connection = new SqlConnection(this.connectionString))
        {
            connection.Open();

            string commandString = "SELECT isManifold FROM [Mesh] WHERE id = @id";
            SqlCommand command = new SqlCommand(commandString, connection);
            command.Parameters.Add("@id", SqlDbType.Int).Value = id;

            object result = command.ExecuteScalar();
            if (result is null)
            {
                throw new ArgumentException("Mesh with given id doesn't exist!");
            }

            return Convert.ToBoolean(result);
        }
    }

    public double getMeshVolume(int id)
    {
        using (SqlConnection connection = new SqlConnection(this.connectionString))
        {
            connection.Open();

            string commandString = "EXEC [getMeshVolume] @meshId = " + id;
            SqlCommand command = new SqlCommand(commandString, connection);

            try
            {
                double volume = Convert.ToDouble(command.ExecuteScalar());
                return volume;
            }
            catch (Exception)
            {
                throw new ArgumentException("Mesh is not manifold!");
            }
        }
    }

    // undefined behaviour when point is on triangles or vertices
    public bool isPointInsideMesh(int pointId, int meshId)
    {
        List<bool> votesForInside = new List<bool>();

        using (SqlConnection connection = new SqlConnection(this.connectionString))
        {
            connection.Open();

            List<double> outsidePoint = getPointBeyondMinimalMeshBox(connection, meshId);
            double distortionValue = 0.1;

            foreach (var distortion in new List<List<double>>([
                [0, 0, 0], 
                [distortionValue, distortionValue, 0], 
                [0, distortionValue, distortionValue],
                [distortionValue, 0, distortionValue], 
                [0, -distortionValue, 0]
            ]))
            {
                votesForInside.Add(checkIfPointIsInsideMesh(connection, pointId, meshId, outsidePoint[0] + distortion[0], outsidePoint[1] + distortion[1], outsidePoint[2] + distortion[2]));
            }
        }

        return votesForInside.Where(vote => vote).Count() > (votesForInside.Count / 2);
    }

    public bool isPointOnTriangle(int pointId, int triangleId)
    {
        using (SqlConnection connection = new SqlConnection(this.connectionString))
        {
            connection.Open();

            string commandString = "EXEC [isPointOnTriangle] @pointId = " + pointId + ", @triangleId = " + triangleId;
            SqlCommand command = new SqlCommand(commandString, connection);

            try
            {
                return Convert.ToBoolean(command.ExecuteScalar());
            }
            catch(Exception)
            {
                throw new ArgumentException("Given point or triangle doesn't exist!");
            }
        }
    }

    public bool isPointOnMesh(int pointId, int meshId)
    {
        using (SqlConnection connection = new SqlConnection(this.connectionString))
        {
            connection.Open();

            string commandString = "EXEC [isPointOnMesh] @pointId = " + pointId + ", @meshId = " + meshId;
            SqlCommand command = new SqlCommand(commandString, connection);

            try
            {
                return Convert.ToBoolean(command.ExecuteScalar());
            }
            catch (Exception)
            {
                throw new ArgumentException("Given point or mesh doesn't exist!");
            }
        }
    }


    /////////////////////////////////////// PRIVATE FUNCTIONS ///////////////////////////////////////

    // will throw SqlException on triangle repeats, returns id
    private int insertTriangleFromPointIdList(SqlConnection openConnection, List<int> pointIds)
    {
        pointIds.Sort();

        string commandString = "INSERT INTO [Triangle] (point_id1, point_id2, point_id3) OUTPUT INSERTED.id VALUES (@aId, @bId, @cId)";
        SqlCommand command = new SqlCommand(commandString, openConnection);
        command.Parameters.Add("@aId", SqlDbType.Int).Value = pointIds[0];
        command.Parameters.Add("@bId", SqlDbType.Int).Value = pointIds[1];
        command.Parameters.Add("@cId", SqlDbType.Int).Value = pointIds[2];
        
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private Dictionary<int, Point> mapPoints(List<Point> points)
    {
        Dictionary<int, Point> pointMap = new Dictionary<int, Point>();
        foreach (Point point in points)
        {
            pointMap.Add(point.id, point);
        }
        return pointMap;
    }

    private Dictionary<int, Triangle> mapTriangles(List<Triangle> triangles)
    {
        Dictionary<int, Triangle> triangleMap = new Dictionary<int, Triangle>();
        foreach (Triangle triangle in triangles)
        {
            triangleMap.Add(triangle.id, triangle);
        }
        return triangleMap;
    }

    private Dictionary<int, Mesh> fetchEmptyMeshMap(SqlConnection openConnection)
    {
        Dictionary<int, Mesh> meshMap = new Dictionary<int, Mesh>();

        string commandString = "SELECT id FROM [Mesh]";
        SqlCommand command = new SqlCommand(commandString, openConnection);

        SqlDataReader datareader = command.ExecuteReader();
        while (datareader.Read())
        {
            int id = datareader.GetInt32(0);
            meshMap.Add(id, new Mesh(id, new List<Triangle>()));
        }
        datareader.Close();

        return meshMap;
    }

    private void fillMeshMapWithTriangles(SqlConnection openConnection, List<Triangle> triangles, ref Dictionary<int, Mesh> meshMap)
    {
        Dictionary<int, Triangle> triangleMap = mapTriangles(triangles);
        string commandString = "SELECT mesh_id, triangle_id FROM [MeshTriangle]";
        SqlCommand command = new SqlCommand(commandString, openConnection);

        SqlDataReader datareader = command.ExecuteReader();
        while (datareader.Read())
        {
            int id = datareader.GetInt32(0);
            Triangle triangle;
            bool t = triangleMap.TryGetValue(datareader.GetInt32(1), out triangle);
            if (!t)
            {
                throw new ArgumentException("A triangle used by a mesh wasn't in the provided list.");
            }
            meshMap[id].triangles.Add(triangle);
        }
        datareader.Close();
    }

    private int createNewMesh(SqlConnection openConnection)
    {
        string commandString = "INSERT INTO [Mesh] OUTPUT INSERTED.id DEFAULT VALUES";
        SqlCommand command = new SqlCommand(commandString, openConnection);

        int id = int.Parse(command.ExecuteScalar().ToString());

        return id;
    }

    // will throw sqlexception on repeats
    private void insertTrianglesIntoMesh(SqlConnection openConnection, List<int> triangleIds, int meshId)
    {
        if (triangleIds.Count == 0)
        {
            return;
        }

        List<string> values = new List<string>();
        foreach (int triangleId in triangleIds)
        {
            values.Add("(" + meshId + ", " + triangleId + ")");
        }
        string commandString = "INSERT INTO [MeshTriangle] (mesh_id, triangle_id) VALUES " + string.Join(", ", values);

        SqlCommand command = new SqlCommand(commandString, openConnection);
        command.ExecuteNonQuery();
    }

    private List<int> fetchTrianglesFromMeshes(SqlConnection openConnection, List<int> meshIds)
    {
        HashSet<int> triangleIds = new HashSet<int>();

        string commandString = "SELECT triangle_id FROM [MeshTriangle] WHERE mesh_id IN (" + string.Join(", ", meshIds) + ")";
        SqlCommand command = new SqlCommand(commandString, openConnection);

        SqlDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            triangleIds.Add(reader.GetInt32(0));
        }
        reader.Close();

        return triangleIds.ToList();
    }

    private void deleteMeshes(SqlConnection openConnection, List<int> meshIds)
    {
        if (meshIds.Count != 0)
        {
            string commandString = "DELETE FROM [Mesh] WHERE id IN (" + string.Join(", ", meshIds) + ")";
            SqlCommand command = new SqlCommand(commandString, openConnection);
            command.ExecuteNonQuery();
        }
    }

    private List<Point> fetchPoints(SqlConnection openConnection)
    {
        List<Point> points = new List<Point>();

        string commandString = "SELECT id, coordinates.X, coordinates.Y, coordinates.Z FROM [Point]";
        SqlCommand command = new SqlCommand(commandString, openConnection);

        SqlDataReader datareader = command.ExecuteReader();
        while (datareader.Read())
        {
            points.Add(new Point(datareader.GetInt32(0), datareader.GetDouble(1), datareader.GetDouble(2), datareader.GetDouble(3)));
        }
        datareader.Close();


        return points;
    }

    private List<Triangle> fetchTriangles(SqlConnection openConnection, List<Point> points)
    {
        List<Triangle> triangles = new List<Triangle>();
        Dictionary<int, Point> pointMap = mapPoints(points);

        string commandString = "SELECT * FROM [Triangle]";
        SqlCommand command = new SqlCommand(commandString, openConnection);

        SqlDataReader datareader = command.ExecuteReader();
        while (datareader.Read())
        {
            Point p1 = null, p2 = null, p3 = null;
            bool b1 = pointMap.TryGetValue(datareader.GetInt32(1), out p1);
            bool b2 = pointMap.TryGetValue(datareader.GetInt32(2), out p2);
            bool b3 = pointMap.TryGetValue(datareader.GetInt32(3), out p3);
            if (!(b1 && b2 && b3))
            {
                throw new ArgumentException("A point used by a triangle wasn't in the provided list.");
            }

            triangles.Add(new Triangle(datareader.GetInt32(0), p1, p2, p3));
        }
        datareader.Close();

        return triangles;
    }

    private List<Mesh> fetchMeshes(SqlConnection openConnection, List<Triangle> triangles)
    {
        Dictionary<int, Mesh> meshMap = fetchEmptyMeshMap(openConnection);
        fillMeshMapWithTriangles(openConnection, triangles, ref meshMap);

        return meshMap.Values.ToList();
    }

    private List<double> getPointBeyondMinimalMeshBox(SqlConnection openConnection, int meshId)
    {
        string commandString = "WITH c as (SELECT [dbo].[getPointBeyondMinimalMeshBox](" + meshId + ") as coords) SELECT coords.X, coords.Y, coords.Z FROM c";
        SqlCommand command = new SqlCommand(commandString, openConnection);

        SqlDataReader reader = command.ExecuteReader();
        reader.Read();
        double outsideX = reader.GetDouble(0);
        double outsideY = reader.GetDouble(1);
        double outsideZ = reader.GetDouble(2);
        reader.Close();

        return new List<double> { outsideX, outsideY, outsideZ };
    }

    private bool checkIfPointIsInsideMesh(SqlConnection openConnection, int pointId, int meshId, double x, double y, double z)
    {
        string commandString = "exec [countCollisionsWithMeshFromOutsideMeshToPoint] @pointId = @pId, @meshId = @mId, @outsideX = @outX, @outsideY = @outY, @outsideZ = @outZ";
        SqlCommand command = new SqlCommand(commandString, openConnection);
        command.Parameters.Add("@pId", SqlDbType.Int).Value = pointId;
        command.Parameters.Add("@mId", SqlDbType.Int).Value = meshId;
        command.Parameters.Add("@outX", SqlDbType.Float).Value = x;
        command.Parameters.Add("@outY", SqlDbType.Float).Value = y;
        command.Parameters.Add("@outZ", SqlDbType.Float).Value = z;

        try
        {
            int collisions = Convert.ToInt32(command.ExecuteScalar());
            return (collisions % 2) != 0;
        }
        catch (Exception)
        {
            throw new ArgumentException("Mesh is not manifold or point doesn't exist!");
        }
    }
}

