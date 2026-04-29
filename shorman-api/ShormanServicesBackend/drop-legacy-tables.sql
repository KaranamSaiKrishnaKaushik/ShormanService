-- Historical cleanup script for the pre-table-rename schema.
-- Do not run this after `rename-api-tables.sql` has been applied.

SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.api_users', N'U') IS NULL
   OR OBJECT_ID(N'dbo.api_addresses', N'U') IS NULL
   OR OBJECT_ID(N'dbo.api_categories', N'U') IS NULL
   OR OBJECT_ID(N'dbo.api_supermarkets', N'U') IS NULL
   OR OBJECT_ID(N'dbo.api_products', N'U') IS NULL
   OR OBJECT_ID(N'dbo.api_carts', N'U') IS NULL
   OR OBJECT_ID(N'dbo.api_cart_items', N'U') IS NULL
   OR OBJECT_ID(N'dbo.api_orders', N'U') IS NULL
   OR OBJECT_ID(N'dbo.api_order_items', N'U') IS NULL
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50001, 'Active api_* tables were not all found. Aborting legacy table cleanup.', 1;
END;

DECLARE @legacyTables TABLE
(
    SchemaName sysname NOT NULL,
    TableName sysname NOT NULL
);

INSERT INTO @legacyTables (SchemaName, TableName)
VALUES
    (N'dbo', N'cart_items'),
    (N'dbo', N'order_items'),
    (N'dbo', N'carts'),
    (N'dbo', N'orders'),
    (N'dbo', N'addresses'),
    (N'dbo', N'products'),
    (N'dbo', N'categories'),
    (N'dbo', N'supermarkets'),
    (N'dbo', N'users');

DECLARE @dropForeignKeys nvarchar(max) = N'';

SELECT @dropForeignKeys = STRING_AGG(
    N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(parent_table.schema_id)) + N'.' + QUOTENAME(parent_table.name)
    + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';',
    CHAR(10)
)
FROM sys.foreign_keys fk
INNER JOIN sys.tables parent_table ON parent_table.object_id = fk.parent_object_id
INNER JOIN sys.tables referenced_table ON referenced_table.object_id = fk.referenced_object_id
LEFT JOIN @legacyTables parent_legacy
    ON parent_legacy.SchemaName = SCHEMA_NAME(parent_table.schema_id)
   AND parent_legacy.TableName = parent_table.name
LEFT JOIN @legacyTables referenced_legacy
    ON referenced_legacy.SchemaName = SCHEMA_NAME(referenced_table.schema_id)
   AND referenced_legacy.TableName = referenced_table.name
WHERE parent_legacy.TableName IS NOT NULL
   OR referenced_legacy.TableName IS NOT NULL;

IF @dropForeignKeys IS NOT NULL AND LEN(@dropForeignKeys) > 0
BEGIN
    EXEC sp_executesql @dropForeignKeys;
END;

DROP TABLE IF EXISTS dbo.cart_items;
DROP TABLE IF EXISTS dbo.order_items;
DROP TABLE IF EXISTS dbo.carts;
DROP TABLE IF EXISTS dbo.orders;
DROP TABLE IF EXISTS dbo.addresses;
DROP TABLE IF EXISTS dbo.products;
DROP TABLE IF EXISTS dbo.categories;
DROP TABLE IF EXISTS dbo.supermarkets;
DROP TABLE IF EXISTS dbo.users;

COMMIT TRANSACTION;

-- Optional cleanup for a stale rename artifact.
-- Only run this after you have verified that dbo.api_orders contains the data you want to keep.
-- DROP TABLE IF EXISTS dbo.api_orders_old;