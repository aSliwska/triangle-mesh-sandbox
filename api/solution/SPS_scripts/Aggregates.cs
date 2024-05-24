using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Server;


namespace TriangleMeshNamespace
{
    [Serializable]
    [Microsoft.SqlServer.Server.SqlUserDefinedAggregate(
        Format.Native, 
        IsInvariantToDuplicates = true,
        IsInvariantToNulls = true,
        IsInvariantToOrder = true,
        IsNullIfEmpty = true
    )]
    public struct MinXAggregate
    {
        private SqlDouble minX;
        public void Init()
        {
            this.minX = SqlDouble.MaxValue;
        }

        public void Accumulate(Point3D point)
        {
            if (this.minX > point.X)
            {
                this.minX = point.X;
            }
        }

        public void Merge (MinXAggregate Group)
        {
            if (this.minX > Group.minX)
            {
                this.minX = Group.minX;
            }
        }

        public SqlDouble Terminate ()
        {
            return this.minX;
        }
    }

    [Serializable]
    [Microsoft.SqlServer.Server.SqlUserDefinedAggregate(
        Format.Native,
        IsInvariantToDuplicates = true,
        IsInvariantToNulls = true,
        IsInvariantToOrder = true,
        IsNullIfEmpty = true
    )]
    public struct MinYAggregate
    {
        private SqlDouble minY;
        public void Init()
        {
            this.minY = SqlDouble.MaxValue;
        }

        public void Accumulate(Point3D point)
        {
            if (this.minY > point.Y)
            {
                this.minY = point.Y;
            }
        }

        public void Merge(MinYAggregate Group)
        {
            if (this.minY > Group.minY)
            {
                this.minY = Group.minY;
            }
        }

        public SqlDouble Terminate()
        {
            return this.minY;
        }
    }

    [Serializable]
    [Microsoft.SqlServer.Server.SqlUserDefinedAggregate(
        Format.Native,
        IsInvariantToDuplicates = true,
        IsInvariantToNulls = true,
        IsInvariantToOrder = true,
        IsNullIfEmpty = true
    )]
    public struct MinZAggregate
    {
        private SqlDouble minZ;
        public void Init()
        {
            this.minZ = SqlDouble.MaxValue;
        }

        public void Accumulate(Point3D point)
        {
            if (this.minZ > point.Z)
            {
                this.minZ = point.Z;
            }
        }

        public void Merge(MinZAggregate Group)
        {
            if (this.minZ > Group.minZ)
            {
                this.minZ = Group.minZ;
            }
        }

        public SqlDouble Terminate()
        {
            return this.minZ;
        }
    }
}


