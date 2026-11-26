# Exercise: Running SQL Server Safely

Exercise SQL for the lesson **Exercise: Running SQL Server Safely** in the C# Full-Stack Bootcamp on [ask-codey.com](https://www.ask-codey.com).

Ten problems that keep the **Northwind** database fast and safe: indexes for slow searches, a full and a differential backup restored as a copy, a role with only the permissions a dashboard needs, a permission taken back, and a report that reads at the right isolation level. Each file holds one problem as a comment, and what the right answer looks like: write your SQL under it.

| File | What you do | Who runs it |
|---|---|---|
| `01_Index_For_The_Country_Search.sql` | A nonclustered index with `INCLUDE`, then the search | The tests |
| `02_Index_For_Each_Salesperson.sql` | An index on two columns, one of them `DESC` | The tests |
| `03_Unique_Category_Names.sql` | A unique index | The tests |
| `04_Full_Backup.sql` | A full backup | You, in SSMS |
| `05_Differential_Backup.sql` | A change, then a differential backup | You, in SSMS |
| `06_Restore_A_Copy.sql` | Both backups restored as `Northwind_Restored` | You, in SSMS |
| `07_Sales_Dashboard_Role.sql` | A role, its permissions and a user in it | The tests |
| `08_Take_A_Permission_Back.sql` | One `REVOKE`, under the lines that set the problem up | The tests |
| `09_Turn_On_Snapshot.sql` | Allow snapshot isolation on Northwind | You, in SSMS |
| `10_Price_List_Report.sql` | An isolation level and the report's `SELECT` | The tests |

## Getting started

1. Open SQL Server Management Studio, and connect to `localhost`.
2. Choose **File > Open > File...** and open `01_Index_For_The_Country_Search.sql`.
3. Choose **Northwind** in the database drop-down.
4. Write your SQL under the problem, and run the whole file with **F5**. Compare what you get with the file's **Expect** line.

Do the problems in order: 05 needs 04's backup, 06 needs both, and 10 needs 09.

## The files you run yourself: 04, 05, 06 and 09

A backup, a restore and `ALTER DATABASE` can't run inside a transaction, and they change your server, so the tests never run them. **You run each of those files once in SSMS**, and the tests check what SQL Server recorded: its list of backups, the restored copy, and the database's settings.

- **Backup files:** give only a file name, such as `'Northwind_Full.bak'`, and SQL Server writes it into its own backup folder, `C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\Backup`. SQL Server writes the file, not you, and it can't write into your own folders.
- **The restored copy's files** go in SQL Server's DATA folder, `C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\DATA`. If your SQL Server keeps its files somewhere else, `RESTORE FILELISTONLY FROM DISK = 'Northwind_Full.bak';` shows the folder.

## Check your answers

`SafetyExercise.Tests` checks all ten problems against your Northwind database. Nothing is changed: the files it runs (01, 02, 03, 07, 08 and 10) run inside a transaction that is rolled back, so the indexes, role and users you made stay as they are. It needs the .NET SDK, which you installed in the first week. Open a terminal in this folder and run:

```bash
dotnet test SafetyExercise.Tests
```

At first every test fails: that is expected. Each problem you solve turns its test green, and a failing test's message says what's still missing. The tests use the Northwind database on `localhost`; to use another one, set the `NORTHWIND_CONNECTION` environment variable to its connection string.

## Starting over

The index and role files use plain `CREATE`, and the tests drop what each file makes before running it, so you can fix a file and test it again. To run one again in SSMS, drop what it made first (`DROP INDEX IF EXISTS IX_Orders_ShipCountry ON Orders;`, `DROP USER IF EXISTS SalesDashboard;` then `DROP ROLE IF EXISTS SalesReporters;`). To redo 05, drop `BackupDrill` first. To put Northwind back as it was, run `Northwind.sql` from the course's sample databases again; then do 04, 05, 06 and 09 again.

When you're done, the extra objects are harmless to the rest of the course. To tidy up, drop the copy with `DROP DATABASE Northwind_Restored;` (from `master`).

## Tips

- Name the index and the columns exactly as each problem says: the tests look for those names.
- `CREATE INDEX` makes a nonclustered index; `UNIQUE` and `INCLUDE (...)` come where the lesson **Indexes: Finding Rows Fast** shows them.
- Restore the full backup `WITH NORECOVERY`, then the differential `WITH RECOVERY`. If the copy is stuck in **Restoring...**, run `RESTORE DATABASE Northwind_Restored WITH RECOVERY;`.
- `EXECUTE AS USER = 'SalesDashboard';` lets you try the dashboard's permissions yourself; `REVERT;` gives you your own back.
- For 10, open a second query window, run `BEGIN TRANSACTION; UPDATE Products SET UnitPrice = 20.00 WHERE ProductId = 1;` there, and run your report in the first: it should answer at once, with Chai at 18.00. Then run `ROLLBACK;` in the second window.

## Hand it in

When it works, push your solution to a public repo of your own on GitHub, then paste that repo's address into **Submit your exercise** on the lesson page at [ask-codey.com](https://www.ask-codey.com). That's what completes the lesson. The lesson page shows the Git commands, step by step.
