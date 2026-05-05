import { Component, inject } from '@angular/core';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { NgIf } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [RouterLink, NgIf],
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.scss']
})
export class RegisterComponent {
  private auth = inject(AuthService);
  private route = inject(ActivatedRoute);

  loading = false;
  errorMsg = '';

  submit(): void {
    this.loading = true;
    this.errorMsg = '';
    const ret = this.route.snapshot.queryParams['returnUrl'] || '/';
    void this.auth.startSignup(ret).catch(() => {
      this.errorMsg = 'Unable to start Auth0 sign-up.';
      this.loading = false;
    });
  }
}
