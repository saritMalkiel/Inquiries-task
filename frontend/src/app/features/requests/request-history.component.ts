import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { of } from 'rxjs';
import { catchError, switchMap, tap } from 'rxjs/operators';
import { StatusHistoryDto } from '../../core/models/request.models';
import { statusLabel } from '../../core/utils/display.utils';
import { EmptyStateComponent } from '../../shared/ui-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/ui-state/error-state.component';
import { DatePipe } from '@angular/common';
import { RequestsService } from '../../core/services/requests.service';

// Standalone route (/requests/:id/history) so a history view is linkable
// and reaches its own URL — halaman not stuck in a dialog.
@Component({
  selector: 'app-request-history',
  standalone: true,
  imports: [
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    EmptyStateComponent,
    ErrorStateComponent,
    DatePipe
  ],
  template: `
    <div class="header" dir="rtl">
      <a mat-icon-button routerLink="/requests" matTooltip="חזרה לרשימה">
        <mat-icon>arrow_forward</mat-icon>
      </a>
      <h1>היסטוריית שינויים</h1>
    </div>

    @if (loading()) {
      <mat-progress-bar mode="indeterminate" />
    }

    @if (error()) {
      <app-error-state [message]="error()!" (retry)="load()" />
    } @else if (!loading() && !history().length) {
      <app-empty-state
        icon="history"
        title="אין היסטוריה"
        message="לפנייה זו טרם בוצעו שינויי סטטוס."
      />
    } @else {
      <mat-card appearance="outlined">
        <mat-card-content>
          <ol class="timeline">
            @for (entry of history(); track entry.changedAt + entry.newStatus) {
              <li class="timeline-item">
                <span class="dot"></span>
                <div class="entry-body">
                  <div class="entry-main">
                    <span class="transition">
                      {{ label(entry.previousStatus) }}
                      <mat-icon class="arrow">arrow_back</mat-icon>
                      <strong>{{ label(entry.newStatus) }}</strong>
                    </span>
                    <span class="entry-date">{{ entry.changedAt | date: 'dd/MM/yyyy HH:mm' }}</span>
                  </div>
                  <div class="entry-meta">בוצע על ידי: {{ entry.changedBy || '—' }}</div>
                </div>
              </li>
            }
          </ol>
        </mat-card-content>
      </mat-card>
    }
  `,
  styles: [
    `
      .header { display: flex; align-items: center; gap: 8px; margin-bottom: 16px; }
      .header h1 { margin: 0; }
      .timeline { list-style: none; margin: 0; padding: 8px 4px; }
      .timeline-item { display: flex; gap: 14px; padding: 14px 0; position: relative; }
      .timeline-item:not(:last-child)::before {
        content: '';
        position: absolute;
        right: 5px;
        top: 32px;
        bottom: -6px;
        width: 2px;
        background: #e0e0e0;
      }
      .dot {
        flex: 0 0 auto;
        width: 12px;
        height: 12px;
        margin-top: 6px;
        border-radius: 50%;
        background: #1565c0;
      }
      .entry-body { flex: 1; }
      .entry-main { display: flex; justify-content: space-between; align-items: center; gap: 12px; }
      .transition { display: flex; align-items: center; gap: 6px; }
      .arrow { font-size: 18px; width: 18px; height: 18px; color: #9e9e9e; }
      .entry-date { color: #666; font-size: 0.85rem; }
      .entry-meta { color: #888; font-size: 0.8rem; margin-top: 4px; }
    `,
  ],
})
export class RequestHistoryComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(RequestsService);

  readonly history = signal<StatusHistoryDto[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    // switchMap keeps the latest route param authoritative; navigating quickly
    // between requests cancels the previous in-flight call.
    this.route.paramMap
      .pipe(
        switchMap((params) => {
          const id = Number(params.get('id'));
          return this.service.getHistory(id).pipe(
            catchError((err: HttpErrorResponse) => {
              this.error.set(
                err.status === 404
                  ? 'הפנייה לא נמצאה.'
                  : 'אירעה שגיאה בטעינת ההיסטוריה.',
              );
              return of(null);
            }),
            tap((data:any) => {
              this.loading.set(false);
              if (data) this.history.set(data);
            }),
          );
        }),
      )
      .subscribe();
  }

  label(status: StatusHistoryDto['newStatus'] | null): string {
    return statusLabel(status);
  }
}