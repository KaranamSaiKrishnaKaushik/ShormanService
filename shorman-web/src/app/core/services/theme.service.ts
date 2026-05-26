import { Injectable, inject } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { AuthService } from './auth.service';

export interface ThemeOption {
  id: ThemeId;
  label: string;
}

export type ThemeId = 'fresh-market' | 'terracotta' | 'coastal';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private static readonly STORAGE_KEY = 'shorman-theme';
  private readonly auth = inject(AuthService);

  readonly themeOptions: ThemeOption[] = [
    { id: 'fresh-market', label: 'Fresh' },
    { id: 'terracotta', label: 'Terracotta' },
    { id: 'coastal', label: 'Coastal' }
  ];

  private readonly themeIdSubject = new BehaviorSubject<ThemeId>('fresh-market');
  readonly themeId$ = this.themeIdSubject.asObservable();

  constructor() {
    this.auth.currentUser$.subscribe(user => {
      const userTheme = user?.themePreference ?? null;
      if (this.isThemeId(userTheme)) {
        this.applyTheme(userTheme);
        return;
      }

      if (!user) {
        const storedTheme = localStorage.getItem(ThemeService.STORAGE_KEY);
        this.applyTheme(this.isThemeId(storedTheme) ? storedTheme : 'fresh-market');
      }
    });
  }

  initialize(): void {
    const stored = localStorage.getItem(ThemeService.STORAGE_KEY);
    const persistedTheme = this.auth.currentUser?.themePreference ?? null;
    const initialTheme = this.isThemeId(persistedTheme)
      ? persistedTheme
      : (this.isThemeId(stored) ? stored : 'fresh-market');
    this.applyTheme(initialTheme);
  }

  setTheme(themeId: ThemeId): void {
    const previousTheme = this.getCurrentThemeId();
    this.applyTheme(themeId);
    localStorage.setItem(ThemeService.STORAGE_KEY, themeId);

    if (!this.auth.currentUser) {
      return;
    }

    this.auth.updateProfile({ themePreference: themeId }).subscribe({
      next: user => {
        const persistedTheme = user.themePreference ?? null;
        if (this.isThemeId(persistedTheme)) {
          this.applyTheme(persistedTheme);
          localStorage.setItem(ThemeService.STORAGE_KEY, persistedTheme);
        }
      },
      error: error => {
        console.error('Failed to persist theme preference.', error);
        this.applyTheme(previousTheme);
        localStorage.setItem(ThemeService.STORAGE_KEY, previousTheme);
      }
    });
  }

  getCurrentThemeId(): ThemeId {
    return this.themeIdSubject.value;
  }

  private applyTheme(themeId: ThemeId): void {
    document.body.dataset['theme'] = themeId;
    this.themeIdSubject.next(themeId);
  }

  private isThemeId(value: string | null): value is ThemeId {
    return this.themeOptions.some(option => option.id === value);
  }
}