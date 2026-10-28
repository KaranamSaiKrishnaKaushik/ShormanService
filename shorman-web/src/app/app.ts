import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { NavbarComponent } from './shared/components/navbar/navbar.component';
import { FooterComponent } from './shared/components/footer/footer.component';
import { CartSidebarComponent } from './features/cart/cart-sidebar.component';
import { LanguageService } from './core/services/language.service';
import { ThemeService } from './core/services/theme.service';
import { CookieConsentService } from './core/services/cookie-consent.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, NavbarComponent, FooterComponent, CartSidebarComponent],
  template: `
    <app-navbar></app-navbar>
    <app-cart-sidebar></app-cart-sidebar>
    <main>
      <router-outlet></router-outlet>
    </main>
    <app-footer></app-footer>
  `,
  styles: [`
    :host { display: flex; flex-direction: column; min-height: 100vh; }
    main { flex: 1; display: flex; flex-direction: column; padding-top: var(--navbar-height); }
  `]
})
export class App implements OnInit {
  private readonly languageService = inject(LanguageService);
  private readonly themeService = inject(ThemeService);
  private readonly cookieConsent = inject(CookieConsentService);

  ngOnInit(): void {
    this.cookieConsent.init();
    this.themeService.initialize();
    void this.languageService.initialize();
  }
}
