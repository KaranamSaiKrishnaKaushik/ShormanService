import { Component, HostListener, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AsyncPipe, NgFor, NgIf } from '@angular/common';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService } from '../../../core/services/auth.service';
import { CartService } from '../../../core/services/cart.service';
import { AppRole, CUSTOMER_ROLES, RIDER_ROLES } from '../../../core/models/user.model';
import { MenuPermissionService } from '../../../core/services/menu-permission.service';
import { USER_MANAGEMENT_MENU_KEYS } from '../../../core/models/menu-permission.model';
import { OrderAlertService } from '../../../core/services/order-alert.service';
import { LanguageService } from '../../../core/services/language.service';
import { ThemeOption, ThemeService } from '../../../core/services/theme.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, AsyncPipe, NgFor, NgIf, TranslatePipe],
  template: `
    <ng-container *ngIf="orderAlert.notification$ | async as notification">
    </ng-container>
    <nav class="navbar">
      <div class="navbar-container">
        <a routerLink="/products" class="navbar-brand">
          <span class="app-icon brand-icon">shopping_bag</span>
          <span class="brand-name">Shorman</span>
          <span class="brand-sub">Service</span>
        </a>

        <!-- Desktop Navigation -->
        <div class="navbar-links">
          <a *ngIf="showProducts()" routerLink="/products" routerLinkActive="active" class="nav-link">{{ 'navbar.products' | translate }}</a>
          <ng-container *ngIf="auth.isLoggedIn$ | async">
            <a *ngIf="showOrders()" routerLink="/orders" routerLinkActive="active" class="nav-link">{{ ordersNavLabelKey() | translate }}</a>
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
            <span class="app-icon">menu</span>
          </button>

          <button *ngIf="showOrderInboxButton()"
            type="button"
            class="order-inbox-btn"
            (click)="toggleOrderDropdown()"
            [attr.aria-label]="orderAlertLabel()">
            <span class="app-icon order-inbox-icon">notifications</span>
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

          <div class="theme-switch">
            <span class="app-icon theme-icon">palette</span>
            <button
              type="button"
              class="theme-trigger"
              [class.open]="themeDropdownOpen"
              (click)="toggleThemeDropdown()"
              aria-label="Theme picker"
              [attr.aria-expanded]="themeDropdownOpen">
              <span class="theme-label">{{ currentThemeLabel() }}</span>
              <span class="app-icon theme-chevron">expand_more</span>
            </button>
            <div *ngIf="themeDropdownOpen" class="theme-dropdown">
              <button
                *ngFor="let theme of themeOptions"
                type="button"
                class="theme-option"
                [class.active]="theme.id === currentThemeId"
                (click)="selectTheme(theme.id)">
                <span class="theme-swatch" [attr.data-theme]="theme.id"></span>
                <span>{{ theme.label }}</span>
              </button>
            </div>
          </div>

          <button *ngIf="showCartButton()" class="cart-btn" (click)="cartService.toggleCart()">
            <span class="app-icon cart-icon">shopping_cart</span>
            <span class="cart-badge" *ngIf="(cartService.cart$ | async)?.itemCount as count">
              {{ count > 99 ? '99+' : count }}
            </span>
          </button>

          <ng-container *ngIf="auth.isLoggedIn$ | async; else loginBtn">
            <div class="user-menu desktop-user-menu">
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
        <a *ngIf="showProducts()" routerLink="/products" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.products' | translate }}</a>
        <ng-container *ngIf="auth.isLoggedIn$ | async">
          <a *ngIf="showOrders()" routerLink="/orders" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ ordersNavLabelKey() | translate }}</a>
          <a *ngIf="showHistoryStats()" routerLink="/history-stats" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.historyStats' | translate }}</a>
          <a *ngIf="auth.hasAnyRole(customerRoles) && menuPermissions.hasPermission('addresses')" routerLink="/addresses" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.addresses' | translate }}</a>
          <a *ngIf="auth.hasAnyRole(customerRoles) && menuPermissions.hasPermission('checkout')" routerLink="/checkout" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.checkout' | translate }}</a>
          <a *ngIf="auth.hasAnyRole(riderRoles) && menuPermissions.hasPermission('rider-dashboard')" routerLink="/rider" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.riderDashboard' | translate }}</a>
          <a *ngIf="showProductManagement()" routerLink="/product-management" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.productManagement' | translate }}</a>
          <a *ngIf="showUserManagement()" routerLink="/user-management" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.userManagement' | translate }}</a>
          <button type="button" class="mobile-nav-link mobile-logout-btn" (click)="logoutFromMobileMenu()">{{ 'navbar.logout' | translate }}</button>
        </ng-container>
      </div>
    </nav>

    <!-- Mobile Menu Backdrop -->
    <div class="mobile-backdrop" [class.open]="mobileMenuOpen" (click)="closeMobileMenu()"></div>
  `,
  styles: [`
    .navbar {
      background: color-mix(in srgb, var(--color-surface) 92%, white);
      border-bottom: 1px solid var(--color-border);
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      z-index: 1000;
      box-shadow: var(--shadow-soft);
      backdrop-filter: blur(18px);
    }
    .navbar-container {
      max-width: 1200px;
      margin: 0 auto;
      padding: 0 1rem;
      display: flex;
      align-items: center;
      height: var(--navbar-height);
      gap: 1.5rem;
    }
    .navbar-brand {
      text-decoration: none;
      display: flex;
      align-items: center;
      gap: 0.4rem;
      flex-shrink: 0;
    }
    .brand-icon {
      font-size: 1.5rem;
      color: var(--color-primary);
    }
    .brand-name {
      font-size: 1.4rem;
      font-weight: 800;
      color: var(--color-primary);
      font-family: var(--font-display);
      letter-spacing: -0.5px;
    }
    .brand-sub {
      font-size: 0.85rem;
      color: var(--color-accent);
      font-weight: 600;
      font-family: var(--font-ui);
    }
    .navbar-links {
      display: flex;
      gap: 0.25rem;
      flex: 1;
    }
    .nav-link {
      text-decoration: none;
      color: var(--color-text-muted);
      padding: 0.4rem 0.75rem;
      border-radius: var(--radius-sm);
      font-weight: 500;
      font-size: 0.95rem;
      transition: all 0.2s;
      font-family: var(--font-ui);
    }
    .nav-link:hover, .nav-link.active {
      color: var(--color-primary);
      background: var(--color-primary-soft);
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
      background: color-mix(in srgb, var(--color-primary-soft) 60%, white);
      border: 1px solid var(--color-border-strong);
      color: var(--color-primary);
      border-radius: var(--radius-sm);
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
      background: var(--color-surface);
      border: 1px solid var(--color-border-strong);
      border-radius: var(--radius-sm);
      padding: 0.4rem 0.75rem;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.25rem;
      transition: all 0.2s;
      font-size: 1.1rem;
      text-decoration: none;
      color: var(--color-primary);
      min-width: 48px;
    }
    .cart-btn:hover,
    .order-inbox-btn:hover {
      background: var(--color-primary-soft);
      border-color: var(--color-primary);
    }
    .cart-badge {
      position: absolute;
      top: -8px;
      right: -8px;
      background: var(--color-accent);
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
      font-family: var(--font-ui);
    }
    .order-inbox-icon {
      line-height: 1;
      font-size: 1.25rem;
    }
    .order-dropdown {
      position: absolute;
      top: calc(100% + 0.75rem);
      right: 5.25rem;
      width: min(320px, 78vw);
      padding: 1rem;
      border-radius: var(--radius-md);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      box-shadow: var(--shadow-medium);
      z-index: 1005;
    }
    .order-dropdown-header {
      display: flex;
      justify-content: space-between;
      gap: 0.75rem;
      align-items: center;
      margin-bottom: 0.6rem;
      color: var(--color-primary-strong);
    }
    .order-dropdown-count {
      background: var(--color-primary-soft);
      color: var(--color-primary);
      border-radius: 999px;
      padding: 0.3rem 0.6rem;
      font-size: 0.78rem;
      font-weight: 700;
      white-space: nowrap;
    }
    .order-dropdown-copy {
      margin: 0 0 0.9rem;
      color: var(--color-text-muted);
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
      background: var(--color-primary-strong);
      color: #fff;
      text-decoration: none;
      font-weight: 700;
      font-family: var(--font-ui);
    }
    .lang-switch {
      display: flex;
      gap: 0.25rem;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-sm);
      padding: 0.12rem;
      background: var(--color-surface-alt);
    }
    .theme-switch {
      display: inline-flex;
      align-items: center;
      gap: 0.45rem;
      min-width: 0;
      padding: 0.28rem 0.45rem;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-sm);
      background: var(--color-surface-alt);
      position: relative;
    }
    .theme-icon {
      color: var(--color-primary);
      font-size: 1rem;
      flex-shrink: 0;
    }
    .theme-trigger {
      border: none;
      background: transparent;
      color: var(--color-text);
      min-width: 8.75rem;
      padding: 0;
      outline: none;
      cursor: pointer;
      font-family: var(--font-ui);
      display: inline-flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
    }
    .theme-label {
      font-size: 0.8rem;
      font-weight: 700;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .theme-chevron {
      font-size: 1rem;
      color: var(--color-text-muted);
      transition: transform 0.2s ease;
    }
    .theme-trigger.open .theme-chevron {
      transform: rotate(180deg);
    }
    .theme-dropdown {
      position: absolute;
      top: calc(100% + 0.55rem);
      right: 0;
      min-width: 12.5rem;
      padding: 0.45rem;
      border-radius: 18px;
      border: 1px solid var(--color-border);
      background: color-mix(in srgb, var(--color-surface) 94%, white);
      box-shadow: var(--shadow-medium);
      display: grid;
      gap: 0.25rem;
      z-index: 1006;
    }
    .theme-option {
      border: none;
      background: transparent;
      width: 100%;
      padding: 0.65rem 0.75rem;
      border-radius: 14px;
      color: var(--color-text-muted);
      font-size: 0.82rem;
      font-weight: 700;
      cursor: pointer;
      font-family: var(--font-ui);
      display: inline-flex;
      align-items: center;
      gap: 0.65rem;
      text-align: left;
    }
    .theme-option:hover,
    .theme-option.active {
      background: var(--color-primary-soft);
      color: var(--color-primary);
    }
    .theme-swatch {
      width: 1rem;
      height: 1rem;
      border-radius: 999px;
      flex-shrink: 0;
      box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.65);
      background: linear-gradient(135deg, var(--color-primary), var(--color-accent));
    }
    .theme-swatch[data-theme='fresh-market'] {
      background: linear-gradient(135deg, #165b33, #f4b63a);
    }
    .theme-swatch[data-theme='terracotta'] {
      background: linear-gradient(135deg, #8f3f2a, #f0a15d);
    }
    .theme-swatch[data-theme='coastal'] {
      background: linear-gradient(135deg, #0c5f78, #5db7d7);
    }
    .lang-btn {
      border: none;
      background: transparent;
      color: var(--color-text-muted);
      border-radius: 8px;
      min-width: 34px;
      height: 28px;
      font-weight: 700;
      font-size: 0.75rem;
      cursor: pointer;
      font-family: var(--font-ui);
    }
    .lang-btn.active {
      background: var(--color-primary);
      color: #fff;
    }
    .user-menu {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .user-name {
      font-weight: 600;
      color: var(--color-primary);
      font-size: 0.9rem;
      font-family: var(--font-ui);
    }
    .btn-logout {
      background: none;
      border: 1px solid var(--color-border);
      padding: 0.35rem 0.75rem;
      border-radius: 8px;
      cursor: pointer;
      font-size: 0.85rem;
      color: var(--color-text-muted);
      transition: all 0.2s;
      font-family: var(--font-ui);
    }
    .btn-logout:hover {
      background: var(--color-danger-soft);
      border-color: var(--color-danger);
      color: var(--color-danger);
    }
    .btn-login {
      text-decoration: none;
      background: var(--color-primary);
      color: #fff;
      padding: 0.4rem 1rem;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.9rem;
      transition: background 0.2s;
      font-family: var(--font-ui);
    }
    .btn-login:hover { background: var(--color-primary-strong); }

    /* Mobile Menu Toggle Button */
    .mobile-menu-toggle {
      display: none;
      background: none;
      border: none;
      color: var(--color-primary);
      cursor: pointer;
      padding: 0.25rem;
      line-height: 1;
    }

    .mobile-menu-toggle .app-icon {
      font-size: 1.6rem;
    }

    /* Mobile Menu */
    .mobile-menu {
      display: none;
      position: fixed;
      top: var(--navbar-height);
      right: 0;
      width: 250px;
      max-width: 80vw;
      background: var(--color-surface);
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
      color: var(--color-text-muted);
      padding: 0.875rem 1.25rem;
      font-weight: 500;
      font-size: 1rem;
      transition: all 0.2s;
      border-left: 3px solid transparent;
      font-family: var(--font-ui);
    }

    .mobile-logout-btn {
      width: 100%;
      text-align: left;
      background: transparent;
      border: none;
      cursor: pointer;
    }

    .mobile-nav-link:hover,
    .mobile-nav-link.active {
      color: var(--color-primary);
      background: var(--color-primary-soft);
      border-left-color: var(--color-primary);
    }

    /* Mobile Backdrop */
    .mobile-backdrop {
      display: none;
      position: fixed;
      top: var(--navbar-height);
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
      .desktop-user-menu { display: none; }
      .brand-sub { display: none; }
      .theme-switch { display: none; }
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
  themeService = inject(ThemeService);
  mobileMenuOpen = false;
  orderDropdownOpen = false;
  themeDropdownOpen = false;
  currentLanguage = 'en';
  currentThemeId = this.themeService.getCurrentThemeId();
  themeOptions: ThemeOption[] = this.themeService.themeOptions;
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

    this.themeService.themeId$.subscribe(themeId => {
      this.currentThemeId = themeId;
    });
  }

  async setLanguage(language: string): Promise<void> {
    await this.languageService.setLanguage(language);
  }

  setTheme(themeId: string): void {
    this.themeService.setTheme(themeId as ThemeOption['id']);
  }

  selectTheme(themeId: ThemeOption['id']): void {
    this.setTheme(themeId);
    this.themeDropdownOpen = false;
  }

  toggleThemeDropdown(): void {
    this.themeDropdownOpen = !this.themeDropdownOpen;
  }

  currentThemeLabel(): string {
    return this.themeOptions.find(theme => theme.id === this.currentThemeId)?.label ?? 'Theme';
  }

  toggleMobileMenu() {
    this.mobileMenuOpen = !this.mobileMenuOpen;
  }

  toggleOrderDropdown() {
    this.orderDropdownOpen = !this.orderDropdownOpen;
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement | null;
    if (!target) {
      this.closeOrderDropdown();
      this.themeDropdownOpen = false;
      return;
    }

    if (this.orderDropdownOpen && !target.closest('.order-dropdown') && !target.closest('.order-inbox-btn')) {
      this.closeOrderDropdown();
    }

    if (this.themeDropdownOpen && !target.closest('.theme-switch')) {
      this.themeDropdownOpen = false;
    }
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

  showProducts(): boolean {
    return !this.auth.isLoggedIn || this.menuPermissions.hasPermission('products');
  }

  showOrderInboxButton(): boolean {
    return this.auth.hasRole('Rider') || this.auth.hasRole('Admin') || this.auth.hasRole('SuperAdmin');
  }

  orderAlertTarget(): string {
    if (this.prefersRiderInbox()) {
      return '/rider';
    }

    return this.orderAlert.currentNotification?.route ?? '/user-management/order-summary';
  }

  orderDropdownTitle(): string {
    return this.prefersRiderInbox()
      ? this.translate.instant('navbar.riderQueue')
      : this.translate.instant('navbar.orderAlerts');
  }

  orderDropdownMessage(): string {
    return this.orderAlert.currentNotification?.message
      ?? (this.prefersRiderInbox()
        ? this.translate.instant('navbar.riderMessage')
        : this.translate.instant('navbar.adminMessage'));
  }

  orderDropdownActionLabel(): string {
    return this.orderAlertTarget() === '/rider'
      ? this.translate.instant('navbar.openRiderDashboard')
      : this.translate.instant('navbar.openOrderHistory');
  }

  orderAlertLabel(): string {
    return this.orderAlert.currentNotification?.message ?? this.translate.instant('navbar.openOrderNotifications');
  }

  showOrders(): boolean {
    return this.auth.isLoggedIn && this.menuPermissions.hasPermission('orders');
  }

  showHistoryStats(): boolean {
    return this.auth.isLoggedIn && this.menuPermissions.hasPermission('history-stats');
  }

  ordersNavLabelKey(): string {
    return this.auth.hasRole('Rider')
      ? 'navbar.orders'
      : 'navbar.orderHistory';
  }

  showUserManagement(): boolean {
    return this.menuPermissions.hasAnyPermission(USER_MANAGEMENT_MENU_KEYS);
  }

  showProductManagement(): boolean {
    return (this.auth.hasRole('Admin') || this.auth.hasRole('SuperAdmin')) && this.menuPermissions.hasPermission('product-management');
  }

  private prefersRiderInbox(): boolean {
    return this.auth.hasAnyRole(this.riderRoles) && this.menuPermissions.hasPermission('rider-dashboard');
  }

  logoutFromMobileMenu(): void {
    this.closeMobileMenu();
    this.auth.logout();
  }
}
