-- 02. Each salesperson's page lists their own orders, newest first.
--     Create a nonclustered index named IX_Orders_EmployeeId_OrderDate on Orders, on two columns: EmployeeId first, then OrderDate, newest first (DESC).
--     Then list the three newest orders of employee 5 (OrderId and OrderDate), newest first.
-- Expect: The index IX_Orders_EmployeeId_OrderDate exists on EmployeeId, then OrderDate DESC. The search gives 3 rows: 11043 (2018-04-22), 10954 (2018-03-17), 10922 (2018-03-03).

-- Write your SQL below.
