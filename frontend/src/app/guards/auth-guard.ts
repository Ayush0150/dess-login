import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { Auth } from '../services/auth';

export const authGuard: CanActivateFn = () => {
  const router = inject(Router);
  return inject(Auth)
    .me()
    .pipe(
      map(() => true),
      catchError(() => of(router.createUrlTree(['/login']))),
    );
};
