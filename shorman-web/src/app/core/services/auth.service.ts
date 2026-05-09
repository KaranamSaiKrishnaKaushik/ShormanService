import { Auth0Client, createAuth0Client } from '@auth0/auth0-spa-js';
import { Injectable, NgZone, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, delay, firstValueFrom, of, tap, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import {
  AppRole,
  User,
  AuthResponse,
  LoginRequest,
  RegisterRequest,
  RegisterResponse,
  VerifyEmailRequest,
  PasswordResetRequest,
  PasswordResetConfirmRequest,
  PasswordResetRequestResponse
} from '../models/user.model';

// Mock user database for development
const MOCK_USERS: Array<User & { password: string; isEmailVerified: boolean; passwordResetCode?: string | null }> = [
  {
    id: 1,
    email: 'demo@shorman.com',
    password: 'demo123',
    firstName: 'Super',
    lastName: 'Admin',
    phone: '+49 123 456789',
    isEmailVerified: true,
    roles: ['SuperAdmin']
  },
  {
    id: 2,
    email: 'test@test.com',
    password: 'test123',
    firstName: 'Customer',
    lastName: 'User',
    phone: '+49 987 654321',
    isEmailVerified: true,
    roles: ['Customer']
  },
  {
    id: 3,
    email: 'admin@shorman.com',
    password: 'admin123',
    firstName: 'Admin',
    lastName: 'User',
    phone: '+49 222 333444',
    isEmailVerified: true,
    roles: ['Admin']
  },
  {
    id: 4,
    email: 'rider@shorman.com',
    password: 'rider123',
    firstName: 'Rider',
    lastName: 'User',
    phone: '+49 555 123456',
    isEmailVerified: true,
    roles: ['Rider']
  }
];

let nextUserId = 5;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private ngZone = inject(NgZone);
  private useMock = environment.useMockAuth;
  private auth0Enabled = !!environment.auth0?.domain && !!environment.auth0?.clientId && !!environment.auth0?.audience;
  private readonly googleConnection = 'google-oauth2';
  private readonly authCallbackPath = '/auth/callback';
  private auth0ClientPromise?: Promise<Auth0Client>;
  private initializationPromise: Promise<void>;

  private readonly TOKEN_KEY = 'shorman_token';
  private readonly USER_KEY = 'shorman_user';
  private readonly RETURN_URL_KEY = 'shorman_return_url';
  private readonly LAST_LOGIN_EMAIL_KEY = 'shorman_last_login_email';

  private currentUserSubject = new BehaviorSubject<User | null>(this.loadUser());
  currentUser$ = this.currentUserSubject.asObservable();

  private isLoggedInSubject = new BehaviorSubject<boolean>(!!this.loadToken());
  isLoggedIn$ = this.isLoggedInSubject.asObservable();

  private authErrorSubject = new BehaviorSubject<string | null>(null);
  authError$ = this.authErrorSubject.asObservable();

  constructor() {
    this.initializationPromise = this.initializeAuth();
  }

  private loadToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  private loadUser(): User | null {
    const raw = localStorage.getItem(this.USER_KEY);
    if (!raw) {
      return null;
    }

    return this.normalizeUser(JSON.parse(raw) as Partial<User>);
  }

  private loadLastLoginEmail(): string | null {
    return localStorage.getItem(this.LAST_LOGIN_EMAIL_KEY);
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  get currentUser(): User | null {
    return this.currentUserSubject.value;
  }

  get isLoggedIn(): boolean {
    return this.isLoggedInSubject.value;
  }

  get authError(): string | null {
    return this.authErrorSubject.value;
  }

  clearAuthError(): void {
    this.authErrorSubject.next(null);
  }

  private storeReturnUrl(returnUrl: string): void {
    const normalizedUrl = returnUrl.startsWith('/') ? returnUrl : `/${returnUrl}`;
    sessionStorage.setItem(this.RETURN_URL_KEY, normalizedUrl);
  }

  private consumeReturnUrl(fallback?: string): string {
    const storedValue = sessionStorage.getItem(this.RETURN_URL_KEY);
    sessionStorage.removeItem(this.RETURN_URL_KEY);
    return storedValue && storedValue.startsWith('/') ? storedValue : (fallback ?? this.getDefaultPostLoginRoute());
  }

  async redirectAfterLogin(target?: string): Promise<void> {
    const defaultTarget = this.getDefaultPostLoginRoute();
    const requestedTarget = target && target.trim().length > 0 ? target : defaultTarget;
    const normalizedTarget = requestedTarget.startsWith('/') ? requestedTarget : `/${requestedTarget}`;
    const resolvedTarget = normalizedTarget === '/login' || normalizedTarget === '/register'
      ? defaultTarget
      : normalizedTarget;

    const navigationAttempt = this.ngZone.run(() => this.router.navigateByUrl(resolvedTarget, { replaceUrl: true }));

    await Promise.race([
      navigationAttempt,
      new Promise(resolve => window.setTimeout(resolve, 250))
    ]);

    if (
      window.location.pathname === '/login' ||
      window.location.pathname === '/register' ||
      window.location.pathname === this.authCallbackPath
    ) {
      window.location.replace(`${window.location.origin}${resolvedTarget}`);
    }
  }

  async ensureReady(): Promise<void> {
    await this.initializationPromise;
  }

  async canActivateProtectedRoute(returnUrl: string): Promise<boolean> {
    if (this.isLoggedIn) {
      return true;
    }

    await this.ensureReady();

    if (this.isLoggedIn) {
      return true;
    }

    await this.router.navigate(['/login'], { queryParams: { returnUrl } });

    return false;
  }

  async startLogin(returnUrl?: string): Promise<void> {
    await this.startGoogleLogin(returnUrl);
  }

  async startGoogleLogin(returnUrl?: string): Promise<void> {
    this.authErrorSubject.next(null);
    const resolvedReturnUrl = returnUrl && returnUrl.trim().length > 0
      ? returnUrl
      : this.getDefaultPostLoginRoute();
    const lastLoginEmail = this.loadLastLoginEmail();

    this.storeReturnUrl(resolvedReturnUrl);

    if (!this.auth0Enabled) {
      await this.router.navigate(['/login'], { queryParams: { returnUrl: resolvedReturnUrl } });
      return;
    }

    const client = await this.getAuth0Client();
    await client.loginWithRedirect({
      appState: { target: resolvedReturnUrl },
      authorizationParams: {
        audience: environment.auth0.audience,
        scope: 'openid profile email',
        connection: this.googleConnection,
        ...(lastLoginEmail ? { login_hint: lastLoginEmail } : {})
      }
    });
  }

  async startSignup(returnUrl?: string): Promise<void> {
    this.authErrorSubject.next(null);
    const resolvedReturnUrl = returnUrl && returnUrl.trim().length > 0
      ? returnUrl
      : this.getDefaultPostLoginRoute();

    this.storeReturnUrl(resolvedReturnUrl);

    if (!this.auth0Enabled) {
      await this.router.navigate(['/register'], { queryParams: { returnUrl: resolvedReturnUrl } });
      return;
    }

    const client = await this.getAuth0Client();
    await client.loginWithRedirect({
      appState: { target: resolvedReturnUrl },
      authorizationParams: {
        audience: environment.auth0.audience,
        scope: 'openid profile email',
        screen_hint: 'signup'
      }
    });
  }

  login(req: LoginRequest): Observable<AuthResponse> {
    if (this.useMock) {
      // Find user by email
      const user = MOCK_USERS.find(u => u.email === req.email);
      
      if (!user || user.password !== req.password) {
        return throwError(() => new Error('Invalid email or password'));
      }

      if (!user.isEmailVerified) {
        return throwError(() => new Error('Email not verified. Verify your email before signing in.'));
      }

      // Create mock auth response
      const { password, isEmailVerified, passwordResetCode, ...userWithoutPassword } = user;
      const mockResponse: AuthResponse = {
        token: `mock-token-${Date.now()}`,
        user: userWithoutPassword
      };

      return of(mockResponse).pipe(
        delay(500), // Simulate network delay
        tap(res => this.handleAuth(res))
      );
    }

    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/login`, req).pipe(
      tap(res => this.handleAuth(res))
    );
  }

  register(req: RegisterRequest): Observable<RegisterResponse> {
    if (this.useMock) {
      // Check if user already exists
      const existingUser = MOCK_USERS.find(u => u.email === req.email);
      if (existingUser) {
        return throwError(() => new Error('Email already registered'));
      }

      // Create new user
      const newUser = {
        id: nextUserId++,
        email: req.email,
        password: req.password,
        firstName: req.firstName,
        lastName: req.lastName,
        phone: req.phone,
        isEmailVerified: false,
        passwordResetCode: null,
        roles: ['Customer'] as AppRole[]
      };

      MOCK_USERS.push(newUser);

      const mockResponse: RegisterResponse = {
        email: req.email,
        message: 'Account created. Verify your email using the code before signing in.',
        verificationRequired: true,
        verificationCode: '123456'
      };

      return of(mockResponse).pipe(
        delay(500)
      );
    }

    return this.http.post<RegisterResponse>(`${environment.apiUrl}/auth/register`, req);
  }

  verifyEmail(req: VerifyEmailRequest): Observable<AuthResponse> {
    if (this.useMock) {
      const user = MOCK_USERS.find(u => u.email === req.email);
      if (!user || req.code.trim() !== '123456') {
        return throwError(() => new Error('Verification code is invalid or expired.'));
      }

      user.isEmailVerified = true;
      const { password, isEmailVerified, passwordResetCode, ...userWithoutPassword } = user;
      return of({
        token: `mock-token-${Date.now()}`,
        user: userWithoutPassword
      }).pipe(
        delay(500),
        tap(res => this.handleAuth(res))
      );
    }

    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/verify-email`, req).pipe(
      tap(res => this.handleAuth(res))
    );
  }

  requestPasswordReset(req: PasswordResetRequest): Observable<PasswordResetRequestResponse> {
    if (this.useMock) {
      const user = MOCK_USERS.find(u => u.email === req.email);
      if (user && user.isEmailVerified) {
        user.passwordResetCode = '654321';
      }

      return of({
        message: 'If an account exists for that email, a reset code has been issued.',
        resetCode: user?.isEmailVerified ? user.passwordResetCode : null
      }).pipe(delay(500));
    }

    return this.http.post<PasswordResetRequestResponse>(`${environment.apiUrl}/auth/password-reset/request`, req);
  }

  resetPassword(req: PasswordResetConfirmRequest): Observable<{ message: string }> {
    if (this.useMock) {
      const user = MOCK_USERS.find(u => u.email === req.email);
      if (!user || user.passwordResetCode !== req.code.trim()) {
        return throwError(() => new Error('Reset code is invalid or expired.'));
      }

      user.password = req.newPassword;
      user.passwordResetCode = null;
      return of({ message: 'Password reset successfully. You can now sign in with the new password.' }).pipe(delay(500));
    }

    return this.http.post<{ message: string }>(`${environment.apiUrl}/auth/password-reset/confirm`, req);
  }

  private handleAuth(res: AuthResponse): void {
    const normalizedUser = this.normalizeUser(res.user);

    if (!normalizedUser) {
      throw new Error('Authentication response did not include a valid user payload.');
    }

    this.authErrorSubject.next(null);
    localStorage.setItem(this.TOKEN_KEY, res.token);
    localStorage.setItem(this.USER_KEY, JSON.stringify(normalizedUser));
    localStorage.setItem(this.LAST_LOGIN_EMAIL_KEY, normalizedUser.email);
    this.currentUserSubject.next(normalizedUser);
    this.isLoggedInSubject.next(true);
  }

  hasRole(role: AppRole): boolean {
    const currentUser = this.currentUser;
    if (!currentUser) {
      return false;
    }

    return currentUser.roles.some(currentRole => currentRole.toLowerCase() === role.toLowerCase());
  }

  hasAnyRole(roles: readonly AppRole[]): boolean {
    const currentUser = this.currentUser;
    if (!currentUser) {
      return false;
    }

    if (this.hasRole('SuperAdmin')) {
      return true;
    }

    return roles.some(role => this.hasRole(role));
  }

  logout(): void {
    this.clearLocalAuthState();
    this.authErrorSubject.next(null);
    sessionStorage.removeItem(this.RETURN_URL_KEY);

    if (this.auth0Enabled) {
      void this.logoutFromAuth0();
      return;
    }

    void this.router.navigate(['/login']);
  }

  private async initializeAuth(): Promise<void> {
    if (!this.auth0Enabled) {
      return;
    }

    try {
      const client = await this.getAuth0Client();
      const searchParams = new URLSearchParams(window.location.search);
      const isAuth0Callback = searchParams.has('code') && searchParams.has('state');

      if (isAuth0Callback) {
        const callbackResult = await client.handleRedirectCallback();
        await this.exchangeAuth0Token(client);

        const target =
          typeof callbackResult.appState?.target === 'string' && callbackResult.appState.target.length > 0
            ? callbackResult.appState.target
            : this.consumeReturnUrl();

        await this.redirectAfterLogin(target);
        return;
      }

      try {
        await client.checkSession();
      } catch {
        // Silent SSO depends on the browser's cookie policy and existing Auth0 session.
      }

      if (await client.isAuthenticated()) {
        if (!this.isLoggedIn) {
          await this.exchangeAuth0Token(client);
        }

        if (window.location.pathname === this.authCallbackPath) {
          await this.redirectAfterLogin(this.consumeReturnUrl());
        }
      }
    } catch (error) {
      console.error('Auth0 initialization failed.', error);
      this.clearLocalAuthState();
      this.authErrorSubject.next(this.toAuthErrorMessage(error));

      const searchParams = new URLSearchParams(window.location.search);
      if (searchParams.has('code') || searchParams.has('state')) {
        await this.router.navigate(['/login'], { replaceUrl: true });
      }
    }
  }

  private getAuth0Client(): Promise<Auth0Client> {
    if (!this.auth0ClientPromise) {
      this.auth0ClientPromise = createAuth0Client({
        domain: environment.auth0.domain,
        clientId: environment.auth0.clientId,
        authorizationParams: {
          redirect_uri: `${window.location.origin}${this.authCallbackPath}`,
          audience: environment.auth0.audience,
          scope: 'openid profile email'
        },
        cacheLocation: 'localstorage'
      });
    }

    return this.auth0ClientPromise;
  }

  private async exchangeAuth0Token(client: Auth0Client): Promise<void> {
    const accessToken = await client.getTokenSilently({
      authorizationParams: {
        audience: environment.auth0.audience,
        scope: 'openid profile email'
      }
    });

    const profile = await client.getUser();

    if (!profile?.email) {
      throw new Error('The Auth0 profile did not include an email address.');
    }

    const authResponse = await firstValueFrom(
      this.http.post<AuthResponse>(
        `${environment.apiUrl}/auth/exchange`,
        {
          email: profile.email,
          firstName: profile.given_name ?? profile.name?.split(' ')[0] ?? null,
          lastName: profile.family_name ?? null
        },
        {
          headers: {
            Authorization: `Bearer ${accessToken}`
          }
        }
      )
    );

    this.handleAuth(authResponse);
  }

  private getDefaultPostLoginRoute(): string {
    if (this.hasRole('SuperAdmin')) {
      return '/user-management';
    }

    if (this.hasRole('Rider')) {
      return '/rider';
    }

    return '/products';
  }

  private async logoutFromAuth0(): Promise<void> {
    try {
      const client = await this.getAuth0Client();
      await client.logout({
        logoutParams: {
          returnTo: `${window.location.origin}`
        }
      });
    } catch (error) {
      console.error('Auth0 logout failed.', error);
      await this.router.navigate(['/login']);
    }
  }

  private clearLocalAuthState(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
    this.currentUserSubject.next(null);
    this.isLoggedInSubject.next(false);
  }

  private toAuthErrorMessage(error: unknown): string {
    const defaultMessage = 'Sign-in could not be completed. Check whether the local backend is running and whether Auth0 exchange is configured for this environment.';

    if (!error || typeof error !== 'object') {
      return defaultMessage;
    }

    const maybeError = error as { message?: unknown; error?: { message?: unknown }; status?: unknown };

    if (typeof maybeError.error?.message === 'string' && maybeError.error.message.trim()) {
      return maybeError.error.message;
    }

    if (typeof maybeError.message === 'string' && maybeError.message.trim()) {
      return `${defaultMessage} (${maybeError.message})`;
    }

    if (typeof maybeError.status === 'number') {
      return `${defaultMessage} (HTTP ${maybeError.status})`;
    }

    return defaultMessage;
  }

  private normalizeUser(user: Partial<User> | null): User | null {
    if (!user || typeof user.id !== 'number' || typeof user.email !== 'string') {
      return null;
    }

    return {
      id: user.id,
      email: user.email,
      firstName: user.firstName ?? '',
      lastName: user.lastName ?? '',
      phone: user.phone,
      createdAt: user.createdAt,
      roles: Array.isArray(user.roles) ? user.roles.filter((role): role is AppRole => typeof role === 'string') : []
    };
  }
}
