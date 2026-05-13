-- SQL Script to Seed Mock Data for Azure MySQL

-- Use the appropriate database
USE `your_database_name`;

-- Insert mock user data (if not already present)
INSERT INTO `users` (`id`, `email`, `name`, `created_at`, `updated_at`)
VALUES
    (1, 'karanamsaikrishna.kaushik@gmail.com', 'Kaushik Karanam', NOW(), NOW())
ON DUPLICATE KEY UPDATE
    `email` = VALUES(`email`),
    `name` = VALUES(`name`),
    `updated_at` = NOW();

-- Insert mock history stats data
INSERT INTO `history_stats` (`id`, `user_id`, `stat_date`, `stat_key`, `stat_value`, `created_at`, `updated_at`)
VALUES
    (1, 1, '2026-05-01', 'orders_count', 10, NOW(), NOW()),
    (2, 1, '2026-05-02', 'orders_count', 15, NOW(), NOW()),
    (3, 1, '2026-05-03', 'orders_count', 20, NOW(), NOW()),
    (4, 1, '2026-05-01', 'revenue', 100.50, NOW(), NOW()),
    (5, 1, '2026-05-02', 'revenue', 150.75, NOW(), NOW()),
    (6, 1, '2026-05-03', 'revenue', 200.00, NOW(), NOW())
ON DUPLICATE KEY UPDATE
    `stat_value` = VALUES(`stat_value`),
    `updated_at` = NOW();

-- Add more mock data as needed for additional users or stats.