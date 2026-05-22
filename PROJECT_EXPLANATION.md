# Shorman Project Explanation

This document explains the project at two levels:

- a business and product explanation for handover or stakeholder discussions
- a more technical explanation for engineers, interviews, and system walkthroughs

## 1. What Shorman Is

Shorman is a grocery and daily-needs ordering platform. Customers can browse products from multiple supermarkets, add items to a cart, verify whether their address is inside the supported delivery radius, place an order, pay through supported payment methods, and track the order through fulfillment. Riders can accept and deliver orders. Admin users can manage products, pricing policy, access permissions, and operational data.

In simple terms, it behaves like a focused local commerce platform for multi-store shopping and delivery.

## 2. Main Workspace Parts

### shorman-api

This is the main ASP.NET Core backend.

It is responsible for:

- authentication and authorization
- catalog APIs for products, categories, and supermarkets
- cart, checkout, payment, and order workflows
- delivery eligibility checks
- rider operations
- admin user management, menu permissions, and pricing policy management
- product import and product history support
- database initialization and seeding

### shorman-web

This is the Angular frontend used by customers and operational users.

It is responsible for:

- login and account flows
- multilingual UI
- browsing the catalog
- store/category filtering and price sorting
- cart and checkout experience
- order history and order status viewing
- admin and rider dashboards
- pricing management and user-management screens

### datafeedService

This is a supporting Python service for scraping or collecting supermarket data. It appears intended for data acquisition and offline product feed generation.

### web-scraper

This is another Python-based scraping/support service. It looks like a separate extraction utility for external product data sources.

### docker-compose.yml

This is the top-level orchestration entry point for running multiple services together in containerized setups.

## 3. Core Product Capabilities

The platform currently covers these major capabilities:

- customer registration, login, verification, and password reset
- Auth0-assisted login exchange into the app's own user model
- role-based access with menu-level permission gates
- address management and delivery eligibility checks
- multi-supermarket product browsing
- product sorting and filtering with server-backed pagination
- cart and checkout
- payment tracking with provider-aware transaction records
- rider assignment and order fulfillment status tracking
- dynamic pricing policy management with version history and audit trail
- product management/import history for maintaining the catalog
- order insights and seeded reporting/demo data

## 4. How the System Is Connected

At a high level, the system has a very clear flow:

```text
Angular frontend
  -> calls REST controllers in ASP.NET Core
  -> controllers dispatch MediatR feature handlers
  -> handlers use EF Core DbContext and service abstractions
  -> data is stored in SQL Server or MySQL
  -> selected integrations call external services like Auth0, Stripe, Key Vault, and geocoding
```

That means the application is split into layers instead of putting all logic inside controllers.

## 5. Backend Architecture

### API style

The backend is a controller-based ASP.NET Core Web API. Controllers are mostly thin entry points. They translate HTTP requests into commands and queries handled through MediatR.

This is a good pattern because it keeps:

- HTTP concerns in controllers
- business logic in feature handlers
- data access in EF Core queries and supporting services

### Feature modules

The main backend feature modules are:

- `AuthFeature`: registration, login, verification, password reset, Auth0 exchange
- `CatalogFeature`: products, categories, supermarkets, browsing, sorting, paging
- `CartFeature`: active cart management
- `OrdersFeature`: order placement, checkout session creation, order retrieval, rider flow, insights
- `DeliveryFeature`: delivery radius validation and geocoding
- `PricingPolicyFeature`: dynamic pricing versioning, current policy lookup, audit trail
- `AdminUsersFeature`: administrative user management
- `ProductManagementFeature`: product upload/import and operational catalog management
- `AddressesFeature`: user delivery address management

### Persistence approach

The backend uses EF Core, but the schema lifecycle is not driven by classic EF migrations. Instead, startup initialization ensures tables and indexes exist.

That means:

- startup is doing real infrastructure work
- SQL Server and MySQL both need explicit script support
- schema changes should be added consistently in entity classes, DbContext configuration, and startup SQL scripts

### Authentication and authorization

The system currently supports two identity entry paths:

- local JWT authentication with email and password
- Auth0 token exchange into the app's own user table

The application still uses its own `users` table as the canonical business identity source. That is an important architectural decision because orders, addresses, payments, and role assignments all depend on app-owned user IDs.

Authorization is not only role-based. It also includes menu-level permission records so that features can be enabled or disabled per role more precisely.

### Configuration model

The backend reads configuration from:

- `appsettings.json`
- optional local override files
- environment variables
- Azure Key Vault when configured

This gives the app a local-development path and a cloud deployment path without hardcoding secrets into source.

## 6. Frontend Architecture

The frontend is an Angular application using standalone architecture and a feature-oriented structure.

Important frontend characteristics:

- HTTP-based communication with the backend API
- RxJS for async flows and request handling
- translated UI via `ngx-translate`
- dedicated features for products, checkout, orders, rider, admin, and pricing
- server-backed product browsing instead of loading huge catalog sets into the browser

Recent catalog work is especially important:

- products are loaded through paged API calls
- price sorting is backed by the API, not fake client-side sorting of one page
- overlapping in-flight catalog requests were canceled on the frontend to avoid broken state during store/filter changes

## 7. Database Design Philosophy

The database is not only a storage layer. It captures the business model explicitly.

The main design ideas are:

- `users`, `roles`, `user_roles`, and `role_menu_permissions` model identity and access
- `categories`, `supermarkets`, and `products` model the live catalog
- `product_upload_runs` and `product_history_data` model catalog operations and audit history
- `pricing_policy_versions` and `pricing_policy_audit_events` model commercial control and governance
- `carts` and `cart_items` model pre-checkout state
- `orders`, `order_items`, `payment_methods`, `payment_transactions`, and `order_status_history` model commercial transactions and fulfillment

One of the best parts of the current design is that it separates base catalog price from customer-visible selling price. The product table stores the base price, while the active pricing policy applies margin and delivery strategy dynamically at runtime.

## 8. Important End-to-End Flows

### A. Customer onboarding

1. User registers.
2. A row is created in `users`.
3. A default customer role is assigned.
4. Email verification completes.
5. JWT token is issued for application access.

### B. Product discovery

1. Frontend requests products with filters like category, supermarket, search, page, page size, and sort.
2. Backend queries the catalog efficiently.
3. Active pricing policy is applied to expose the effective customer price.
4. Results are returned with pagination metadata.

### C. Cart to checkout

1. User adds products to cart.
2. Backend persists the cart in `carts` and `cart_items`.
3. Delivery eligibility is checked against the configured delivery zone.
4. At checkout, cart data is converted into `orders` and `order_items`.
5. Payment flow creates one or more `payment_transactions`.

### D. Payment completion

There are two broad paths:

- cash-style order flow, where the order can move into pickup and delivery while payment stays operationally tracked
- provider-managed flow, where checkout session creation happens first and transaction references are stored from the provider

### E. Rider fulfillment

1. An available rider sees orders waiting for pickup.
2. Rider accepts one order.
3. The order is assigned through `AssignedRiderId`.
4. Status moves through pickup, out-for-delivery, delivered, cash-collected, and completed stages.
5. Each transition is also written into `order_status_history`.

### F. Pricing administration

1. Super admin changes X factor, Y factor, or delivery charge.
2. Current policy is closed.
3. New active version is created.
4. Audit event is stored.
5. Product price calculations across catalog, cart, order, and delivery flows use the new active policy.

### G. Product import and catalog maintenance

1. Admin uploads or triggers product feed ingestion.
2. Import job is tracked in `product_upload_runs`.
3. Current catalog rows in `products` are inserted or updated.
4. History snapshots are written to `product_history_data`.

## 9. Pricing Model Explained Clearly

This is one of the most important business features in the project.

The system does not overwrite imported product prices every time the business wants to change margin. Instead, it keeps:

- a base price on the product
- an active pricing policy version

The pricing policy applies:

- `XFactorPercent`: a percentage adjustment
- `YFactorAmount`: a fixed additive adjustment
- `DeliveryCharge`: delivery fee logic managed alongside pricing policy

This approach is stronger than directly editing catalog prices because it gives:

- version history
- auditability
- rollback-friendly governance
- consistent pricing behavior across the whole order lifecycle

## 10. Why the Current Architecture Is Good

From a technical interview perspective, the strongest points are:

- clear separation of frontend, backend, and data ingestion utilities
- thin-controller plus MediatR handler design in the backend
- app-owned canonical user table even when external identity providers are used
- versioned pricing instead of destructive price overwrites
- snapshot-based order design for historical consistency
- explicit rider flow and operational timestamps
- audit/history tables for commercial and import activity
- cloud-ready configuration through environment variables and Key Vault
- multi-database support for SQL Server and MySQL

## 11. Interview-Style Technical Explanation

If you had to explain this project to an interviewer, a strong summary would be:

"Shorman is a multi-store grocery delivery platform built with an Angular frontend and an ASP.NET Core backend. The backend uses MediatR and EF Core to organize business logic cleanly around features such as catalog browsing, cart, checkout, rider delivery flow, pricing governance, and admin operations. The database is structured around core commerce entities like users, roles, products, orders, payments, and status history, with separate tables for pricing policy versions and audit events so pricing can be changed centrally without mutating base catalog prices. The system also supports operational catalog imports, role/menu permission gating, delivery radius checks, and cloud-friendly configuration including Azure Key Vault."

You can then go one level deeper:

- "Controllers are intentionally thin and delegate to MediatR handlers."
- "The pricing engine is policy-based and applied at runtime, which avoids rewriting catalog prices."
- "Orders use snapshot fields so historical records remain correct even if products or user data change later."
- "The schema supports both transactional state and audit history, which is important for commerce systems."
- "Catalog performance work focused on server-side paging, API-backed sorting, targeted indexes, and leaner queries."

## 12. Important Current Engineering Realities

These are practical things someone taking over the project should know:

- the root README is useful for orientation but some details are older than the current implementation
- schema creation happens at startup, so heavy index creation can affect first-run boot time
- SQL Server and MySQL paths both need attention whenever schema changes are introduced
- the products page has already been optimized away from client-side multi-store fan-out toward cleaner API-backed paging and sorting
- backend build issues during local development may sometimes be caused by locked output files from already running backend processes, not actual code errors

## 13. Where to Look First When Explaining or Extending the System

For the cleanest understanding of the codebase, start here:

- backend entry and wiring: `Program.cs` and `DependencyInjection.cs`
- database model: `ApiDbContext.cs` and `ApiEntities.cs`
- authentication: `AuthFeature.cs`
- catalog and performance-sensitive browsing: `CatalogFeature.cs`
- checkout and rider flow: `OrdersFeature.cs`
- pricing logic: `PricingPolicyFeature.cs`
- frontend customer journey: products, checkout, orders, rider, and product-management feature folders in the Angular app

## 14. Short Non-Technical Summary

If you need to explain it to a non-technical person:

Shorman is an app where users can order groceries and household products from different supermarkets, pay through supported payment methods, and get the order delivered by riders. Admins can control who accesses which features, update pricing rules centrally, and maintain the product catalog. The system is built so pricing, order history, and operational tracking stay reliable even as the business grows.