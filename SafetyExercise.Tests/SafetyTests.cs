using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace SafetyExercise.Tests;

// Checks your ten problems against your own Northwind database, and changes nothing.
// - 01, 02, 03, 07, 08 and 10: the tests run your file inside a transaction that is rolled back, so the indexes, role
//   and users you made in SSMS are still there afterwards.
// - 04, 05, 06 and 09: a backup, a restore and ALTER DATABASE can't run inside a transaction, so you run those files
//   yourself in SSMS, and the tests read what SQL Server recorded.
// You don't need to change this file.
[TestClass]
public sealed class SafetyTests
{
    // The NORTHWIND_CONNECTION environment variable, if set, points the tests at another database. Pooling is off, so
    // every test gets a fresh connection and one test's isolation level never carries over into another.
    private static readonly string connectionString = new SqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("NORTHWIND_CONNECTION")
        ?? "Server=localhost;Database=Northwind;Integrated Security=True;TrustServerCertificate=True")
    {
        Pooling = false
    }.ConnectionString;

    private const string DrillNote = "Added between the full and the differential backup";

    private static SqlConnection Open()
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }

    // A file's batches: its GO lines split it, as SSMS does.
    private static IEnumerable<string> Batches(string file)
    {
        string sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Queries", file));
        return Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Where(batch => !string.IsNullOrWhiteSpace(batch));
    }

    // Runs some SQL and returns every result it shows, in order.
    private static List<DataTable> Execute(SqlConnection connection, SqlTransaction? transaction, string sql,
        params SqlParameter[] parameters)
    {
        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddRange(parameters);
        using var reader = command.ExecuteReader();
        var results = new List<DataTable>();
        while (!reader.IsClosed && reader.FieldCount > 0)
        {
            var result = new DataTable();
            result.Load(reader);   // reads one result, then moves on to the next
            results.Add(result);
        }
        return results;
    }

    private static object? Scalar(SqlConnection connection, SqlTransaction? transaction, string sql,
        params SqlParameter[] parameters)
    {
        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddRange(parameters);
        object? value = command.ExecuteScalar();
        return value is DBNull ? null : value;
    }

    // Runs `before` (which drops what the file creates, so a file without IF EXISTS runs again), then the file, then
    // the check, all inside one transaction that is rolled back.
    private static void InRolledBackTransaction(string file, string before,
        Action<SqlConnection, SqlTransaction, List<DataTable>> check)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            if (before.Length > 0) Execute(connection, transaction, before);
            var results = new List<DataTable>();
            foreach (string batch in Batches(file)) results.AddRange(Execute(connection, transaction, batch));
            check(connection, transaction, results);
        }
        finally
        {
            try { transaction.Rollback(); } catch (InvalidOperationException) { } catch (SqlException) { }
        }
    }

    private sealed record IndexColumn(string Type, bool Unique, string Column, bool Descending, bool Included);

    // The columns of one index, its key columns in order first, then the included ones.
    private static List<IndexColumn> IndexColumns(SqlConnection connection, SqlTransaction transaction, string table, string index)
    {
        const string sql = """
            SELECT i.type_desc, i.is_unique, c.name, ic.is_descending_key, ic.is_included_column
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID(@table) AND i.name = @index
            ORDER BY ic.is_included_column, ic.key_ordinal, c.name;
            """;
        DataTable result = Execute(connection, transaction, sql,
            new SqlParameter("@table", table), new SqlParameter("@index", index))[0];
        return result.Rows.Cast<DataRow>()
            .Select(row => new IndexColumn((string)row[0], (bool)row[1], (string)row[2], (bool)row[3], (bool)row[4]))
            .ToList();
    }

    // Whether the user, borrowed with EXECUTE AS USER, has the permission: 1 or 0.
    private static int Can(SqlConnection connection, SqlTransaction transaction, string user, string table, string permission, string problem)
    {
        try
        {
            object? value = Scalar(connection, transaction,
                "EXECUTE AS USER = @user; SELECT HAS_PERMS_BY_NAME(@table, 'OBJECT', @permission); REVERT;",
                new SqlParameter("@user", user), new SqlParameter("@table", table), new SqlParameter("@permission", permission));
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (SqlException e) when (e.Number == 15517)
        {
            Assert.Fail($"{problem}: there's no user named {user} yet. Create it WITHOUT LOGIN.");
            return 0;
        }
    }

    // True when one of the row's values is the text.
    private static bool Has(DataRow row, string value) =>
        row.ItemArray.Any(cell => string.Equals(cell?.ToString(), value, StringComparison.Ordinal));

    // True when one of the row's values is the number: 18.0000 is the same answer as 18.00.
    private static bool Has(DataRow row, decimal value) =>
        row.ItemArray.Any(cell => Number(cell) is decimal number && Math.Abs(number - value) < 0.005m);

    // True when one of the row's values is that day.
    private static bool Has(DataRow row, DateTime day) =>
        row.ItemArray.OfType<DateTime>().Any(date => date.Date == day);

    private static decimal? Number(object? cell) =>
        cell is not null and not DBNull and not DateTime && decimal.TryParse(Convert.ToString(cell, CultureInfo.InvariantCulture),
            NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number) ? number : null;

    private static DataRow[] Rows(DataTable result) => result.Rows.Cast<DataRow>().ToArray();

    private static string Names(IEnumerable<IndexColumn> columns) => string.Join(", ", columns.Select(c => c.Column));

    [TestMethod]
    public void Problem01_CountryIndex_CoversTheBrazilSearch() =>
        InRolledBackTransaction("01_Index_For_The_Country_Search.sql", "DROP INDEX IF EXISTS IX_Orders_ShipCountry ON Orders;",
            (connection, transaction, results) =>
            {
                List<IndexColumn> columns = IndexColumns(connection, transaction, "dbo.Orders", "IX_Orders_ShipCountry");
                Assert.IsNotEmpty(columns, "01: create the index IX_Orders_ShipCountry on Orders.");
                Assert.AreEqual("NONCLUSTERED", columns[0].Type, "01: IX_Orders_ShipCountry is a nonclustered index.");
                Assert.AreEqual("ShipCountry", Names(columns.Where(c => !c.Included)), "01: the index is on ShipCountry only.");
                Assert.AreEqual("OrderDate, ShipCity", Names(columns.Where(c => c.Included).OrderBy(c => c.Column)),
                    "01: INCLUDE (OrderDate, ShipCity), so the search finds every column it needs in the index.");

                Assert.IsNotEmpty(results, "01: after the index, run the search for Brazil.");
                DataRow[] rows = Rows(results[^1]);
                Assert.HasCount(83, rows, "01: Brazil has 83 orders.");
                Assert.IsTrue(Has(rows[0], 10250m) && Has(rows[0], "Rio de Janeiro"),
                    "01: sorted by OrderId, the first is 10250, shipped to Rio de Janeiro.");
            });

    [TestMethod]
    public void Problem02_SalespersonIndex_PutsEmployeeIdFirstAndOrderDateNewestFirst() =>
        InRolledBackTransaction("02_Index_For_Each_Salesperson.sql", "DROP INDEX IF EXISTS IX_Orders_EmployeeId_OrderDate ON Orders;",
            (connection, transaction, results) =>
            {
                List<IndexColumn> columns = IndexColumns(connection, transaction, "dbo.Orders", "IX_Orders_EmployeeId_OrderDate");
                Assert.IsNotEmpty(columns, "02: create the index IX_Orders_EmployeeId_OrderDate on Orders.");
                Assert.AreEqual("NONCLUSTERED", columns[0].Type, "02: IX_Orders_EmployeeId_OrderDate is a nonclustered index.");
                Assert.AreEqual("EmployeeId, OrderDate", Names(columns.Where(c => !c.Included)),
                    "02: the index is on two columns, EmployeeId first, then OrderDate.");
                Assert.IsFalse(columns[0].Descending, "02: EmployeeId is in the usual order (ASC).");
                Assert.IsTrue(columns[1].Descending, "02: OrderDate is newest first: write DESC after it.");

                Assert.IsNotEmpty(results, "02: after the index, list employee 5's three newest orders.");
                DataRow[] rows = Rows(results[^1]);
                Assert.HasCount(3, rows, "02: the three newest orders, so 3 rows.");
                Assert.IsTrue(Has(rows[0], 11043m) && Has(rows[0], new DateTime(2018, 4, 22)), "02: the first is 11043, from 2018-04-22.");
                Assert.IsTrue(Has(rows[1], 10954m) && Has(rows[1], new DateTime(2018, 3, 17)), "02: the second is 10954, from 2018-03-17.");
                Assert.IsTrue(Has(rows[2], 10922m) && Has(rows[2], new DateTime(2018, 3, 3)), "02: the third is 10922, from 2018-03-03.");
            });

    [TestMethod]
    public void Problem03_UniqueCategoryNames_RefusesASecondBeverages() =>
        InRolledBackTransaction("03_Unique_Category_Names.sql", "DROP INDEX IF EXISTS UX_Categories_CategoryName ON Categories;",
            (connection, transaction, results) =>
            {
                List<IndexColumn> columns = IndexColumns(connection, transaction, "dbo.Categories", "UX_Categories_CategoryName");
                Assert.IsNotEmpty(columns, "03: create the index UX_Categories_CategoryName on Categories.");
                Assert.IsTrue(columns[0].Unique, "03: make the index UNIQUE.");
                Assert.AreEqual("NONCLUSTERED", columns[0].Type, "03: UX_Categories_CategoryName is a nonclustered index.");
                Assert.AreEqual("CategoryName", Names(columns), "03: the index is on CategoryName.");

                SqlException refused = Assert.Throws<SqlException>(() =>
                    Execute(connection, transaction, "INSERT INTO Categories (CategoryName) VALUES ('Beverages');"),
                    "03: a second category named Beverages should be refused.");
                Assert.AreEqual(2601, refused.Number, "03: the unique index refuses the duplicate name (Msg 2601).");
            });

    [TestMethod]
    public void Problem04_FullBackup_IsInSqlServersListOfBackups()
    {
        using var connection = Open();
        object? finished = Scalar(connection, null, """
            SELECT MAX(b.backup_finish_date)
            FROM msdb.dbo.backupset b
            JOIN msdb.dbo.backupmediafamily m ON m.media_set_id = b.media_set_id
            WHERE b.database_name = DB_NAME() AND b.type = 'D' AND m.physical_device_name LIKE '%Northwind_Full.bak';
            """);
        Assert.IsNotNull(finished, "04: SQL Server has no full backup of this database in Northwind_Full.bak yet. Run 04_Full_Backup.sql in SSMS.");
    }

    [TestMethod]
    public void Problem05_DifferentialBackup_FollowsTheFullBackupAndTheNote()
    {
        using var connection = Open();
        try
        {
            object? notes = Scalar(connection, null, "SELECT COUNT(*) FROM BackupDrill WHERE Note = @note;", new SqlParameter("@note", DrillNote));
            Assert.AreEqual(1, Convert.ToInt32(notes, CultureInfo.InvariantCulture),
                $"05: BackupDrill should hold one note: '{DrillNote}'.");
        }
        catch (SqlException e) when (e.Number == 208)
        {
            Assert.Fail("05: there's no BackupDrill table yet. Create it, insert the note, then take the differential backup.");
        }

        object? differentials = Scalar(connection, null, """
            DECLARE @lastFull datetime = (SELECT MAX(backup_finish_date) FROM msdb.dbo.backupset WHERE database_name = DB_NAME() AND type = 'D');
            SELECT COUNT(*)
            FROM msdb.dbo.backupset b
            JOIN msdb.dbo.backupmediafamily m ON m.media_set_id = b.media_set_id
            WHERE b.database_name = DB_NAME() AND b.type = 'I' AND b.backup_finish_date >= @lastFull
              AND m.physical_device_name LIKE '%Northwind_Diff.bak';
            """);
        Assert.IsTrue(Convert.ToInt32(differentials, CultureInfo.InvariantCulture) > 0,
            "05: SQL Server has no differential backup in Northwind_Diff.bak taken after your latest full backup. Run 04, then 05, in SSMS.");
    }

    [TestMethod]
    public void Problem06_RestoredCopy_IsOnlineWithTheNote()
    {
        using var connection = Open();
        string restored = (string)Scalar(connection, null, "SELECT DB_NAME() + '_Restored';")!;
        object? state = Scalar(connection, null, "SELECT state_desc FROM sys.databases WHERE name = @name;", new SqlParameter("@name", restored));
        Assert.IsNotNull(state, $"06: there's no database named {restored} yet. Restore your backups as a copy with that name.");
        Assert.AreEqual("ONLINE", state, $"06: {restored} is {state}. Restore the last backup WITH RECOVERY, or run RESTORE DATABASE {restored} WITH RECOVERY;");
        try
        {
            object? notes = Scalar(connection, null, $"SELECT COUNT(*) FROM [{restored}].dbo.BackupDrill WHERE Note = @note;", new SqlParameter("@note", DrillNote));
            Assert.AreEqual(1, Convert.ToInt32(notes, CultureInfo.InvariantCulture), $"06: {restored}'s BackupDrill should hold the note '{DrillNote}'.");
        }
        catch (SqlException e) when (e.Number == 208)
        {
            Assert.Fail($"06: {restored} has no BackupDrill table, so the differential wasn't restored. Restore the full backup WITH NORECOVERY, then the differential WITH RECOVERY.");
        }
    }

    [TestMethod]
    public void Problem07_SalesDashboard_ReadsTheSalesTablesThroughItsRoleAndNothingElse() =>
        InRolledBackTransaction("07_Sales_Dashboard_Role.sql", "DROP USER IF EXISTS SalesDashboard; DROP ROLE IF EXISTS SalesReporters;",
            (connection, transaction, results) =>
            {
                object? role = Scalar(connection, transaction, "SELECT DATABASE_PRINCIPAL_ID('SalesReporters');");
                Assert.IsNotNull(role, "07: create the role SalesReporters.");

                foreach (string table in new[] { "dbo.Orders", "dbo.OrderDetails", "dbo.Products", "dbo.Categories" })
                {
                    Assert.AreEqual(1, Can(connection, transaction, "SalesDashboard", table, "SELECT", "07"), $"07: SalesDashboard should be able to SELECT from {table}.");
                    foreach (string change in new[] { "INSERT", "UPDATE", "DELETE" })
                    {
                        Assert.AreEqual(0, Can(connection, transaction, "SalesDashboard", table, change, "07"), $"07: SalesDashboard should not be able to {change} {table}: it only reads.");
                    }
                }
                foreach (string table in new[] { "dbo.Customers", "dbo.Employees" })
                {
                    Assert.AreEqual(0, Can(connection, transaction, "SalesDashboard", table, "SELECT", "07"), $"07: SalesDashboard should not be able to read {table}: the dashboard doesn't need it.");
                }

                object? member = Scalar(connection, transaction, "SELECT IS_ROLEMEMBER('SalesReporters', 'SalesDashboard');");
                Assert.AreEqual(1, Convert.ToInt32(member, CultureInfo.InvariantCulture), "07: add SalesDashboard to the role with ALTER ROLE ... ADD MEMBER.");
                object? direct = Scalar(connection, transaction, """
                    SELECT COUNT(*)
                    FROM sys.database_permissions p
                    JOIN sys.database_principals u ON u.principal_id = p.grantee_principal_id
                    WHERE u.name = 'SalesDashboard' AND p.class = 1;
                    """);
                Assert.AreEqual(0, Convert.ToInt32(direct, CultureInfo.InvariantCulture), "07: grant the permissions to the role, not to SalesDashboard itself.");
            });

    [TestMethod]
    public void Problem08_TakeAPermissionBack_FromTheShiftLeadAndTheNewHire() =>
        InRolledBackTransaction("08_Take_A_Permission_Back.sql", "",
            (connection, transaction, results) =>
            {
                Assert.AreEqual(0, Can(connection, transaction, "ShiftLead", "dbo.Employees", "SELECT", "08"), "08: ShiftLead can still read Employees.");
                Assert.AreEqual(0, Can(connection, transaction, "NewHire", "dbo.Employees", "SELECT", "08"),
                    "08: NewHire can still read Employees: take it back from everyone ShiftLead gave it to, as well.");
            });

    [TestMethod]
    public void Problem09_Snapshot_IsAllowedOnTheDatabase()
    {
        using var connection = Open();
        object? state = Scalar(connection, null, "SELECT snapshot_isolation_state_desc FROM sys.databases WHERE name = DB_NAME();");
        Assert.AreEqual("ON", state, "09: this database doesn't allow snapshot isolation yet. Run 09_Turn_On_Snapshot.sql in SSMS.");
    }

    [TestMethod]
    public void Problem10_PriceListReport_ShowsTheCommittedPriceWithoutWaiting()
    {
        // A clerk changes Chai's price to 20.00 and doesn't commit, while the report runs.
        using var clerk = Open();
        using var clerkTransaction = clerk.BeginTransaction();
        Execute(clerk, clerkTransaction, "UPDATE Products SET UnitPrice = 20.00 WHERE ProductId = 1;");
        try
        {
            using var report = Open();
            Execute(report, null, "SET LOCK_TIMEOUT 3000;");
            var results = new List<DataTable>();
            foreach (string batch in Batches("10_Price_List_Report.sql")) results.AddRange(Execute(report, null, batch));

            Assert.IsNotEmpty(results, "10: write the report's SELECT under the isolation level.");
            DataRow[] rows = Rows(results[^1]);
            Assert.HasCount(12, rows, "10: category 1 has 12 products.");
            Assert.IsTrue(Has(rows[0], "Chai"), "10: sorted by ProductName, the first is Chai.");
            Assert.IsFalse(Has(rows[0], 20.00m), "10: your report showed Chai at 20.00, a price the clerk never committed (a dirty read).");
            Assert.IsTrue(Has(rows[0], 18.00m), "10: Chai's committed price is 18.00.");
        }
        catch (SqlException e) when (e.Number == 1222)
        {
            Assert.Fail("10: your report waited for the clerk's unfinished change to Chai's price. Choose a level that reads the last committed price without waiting.");
        }
        catch (SqlException e) when (e.Number == 3952)
        {
            Assert.Fail("10: this database doesn't allow snapshot isolation yet: do 09 first.");
        }
        finally
        {
            clerkTransaction.Rollback();
        }
    }
}
