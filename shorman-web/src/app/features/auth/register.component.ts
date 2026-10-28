import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { NgIf } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [RouterLink, NgIf, ReactiveFormsModule],
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.scss']
})
export class RegisterComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  loading = false;
  errorMsg = '';
  successMsg = '';
  showPassword = false;
  verificationPending = false;
  verificationCodePreview = '';
  pendingEmail = '';

  readonly verificationForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.minLength(6)]]
  });

  readonly registerForm = this.fb.nonNullable.group({
    firstName: ['', [Validators.required, Validators.maxLength(80)]],
    lastName: ['', [Validators.required, Validators.maxLength(80)]],
    email: ['', [Validators.required, Validators.email]],
    phone: [''],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });

  submit(): void {
    if (this.registerForm.invalid) {
      this.registerForm.markAllAsTouched();
      return;
    }

    this.loading = true;
    this.errorMsg = '';
    this.successMsg = '';

    this.auth.register(this.registerForm.getRawValue()).subscribe({
      next: response => {
        this.pendingEmail = response.email;
        this.verificationPending = response.verificationRequired;
        this.verificationCodePreview = response.verificationCode ?? '';
        this.successMsg = response.message;
        this.loading = false;
      },
      error: err => {
        this.errorMsg = err?.error?.message || 'Unable to create account. Please try again.';
        this.loading = false;
      }
    });
  }

  verifyEmail(): void {
    if (this.verificationForm.invalid || !this.pendingEmail) {
      this.verificationForm.markAllAsTouched();
      return;
    }

    this.loading = true;
    this.errorMsg = '';

    this.auth.verifyEmail({
      email: this.pendingEmail,
      code: this.verificationForm.controls.code.getRawValue()
    }).subscribe({
      next: async () => {
        this.successMsg = 'Email verified successfully. Redirecting...';
        this.loading = false;
        await this.auth.redirectAfterLogin(this.returnUrl);
      },
      error: err => {
        this.errorMsg = err?.error?.message || err?.message || 'Unable to verify the email code.';
        this.loading = false;
      }
    });
  }

  continueWithGoogle(): void {
    this.loading = true;
    this.errorMsg = '';
    this.successMsg = '';

    void this.auth.startGoogleLogin(this.returnUrl).catch(() => {
      this.errorMsg = 'Unable to start Google sign-in.';
      this.loading = false;
    });
  }

  get returnUrl(): string | undefined {
    const queryValue = this.route.snapshot.queryParams['returnUrl'];
    return typeof queryValue === 'string' && queryValue.trim().length > 0 ? queryValue : undefined;
  }

  hasError(controlName: 'firstName' | 'lastName' | 'email' | 'password', errorKey: string): boolean {
    const control = this.registerForm.controls[controlName];
    return !!control && control.touched && control.hasError(errorKey);
  }

  hasVerificationError(errorKey: string): boolean {
    const control = this.verificationForm.controls.code;
    return !!control && control.touched && control.hasError(errorKey);
  }
}
