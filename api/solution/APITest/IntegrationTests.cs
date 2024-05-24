using System.Data.SqlClient;
using System.Transactions;
using static TriangleMeshAPI;
using Moq;
using FluentAssertions;
using NUnit.Framework.Constraints;
using System.Data.SqlTypes;

namespace APITest
{
    [TestFixture]
    public class IntegrationTestsOnEmptyDB
    {
        private TriangleMeshAPI api;
        private TransactionScope innerTransactionScope;
        private string connectionString;

        [OneTimeSetUp]
        public void ClassSetup()
        {
            connectionString = @"DATA SOURCE=MSSQLServer; INITIAL CATALOG=TriangleMeshAPI; INTEGRATED SECURITY=SSPI; Persist Security Info=False; Server=GIENIO\SQLEXPRESS";
            api = new TriangleMeshAPI(connectionString);
        }

        [SetUp]
        public void TestSetup()
        {
            innerTransactionScope = new TransactionScope();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string commandString = "if (exists(select null from triangle)) begin delete from triangle; end;";
                SqlCommand command = new SqlCommand(commandString, connection);
                command.ExecuteNonQuery();

                command.CommandText = "if (exists(select null from point)) begin delete from point; end;";
                command.ExecuteNonQuery();

                command.CommandText = "if (exists(select null from mesh)) begin delete from mesh; end;";
                command.ExecuteNonQuery();
            }
        }

        [TearDown]
        public void TestTearDown()
        {
            innerTransactionScope.Dispose();
        }


        [Test]
        public void fetchPointsWhenEmptyTest()
        {
            List<Point> points = api.fetchPoints();

            Assert.That(points, Is.EqualTo(new List<Point>()));
        }

        [Test]
        public void fetchPointsTest()
        {
            api.insertPoint(1,0.22,1.435657);
            api.insertPoint(2,2,2);
            List<Point> points = api.fetchPoints();

            points.Should().BeEquivalentTo(new List<Point> { 
                new Point(1,1,0.22,1.435657),
                new Point(2,2,2,2)
            });
        }

        [Test]
        public void fetchTrianglesWhenEmptyTest()
        {
            List<Triangle> triangles = api.fetchTriangles(api.fetchPoints());

            Assert.That(triangles, Is.EqualTo(new List<Triangle>()));
        }

        [Test]
        public void fetchTrianglesTest()
        {
            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertPoint(4,4,4);
            api.insertTriangle(1, 3, 2);
            api.insertTriangle(1, 2, 4);
            List<Triangle> triangles = api.fetchTriangles(api.fetchPoints());

            triangles.Should().BeEquivalentTo(new List<Triangle> {
                new Triangle(1, new Point(1, 1, 1, 1), new Point(2, 2, 2, 2), new Point(3, 3, 3, 3)),
                new Triangle(2, new Point(1, 1, 1, 1), new Point(2, 2, 2, 2), new Point(4,4,4,4))
            });
        }

        [Test]
        public void throwWhenFetchingTrianglesWithMissingPointsInListTest()
        {
            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertTriangle(1, 2, 3);

            Assert.Throws(
                Is.TypeOf<ArgumentException>().And.Message.EqualTo("A point used by a triangle wasn't in the provided list."), 
                () => api.fetchTriangles(new List<Point>())
            );
        }

        [Test]
        public void throwWhenFetchingTrianglesWithRepeatingIdsInPointListTest()
        {
            Assert.Throws(
                Is.TypeOf<ArgumentException>(),
                () => api.fetchTriangles(new List<Point>
                {
                    new Point(1,1,1,1),
                    new Point(1,1,1,1)
                })
            );
        }

        [Test]
        public void fetchMeshesWhenEmptyTest()
        {
            List<Mesh> meshes = api.fetchMeshes(api.fetchTriangles(api.fetchPoints()));

            Assert.That(meshes, Is.EqualTo(new List<Mesh>()));
        }

        [Test]
        public void fetchMeshesTest()
        {
            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertPoint(4, 4, 4);
            api.insertTriangle(1, 2, 3);
            api.insertTriangle(1, 2, 4);
            List<Triangle> triangles = api.fetchTriangles(api.fetchPoints());
            api.insertMesh(triangles);
            api.insertMesh(new List<int> { 2 });
            List<Mesh> meshes = api.fetchMeshes(triangles);

            meshes.Should().BeEquivalentTo(new List<Mesh> { 
                new Mesh(1, 
                    new List<Triangle> {
                        new Triangle(1, new Point(1, 1, 1, 1), new Point(2, 2, 2, 2), new Point(3, 3, 3, 3)),
                        new Triangle(2, new Point(1, 1, 1, 1), new Point(2, 2, 2, 2), new Point(4,4,4,4))
                    }
                ),
                new Mesh(2,
                    new List<Triangle> {
                        new Triangle(2, new Point(1, 1, 1, 1), new Point(2, 2, 2, 2), new Point(4,4,4,4))
                    }
                )
            });
        }

        [Test]
        public void throwWhenFetchingMeshesWithMissingTrianglesInListTest()
        {
            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertTriangle(1, 2, 3);
            api.insertMesh(new List<int>([1]));

            Assert.Throws(
                Is.TypeOf<ArgumentException>().And.Message.EqualTo("A triangle used by a mesh wasn't in the provided list."),
                () => api.fetchMeshes(new List<Triangle>())
            );
        }

        [Test]
        public void throwWhenFetchingMeshesWithRepeatingIdsInTriangleListTest()
        {
            Assert.Throws(
                Is.TypeOf<ArgumentException>(),
                () => api.fetchMeshes(new List<Triangle>
                {
                    new Triangle(1, new Point(1,1,1,1), new Point(2,2,2,2), new Point(3,3,3,3)),
                    new Triangle(1, new Point(4,4,4,4), new Point(5,5,5,5), new Point(6,6,6,6))
                })
            );
        }

        [Test]
        public void insertPointTest()
        {
            int id1 = api.insertPoint(1, 1, 1);
            int id2 = api.insertPoint(1, 1, 1);

            Assert.Multiple(() =>
            {
                Assert.That(id1, Is.EqualTo(1));
                Assert.That(id2, Is.EqualTo(2));
            });
        }

        [Test]
        public void insertTriangleTest()
        {
            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertPoint(4, 4, 4);
            int id1 = api.insertTriangle(1, 3, 2);
            int id2 = api.insertTriangle(
                new Point(1,1,1,1),
                new Point(2,2,2,2),
                new Point(4,4,4,4)
            );

            Assert.Multiple(() =>
            {
                Assert.That(id1, Is.EqualTo(1));
                Assert.That(id2, Is.EqualTo(2));
            });
        }

        [Test]
        public void throwsWhenInsertingTriangleRepeatsTest()
        {
            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertTriangle(1, 2, 3);

            Assert.Throws(
                Is.TypeOf<SqlException>(),
                () => api.insertTriangle(1, 2, 3)
            );
        }

        [Test]
        public void throwsWhenInsertingTriangleWithRepeatingPointsTest()
        {
            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);

            Assert.Throws(
                Is.TypeOf<SqlException>(),
                () => api.insertTriangle(1, 1, 2)
            );
        }

        [Test]
        public void throwsWhenInsertingTrianglesWithNonExistingPointsTest()
        {
            Assert.Throws(
                Is.TypeOf<SqlException>(),
                () => api.insertTriangle(1, 2, 3)
            );
        }

        [Test]
        public void insertMeshTest()
        {
            int id1 = api.insertMesh(new List<Triangle>());
            int id2 = api.insertMesh(new List<int>());

            Assert.Multiple(() =>
            {
                Assert.That(id1, Is.EqualTo(1));
                Assert.That(id2, Is.EqualTo(2));
            });
        }

        [Test]
        public void throwsWhenInsertingRepeatTrianglesIntoMeshTest()
        {
            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertTriangle(1, 2, 3);

            Assert.Throws(
                Is.TypeOf<SqlException>(),
                () => api.insertMesh(new List<int> { 1, 1 })
            );
        }

        [Test]
        public void mergeMeshesIntoANewOneAndDeleteOldTest()
        {
            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertPoint(4, 4, 4);
            api.insertTriangle(1, 2, 3);
            api.insertTriangle(1, 2, 4);
            api.insertMesh(new List<int> { 1 });
            api.insertMesh(new List<int> { 2 });
            api.insertMesh(new List<int> { 2 });

            List<Triangle> triangles = api.fetchTriangles(api.fetchPoints());
            List<Mesh> meshes = api.mergeMeshes(new List<int> { 1, 2 }, triangles);

            meshes.Should().BeEquivalentTo(new List<Mesh> {
                new Mesh(3,
                    new List<Triangle> {
                        new Triangle(2, new Point(1, 1, 1, 1), new Point(2, 2, 2, 2), new Point(4, 4, 4, 4))
                    }
                ),
                new Mesh(4,
                    new List<Triangle> {
                        new Triangle(1, new Point(1, 1, 1, 1), new Point(2, 2, 2, 2), new Point(3, 3, 3, 3)),
                        new Triangle(2, new Point(1, 1, 1, 1), new Point(2, 2, 2, 2), new Point(4, 4, 4, 4))
                    }
                )
            });
        }

        [Test]
        public void mergeMeshesWithSameTriangesTest()
        {
            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertPoint(4, 4, 4);
            api.insertTriangle(1, 2, 3);
            api.insertTriangle(1, 2, 4);
            api.insertMesh(new List<int> { 1 });
            api.insertMesh(new List<int> { 1, 2 });

            List<Triangle> triangles = api.fetchTriangles(api.fetchPoints());
            List<Mesh> meshes = api.mergeMeshes(new List<int> { 1, 2 }, triangles);

            meshes.Should().BeEquivalentTo(new List<Mesh> {
                new Mesh(3,
                    new List<Triangle> {
                        new Triangle(1, new Point(1, 1, 1, 1), new Point(2, 2, 2, 2), new Point(3, 3, 3, 3)),
                        new Triangle(2, new Point(1, 1, 1, 1), new Point(2, 2, 2, 2), new Point(4, 4, 4, 4))
                    }
                )
            });
        }

        [Test]
        public void doesntChangeAnythingWhenMergingOneMeshTest()
        {
            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertTriangle(1, 2, 3);
            api.insertMesh(new List<int> { 1 });

            List<Triangle> triangles = api.fetchTriangles(api.fetchPoints());
            List<Mesh> meshes = api.mergeMeshes(new List<int> { 1 }, triangles);

            meshes.Should().BeEquivalentTo(new List<Mesh> {
                new Mesh(1,
                    new List<Triangle> {
                        new Triangle(1, new Point(1, 1, 1, 1), new Point(2, 2, 2, 2), new Point(3, 3, 3, 3))
                    }
                )
            });
        }

        [Test]
        public void doesntChangeAnythingWhenMergingEmptyMeshListTest()
        {
            List<Mesh> meshes = api.mergeMeshes(new List<int>(), new List<Triangle>());

            meshes.Should().BeEquivalentTo(new List<Mesh>());
        }

        [Test]
        public void throwsWhenMergingMeshesWithMissingTrianglesInListTest()
        {
            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertTriangle(1, 2, 3);
            api.insertMesh(new List<int> { 1 });

            Assert.Throws(
                Is.TypeOf<ArgumentException>().And.Message.EqualTo("A triangle used by a mesh wasn't in the provided list."),
                () => api.mergeMeshes(new List<int> { 1, 1 }, new List<Triangle>())
            );
        }

        [Test]
        public void deletePointsTest()
        {
            List<Point> points;
            List<Triangle> triangles;
            List<Mesh> meshes;

            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);

            api.deletePoints(new List<int> { 1, 2 }, out points, out triangles, out meshes);

            points = api.fetchPoints();

            points.Should().BeEquivalentTo(new List<Point> {
                new Point(3, 3, 3, 3)
            });
        }

        [Test]
        public void deletingPointsReturnsCorrectStateOfDBTest()
        {
            List<Point> points;
            List<Triangle> triangles;
            List<Mesh> meshes;
            Point p2 = new Point(2, 2, 2, 2);
            Point p3 = new Point(3, 3, 3, 3);
            Point p4 = new Point(4, 4, 4, 4);
            Triangle t2 = new Triangle(2, p2, p3, p4);
            Mesh m2 = new Mesh(2, new List<Triangle> { t2 });

            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertPoint(4, 4, 4);
            api.insertTriangle(1, 2, 3);
            api.insertTriangle(2, 3, 4);
            api.insertMesh(new List<int>{ 1 });
            api.insertMesh(new List<int> { 2 });

            api.deletePoints(new List<int> { 1 }, out points, out triangles, out meshes);

            points.Should().BeEquivalentTo(new List<Point> { p2, p3, p4 });

            triangles.Should().BeEquivalentTo(new List<Triangle> { t2 });

            meshes.Should().BeEquivalentTo(new List<Mesh> { m2 });
        }

        [Test]
        public void deletingPointsResetsIndexingToSmallestIdTest()
        {
            List<Point> points;
            List<Triangle> triangles;
            List<Mesh> meshes;

            api.insertPoint(1,1,1);
            api.insertPoint(2,2,2);
            api.insertPoint(3,3,3);
            api.insertPoint(4,4,4);
            api.deletePoints(new List<int> { 2, 4 }, out points, out triangles, out meshes);
            int id = api.insertPoint(5,5,5);

            Assert.That(id, Is.EqualTo(4));
        }

        [Test]
        public void deletingAllPointsResetsIndexingToOneTest()
        {
            List<Point> points;
            List<Triangle> triangles;
            List<Mesh> meshes;

            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.deletePoints(new List<int> { 1, 2 }, out points, out triangles, out meshes);
            int id = api.insertPoint(3,3,3);

            Assert.That(id, Is.EqualTo(1));
        }

        [Test]
        public void deletingNoPointsChangesNothingTest()
        {
            List<Point> points;
            List<Triangle> triangles;
            List<Mesh> meshes;

            api.insertPoint(1, 1, 1);
            api.deletePoints(new List<int> { }, out points, out triangles, out meshes);

            points.Should().BeEquivalentTo(new List<Point> {
                new Point(1, 1, 1, 1)
            });
        }

        [Test]
        public void deleteTrianglesTest()
        {
            List<Triangle> triangles;
            List<Mesh> meshes;

            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertPoint(4,4,4);
            api.insertTriangle(1, 2, 3);
            api.insertTriangle(1, 2, 4);
            api.insertTriangle(1, 3, 4);


            api.deleteTriangles(new List<int> { 1, 2 }, api.fetchPoints(), out triangles, out meshes);

            triangles = api.fetchTriangles(api.fetchPoints());

            triangles.Should().BeEquivalentTo(new List<Triangle> {
                new Triangle(3, new Point(1,1,1,1), new Point(3, 3, 3, 3), new Point(4,4,4,4))
            });
        }

        [Test]
        public void deletingTrianglesReturnsCorrectTrianglesAndMeshesTest()
        {
            List<Triangle> triangles;
            List<Mesh> meshes;
            Point p2 = new Point(2, 2, 2, 2);
            Point p3 = new Point(3, 3, 3, 3);
            Point p4 = new Point(4, 4, 4, 4);
            Triangle t2 = new Triangle(2, p2, p3, p4);
            Mesh m2 = new Mesh(2, new List<Triangle> { t2 });

            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertPoint(4, 4, 4);
            api.insertTriangle(1, 2, 3);
            api.insertTriangle(2, 3, 4);
            api.insertMesh(new List<int> { 1 });
            api.insertMesh(new List<int> { 2 });

            api.deleteTriangles(new List<int> { 1 }, api.fetchPoints(), out triangles, out meshes);

            triangles.Should().BeEquivalentTo(new List<Triangle> { t2 });

            meshes.Should().BeEquivalentTo(new List<Mesh> { m2 });
        }

        [Test]
        public void deletingTrianglesDoesntDeletePointsTest()
        {
            List<Triangle> triangles;
            List<Mesh> meshes;

            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertTriangle(1, 2, 3);

            api.deleteTriangles(new List<int> { 1 }, api.fetchPoints(), out triangles, out meshes);

            List<Point> points = api.fetchPoints();

            points.Should().BeEquivalentTo(new List<Point> { 
                new Point(1,1,1,1),
                new Point(2,2,2,2),
                new Point(3,3,3,3)
            });
        }

        [Test]
        public void deletingTrianglesResetsIndexingToSmallestIdTest()
        {
            List<Triangle> triangles;
            List<Mesh> meshes;

            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertPoint(4, 4, 4);
            api.insertTriangle(1, 2, 3);
            api.insertTriangle(1, 2, 4);
            api.insertTriangle(1, 3, 4);
            api.insertTriangle(2, 3, 4);
            api.deleteTriangles(new List<int> { 2, 4 }, api.fetchPoints(), out triangles, out meshes);
            int id = api.insertTriangle(1, 2, 4);

            Assert.That(id, Is.EqualTo(4));
        }

        [Test]
        public void deletingAllTrianglesResetsIndexingToOneTest()
        {
            List<Triangle> triangles;
            List<Mesh> meshes;

            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertPoint(4, 4, 4);
            api.insertTriangle(1, 2, 3);
            api.insertTriangle(1, 2, 4);
            api.deleteTriangles(new List<int> { 1, 2 }, api.fetchPoints(), out triangles, out meshes);
            int id = api.insertTriangle(1, 2, 4);

            Assert.That(id, Is.EqualTo(1));
        }

        [Test]
        public void deletingNoTrianglesChangesNothingTest()
        {
            List<Triangle> triangles;
            List<Mesh> meshes;

            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertTriangle(1, 2, 3);
            api.deleteTriangles(new List<int>(), api.fetchPoints(), out triangles, out meshes);

            triangles.Should().BeEquivalentTo(new List<Triangle>
            {
                new Triangle(1, new Point(1,1,1,1), new Point(2,2,2,2), new Point(3,3,3,3))
            });
        }

        [Test]
        public void throwsWhenDeletingTrianglesWithMissingPointsInListTest()
        {
            List<Triangle> triangles;
            List<Mesh> meshes;

            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertPoint(4, 4, 4);
            api.insertTriangle(1, 2, 3);
            api.insertTriangle(1, 2, 4);

            Assert.Throws(
                Is.TypeOf<ArgumentException>().And.Message.EqualTo("A point used by a triangle wasn't in the provided list."),
                () => api.deleteTriangles(new List<int> { 1 }, new List<Point>(), out triangles, out meshes)
            );
        }

        [Test]
        public void deleteMeshesTest()
        {
            api.insertMesh(new List<Triangle>());
            api.insertMesh(new List<Triangle>());
            api.deleteMeshes(new List<int> { 1 });

            List<Mesh> meshes = api.fetchMeshes(new List<Triangle>());

            meshes.Should().BeEquivalentTo(new List<Mesh>
            {
                new Mesh(2, new List<Triangle>())
            });
        }

        [Test]
        public void deletingMeshesDoesntDeleteTrianglesTest()
        {
            List<Triangle> triangles = new List<Triangle>
            {
                new Triangle(1, new Point(1,1,1,1), new Point(2,2,2,2), new Point(3,3,3,3))
            };
            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);
            api.insertTriangle(1, 2, 3);
            api.insertMesh(triangles);

            api.deleteMeshes(new List<int> { 1 });

            List<Triangle> fetchedTriangles = api.fetchTriangles(api.fetchPoints());

            fetchedTriangles.Should().BeEquivalentTo(triangles);
        }

        [Test]
        public void deletingMeshesResetsIndexingToSmallestIdTest()
        {
            api.insertMesh(new List<Triangle>());
            api.insertMesh(new List<Triangle>());
            api.insertMesh(new List<Triangle>());
            api.deleteMeshes(new List<int> { 2, 3 });
            int id = api.insertMesh(new List<Triangle>());

            Assert.That(id, Is.EqualTo(2));
        }

        [Test]
        public void deletingAllMeshesResetsIndexingToOneTest()
        {
            api.insertMesh(new List<Triangle>());
            api.insertMesh(new List<Triangle>());
            api.deleteMeshes(new List<int> { 1, 2 });
            int id = api.insertMesh(new List<Triangle>());

            Assert.That(id, Is.EqualTo(1));
        }

        [Test]
        public void deletingNoMeshesChangesNothingTest()
        {
            api.insertMesh(new List<Triangle>());
            api.insertMesh(new List<Triangle>());
            api.deleteMeshes(new List<int>());

            List<Mesh> meshes = api.fetchMeshes(new List<Triangle>());

            meshes.Should().BeEquivalentTo(new List<Mesh>
            {
                new Mesh(1, new List<Triangle>()),
                new Mesh(2, new List<Triangle>())
            });
        }

        [Test]
        public void getDistanceBetweenPointsTest()
        {
            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);

            double distance = api.getDistanceBetweenPoints(2, 1);

            Assert.That(distance, Is.EqualTo(Math.Sqrt(3)));
        }

        [Test]
        public void getDistanceBetweenTheSamePointsTest()
        {
            api.insertPoint(1, 1, 1);

            double distance = api.getDistanceBetweenPoints(1, 1);

            Assert.That(distance, Is.EqualTo(0));
        }

        [Test]
        public void throwsWhenGettingDistanceBetweenNonExistingPointsTest()
        {
            api.insertPoint(1, 1, 1);

            Assert.Throws(
                Is.TypeOf<InvalidCastException>(),
                () => api.getDistanceBetweenPoints(1, 2)
            );
        }

        [Test]
        public void getTriangleAreaTest()
        {
            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 3, 2);
            api.insertPoint(0.5, 3, 3);

            double distance = api.getTriangleArea(new Triangle(1,
                new Point(1, 1, 1, 1), new Point(2, 2, 3, 2), new Point(3, 0.5, 3, 3)
            ));

            Assert.That(distance, Is.EqualTo(2.193741097).Within(10e-7));
        }

        [Test]
        public void returnsZeroAreaWhenTriangleIsALineTest()
        {
            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);
            api.insertPoint(3, 3, 3);

            double distance = api.getTriangleArea(new Triangle(1,
                new Point(1, 1, 1, 1), new Point(2, 2, 2, 2), new Point(3, 3, 3, 3)
            ));

            Assert.That(distance, Is.EqualTo(0));
        }

        [Test]
        public void returnsZeroAreaWhenTriangleHasTwoSamePointsTest()
        {
            api.insertPoint(1, 1, 1);
            api.insertPoint(2, 2, 2);

            double distance = api.getTriangleArea(new Triangle(1,
                new Point(1, 1, 1, 1), new Point(2, 2, 2, 2), new Point(2,2,2,2)
            ));

            Assert.That(distance, Is.EqualTo(0));
        }

        [Test]
        public void throwsWhenTriangleUsesNonExistingPointsWhenGettingTriangleAreaTest()
        {
            api.insertPoint(1, 1, 1);
            api.insertPoint(2,2,2);

            Assert.Throws(
                Is.TypeOf<InvalidCastException>(),
                () => api.getTriangleArea(new Triangle(1, 
                    new Point(1,1,1,1), new Point(2,2,2,2), new Point(3,3,3,3)
                ))
            );
        }

        [Test]
        public void getMeshAreaTest()
        {
            Point p1 = new Point(1, 0, 0, 0);
            Point p2 = new Point(2, 1, 0, 0);
            Point p3 = new Point(3, 0, 0, 1);
            Point p4 = new Point(4, 0, 1, 0);
            Triangle t1 = new Triangle(1, p1, p2, p3);
            Triangle t2 = new Triangle(2, p1, p2, p4);
            Triangle t3 = new Triangle(3, p2, p3, p4);

            api.insertPoint(0, 0, 0);
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0, 1, 0);
            api.insertTriangle(1, 2, 3);
            api.insertTriangle(1, 2, 4);
            api.insertTriangle(2, 3, 4);
            api.insertMesh(new List<Triangle>{t1, t2, t3});

            double area = api.getMeshArea(1);

            Assert.That(area, Is.EqualTo(1.866025404).Within(10e-5));
        }

        [Test]
        public void throwsWhenGettingNonExistingMeshAreaTest()
        {
            Assert.Throws(
                Is.TypeOf<InvalidCastException>(),
                () => api.getMeshArea(1)
            );
        }

        [Test]
        public void returnsZeroWhenGettingEmptyMeshAreaTest()
        {
            api.insertMesh(new List<Triangle>());

            double area = api.getMeshArea(1);

            Assert.That(area, Is.EqualTo(0));
        }

        [Test]
        public void isMeshManifoldTrueTest()
        {
            api.insertPoint(0, 0, 0);
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0, 1, 0);

            api.insertTriangle(1, 2, 3);
            api.insertTriangle(1, 2, 4);
            api.insertTriangle(1, 3, 4);
            api.insertTriangle(2, 3, 4);

            api.insertMesh(api.fetchTriangles(api.fetchPoints()));

            bool isManifold = api.isMeshManifold(1);

            Assert.That(isManifold, Is.True);
        }

        [Test]
        public void meshWithThreeTrianglesUsingTheSameEdgeIsNotManifoldTest()
        {
            api.insertPoint(0, 0, 0);
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0, 1, 0);
            api.insertPoint(1, 0, 1);

            api.insertTriangle(1, 2, 3);
            api.insertTriangle(1, 2, 4);
            api.insertTriangle(1, 3, 4);
            api.insertTriangle(2, 3, 4);
            api.insertTriangle(2, 3, 5);

            api.insertMesh(api.fetchTriangles(api.fetchPoints()));

            bool isManifold = api.isMeshManifold(1);

            Assert.That(isManifold, Is.False);
        }

        [Test]
        public void openMeshIsNotManifoldTest()
        {
            api.insertPoint(0, 0, 0);
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0, 1, 0);
            api.insertPoint(1, 0, 1);

            api.insertTriangle(1, 2, 3);
            api.insertTriangle(1, 2, 4);
            api.insertTriangle(1, 3, 4);

            api.insertMesh(api.fetchTriangles(api.fetchPoints()));

            bool isManifold = api.isMeshManifold(1);

            Assert.That(isManifold, Is.False);
        }

        [Test]
        public void emptyMeshIsNotManifoldTest()
        {
            api.insertMesh(new List<Triangle>());

            bool isManifold = api.isMeshManifold(1);

            Assert.That(isManifold, Is.False);
        }

        [Test]
        public void meshWithHourglassShapeIsNotManifoldTest()
        {
            api.insertPoint(0, 0, 0);
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0, 1, 0);
            api.insertPoint(0, -1, 0);
            api.insertPoint(0, 0, -1);
            api.insertPoint(-1, 0, 0);

            api.insertTriangle(1, 2, 3);
            api.insertTriangle(1, 2, 4);
            api.insertTriangle(1, 3, 4);
            api.insertTriangle(2, 3, 4);
            api.insertTriangle(1, 5, 6);
            api.insertTriangle(1, 5, 7);
            api.insertTriangle(1, 6, 7);
            api.insertTriangle(5, 6, 7);

            api.insertMesh(api.fetchTriangles(api.fetchPoints()));

            bool isManifold = api.isMeshManifold(1);

            Assert.That(isManifold, Is.False);
        }

        [Test]
        public void throwsWhenGettingNonExistingMeshManifoldCheckTest()
        {
            Assert.Throws(
                Is.TypeOf<ArgumentException>().And.Message.EqualTo("Mesh with given id doesn't exist!"),
                () => api.isMeshManifold(1)
            );
        }

        [Test]
        public void getMeshVolumeTest()
        {
            api.insertPoint(0, 0, 0);
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0, 1, 0);
            api.insertTriangle(1, 2, 3);
            api.insertTriangle(1, 2, 4);
            api.insertTriangle(1, 3, 4);
            api.insertTriangle(2, 3, 4);
            api.insertMesh(api.fetchTriangles(api.fetchPoints()));

            double volume = api.getMeshVolume(1);

            Assert.That(volume, Is.EqualTo(0.1666666).Within(10e-5));
        }

        [Test]
        public void returnsMeshVolumeZeroWhenMeshIsFlatTest()
        {
            api.insertPoint(0, 0, 0);
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0.5, 0, 0.5);
            api.insertTriangle(1, 2, 3);
            api.insertTriangle(1, 2, 4);
            api.insertTriangle(1, 3, 4);
            api.insertTriangle(2, 3, 4);
            api.insertMesh(api.fetchTriangles(api.fetchPoints()));

            double volume = api.getMeshVolume(1);

            Assert.That(volume, Is.Zero);
        }

        [Test]
        public void throwsWhenGettingNonExistingMeshVolumeTest()
        {
            Assert.Throws(
                Is.TypeOf<ArgumentException>(),
                () => api.getMeshVolume(1)
            );
        }

        [Test]
        public void throwsWhenGettingNonManifoldMeshVolumeTest()
        {
            api.insertMesh(new List<Triangle>());

            Assert.Throws(
                Is.TypeOf<ArgumentException>().And.Message.EqualTo("Mesh is not manifold!"),
                () => api.getMeshVolume(1)
            );
        }

        [Test]
        public void isPointInsideMeshTrueTest()
        {
            api.insertPoint(0, 0, 0);
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0, 1, 0);

            api.insertTriangle(1, 2, 3);
            api.insertTriangle(1, 2, 4);
            api.insertTriangle(1, 3, 4);
            api.insertTriangle(2, 3, 4);

            api.insertMesh(api.fetchTriangles(api.fetchPoints()));
            int pid = api.insertPoint(0.25, 0.25, 0.25);

            bool isPointInside = api.isPointInsideMesh(pid, 1);

            Assert.That(isPointInside, Is.True);
        }

        [Test]
        public void isPointInsideMeshFalseTest()
        {
            api.insertPoint(0, 0, 0);
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0, 1, 0);

            api.insertTriangle(1, 2, 3);
            api.insertTriangle(1, 2, 4);
            api.insertTriangle(1, 3, 4);
            api.insertTriangle(2, 3, 4);

            api.insertMesh(api.fetchTriangles(api.fetchPoints()));
            int pid = api.insertPoint(0.5, 0.5, 0.5);

            bool isPointInside = api.isPointInsideMesh(pid, 1);

            Assert.That(isPointInside, Is.False);
        }

        [Test]
        public void throwsWhenCheckingIfNonExistentPointIsInsideMeshTest()
        {
            api.insertMesh(new List<Triangle>());

            Assert.Throws(
                Is.TypeOf<ArgumentException>().And.Message.EqualTo("Mesh is not manifold or point doesn't exist!"),
                () => api.isPointInsideMesh(1, 1)
            );
        }

        [Test]
        public void throwsWhenCheckingIfPointIsInsideNonExistentMeshTest()
        {
            api.insertPoint(1, 1, 1);

            Assert.Throws(
                Is.TypeOf<SqlNullValueException>(),
                () => api.isPointInsideMesh(1, 1)
            );
        }

        [Test]
        public void throwsWhenCheckingIfPointIsInsideNonManifoldMeshTest()
        {
            api.insertPoint(1,1,1);
            api.insertMesh(new List<Triangle>());

            Assert.Throws(
                Is.TypeOf<ArgumentException>().And.Message.EqualTo("Mesh is not manifold or point doesn't exist!"),
                () => api.isPointInsideMesh(1, 1)
            );
        }

        [Test]
        public void isPointOnTriangleTrueTest()
        {
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0, 1, 0);
            api.insertTriangle(1, 2, 3);
            api.insertPoint(0.3333, 0.3333, 0.3333);

            bool isPointOnTriangle = api.isPointOnTriangle(4, 1);

            Assert.That(isPointOnTriangle, Is.True);
        }

        [Test]
        public void isPointOnTriangleFalseTest()
        {
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0, 1, 0);
            api.insertTriangle(1, 2, 3);
            api.insertPoint(0,0,0);

            bool isPointOnTriangle = api.isPointOnTriangle(4, 1);

            Assert.That(isPointOnTriangle, Is.False);
        }

        [Test]
        public void pointIsOnTriangleWhenItIsItsVertexTest()
        {
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0, 1, 0);
            api.insertTriangle(1, 2, 3);

            bool isPointOnTriangle = api.isPointOnTriangle(3, 1);

            Assert.That(isPointOnTriangle, Is.True);
        }

        [Test]
        public void pointIsOnTriangleWhenItIsOnItsEdgeTest()
        {
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0, 1, 0);
            api.insertTriangle(1, 2, 3);
            api.insertPoint(0.5, 0, 0.5);

            bool isPointOnTriangle = api.isPointOnTriangle(4, 1);

            Assert.That(isPointOnTriangle, Is.True);
        }

        [Test]
        public void throwsWhenCheckingIfNonExistentPointIsOnTriangleTest()
        {
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0, 1, 0);
            api.insertTriangle(1, 2, 3);

            Assert.Throws(
                Is.TypeOf<ArgumentException>().And.Message.EqualTo("Given point or triangle doesn't exist!"),
                () => api.isPointOnTriangle(4, 1)
            );
        }

        [Test]
        public void throwsWhenCheckingIfPointIsOnNonExistentTriangleTest()
        {
            api.insertPoint(0, 0, 0);

            Assert.Throws(
                Is.TypeOf<ArgumentException>().And.Message.EqualTo("Given point or triangle doesn't exist!"),
                () => api.isPointOnTriangle(1, 1)
            );
        }

        [Test]
        public void isPointOnMeshTrueTest()
        {
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0, 1, 0);
            api.insertTriangle(1, 2, 3);
            api.insertMesh(api.fetchTriangles(api.fetchPoints()));
            api.insertPoint(0.3333, 0.3333, 0.3333);

            bool isPointOnTriangle = api.isPointOnMesh(4, 1);

            Assert.That(isPointOnTriangle, Is.True);
        }

        [Test]
        public void isPointOnMeshFalseTest()
        {
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0, 1, 0);
            api.insertTriangle(1, 2, 3);
            api.insertMesh(api.fetchTriangles(api.fetchPoints()));
            api.insertPoint(0, 0, 0);

            bool isPointOnTriangle = api.isPointOnMesh(4, 1);

            Assert.That(isPointOnTriangle, Is.False);
        }

        [Test]
        public void pointIsOnMeshWhenItIsItsVertexTest()
        {
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0, 1, 0);
            api.insertTriangle(1, 2, 3);
            api.insertMesh(api.fetchTriangles(api.fetchPoints()));

            bool isPointOnTriangle = api.isPointOnMesh(3, 1);

            Assert.That(isPointOnTriangle, Is.True);
        }

        [Test]
        public void pointIsOnMeshWhenItIsOnItsEdgeTest()
        {
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0, 1, 0);
            api.insertTriangle(1, 2, 3);
            api.insertMesh(api.fetchTriangles(api.fetchPoints()));
            api.insertPoint(0.5, 0, 0.5);

            bool isPointOnTriangle = api.isPointOnMesh(4, 1);

            Assert.That(isPointOnTriangle, Is.True);
        }

        [Test]
        public void throwsWhenCheckingIfNonExistentPointIsOnMeshTest()
        {
            api.insertPoint(1, 0, 0);
            api.insertPoint(0, 0, 1);
            api.insertPoint(0, 1, 0);
            api.insertTriangle(1, 2, 3);
            api.insertMesh(api.fetchTriangles(api.fetchPoints()));

            Assert.Throws(
                Is.TypeOf<ArgumentException>().And.Message.EqualTo("Given point or mesh doesn't exist!"),
                () => api.isPointOnMesh(4, 1)
            );
        }

        [Test]
        public void throwsWhenCheckingIfPointIsOnNonExistentMeshTest()
        {
            api.insertPoint(0, 0, 0);

            Assert.Throws(
                Is.TypeOf<ArgumentException>().And.Message.EqualTo("Given point or mesh doesn't exist!"),
                () => api.isPointOnMesh(1, 1)
            );
        }





    }
}