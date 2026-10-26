/* =========================================================================
   FILE   : docs/performance-queries.sql
   PURPOSE: measure query performance on 100,000 rows.
   SCHEMA : Status and Priority are nvarchar(20) holding the enum member name
            ("New", "InProgress", "Waiting", "Completed" / "Low", "Medium", "High").
            Filters therefore compare against N'...' strings, never integers.
   PREREQ : POST /api/dev/seed?count=100000&reset=true already executed.
   HOW    : sqlcmd -S "(localdb)\mssqllocaldb" -d RequestsDb -i docs\performance-queries.sql -o docs\perf.txt
            Get-Content docs\perf.txt
   ========================================================================= */
SET NOCOUNT ON;
GO

USE [RequestsDb];   -- <<< change to your actual database name
GO

-- Warm the buffer cache once so the numbers below are not dominated by the
-- very first physical read. This is NOT part of the measurement.
SELECT COUNT(*) FROM [Requests];
GO

PRINT '=========================================================';
PRINT ' SCHEMA CHECK';
PRINT '=========================================================';
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Requests' AND COLUMN_NAME IN ('Status','Priority');
GO

PRINT '=========================================================';
PRINT ' ROW COUNT + DISTRIBUTION';
PRINT '=========================================================';
SELECT COUNT(*) AS TotalRows FROM [Requests];
SELECT [Status], COUNT(*) AS Cnt FROM [Requests] GROUP BY [Status] ORDER BY [Status];
SELECT [Priority], COUNT(*) AS Cnt FROM [Requests] GROUP BY [Priority] ORDER BY [Priority];
GO


/* -------------------------------------------------------------------------
   BASELINE - the cost of a full scan. This is the reference number every
   indexed query below is compared against.
   ------------------------------------------------------------------------- */
PRINT '=========================================================';
PRINT ' BASELINE: forced full scan (no WHERE)';
PRINT '=========================================================';
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

SELECT COUNT(*) FROM [Requests] WITH (INDEX(0));

SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;
GO


/* -------------------------------------------------------------------------
   CASE 1 - paged list, default sort.
   Endpoint : GET /api/requests?page=1&pageSize=25
   Expected : Index Seek on IX_Requests_CreatedAt_Id, NO Sort operator - the
              index is declared (CreatedAt DESC, Id DESC), matching the query.
   ------------------------------------------------------------------------- */
PRINT '=========================================================';
PRINT ' CASE 1: default sort, paged  <- IX_Requests_CreatedAt_Id';
PRINT '=========================================================';
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

SELECT TOP (25) [Id], [Title], [Description], [OrganizationName], [Status],
                 [Priority], [AssignedTo], [CreatedAt], [UpdatedAt]
FROM [Requests]
ORDER BY [CreatedAt] DESC, [Id] DESC
OFFSET 0 ROWS FETCH NEXT 25 ROWS ONLY;

SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;
GO


/* -------------------------------------------------------------------------
   CASE 1b - deep paging: rows 2000-2024.
   Exposes the OFFSET cost: SQL Server still walks and discards the first
   2,000 rows. Compare its logical reads with CASE 1.
   ------------------------------------------------------------------------- */
PRINT '=========================================================';
PRINT ' CASE 1b: deep paging, OFFSET 2000  <- OFFSET cost';
PRINT '=========================================================';
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

SELECT TOP (25) [Id], [Title], [Status], [Priority], [CreatedAt]
FROM [Requests]
ORDER BY [CreatedAt] DESC, [Id] DESC
OFFSET 2000 ROWS FETCH NEXT 25 ROWS ONLY;

SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;
GO


/* -------------------------------------------------------------------------
   CASE 2 - status filter + default sort.
   Endpoint : GET /api/requests?status=New,InProgress&page=1&pageSize=25
   Expected : Index Seek on IX_Requests_Status_CreatedAt_Id, NO Sort.
   NOTE: the WHERE compares against the STORED enum names, not against ints.
   ------------------------------------------------------------------------- */
PRINT '=========================================================';
PRINT ' CASE 2: status filter  <- IX_Requests_Status_CreatedAt_Id';
PRINT '=========================================================';
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

SELECT TOP (25) [Id], [Title], [Status], [Priority], [CreatedAt]
FROM [Requests]
WHERE [Status] IN (N'New', N'InProgress')
ORDER BY [CreatedAt] DESC, [Id] DESC
OFFSET 0 ROWS FETCH NEXT 25 ROWS ONLY;

SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;
GO


/* -------------------------------------------------------------------------
   CASE 3 - status + priority combined (the dashboard's hot path).
   Expected : Index Seek on IX_Requests_Status_Priority_CreatedAt.
   ------------------------------------------------------------------------- */
PRINT '=========================================================';
PRINT ' CASE 3: status+priority  <- IX_Requests_Status_Priority_CreatedAt';
PRINT '=========================================================';
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

SELECT TOP (25) [Id], [Title], [Status], [Priority], [CreatedAt]
FROM [Requests]
WHERE [Status] = N'New' AND [Priority] = N'High'
ORDER BY [CreatedAt] DESC, [Id] DESC
OFFSET 0 ROWS FETCH NEXT 25 ROWS ONLY;

SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;
GO


/* -------------------------------------------------------------------------
   CASE 4 - free-text search (LIKE '%...%').
   Expected : FULL SCAN + Sort. This is the documented bottleneck; a leading
   wildcard cannot be seeked by any B-tree index.
   ------------------------------------------------------------------------- */
PRINT '=========================================================';
PRINT ' CASE 4: free-text search  <- BOTTLENECK (full scan)';
PRINT '=========================================================';
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

SELECT TOP (25) [Id], [Title], [OrganizationName]
FROM [Requests]
WHERE [Title] LIKE N'%פנייה 999%' OR [OrganizationName] LIKE N'%פנייה 999%'
ORDER BY [CreatedAt] DESC, [Id] DESC
OFFSET 0 ROWS FETCH NEXT 25 ROWS ONLY;

SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;
GO


/* -------------------------------------------------------------------------
   CASE 5 - prefix search (LIKE 'term%') - SARGable.
   This is the comparison that justifies StartsWith over Contains for the
   organisation / assignee filters.
   ------------------------------------------------------------------------- */
PRINT '=========================================================';
PRINT ' CASE 5: prefix search (SARGable)  <- vs CASE 4';
PRINT '=========================================================';
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

SELECT TOP (25) [Id], [Title], [OrganizationName]
FROM [Requests]
WHERE [OrganizationName] LIKE N'עיריית%'
ORDER BY [CreatedAt] DESC, [Id] DESC
OFFSET 0 ROWS FETCH NEXT 25 ROWS ONLY;

SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;
GO


/* -------------------------------------------------------------------------
   CASE 6 - history for a single request.
   Endpoint : GET /api/requests/{id}/history
   Expected : Index Seek on IX_StatusChanges_RequestId_ChangedAt.
   ------------------------------------------------------------------------- */
PRINT '=========================================================';
PRINT ' CASE 6: single request history  <- IX_StatusChanges_...';
PRINT '=========================================================';
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

SELECT TOP (25) [PreviousStatus], [NewStatus], [ChangedAt], [ChangedBy]
FROM [StatusChanges]
WHERE [RequestId] = 1
ORDER BY [ChangedAt] DESC;

SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;
GO


/* -------------------------------------------------------------------------
   CASE 7 - the aggregation endpoint, uncached (three scans).
   This is the measurement that justifies the cache.
   ------------------------------------------------------------------------- */
PRINT '=========================================================';
PRINT ' CASE 7: aggregations uncached  <- justification for cache';
PRINT '=========================================================';
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

SELECT COUNT(*) FROM [Requests];

SELECT [Status] AS [Key], COUNT(*) AS Cnt FROM [Requests] GROUP BY [Status];
SELECT [Priority] AS [Key], COUNT(*) AS Cnt FROM [Requests] GROUP BY [Priority];

SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;
GO


/* -------------------------------------------------------------------------
   PLAN INSPECTION - programmatic alternative to the SSMS visual plan.
   One row per operator with its object and index, so you can confirm
   Index Seek vs Index Scan vs Sort without opening a diagram.
   ------------------------------------------------------------------------- */
PRINT '=========================================================';
PRINT ' PLAN: CASE 1 (index seek expected, no sort)';
PRINT '=========================================================';
SET SHOWPLAN_ALL ON;
GO

SELECT TOP (25) [Id], [Title], [Status], [Priority], [CreatedAt]
FROM [Requests]
ORDER BY [CreatedAt] DESC, [Id] DESC
OFFSET 0 ROWS FETCH NEXT 25 ROWS ONLY;
GO

SET SHOWPLAN_ALL OFF;
GO

PRINT '=========================================================';
PRINT ' PLAN: CASE 4 (full scan expected - the bottleneck)';
PRINT '=========================================================';
SET SHOWPLAN_ALL ON;
GO

SELECT TOP (25) [Id], [Title], [OrganizationName]
FROM [Requests]
WHERE [Title] LIKE N'%פנייה 999%'
ORDER BY [CreatedAt] DESC, [Id] DESC
OFFSET 0 ROWS FETCH NEXT 25 ROWS ONLY;
GO

SET SHOWPLAN_ALL OFF;
GO

PRINT 'DONE. Copy the STATISTICS IO/TIME output into docs/performance.md';
GO