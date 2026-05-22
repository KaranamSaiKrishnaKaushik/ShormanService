import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface ThemeOption {
  id: ThemeId;
  label: string;
}

export type ThemeId = 'fresh-market' | 'terracotta' | 'coastal';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private static readonly STORAGE_KEY = 'shorman-theme';

  readonly themeOptions: ThemeOption[] = [
    { id: 'fresh-market', label: 'Fresh' },
    { id: 'terracotta', label: 'Terracotta' },
    { id: 'coastal', label: 'Coastal' }
  ];

  private readonly themeIdSubject = new BehaviorSubject<ThemeId>('fresh-market');
  readonly themeId$ = this.themeIdSubject.asObservable();

  initialize(): void {
    const stored = localStorage.getItem(ThemeService.STORAGE_KEY);
    const initialTheme = this.isThemeId(stored) ? stored : 'fresh-market';
    this.applyTheme(initialTheme);
  }

  setTheme(themeId: ThemeId): void {
    this.applyTheme(themeId);
    localStorage.setItem(ThemeService.STORAGE_KEY, themeId);
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