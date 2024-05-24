USE [master];
GO

-- destroy db

IF DB_ID (N'TriangleMeshAPI') IS NOT NULL
begin
    alter database [TriangleMeshAPI] set single_user with rollback immediate;
    DROP DATABASE [TriangleMeshAPI];
end

go

