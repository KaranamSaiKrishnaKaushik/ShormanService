import { Component } from '@angular/core';

@Component({
  selector: 'app-footer',
  standalone: true,
  template: `
    <footer class="footer">
      <div class="footer-container">
        <div class="footer-brand">
          <span class="brand-icon">🛒</span>
          <strong>Shorman Service</strong>
          <span class="footer-tagline">Fresh groceries, delivered fast.</span>
        </div>
        <div class="footer-links">
          <span>© {{ year }} Shorman Service. All rights reserved.</span>
        </div>
      </div>
    </footer>
  `,
  styles: [`
    .footer {
      background: #1B5E20;
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
    }
    .brand-icon { font-size: 1.3rem; }
    .footer-tagline {
      color: #A5D6A7;
      font-size: 0.85rem;
    }
    .footer-links {
      font-size: 0.85rem;
      color: #A5D6A7;
    }
  `]
})
export class FooterComponent {
  year = new Date().getFullYear();
}
