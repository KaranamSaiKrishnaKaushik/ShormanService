import { Component } from '@angular/core';

@Component({
  selector: 'app-auth-callback',
  standalone: true,
  template: `
    <section class="callback-shell">
      <div class="callback-card">
        <div class="spinner"></div>
        <h1>Signing you in</h1>
        <p>Finalizing your session and redirecting you now.</p>
      </div>
    </section>
  `,
  styles: [`
    .callback-shell {
      min-height: calc(100vh - 64px);
      display: grid;
      place-items: center;
      padding: 2rem;
      background: linear-gradient(180deg, #f6fbf7 0%, #eef4ff 100%);
    }
    .callback-card {
      width: min(100%, 420px);
      background: rgba(255, 255, 255, 0.96);
      border: 1px solid #d8e6da;
      border-radius: 18px;
      padding: 2rem;
      text-align: center;
      box-shadow: 0 18px 40px rgba(36, 64, 53, 0.12);
    }
    .spinner {
      width: 42px;
      height: 42px;
      margin: 0 auto 1rem;
      border-radius: 50%;
      border: 4px solid rgba(46, 125, 50, 0.18);
      border-top-color: #2E7D32;
      animation: spin 0.8s linear infinite;
    }
    h1 {
      margin: 0 0 0.5rem;
      color: #174d2b;
      font-size: 1.45rem;
    }
    p {
      margin: 0;
      color: #4d6658;
    }
    @keyframes spin {
      to { transform: rotate(360deg); }
    }
  `]
})
export class AuthCallbackComponent {}