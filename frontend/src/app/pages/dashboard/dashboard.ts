import { Component, DestroyRef, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { timer } from 'rxjs';
import { Auth } from '../../services/auth';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard {
  private auth = inject(Auth);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);
  user = this.auth.user;
  loggingOut = signal(false);
  error = signal('');
  loginSuccess = signal(this.router.currentNavigation()?.extras.info === 'login-success');

  constructor() {
    if (this.loginSuccess()) {
      timer(3500).pipe(takeUntilDestroyed()).subscribe(() => this.loginSuccess.set(false));
    }
  }

  logout() {
    if (this.loggingOut()) return;
    this.loggingOut.set(true);
    this.error.set('');
    this.auth.logout().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => void this.router.navigateByUrl('/login'),
      error: () => {
        this.loggingOut.set(false);
        this.error.set('Unable to log out. Please try again.');
      },
    });
  }
}
