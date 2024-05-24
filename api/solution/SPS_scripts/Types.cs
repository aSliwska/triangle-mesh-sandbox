using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Globalization;
using Microsoft.SqlServer.Server;


namespace TriangleMeshNamespace
{
    [Serializable]
    [Microsoft.SqlServer.Server.SqlUserDefinedType(Format.Native)]
    public struct Point3D: INullable
    {
        private double _x;
        private double _y;
        private double _z;
        private bool _null;

        public override string ToString()
        {
            CultureInfo pointCulture = new CultureInfo("en")
            {
                NumberFormat = { NumberDecimalSeparator = "." }
            };
            return _x.ToString(pointCulture) + "," + _y.ToString(pointCulture) + "," + _z.ToString(pointCulture);
        }
    
        public bool IsNull
        {
            get { return _null; }
        }
    
        public static Point3D Null
        {
            get
            {
                Point3D p = new Point3D();
                p._null = true;
                return p;
            }
        }

        public Point3D(double x, double y, double z)
        {
            _x = x;
            _y = y;
            _z = z;
            _null = false;
        }
        public Point3D(bool nothing)
        {
            _x = _y = _z = 0;
            _null = true;
        }
        public double X
        {
            get { return _x; }
            set { _x = value; }
        }
        public double Y
        {
            get { return _y; }
            set { _y = value; }
        }
        public double Z
        {
            get { return _z; }
            set { _z = value; }
        }

        public static Point3D Parse(SqlString s)
        {
            string value = s.Value;

            if (s.IsNull || value.Trim() == "")
            {
                return Null;
            }

            int firstIdx = value.IndexOf(',');
            string xstr = value.Substring(0, firstIdx);

            int secondIdx = value.IndexOf(',', firstIdx + 1);
            string ystr = value.Substring(firstIdx + 1, secondIdx - firstIdx - 1);

            string zstr = value.Substring(secondIdx + 1, value.Length - secondIdx - 1);

            CultureInfo pointCulture = new CultureInfo("en")
            {
                NumberFormat = { NumberDecimalSeparator = "." }
            };

            double x = double.Parse(xstr, pointCulture);
            double y = double.Parse(ystr, pointCulture);
            double z = double.Parse(zstr, pointCulture);

            return new Point3D(x, y, z);
        }

        public Point3D getVectorTo(Point3D other)
        {
            return new Point3D(other._x - _x, other._y - _y, other._z - _z);
        }

        public bool equals(Point3D other)
        {
            if (other.IsNull)
            {
                return false;
            }
            return (other._x == _x) && (other._y == _y) && (other._z == _z);
        }
    }
}


