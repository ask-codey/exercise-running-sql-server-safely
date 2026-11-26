-- 03. Two categories with the same name would confuse every menu on the site.
--     Create a UNIQUE nonclustered index named UX_Categories_CategoryName on Categories, on CategoryName.
-- Expect: The index UX_Categories_CategoryName is unique, on CategoryName. An INSERT of a second category named Beverages is refused (Msg 2601).

-- Write your SQL below.
