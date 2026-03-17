import { Component, inject } from '@angular/core';
import { FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NgIf } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, NgIf],
  template: `
    <div class="auth-page">
      <div class="auth-card">
        <div class="auth-header">
          <div class="auth-logo">🛒</div>
          <h1>Create Account</h1>
          <p>Join Shorman for fresh grocery delivery</p>
        </div>

        <form [formGroup]="form" (ngSubmit)="submit()">
          <div class="form-row">
            <div class="form-group">
              <label>First Name</label>
              <input type="text" formControlName="firstName" placeholder="John" class="form-control"
                [class.is-invalid]="form.get('firstName')?.invalid && form.get('firstName')?.touched" />
              <div class="invalid-feedback" *ngIf="form.get('firstName')?.invalid && form.get('firstName')?.touched">Required</div>
            </div>
            <div class="form-group">
              <label>Last Name</label>
              <input type="text" formControlName="lastName" placeholder="Doe" class="form-control"
                [class.is-invalid]="form.get('lastName')?.invalid && form.get('lastName')?.touched" />
              <div class="invalid-feedback" *ngIf="form.get('lastName')?.invalid && form.get('lastName')?.touched">Required</div>
            </div>
          </div>

          <div class="form-group">
            <label>Email</label>
            <input type="email" formControlName="email" placeholder="john@example.com" class="form-control"
              [class.is-invalid]="form.get('email')?.invalid && form.get('email')?.touched" />
            <div class="invalid-feedback" *ngIf="form.get('email')?.invalid && form.get('email')?.touched">Valid email is required</div>
          </div>

          <div class="form-group">
            <label>Phone (optional)</label>
            <input type="tel" formControlName="phone" placeholder="+49 123 456789" class="form-control" />
          </div>

          <div class="form-group">
            <label>Password</label>
            <div class="input-wrapper">
              <input [type]="showPwd ? 'text' : 'password'" formControlName="password" placeholder="Min. 6 characters" class="form-control"
                [class.is-invalid]="form.get('password')?.invalid && form.get('password')?.touched" />
              <button type="button" class="toggle-pwd" (click)="showPwd = !showPwd">
                {{ showPwd ? '🙈' : '👁️' }}
              </button>
            </div>
            <div class="invalid-feedback" *ngIf="form.get('password')?.invalid && form.get('password')?.touched">
              Password must be at least 6 characters
            </div>
          </div>

          <div class="error-msg" *ngIf="errorMsg">{{ errorMsg }}</div>
          <div class="success-msg" *ngIf="successMsg">{{ successMsg }}</div>

          <button type="submit" class="btn-primary" [disabled]="loading">
            <span *ngIf="loading" class="btn-spinner"></span>
            {{ loading ? 'Creating account…' : 'Create Account' }}
          </button>
        </form>

        <div class="auth-footer">
          Already have an account? <a routerLink="/login">Sign in</a>
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
      max-width: 480px;
      box-shadow: 0 8px 32px rgba(0,0,0,0.12);
    }
    .auth-header { text-align: center; margin-bottom: 2rem; }
    .auth-logo { font-size: 3rem; margin-bottom: 0.5rem; }
    .auth-header h1 { font-size: 1.75rem; font-weight: 800; color: #1a1a1a; margin: 0 0 0.25rem; }
    .auth-header p { color: #666; font-size: 0.95rem; margin: 0; }
    .form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
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
    .success-msg { background: #F1F8F1; color: #2E7D32; padding: 0.75rem 1rem; border-radius: 8px; margin-bottom: 1rem; font-size: 0.9rem; border: 1px solid #a5d6a7; }
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
    .auth-footer { text-align: center; margin-top: 1.5rem; font-size: 0.9rem; color: #666; }
    .auth-footer a { color: #2E7D32; font-weight: 600; text-decoration: none; }
    .auth-footer a:hover { text-decoration: underline; }
    @media (max-width: 480px) { .form-row { grid-template-columns: 1fr; } }
  `]
})
export class RegisterComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);

  form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phone: [''],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });

  loading = false;
  errorMsg = '';
  successMsg = '';
  showPwd = false;

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading = true;
    this.errorMsg = '';
    const val = this.form.getRawValue();
    this.auth.register({
      firstName: val.firstName,
      lastName: val.lastName,
      email: val.email,
      password: val.password,
      phone: val.phone || undefined
    }).subscribe({
      next: () => this.router.navigate(['/products']),
      error: (err) => {
        this.errorMsg = err?.error?.message || 'Registration failed. Please try again.';
        this.loading = false;
      }
    });
  }
}
