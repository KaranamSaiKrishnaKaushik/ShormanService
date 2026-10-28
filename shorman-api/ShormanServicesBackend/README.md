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

Required settings are split between non-secret app settings and vault-backed secrets.

Expected file set:

- `appsettings.json` - shared defaults for every environment
- `appsettings.Development.json` - development-only defaults such as verbose logging
- `appsettings.Local.json` - optional machine-specific local overrides and secrets, ignored by git
- `appsettings.Local.example.json` - tracked template for local setup

`appsettings.Development.Local.json` is no longer used.

Keep these as App Service settings or local JSON values:

- `KeyVault:VaultUri`
- `Database:Provider`
- `Cors:AllowedOrigins:*`
- `Jwt:Issuer`
- `Jwt:Audience`
- `Auth0:Domain`
- `Auth0:Audience`
- `RoleBootstrap:SuperAdminEmails`
- `Stripe:FrontendBaseUrl`
- `Stripe:CheckoutSuccessPath`
- `Stripe:CheckoutCancelPath`
- `Stripe:Currency`

Move these to Azure Key Vault for production:

- `ConnectionStrings--ApiConnection`
- `Jwt--SigningKey`
- `Stripe--SecretKey`
- `Stripe--WebhookSecret`

Notes:

- Azure Key Vault secret names use `--` to represent `:` in configuration keys.
- `ConnectionStrings--DefaultConnection` is also supported, but `ConnectionStrings--ApiConnection` is the primary key used by this app.
- `Stripe:PublishableKey` is not currently consumed by the backend and should be treated as public client configuration if you expose it to the frontend.
- The Angular frontend should keep only public configuration values. Frontend Auth0 identifiers, API base URLs, and feature flags are not secrets once bundled into browser JavaScript.

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

