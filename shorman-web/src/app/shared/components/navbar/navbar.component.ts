import { Component, HostListener, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AsyncPipe, NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
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
  imports: [RouterLink, RouterLinkActive, AsyncPipe, NgFor, NgIf, FormsModule, TranslatePipe],
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
        <div class="navbar-links-shell">
          <div class="desktop-menu-panel">
            <button
              type="button"
              class="desktop-menu-trigger"
              [class.open]="desktopMenuOpen"
              (click)="toggleDesktopMenu()"
              [attr.aria-expanded]="desktopMenuOpen"
              aria-label="Open navigation menu">
              <span class="app-icon">menu</span>
              <span>Menu</span>
              <span class="desktop-menu-count">{{ desktopNavItems.length }}</span>
            </button>

            <div *ngIf="desktopMenuOpen" class="desktop-menu-dropdown">
              <a
                *ngFor="let item of desktopNavItems"
                [routerLink]="item.path"
                routerLinkActive="active"
                class="desktop-menu-link"
                (click)="closeDesktopMenu()">
                {{ item.labelKey | translate }}
              </a>
            </div>
          </div>
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

          <div *ngIf="auth.isLoggedIn$ | async" class="mobile-toolbar-user">
            <span class="mobile-toolbar-greeting">Hi {{ currentUserLabel() }}</span>
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
            <div class="user-menu desktop-user-menu profile-editor">
              <span class="user-name">Hi {{ currentUserLabel() }}</span>
              <button type="button" class="profile-action-btn" (click)="toggleDisplayNameEditor()" title="Set display name">
                <span class="app-icon">edit</span>
                <span>Display name</span>
              </button>
              <form *ngIf="displayNameEditorOpen" class="display-name-popover" (ngSubmit)="saveDisplayName()">
                <label class="display-name-label" for="navbar-display-name">Display name</label>
                <input
                  id="navbar-display-name"
                  type="text"
                  class="display-name-input"
                  [(ngModel)]="displayNameDraft"
                  name="displayName"
                  maxlength="80"
                  autocomplete="nickname"
                  placeholder="Enter display name" />
                <p *ngIf="displayNameError" class="display-name-error">{{ displayNameError }}</p>
                <div class="display-name-actions">
                  <button type="button" class="display-name-secondary" (click)="closeDisplayNameEditor()">Cancel</button>
                  <button type="submit" class="display-name-primary" [disabled]="displayNameSaving">
                    {{ displayNameSaving ? 'Saving...' : 'Save' }}
                  </button>
                </div>
              </form>
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
        <div *ngIf="auth.isLoggedIn$ | async" class="mobile-account-card">
          <p class="mobile-account-kicker">Signed in as</p>
          <p class="mobile-account-name">{{ currentUserLabel() }}</p>
          <button type="button" class="mobile-account-action" (click)="toggleDisplayNameEditorFromMobileMenu()">Set display name</button>
          <div class="mobile-theme-panel">
            <span class="mobile-theme-title">Theme</span>
            <div class="mobile-theme-options">
              <button
                *ngFor="let theme of themeOptions"
                type="button"
                class="mobile-theme-option"
                [class.active]="theme.id === currentThemeId"
                (click)="selectTheme(theme.id)">
                <span class="theme-swatch" [attr.data-theme]="theme.id"></span>
                <span>{{ theme.label }}</span>
              </button>
            </div>
          </div>
        </div>
        <a *ngIf="showProducts()" routerLink="/products" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.products' | translate }}</a>
        <a *ngIf="showHistoryStats()" routerLink="/history-stats" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.historyStats' | translate }}</a>
        <ng-container *ngIf="auth.isLoggedIn$ | async">
          <a *ngIf="showOrders()" routerLink="/orders" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ ordersNavLabelKey() | translate }}</a>
          <a *ngIf="auth.hasAnyRole(customerRoles) && menuPermissions.hasPermission('addresses')" routerLink="/addresses" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.addresses' | translate }}</a>
          <a *ngIf="auth.hasAnyRole(customerRoles) && menuPermissions.hasPermission('checkout')" routerLink="/checkout" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.checkout' | translate }}</a>
          <a *ngIf="auth.hasAnyRole(riderRoles) && menuPermissions.hasPermission('rider-dashboard')" routerLink="/rider" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.riderDashboard' | translate }}</a>
          <a *ngIf="showProductManagement()" routerLink="/product-management" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.productManagement' | translate }}</a>
          <a *ngIf="showUserManagement()" routerLink="/user-management" routerLinkActive="active" class="mobile-nav-link" (click)="closeMobileMenu()">{{ 'navbar.userManagement' | translate }}</a>
          <form *ngIf="displayNameEditorOpen" class="mobile-display-name-form" (ngSubmit)="saveDisplayName()">
            <input
              type="text"
              class="mobile-display-name-input"
              [(ngModel)]="displayNameDraft"
              name="mobileDisplayName"
              maxlength="80"
              autocomplete="nickname"
              placeholder="Enter display name" />
            <p *ngIf="displayNameError" class="display-name-error mobile-display-name-error">{{ displayNameError }}</p>
            <div class="mobile-display-name-actions">
              <button type="button" class="mobile-display-name-btn secondary" (click)="closeDisplayNameEditor()">Cancel</button>
              <button type="submit" class="mobile-display-name-btn primary" [disabled]="displayNameSaving">
                {{ displayNameSaving ? 'Saving...' : 'Save' }}
              </button>
            </div>
          </form>
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
      display: grid;
      grid-template-columns: auto minmax(0, 1fr) auto;
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
    .navbar-links-shell {
      min-width: 0;
      display: flex;
      justify-content: center;
    }
    .desktop-menu-panel {
      position: relative;
    }
    .desktop-menu-trigger {
      border: 1px solid color-mix(in srgb, var(--color-primary) 22%, var(--color-border));
      background: color-mix(in srgb, var(--color-primary-soft) 58%, white);
      color: var(--color-primary-strong);
      border-radius: 999px;
      min-height: 42px;
      padding: 0.45rem 0.9rem;
      font: inherit;
      font-weight: 800;
      display: inline-flex;
      align-items: center;
      gap: 0.55rem;
      cursor: pointer;
      box-shadow: var(--shadow-soft);
    }
    .desktop-menu-trigger.open {
      border-color: var(--color-primary);
      background: color-mix(in srgb, var(--color-primary-soft) 75%, white);
    }
    .desktop-menu-count {
      min-width: 1.45rem;
      height: 1.45rem;
      padding: 0 0.35rem;
      border-radius: 999px;
      background: var(--color-primary);
      color: #fff;
      font-size: 0.72rem;
      display: inline-flex;
      align-items: center;
      justify-content: center;
    }
    .desktop-menu-dropdown {
      position: absolute;
      top: calc(100% + 0.55rem);
      left: 50%;
      transform: translateX(-50%);
      width: min(720px, calc(100vw - 9rem));
      padding: 0.7rem;
      border-radius: 20px;
      border: 1px solid var(--color-border);
      background: color-mix(in srgb, var(--color-surface) 95%, white);
      box-shadow: var(--shadow-medium);
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
      gap: 0.55rem;
      z-index: 1004;
    }
    .desktop-menu-link {
      text-decoration: none;
      color: var(--color-text-muted);
      padding: 0.8rem 0.9rem;
      border-radius: 14px;
      font-weight: 700;
      font-size: 0.92rem;
      transition: all 0.2s;
      font-family: var(--font-ui);
      background: var(--color-surface-alt);
      text-align: center;
    }
    .desktop-menu-link:hover,
    .desktop-menu-link.active {
      color: var(--color-primary-strong);
      background: var(--color-primary-soft);
    }
    .navbar-actions {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      flex-shrink: 0;
      position: relative;
      justify-self: end;
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
    .mobile-toolbar-user {
      display: none;
      align-items: center;
      color: var(--color-primary-strong);
      max-width: 10rem;
    }
    .mobile-toolbar-greeting {
      display: block;
      font-size: 0.88rem;
      font-weight: 900;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
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
    .profile-action-btn {
      border: 1px solid var(--color-border);
      background: var(--color-surface-alt);
      color: var(--color-primary);
      border-radius: 999px;
      padding: 0.35rem 0.75rem;
      cursor: pointer;
      font-size: 0.82rem;
      font-weight: 700;
      font-family: var(--font-ui);
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      transition: all 0.2s ease;
    }
    .profile-action-btn:hover {
      background: var(--color-primary-soft);
      border-color: var(--color-primary);
    }
    .profile-editor { position: relative; }
    .display-name-popover {
      position: absolute;
      top: calc(100% + 0.5rem);
      right: 3.8rem;
      width: min(18rem, 72vw);
      padding: 0.8rem;
      border: 1px solid var(--color-border);
      border-radius: 16px;
      background: var(--color-surface);
      box-shadow: var(--shadow-medium);
      display: grid;
      gap: 0.55rem;
      z-index: 1007;
    }
    .display-name-label {
      font-size: 0.76rem;
      font-weight: 700;
      color: var(--color-text-muted);
      font-family: var(--font-ui);
    }
    .display-name-input,
    .mobile-display-name-input {
      width: 100%;
      border: 1px solid var(--color-border);
      border-radius: 12px;
      padding: 0.65rem 0.8rem;
      font: inherit;
      color: var(--color-text);
      background: color-mix(in srgb, var(--color-surface-alt) 88%, white);
      box-sizing: border-box;
    }
    .display-name-actions,
    .mobile-display-name-actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.45rem;
    }
    .display-name-primary,
    .display-name-secondary,
    .mobile-display-name-btn {
      border-radius: 999px;
      padding: 0.45rem 0.85rem;
      font: inherit;
      font-weight: 700;
      cursor: pointer;
    }
    .display-name-primary,
    .mobile-display-name-btn.primary {
      border: 1px solid var(--color-primary);
      background: var(--color-primary);
      color: #fff;
    }
    .display-name-secondary,
    .mobile-display-name-btn.secondary {
      border: 1px solid var(--color-border);
      background: transparent;
      color: var(--color-text-muted);
    }
    .display-name-error {
      margin: 0;
      color: var(--color-danger);
      font-size: 0.76rem;
      font-weight: 600;
    }
    .mobile-display-name-form {
      padding: 0.75rem 1.25rem 0;
      display: grid;
      gap: 0.55rem;
    }
    .mobile-account-card {
      margin: 0 1rem 0.8rem;
      padding: 1rem;
      border: 1px solid var(--color-border);
      border-radius: 16px;
      background: color-mix(in srgb, var(--color-primary-soft) 58%, white);
      display: grid;
      gap: 0.65rem;
    }
    .mobile-account-kicker {
      margin: 0;
      font-size: 0.72rem;
      font-weight: 800;
      text-transform: uppercase;
      letter-spacing: 0.08em;
      color: var(--color-text-muted);
    }
    .mobile-account-name {
      margin: 0;
      font-size: 1.05rem;
      font-weight: 800;
      color: var(--color-primary-strong);
    }
    .mobile-account-action {
      border: 1px solid var(--color-primary);
      background: var(--color-surface);
      color: var(--color-primary);
      border-radius: 999px;
      padding: 0.6rem 0.9rem;
      font: inherit;
      font-weight: 700;
      cursor: pointer;
      justify-self: start;
    }
    .mobile-theme-panel {
      display: grid;
      gap: 0.45rem;
    }
    .mobile-theme-title {
      font-size: 0.78rem;
      font-weight: 700;
      color: var(--color-text-muted);
    }
    .mobile-theme-options {
      display: flex;
      flex-wrap: wrap;
      gap: 0.45rem;
    }
    .mobile-theme-option {
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-text);
      border-radius: 999px;
      padding: 0.45rem 0.7rem;
      font: inherit;
      font-size: 0.82rem;
      font-weight: 700;
      display: inline-flex;
      align-items: center;
      gap: 0.45rem;
      cursor: pointer;
    }
    .mobile-theme-option.active {
      border-color: var(--color-primary);
      background: var(--color-primary-soft);
      color: var(--color-primary-strong);
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
      left: 50%;
      width: min(420px, calc(100vw - 1.25rem));
      background: var(--color-surface);
      box-shadow: var(--shadow-medium);
      border-radius: 0 0 18px 18px;
      z-index: 999;
      transform: translateX(-50%) translateY(-0.4rem);
      transition: transform 0.25s ease, opacity 0.25s ease;
      padding: 0.85rem 0;
      max-height: calc(100vh - var(--navbar-height) - 1rem);
      overflow-y: auto;
      opacity: 0;
      pointer-events: none;
    }

    .mobile-menu.open {
      transform: translateX(-50%) translateY(0);
      opacity: 1;
      pointer-events: auto;
    }

    .mobile-nav-link {
      display: block;
      text-decoration: none;
      color: var(--color-text-muted);
      padding: 0.82rem 1.25rem;
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
      .navbar-container {
        grid-template-columns: auto 1fr auto;
        gap: 0.65rem;
        padding: 0 0.75rem;
      }
      .navbar-links-shell { display: none; }
      .mobile-menu-toggle { display: block; }
      .mobile-menu { display: block; }
      .mobile-backdrop { display: block; }
      .user-name { display: none; }
      .desktop-user-menu { display: none; }
      .brand-sub { display: none; }
      .theme-switch { display: none; }
      .mobile-toolbar-user { display: inline-flex; }
      .navbar-actions { gap: 0.5rem; }
      .btn-login { padding: 0.35rem 0.75rem; font-size: 0.85rem; }
      .order-dropdown {
        right: auto;
        left: 50%;
        transform: translateX(-50%);
      }
    }

    @media (max-width: 600px) {
      .brand-name { font-size: 1.2rem; }
      .mobile-toolbar-user {
        max-width: 7.5rem;
      }
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
  desktopMenuOpen = false;
  themeDropdownOpen = false;
  displayNameEditorOpen = false;
  displayNameSaving = false;
  displayNameDraft = '';
  displayNameError = '';
  currentLanguage = 'en';
  currentThemeId = this.themeService.getCurrentThemeId();
  themeOptions: ThemeOption[] = this.themeService.themeOptions;
  customerRoles: AppRole[] = CUSTOMER_ROLES;
  riderRoles: AppRole[] = RIDER_ROLES;
  desktopNavItems: Array<{ path: string; labelKey: string }> = [];

  constructor() {
    this.currentLanguage = this.languageService.getCurrentLanguage();
    this.auth.currentUser$.subscribe(async user => {
      if (user) {
        await this.menuPermissions.ensureLoaded();
      }

      this.refreshDesktopNavItems();
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

  toggleDesktopMenu(): void {
    this.desktopMenuOpen = !this.desktopMenuOpen;
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement | null;
    if (!target) {
      this.closeOrderDropdown();
      this.closeDesktopMenu();
      this.themeDropdownOpen = false;
      this.closeDisplayNameEditor();
      return;
    }

    if (this.orderDropdownOpen && !target.closest('.order-dropdown') && !target.closest('.order-inbox-btn')) {
      this.closeOrderDropdown();
    }

    if (this.desktopMenuOpen && !target.closest('.desktop-menu-panel')) {
      this.closeDesktopMenu();
    }

    if (this.themeDropdownOpen && !target.closest('.theme-switch')) {
      this.themeDropdownOpen = false;
    }

    if (this.displayNameEditorOpen && !target.closest('.profile-editor') && !target.closest('.mobile-display-name-form')) {
      this.closeDisplayNameEditor();
    }
  }

  closeMobileMenu() {
    this.mobileMenuOpen = false;
  }

  closeOrderDropdown() {
    this.orderDropdownOpen = false;
  }

  closeDesktopMenu(): void {
    this.desktopMenuOpen = false;
  }

  showCartButton(): boolean {
    return true;
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
    return !this.auth.isLoggedIn || this.menuPermissions.hasPermission('history-stats');
  }

  refreshDesktopNavItems(): void {
    const items: Array<{ path: string; labelKey: string }> = [];

    if (this.showProducts()) {
      items.push({ path: '/products', labelKey: 'navbar.products' });
    }

    if (this.showHistoryStats()) {
      items.push({ path: '/history-stats', labelKey: 'navbar.historyStats' });
    }

    if (this.auth.isLoggedIn) {
      if (this.showOrders()) {
        items.push({ path: '/orders', labelKey: this.ordersNavLabelKey() });
      }

      if (this.auth.hasAnyRole(this.customerRoles) && this.menuPermissions.hasPermission('addresses')) {
        items.push({ path: '/addresses', labelKey: 'navbar.addresses' });
      }

      if (this.auth.hasAnyRole(this.customerRoles) && this.menuPermissions.hasPermission('checkout')) {
        items.push({ path: '/checkout', labelKey: 'navbar.checkout' });
      }

      if (this.auth.hasAnyRole(this.riderRoles) && this.menuPermissions.hasPermission('rider-dashboard')) {
        items.push({ path: '/rider', labelKey: 'navbar.riderDashboard' });
      }

      if (this.showProductManagement()) {
        items.push({ path: '/product-management', labelKey: 'navbar.productManagement' });
      }

      if (this.showUserManagement()) {
        items.push({ path: '/user-management', labelKey: 'navbar.userManagement' });
      }
    }

    this.desktopNavItems = items;
  }

  currentUserLabel(): string {
    return this.auth.getPreferredUserName(this.auth.currentUser, 'Account');
  }

  toggleDisplayNameEditor(): void {
    const user = this.auth.currentUser;
    if (!user) {
      return;
    }

    this.displayNameEditorOpen = !this.displayNameEditorOpen;
    this.displayNameError = '';
    this.displayNameDraft = user.displayName?.trim() || user.firstName || '';
  }

  closeDisplayNameEditor(): void {
    this.displayNameEditorOpen = false;
    this.displayNameError = '';
    this.displayNameDraft = '';
    this.displayNameSaving = false;
  }

  saveDisplayName(): void {
    const user = this.auth.currentUser;
    if (!user || this.displayNameSaving) {
      return;
    }

    const nextDisplayName = this.displayNameDraft.trim();
    if (nextDisplayName.length > 80) {
      this.displayNameError = 'Display name must be 80 characters or fewer.';
      return;
    }

    this.displayNameSaving = true;
    this.displayNameError = '';

    this.auth.updateProfile({ displayName: nextDisplayName }).subscribe({
      next: () => {
        this.closeDisplayNameEditor();
      },
      error: error => {
        this.displayNameSaving = false;
        console.error('Failed to update display name.', error);
        this.displayNameError = 'Display name could not be updated right now.';
      }
    });
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

  toggleDisplayNameEditorFromMobileMenu(): void {
    this.displayNameEditorOpen = !this.displayNameEditorOpen;
    this.displayNameError = '';
    this.displayNameDraft = this.auth.currentUser?.displayName?.trim() || this.auth.currentUser?.firstName || '';
  }
}
