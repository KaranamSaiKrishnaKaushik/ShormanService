import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { AppRole } from '../models/user.model';
import { MenuPermissionService } from '../services/menu-permission.service';
import { MenuPermissionKey } from '../models/menu-permission.model';

export const roleGuard: CanActivateFn = async (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const menuPermissions = inject(MenuPermissionService);
  const requiredRoles = (route.data?.['roles'] as AppRole[] | undefined) ?? [];
  const requiredMenuKey = route.data?.['menuKey'] as MenuPermissionKey | undefined;
  const requiredAnyMenuKeys = (route.data?.['anyMenuKeys'] as MenuPermissionKey[] | undefined) ?? [];

  if (!auth.isLoggedIn) {
    return auth.canActivateProtectedRoute(state.url);
  }

  await auth.ensureReady();

  if (!auth.isLoggedIn) {
    return auth.canActivateProtectedRoute(state.url);
  }

  if (requiredRoles.length === 0 || auth.hasAnyRole(requiredRoles)) {
    if (!requiredMenuKey && requiredAnyMenuKeys.length === 0) {
      return true;
    }

    await menuPermissions.ensureLoaded();

    if (requiredMenuKey && !menuPermissions.hasPermission(requiredMenuKey)) {
      return router.createUrlTree(['/unauthorized']);
    }

    if (requiredAnyMenuKeys.length > 0 && !menuPermissions.hasAnyPermission(requiredAnyMenuKeys)) {
      return router.createUrlTree(['/unauthorized']);
    }

    return true;
  }

  return router.createUrlTree(['/unauthorized']);
};