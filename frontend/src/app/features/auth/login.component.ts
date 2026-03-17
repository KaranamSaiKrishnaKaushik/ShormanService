import { Component, inject } from '@angular/core';
import { FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { NgIf } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, NgIf],
  template: `
    <div class="auth-page">
      <div class="auth-card">
        <div class="auth-header">
          <div class="auth-logo">🛒</div>
          <h1>Welcome Back</h1>
          <p>Sign in to your Shorman account</p>
        </div>

        <form [formGroup]="form" (ngSubmit)="submit()">
          <div class="form-group">
            <label>Email</label>
            <input type="email" formControlName="email" placeholder="your@email.com" class="form-control"
              [class.is-invalid]="form.get('email')?.invalid && form.get('email')?.touched" />
            <div class="invalid-feedback" *ngIf="form.get('email')?.invalid && form.get('email')?.touched">
              Valid email is required
            </div>
          </div>

          <div class="form-group">
            <label>Password</label>
            <div class="input-wrapper">
              <input [type]="showPwd ? 'text' : 'password'" formControlName="password" placeholder="••••••••" class="form-control"
                [class.is-invalid]="form.get('password')?.invalid && form.get('password')?.touched" />
              <button type="button" class="toggle-pwd" (click)="showPwd = !showPwd">
                {{ showPwd ? '🙈' : '👁️' }}
              </button>
            </div>
            <div class="invalid-feedback" *ngIf="form.get('password')?.invalid && form.get('password')?.touched">
              Password is required
            </div>
          </div>

          <div class="error-msg" *ngIf="errorMsg">{{ errorMsg }}</div>

          <button type="submit" class="btn-primary" [disabled]="loading">
            <span *ngIf="loading" class="btn-spinner"></span>
            {{ loading ? 'Signing in…' : 'Sign In' }}
          </button>
        </form>

        <div class="divider"><span>or</span></div>

        <button class="btn-google" (click)="loginWithGoogle()">
          <span class="google-icon">G</span>
          Continue with Google
        </button>

        <div class="auth-footer">
          Don't have an account? <a routerLink="/register">Create one</a>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .auth-page {
      min-height: calc(100vh - 128px);
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(135deg, #E8F5E9 0%, #F1F8E9 100%);
      padding: 2rem 1rem;
    }
    .auth-card {
      background: #fff;
      border-radius: 16px;
      padding: 2.5rem 2rem;
      width: 100%;
      max-width: 420px;
      box-shadow: 0 8px 32px rgba(0,0,0,0.12);
    }
    .auth-header { text-align: center; margin-bottom: 2rem; }
    .auth-logo { font-size: 3rem; margin-bottom: 0.5rem; }
    .auth-header h1 { font-size: 1.75rem; font-weight: 800; color: #1a1a1a; margin: 0 0 0.25rem; }
    .auth-header p { color: #666; font-size: 0.95rem; margin: 0; }
    .form-group { margin-bottom: 1.25rem; }
    .form-group label { display: block; font-weight: 600; color: #333; margin-bottom: 0.4rem; font-size: 0.9rem; }
    .form-control {
      width: 100%;
      padding: 0.65rem 0.9rem;
      border: 1.5px solid #ddd;
      border-radius: 8px;
      font-size: 0.95rem;
      transition: border-color 0.2s;
      box-sizing: border-box;
    }
    .form-control:focus { outline: none; border-color: #2E7D32; box-shadow: 0 0 0 3px rgba(46,125,50,0.1); }
    .form-control.is-invalid { border-color: #dc3545; }
    .invalid-feedback { color: #dc3545; font-size: 0.8rem; margin-top: 0.25rem; }
    .input-wrapper { position: relative; }
    .input-wrapper .form-control { padding-right: 2.5rem; }
    .toggle-pwd {
      position: absolute; right: 0.6rem; top: 50%; transform: translateY(-50%);
      background: none; border: none; cursor: pointer; font-size: 1.1rem; padding: 0;
    }
    .error-msg { background: #FFF3F3; color: #dc3545; padding: 0.75rem 1rem; border-radius: 8px; margin-bottom: 1rem; font-size: 0.9rem; border: 1px solid #f5c6cb; }
    .btn-primary {
      width: 100%;
      background: #2E7D32;
      color: #fff;
      border: none;
      padding: 0.75rem;
      border-radius: 8px;
      font-size: 1rem;
      font-weight: 700;
      cursor: pointer;
      transition: background 0.2s;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
    }
    .btn-primary:hover:not(:disabled) { background: #1B5E20; }
    .btn-primary:disabled { opacity: 0.6; cursor: not-allowed; }
    .btn-spinner {
      width: 16px; height: 16px;
      border: 2px solid rgba(255,255,255,0.4);
      border-top-color: #fff;
      border-radius: 50%;
      animation: spin 0.7s linear infinite;
    }
    @keyframes spin { to { transform: rotate(360deg); } }
    .divider { text-align: center; margin: 1.25rem 0; color: #aaa; font-size: 0.85rem; position: relative; }
    .divider::before, .divider::after { content: ''; display: inline-block; width: 40%; height: 1px; background: #eee; vertical-align: middle; margin: 0 0.5rem; }
    .btn-google {
      width: 100%;
      background: #fff;
      border: 1.5px solid #ddd;
      padding: 0.7rem;
      border-radius: 8px;
      font-size: 0.95rem;
      font-weight: 600;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      transition: all 0.2s;
      color: #333;
    }
    .btn-google:hover { background: #f8f8f8; border-color: #bbb; }
    .google-icon {
      width: 22px; height: 22px;
      background: #4285F4; color: #fff;
      border-radius: 50%; display: flex;
      align-items: center; justify-content: center;
      font-weight: 900; font-size: 0.85rem;
    }
    .auth-footer { text-align: center; margin-top: 1.5rem; font-size: 0.9rem; color: #666; }
    .auth-footer a { color: #2E7D32; font-weight: 600; text-decoration: none; }
    .auth-footer a:hover { text-decoration: underline; }
  `]
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required]
  });

  loading = false;
  errorMsg = '';
  showPwd = false;

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading = true;
    this.errorMsg = '';
    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => {
        const ret = this.route.snapshot.queryParams['returnUrl'] || '/products';
        this.router.navigateByUrl(ret);
      },
      error: (err) => {
        this.errorMsg = err?.error?.message || 'Invalid email or password.';
        this.loading = false;
      }
    });
  }

  loginWithGoogle(): void {
    alert('Google OAuth integration coming soon!');
  }
}
