import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AdminUser, AppRole } from '../models/user.model';
import { RoleMenuPermissions, MenuPermissionKey } from '../models/menu-permission.model';
import { AdminOrderSummary } from '../models/order.model';

interface OrderAlertCountResponse {
  count: number;
}

@Injectable({ providedIn: 'root' })
export class UserManagementService {
  private http = inject(HttpClient);

  getUsers(): Observable<AdminUser[]> {
    return this.http.get<AdminUser[]>(`${environment.apiUrl}/admin/users`);
  }

  getOrderSummary(): Observable<AdminOrderSummary[]> {
    return this.http.get<AdminOrderSummary[]>(`${environment.apiUrl}/admin/users/order-summary`);
  }

  getAwaitingPickupCount(): Observable<number> {
    return this.http.get<OrderAlertCountResponse>(`${environment.apiUrl}/admin/users/order-summary/awaiting-pickup-count`)
      .pipe(map(response => response.count ?? 0));
  }

  updateUserRole(userId: number, role: AppRole): Observable<AdminUser> {
    return this.http.put<AdminUser>(`${environment.apiUrl}/admin/users/${userId}/role`, { role });
  }

  deleteUser(userId: number): Observable<{ userId: number; message: string }> {
    return this.http.delete<{ userId: number; message: string }>(`${environment.apiUrl}/admin/users/${userId}`);
  }

  getMenuPermissions(): Observable<RoleMenuPermissions[]> {
    return this.http.get<RoleMenuPermissions[]>(`${environment.apiUrl}/admin/users/menu-permissions`);
  }

  updateMenuPermissions(role: AppRole, enabledMenuKeys: MenuPermissionKey[]): Observable<RoleMenuPermissions> {
    return this.http.put<RoleMenuPermissions>(`${environment.apiUrl}/admin/users/menu-permissions/${role}`, {
      role,
      enabledMenuKeys
    });
  }
}