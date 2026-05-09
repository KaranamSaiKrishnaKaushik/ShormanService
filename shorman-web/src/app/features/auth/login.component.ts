import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { NgIf } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [RouterLink, NgIf, ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  auth = inject(AuthService);
  private route = inject(ActivatedRoute);

  loading = false;
  resetLoading = false;
  errorMsg = '';
  successMsg = '';
  resetMsg = '';
  resetErrorMsg = '';
  resetCodePreview = '';
  showPassword = false;
  showResetPanel = false;

  readonly loginForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });

  readonly resetForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    code: ['', [Validators.required, Validators.minLength(6)]],
    newPassword: ['', [Validators.required, Validators.minLength(6)]]
  });

  async ngOnInit(): Promise<void> {
    await this.auth.ensureReady();

    if (this.auth.isLoggedIn) {
      await this.auth.redirectAfterLogin(this.returnUrl);
    }
  }

  get returnUrl(): string | undefined {
    const queryValue = this.route.snapshot.queryParams['returnUrl'];
    return typeof queryValue === 'string' && queryValue.trim().length > 0 ? queryValue : undefined;
  }

  get combinedErrorMsg(): string {
    return this.errorMsg || this.auth.authError || '';
  }

  submit(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.loading = true;
    this.auth.clearAuthError();
    this.errorMsg = '';
    this.successMsg = '';

    this.auth.login(this.loginForm.getRawValue()).subscribe({
      next: async () => {
        this.loading = false;
        await this.auth.redirectAfterLogin(this.returnUrl);
      },
      error: err => {
        this.errorMsg = err?.error?.message || err?.message || 'Unable to sign in. Please try again.';
        this.loading = false;
      }
    });
  }

  loginWithGoogle(): void {
    this.loading = true;
    this.errorMsg = '';
    this.successMsg = '';
    this.auth.clearAuthError();
    void this.auth.startGoogleLogin(this.returnUrl).catch(() => {
      this.loading = false;
    });
  }

  requestPasswordReset(): void {
    const emailControl = this.resetForm.controls.email;
    if (emailControl.invalid) {
      emailControl.markAsTouched();
      return;
    }

    this.resetLoading = true;
    this.resetErrorMsg = '';
    this.resetMsg = '';
    this.resetCodePreview = '';

    this.auth.requestPasswordReset({ email: emailControl.getRawValue() }).subscribe({
      next: response => {
        this.resetMsg = response.message;
        this.resetCodePreview = response.resetCode ?? '';
        this.resetLoading = false;
      },
      error: err => {
        this.resetErrorMsg = err?.error?.message || err?.message || 'Unable to request a reset code.';
        this.resetLoading = false;
      }
    });
  }

  confirmPasswordReset(): void {
    if (this.resetForm.invalid) {
      this.resetForm.markAllAsTouched();
      return;
    }

    this.resetLoading = true;
    this.resetErrorMsg = '';
    this.resetMsg = '';

    this.auth.resetPassword(this.resetForm.getRawValue()).subscribe({
      next: response => {
        this.resetMsg = response.message;
        this.successMsg = 'Password updated. Sign in with your new password.';
        this.errorMsg = '';
        this.loginForm.controls.email.setValue(this.resetForm.controls.email.getRawValue());
        this.resetForm.controls.code.reset('');
        this.resetForm.controls.newPassword.reset('');
        this.resetLoading = false;
        this.showResetPanel = false;
      },
      error: err => {
        this.resetErrorMsg = err?.error?.message || err?.message || 'Unable to reset the password.';
        this.resetLoading = false;
      }
    });
  }

  toggleResetPanel(): void {
    this.showResetPanel = !this.showResetPanel;
    this.resetErrorMsg = '';
    this.resetMsg = '';
    this.resetCodePreview = '';

    if (this.loginForm.controls.email.value) {
      this.resetForm.controls.email.setValue(this.loginForm.controls.email.getRawValue());
    }
  }

  hasLoginError(controlName: 'email' | 'password', errorKey: string): boolean {
    const control = this.loginForm.controls[controlName];
    return !!control && control.touched && control.hasError(errorKey);
  }

  hasResetError(controlName: 'email' | 'code' | 'newPassword', errorKey: string): boolean {
    const control = this.resetForm.controls[controlName];
    return !!control && control.touched && control.hasError(errorKey);
  }
}
