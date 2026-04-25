import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AsyncPipe, NgIf } from '@angular/common';
import { AuthService } from '../../../core/services/auth.service';
import { CartService } from '../../../core/services/cart.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, AsyncPipe, NgIf],
  template: `
    <nav class="navbar">
      <div class="navbar-container">
        <a routerLink="/" class="navbar-brand">
          <span class="brand-icon">🛒</span>
          <span class="brand-name">Shorman</span>
          <span class="brand-sub">Service</span>
        </a>

        <!-- Desktop Navigation -->
        <div class="navbar-links">
          <a routerLink="/products" routerLinkActive="active" class="nav-link">Products</a>
          <ng-container *ngIf="auth.isLoggedIn$ | async">
            <a routerLink="/orders" routerLinkActive="active" class="nav-link">Orders</a>
            <a routerLink="/addresses" routerLinkActive="active" class="nav-link">Addresses</a>
          </ng-container>
        </div>

        <div class="navbar-actions">
          <!-- Mobile Menu Toggle -->
          <button class="mobile-menu-toggle" (click)="toggleMobileMenu()">
            <span>☰</span>
          </button>

          <button class="cart-btn" (click)="cartService.toggleCart()">
            <span class="cart-icon">🛒</span>
            <span class="cart-badge" *ngIf="(cartService.cart$ | async)?.itemCount as count">
              {{ count > 99 ? '99+' : count }}
            </span>
          </button>

          <ng-container *ngIf="auth.isLoggedIn$ | async; else loginBtn">
            <div class="user-menu">
              <span class="user-name">{{ (auth.currentUser$ | async)?.firstName }}</span>
              <button class="btn-logout" (click)="auth.logout()">Logout</button>
            </div>
          </ng-container>
          <ng-template #loginBtn>
            <a routerLink="/login" class="btn-login">Login</a>
          </ng-template>
        </div>
      </div>

      <!-- Mobile Navigation Menu -->
      <div class="mobile-menu" [class.open]="mobileMenuOpen">
        <a routerLink="/products" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">Products</a>
        <ng-container *ngIf="auth.isLoggedIn$ | async">
          <a routerLink="/orders" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">Orders</a>
          <a routerLink="/addresses" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">Addresses</a>
        </ng-container>
      </div>
    </nav>

    <!-- Mobile Menu Backdrop -->
    <div class="mobile-backdrop" [class.open]="mobileMenuOpen" (click)="closeMobileMenu()"></div>
  `,
  styles: [`
    .navbar {
      background: #fff;
      border-bottom: 2px solid #2E7D32;
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      z-index: 1000;
      box-shadow: 0 2px 8px rgba(0,0,0,0.1);
    }
    .navbar-container {
      max-width: 1200px;
      margin: 0 auto;
      padding: 0 1rem;
      display: flex;
      align-items: center;
      height: 64px;
      gap: 1.5rem;
    }
    .navbar-brand {
      text-decoration: none;
      display: flex;
      align-items: center;
      gap: 0.4rem;
      flex-shrink: 0;
    }
    .brand-icon { font-size: 1.6rem; }
    .brand-name {
      font-size: 1.4rem;
      font-weight: 800;
      color: #2E7D32;
      letter-spacing: -0.5px;
    }
    .brand-sub {
      font-size: 0.85rem;
      color: #FF6F00;
      font-weight: 600;
    }
    .navbar-links {
      display: flex;
      gap: 0.25rem;
      flex: 1;
    }
    .nav-link {
      text-decoration: none;
      color: #555;
      padding: 0.4rem 0.75rem;
      border-radius: 6px;
      font-weight: 500;
      font-size: 0.95rem;
      transition: all 0.2s;
    }
    .nav-link:hover, .nav-link.active {
      color: #2E7D32;
      background: #E8F5E9;
    }
    .navbar-actions {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      flex-shrink: 0;
    }
    .cart-btn {
      position: relative;
      background: none;
      border: 2px solid #2E7D32;
      border-radius: 8px;
      padding: 0.4rem 0.75rem;
      cursor: pointer;
      display: flex;
      align-items: center;
      gap: 0.25rem;
      transition: all 0.2s;
      font-size: 1.1rem;
    }
    .cart-btn:hover { background: #E8F5E9; }
    .cart-badge {
      position: absolute;
      top: -8px;
      right: -8px;
      background: #FF6F00;
      color: #fff;
      border-radius: 50%;
      min-width: 20px;
      height: 20px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 0.7rem;
      font-weight: 700;
      padding: 0 3px;
    }
    .user-menu {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .user-name {
      font-weight: 600;
      color: #2E7D32;
      font-size: 0.9rem;
    }
    .btn-logout {
      background: none;
      border: 1px solid #ccc;
      padding: 0.35rem 0.75rem;
      border-radius: 6px;
      cursor: pointer;
      font-size: 0.85rem;
      color: #666;
      transition: all 0.2s;
    }
    .btn-logout:hover { background: #fee; border-color: #f44; color: #f44; }
    .btn-login {
      text-decoration: none;
      background: #2E7D32;
      color: #fff;
      padding: 0.4rem 1rem;
      border-radius: 6px;
      font-weight: 600;
      font-size: 0.9rem;
      transition: background 0.2s;
    }
    .btn-login:hover { background: #1B5E20; }

    /* Mobile Menu Toggle Button */
    .mobile-menu-toggle {
      display: none;
      background: none;
      border: none;
      font-size: 1.8rem;
      color: #2E7D32;
      cursor: pointer;
      padding: 0.25rem;
      line-height: 1;
    }

    /* Mobile Menu */
    .mobile-menu {
      display: none;
      position: fixed;
      top: 64px;
      right: 0;
      width: 250px;
      max-width: 80vw;
      background: #fff;
      box-shadow: -2px 0 8px rgba(0,0,0,0.15);
      z-index: 999;
      transform: translateX(100%);
      transition: transform 0.3s ease;
      padding: 1rem 0;
    }

    .mobile-menu.open {
      transform: translateX(0);
    }

    .mobile-nav-link {
      display: block;
      text-decoration: none;
      color: #555;
      padding: 0.875rem 1.25rem;
      font-weight: 500;
      font-size: 1rem;
      transition: all 0.2s;
      border-left: 3px solid transparent;
    }

    .mobile-nav-link:hover,
    .mobile-nav-link.active {
      color: #2E7D32;
      background: #E8F5E9;
      border-left-color: #2E7D32;
    }

    /* Mobile Backdrop */
    .mobile-backdrop {
      display: none;
      position: fixed;
      top: 64px;
      left: 0;
      right: 0;
      bottom: 0;
      background: rgba(0, 0, 0, 0.5);
      z-index: 998;
      opacity: 0;
      pointer-events: none;
      transition: opacity 0.3s ease;
    }

    .mobile-backdrop.open {
      opacity: 1;
      pointer-events: all;
    }

    @media (max-width: 768px) {
      .navbar-links { display: none; }
      .mobile-menu-toggle { display: block; }
      .mobile-menu { display: block; }
      .mobile-backdrop { display: block; }
      .user-name { display: none; }
      .brand-sub { display: none; }
      .btn-login { padding: 0.35rem 0.75rem; font-size: 0.85rem; }
    }

    @media (max-width: 600px) {
      .brand-name { font-size: 1.2rem; }
    }
  `]
})
export class NavbarComponent {
  auth = inject(AuthService);
  cartService = inject(CartService);
  mobileMenuOpen = false;

  toggleMobileMenu() {
    this.mobileMenuOpen = !this.mobileMenuOpen;
  }

  closeMobileMenu() {
    this.mobileMenuOpen = false;
  }
}
