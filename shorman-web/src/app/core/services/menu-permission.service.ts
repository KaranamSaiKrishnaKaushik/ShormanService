import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, firstValueFrom, map, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { CurrentMenuPermissions, MenuPermissionKey } from '../models/menu-permission.model';

@Injectable({ providedIn: 'root' })
export class MenuPermissionService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly enabledKeysSubject = new BehaviorSubject<Set<MenuPermissionKey> | null>(null);
  private readonly loadedForUserSubject = new BehaviorSubject<number | null>(null);

  constructor() {
    this.auth.currentUser$.subscribe(user => {
      const currentLoadedUserId = this.loadedForUserSubject.value;
      if (!user) {
        this.enabledKeysSubject.next(null);
        this.loadedForUserSubject.next(null);
        return;
      }

      if (currentLoadedUserId !== user.id) {
        this.enabledKeysSubject.next(null);
        this.loadedForUserSubject.next(null);
      }
    });
  }

  async ensureLoaded(): Promise<void> {
    const currentUser = this.auth.currentUser;
    if (!currentUser) {
      this.enabledKeysSubject.next(null);
      this.loadedForUserSubject.next(null);
      return;
    }

    if (this.auth.hasRole('SuperAdmin')) {
      return;
    }

    if (this.loadedForUserSubject.value === currentUser.id && this.enabledKeysSubject.value !== null) {
      return;
    }

    const enabledKeys = await firstValueFrom(
      this.http.get<CurrentMenuPermissions>(`${environment.apiUrl}/menu-permissions/current`).pipe(
        map(response => new Set((response.enabledMenuKeys ?? []).filter((key): key is MenuPermissionKey => typeof key === 'string'))),
        catchError(() => of(new Set<MenuPermissionKey>()))
      )
    );

    this.enabledKeysSubject.next(enabledKeys);
    this.loadedForUserSubject.next(currentUser.id);
  }

  hasPermission(key: MenuPermissionKey): boolean {
    if (this.auth.hasRole('SuperAdmin')) {
      return true;
    }

    return this.enabledKeysSubject.value?.has(key) ?? false;
  }

  hasAnyPermission(keys: readonly MenuPermissionKey[]): boolean {
    if (this.auth.hasRole('SuperAdmin')) {
      return true;
    }

    const current = this.enabledKeysSubject.value;
    return current ? keys.some(key => current.has(key)) : false;
  }
}