SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @tableRenames TABLE
(
    OldName sysname NOT NULL,
    NewName sysname NOT NULL
);

INSERT INTO @tableRenames (OldName, NewName)
VALUES
    (N'api_users', N'users'),
    (N'api_addresses', N'addresses'),
    (N'api_categories', N'categories'),
    (N'api_supermarkets', N'supermarkets'),
    (N'api_products', N'products'),
    (N'api_carts', N'carts'),
    (N'api_cart_items', N'cart_items'),
    (N'api_orders', N'orders'),
    (N'api_order_items', N'order_items');

IF EXISTS
(
    SELECT 1
    FROM @tableRenames tr
    WHERE OBJECT_ID(N'dbo.' + tr.OldName, N'U') IS NOT NULL
      AND OBJECT_ID(N'dbo.' + tr.NewName, N'U') IS NOT NULL
)
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50020, 'Cannot rename api_* tables because both the old and new table names already exist.', 1;
END;

DECLARE @renameSql nvarchar(max) = N'';

SELECT @renameSql = STRING_AGG(
    N'EXEC sp_rename ''dbo.' + tr.OldName + ''', ''' + tr.NewName + N''';',
    CHAR(10)
)
FROM @tableRenames tr
WHERE OBJECT_ID(N'dbo.' + tr.OldName, N'U') IS NOT NULL
  AND OBJECT_ID(N'dbo.' + tr.NewName, N'U') IS NULL;

IF @renameSql IS NOT NULL AND LEN(@renameSql) > 0
BEGIN
    EXEC sp_executesql @renameSql;
END;

COMMIT TRANSACTION;

-- Notes:
-- 1. This script renames tables only. Existing foreign keys and indexes continue to work.
-- 2. Constraint names may still retain the old api_ prefix. That is cosmetic and does not affect runtime behavior.
-- 3. If dbo.api_orders_old still exists, review it manually before dropping or renaming it.