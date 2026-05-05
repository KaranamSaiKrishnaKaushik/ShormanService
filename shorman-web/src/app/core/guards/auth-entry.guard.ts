import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authEntryGuard: CanActivateFn = async route => {
  const auth = inject(AuthService);
  const router = inject(Router);

  await auth.ensureReady();

  if (!auth.isLoggedIn) {
    return true;
  }

  const returnUrl = route.queryParamMap.get('returnUrl');
  const target = returnUrl && returnUrl.startsWith('/') ? returnUrl : '/products';
  return router.createUrlTree([target]);
};