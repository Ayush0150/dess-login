import { Routes } from '@angular/router';
import { Login } from './pages/login/login';
import { authGuard } from './guards/auth-guard';

export const routes: Routes = [
  { path: 'login', title: 'Login | DESS', component: Login },
  {
    path: 'dashboard',
    title: 'Dashboard | DESS',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/dashboard/dashboard').then((module) => module.Dashboard),
  },
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: '**', redirectTo: 'dashboard' },
];
