import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { authEntryGuard } from './core/guards/auth-entry.guard';
import { roleGuard } from './core/guards/role.guard';
import { homeRedirectGuard } from './core/guards/home-redirect.guard';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./features/home-redirect/home-redirect.component').then(m => m.HomeRedirectComponent), canActivate: [homeRedirectGuard], pathMatch: 'full' },
  { path: 'auth/callback', loadComponent: () => import('./features/auth/auth-callback.component').then(m => m.AuthCallbackComponent) },
  { path: 'products', loadComponent: () => import('./features/products/products.component').then(m => m.ProductsComponent) },
  { path: 'login', loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent), canActivate: [authEntryGuard] },
  { path: 'register', loadComponent: () => import('./features/auth/register.component').then(m => m.RegisterComponent), canActivate: [authEntryGuard] },
  { path: 'cart', loadComponent: () => import('./features/cart/cart-sidebar.component').then(m => m.CartSidebarComponent) },
  { path: 'checkout', loadComponent: () => import('./features/checkout/checkout.component').then(m => m.CheckoutComponent), canActivate: [roleGuard], data: { roles: ['SuperAdmin', 'Admin', 'Customer'] } },
  { path: 'orders', loadComponent: () => import('./features/orders/orders.component').then(m => m.OrdersComponent), canActivate: [roleGuard], data: { roles: ['SuperAdmin', 'Admin', 'Customer'] } },
  { path: 'addresses', loadComponent: () => import('./features/addresses/addresses.component').then(m => m.AddressesComponent), canActivate: [roleGuard], data: { roles: ['SuperAdmin', 'Admin', 'Customer'] } },
  { path: 'rider', loadComponent: () => import('./features/rider-dashboard/rider-dashboard.component').then(m => m.RiderDashboardComponent), canActivate: [roleGuard], data: { roles: ['SuperAdmin', 'Rider'] } },
  { path: 'user-management', loadComponent: () => import('./features/user-management/user-management.component').then(m => m.UserManagementComponent), canActivate: [roleGuard], data: { roles: ['SuperAdmin'] } },
  { path: 'unauthorized', loadComponent: () => import('./features/unauthorized/unauthorized.component').then(m => m.UnauthorizedComponent), canActivate: [authGuard] },
  { path: '**', redirectTo: '' }
];
