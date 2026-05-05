import { Component } from '@angular/core';

@Component({
  selector: 'app-home-redirect',
  standalone: true,
  template: `
    <section class="redirect-shell">
      <div class="redirect-card">
        <h1>Preparing your workspace</h1>
        <p>Routing you to the right area for your role.</p>
      </div>
    </section>
  `,
  styles: [`
    .redirect-shell {
      min-height: calc(100vh - 64px);
      display: grid;
      place-items: center;
      padding: 2rem;
      background: linear-gradient(180deg, #f6fbf7 0%, #eef4ff 100%);
    }
    .redirect-card {
      background: rgba(255, 255, 255, 0.92);
      border: 1px solid #d8e6da;
      border-radius: 18px;
      padding: 2rem 2.25rem;
      box-shadow: 0 18px 40px rgba(36, 64, 53, 0.12);
      text-align: center;
    }
    h1 {
      margin: 0 0 0.5rem;
      color: #174d2b;
      font-size: 1.5rem;
    }
    p {
      margin: 0;
      color: #4d6658;
    }
  `]
})
export class HomeRedirectComponent {}