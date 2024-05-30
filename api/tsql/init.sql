USE [master];
GO



-- create db

CREATE DATABASE [TriangleMeshAPI];
GO

USE [TriangleMeshAPI];
GO

-- create types

CREATE ASSEMBLY [TriangleMeshTypes] FROM
'C:\Users\Gienio\desktop safe place\school\sem6\bd\projekt\api\solution\SPS_scripts\Types.dll';
GO

CREATE TYPE [Point3D]
EXTERNAL NAME [TriangleMeshTypes].[TriangleMeshNamespace.Point3D];  
GO


-- create aggregates

CREATE ASSEMBLY [TriangleMeshAggregates] FROM
'C:\Users\Gienio\desktop safe place\school\sem6\bd\projekt\api\solution\SPS_scripts\Aggregates.dll';
GO

CREATE AGGREGATE [MinX] (@point [Point3D])
RETURNS FLOAT
EXTERNAL NAME [TriangleMeshAggregates].[TriangleMeshNamespace.MinXAggregate];  
GO

CREATE AGGREGATE [MinY] (@point [Point3D])
RETURNS FLOAT
EXTERNAL NAME [TriangleMeshAggregates].[TriangleMeshNamespace.MinYAggregate];  
GO

CREATE AGGREGATE [MinZ] (@point [Point3D])
RETURNS FLOAT
EXTERNAL NAME [TriangleMeshAggregates].[TriangleMeshNamespace.MinZAggregate];  
GO


-- create functions

CREATE ASSEMBLY [TriangleMeshFunctions] FROM
'C:\Users\Gienio\desktop safe place\school\sem6\bd\projekt\api\solution\SPS_scripts\Functions.dll';
GO

CREATE FUNCTION [getDistance] (@a [Point3D], @b [Point3D])
RETURNS FLOAT
WITH RETURNS NULL ON NULL INPUT
AS EXTERNAL NAME
[TriangleMeshFunctions].[TriangleMeshNamespace.UserDefinedFunctions].[GetDistance];
GO
	
CREATE FUNCTION [getTriangleArea] (@a [Point3D], @b [Point3D], @c [Point3D])
RETURNS FLOAT
WITH RETURNS NULL ON NULL INPUT
AS EXTERNAL NAME
[TriangleMeshFunctions].[TriangleMeshNamespace.UserDefinedFunctions].[GetTriangleArea];
GO

CREATE FUNCTION [checkIfPointInTriangle] (@p [Point3D], @a [Point3D], @b [Point3D], @c [Point3D])
RETURNS BIT
WITH RETURNS NULL ON NULL INPUT
AS EXTERNAL NAME
[TriangleMeshFunctions].[TriangleMeshNamespace.UserDefinedFunctions].[CheckIfPointInTriangle];
GO

CREATE FUNCTION [doesRayIntersect] (@a [Point3D], @b [Point3D], @c [Point3D], @r1 [Point3D], @r2 [Point3D])
RETURNS BIT
WITH RETURNS NULL ON NULL INPUT
AS EXTERNAL NAME
[TriangleMeshFunctions].[TriangleMeshNamespace.UserDefinedFunctions].[DoesRayIntersect];
GO

CREATE FUNCTION [calculateMeshVolume] (@meshString nvarchar(max))
RETURNS FLOAT
WITH RETURNS NULL ON NULL INPUT
AS EXTERNAL NAME
[TriangleMeshFunctions].[TriangleMeshNamespace.UserDefinedFunctions].[CalculateMeshVolume];
GO

CREATE FUNCTION [createPoint3D] (@x FLOAT, @y FLOAT, @z FLOAT)
RETURNS [Point3D]
WITH RETURNS NULL ON NULL INPUT
AS EXTERNAL NAME
[TriangleMeshFunctions].[TriangleMeshNamespace.UserDefinedFunctions].[CreatePoint3D];
GO

CREATE FUNCTION [isMeshIsManifold] (@triangles NVARCHAR(max))
RETURNS BIT
WITH RETURNS NULL ON NULL INPUT
AS EXTERNAL NAME
[TriangleMeshFunctions].[TriangleMeshNamespace.UserDefinedFunctions].[IsMeshIsManifold];
GO

CREATE FUNCTION [checkIfMeshIsManifold] (@id int)
RETURNS BIT
BEGIN
	if (not exists( 
		select null 
		from [Mesh]
		where id = @id
	))
	begin
		return null;
	end;

	if (not exists( 
		select null 
		from [MeshTriangle]
		where mesh_id = @id
	))
	begin
		return 0;
	end;

	DECLARE @triangles AS NVARCHAR(max);

	SELECT @triangles = STRING_AGG(CONCAT(point_id1, '-', point_id2, '-', point_id3), ';')
	FROM [MeshTriangle] mt JOIN [Triangle] t ON (t.id = mt.triangle_id) 
	WHERE mesh_id = @id;

	RETURN [dbo].[isMeshIsManifold](@triangles);
END;
GO

-- if the mesh was surrounded by the smallest possible box with sides parallel to the 3 dimensions,
-- get the corner with the smallest coordinates and subtract 1 from all of them
CREATE FUNCTION [getPointBeyondMinimalMeshBox] (@meshId int)
RETURNS [Point3D]
AS
BEGIN
	if (not exists(
		select null
		from [Mesh] 
		where id = @meshId
	))
	begin
		return null;
	end;

	DECLARE @x FLOAT, @y FLOAT, @z FLOAT;

	SELECT @x = [dbo].[MinX](coordinates), @y = [dbo].[MinY](coordinates), @z = [dbo].[MinZ](coordinates)
	FROM [MeshTriangle] mt JOIN [Triangle] t ON (t.id = mt.triangle_id) 
	JOIN [Point] p ON (p.id IN (t.point_id1, t.point_id2, t.point_id3)) WHERE mesh_id = @meshId;

	RETURN [dbo].[createPoint3D](@x - 1, @y - 1, @z - 1);
END;
GO


-- create tables

CREATE TABLE [Point] ( 
	id int IDENTITY (1,1) NOT NULL, 
	coordinates [Point3D] NOT NULL,
	CONSTRAINT pk_point PRIMARY KEY CLUSTERED (id),
);
GO

CREATE TABLE [Triangle] ( 
	id int IDENTITY (1,1) NOT NULL, 
	point_id1 int NOT NULL,
	point_id2 int NOT NULL,
	point_id3 int NOT NULL,
	CONSTRAINT pk_triangle PRIMARY KEY CLUSTERED (id),
	CONSTRAINT fk_triangle_point1 FOREIGN KEY (point_id1) REFERENCES [Point] (id),
	CONSTRAINT fk_triangle_point2 FOREIGN KEY (point_id2) REFERENCES [Point] (id),
	CONSTRAINT fk_triangle_point3 FOREIGN KEY (point_id3) REFERENCES [Point] (id),
	CONSTRAINT unique_points UNIQUE(point_id1, point_id2, point_id3),
	CHECK (point_id1 < point_id2),
	CHECK (point_id2 < point_id3)
);
GO

CREATE TABLE [Mesh] ( 
	id int IDENTITY (1,1) NOT NULL, 
	isManifold AS ([dbo].[checkIfMeshIsManifold](id)),
	CONSTRAINT pk_mesh PRIMARY KEY CLUSTERED (id),
);
GO

CREATE TABLE [MeshTriangle] ( 
	mesh_id int NOT NULL,
	triangle_id int NOT NULL,
	CONSTRAINT pk_meshtriangle PRIMARY KEY CLUSTERED (mesh_id, triangle_id),
	CONSTRAINT fk_meshtriangle_mesh FOREIGN KEY (mesh_id) REFERENCES [Mesh] (id) ON DELETE CASCADE,
	CONSTRAINT fk_meshtriangle_triangle FOREIGN KEY (triangle_id) REFERENCES [Triangle] (id) ON DELETE CASCADE,
);
GO


-- create triggers

CREATE TRIGGER [deleteTrianglesThatUsedDeletedPointTrigger] ON [Point] 
INSTEAD OF DELETE AS
BEGIN
	DELETE FROM [Triangle] 
	WHERE (point_id1 IN (SELECT id from deleted)) OR (point_id2 IN (SELECT id from deleted)) OR (point_id3 IN (SELECT id from deleted));

	DELETE FROM [Point] WHERE id IN (SELECT id from deleted);
END;
GO

CREATE TRIGGER [deleteMeshesThatHaveNoTrianglesTrigger] ON [MeshTriangle] 
AFTER DELETE AS
BEGIN
	DELETE FROM [Mesh]
	WHERE id IN (SELECT mesh_id from deleted)
	AND NOT EXISTS(
		SELECT NULL 
		FROM [MeshTriangle] mt
		WHERE mt.mesh_id = id
	);
END;
GO

CREATE TRIGGER [resetIdentityOnPointTrigger] ON [Point] 
AFTER DELETE AS
BEGIN
	DECLARE @max INT;
	SELECT @max=ISNULL(MAX(id), 0) FROM [Point]; 
	DBCC CHECKIDENT ('[Point]', RESEED, @max);
END;
GO

CREATE TRIGGER [resetIdentityOnTriangleTrigger] ON [Triangle] 
AFTER DELETE AS
BEGIN
	DECLARE @max INT;
	SELECT @max=ISNULL(MAX(id), 0) FROM [Triangle]; 
	DBCC CHECKIDENT ('[Triangle]', RESEED, @max);
END;
GO

CREATE TRIGGER [resetIdentityOnMeshTrigger] ON [Mesh] 
AFTER DELETE AS
BEGIN
	DECLARE @max INT;
	SELECT @max=ISNULL(MAX(id), 0) FROM [Mesh]; 
	DBCC CHECKIDENT ('[Mesh]', RESEED, @max);
END;
GO


-- create procedures

CREATE PROCEDURE [isPointOnMesh] (
	@pointId int,
	@meshId int
)
AS BEGIN
	if (not exists(
		select null
		from [Mesh]
		where id = @meshId
	))
	begin
		select null;
		return;
	end;
	if (not exists(
		select null
		from [Point]
		where id = @pointId
	))
	begin
		select null;
		return;
	end;
	if (exists(
		select null
		from [MeshTriangle] mt join [Triangle] t on (t.id = mt.triangle_id)
		where (mesh_id = @meshId) and (@pointId in (point_id1, point_id2, point_id3))
	))
	begin
		select 1;
		return;
	end;

	DECLARE @point AS [Point3D];
	SET @point = (SELECT coordinates FROM [Point] WHERE id = @pointId);

	with p1 as (
		SELECT p.coordinates, t.point_id1, t.id
		FROM [MeshTriangle] mt JOIN [Triangle] t ON (mt.triangle_id = t.id) JOIN [Point] p ON (t.point_id1 = p.id)
		WHERE mt.mesh_id = @meshId
	), p2 as (
		SELECT p.coordinates, t.point_id2, t.id
		FROM [MeshTriangle] mt JOIN [Triangle] t ON (mt.triangle_id = t.id) JOIN [Point] p ON (t.point_id2 = p.id)
		WHERE mt.mesh_id = @meshId
	), p3 as (
		SELECT p.coordinates, t.point_id3, t.id
		FROM [MeshTriangle] mt JOIN [Triangle] t ON (mt.triangle_id = t.id) JOIN [Point] p ON (t.point_id3 = p.id)
		WHERE mt.mesh_id = @meshId
	)
	SELECT CASE
		WHEN 1 = ANY(
			SELECT [dbo].[checkIfPointInTriangle](@point, p1.coordinates, p2.coordinates, p3.coordinates)
			FROM p1 JOIN p2 ON (p1.id = p2.id) JOIN p3 ON (p1.id = p3.id)
		)
		THEN 1 ELSE 0
	END;
END;
GO

CREATE PROCEDURE [isPointOnTriangle] (
	@pointId int,
	@triangleId int
)
AS BEGIN
	if (not exists(
		select null
		from [Triangle]
		where id = @triangleId
	))
	begin
		select null;
		return;
	end;
	if (not exists(
		select null
		from [Point]
		where id = @pointId
	))
	begin
		select null;
		return;
	end;
	if (exists(
		select null
		from [Triangle]
		where @pointId in (point_id1, point_id2, point_id3)
		and id = @triangleId
	))
	begin
		select 1;
		return;
	end;

	DECLARE @point AS [Point3D];
	SET @point = (SELECT coordinates FROM [Point] WHERE id = @pointId);

	with coords as (
		select ROW_NUMBER() over (order by p.id) as rn, coordinates
		from Triangle t join Point p on p.id in (t.point_id1, t.point_id2, t.point_id3)
		where t.id = @triangleId
	)
	SELECT CASE
		WHEN 1 = (
			SELECT TOP 1 [dbo].[checkIfPointInTriangle](
				@point, 
				(SELECT coordinates FROM coords WHERE rn = 1),
				(SELECT coordinates FROM coords WHERE rn = 2),
				(SELECT coordinates FROM coords WHERE rn = 3)
			)
			FROM coords
		)
		THEN 1 ELSE 0
	END;
END;
GO

CREATE PROCEDURE [getMeshArea] (@meshId int)
AS BEGIN
	if (not exists(
		select null
		from [Mesh] 
		where id = @meshId
	))
	begin
		select null;
		return;
	end;

	if (not exists(
		select null
		from [MeshTriangle] 
		where mesh_id = @meshId
	))
	begin
		select 0;
		return;
	end;

	with p1 as (
		SELECT p.coordinates, t.point_id1, t.id
		FROM [MeshTriangle] mt JOIN [Triangle] t ON (mt.triangle_id = t.id) JOIN [Point] p ON (t.point_id1 = p.id)
		WHERE mt.mesh_id = @meshId
	), p2 as (
		SELECT p.coordinates, t.point_id2, t.id
		FROM [MeshTriangle] mt JOIN [Triangle] t ON (mt.triangle_id = t.id) JOIN [Point] p ON (t.point_id2 = p.id)
		WHERE mt.mesh_id = @meshId
	), p3 as (
		SELECT p.coordinates, t.point_id3, t.id
		FROM [MeshTriangle] mt JOIN [Triangle] t ON (mt.triangle_id = t.id) JOIN [Point] p ON (t.point_id3 = p.id)
		WHERE mt.mesh_id = @meshId
	)
	SELECT SUM([dbo].[getTriangleArea](p1.coordinates, p2.coordinates, p3.coordinates))
	FROM p1 JOIN p2 ON (p1.id = p2.id) JOIN p3 ON (p1.id = p3.id);
END;
GO

CREATE PROCEDURE [getMeshVolume] (@meshId int)
AS BEGIN
	IF ((SELECT isManifold FROM [Mesh] WHERE id = @meshId) = 0)
		SELECT NULL;
	ELSE
	BEGIN
		DECLARE @meshString AS NVARCHAR(max);

		with p1 as (
			SELECT p.coordinates as coords, t.point_id1 as pid, t.id as tid
			FROM [MeshTriangle] mt JOIN [Triangle] t ON (mt.triangle_id = t.id) JOIN [Point] p ON (t.point_id1 = p.id)
			WHERE mt.mesh_id = @meshId
		), p2 as (
			SELECT p.coordinates as coords, t.point_id2 as pid, t.id as tid
			FROM [MeshTriangle] mt JOIN [Triangle] t ON (mt.triangle_id = t.id) JOIN [Point] p ON (t.point_id2 = p.id)
			WHERE mt.mesh_id = @meshId
		), p3 as (
			SELECT p.coordinates as coords, t.point_id3 as pid, t.id as tid
			FROM [MeshTriangle] mt JOIN [Triangle] t ON (mt.triangle_id = t.id) JOIN [Point] p ON (t.point_id3 = p.id)
			WHERE mt.mesh_id = @meshId
		)
		SELECT @meshString = STRING_AGG(CONCAT(p1.pid, ',', CAST(p1.coords as nvarchar(max)), '#', p2.pid, ',', CAST(p2.coords as nvarchar(max)), '#', p3.pid, ',', CAST(p3.coords as nvarchar(max))), ';')
		FROM p1 JOIN p2 ON (p1.tid = p2.tid) JOIN p3 ON (p1.tid = p3.tid);

		SELECT [dbo].[calculateMeshVolume](@meshString);
	END;
END;
GO

CREATE PROCEDURE [countCollisionsWithMeshFromOutsideMeshToPoint] (
	@pointId int, 
	@meshId int,
	@outsideX float,
	@outsideY float,
	@outsideZ float
)
AS BEGIN
	IF ((SELECT isManifold FROM [Mesh] WHERE id = @meshId) = 0)
		SELECT NULL;
	ELSE
	BEGIN
		DECLARE @point AS [Point3D];
		SET @point = (SELECT coordinates FROM [Point] WHERE id = @pointId);		
		DECLARE @outsidePoint AS [Point3D];
		SET @outsidePoint = [dbo].[createPoint3D](@outsideX, @outsideY, @outsideZ);

		with p1 as (
			SELECT p.coordinates, t.point_id1, t.id
			FROM [MeshTriangle] mt JOIN [Triangle] t ON (mt.triangle_id = t.id) JOIN [Point] p ON (t.point_id1 = p.id)
			WHERE mt.mesh_id = @meshId
		), p2 as (
			SELECT p.coordinates, t.point_id2, t.id
			FROM [MeshTriangle] mt JOIN [Triangle] t ON (mt.triangle_id = t.id) JOIN [Point] p ON (t.point_id2 = p.id)
			WHERE mt.mesh_id = @meshId
		), p3 as (
			SELECT p.coordinates, t.point_id3, t.id
			FROM [MeshTriangle] mt JOIN [Triangle] t ON (mt.triangle_id = t.id) JOIN [Point] p ON (t.point_id3 = p.id)
			WHERE mt.mesh_id = @meshId
		)
		SELECT SUM(CAST([dbo].[doesRayIntersect](p1.coordinates, p2.coordinates, p3.coordinates, @point, @outsidePoint) AS INT))
		FROM p1 JOIN p2 ON (p1.id = p2.id) JOIN p3 ON (p1.id = p3.id);
	END;
END;
