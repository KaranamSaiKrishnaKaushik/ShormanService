# Shorman-Service 🛒

A handover-ready grocery ordering web application that allows users to browse, compare, and order grocery products from multiple supermarkets (REWE, ALDI, PENNY, LIDL).

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | Angular 18 (standalone components, TypeScript) |
| Backend | ASP.NET Core 8 Web API (C#) |
| Database | MySQL (Azure SQL compatible via EF Core) |
| ORM | Entity Framework Core 8 (Pomelo MySQL provider) |
| Auth | JWT Bearer + local accounts |
| Logging | Serilog (structured, cloud-friendly) |
| Containerization | Docker + Docker Compose |
| CI/CD | GitHub Actions |

## Architecture

### Backend (Clean Architecture)
```
backend/
├── ShormanService.Domain/          # Entities, repository interfaces
├── ShormanService.Application/     # Services, DTOs, AutoMapper profiles
├── ShormanService.Infrastructure/  # EF Core, repositories, JWT, data seeder
├── ShormanService.API/             # Controllers, middleware, Program.cs
├── Dockerfile
└── docker-compose.yml
```

### Frontend (Angular Standalone)
```
shorman-web/src/app/
├── core/
│   ├── models/                     # TypeScript interfaces
│   ├── services/                   # HTTP API services
│   ├── guards/                     # Auth guards
│   └── interceptors/               # JWT interceptor
├── features/
│   ├── auth/                       # Login, Register
│   ├── products/                   # Main shopping page
│   ├── cart/                       # Cart sidebar
│   ├── checkout/                   # Checkout flow
│   ├── orders/                     # Order history
│   └── addresses/                  # Address management
└── shared/
    └── components/                 # Navbar, Footer
```

## Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/) and npm
- [MySQL 8+](https://dev.mysql.com/) or Docker

### Quick Start with Docker Compose

```bash
# Copy safe local env template and fill in secrets
cp .env.example .env

# Start frontend + backend containers
docker compose up -d --build

# App will be available on http://localhost:4200
```

Visit `http://localhost:4200`

### Manual Setup

#### Backend

1. Configure database:
```bash
cp shorman-api/ShormanServicesBackend/appsettings.Local.example.json shorman-api/ShormanServicesBackend/appsettings.Local.json
cp shorman-api/ShormanServicesBackend/appsettings.Development.Local.example.json shorman-api/ShormanServicesBackend/appsettings.Development.Local.json
# Edit the local file you use for your startup flow with your database connection and JWT signing key
```

2. Apply migrations:
```bash
cd shorman-api
dotnet build ShormanServicesBackend.sln
```

3. Run the API:
```bash
cd shorman-api/ShormanServicesBackend
dotnet run
# API runs at http://localhost:5000
# Swagger UI at http://localhost:5000/swagger
# Health check at http://localhost:5000/health
```

#### Frontend

```bash
cd shorman-web
npm install
npm start
# App runs at http://localhost:4200
```

## Features

### For All Users (No Login Required)
- Browse products from REWE, ALDI, PENNY, LIDL
- Filter by category (Eggs & Dairy, Meat, Fruits & Vegetables, Bakery, Beverages, Snacks)
- Search products by name
- View product details

### For Authenticated Users
- Add products to cart with quantity controls
- Manage multiple delivery addresses
- Choose default address for checkout
- Select payment method (PayPal, Bank Transfer, Cash on Delivery)
- Place orders and view order history
- Delivery eligibility check by postal code

## API Endpoints

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/auth/register` | ❌ | Register new user |
| POST | `/api/auth/login` | ❌ | Login & get JWT |
| GET | `/api/products` | ❌ | List products (filter: supermarketId, categoryId, search) |
| GET | `/api/products/{id}` | ❌ | Get product details |
| GET | `/api/categories` | ❌ | List categories |
| GET | `/api/supermarkets` | ❌ | List supermarkets |
| GET | `/api/cart` | ✅ | Get user cart |
| POST | `/api/cart/items` | ✅ | Add item to cart |
| PUT | `/api/cart/items/{id}` | ✅ | Update cart item quantity |
| DELETE | `/api/cart/items/{id}` | ✅ | Remove cart item |
| DELETE | `/api/cart` | ✅ | Clear cart |
| GET | `/api/orders` | ✅ | List user orders |
| POST | `/api/orders` | ✅ | Create order |
| GET | `/api/orders/{id}` | ✅ | Get order details |
| GET | `/api/addresses` | ✅ | List user addresses |
| POST | `/api/addresses` | ✅ | Add address |
| PUT | `/api/addresses/{id}` | ✅ | Update address |
| DELETE | `/api/addresses/{id}` | ✅ | Delete address |
| PUT | `/api/addresses/{id}/set-default` | ✅ | Set default address |
| POST | `/api/delivery/check` | ❌ | Check delivery eligibility by postal code |
| GET | `/health` | ❌ | Health check |

## Environment Configuration

### Backend
- `shorman-api/ShormanServicesBackend/appsettings.json` - base configuration
- `shorman-api/ShormanServicesBackend/appsettings.Development.json` - dev overrides
- `shorman-api/ShormanServicesBackend/appsettings.Local.json` - local secrets override for any environment (gitignored)
- `shorman-api/ShormanServicesBackend/appsettings.Development.Local.json` - local secrets override for development only (gitignored)
- Environment variables override appsettings in production

**Critical env vars for production:**
```
API_CONNECTION_STRING=<your-connection-string>
JWT_SIGNING_KEY=<min-32-char-secret>
```

### Frontend
- `shorman-web/src/environments/environment.ts` - development
- `shorman-web/src/environments/environment.production.ts` - production

## CI/CD

GitHub Actions workflows in `.github/workflows/`:
- `backend-ci.yml` - runs on pushes to `develop` for backend changes: restore, build, test, Docker build and push with GitHub Actions cache
- `frontend-ci.yml` - runs on pushes to `develop` for frontend changes: install, lint, test, production build, Docker build and push with GitHub Actions cache

### GitHub Actions Setup

Configure these repository secrets before enabling Docker pushes:

- `DOCKERHUB_USERNAME` - your Docker Hub username
- `DOCKERHUB_TOKEN` - a Docker Hub access token with push access

Optional repository variable:

- `DOCKERHUB_NAMESPACE` - overrides the namespace/organization used for image tags; if omitted, the workflows use `DOCKERHUB_USERNAME`

Published image tags on `develop` pushes:

- backend: `<namespace>/shorman-api:develop` and `<namespace>/shorman-api:develop-<commit-sha>`
- frontend: `<namespace>/shorman-web:develop` and `<namespace>/shorman-web:develop-<commit-sha>`

The Docker jobs use `docker/build-push-action` with GitHub Actions layer caching (`cache-from/cache-to: type=gha`) so repeat builds on `develop` stay fast unless Dockerfile inputs change.

## Security Notes

- Never commit `.env` files (they are gitignored)
- Keep `API_CONNECTION_STRING`, `JWT_SIGNING_KEY`, and any future tokens only in local `.env`, GitHub secrets, or Azure app settings
- JWT secret key must be at least 32 characters in production
- Use environment variables for all secrets in production/Azure
- CORS is configured for localhost in development; update for production domains

## Future Roadmap

- [ ] Google/SSO authentication integration
- [ ] Product price comparison across supermarkets
- [ ] Web scraping service for real-time prices
- [ ] Real payment gateway (Stripe/PayPal SDK)
- [ ] Azure deployment (App Service + Azure Database for MySQL)
- [ ] Push notifications for order status
- [ ] Admin dashboard for product management
- [ ] Full E2E test suite
