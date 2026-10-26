import { Component, OnInit, effect, inject, input, output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { debounceTime, distinctUntilChanged, merge } from 'rxjs';
import {
    ALL_PRIORITIES,
    ALL_STATUSES,
    PRIORITY_LABELS,
    STATUS_LABELS,
} from '../../core/utils/display.utils';
import { RequestPriority, RequestStatus } from '../../core/models/request.models';
import { EMPTY_FILTERS, FilterValue } from '../../core/models/request-filters.model';

// Emits a normalized filter object.
// Text inputs are debounced here (300ms) to avoid a request per keystroke.
// Selects and date pickers emit immediately - a click/drag is already a
// deliberate action. The parent must NOT debounce again.
@Component({
    selector: 'app-request-filters',
    standalone: true,
    imports: [
        ReactiveFormsModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatCheckboxModule,
        MatDatepickerModule,
        MatButtonModule,
        MatIconModule,
    ],
    template: `
        <form [formGroup]="form" class="filters" dir="rtl">
            <mat-form-field appearance="outline" class="search-field">
                <mat-label>חיפוש חופשי</mat-label>
                <input
                    matInput
                    formControlName="search"
                    placeholder="כותרת פנייה או שם ארגון"
                />
                <mat-icon matSuffix>search</mat-icon>
            </mat-form-field>

            <mat-form-field appearance="outline">
                <mat-label>סטטוס</mat-label>
                <mat-select formControlName="statuses" multiple>
                    @for (status of allStatuses; track status) {
                        <mat-option [value]="status">{{ statusLabels[status] }}</mat-option>
                    }
                </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline">
                <mat-label>עדיפות</mat-label>
                <mat-select formControlName="priority">
                    <mat-option [value]="null">הכל</mat-option>
                    @for (priority of allPriorities; track priority) {
                        <mat-option [value]="priority">{{ priorityLabels[priority] }}</mat-option>
                    }
                </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline">
                <mat-label>שם ארגון</mat-label>
                <input matInput formControlName="organizationName" />
            </mat-form-field>

            <mat-form-field appearance="outline">
                <mat-label>מטפל</mat-label>
                <input matInput formControlName="assignedTo" />
            </mat-form-field>

            <mat-form-field appearance="outline">
                <mat-label>מתאריך</mat-label>
                <input matInput [matDatepicker]="fromPicker" formControlName="fromDate" />
                <mat-datepicker-toggle matIconSuffix [for]="fromPicker" />
                <mat-datepicker #fromPicker />
            </mat-form-field>

            <mat-form-field appearance="outline">
                <mat-label>עד תאריך</mat-label>
                <input matInput [matDatepicker]="toPicker" formControlName="toDate" />
                <mat-datepicker-toggle matIconSuffix [for]="toPicker" />
                <mat-datepicker #toPicker />
            </mat-form-field>

            <button mat-stroked-button type="button" (click)="reset()" class="reset-btn">
                <mat-icon>clear</mat-icon>
                ניקוי סינון
            </button>
        </form>
    `,
    styles: [
        `
            .filters {
                display: grid;
                grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
                gap: 8px 16px;
                align-items: start;
                margin-bottom: 8px;
            }
            .search-field {
                grid-column: span 2;
            }
            .reset-btn {
                align-self: center;
                margin-bottom: 22px;
            }
        `,
    ],
})
export class RequestFiltersComponent implements OnInit {
    private readonly fb = inject(FormBuilder);

    readonly filterChanged = output<FilterValue>();

    readonly initialValue = input<FilterValue | null>(null);

    readonly allStatuses = ALL_STATUSES;
    readonly allPriorities = ALL_PRIORITIES;
    readonly statusLabels = STATUS_LABELS;
    readonly priorityLabels = PRIORITY_LABELS;

    readonly form = this.fb.group({
        search: [''],
        statuses: [[] as RequestStatus[]],
        priority: [null as RequestPriority | null],
        organizationName: [''],
        assignedTo: [''],
        fromDate: [null as Date | null],
        toDate: [null as Date | null],
    });

    constructor() {
        // emitEvent:false is critical — otherwise restoring the value would
        // fire filterChanged and trigger a redundant request.
        effect(() => {
            const value = this.initialValue();
            if (value) this.form.patchValue(value, { emitEvent: false });
        });
    }

    ngOnInit(): void {
        // Text inputs: debounced (300ms) + distinct check on the normalized value.
        const debounced$ = merge(
            this.form.controls.search.valueChanges,
            this.form.controls.organizationName.valueChanges,
            this.form.controls.assignedTo.valueChanges,
        ).pipe(
            debounceTime(300),
            distinctUntilChanged((prev, curr) => prev === curr),
        );

        // Selects and dates: emit on the user's action, no artificial delay.
        const immediate$ = merge(
            this.form.controls.statuses.valueChanges,
            this.form.controls.priority.valueChanges,
            this.form.controls.fromDate.valueChanges,
            this.form.controls.toDate.valueChanges,
        );

        merge(debounced$, immediate$).subscribe(() => this.filterChanged.emit(this.normalized()));
    }

    reset(): void {
        this.form.reset(EMPTY_FILTERS);
    }

    private normalized(): FilterValue {
        const v = this.form.getRawValue();
        return {
            search: v.search ?? '',
            statuses: v.statuses ?? [],
            priority: v.priority ?? null,
            organizationName: v.organizationName ?? '',
            assignedTo: v.assignedTo ?? '',
            fromDate: v.fromDate,
            toDate: v.toDate,
        };
    }
}