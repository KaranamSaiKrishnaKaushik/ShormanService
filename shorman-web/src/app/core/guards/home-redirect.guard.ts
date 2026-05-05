import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const homeRedirectGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isLoggedIn) {
    return router.createUrlTree(['/products']);
  }

  await auth.ensureReady();

  if (!auth.isLoggedIn) {
    return router.createUrlTree(['/products']);
  }

  if (auth.hasRole('SuperAdmin')) {
    return router.createUrlTree(['/user-management']);
  }

  if (auth.hasRole('Rider')) {
    return router.createUrlTree(['/rider']);
  }

  return router.createUrlTree(['/products']);
};