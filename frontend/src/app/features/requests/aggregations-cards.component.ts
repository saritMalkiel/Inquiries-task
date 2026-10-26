import { Component, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { AggregationsDto } from '../../core/models/request.models';
import { PRIORITY_LABELS, STATUS_LABELS } from '../../core/utils/display.utils';
import { CommonModule } from '@angular/common';

// Pure presentational component: receives the aggregations and renders cards.
@Component({
  selector: 'app-aggregations-cards',
  standalone: true,
  imports: [MatCardModule, MatProgressBarModule,CommonModule],
  template: `
    @if (loading()) {
      <mat-progress-bar mode="indeterminate"></mat-progress-bar>
    }

    @if (data(); as agg) {
      <div class="agg-grid">
        <mat-card appearance="outlined" class="agg-card total">
          <mat-card-content>
            <span class="agg-label">סך פניות</span>
            <span class="agg-value">{{ agg.totalRequests | number }}</span>
          </mat-card-content>
        </mat-card>

        @for (entry of statusEntries(); track entry.key) {
          <mat-card appearance="outlined" class="agg-card">
            <mat-card-content>
              <span class="agg-label">{{ entry.label }}</span>
              <span class="agg-value">{{ entry.value | number }}</span>
            </mat-card-content>
          </mat-card>
        }

        @for (entry of priorityEntries(); track entry.key) {
          <mat-card appearance="outlined" class="agg-card priority">
            <mat-card-content>
              <span class="agg-label">עדיפות {{ entry.label }}</span>
              <span class="agg-value">{{ entry.value | number }}</span>
            </mat-card-content>
          </mat-card>
        }
      </div>
    }
  `,
  styles: [
    `
      .agg-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
        gap: 12px;
        margin-bottom: 20px;
      }
      .agg-card mat-card-content {
        display: flex;
        flex-direction: column;
        gap: 4px;
        padding: 12px 16px;
      }
      .agg-label {
        font-size: 0.85rem;
        color: #666;
      }
      .agg-value {
        font-size: 1.5rem;
        font-weight: 600;
      }
      .total .agg-value {
        color: #1565c0;
      }
      .priority .agg-value {
        font-size: 1.25rem;
      }
    `,
  ],
})
export class AggregationsCardsComponent {
  readonly data = input<AggregationsDto | null>(null);
  readonly loading = input(false);

  // Values are mapped to Hebrew labels; unknown keys fall back to the raw key
  // so a new server-side status never breaks the UI.
  statusEntries() {
    const byStatus = this.data()?.byStatus ?? {};
    return Object.entries(byStatus).map(([key, value]) => ({
      key,
      value,
      label: STATUS_LABELS[key as keyof typeof STATUS_LABELS] ?? key,
    }));
  }

  priorityEntries() {
    const byPriority = this.data()?.byPriority ?? {};
    return Object.entries(byPriority).map(([key, value]) => ({
      key,
      value,
      label: PRIORITY_LABELS[key as keyof typeof PRIORITY_LABELS] ?? key,
    }));
  }
}