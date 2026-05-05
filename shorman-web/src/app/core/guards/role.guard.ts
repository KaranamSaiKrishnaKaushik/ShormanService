import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { AppRole } from '../models/user.model';

export const roleGuard: CanActivateFn = async (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const requiredRoles = (route.data?.['roles'] as AppRole[] | undefined) ?? [];

  if (!auth.isLoggedIn) {
    return auth.canActivateProtectedRoute(state.url);
  }

  await auth.ensureReady();

  if (!auth.isLoggedIn) {
    return auth.canActivateProtectedRoute(state.url);
  }

  if (requiredRoles.length === 0 || auth.hasAnyRole(requiredRoles)) {
    return true;
  }

  return router.createUrlTree(['/unauthorized']);
};