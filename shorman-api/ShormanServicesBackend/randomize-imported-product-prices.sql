SET NOCOUNT ON;

BEGIN TRANSACTION;

UPDATE p
SET Price = CAST((100 + ABS(CHECKSUM(NEWID())) % 901) / 100.0 AS DECIMAL(10,2))
FROM products AS p
INNER JOIN off_product_links AS l ON l.ProductId = p.Id;

UPDATE s
SET Price = CAST((100 + ABS(CHECKSUM(NEWID())) % 901) / 100.0 AS DECIMAL(10,2))
FROM off_store_products AS s;

COMMIT TRANSACTION;

SELECT
    COUNT(*) AS ProductCount,
    MIN(Price) AS MinPrice,
    MAX(Price) AS MaxPrice
FROM products
WHERE Id IN (SELECT ProductId FROM off_product_links);