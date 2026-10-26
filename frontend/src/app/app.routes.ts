import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'home' },
  {
    path: 'home',
    // Lazy-loaded so the initial bundle stays small.
    loadComponent: () =>
      import('./features/home/home.component').then((m) => m.HomeComponent),
  },
  {
    path: 'requests',
    loadComponent: () =>
      import('./features/requests/request-list.component').then(
        (m) => m.RequestListComponent,
      ),
  },
  {
    path: 'requests/:id/history',
    loadComponent: () =>
      import('./features/requests/request-history.component').then(
        (m) => m.RequestHistoryComponent,
      ),
  },
  { path: '**', redirectTo: 'home' },
];