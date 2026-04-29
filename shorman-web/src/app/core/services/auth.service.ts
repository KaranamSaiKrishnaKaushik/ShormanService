import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap, of, delay, throwError } from 'rxjs';
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

  private readonly TOKEN_KEY = 'shorman_token';
  private readonly USER_KEY = 'shorman_user';

  private currentUserSubject = new BehaviorSubject<User | null>(this.loadUser());
  currentUser$ = this.currentUserSubject.asObservable();

  private isLoggedInSubject = new BehaviorSubject<boolean>(!!this.loadToken());
  isLoggedIn$ = this.isLoggedInSubject.asObservable();

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
    const value = this.isLoggedInSubject.value;
    console.log('AuthService.isLoggedIn getter called, returning:', value);
    return value;
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
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
    this.currentUserSubject.next(null);
    this.isLoggedInSubject.next(false);
    this.router.navigate(['/login']);
  }
}
