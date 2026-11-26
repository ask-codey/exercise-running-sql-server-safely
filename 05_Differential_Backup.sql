-- 05. Make a change, then take a differential backup, so you can prove later that a restore brought the change back.
--     First create a table named BackupDrill with a NoteId (int, IDENTITY, primary key) and a Note (nvarchar(100), NOT NULL),
--     and insert one note: 'Added between the full and the differential backup'.
--     Then take a differential backup of Northwind into Northwind_Diff.bak, replacing anything already in that file.
--     Run 04 first, then this file, in SSMS.
-- Expect: Northwind has a BackupDrill table holding the note, and SQL Server's list of backups has a differential taken after the latest full backup.

-- Write your SQL below.
