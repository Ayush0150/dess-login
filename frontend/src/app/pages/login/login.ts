import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import { Auth } from '../../services/auth';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';

@Component({
  imports: [ReactiveFormsModule],
  selector: 'app-login',
  styleUrl: './login.css',
  templateUrl: './login.html',
})
export class Login {
  private auth = inject(Auth);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);
  loading = signal(false);
  error = signal('');
  showPassword = false;
  username = new FormControl('', { nonNullable: true, validators: [Validators.required] });
  password = new FormControl('', { nonNullable: true, validators: [Validators.required] });

  loginForm = new FormGroup({
    username: this.username,
    password: this.password,
  });

  onSubmit() {
    if (this.loading()) return;
    this.error.set('');
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.auth
      .login(this.username.value, this.password.value)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: () => {
          this.password.reset();
          void this.router.navigateByUrl('/dashboard', { info: 'login-success' });
        },
        error: (error: HttpErrorResponse) => {
          this.error.set(
            error.status === 401
              ? 'Incorrect username or password.'
              : error.status === 429
                ? 'Too many attempts. Please wait a minute and try again.'
                : 'Unable to sign in. Please try again.',
          );
        },
      });
  }
}
