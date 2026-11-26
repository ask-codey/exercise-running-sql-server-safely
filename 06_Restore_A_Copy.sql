-- 06. Restore your two backups as a copy named Northwind_Restored, beside the real Northwind: the full backup first, then the differential.
--     The copy needs files of its own: MOVE the logical file Northwind to Northwind_Restored.mdf and Northwind_log to Northwind_Restored_log.ldf,
--     in SQL Server's DATA folder (RESTORE FILELISTONLY shows the folder). Leave the copy open for use when you're done.
--     Run this file yourself in SSMS, after 04 and 05.
-- Expect: The database Northwind_Restored is ONLINE, and its BackupDrill table holds the note 'Added between the full and the differential backup'.

-- Write your SQL below.
