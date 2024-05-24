using NUnit.Framework;
using static TriangleMeshAPI;
using Moq;

namespace APITest
{
    [TestFixture]
    public class UnitTests
    {
        private TriangleMeshAPI api;

        [OneTimeSetUp]
        public void Setup()
        {
            api = new TriangleMeshAPI(
                @"DATA SOURCE=MSSQLServer; INITIAL CATALOG=TriangleMeshAPI; INTEGRATED SECURITY=SSPI; Persist Security Info=False; Server=GIENIO\SQLEXPRESS"
            );
        }

        [Test]
        public void PointToStringTest()
        {
            Point point = new Point(1, 0.2, 0.4, 3);
            Assert.That(point.ToString(), Is.EqualTo("(p 1: [0.2, 0.4, 3])"));
        }

        [Test]
        public void TriangleToStringTest()
        {
            var point1 = new Mock<Point>();
            point1.Setup(p => p.ToString()).Returns("a");
            var point2 = new Mock<Point>();
            point2.Setup(p => p.ToString()).Returns("b");
            var point3 = new Mock<Point>();
            point3.Setup(p => p.ToString()).Returns("c");

            Triangle triangle = new Triangle(2, point1.Object, point2.Object, point3.Object);
            Assert.That(triangle.ToString(), Is.EqualTo("(t 2: a, b, c)"));
        }

        [Test]
        public void MeshToStringTest()
        {
            var triangle1 = new Mock<Triangle>();
            triangle1.Setup(t => t.ToString()).Returns("a");
            var triangle2 = new Mock<Triangle>();
            triangle2.Setup(t => t.ToString()).Returns("b");

            Mesh mesh = new Mesh(1, new List<Triangle> { triangle1.Object, triangle2.Object });
            Assert.That(mesh.ToString(), Is.EqualTo("(m 1: \n\ta,\n\tb\n)"));
        }

        [Test]
        public void MeshWithOneTriangleToStringTest()
        {
            var triangle1 = new Mock<Triangle>();
            triangle1.Setup(t => t.ToString()).Returns("a");

            Mesh mesh = new Mesh(1, new List<Triangle> { triangle1.Object });
            Assert.That(mesh.ToString(), Is.EqualTo("(m 1: \n\ta\n)"));
        }

        [Test]
        public void MeshWithNoTrianglesToStringTest()
        {
            Mesh mesh = new Mesh(1, new List<Triangle>());
            Assert.That(mesh.ToString(), Is.EqualTo("(m 1: \n\t\n)"));
        }
    }
}