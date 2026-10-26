import { Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-error-state',
  standalone: true,
  imports: [MatIconModule, MatButtonModule],
  template: `
    <div class="error">
      <mat-icon class="error-icon">error_outline</mat-icon>
      <h3>{{ title() }}</h3>
      <p>{{ message() }}</p>
      <button mat-flat-button color="primary" (click)="retry.emit()">
        נסה שוב
      </button>
    </div>
  `,
  styles: [
    `
      .error {
        display: flex;
        flex-direction: column;
        align-items: center;
        padding: 48px 16px;
        text-align: center;
        color: #b3261e;
      }
      .error-icon {
        font-size: 48px;
        width: 48px;
        height: 48px;
        margin-bottom: 8px;
      }
      h3 {
        margin: 0 0 6px;
        font-size: 1.1rem;
      }
      p {
        margin: 0 0 16px;
        max-width: 420px;
      }
    `,
  ],
})
export class ErrorStateComponent {
  readonly title = input('אירעה שגיאה');
  readonly message = input('לא ניתן היה לטעון את הנתונים.');
  readonly retry = output<void>();
}