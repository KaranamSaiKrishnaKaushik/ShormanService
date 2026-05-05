import { Component, OnInit, inject } from '@angular/core';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { NgIf } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [RouterLink, NgIf],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  loading = false;

  async ngOnInit(): Promise<void> {
    await this.auth.ensureReady();

    if (this.auth.isLoggedIn) {
      await this.auth.redirectAfterLogin(this.returnUrl);
    }
  }

  get errorMsg(): string {
    return this.auth.authError ?? '';
  }

  get returnUrl(): string {
    return this.route.snapshot.queryParams['returnUrl'] || '/products';
  }

  submit(): void {
    this.loading = true;
    void this.auth.startGoogleLogin(this.returnUrl).catch(() => {
      this.loading = false;
    });
  }

  loginWithGoogle(): void {
    this.submit();
  }

  signup(): void {
    this.loading = true;
    void this.auth.startSignup(this.returnUrl).catch(() => {
      this.loading = false;
    });
  }
}
