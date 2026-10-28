import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CookieConsentService } from '../../../core/services/cookie-consent.service';

@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [RouterLink],
  template: `
    <footer class="footer">
      <div class="footer-container">
        <div class="footer-brand">
          <span class="app-icon brand-icon">local_mall</span>
          <strong>Shorman Service</strong>
          <span class="footer-tagline">Fresh groceries, delivered fast.</span>
        </div>
        <div class="footer-links">
          <a routerLink="/impressum">Impressum</a>
          <a routerLink="/datenschutz">Datenschutz</a>
          <a href="#" (click)="openCookies($event)">Cookie-Einstellungen</a>
          <span>© {{ year }} Shorman Service</span>
        </div>
      </div>
    </footer>
  `,
  styles: [`
    .footer {
      background: var(--color-primary-strong);
      color: #fff;
      margin-top: auto;
      padding: 1.5rem 0;
    }
    .footer-container {
      max-width: 1200px;
      margin: 0 auto;
      padding: 0 1rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    .footer-brand {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 1rem;
      font-family: var(--font-ui);
    }
    .brand-icon {
      font-size: 1.2rem;
      color: #fff4d8;
    }
    .footer-tagline {
      color: color-mix(in srgb, white 72%, var(--color-accent) 28%);
      font-size: 0.85rem;
    }
    .footer-links {
      font-size: 0.85rem;
      color: color-mix(in srgb, white 74%, var(--color-accent) 26%);
      font-family: var(--font-ui);
      display: flex;
      gap: 1rem;
      flex-wrap: wrap;
    }
    .footer-links a { color: inherit; text-decoration: underline; }
  `]
})
export class FooterComponent {
  private readonly cookies = inject(CookieConsentService);
  year = new Date().getFullYear();

  openCookies(event: Event): void {
    event.preventDefault();
    this.cookies.openPreferences();
  }
}
