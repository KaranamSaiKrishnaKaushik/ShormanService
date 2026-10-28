import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgFor, NgIf } from '@angular/common';
import { UserManagementService } from '../../core/services/user-management.service';
import { MenuPermissionKey, RoleMenuPermissions } from '../../core/models/menu-permission.model';
import { AppRole } from '../../core/models/user.model';

type ManagedRole = 'Admin' | 'Customer' | 'Rider';

@Component({
  selector: 'app-role-access-management',
  standalone: true,
  imports: [FormsModule, NgFor, NgIf],
  template: `
    <p *ngIf="errorMsg" class="error-msg">{{ errorMsg }}</p>

    <section *ngIf="loading" class="state-card">
      <h2>Loading role access</h2>
      <p>Fetching the current menu-permission matrix.</p>
    </section>

    <section *ngIf="!loading" class="grid-shell">
      <article *ngFor="let role of managedRoles" class="role-card">
        <div class="card-header">
          <div>
            <h2>{{ role }}</h2>
            <p>Choose which menus and admin functions this role can open.</p>
          </div>
          <button type="button" class="save-btn" [disabled]="savingRole === role" (click)="saveRole(role)">
            {{ savingRole === role ? 'Saving...' : 'Save Access' }}
          </button>
        </div>

        <label *ngFor="let item of menuItems" class="permission-row">
          <input type="checkbox" [(ngModel)]="draft[role][item.key]" />
          <span>
            <strong>{{ item.label }}</strong>
            <small>{{ item.description }}</small>
          </span>
        </label>
      </article>
    </section>
  `,
  styles: [`
    .error-msg {
      margin: 0 0 1rem;
      padding: 0.85rem 1rem;
      border-radius: 14px;
      background: #fff1f0;
      color: #9a2f27;
      border: 1px solid #f1c8c4;
    }
    .state-card,
    .role-card {
      background: rgba(255, 255, 255, 0.95);
      border: 1px solid #dde6df;
      border-radius: 22px;
      box-shadow: 0 18px 36px rgba(25, 53, 41, 0.08);
    }
    .state-card {
      padding: 1.5rem;
    }
    .grid-shell {
      display: grid;
      gap: 1rem;
      grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
    }
    .role-card {
      padding: 1.25rem;
      display: grid;
      gap: 0.9rem;
    }
    .card-header {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: start;
    }
    .card-header h2 {
      margin: 0 0 0.35rem;
      color: #203328;
    }
    .card-header p {
      margin: 0;
      color: #607568;
      line-height: 1.5;
    }
    .permission-row {
      display: flex;
      gap: 0.85rem;
      align-items: start;
      padding: 0.85rem;
      border-radius: 14px;
      background: #f7faf7;
    }
    .permission-row input {
      margin-top: 0.2rem;
      transform: scale(1.15);
    }
    .permission-row strong,
    .permission-row small {
      display: block;
    }
    .permission-row small {
      margin-top: 0.25rem;
      color: #66796d;
      line-height: 1.45;
    }
    .save-btn {
      border: none;
      border-radius: 999px;
      padding: 0.75rem 1rem;
      font-weight: 700;
      cursor: pointer;
      background: #123f27;
      color: #fff;
    }
    .save-btn:disabled {
      opacity: 0.65;
      cursor: default;
    }
    @media (max-width: 720px) {
      .card-header {
        flex-direction: column;
      }
    }
  `]
})
export class RoleAccessManagementComponent implements OnInit {
  private readonly userManagementService = inject(UserManagementService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly managedRoles: ManagedRole[] = ['Admin', 'Customer', 'Rider'];
  readonly menuItems: Array<{ key: MenuPermissionKey; label: string; description: string }> = [
    { key: 'products', label: 'Products', description: 'Allow the storefront catalog and products landing page in navigation.' },
    { key: 'checkout', label: 'Checkout', description: 'Allow the role to open checkout and place orders from the cart.' },
    { key: 'orders', label: 'Order History', description: 'Allow the order-history page and order detail pages for this role.' },
    { key: 'history-stats', label: 'History Stats', description: 'Allow the shopping-history analytics page in navigation and routing.' },
    { key: 'addresses', label: 'Addresses', description: 'Allow address management pages.' },
    { key: 'rider-dashboard', label: 'Rider Dashboard', description: 'Allow the rider workspace entry in navigation and routing.' },
    { key: 'product-management', label: 'Product Management', description: 'Allow the product import workspace with uploads, history, and catalog review.' },
    { key: 'user-management.order-summary', label: 'Order History Admin', description: 'Allow the admin order-history tab inside user management.' },
    { key: 'user-management.user-list', label: 'User List Admin', description: 'Allow the registered-user list and role assignment view.' },
    { key: 'user-management.role-access', label: 'Role Access Admin', description: 'Allow editing the menu-permission matrix itself.' }
  ];

  permissions: RoleMenuPermissions[] = [];
  draft: Record<ManagedRole, Record<MenuPermissionKey, boolean>> = {
    Admin: {} as Record<MenuPermissionKey, boolean>,
    Customer: {} as Record<MenuPermissionKey, boolean>,
    Rider: {} as Record<MenuPermissionKey, boolean>
  };
  loading = true;
  savingRole: ManagedRole | null = null;
  errorMsg = '';

  ngOnInit(): void {
    this.loadPermissions();
  }

  loadPermissions(): void {
    this.loading = true;
    this.errorMsg = '';

    this.userManagementService.getMenuPermissions().subscribe({
      next: permissions => {
        this.permissions = permissions;
        this.initializeDraft(permissions);
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: err => {
        this.errorMsg = err?.error?.message || 'Failed to load role access.';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  saveRole(role: ManagedRole): void {
    this.savingRole = role;
    this.errorMsg = '';

    const enabledMenuKeys = this.menuItems
      .filter(item => this.draft[role][item.key])
      .map(item => item.key);

    this.userManagementService.updateMenuPermissions(role as AppRole, enabledMenuKeys).subscribe({
      next: updated => {
        this.permissions = this.permissions.map(existing => existing.role === updated.role ? updated : existing);
        this.initializeDraft(this.permissions);
        this.savingRole = null;
        this.cdr.detectChanges();
      },
      error: err => {
        this.errorMsg = err?.error?.message || 'Failed to save role access.';
        this.savingRole = null;
        this.cdr.detectChanges();
      }
    });
  }

  private initializeDraft(permissions: RoleMenuPermissions[]): void {
    for (const role of this.managedRoles) {
      const rolePermissions = permissions.find(item => item.role === role)?.permissions ?? [];
      this.draft[role] = this.menuItems.reduce<Record<MenuPermissionKey, boolean>>((accumulator, item) => {
        accumulator[item.key] = rolePermissions.find(permission => permission.menuKey === item.key)?.isEnabled ?? false;
        return accumulator;
      }, {} as Record<MenuPermissionKey, boolean>);
    }
  }
}