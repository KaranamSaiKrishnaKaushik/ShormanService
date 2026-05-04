import { Auth0Client, createAuth0Client } from '@auth0/auth0-spa-js';
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, delay, firstValueFrom, of, tap, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { User, AuthResponse, LoginRequest, RegisterRequest } from '../models/user.model';

// Mock user database for development
const MOCK_USERS: Array<User & { password: string }> = [
  {
    id: 1,
    email: 'demo@shorman.com',
    password: 'demo123',
    firstName: 'Hesham',
    lastName: 'Shorman',
    phone: '+49 123 456789'
  },
  {
    id: 2,
    email: 'test@test.com',
    password: 'test123',
    firstName: 'Kaushik',
    lastName: 'Karanam',
    phone: '+49 987 654321'
  }
];

let nextUserId = 3;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private useMock = environment.useMockAuth;
  private auth0Enabled = !!environment.auth0?.domain && !!environment.auth0?.clientId && !!environment.auth0?.audience;
  private auth0ClientPromise?: Promise<Auth0Client>;
  private initializationPromise: Promise<void>;

  private readonly TOKEN_KEY = 'shorman_token';
  private readonly USER_KEY = 'shorman_user';

  private currentUserSubject = new BehaviorSubject<User | null>(this.loadUser());
  currentUser$ = this.currentUserSubject.asObservable();

  private isLoggedInSubject = new BehaviorSubject<boolean>(!!this.loadToken());
  isLoggedIn$ = this.isLoggedInSubject.asObservable();

  constructor() {
    this.initializationPromise = this.initializeAuth();
  }

  private loadToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  private loadUser(): User | null {
    const raw = localStorage.getItem(this.USER_KEY);
    return raw ? JSON.parse(raw) : null;
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

    if (this.auth0Enabled) {
      await this.startLogin(returnUrl);
    } else {
      await this.router.navigate(['/login'], { queryParams: { returnUrl } });
    }

    return false;
  }

  async startLogin(returnUrl = '/products'): Promise<void> {
    if (!this.auth0Enabled) {
      await this.router.navigate(['/login'], { queryParams: { returnUrl } });
      return;
    }

    const client = await this.getAuth0Client();
    await client.loginWithRedirect({
      appState: { target: returnUrl },
      authorizationParams: {
        audience: environment.auth0.audience,
        scope: 'openid profile email',
        prompt: 'login'
      }
    });
  }

  async startSignup(returnUrl = '/products'): Promise<void> {
    if (!this.auth0Enabled) {
      await this.router.navigate(['/register'], { queryParams: { returnUrl } });
      return;
    }

    const client = await this.getAuth0Client();
    await client.loginWithRedirect({
      appState: { target: returnUrl },
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

      // Create mock auth response
      const { password, ...userWithoutPassword } = user;
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

  register(req: RegisterRequest): Observable<AuthResponse> {
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
        phone: req.phone
      };

      MOCK_USERS.push(newUser);

      // Create mock auth response
      const { password, ...userWithoutPassword } = newUser;
      const mockResponse: AuthResponse = {
        token: `mock-token-${Date.now()}`,
        user: userWithoutPassword
      };

      return of(mockResponse).pipe(
        delay(500), // Simulate network delay
        tap(res => this.handleAuth(res))
      );
    }

    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/register`, req).pipe(
      tap(res => this.handleAuth(res))
    );
  }

  private handleAuth(res: AuthResponse): void {
    localStorage.setItem(this.TOKEN_KEY, res.token);
    localStorage.setItem(this.USER_KEY, JSON.stringify(res.user));
    this.currentUserSubject.next(res.user);
    this.isLoggedInSubject.next(true);
  }

  logout(): void {
    this.clearLocalAuthState();

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

      if (searchParams.has('code') && searchParams.has('state')) {
        const callbackResult = await client.handleRedirectCallback();
        await this.exchangeAuth0Token(client);

        const target =
          typeof callbackResult.appState?.target === 'string' && callbackResult.appState.target.length > 0
            ? callbackResult.appState.target
            : '/products';

        await this.router.navigateByUrl(target);
        return;
      }

      if (await client.isAuthenticated()) {
        if (!this.isLoggedIn) {
          await this.exchangeAuth0Token(client);
        }
      }
    } catch (error) {
      console.error('Auth0 initialization failed.', error);
      this.clearLocalAuthState();
    }
  }

  private getAuth0Client(): Promise<Auth0Client> {
    if (!this.auth0ClientPromise) {
      this.auth0ClientPromise = createAuth0Client({
        domain: environment.auth0.domain,
        clientId: environment.auth0.clientId,
        authorizationParams: {
          redirect_uri: `${window.location.origin}/login`,
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
}
