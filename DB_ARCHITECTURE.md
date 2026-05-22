# Shorman Database Architecture

This document describes the current live database model used by the Shorman backend. It is based on the EF Core model in `ApiDbContext` and the entity definitions in `ApiEntities`, which are the real source of truth for the application.

## 1. Database Purpose

The database supports five main business areas:

- identity and role-based access
- customer profile and delivery addresses
- supermarket catalog and product import history
- pricing policy versioning and audit trail
- cart, checkout, payments, order fulfillment, and rider operations

The backend supports both SQL Server and MySQL. Tables are created and upgraded at application startup through SQL scripts in backend initialization rather than EF migrations.

## 2. Domain Map

```text
users
  |--< addresses
  |--< carts --< cart_items >-- products >-- categories
  |             \
  |              \------------------------> supermarkets
  |
  |--< orders --< order_items
  |      |  \
  |      |   \--< payment_transactions >-- payment_methods
  |      |
  |      \--< order_status_history
  |
  |--< payment_methods
  |--< user_roles >-- roles --< role_menu_permissions
  |
  |-- logical references only --> pricing_policy_versions.CreatedByUserId
  |-- logical references only --> pricing_policy_audit_events.ChangedByUserId
  |-- logical references only --> product_upload_runs.UploadedByUserId

products
  |-- logical references only --> product_upload_runs via LastImportRunId
  \-- logical historical snapshots --> product_history_data
```

## 3. Table-by-Table Breakdown

### users

Purpose: canonical application user table for customers, admins, super admins, and riders.

Primary key:

- `Id`

Important columns:

- `Email`: unique login identity for local auth
- `PasswordHash`: BCrypt hash for local login; may be empty for Auth0-provisioned users
- `FirstName`, `LastName`, `Phone`: profile data
- `IsEmailVerified`, `EmailVerificationCode`, `EmailVerificationExpiresAtUtc`: email verification flow
- `PasswordResetCode`, `PasswordResetExpiresAtUtc`: password reset flow
- `IsDeleted`, `DeletedAtUtc`: soft delete support
- `CreatedAtUtc`: user creation timestamp

Relationships:

- one user to many addresses
- one user to zero or more carts, though the current schema enforces one active cart per user
- one user to many orders as customer
- one user to many assigned orders as rider
- one user to many payment methods
- many-to-many with roles through `user_roles`

Notes:

- This is the core identity table for the application.
- The same table supports both classic email/password login and Auth0 exchange login.

### roles

Purpose: stores application roles such as customer, admin, super admin, and rider.

Primary key:

- `Id`

Important columns:

- `Name`: unique role name

Relationships:

- many-to-many with users through `user_roles`
- one role to many menu permission entries through `role_menu_permissions`

### user_roles

Purpose: join table connecting users and roles.

Primary key:

- composite key: `UserId`, `RoleId`

Foreign keys:

- `UserId -> users.Id`
- `RoleId -> roles.Id`

Why it exists:

- lets one user have multiple roles
- keeps authorization extensible instead of storing one role column on `users`

### role_menu_permissions

Purpose: fine-grained menu and feature visibility mapping per role.

Primary key:

- composite key: `RoleId`, `MenuKey`

Foreign keys:

- `RoleId -> roles.Id`

Important columns:

- `MenuKey`: logical frontend/backend permission key
- `IsEnabled`: whether the role can access that menu/feature

Why it exists:

- separates broad roles from per-feature navigation and access gating
- supports future expansion of role-based administration without redesigning the role table

### addresses

Purpose: stores customer delivery addresses.

Primary key:

- `Id`

Foreign keys:

- `UserId -> users.Id`

Important columns:

- `Label`: friendly name such as Home or Work
- `Street`, `HouseNumber`, `PostalCode`, `City`, `Country`: delivery address data
- `IsDefault`: marks the preferred address for checkout

Relationships:

- many addresses can belong to one user
- orders reference an address snapshot source through `AddressId`

Notes:

- Orders use `DeleteBehavior.Restrict` on address deletion so historical orders do not lose their delivery address reference.

### categories

Purpose: product taxonomy such as bakery, beverages, dairy, meat, skin care, and so on.

Primary key:

- `Id`

Important columns:

- `Name`: display name
- `Slug`: unique stable identifier
- `Icon`: optional UI icon identifier

Relationships:

- one category to many products

### supermarkets

Purpose: master table for store brands supported in the marketplace.

Primary key:

- `Id`

Important columns:

- `Name`: display name
- `Slug`: unique stable identifier
- `LogoUrl`: store logo asset
- `Color`: branding color used by the UI

Relationships:

- one supermarket to many products

### products

Purpose: current live product catalog shown to customers.

Primary key:

- `Id`

Foreign keys:

- `CategoryId -> categories.Id`
- `SupermarketId -> supermarkets.Id`

Important columns:

- `ProductKey`: optional cross-import/business key for deduplication and history tracking
- `Name`, `Description`: product display content
- `Price`: base stored price before dynamic pricing policy adjustment
- `ImageUrl`: product image path or URL
- `Unit`: pack or unit text
- `Stock`: optional stock count
- `IsAvailable`: customer-facing availability flag
- `DataSource`: where the record came from, for example manual, seed, or import
- `UpdatedAtUtc`: last update time
- `LastImportRunId`: points to the latest import run logically, but not as a hard FK in EF

Relationships:

- many products belong to one category
- many products belong to one supermarket
- one product can appear in many cart items
- order items keep a copied snapshot of product details instead of a strict FK dependency for product name/image/store text

Performance notes:

- the catalog now has targeted indexes for name, category, supermarket, product key, and price sorting paths
- filtered catalog indexes are used to speed up available products with images during browse and sort scenarios

### product_upload_runs

Purpose: audit trail for bulk product import jobs.

Primary key:

- `Id`

Important columns:

- `StoreSlug`: target store of the import
- `OriginalFileName`, `StoredFilePath`: source file tracking
- `Status`: processing, completed, failed, and similar states
- `TotalRows`, `InsertedCount`, `UpdatedCount`, `UnchangedCount`, `DeactivatedCount`: import metrics
- `UploadedAtUtc`, `CompletedAtUtc`: operational timestamps
- `UploadedByUserId`: logical link to the user who triggered the import
- `ErrorMessage`: error details when import fails

Relationship note:

- there is no hard FK from `UploadedByUserId` to `users`
- `products.LastImportRunId` is also a logical reference, not a strict relational FK

Why that matters:

- import records remain stable even if surrounding data evolves
- the import log can be treated as operational history, not only normalized business state

### product_history_data

Purpose: immutable-style historical product snapshots used for change tracking.

Primary key:

- `Id`

Important columns:

- `ProductId`: optional link back to the live product row
- `ProductKey`: business/import key for matching across runs
- `Name`, `Description`, `Price`, `ImageUrl`, `Unit`, `Stock`, `IsAvailable`: snapshot of product state at change time
- `CategoryId`, `SupermarketId`: domain context at that moment
- `DataSource`: origin of the data
- `ChangeType`: inserted, updated, deactivated, and similar history labels
- `ChangedAtUtc`: snapshot timestamp
- `ChangedByUserId`: logical actor reference
- `ImportRunId`: logical link to the upload run that caused the change

Design note:

- this table is intentionally denormalized and only lightly constrained
- it preserves what the product looked like when the change happened, even if the current product row later changes again

### pricing_policy_versions

Purpose: versioned pricing rules that adjust customer-facing prices without overwriting base product prices.

Primary key:

- `Id`

Important columns:

- `VersionNo`: unique policy version number
- `XFactorPercent`: percentage uplift or reduction applied to base product price
- `YFactorAmount`: fixed additive adjustment in EUR
- `DeliveryCharge`: delivery fee bundled into the active policy
- `IsActive`: current active version flag
- `EffectiveFromUtc`, `EffectiveToUtc`: policy validity window
- `Reason`: business explanation for the change
- `CreatedByUserId`: logical link to the admin who created the version
- `CreatedAtUtc`: creation timestamp

Business rule:

- only one version is intended to be active at a time
- runtime pricing is calculated approximately as `(base price * (1 + X / 100)) + Y`, then rounded and clamped

Relationship note:

- `CreatedByUserId` is stored, but not enforced as a formal FK in the EF model

### pricing_policy_audit_events

Purpose: immutable audit trail of pricing changes.

Primary key:

- `Id`

Important columns:

- `PolicyVersionId`: links audit entries to a pricing version at the business level
- `ActionType`: created, activated, or similar event type
- old and new values for `XFactorPercent`, `YFactorAmount`, and `DeliveryCharge`
- `ChangedByUserId`: logical actor reference
- `ChangedAtUtc`: event time
- `CorrelationId`: groups related audit operations
- `MetadataJson`: free-form contextual metadata

Indexes:

- lookup by policy version
- lookup by timestamp
- lookup by actor and timestamp

Relationship note:

- the model treats this as an audit table with logical links rather than strict relational navigation properties

### carts

Purpose: current shopping basket for a user.

Primary key:

- `Id`

Foreign keys:

- `UserId -> users.Id`

Important columns:

- `CreatedAtUtc`, `UpdatedAtUtc`: lifecycle tracking

Relationships:

- one cart belongs to one user
- one cart has many cart items

Important constraint:

- `UserId` is unique, so the design currently supports one cart per user

### cart_items

Purpose: individual products currently held in a cart.

Primary key:

- `Id`

Foreign keys:

- `CartId -> carts.Id`
- `ProductId -> products.Id`

Important columns:

- `Quantity`
- `UnitPrice`: price captured when item was added or updated in cart flow

Important constraint:

- unique composite index on `CartId + ProductId`

Meaning:

- the same product appears only once per cart row and uses quantity for aggregation

### orders

Purpose: placed customer orders and the central fulfillment record.

Primary key:

- `Id`

Foreign keys:

- `UserId -> users.Id`
- `AddressId -> addresses.Id`
- `AssignedRiderId -> users.Id` as an optional rider reference

Important columns:

- `CustomerNameSnapshot`, `CustomerEmailSnapshot`: copied customer identity at order time
- `Status`: fulfillment lifecycle, for example pending, awaiting pickup, assigned, picked up, delivered, completed, cancelled
- `PaymentMethod`, `PaymentStatus`: checkout/payment state
- `Subtotal`, `DeliveryFee`, `Total`: money values captured at order time
- operational timestamps such as `AcceptedAtUtc`, `PickedUpAtUtc`, `OutForDeliveryAtUtc`, `DeliveredAtUtc`, `CashCollectedAtUtc`, `CompletedAtUtc`

Relationships:

- one user places many orders
- one address can be referenced by many orders
- one rider can be assigned many orders
- one order has many order items
- one order has many payment transactions
- one order has many status history entries

Important design choice:

- the order is the long-lived business record; it stores snapshots so downstream operations stay stable even if product prices or customer profile data later change

### order_items

Purpose: line items inside an order.

Primary key:

- `Id`

Foreign keys:

- `OrderId -> orders.Id`

Important columns:

- `ProductId`: source product reference kept for traceability
- `ProductName`, `ProductImageUrl`, `SupermarketName`: copied snapshot fields for historical accuracy
- `Quantity`, `UnitPrice`, `TotalPrice`: commercial details captured at purchase time

Design note:

- `ProductId` is indexed, but the EF model does not depend on a full product navigation here
- that keeps completed orders readable even if the catalog row changes or disappears later

### payment_methods

Purpose: saved customer payment methods.

Primary key:

- `Id`

Foreign keys:

- `UserId -> users.Id`

Important columns:

- `Type`: app-level payment type such as card or PayPal-style method identifiers
- `Provider`: Stripe or another gateway/provider
- `ProviderPaymentMethodRef`: external provider token/reference
- `DisplayLabel`, `Last4`, `ExpiryMonth`, `ExpiryYear`, `Country`, `Fingerprint`: customer-facing and dedupe metadata
- `IsDefault`
- `CreatedAtUtc`, `UpdatedAtUtc`

Important constraint:

- unique composite index on `UserId + Provider + ProviderPaymentMethodRef`

Meaning:

- prevents the same external method from being stored repeatedly for one user

### payment_transactions

Purpose: payment attempt and payment outcome ledger for orders.

Primary key:

- `Id`

Foreign keys:

- `OrderId -> orders.Id`
- `PaymentMethodId -> payment_methods.Id` optional

Important columns:

- `Provider`, `PaymentType`: gateway/provider and payment mode
- `Amount`, `Currency`
- `Status`: pending, succeeded, failed, canceled, and related states
- provider references such as `ProviderRef`, `ProviderPaymentIntentRef`, `ProviderSessionRef`, `ProviderChargeRef`
- settlement fields such as `FeeAmount`, `NetAmount`
- `RawProviderStatus`, `FailureCode`, `FailureMessage`, `MetadataJson`
- `CreatedAtUtc`, `UpdatedAtUtc`

Relationships:

- one order can have multiple payment transactions over its lifecycle
- one saved payment method can be linked to many transactions

Why multiple rows matter:

- retries, checkout session creation, provider callbacks, cancellations, and final settlement can all be tracked without mutating a single flat column on `orders`

### order_status_history

Purpose: append-only history of order lifecycle changes.

Primary key:

- `Id`

Foreign keys:

- `OrderId -> orders.Id`

Important columns:

- `Status`
- `Note`
- `ChangedAtUtc`

Relationships:

- one order has many status history events

Why it matters:

- the current order row gives the latest state
- this table tells the story of how the order got there

## 4. Relationship Summary

### Strongly enforced relational links

- `user_roles` enforces user-to-role and role-to-user mappings
- `role_menu_permissions` enforces role-to-menu mappings
- `addresses` enforces user ownership
- `products` enforces category and supermarket ownership
- `carts` and `cart_items` enforce cart structure
- `orders` enforces customer, address, and optional rider assignment
- `payment_methods` enforces user ownership
- `payment_transactions` enforces order linkage and optional payment method linkage
- `order_status_history` enforces order linkage

### Logical references without strict FK navigation

- pricing version creator user
- pricing audit actor user
- product upload actor user
- product last import run
- product history references to product/import/user context

These looser links are intentional in places where the record behaves more like audit history or operational logging than active transactional state.

## 5. Core Business Flows Mapped to Tables

### Registration and login

Flow:

- create user in `users`
- assign default role in `user_roles`
- email verification and password reset also remain inside `users`
- optional Auth0 exchange still resolves into the same canonical `users` row

### Browsing and carting

Flow:

- customer browses `products` joined with `categories` and `supermarkets`
- active price shown to the customer is calculated at runtime using `pricing_policy_versions`
- customer cart lives in `carts` and `cart_items`

### Checkout and payment

Flow:

- cart is converted into `orders` and `order_items`
- payment attempts are logged in `payment_transactions`
- reusable methods are stored in `payment_methods`
- every state change is appended to `order_status_history`

### Rider fulfillment

Flow:

- rider assignment is stored on `orders.AssignedRiderId`
- pickup, delivery, cash collection, and completion timestamps are stored directly on `orders`
- the event trail is stored in `order_status_history`

### Product import and auditing

Flow:

- import job tracked in `product_upload_runs`
- live catalog rows updated in `products`
- snapshots written to `product_history_data`

### Pricing governance

Flow:

- new policy inserted into `pricing_policy_versions`
- previous active policy closed out
- audit event appended to `pricing_policy_audit_events`
- pricing provider cache invalidated and new pricing becomes active in catalog/cart/order calculations

## 6. Important Architectural Notes

### Base price vs selling price

- `products.Price` stores the base catalog price
- customer-facing price is computed dynamically using the active pricing policy
- this preserves original imported prices while still allowing margin and delivery strategy control

### Snapshot-heavy order design

- `orders` and `order_items` intentionally duplicate some customer/product fields
- this is the correct tradeoff for a commerce system because historical orders must stay readable after catalog or profile changes

### One cart per user

- the unique index on `carts.UserId` means the system currently models one active cart per user, not multiple saved baskets

### Startup-managed schema

- schema and indexes are created during application startup
- there is no EF migration chain in the current setup
- operationally, that means startup must be allowed enough time to create heavy indexes on fresh environments

### Catalog performance work already in place

- product browsing and sorting now rely on targeted catalog indexes
- leaner query patterns were introduced so browsing pages sort/page against product rows first, then load small metadata lookups for the returned page

## 7. If You Need to Extend the Schema

Typical safe extension patterns in this codebase:

- add the entity property in `ApiEntities`
- configure table and constraints in `ApiDbContext`
- add startup SQL for SQL Server and MySQL in backend initialization
- update seeding only if the new feature needs bootstrap data
- update the feature handler and contracts, not only the controller

For high-risk changes, be careful with tables that are intentionally history-oriented: `product_history_data`, `pricing_policy_audit_events`, `payment_transactions`, and `order_status_history`.