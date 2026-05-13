import { Injectable } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { BehaviorSubject, firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly storageKey = 'app.language';
  private readonly fallbackLanguage = environment.defaultLanguage ?? 'en';
  private readonly supportedLanguages = environment.supportedLanguages ?? ['en'];
  private initializationPromise?: Promise<void>;

  private readonly currentLanguageSubject = new BehaviorSubject<string>(this.fallbackLanguage);
  readonly currentLanguage$ = this.currentLanguageSubject.asObservable();

  constructor(private readonly translate: TranslateService) {}

  async initialize(): Promise<void> {
    if (!this.initializationPromise) {
      this.initializationPromise = this.initializeInternal();
    }

    await this.initializationPromise;
  }

  async setLanguage(language: string): Promise<void> {
    const selected = this.isSupported(language) ? language : this.fallbackLanguage;
    await this.useLanguage(selected);
  }

  getCurrentLanguage(): string {
    return this.currentLanguageSubject.value;
  }

  getSupportedLanguages(): string[] {
    return [...this.supportedLanguages];
  }

  private async useLanguage(language: string): Promise<void> {
    await firstValueFrom(this.translate.use(language));
    this.currentLanguageSubject.next(language);
    localStorage.setItem(this.storageKey, language);
    document.documentElement.lang = language;
  }

  private async initializeInternal(): Promise<void> {
    this.translate.addLangs(this.supportedLanguages);
    this.translate.setFallbackLang(this.fallbackLanguage);

    const stored = localStorage.getItem(this.storageKey);
    const selected = this.isSupported(stored) ? stored : this.fallbackLanguage;
    await this.useLanguage(selected);
  }

  private isSupported(language: string | null): language is string {
    return !!language && this.supportedLanguages.includes(language);
  }
}
