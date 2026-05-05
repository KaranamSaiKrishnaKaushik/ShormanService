import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { DatePipe, NgClass, NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminUser, AppRole, APP_ROLES } from '../../core/models/user.model';
import { UserManagementService } from '../../core/services/user-management.service';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [FormsModule, NgFor, NgIf, NgClass, DatePipe],
  template: `
    <section class="page-shell">
      <header class="page-hero">
        <div>
          <span class="eyebrow">Super Admin Area</span>
          <h1>User Management</h1>
          <p>Manage registered users and assign exactly one application role per user for now.</p>
        </div>
        <button class="refresh-btn" (click)="loadUsers()" [disabled]="loading">
          {{ loading ? 'Refreshing...' : 'Refresh' }}
        </button>
      </header>

      <section class="toolbar">
        <input
          type="search"
          [(ngModel)]="searchTerm"
          placeholder="Search by name, email, or phone"
          aria-label="Search users"
        />
        <div class="role-filter">
          <button
            *ngFor="let role of filterOptions"
            type="button"
            class="filter-pill"
            [ngClass]="{ active: roleFilter === role }"
            (click)="roleFilter = role">
            {{ role === 'All' ? 'All Roles' : role }}
          </button>
        </div>
      </section>

      <p *ngIf="errorMsg" class="error-msg">{{ errorMsg }}</p>

      <section *ngIf="loading" class="state-card">
        <h2>Loading users</h2>
        <p>Fetching the latest role assignments from the backend.</p>
      </section>

      <section *ngIf="!loading && filteredUsers.length === 0" class="state-card">
        <h2>No users found</h2>
        <p>Try a different search term or role filter.</p>
      </section>

      <section *ngIf="!loading && filteredUsers.length > 0" class="table-shell">
        <article *ngFor="let user of filteredUsers" class="user-card">
          <div class="user-summary">
            <div>
              <h2>{{ user.firstName }} {{ user.lastName }}</h2>
              <p>{{ user.email }}</p>
              <span *ngIf="user.phone">{{ user.phone }}</span>
            </div>
            <div class="meta">
              <span class="badge">{{ getPrimaryRole(user) }}</span>
              <small>Joined {{ user.createdAt | date: 'mediumDate' }}</small>
            </div>
          </div>

          <div class="user-actions">
            <label>
              <span>Role</span>
              <select [(ngModel)]="pendingRoles[user.id]">
                <option *ngFor="let role of roles" [ngValue]="role">{{ role }}</option>
              </select>
            </label>

            <button
              type="button"
              class="save-btn"
              [disabled]="savingUserId === user.id || pendingRoles[user.id] === getPrimaryRole(user)"
              (click)="saveRole(user)">
              {{ savingUserId === user.id ? 'Saving...' : 'Save Role' }}
            </button>
          </div>
        </article>
      </section>
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
    .refresh-btn,
    .save-btn {
      border: none;
      border-radius: 999px;
      padding: 0.8rem 1.15rem;
      font-weight: 700;
      cursor: pointer;
      transition: transform 0.2s ease, opacity 0.2s ease;
    }
    .refresh-btn {
      background: #123f27;
      color: #fff;
      box-shadow: 0 12px 24px rgba(18, 63, 39, 0.18);
    }
    .toolbar {
      background: linear-gradient(180deg, #ffffff 0%, #f7faf7 100%);
      border: 1px solid #dde6df;
      border-radius: 20px;
      padding: 1rem;
      display: grid;
      gap: 1rem;
      margin-bottom: 1rem;
    }
    .toolbar input {
      width: 100%;
      border: 1px solid #cad6cc;
      border-radius: 14px;
      padding: 0.9rem 1rem;
      font-size: 1rem;
      background: #fff;
    }
    .role-filter {
      display: flex;
      gap: 0.65rem;
      flex-wrap: wrap;
    }
    .filter-pill {
      border: 1px solid #d0dbd2;
      background: #fff;
      color: #305240;
      border-radius: 999px;
      padding: 0.5rem 0.9rem;
      cursor: pointer;
      font-weight: 600;
    }
    .filter-pill.active {
      background: #174d2b;
      color: #fff;
      border-color: #174d2b;
    }
    .error-msg {
      margin: 0 0 1rem;
      padding: 0.85rem 1rem;
      border-radius: 14px;
      background: #fff1f0;
      color: #9a2f27;
      border: 1px solid #f1c8c4;
    }
    .state-card,
    .user-card {
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
    .table-shell {
      display: grid;
      gap: 1rem;
    }
    .user-card {
      padding: 1.2rem;
      display: grid;
      gap: 1rem;
    }
    .user-summary,
    .user-actions {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: center;
      flex-wrap: wrap;
    }
    .user-summary h2 {
      margin: 0 0 0.35rem;
      font-size: 1.2rem;
      color: #21352a;
    }
    .user-summary p,
    .user-summary span,
    .meta small,
    .user-actions label span {
      color: #607568;
    }
    .user-summary p,
    .user-summary span,
    .meta small {
      display: block;
      margin: 0.15rem 0;
    }
    .meta {
      text-align: right;
    }
    .badge {
      display: inline-block;
      padding: 0.5rem 0.75rem;
      border-radius: 999px;
      background: #edf7ee;
      color: #1f6a35;
      font-weight: 800;
      margin-bottom: 0.45rem;
    }
    .user-actions label {
      display: grid;
      gap: 0.45rem;
      min-width: 220px;
    }
    .user-actions select {
      border: 1px solid #cad6cc;
      border-radius: 12px;
      padding: 0.8rem 0.9rem;
      background: #fff;
      font-size: 0.95rem;
    }
    .save-btn {
      background: #ff8f00;
      color: #1f1a12;
    }
    .refresh-btn:hover,
    .save-btn:hover {
      transform: translateY(-1px);
    }
    .refresh-btn:disabled,
    .save-btn:disabled {
      opacity: 0.65;
      cursor: default;
      transform: none;
    }
    @media (max-width: 720px) {
      .page-hero,
      .user-summary,
      .user-actions {
        flex-direction: column;
        align-items: start;
      }
      .meta {
        text-align: left;
      }
    }
  `]
})
export class UserManagementComponent implements OnInit {
  private userManagementService = inject(UserManagementService);
  private cdr = inject(ChangeDetectorRef);

  readonly roles: AppRole[] = APP_ROLES;
  readonly filterOptions: Array<'All' | AppRole> = ['All', ...APP_ROLES];

  users: AdminUser[] = [];
  pendingRoles: Record<number, AppRole> = {};
  roleFilter: 'All' | AppRole = 'All';
  searchTerm = '';
  loading = true;
  savingUserId: number | null = null;
  errorMsg = '';

  ngOnInit(): void {
    this.loadUsers();
  }

  get filteredUsers(): AdminUser[] {
    const query = this.searchTerm.trim().toLowerCase();

    return this.users.filter(user => {
      const primaryRole = this.getPrimaryRole(user);
      const matchesRole = this.roleFilter === 'All' || primaryRole === this.roleFilter;
      const matchesText =
        query.length === 0 ||
        `${user.firstName} ${user.lastName}`.toLowerCase().includes(query) ||
        user.email.toLowerCase().includes(query) ||
        (user.phone ?? '').toLowerCase().includes(query);

      return matchesRole && matchesText;
    });
  }

  loadUsers(): void {
    this.loading = true;
    this.errorMsg = '';

    this.userManagementService.getUsers().subscribe({
      next: users => {
        this.users = Array.isArray(users) ? users : [];
        this.pendingRoles = this.users.reduce<Record<number, AppRole>>((accumulator, user) => {
          accumulator[user.id] = this.getPrimaryRole(user);
          return accumulator;
        }, {});
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: err => {
        this.errorMsg = err?.error?.message || 'Failed to load users.';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  getPrimaryRole(user: AdminUser): AppRole {
    return user.roles[0] ?? 'Customer';
  }

  saveRole(user: AdminUser): void {
    const role = this.pendingRoles[user.id] ?? this.getPrimaryRole(user);
    this.savingUserId = user.id;
    this.errorMsg = '';

    this.userManagementService.updateUserRole(user.id, role).subscribe({
      next: updatedUser => {
        this.users = this.users.map(existingUser => existingUser.id === updatedUser.id ? updatedUser : existingUser);
        this.pendingRoles[updatedUser.id] = this.getPrimaryRole(updatedUser);
        this.savingUserId = null;
        this.cdr.detectChanges();
      },
      error: err => {
        this.errorMsg = err?.error?.message || 'Failed to update user role.';
        this.savingUserId = null;
        this.cdr.detectChanges();
      }
    });
  }
}