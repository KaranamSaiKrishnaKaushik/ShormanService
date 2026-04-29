# ShormanServicesBackend

This backend now runs only the active `Api` architecture.

## Active Backend Surface

- `Api/` - EF Core context, entities, MediatR features, JWT auth, and startup initialization
- `Controllers/` - HTTP endpoints for auth, addresses, products, supermarkets, cart, orders, and delivery
- `Program.cs` - application bootstrap

## Database

The application creates and maintains only the active runtime tables through `Api` startup initialization.

Current runtime tables:

- `users`
- `addresses`
- `categories`
- `supermarkets`
- `products`
- `carts`
- `cart_items`
- `orders`
- `order_items`

Existing SQL Server databases can be renamed in place with `rename-api-tables.sql` before deployment.

## Configuration

Required settings are in `appsettings.json` / `appsettings.Development.json`:

- `Database:Provider`: `SqlServer` or `MySql`
- `ConnectionStrings:ApiConnection`
- `Jwt:*`

## Build

```powershell
dotnet build "C:\Users\KaranamK\OneDrive - Lucht Probst Associates GmbH\Desktop\Data\Personal Apps\ShormanApp\ShormanService\shorman-api\ShormanServicesBackend\ShormanServicesBackend.csproj"
```

## Active API Endpoints

- `POST /api/auth/login`
- `POST /api/auth/register`
- `GET /api/products`
- `GET /api/products/{id}`
- `GET /api/categories`
- `GET /api/supermarkets`
- `GET /api/addresses`
- `POST /api/addresses`
- `PUT /api/addresses/{id}`
- `DELETE /api/addresses/{id}`
- `POST /api/addresses/{id}/default`
- `GET /api/cart`
- `POST /api/cart/items`
- `PUT /api/cart/items/{itemId}`
- `DELETE /api/cart/items/{itemId}`
- `DELETE /api/cart`
- `GET /api/orders`
- `GET /api/orders/{id}`
- `POST /api/orders`
- `GET /api/delivery/check`
- `GET /health`

