-- 10. The price list report shows every product in category 1 (Beverages): its ProductName and UnitPrice, sorted by ProductName.
--     It must never wait for a clerk's unfinished change, and it must never show a price that hasn't been committed.
--     Set the isolation level that does both, then write the report's SELECT. (Do 09 first.)
-- Expect: 12 rows; the first is Chai at 18.00, even while another window has changed Chai's price to 20.00 and not committed it.

-- Write your SQL below.
