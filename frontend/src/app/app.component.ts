import { Component } from '@angular/core';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule, RouterLink, RouterOutlet
  ],
  template: `
    <mat-toolbar color="primary" class="app-bar" dir="rtl">
      <span class="app-title">ניהול פניות</span>
      <span class="spacer"></span>
      <a mat-button routerLink="/home" routerLinkActive="active-link">דף הבית</a>
      <a mat-button routerLink="/requests" routerLinkActive="active-link">פניות</a>
    </mat-toolbar>

    <main class="app-content" dir="rtl">
      <router-outlet />
    </main>
  `,
  styles: [
    `
      .app-bar {
        position: sticky;
        top: 0;
        z-index: 10;
      }
      .app-title {
        font-weight: 600;
        font-size: 1.15rem;
      }
      .spacer {
        flex: 1 1 auto;
      }
      .active-link {
        background: rgba(255, 255, 255, 0.18);
        border-radius: 4px;
      }
      .app-content {
        padding: 24px;
        max-width: 1400px;
        margin: 0 auto;
      }
    `,
  ],
})
export class AppComponent {}