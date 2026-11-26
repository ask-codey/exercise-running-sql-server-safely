-- 01. The shipping page lists the orders going to one country, with each order's OrderDate and ShipCity, and it's getting slow.
--     Create a nonclustered index named IX_Orders_ShipCountry on Orders, on ShipCountry, that also INCLUDEs OrderDate and ShipCity,
--     so the search below never has to go back to the table. Then run the search for Brazil, sorted by OrderId.
-- Expect: The index IX_Orders_ShipCountry exists on ShipCountry, with OrderDate and ShipCity included. The search gives 83 rows; the first is 10250, 2016-07-08, Rio de Janeiro.

-- Write your SQL below.
