import { inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { switchMap, tap } from 'rxjs';

interface Session {
  username: string;
}

@Injectable({ providedIn: 'root' })
export class Auth {
  private http = inject(HttpClient);
  private session = signal<Session | null>(null);
  readonly user = this.session.asReadonly();

  login(username: string, password: string) {
    return this.csrf().pipe(
      switchMap(() =>
        this.http.post<{ message: string }>('/api/auth/login', { username, password }),
      ),
    );
  }

  me() {
    return this.http.get<Session>('/api/auth/me').pipe(tap((user) => this.session.set(user)));
  }

  logout() {
    return this.csrf().pipe(
      switchMap(() => this.http.post<{ message: string }>('/api/auth/logout', {})),
      tap(() => this.session.set(null)),
    );
  }

  private csrf() {
    return this.http.get('/api/auth/csrf');
  }
}
