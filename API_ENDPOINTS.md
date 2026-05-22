# Shorman API Endpoints

This document is a practical API reference for the current backend controller surface. It is grouped by business area so a new developer or integrator can quickly find the right endpoint.

## Conventions

- Base URL locally is typically `/api/...` behind the ASP.NET Core backend.
- Most secured endpoints use the app JWT and role claims.
- Auth0 login exchange uses the Auth0 bearer scheme on the dedicated exchange endpoint.
- Pagination responses generally use `PagedResultDto<T>`.
- When a controller catches `InvalidOperationException`, the API usually returns `400`, `401`, or `409` with a simple `{ message }` payload.

## 1. Authentication

Base route: `/api/auth`

### `POST /api/auth/exchange`

Purpose: exchange an Auth0-authenticated identity into the app's own JWT and canonical user record.

Auth:

- requires Auth0 authentication scheme

Body:

- `Auth0ExchangeRequest`
- fields may include `email`, `firstName`, `lastName`

Response:

- `200 OK` with `AuthResponse`
- `401 Unauthorized` if email cannot be resolved from request or Auth0 claims

### `POST /api/auth/login`

Purpose: local email/password login.

Body:

- `LoginRequest`

Response:

- `200 OK` with `AuthResponse`
- `401 Unauthorized` on invalid credentials or unverified email

### `POST /api/auth/register`

Purpose: create a local user account.

Body:

- `RegisterRequest`

Response:

- `200 OK` with `RegisterResponse`
- `409 Conflict` if the email is already registered

### `POST /api/auth/verify-email`

Purpose: verify a newly registered account using the one-time code.

Body:

- `VerifyEmailRequest`

Response:

- `200 OK` with `AuthResponse`
- `400 Bad Request` when the code is invalid or expired

### `POST /api/auth/password-reset/request`

Purpose: start password reset flow.

Body:

- `PasswordResetRequest`

Response:

- `200 OK` with `PasswordResetRequestResponse`

### `POST /api/auth/password-reset/confirm`

Purpose: complete password reset using the reset code.

Body:

- `PasswordResetConfirmRequest`

Response:

- `200 OK`
- `400 Bad Request` on invalid code or invalid password

## 2. Catalog Discovery

### `GET /api/categories`

Purpose: list all product categories.

Auth:

- public

Response:

- `200 OK` with `IReadOnlyCollection<CategoryDto>`

### `GET /api/supermarkets`

Purpose: list supported supermarkets.

Auth:

- public

Response:

- `200 OK` with `IReadOnlyCollection<SupermarketDto>`

### `GET /api/products`

Purpose: customer-facing product browse endpoint.

Auth:

- public

Query parameters:

- `search`
- `categoryId`
- `supermarketId`
- `supermarketIds` as array
- `sort` with `Default`, `PriceLowToHigh`, `PriceHighToLow`
- `page`, default `1`
- `pageSize`, default `30`

Response:

- `200 OK` with `PagedResultDto<ProductDto>`

Notes:

- this is the main endpoint used by the groceries page
- sorting is API-backed, not only client-side

### `GET /api/products/{id}`

Purpose: retrieve one product by id.

Auth:

- public

Response:

- `200 OK` with `ProductDto`
- `404 Not Found` if the product does not exist

## 3. Admin Catalog Maintenance

### `GET /api/products/admin`

Purpose: paged product listing for admin operations.

Auth:

- roles: `SuperAdmin`, `Admin`

Query parameters:

- `search`
- `categoryId`
- `supermarketId`
- `supermarketIds`
- `page`, default `1`
- `pageSize`, default `50`

Response:

- `200 OK` with `PagedResultDto<ProductDto>`

### `PUT /api/products/{id}`

Purpose: update an existing product.

Auth:

- roles: `SuperAdmin`, `Admin`

Body:

- `UpdateProductRequest`

Response:

- `200 OK` with updated `ProductDto`
- `400 Bad Request` when validation fails
- `404 Not Found` if the product does not exist

### `DELETE /api/products/{id}`

Purpose: delete a product.

Auth:

- roles: `SuperAdmin`, `Admin`

Response:

- `204 No Content` on success
- `404 Not Found` if missing

## 4. Product Management Imports and History

Base route: `/api/product-management`

Auth for all endpoints in this section:

- roles: `SuperAdmin`, `Admin`

### `GET /api/product-management/products`

Purpose: paged operational product list for management tooling.

Query parameters:

- `search`
- `categoryId`
- `supermarketId`
- `page`, default `1`
- `pageSize`, default `50`

Response:

- `200 OK` with `PagedResultDto<ProductManagementProductDto>`

### `GET /api/product-management/history`

Purpose: browse product change history snapshots.

Query parameters:

- `search`
- `supermarketId`
- `page`, default `1`
- `pageSize`, default `25`

Response:

- `200 OK` with `PagedResultDto<ProductHistoryDataDto>`

### `GET /api/product-management/uploads`

Purpose: browse product upload/import runs.

Query parameters:

- `storeSlug`
- `page`, default `1`
- `pageSize`, default `25`

Response:

- `200 OK` with `PagedResultDto<ProductUploadRunDto>`

### `POST /api/product-management/upload`

Purpose: upload a product sheet for ingestion.

Request type:

- multipart form data
- field: `file`

Limits:

- request size limit is `20 MB`

Response:

- `200 OK` with `ProductUploadRunDto`
- `400 Bad Request` if no file is attached or the import validation fails

## 5. Pricing Administration

Base route: `/api/product-management/pricing`

Auth for all endpoints in this section:

- role: `SuperAdmin`

### `GET /api/product-management/pricing/current`

Purpose: return the current active pricing policy.

Response:

- `200 OK` with `PricingPolicyVersionDto`

### `GET /api/product-management/pricing/history`

Purpose: paged pricing policy version history.

Query parameters:

- `page`, default `1`
- `pageSize`, default `25`

Response:

- `200 OK` with `PagedResultDto<PricingPolicyVersionDto>`

### `GET /api/product-management/pricing/audit`

Purpose: paged pricing audit event history.

Query parameters:

- `page`, default `1`
- `pageSize`, default `25`

Response:

- `200 OK` with `PagedResultDto<PricingPolicyAuditEventDto>`

### `POST /api/product-management/pricing/apply`

Purpose: activate a new pricing policy version.

Body:

- `ApplyPricingPolicyRequest`

Response:

- `200 OK` with `PricingPolicyVersionDto`
- `400 Bad Request` if pricing values fall outside allowed limits

## 6. Addresses and Delivery

### `GET /api/delivery/check`

Purpose: validate whether an address is inside the supported delivery radius.

Auth:

- public

Query parameters:

- `postalCode` required
- `city`
- `street`
- `houseNumber`
- `country`

Response:

- `200 OK` with `DeliveryCheckResult`
- `400 Bad Request` if `postalCode` is missing

### `GET /api/addresses`

Purpose: list the current user's saved addresses.

Auth:

- roles: `SuperAdmin`, `Admin`, `Customer`

Response:

- `200 OK` with `IReadOnlyCollection<AddressDto>`

### `POST /api/addresses`

Purpose: create an address for the current user.

Auth:

- roles: `SuperAdmin`, `Admin`, `Customer`

Body:

- `CreateAddressRequest`

Response:

- `200 OK` with `AddressDto`

### `PUT /api/addresses/{id}`

Purpose: update one address.

Auth:

- roles: `SuperAdmin`, `Admin`, `Customer`

Body:

- `UpdateAddressRequest`

Response:

- `200 OK` with `AddressDto`
- `404 Not Found`

### `DELETE /api/addresses/{id}`

Purpose: delete one address.

Auth:

- roles: `SuperAdmin`, `Admin`, `Customer`

Response:

- `204 No Content`
- `404 Not Found`

### `PATCH /api/addresses/{id}/default`

Purpose: mark one address as the user's default address.

Auth:

- roles: `SuperAdmin`, `Admin`, `Customer`

Response:

- `200 OK` with `AddressDto`
- `404 Not Found`

## 7. Cart and Checkout

Base route: `/api/cart`

Auth for all endpoints in this section:

- roles: `SuperAdmin`, `Admin`, `Customer`

### `GET /api/cart`

Purpose: return the current user's active cart.

Response:

- `200 OK` with `CartDto`

### `POST /api/cart/items`

Purpose: add an item to the cart.

Body:

- `AddCartItemRequest`

Response:

- `200 OK` with updated `CartDto`
- `400 Bad Request` when product or quantity is invalid

### `PUT /api/cart/items/{itemId}`

Purpose: update cart item quantity or details.

Body:

- `UpdateCartItemRequest`

Response:

- `200 OK` with updated `CartDto`
- `400 Bad Request` on invalid operation

### `DELETE /api/cart/items/{itemId}`

Purpose: remove one item from the cart.

Response:

- `200 OK` with updated `CartDto`

### `DELETE /api/cart`

Purpose: clear the entire cart.

Response:

- `200 OK` with updated `CartDto`

## 8. Orders

Base route: `/api/orders`

Auth for all endpoints in this section:

- roles: `SuperAdmin`, `Admin`, `Customer`

### `GET /api/orders`

Purpose: list the current user's orders.

Response:

- `200 OK` with `IReadOnlyCollection<OrderDto>`

### `GET /api/orders/{id}`

Purpose: fetch one order owned by the current user.

Response:

- `200 OK` with `OrderDto`
- `404 Not Found`

### `POST /api/orders`

Purpose: place an order directly.

Use case:

- cash or non-Stripe-managed flows

Body:

- `CreateOrderRequest`

Response:

- `201 Created` with `OrderDto`
- `400 Bad Request` on invalid state or unsupported checkout path

### `POST /api/orders/checkout-session`

Purpose: create a Stripe checkout session for a provider-managed payment flow.

Body:

- `CreateOrderRequest`

Response:

- `200 OK` with `CheckoutSessionResponse`
- `400 Bad Request` if the chosen payment method is not Stripe-managed or order preparation fails

### `POST /api/orders/{id}/payment-cancelled`

Purpose: mark a pending checkout flow as cancelled by the user.

Response:

- `200 OK` with updated `OrderDto`
- `400 Bad Request` when cancellation is not valid for the order/payment state

## 9. Payment Callbacks and Confirmation

Base route: `/api/payments`

### `POST /api/payments/stripe/webhook`

Purpose: Stripe webhook receiver for checkout lifecycle events.

Auth:

- provider callback endpoint, not user-authenticated

Expected header:

- `Stripe-Signature`

Behavior:

- handles completed, async success, async failed, and expired checkout events
- updates `orders`, `payment_transactions`, `payment_methods`, and `order_status_history`

Response:

- `200 OK` on accepted processing
- `400 Bad Request` on missing/invalid signature or invalid payload

### `POST /api/payments/stripe/checkout/{orderId}/confirm`

Purpose: explicit customer confirmation path for Stripe checkout completion.

Auth:

- roles: `SuperAdmin`, `Admin`, `Customer`

Query parameters:

- optional `sessionId`

Response:

- `200 OK` with confirmation payload
- `400 Bad Request` on invalid session/payment type
- `404 Not Found` if the order does not belong to the user or does not exist

## 10. Rider Operations

Base route: `/api/rider/orders`

Auth for all endpoints in this section:

- roles: `SuperAdmin`, `Rider`

### `GET /api/rider/orders/available`

Purpose: list orders waiting for rider pickup.

Response:

- `200 OK` with `IReadOnlyCollection<OrderDto>`

### `GET /api/rider/orders/mine`

Purpose: list the current rider's assigned orders.

Response:

- `200 OK` with `IReadOnlyCollection<OrderDto>`

### `GET /api/rider/orders/{id}`

Purpose: get one rider-visible order.

Response:

- `200 OK` with `OrderDto`
- `404 Not Found`

### `POST /api/rider/orders/{id}/accept`

Purpose: accept an available order.

### `POST /api/rider/orders/{id}/picked-up`

Purpose: mark accepted order as picked up.

### `POST /api/rider/orders/{id}/out-for-delivery`

Purpose: mark order as out for delivery.

### `POST /api/rider/orders/{id}/delivered`

Purpose: mark order as delivered.

### `POST /api/rider/orders/{id}/cash-collected`

Purpose: mark cash collection complete.

### `POST /api/rider/orders/{id}/complete`

Purpose: fully complete order lifecycle.

For all rider transition endpoints:

- response is `200 OK` with `OrderDto` on success
- response is `400 Bad Request` when the status transition is invalid

## 11. Admin Users and Permissions

Base route: `/api/admin/users`

### `GET /api/admin/users`

Purpose: list all users for super admin administration.

Auth:

- role: `SuperAdmin`

Response:

- `200 OK` with `IReadOnlyCollection<AdminUserListItemDto>`

### `GET /api/admin/users/order-summary`

Purpose: high-level order summary for admin and rider operational views.

Auth:

- roles: `SuperAdmin`, `Admin`, `Rider`

Response:

- `200 OK` with `IReadOnlyCollection<AdminOrderSummaryDto>`

### `GET /api/admin/users/order-summary/awaiting-pickup-count`

Purpose: lightweight alert count for pickup queue awareness.

Auth:

- roles: `SuperAdmin`, `Admin`, `Rider`

Response:

- `200 OK` with `OrderAlertCountDto`

### `PUT /api/admin/users/{id}/role`

Purpose: change a user's application role.

Auth:

- role: `SuperAdmin`

Body:

- `UpdateUserRoleRequest`

Response:

- `200 OK` with `AdminUserListItemDto`
- `400 Bad Request` on invalid role change
- `404 Not Found` if user does not exist

### `DELETE /api/admin/users/{id}`

Purpose: soft-delete or remove a user through admin action.

Auth:

- role: `SuperAdmin`

Response:

- `200 OK` with `DeleteUserResponse`
- `400 Bad Request` on protected/invalid delete
- `404 Not Found` if user does not exist

### `GET /api/admin/users/menu-permissions`

Purpose: fetch menu permissions grouped by role.

Auth:

- role: `SuperAdmin`

Response:

- `200 OK` with `IReadOnlyCollection<RoleMenuPermissionsDto>`

### `PUT /api/admin/users/menu-permissions/{role}`

Purpose: replace or update enabled menu keys for a role.

Auth:

- role: `SuperAdmin`

Body:

- `UpdateRoleMenuPermissionsRequest`

Response:

- `200 OK` with `RoleMenuPermissionsDto`
- `400 Bad Request` if the role or permission payload is invalid

## 12. Current User Menu Access

### `GET /api/menu-permissions/current`

Purpose: resolve current user's enabled menu keys from their role claims.

Auth:

- any authenticated user

Response:

- `200 OK` with `CurrentMenuPermissionsDto`

## 13. Insights and Reporting

### `GET /api/order-insights`

Purpose: return customer/admin order insight data for a requested time range.

Auth:

- roles: `SuperAdmin`, `Admin`, `Customer`

Query parameters:

- `range`, default `1y`

Response:

- `200 OK` with `OrderInsightsDto`

## 14. Suggested Reading Order for Integrators

If you are integrating with the system for the first time, the most useful sequence is:

1. authentication endpoints
2. categories, supermarkets, products, and delivery check
3. cart endpoints
4. order placement or checkout-session flow
5. payment confirmation or webhook behavior
6. order retrieval and rider/admin operational endpoints

## 15. Source of Truth

This file is based on the live controller implementations in the backend. If behavior drifts later, the controllers and feature handlers are the definitive reference.