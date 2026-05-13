import { ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { ActivatedRoute, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../core/services/auth.service';
import { MenuPermissionService } from '../../core/services/menu-permission.service';
import { MenuPermissionKey } from '../../core/models/menu-permission.model';
import { AppRole } from '../../core/models/user.model';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [NgFor, NgIf, RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <section class="page-shell">
      <header class="page-hero">
        <div>
          <span class="eyebrow">Management Workspace</span>
          <h1>User Management</h1>
          <p>Use the submenus to open the management views enabled for your account.</p>
        </div>
      </header>

      <section *ngIf="loading" class="state-card">
        <h2>Loading management views</h2>
        <p>Checking your available user-management access.</p>
      </section>

      <section *ngIf="!loading && tabs.length > 0" class="subnav-shell">
        <a *ngFor="let tab of tabs"
          [routerLink]="tab.path"
          routerLinkActive="active"
          class="subnav-link">
          {{ tab.label }}
        </a>
      </section>

      <section *ngIf="!loading && tabs.length === 0" class="state-card">
        <h2>No available management views</h2>
        <p>The current role does not have any enabled user-management submenu.</p>
      </section>

      <router-outlet *ngIf="!loading && tabs.length > 0"></router-outlet>
    </section>
  `,
  styles: [`
    .page-shell {
      max-width: 1120px;
      margin: 0 auto;
      padding: 2rem 1.25rem 3rem;
    }
    .page-hero {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: end;
      margin-bottom: 1.5rem;
    }
    .eyebrow {
      display: inline-block;
      margin-bottom: 0.55rem;
      color: #9d5a00;
      font-size: 0.8rem;
      font-weight: 700;
      letter-spacing: 0.14em;
      text-transform: uppercase;
    }
    h1 {
      margin: 0;
      font-size: clamp(2rem, 4vw, 3rem);
      color: #1a3124;
    }
    .page-hero p {
      margin: 0.6rem 0 0;
      color: #587062;
      max-width: 700px;
      line-height: 1.6;
    }
    .subnav-shell {
      background: linear-gradient(180deg, #ffffff 0%, #f7faf7 100%);
      border: 1px solid #dde6df;
      border-radius: 20px;
      padding: 1rem;
      display: flex;
      gap: 0.75rem;
      flex-wrap: wrap;
      margin-bottom: 1rem;
    }
    .subnav-link {
      text-decoration: none;
      border-radius: 999px;
      padding: 0.7rem 1rem;
      font-weight: 700;
      color: #305240;
      background: #fff;
      border: 1px solid #d0dbd2;
    }
    .subnav-link.active {
      background: #174d2b;
      color: #fff;
      border-color: #174d2b;
    }
    .state-card {
      background: rgba(255, 255, 255, 0.95);
      border: 1px solid #dde6df;
      border-radius: 22px;
      box-shadow: 0 18px 36px rgba(25, 53, 41, 0.08);
    }
    .state-card {
      padding: 1.5rem;
    }
    .state-card h2 {
      margin: 0 0 0.5rem;
      color: #1f3428;
    }
    .state-card p {
      margin: 0;
      color: #5b7063;
    }
    @media (max-width: 720px) {
      .page-hero {
        flex-direction: column;
        align-items: start;
      }
    }
  `]
})
export class UserManagementComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly menuPermissions = inject(MenuPermissionService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdr = inject(ChangeDetectorRef);

  tabs: Array<{ path: string; label: string; menuKey: MenuPermissionKey }> = [];
  loading = true;

  ngOnInit(): void {
    this.auth.currentUser$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        void this.initializeTabs();
      });
  }

  private async initializeTabs(): Promise<void> {
    this.loading = true;
    this.cdr.detectChanges();

    await this.auth.ensureReady();

    if (!this.auth.isLoggedIn) {
      this.tabs = [];
      this.loading = false;
      this.cdr.detectChanges();
      return;
    }

    await this.menuPermissions.ensureLoaded();

    const candidates: Array<{ path: string; label: string; menuKey: MenuPermissionKey; roles: AppRole[] }> = [
      { path: 'user-list', label: 'User List', menuKey: 'user-management.user-list', roles: ['SuperAdmin'] },
      { path: 'role-access', label: 'Role Access', menuKey: 'user-management.role-access', roles: ['SuperAdmin'] },
      { path: 'order-summary', label: 'Order History', menuKey: 'user-management.order-summary', roles: ['SuperAdmin', 'Admin', 'Rider'] }
    ];

    this.tabs = candidates.filter(tab => this.auth.hasAnyRole(tab.roles) && this.menuPermissions.hasPermission(tab.menuKey));
    this.loading = false;
    this.cdr.detectChanges();

    if (this.tabs.length === 0) {
      await this.router.navigate(['/unauthorized']);
      return;
    }

    if (!this.route.firstChild) {
      await this.router.navigate([this.tabs[0].path], { relativeTo: this.route });
    }
  }
}