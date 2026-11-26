-- 07. A sales dashboard needs to read the orders, their lines, and the products and categories they name. Nothing else, and it never changes anything.
--     Create a role named SalesReporters, and GRANT it SELECT on Orders, OrderDetails, Products and Categories.
--     Then create a user named SalesDashboard WITHOUT LOGIN, and add it to the role. Grant nothing to the user itself: it gets everything through the role.
-- Expect: As SalesDashboard, SELECT works on Orders, OrderDetails, Products and Categories, and nothing else does: no SELECT on Customers or Employees, no INSERT, UPDATE or DELETE.

-- Write your SQL below.
