-- History Stats seed for Azure MySQL (Shorman)
-- Idempotent script for:
-- 1) karanamsaikrishna.kaushik@gmail.com
-- 2) kaushik.kea@gmail.com
--
-- Safe-update friendly: UPDATE/DELETE statements use key-based predicates.

START TRANSACTION;

DROP TEMPORARY TABLE IF EXISTS tmp_seed_users;
CREATE TEMPORARY TABLE tmp_seed_users (
    Email VARCHAR(256) NOT NULL,
    FirstName VARCHAR(100) NOT NULL,
    LastName VARCHAR(100) NOT NULL,
    Phone VARCHAR(50) NULL,
    PRIMARY KEY (Email)
);

INSERT INTO tmp_seed_users (Email, FirstName, LastName, Phone)
VALUES
    ('karanamsaikrishna.kaushik@gmail.com', 'Karanam', 'Kaushik', '+49 111 222333'),
    ('kaushik.kea@gmail.com', 'Kaushik', 'Kea', '+49 111 444555');

-- Ensure users exist
INSERT INTO users (
    Email, PasswordHash, FirstName, LastName, Phone,
    IsEmailVerified, IsDeleted, CreatedAtUtc
)
SELECT
    s.Email,
    '$2a$11$4h4f0f5vA2sLw0b7zKX9geCduU7slZx2L4wEN0NQ9uP6yQu8y9YqS',
    s.FirstName,
    s.LastName,
    s.Phone,
    1,
    0,
    UTC_TIMESTAMP()
FROM tmp_seed_users s
LEFT JOIN users u ON LOWER(u.Email) = LOWER(s.Email)
WHERE u.Id IS NULL;

DROP TEMPORARY TABLE IF EXISTS tmp_seed_user_ids;
CREATE TEMPORARY TABLE tmp_seed_user_ids (
    UserId INT NOT NULL,
    PRIMARY KEY (UserId)
);

INSERT INTO tmp_seed_user_ids (UserId)
SELECT u.Id
FROM users u
JOIN tmp_seed_users s ON LOWER(u.Email) = LOWER(s.Email);

-- Normalize user profile fields
UPDATE users u
JOIN tmp_seed_users s ON LOWER(u.Email) = LOWER(s.Email)
JOIN tmp_seed_user_ids sui ON sui.UserId = u.Id
SET
    u.FirstName = s.FirstName,
    u.LastName = s.LastName,
    u.Phone = s.Phone,
    u.IsEmailVerified = 1,
    u.IsDeleted = 0,
    u.EmailVerificationCode = NULL,
    u.EmailVerificationExpiresAtUtc = NULL
WHERE u.Id = sui.UserId;

-- Ensure each target user has at least one default address
INSERT INTO addresses (
    UserId, Label, Street, HouseNumber, PostalCode, City, Country, IsDefault
)
SELECT
    u.Id,
    'Home',
    'Musterstrasse',
    '24',
    '10115',
    'Berlin',
    'Germany',
    1
FROM users u
JOIN tmp_seed_user_ids sui ON sui.UserId = u.Id
LEFT JOIN addresses a ON a.UserId = u.Id
WHERE a.Id IS NULL;

-- Ensure categories exist
INSERT INTO categories (Name, Slug, Icon)
SELECT x.Name, x.Slug, x.Icon
FROM (
    SELECT 'Beverages' AS Name, 'beverages' AS Slug, '?' AS Icon UNION ALL
    SELECT 'Bakery', 'bakery', '?' UNION ALL
    SELECT 'Eggs & Dairy', 'eggs-dairy', '?' UNION ALL
    SELECT 'Fruits & Vegetables', 'fruits-vegetables', '?' UNION ALL
    SELECT 'Snacks', 'snacks', '?' UNION ALL
    SELECT 'Meat', 'meat', '?' UNION ALL
    SELECT 'Skin Care', 'skin-care', '?' UNION ALL
    SELECT 'Hair Care', 'hair-care', '?' UNION ALL
    SELECT 'Body & Bath', 'body-bath', '?' UNION ALL
    SELECT 'Health & Wellness', 'health-wellness', '?'
) x
LEFT JOIN categories c ON LOWER(c.Slug) = LOWER(x.Slug)
WHERE c.Id IS NULL;

-- Ensure supermarkets exist
INSERT INTO supermarkets (Name, Slug, Color)
SELECT x.Name, x.Slug, x.Color
FROM (
    SELECT 'LIDL' AS Name, 'lidl' AS Slug, '#0050AA' AS Color UNION ALL
    SELECT 'ALDI', 'aldi', '#00519C' UNION ALL
    SELECT 'REWE', 'rewe', '#CC0000' UNION ALL
    SELECT 'PENNY', 'penny', '#CC0000' UNION ALL
    SELECT 'EDEKA', 'edeka', '#003A70' UNION ALL
    SELECT 'dm', 'dm', '#003E91' UNION ALL
    SELECT 'ROSSMANN', 'rossmann', '#C3002F'
) x
LEFT JOIN supermarkets sm ON LOWER(sm.Slug) = LOWER(x.Slug)
WHERE sm.Id IS NULL;

-- Ensure seed products exist (linked to category/store)
INSERT INTO products (
    ProductKey, Name, Description, Price, ImageUrl,
    CategoryId, SupermarketId, Unit, Stock, IsAvailable, DataSource, UpdatedAtUtc
)
SELECT
    p.ProductKey,
    p.Name,
    'Insights seed product',
    p.Price,
    NULL,
    c.Id,
    sm.Id,
    'pcs',
    100,
    1,
    'seed',
    UTC_TIMESTAMP()
FROM (
    SELECT 'insights-bev-lidl' AS ProductKey, 'Mineral Water 1.5L' AS Name, 'lidl' AS StoreSlug, 'beverages' AS CategorySlug, 1.45 AS Price UNION ALL
    SELECT 'insights-dairy-aldi', 'Frische Vollmilch 1L', 'aldi', 'eggs-dairy', 1.29 UNION ALL
    SELECT 'insights-fruit-rewe', 'Banane lose 1kg', 'rewe', 'fruits-vegetables', 2.19 UNION ALL
    SELECT 'insights-snack-penny', 'Dark Chocolate 100g', 'penny', 'snacks', 1.59 UNION ALL
    SELECT 'insights-meat-edeka', 'Haehnchenbrust 500g', 'edeka', 'meat', 5.49 UNION ALL
    SELECT 'insights-skin-dm', 'Body Lotion 500ml', 'dm', 'skin-care', 4.79 UNION ALL
    SELECT 'insights-hair-rossmann', 'Shampoo Volume', 'rossmann', 'hair-care', 3.99
) p
JOIN categories c ON LOWER(c.Slug) = LOWER(p.CategorySlug)
JOIN supermarkets sm ON LOWER(sm.Slug) = LOWER(p.StoreSlug)
LEFT JOIN products ex ON LOWER(COALESCE(ex.ProductKey, '')) = LOWER(p.ProductKey)
WHERE ex.Id IS NULL;

-- Keep seed products aligned (safe update key predicate)
UPDATE products pr
JOIN (
    SELECT 'insights-bev-lidl' AS ProductKey, 'lidl' AS StoreSlug, 'beverages' AS CategorySlug, 1.45 AS Price UNION ALL
    SELECT 'insights-dairy-aldi', 'aldi', 'eggs-dairy', 1.29 UNION ALL
    SELECT 'insights-fruit-rewe', 'rewe', 'fruits-vegetables', 2.19 UNION ALL
    SELECT 'insights-snack-penny', 'penny', 'snacks', 1.59 UNION ALL
    SELECT 'insights-meat-edeka', 'edeka', 'meat', 5.49 UNION ALL
    SELECT 'insights-skin-dm', 'dm', 'skin-care', 4.79 UNION ALL
    SELECT 'insights-hair-rossmann', 'rossmann', 'hair-care', 3.99
) map ON LOWER(COALESCE(pr.ProductKey, '')) = LOWER(map.ProductKey)
JOIN categories c ON LOWER(c.Slug) = LOWER(map.CategorySlug)
JOIN supermarkets sm ON LOWER(sm.Slug) = LOWER(map.StoreSlug)
SET
    pr.CategoryId = c.Id,
    pr.SupermarketId = sm.Id,
    pr.Price = map.Price,
    pr.IsAvailable = 1,
    pr.DataSource = 'seed',
    pr.UpdatedAtUtc = UTC_TIMESTAMP()
WHERE pr.Id > 0;

-- Remove previous seeded insights data for target users
DELETE oi
FROM order_items oi
JOIN orders o ON o.Id = oi.OrderId
JOIN tmp_seed_user_ids sui ON sui.UserId = o.UserId
WHERE o.PaymentMethod = 'SEEDED_INSIGHTS'
  AND oi.Id > 0
  AND o.Id > 0;

DELETE o
FROM orders o
JOIN tmp_seed_user_ids sui ON sui.UserId = o.UserId
WHERE o.PaymentMethod = 'SEEDED_INSIGHTS'
  AND o.Id > 0;

DROP TEMPORARY TABLE IF EXISTS tmp_monthly_spend;
CREATE TEMPORARY TABLE tmp_monthly_spend (
    MonthIndex INT NOT NULL,
    StoreSlug VARCHAR(100) NOT NULL,
    Spend DECIMAL(10,2) NOT NULL,
    PRIMARY KEY (MonthIndex, StoreSlug)
);

INSERT INTO tmp_monthly_spend (MonthIndex, StoreSlug, Spend)
VALUES
    (0,'lidl',86),(1,'lidl',92),(2,'lidl',97),(3,'lidl',101),(4,'lidl',104),(5,'lidl',112),(6,'lidl',116),(7,'lidl',109),(8,'lidl',113),(9,'lidl',121),(10,'lidl',126),(11,'lidl',132),
    (0,'aldi',54),(1,'aldi',58),(2,'aldi',56),(3,'aldi',61),(4,'aldi',59),(5,'aldi',64),(6,'aldi',63),(7,'aldi',66),(8,'aldi',65),(9,'aldi',67),(10,'aldi',69),(11,'aldi',72),
    (0,'rewe',42),(1,'rewe',45),(2,'rewe',48),(3,'rewe',47),(4,'rewe',50),(5,'rewe',53),(6,'rewe',55),(7,'rewe',54),(8,'rewe',52),(9,'rewe',56),(10,'rewe',57),(11,'rewe',60),
    (0,'penny',18),(1,'penny',19),(2,'penny',20),(3,'penny',19),(4,'penny',21),(5,'penny',23),(6,'penny',22),(7,'penny',24),(8,'penny',25),(9,'penny',24),(10,'penny',26),(11,'penny',28),
    (0,'edeka',15),(1,'edeka',14),(2,'edeka',16),(3,'edeka',15),(4,'edeka',16),(5,'edeka',18),(6,'edeka',17),(7,'edeka',18),(8,'edeka',19),(9,'edeka',20),(10,'edeka',19),(11,'edeka',20),
    (0,'dm',32),(1,'dm',34),(2,'dm',36),(3,'dm',35),(4,'dm',38),(5,'dm',39),(6,'dm',41),(7,'dm',42),(8,'dm',43),(9,'dm',45),(10,'dm',44),(11,'dm',47),
    (0,'rossmann',19),(1,'rossmann',20),(2,'rossmann',21),(3,'rossmann',22),(4,'rossmann',21),(5,'rossmann',23),(6,'rossmann',24),(7,'rossmann',23),(8,'rossmann',24),(9,'rossmann',25),(10,'rossmann',26),(11,'rossmann',27);

DROP TEMPORARY TABLE IF EXISTS tmp_store_product;
CREATE TEMPORARY TABLE tmp_store_product (
    StoreSlug VARCHAR(100) NOT NULL PRIMARY KEY,
    ProductKey VARCHAR(64) NOT NULL
);

INSERT INTO tmp_store_product (StoreSlug, ProductKey)
VALUES
    ('lidl','insights-bev-lidl'),
    ('aldi','insights-dairy-aldi'),
    ('rewe','insights-fruit-rewe'),
    ('penny','insights-snack-penny'),
    ('edeka','insights-meat-edeka'),
    ('dm','insights-skin-dm'),
    ('rossmann','insights-hair-rossmann');

SET @seed_first_month = DATE_ADD(DATE_FORMAT(UTC_TIMESTAMP(), '%Y-%m-01'), INTERVAL -11 MONTH);

-- Insert 2 completed orders per month/store/user (168 orders total)
INSERT INTO orders (
    UserId, CustomerNameSnapshot, CustomerEmailSnapshot,
    Status, PaymentMethod, PaymentStatus,
    AddressId, Subtotal, DeliveryFee, Total,
    CreatedAtUtc, UpdatedAtUtc, CompletedAtUtc
)
SELECT
    u.Id,
    TRIM(CONCAT(COALESCE(u.FirstName, ''), ' ', COALESCE(u.LastName, ''))),
    u.Email,
    'COMPLETED',
    'SEEDED_INSIGHTS',
    'PAID',
    a.Id,
    ROUND(ms.Spend / 2, 2),
    0,
    ROUND(ms.Spend / 2, 2),
    DATE_ADD(
        DATE_ADD(
            DATE_ADD(DATE_ADD(@seed_first_month, INTERVAL ms.MonthIndex MONTH), INTERVAL CASE WHEN seq.SeqNo = 1 THEN 8 ELSE 20 END DAY),
            INTERVAL (10 + MOD(ms.MonthIndex + seq.SeqNo, 5)) HOUR
        ),
        INTERVAL CASE ms.StoreSlug
            WHEN 'lidl' THEN 1
            WHEN 'aldi' THEN 2
            WHEN 'rewe' THEN 3
            WHEN 'penny' THEN 4
            WHEN 'edeka' THEN 5
            WHEN 'dm' THEN 6
            ELSE 7
        END MINUTE
    ),
    DATE_ADD(
        DATE_ADD(
            DATE_ADD(DATE_ADD(@seed_first_month, INTERVAL ms.MonthIndex MONTH), INTERVAL CASE WHEN seq.SeqNo = 1 THEN 8 ELSE 20 END DAY),
            INTERVAL (10 + MOD(ms.MonthIndex + seq.SeqNo, 5)) HOUR
        ),
        INTERVAL CASE ms.StoreSlug
            WHEN 'lidl' THEN 1
            WHEN 'aldi' THEN 2
            WHEN 'rewe' THEN 3
            WHEN 'penny' THEN 4
            WHEN 'edeka' THEN 5
            WHEN 'dm' THEN 6
            ELSE 7
        END MINUTE
    ),
    DATE_ADD(
        DATE_ADD(
            DATE_ADD(
                DATE_ADD(DATE_ADD(@seed_first_month, INTERVAL ms.MonthIndex MONTH), INTERVAL CASE WHEN seq.SeqNo = 1 THEN 8 ELSE 20 END DAY),
                INTERVAL (10 + MOD(ms.MonthIndex + seq.SeqNo, 5)) HOUR
            ),
            INTERVAL CASE ms.StoreSlug
                WHEN 'lidl' THEN 1
                WHEN 'aldi' THEN 2
                WHEN 'rewe' THEN 3
                WHEN 'penny' THEN 4
                WHEN 'edeka' THEN 5
                WHEN 'dm' THEN 6
                ELSE 7
            END MINUTE
        ),
        INTERVAL 2 HOUR
    )
FROM tmp_seed_user_ids sui
JOIN users u ON u.Id = sui.UserId
JOIN addresses a ON a.Id = (
    SELECT a2.Id
    FROM addresses a2
    WHERE a2.UserId = u.Id
    ORDER BY a2.IsDefault DESC, a2.Id DESC
    LIMIT 1
)
JOIN tmp_monthly_spend ms
JOIN (
    SELECT 1 AS SeqNo
    UNION ALL
    SELECT 2 AS SeqNo
) seq;

-- One item per seeded order; store chosen from minute marker in CreatedAtUtc
INSERT INTO order_items (
    OrderId, ProductId, ProductName, ProductImageUrl, SupermarketName,
    Quantity, UnitPrice, TotalPrice
)
SELECT
    o.Id,
    p.Id,
    p.Name,
    p.ImageUrl,
    sm.Name,
    GREATEST(1, ROUND(o.Total / NULLIF(p.Price, 0), 0)),
    p.Price,
    ROUND(GREATEST(1, ROUND(o.Total / NULLIF(p.Price, 0), 0)) * p.Price, 2)
FROM orders o
JOIN tmp_seed_user_ids sui ON sui.UserId = o.UserId
JOIN supermarkets sm ON LOWER(sm.Name) = LOWER(
    CASE
        WHEN MINUTE(o.CreatedAtUtc) = 1 THEN 'LIDL'
        WHEN MINUTE(o.CreatedAtUtc) = 2 THEN 'ALDI'
        WHEN MINUTE(o.CreatedAtUtc) = 3 THEN 'REWE'
        WHEN MINUTE(o.CreatedAtUtc) = 4 THEN 'PENNY'
        WHEN MINUTE(o.CreatedAtUtc) = 5 THEN 'EDEKA'
        WHEN MINUTE(o.CreatedAtUtc) = 6 THEN 'dm'
        ELSE 'ROSSMANN'
    END
)
JOIN tmp_store_product sp ON LOWER(sp.StoreSlug) = LOWER(sm.Slug)
JOIN products p ON LOWER(COALESCE(p.ProductKey, '')) = LOWER(sp.ProductKey)
WHERE o.PaymentMethod = 'SEEDED_INSIGHTS'
  AND o.Status = 'COMPLETED';

-- Sync order totals to order_items totals (safe update key predicate)
UPDATE orders o
JOIN (
    SELECT oi.OrderId, ROUND(SUM(oi.TotalPrice), 2) AS ItemTotal
    FROM order_items oi
    GROUP BY oi.OrderId
) s ON s.OrderId = o.Id
JOIN tmp_seed_user_ids sui ON sui.UserId = o.UserId
SET
    o.Subtotal = s.ItemTotal,
    o.Total = s.ItemTotal,
    o.DeliveryFee = 0,
    o.UpdatedAtUtc = UTC_TIMESTAMP()
WHERE o.Id = s.OrderId
  AND o.Id > 0;

-- Sanity checks
SELECT u.Email, COUNT(*) AS SeededOrders, ROUND(SUM(o.Total), 2) AS SeededTotal
FROM orders o
JOIN users u ON u.Id = o.UserId
JOIN tmp_seed_user_ids sui ON sui.UserId = u.Id
WHERE o.PaymentMethod = 'SEEDED_INSIGHTS'
GROUP BY u.Email;

SELECT u.Email, c.Name AS CategoryName, ROUND(SUM(oi.TotalPrice), 2) AS Spend
FROM order_items oi
JOIN orders o ON o.Id = oi.OrderId
JOIN users u ON u.Id = o.UserId
JOIN products p ON p.Id = oi.ProductId
JOIN categories c ON c.Id = p.CategoryId
JOIN tmp_seed_user_ids sui ON sui.UserId = u.Id
WHERE o.PaymentMethod = 'SEEDED_INSIGHTS'
GROUP BY u.Email, c.Name
ORDER BY u.Email, Spend DESC;

COMMIT;
