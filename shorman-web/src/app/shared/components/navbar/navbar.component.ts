import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AsyncPipe, NgIf } from '@angular/common';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService } from '../../../core/services/auth.service';
import { CartService } from '../../../core/services/cart.service';
import { AppRole, CUSTOMER_ROLES, RIDER_ROLES } from '../../../core/models/user.model';
import { MenuPermissionService } from '../../../core/services/menu-permission.service';
import { USER_MANAGEMENT_MENU_KEYS } from '../../../core/models/menu-permission.model';
import { OrderAlertService } from '../../../core/services/order-alert.service';
import { LanguageService } from '../../../core/services/language.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, AsyncPipe, NgIf, TranslatePipe],
  template: `
    <ng-container *ngIf="orderAlert.notification$ | async as notification">
    </ng-container>
    <nav class="navbar">
      <div class="navbar-container">
        <a routerLink="/products" class="navbar-brand">
          <span class="brand-icon">🛒</span>
          <span class="brand-name">Shorman</span>
          <span class="brand-sub">Service</span>
        </a>

        <!-- Desktop Navigation -->
        <div class="navbar-links">
          <a routerLink="/products" routerLinkActive="active" class="nav-link">{{ 'navbar.products' | translate }}</a>
          <ng-container *ngIf="auth.isLoggedIn$ | async">
            <a *ngIf="showOrders()" routerLink="/orders" routerLinkActive="active" class="nav-link">{{ 'navbar.orders' | translate }}</a>
            <a *ngIf="showHistoryStats()" routerLink="/history-stats" routerLinkActive="active" class="nav-link">{{ 'navbar.historyStats' | translate }}</a>
            <a *ngIf="auth.hasAnyRole(customerRoles) && menuPermissions.hasPermission('addresses')" routerLink="/addresses" routerLinkActive="active" class="nav-link">{{ 'navbar.addresses' | translate }}</a>
            <a *ngIf="auth.hasAnyRole(customerRoles) && menuPermissions.hasPermission('checkout')" routerLink="/checkout" routerLinkActive="active" class="nav-link">{{ 'navbar.checkout' | translate }}</a>
            <a *ngIf="auth.hasAnyRole(riderRoles) && menuPermissions.hasPermission('rider-dashboard')" routerLink="/rider" routerLinkActive="active" class="nav-link">{{ 'navbar.riderDashboard' | translate }}</a>
            <a *ngIf="showProductManagement()" routerLink="/product-management" routerLinkActive="active" class="nav-link">{{ 'navbar.productManagement' | translate }}</a>
            <a *ngIf="showUserManagement()" routerLink="/user-management" routerLinkActive="active" class="nav-link">{{ 'navbar.userManagement' | translate }}</a>
          </ng-container>
        </div>

        <div class="navbar-actions">
          <!-- Mobile Menu Toggle -->
          <button class="mobile-menu-toggle" (click)="toggleMobileMenu()">
            <span>☰</span>
          </button>

          <button *ngIf="showOrderInboxButton()"
            type="button"
            class="order-inbox-btn"
            (click)="toggleOrderDropdown()"
            [attr.aria-label]="orderAlertLabel()">
            <span class="order-inbox-icon">🔔</span>
            <span class="cart-badge" *ngIf="orderAlert.notification$ | async as notification">
              {{ notification.count > 99 ? '99+' : notification.count }}
            </span>
          </button>

          <div *ngIf="orderDropdownOpen && showOrderInboxButton()" class="order-dropdown">
            <div class="order-dropdown-header">
              <strong>{{ orderDropdownTitle() }}</strong>
              <span *ngIf="orderAlert.notification$ | async as notification" class="order-dropdown-count">
                {{ notification.count }} {{ 'navbar.waiting' | translate }}
              </span>
            </div>
            <p class="order-dropdown-copy">{{ orderDropdownMessage() }}</p>
            <a class="order-dropdown-link" [routerLink]="orderAlertTarget()" (click)="closeOrderDropdown()">
              {{ orderDropdownActionLabel() }}
            </a>
          </div>

          <div class="lang-switch">
            <button type="button" class="lang-btn" [class.active]="currentLanguage === 'en'" (click)="setLanguage('en')">{{ 'lang.en' | translate }}</button>
            <button type="button" class="lang-btn" [class.active]="currentLanguage === 'de'" (click)="setLanguage('de')">{{ 'lang.de' | translate }}</button>
          </div>

          <button *ngIf="showCartButton()" class="cart-btn" (click)="cartService.toggleCart()">
            <span class="cart-icon">🛒</span>
            <span class="cart-badge" *ngIf="(cartService.cart$ | async)?.itemCount as count">
              {{ count > 99 ? '99+' : count }}
            </span>
          </button>

          <ng-container *ngIf="auth.isLoggedIn$ | async; else loginBtn">
            <div class="user-menu">
              <span class="user-name">{{ (auth.currentUser$ | async)?.firstName }}</span>
              <button class="btn-logout" (click)="auth.logout()">{{ 'navbar.logout' | translate }}</button>
            </div>
          </ng-container>
          <ng-template #loginBtn>
            <a routerLink="/login" class="btn-login">{{ 'navbar.login' | translate }}</a>
          </ng-template>
        </div>
      </div>

      <!-- Mobile Navigation Menu -->
      <div class="mobile-menu" [class.open]="mobileMenuOpen">
        <a routerLink="/products" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.products' | translate }}</a>
        <ng-container *ngIf="auth.isLoggedIn$ | async">
          <a *ngIf="showOrders()" routerLink="/orders" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.orders' | translate }}</a>
          <a *ngIf="showHistoryStats()" routerLink="/history-stats" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.historyStats' | translate }}</a>
          <a *ngIf="auth.hasAnyRole(customerRoles) && menuPermissions.hasPermission('addresses')" routerLink="/addresses" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.addresses' | translate }}</a>
          <a *ngIf="auth.hasAnyRole(customerRoles) && menuPermissions.hasPermission('checkout')" routerLink="/checkout" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.checkout' | translate }}</a>
          <a *ngIf="auth.hasAnyRole(riderRoles) && menuPermissions.hasPermission('rider-dashboard')" routerLink="/rider" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.riderDashboard' | translate }}</a>
          <a *ngIf="showProductManagement()" routerLink="/product-management" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.productManagement' | translate }}</a>
          <a *ngIf="showUserManagement()" routerLink="/user-management" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.userManagement' | translate }}</a>
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
      position: relative;
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
    .order-inbox-btn {
      position: relative;
      background: #fff;
      border: 2px solid #2E7D32;
      border-radius: 8px;
      padding: 0.4rem 0.75rem;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.25rem;
      transition: all 0.2s;
      font-size: 1.1rem;
      text-decoration: none;
      color: #2E7D32;
      min-width: 48px;
    }
    .cart-btn:hover { background: #E8F5E9; }
    .order-inbox-btn:hover { background: #E8F5E9; }
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
    .order-inbox-icon {
      line-height: 1;
    }
    .order-dropdown {
      position: absolute;
      top: calc(100% + 0.75rem);
      right: 5.25rem;
      width: min(320px, 78vw);
      padding: 1rem;
      border-radius: 16px;
      background: #fff;
      border: 1px solid #dce6d8;
      box-shadow: 0 18px 38px rgba(24, 54, 44, 0.16);
      z-index: 1005;
    }
    .order-dropdown-header {
      display: flex;
      justify-content: space-between;
      gap: 0.75rem;
      align-items: center;
      margin-bottom: 0.6rem;
      color: #174d2b;
    }
    .order-dropdown-count {
      background: #eef7ed;
      color: #1f6a35;
      border-radius: 999px;
      padding: 0.3rem 0.6rem;
      font-size: 0.78rem;
      font-weight: 700;
      white-space: nowrap;
    }
    .order-dropdown-copy {
      margin: 0 0 0.9rem;
      color: #566b5d;
      font-size: 0.9rem;
      line-height: 1.5;
    }
    .order-dropdown-link {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-height: 40px;
      padding: 0.6rem 0.9rem;
      border-radius: 999px;
      background: #174d2b;
      color: #fff;
      text-decoration: none;
      font-weight: 700;
    }
    .lang-switch {
      display: flex;
      gap: 0.25rem;
      border: 1px solid #c8d8c8;
      border-radius: 8px;
      padding: 0.12rem;
      background: #f7fbf7;
    }
    .lang-btn {
      border: none;
      background: transparent;
      color: #3f4f3f;
      border-radius: 6px;
      min-width: 34px;
      height: 28px;
      font-weight: 700;
      font-size: 0.75rem;
      cursor: pointer;
    }
    .lang-btn.active {
      background: #2E7D32;
      color: #fff;
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
      .order-dropdown {
        right: 0;
      }
    }

    @media (max-width: 600px) {
      .brand-name { font-size: 1.2rem; }
    }
  `]
})
export class NavbarComponent {
  auth = inject(AuthService);
  cartService = inject(CartService);
  menuPermissions = inject(MenuPermissionService);
  orderAlert = inject(OrderAlertService);
  languageService = inject(LanguageService);
  translate = inject(TranslateService);
  mobileMenuOpen = false;
  orderDropdownOpen = false;
  currentLanguage = 'en';
  customerRoles: AppRole[] = CUSTOMER_ROLES;
  riderRoles: AppRole[] = RIDER_ROLES;

  constructor() {
    this.currentLanguage = this.languageService.getCurrentLanguage();
    this.auth.currentUser$.subscribe(async user => {
      if (user) {
        await this.menuPermissions.ensureLoaded();
      }
    });

    this.languageService.currentLanguage$.subscribe(lang => {
      this.currentLanguage = lang;
    });
  }

  async setLanguage(language: string): Promise<void> {
    await this.languageService.setLanguage(language);
  }

  toggleMobileMenu() {
    this.mobileMenuOpen = !this.mobileMenuOpen;
  }

  toggleOrderDropdown() {
    this.orderDropdownOpen = !this.orderDropdownOpen;
  }

  closeMobileMenu() {
    this.mobileMenuOpen = false;
  }

  closeOrderDropdown() {
    this.orderDropdownOpen = false;
  }

  showCartButton(): boolean {
    return this.auth.hasAnyRole(this.customerRoles);
  }

  showOrderInboxButton(): boolean {
    return this.auth.hasRole('Rider') || this.auth.hasRole('Admin') || this.auth.hasRole('SuperAdmin');
  }

  orderAlertTarget(): string {
    if (this.auth.hasRole('Rider')) {
      return '/rider';
    }

    return this.orderAlert.currentNotification?.route ?? '/user-management/order-summary';
  }

  orderDropdownTitle(): string {
    return this.auth.hasRole('Rider')
      ? this.translate.instant('navbar.riderQueue')
      : this.translate.instant('navbar.orderAlerts');
  }

  orderDropdownMessage(): string {
    return this.orderAlert.currentNotification?.message
      ?? (this.auth.hasRole('Rider')
        ? this.translate.instant('navbar.riderMessage')
        : this.translate.instant('navbar.adminMessage'));
  }

  orderDropdownActionLabel(): string {
    return this.auth.hasRole('Rider')
      ? this.translate.instant('navbar.openRiderDashboard')
      : this.translate.instant('navbar.openOrderHistory');
  }

  orderAlertLabel(): string {
    return this.orderAlert.currentNotification?.message ?? this.translate.instant('navbar.openOrderNotifications');
  }

  showOrders(): boolean {
    return (this.auth.hasRole('Customer') || this.auth.hasRole('Rider')) && this.menuPermissions.hasPermission('orders');
  }

  showHistoryStats(): boolean {
    return this.auth.hasRole('Customer') || this.auth.hasRole('Admin') || this.auth.hasRole('SuperAdmin');
  }

  showUserManagement(): boolean {
    return this.menuPermissions.hasAnyPermission(USER_MANAGEMENT_MENU_KEYS);
  }

  showProductManagement(): boolean {
    return (this.auth.hasRole('Admin') || this.auth.hasRole('SuperAdmin')) && this.menuPermissions.hasPermission('product-management');
  }
}
