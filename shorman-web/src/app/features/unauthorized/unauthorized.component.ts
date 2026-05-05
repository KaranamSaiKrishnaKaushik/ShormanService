import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="unauthorized-shell">
      <div class="unauthorized-card">
        <span class="eyebrow">Access Restricted</span>
        <h1>You do not have permission to open this page.</h1>
        <p>Your current role does not include this area. Use the menu to return to pages assigned to you.</p>
        <div class="actions">
          <a routerLink="/" class="btn-primary">Go To My Start Page</a>
          <a routerLink="/products" class="btn-secondary">Customer Area</a>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .unauthorized-shell {
      min-height: calc(100vh - 64px);
      display: grid;
      place-items: center;
      padding: 2rem;
      background:
        radial-gradient(circle at top left, rgba(255, 153, 0, 0.18), transparent 36%),
        radial-gradient(circle at bottom right, rgba(46, 125, 50, 0.18), transparent 30%),
        #f7f9f4;
    }
    .unauthorized-card {
      width: min(720px, 100%);
      background: rgba(255, 255, 255, 0.94);
      border: 1px solid #e4e8db;
      border-radius: 24px;
      padding: 2.5rem;
      box-shadow: 0 20px 50px rgba(0, 0, 0, 0.08);
    }
    .eyebrow {
      display: inline-block;
      margin-bottom: 0.85rem;
      font-size: 0.82rem;
      letter-spacing: 0.12em;
      text-transform: uppercase;
      color: #a45a00;
      font-weight: 700;
    }
    h1 {
      margin: 0 0 0.75rem;
      font-size: clamp(1.8rem, 4vw, 2.6rem);
      line-height: 1.1;
      color: #253128;
    }
    p {
      margin: 0;
      color: #536157;
      font-size: 1rem;
      line-height: 1.6;
    }
    .actions {
      display: flex;
      gap: 0.9rem;
      flex-wrap: wrap;
      margin-top: 1.5rem;
    }
    .btn-primary,
    .btn-secondary {
      text-decoration: none;
      padding: 0.8rem 1.15rem;
      border-radius: 999px;
      font-weight: 700;
      transition: transform 0.2s ease, box-shadow 0.2s ease;
    }
    .btn-primary {
      background: #174d2b;
      color: #fff;
      box-shadow: 0 10px 24px rgba(23, 77, 43, 0.22);
    }
    .btn-secondary {
      background: #fff;
      color: #174d2b;
      border: 1px solid #c9d7cb;
    }
    .btn-primary:hover,
    .btn-secondary:hover {
      transform: translateY(-1px);
    }
  `]
})
export class UnauthorizedComponent {}