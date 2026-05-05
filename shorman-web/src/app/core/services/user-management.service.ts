import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AdminUser, AppRole } from '../models/user.model';

@Injectable({ providedIn: 'root' })
export class UserManagementService {
  private http = inject(HttpClient);

  getUsers(): Observable<AdminUser[]> {
    return this.http.get<AdminUser[]>(`${environment.apiUrl}/admin/users`);
  }

  updateUserRole(userId: number, role: AppRole): Observable<AdminUser> {
    return this.http.put<AdminUser>(`${environment.apiUrl}/admin/users/${userId}/role`, { role });
  }
}