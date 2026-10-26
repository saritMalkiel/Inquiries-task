import { Component, inject, model } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { RequestStatus, ServiceRequestDto } from '../../core/models/request.models';
import { allowedTransitions, statusLabel } from '../../core/utils/display.utils';

export interface StatusUpdateDialogData {
  request: ServiceRequestDto;
}

// Lets the user pick a new status; only legal transitions are offered.
@Component({
  selector: 'app-status-update-dialog',
  standalone: true,
  imports: [FormsModule, MatDialogModule, MatFormFieldModule, MatSelectModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>עדכון סטטוס</h2>
    <mat-dialog-content dir="rtl">
      <p class="req-title">{{ data.request.title }}</p>
      <p class="req-meta">
        סטטוס נוכחי: <strong>{{ currentLabel }}</strong>
      </p>

      <mat-form-field appearance="outline" class="full-width">
        <mat-label>סטטוס חדש</mat-label>
        <mat-select [(ngModel)]="selected">
          @for (status of transitions; track status) {
            <mat-option [value]="status">{{ labels[status] }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      @if (!transitions.length) {
        <p class="no-transitions">לא ניתן לשנות סטטוס של פנייה שהושלמה.</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>ביטול</button>
      <button
        mat-flat-button
        color="primary"
        [disabled]="!selected"
        (click)="confirm()"
      >
        עדכן
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .full-width { width: 100%; margin-top: 8px; }
      .req-title { font-weight: 600; margin: 0 0 4px; }
      .req-meta { color: #666; margin: 0; }
      .no-transitions { color: #b3261e; margin-top: 8px; }
      mat-dialog-content { min-width: 340px; }
    `,
  ],
})
export class StatusUpdateDialogComponent {
  readonly data = inject<StatusUpdateDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<StatusUpdateDialogComponent>);

  readonly labels = {
    [RequestStatus.New]: 'חדשה',
    [RequestStatus.InProgress]: 'בטיפול',
    [RequestStatus.Waiting]: 'ממתינה',
    [RequestStatus.Completed]: 'הושלמה',
  } as Record<RequestStatus, string>;

  readonly transitions = allowedTransitions(this.data.request.status);
  readonly currentLabel = statusLabel(this.data.request.status);

  selected = model<RequestStatus | null>(null);

  confirm(): void {
    const value = this.selected();
    if (value) this.dialogRef.close(value);
  }
}