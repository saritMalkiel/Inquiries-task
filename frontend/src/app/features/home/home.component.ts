// src/app/features/home/home.component.ts
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink, MatCardModule, MatButtonModule, MatIconModule],
  template: `
    <div class="home">
      <h1>ברוכים הבאים</h1>
      <p>מערכת ניהול בקשות</p>
      <a mat-raised-button color="primary" routerLink="/requests">
        <mat-icon>list</mat-icon>
לרשימת הפניות</a>
    </div>
  `,
  styles: [`
    .home { padding: 24px; text-align: center; }
    h1 { margin-bottom: 8px; }
    p { color: #666; margin-bottom: 24px; }
  `]
})
export class HomeComponent { }