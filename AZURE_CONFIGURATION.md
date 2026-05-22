# Azure Configuration Split

Use this rule:

- Put a value in Key Vault if disclosure would grant access, signing authority, or payment control.
- Keep a value in environment variables if it is non-secret runtime behavior, bootstrap wiring, or public client configuration.

## Backend API

Keep these in App Service environment variables:

- `KeyVault__VaultUri`
- `ASPNETCORE_ENVIRONMENT`
- `Database__Provider`
- `Cors__AllowedOrigins__0`
- `Cors__AllowedOrigins__1`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Auth0__Domain`
- `Auth0__Audience`
- `RoleBootstrap__SuperAdminEmails`
- `Stripe__FrontendBaseUrl`
- `Stripe__CheckoutSuccessPath`
- `Stripe__CheckoutCancelPath`
- `Stripe__Currency`

Store these as Key Vault secrets:

- `ConnectionStrings--ApiConnection`
- `Jwt--SigningKey`
- `Stripe--SecretKey`
- `Stripe--WebhookSecret`

Optional:

- `ConnectionStrings--DefaultConnection` if you still want a fallback connection string

## Angular Frontend

These are public client settings and should stay outside Key Vault unless your build pipeline merely reads them from Vault and injects them at build time:

- API base URL
- `auth0_domain`
- `auth0_clientId`
- `auth0_audience`
- `useMockAuth`
- `useMockAddresses`
- `useMockOrders`
- language defaults and feature flags

Important:

- Angular production values compiled into the bundle are public in the browser.
- Putting frontend values in Key Vault does not make them secret once the app is built.
- Use Key Vault for backend-only secrets, not for browser-delivered values.