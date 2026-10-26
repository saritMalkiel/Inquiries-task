import { Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [MatIconModule, MatButtonModule],
  template: `
    <div class="empty">
      <mat-icon class="empty-icon">{{ icon() }}</mat-icon>
      <h3>{{ title() }}</h3>
      <p>{{ message() }}</p>
      @if (actionLabel()) {
        <button mat-stroked-button (click)="actionTriggered.emit()">
          {{ actionLabel() }}
        </button>
      }
    </div>
  `,
  styles: [
    `
      .empty {
        display: flex;
        flex-direction: column;
        align-items: center;
        padding: 48px 16px;
        text-align: center;
        color: #666;
      }
      .empty-icon {
        font-size: 48px;
        width: 48px;
        height: 48px;
        margin-bottom: 8px;
        color: #9e9e9e;
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
export class EmptyStateComponent {
  readonly icon = input('inbox');
  readonly title = input('אין נתונים להצגה');
  readonly message = input('');
  readonly actionLabel = input<string | null>(null);
  readonly actionTriggered = output<void>();
}