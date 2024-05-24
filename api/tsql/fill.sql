use [TriangleMeshAPI];
GO

if (exists(select null from triangle))
begin
	delete from triangle;
end;
go

if (exists(select null from point))
begin
	delete from point;
end;
go

if (exists(select null from mesh))
begin
	delete from mesh;
end;
go


INSERT INTO [Point] (coordinates) VALUES 
(CAST('0,0,0' AS [Point3D])),
(CAST('1,0,0' AS [Point3D])),
(CAST('1,0,1' AS [Point3D])),
(CAST('0,0,1' AS [Point3D])),
(CAST('0,1,0' AS [Point3D])),
(CAST('1,1,0' AS [Point3D])),
(CAST('1,1,1' AS [Point3D])),
(CAST('0,1,1' AS [Point3D])),
(CAST('0,1.5,1' AS [Point3D])),
(CAST('0.5,0.5,0.5' AS [Point3D])),
(CAST('0,1.25,1' AS [Point3D])),
(CAST('0.25,1.25,0.999999' AS [Point3D]))
;
GO

-- select * from point
-- select * from triangle
-- select * from meshtriangle
-- select * from mesh


INSERT INTO [Triangle] (point_id1, point_id2, point_id3) VALUES 
(1, 2, 5),
(2, 5, 6), 
(5, 6, 8), 
(6, 7, 8), 
(3, 7, 8), 
(3, 4, 8), 
(2, 3, 4), 
(1, 2, 4), 
(1, 4, 5), 
(4, 5, 8), 
(2, 3, 6), 
(3, 6, 7), 
(7, 8, 9)
;
GO

INSERT INTO [Mesh] DEFAULT VALUES;
GO
INSERT INTO [Mesh] DEFAULT VALUES;
GO

INSERT INTO [MeshTriangle] (triangle_id, mesh_id) VALUES 
(1, 1),
(2, 1),
(3, 1),
(4, 1),
(5, 1),
(6, 1),
(7, 1),
(8, 1),
(9, 1),
(10, 1),
(11, 1),
(12, 1),
(1, 2),
(2, 2),
(3, 2),
(4, 2),
(5, 2),
(6, 2),
(7, 2),
(8, 2),
(9, 2),
(10, 2),
(11, 2),
(12, 2),
(13, 2)
;
GO

