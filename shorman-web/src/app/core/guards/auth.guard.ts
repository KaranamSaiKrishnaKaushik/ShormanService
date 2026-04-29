import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const token = localStorage.getItem('shorman_token');
  console.log('Auth Guard Check:', {
    url: state.url,
    isLoggedIn: auth.isLoggedIn,
    hasToken: !!token,
    token: token
  });
  if (auth.isLoggedIn) {
    console.log('Auth guard: ALLOWED');
    return true;
  }
  console.log('Auth guard: BLOCKED - Redirecting to login');
  router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
  return false;
};
