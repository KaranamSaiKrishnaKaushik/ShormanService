using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShormanService.Domain.Entities;

namespace ShormanService.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext context, ILogger logger)
    {
        await context.Database.EnsureCreatedAsync();

        if (!await context.Supermarkets.AnyAsync())
        {
            var supermarkets = new List<Supermarket>
            {
                new() { Id = 1, Name = "REWE", LogoUrl = "/images/supermarkets/rewe.png" },
                new() { Id = 2, Name = "ALDI", LogoUrl = "/images/supermarkets/aldi.png" },
                new() { Id = 3, Name = "PENNY", LogoUrl = "/images/supermarkets/penny.png" },
                new() { Id = 4, Name = "LIDL", LogoUrl = "/images/supermarkets/lidl.png" }
            };
            await context.Supermarkets.AddRangeAsync(supermarkets);
        }

        if (!await context.Categories.AnyAsync())
        {
            var categories = new List<Category>
            {
                new() { Id = 1, Name = "Eggs & Dairy", IconUrl = "/images/categories/dairy.png" },
                new() { Id = 2, Name = "Meat", IconUrl = "/images/categories/meat.png" },
                new() { Id = 3, Name = "Fruits & Vegetables", IconUrl = "/images/categories/fruits.png" },
                new() { Id = 4, Name = "Bakery", IconUrl = "/images/categories/bakery.png" },
                new() { Id = 5, Name = "Beverages", IconUrl = "/images/categories/beverages.png" },
                new() { Id = 6, Name = "Snacks", IconUrl = "/images/categories/snacks.png" }
            };
            await context.Categories.AddRangeAsync(categories);
        }

        await context.SaveChangesAsync();

        if (!await context.Products.AnyAsync())
        {
            var products = new List<Product>
            {
                new() { Name = "Whole Milk 1L", Description = "Fresh whole milk", Price = 1.29m, ImageUrl = "/images/products/milk.jpg", StockQuantity = 100, SupermarketId = 1, CategoryId = 1, IsAvailable = true },
                new() { Name = "Free Range Eggs (10)", Description = "Large free range eggs", Price = 2.49m, ImageUrl = "/images/products/eggs.jpg", StockQuantity = 80, SupermarketId = 1, CategoryId = 1, IsAvailable = true },
                new() { Name = "Greek Yogurt 500g", Description = "Creamy Greek yogurt", Price = 1.99m, ImageUrl = "/images/products/yogurt.jpg", StockQuantity = 60, SupermarketId = 2, CategoryId = 1, IsAvailable = true },
                new() { Name = "Chicken Breast 500g", Description = "Fresh chicken breast fillets", Price = 4.99m, ImageUrl = "/images/products/chicken.jpg", StockQuantity = 50, SupermarketId = 1, CategoryId = 2, IsAvailable = true },
                new() { Name = "Ground Beef 500g", Description = "Fresh minced beef 20% fat", Price = 3.79m, ImageUrl = "/images/products/beef.jpg", StockQuantity = 40, SupermarketId = 3, CategoryId = 2, IsAvailable = true },
                new() { Name = "Pork Schnitzel 400g", Description = "Ready to cook pork schnitzel", Price = 4.49m, ImageUrl = "/images/products/pork.jpg", StockQuantity = 35, SupermarketId = 4, CategoryId = 2, IsAvailable = true },
                new() { Name = "Bananas 1kg", Description = "Ripe Cavendish bananas", Price = 1.19m, ImageUrl = "/images/products/bananas.jpg", StockQuantity = 120, SupermarketId = 2, CategoryId = 3, IsAvailable = true },
                new() { Name = "Tomatoes 500g", Description = "Vine tomatoes", Price = 1.49m, ImageUrl = "/images/products/tomatoes.jpg", StockQuantity = 90, SupermarketId = 1, CategoryId = 3, IsAvailable = true },
                new() { Name = "Mixed Salad 150g", Description = "Pre-washed mixed leaf salad", Price = 1.29m, ImageUrl = "/images/products/salad.jpg", StockQuantity = 70, SupermarketId = 3, CategoryId = 3, IsAvailable = true },
                new() { Name = "Apples Gala 1kg", Description = "Sweet Gala apples", Price = 1.89m, ImageUrl = "/images/products/apples.jpg", StockQuantity = 100, SupermarketId = 4, CategoryId = 3, IsAvailable = true },
                new() { Name = "Sourdough Bread 750g", Description = "Traditional sourdough loaf", Price = 2.29m, ImageUrl = "/images/products/sourdough.jpg", StockQuantity = 45, SupermarketId = 1, CategoryId = 4, IsAvailable = true },
                new() { Name = "Croissants 4-pack", Description = "Buttery all-butter croissants", Price = 1.99m, ImageUrl = "/images/products/croissants.jpg", StockQuantity = 55, SupermarketId = 2, CategoryId = 4, IsAvailable = true },
                new() { Name = "Pretzels 250g", Description = "Traditional German laugen pretzels", Price = 1.59m, ImageUrl = "/images/products/pretzels.jpg", StockQuantity = 65, SupermarketId = 3, CategoryId = 4, IsAvailable = true },
                new() { Name = "Sparkling Water 1.5L", Description = "Natural mineral water with bubbles", Price = 0.59m, ImageUrl = "/images/products/water.jpg", StockQuantity = 200, SupermarketId = 4, CategoryId = 5, IsAvailable = true },
                new() { Name = "Orange Juice 1L", Description = "Freshly squeezed orange juice", Price = 2.49m, ImageUrl = "/images/products/oj.jpg", StockQuantity = 80, SupermarketId = 1, CategoryId = 5, IsAvailable = true },
                new() { Name = "Cola 1.5L", Description = "Classic cola beverage", Price = 1.79m, ImageUrl = "/images/products/cola.jpg", StockQuantity = 150, SupermarketId = 2, CategoryId = 5, IsAvailable = true },
                new() { Name = "Potato Chips 150g", Description = "Salted crunchy potato chips", Price = 1.49m, ImageUrl = "/images/products/chips.jpg", StockQuantity = 120, SupermarketId = 3, CategoryId = 6, IsAvailable = true },
                new() { Name = "Chocolate Bar 100g", Description = "Milk chocolate bar", Price = 0.99m, ImageUrl = "/images/products/chocolate.jpg", StockQuantity = 180, SupermarketId = 4, CategoryId = 6, IsAvailable = true },
                new() { Name = "Gummy Bears 200g", Description = "Assorted fruit gummy bears", Price = 1.29m, ImageUrl = "/images/products/gummies.jpg", StockQuantity = 160, SupermarketId = 1, CategoryId = 6, IsAvailable = true },
                new() { Name = "Mixed Nuts 200g", Description = "Roasted salted mixed nuts", Price = 2.99m, ImageUrl = "/images/products/nuts.jpg", StockQuantity = 75, SupermarketId = 2, CategoryId = 6, IsAvailable = true }
            };
            await context.Products.AddRangeAsync(products);
            await context.SaveChangesAsync();
            logger.LogInformation("Database seeded with {Count} products.", products.Count);
        }
    }
}
