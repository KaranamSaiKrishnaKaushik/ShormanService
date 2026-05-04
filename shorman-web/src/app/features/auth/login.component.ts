import { Component, inject } from '@angular/core';
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
  errorMsg = '';

  submit(): void {
    this.loading = true;
    this.errorMsg = '';
    const ret = this.route.snapshot.queryParams['returnUrl'] || '/products';
    void this.auth.startLogin(ret).catch(() => {
      this.errorMsg = 'Unable to start Auth0 sign-in.';
      this.loading = false;
    });
  }

  loginWithGoogle(): void {
    this.submit();
  }
}
