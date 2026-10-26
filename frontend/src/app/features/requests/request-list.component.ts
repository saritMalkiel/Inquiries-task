import { Component, OnInit, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorIntl, MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import {
    RequestSortField,
    RequestStatus,
    ServiceRequestDto,
} from '../../core/models/request.models';
import { statusLabel } from '../../core/utils/display.utils';
import { EmptyStateComponent } from '../../shared/ui-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/ui-state/error-state.component';
import { AggregationsCardsComponent } from './aggregations-cards.component';
import { StatusUpdateDialogComponent } from './status-update-dialog.component';
import { HebrewPaginatorIntl } from '../../core/i18n/hebrew-paginator-intl';
import { RequestsService } from '../../core/services/requests.service';
import { RequestFiltersComponent } from './request-filter';
import { RequestListStore } from '../../core/store/request-list.store';
import { FilterValue } from '../../core/models/request-filters.model';

const SORT_FIELD_MAP: Record<string, RequestSortField> = {
    title: 'title',
    priority: 'priority',
    status: 'status',
    createdAt: 'createdAt',
    updatedAt: 'updatedAt',
};

@Component({
    selector: 'app-request-list',
    standalone: true,
    imports: [
        DatePipe,
        RouterLink,
        MatTableModule,
        MatSortModule,
        MatPaginatorModule,
        MatIconModule,
        MatButtonModule,
        MatProgressBarModule,
        MatTooltipModule,
        RequestFiltersComponent,
        AggregationsCardsComponent,
        EmptyStateComponent,
        ErrorStateComponent,
    ],
    providers: [{ provide: MatPaginatorIntl, useClass: HebrewPaginatorIntl }],
    template: `
        <h1 class="page-title">פניות</h1>

        <app-aggregations-cards
            [data]="store.aggregations()"
            [loading]="store.aggregationsLoading()"
        />

        <app-request-filters
            [initialValue]="store.filters()"
            (filterChanged)="onFilterChanged($event)"
        />

        @if (store.loading()) {
            <mat-progress-bar mode="indeterminate" class="loader" />
        }

        @if (store.error(); as err) {
            <app-error-state [message]="err" (retry)="store.retry()" />
        } @else if (!store.loading() && !store.items().length) {
            <app-empty-state
                icon="inbox"
                title="לא נמצאו פניות"
                message="לא נמצאו פניות המתאימות לסינון שהוגדר. נסו לשנות את תנאי החיפוש."
                actionLabel="ניקוי סינון"
                (actionTriggered)="store.resetFilters()"
            />
        } @else {
            <div class="table-wrap">
                <table
                    mat-table
                    [dataSource]="store.items()"
                    matSort
                    (matSortChange)="onSortChanged($event)"
                    class="full-width"
                >
                    <ng-container matColumnDef="title">
                        <th mat-header-cell *matHeaderCellDef mat-sort-header="title">כותרת</th>
                        <td mat-cell *matCellDef="let row">
                            <span class="cell-title">{{ row.title }}</span>
                        </td>
                    </ng-container>

                    <ng-container matColumnDef="organizationName">
                        <th mat-header-cell *matHeaderCellDef>ארגון</th>
                        <td mat-cell *matCellDef="let row">{{ row.organizationName }}</td>
                    </ng-container>

                    <ng-container matColumnDef="status">
                        <th mat-header-cell *matHeaderCellDef mat-sort-header="status">סטטוס</th>
                        <td mat-cell *matCellDef="let row">
                            <span class="badge" [class]="'badge-' + row.status">{{
                                labelFor(row.status)
                            }}</span>
                        </td>
                    </ng-container>

                    <ng-container matColumnDef="priority">
                        <th mat-header-cell *matHeaderCellDef mat-sort-header="priority">עדיפות</th>
                        <td mat-cell *matCellDef="let row">
                            <span class="prio" [class]="'prio-' + row.priority">{{
                                row.priority
                            }}</span>
                        </td>
                    </ng-container>

                    <ng-container matColumnDef="assignedTo">
                        <th mat-header-cell *matHeaderCellDef>מטפל</th>
                        <td mat-cell *matCellDef="let row">{{ row.assignedTo || '—' }}</td>
                    </ng-container>

                    <ng-container matColumnDef="createdAt">
                        <th mat-header-cell *matHeaderCellDef mat-sort-header="createdAt">נוצר</th>
                        <td mat-cell *matCellDef="let row">
                            {{ row.createdAt | date: 'dd/MM/yyyy HH:mm' }}
                        </td>
                    </ng-container>

                    <ng-container matColumnDef="updatedAt">
                        <th mat-header-cell *matHeaderCellDef mat-sort-header="updatedAt">עודכן</th>
                        <td mat-cell *matCellDef="let row">
                            {{ row.updatedAt | date: 'dd/MM/yyyy HH:mm' }}
                        </td>
                    </ng-container>

                    <ng-container matColumnDef="actions">
                        <th mat-header-cell *matHeaderCellDef>פעולות</th>
                        <td mat-cell *matCellDef="let row">
                            <button
                                mat-icon-button
                                color="primary"
                                matTooltip="עדכון סטטוס"
                                (click)="openStatusDialog(row)"
                            >
                                <mat-icon>edit_note</mat-icon>
                            </button>
                            <a
                                mat-icon-button
                                matTooltip="היסטוריית שינויים"
                                [routerLink]="['/requests', row.id, 'history']"
                            >
                                <mat-icon>history</mat-icon>
                            </a>
                        </td>
                    </ng-container>

                    <tr mat-header-row *matHeaderRowDef="columns"></tr>
                    <tr mat-row *matRowDef="let row; columns: columns"></tr>
                </table>
            </div>

            <mat-paginator
                [length]="store.totalCount()"
                [pageSizeOptions]="[10, 20, 50, 100]"
                [pageSize]="store.pageSize()"
                [pageIndex]="store.page() - 1"
                showFirstLastButtons
                (page)="onPageChanged($event)"
            />
        }
    `,
    styles: [
        `
            .page-title { margin: 0 0 20px; }
            .loader { margin-bottom: 12px; }
            .table-wrap { overflow-x: auto; background: #fff; border-radius: 8px; }
            .full-width { width: 100%; }
            .cell-title { font-weight: 500; }
            .badge {
                display: inline-block;
                padding: 2px 10px;
                border-radius: 12px;
                font-size: 0.8rem;
            }
            .badge-New { background: #e3f2fd; color: #0d47a1; }
            .badge-InProgress { background: #fff8e1; color: #8d6e00; }
            .badge-Waiting { background: #f3e5f5; color: #6a1b9a; }
            .badge-Completed { background: #e8f5e9; color: #1b5e20; }
            .prio { font-weight: 600; }
            .prio-High { color: #c62828; }
            .prio-Medium { color: #ef6c00; }
            .prio-Low { color: #2e7d32; }
        `,
    ],
})
export class RequestListComponent implements OnInit {
    private readonly dialog = inject(MatDialog);
    private readonly snackBar = inject(MatSnackBar);
    private readonly service = inject(RequestsService);

    readonly store: any = inject(RequestListStore);

    readonly columns = [
        'title',
        'organizationName',
        'status',
        'priority',
        'assignedTo',
        'createdAt',
        'updatedAt',
        'actions',
    ];

    ngOnInit(): void {
        this.store.load();
        this.store.loadAggregations();
    }

    onFilterChanged(value: FilterValue): void {
        this.store.setFilters(value);
    }

    onSortChanged(sort: Sort): void {
        this.store.setSort(
            sort.direction ? SORT_FIELD_MAP[sort.active] : undefined,
            sort.direction || undefined,
        );
    }

    onPageChanged(event: PageEvent): void {
        this.store.setPage(event.pageIndex + 1, event.pageSize);
    }

    labelFor(status: RequestStatus): string {
        return statusLabel(status);
    }

    openStatusDialog(row: ServiceRequestDto): void {
        this.dialog
            .open(StatusUpdateDialogComponent, {
                data: { request: row },
                width: '420px',
                direction: 'rtl',
            })
            .afterClosed()
            .subscribe((newStatus: RequestStatus | undefined) => {
                if (!newStatus) return;

                this.service
                    .updateStatus(row.id, { status: newStatus, rowVersion: row.rowVersion })
                    .subscribe({
                        next: () => {
                            this.snackBar.open('הסטטוס עודכן בהצלחה.', 'סגירה', { duration: 3500 });
                            this.store.reload();
                            this.store.loadAggregations();
                        },
                        error: (err: HttpErrorResponse) => {
                            if (err.status === 409) {
                                this.snackBar.open(
                                    'הפנייה עודכנה על ידי משתמש אחר. הנתונים רועננו — בדקו ונסו שוב.',
                                    'סגירה',
                                    { duration: 6000 },
                                );
                                this.store.reload();
                                this.store.loadAggregations();
                            } else if (err.status === 400) {
                                this.snackBar.open(
                                    'השינוי המבוקש אינו חוקי עבור פנייה זו.',
                                    'סגירה',
                                    { duration: 5000 },
                                );
                            } else if (err.status === 404) {
                                this.snackBar.open('הפנייה לא נמצאה.', 'סגירה', { duration: 5000 });
                                this.store.reload();
                            }
                        },
                    });
            });
    }
}