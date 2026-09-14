import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './register.component.html',
  styleUrl: '../login/login.component.scss',
})
export class RegisterComponent {
  companyName = '';
  displayName = '';
  email = '';
  password = '';
  readonly errorMessage = signal<string | null>(null);
  readonly isSubmitting = signal(false);

  constructor(private auth: AuthService, private router: Router) {}

  submit(): void {
    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    this.auth
      .register({ companyName: this.companyName, displayName: this.displayName, email: this.email, password: this.password })
      .subscribe({
        next: () => this.router.navigate(['/connections']),
        error: (err) => {
          this.isSubmitting.set(false);
          this.errorMessage.set(
            err.status === 409 ? 'An account with that email already exists.' : 'Could not create the account. Check your details and try again.'
          );
        },
      });
  }
}
