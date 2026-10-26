// src/app/features/requests/request-list.store.ts
import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import {
    AggregationsDto,
    PagedResult,
    RequestQuery,
    RequestSortField,
    ServiceRequestDto,
} from '../../core/models/request.models';
import { RequestsService } from '../../core/services/requests.service';
import { FilterValue } from '../models/request-filters.model';
 
export const EMPTY_FILTERS: FilterValue = {
    search: '',
    statuses: [],
    priority: null,
    organizationName: '',
    assignedTo: '',
    fromDate: null,
    toDate: null,
};

export interface RequestListState {
    items: ServiceRequestDto[];
    totalCount: number;
    page: number;
    pageSize: number;
    filters: FilterValue;
    sortBy: RequestSortField | undefined;
    sortDir: 'asc' | 'desc' | undefined;
    loading: boolean;
    error: string | null;
    aggregations: AggregationsDto | null;
    aggregationsLoading: boolean;
}

// Single source of truth for the requests list screen.
// Root scope, so filters/sort/page survive navigation between routes.
@Injectable({ providedIn: 'root' })
export class RequestListStore {
    private readonly service = inject(RequestsService);

    private readonly state = signal<RequestListState>({
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 20,
        filters: EMPTY_FILTERS,
        sortBy: undefined,
        sortDir: undefined,
        loading: false,
        error: null,
        aggregations: null,
        aggregationsLoading: false,
    });

    // ---- Read-only slices ----
    readonly items = computed(() => this.state().items);
    readonly totalCount = computed(() => this.state().totalCount);
    readonly page = computed(() => this.state().page);
    readonly pageSize = computed(() => this.state().pageSize);
    readonly filters = computed(() => this.state().filters);
    readonly loading = computed(() => this.state().loading);
    readonly error = computed(() => this.state().error);
    readonly aggregations = computed(() => this.state().aggregations);
    readonly aggregationsLoading = computed(() => this.state().aggregationsLoading);

    // ---- Actions ----

    setFilters(filters: FilterValue): void {
        this.patch({ filters, page: 1 });
        this.load();
    }

    setSort(by: RequestSortField | undefined, dir: 'asc' | 'desc' | undefined): void {
        this.patch({ sortBy: by, sortDir: dir, page: 1 });
        this.load();
    }

    setPage(page: number, pageSize: number): void {
        this.patch({ page, pageSize });
        this.load();
    }

    resetFilters(): void {
        this.patch({ filters: EMPTY_FILTERS, page: 1 });
        this.load();
    }

    reload(): void {
        this.load();
    }

    retry(): void {
        this.load();
        this.loadAggregations();
    }

    load(): void {
        this.patch({ loading: true, error: null });
        this.fetch(this.buildQuery()).subscribe();
    }

    loadAggregations(): void {
        this.patch({ aggregationsLoading: true });
        this.service
            .getAggregations()
            .pipe(
                catchError(() => of(null)),
                tap((data: AggregationsDto | null) => {
                    this.patch({
                        aggregationsLoading: false,
                        aggregations: data ?? this.state().aggregations,
                    });
                }),
            )
            .subscribe();
    }

    // ---- Internals ----

    private fetch(query: RequestQuery): Observable<PagedResult<ServiceRequestDto> | null> {
        return this.service.getRequests(query).pipe(
            catchError((err: HttpErrorResponse) => {
                this.patch({ loading: false, error: this.messageFor(err) });
                return of(null);
            }),
            tap((result: PagedResult<ServiceRequestDto> | null) => {
                if (!result) return;
                this.patch({
                    loading: false,
                    items: result.items,
                    totalCount: result.totalCount ?? 0,
                });
            }),
        );
    }

    private messageFor(err: HttpErrorResponse): string {
        return err.status === 0
            ? 'לא ניתן להתחבר לשרת. בדקו את החיבור ונסו שוב.'
            : 'אירעה שגיאה בטעינת הפניות.';
    }

    private buildQuery(): RequestQuery {
        const s = this.state();
        const f = s.filters;
        return {
            page: s.page,
            pageSize: s.pageSize,
            search: f.search || undefined,
            status: f.statuses.length ? f.statuses : undefined,
            priority: f.priority ?? undefined,
            organizationName: f.organizationName || undefined,
            assignedTo: f.assignedTo || undefined,
            fromDate: f.fromDate ? this.formatDate(f.fromDate) : undefined,
            toDate: f.toDate ? this.formatDate(f.toDate) : undefined,
            sortBy: s.sortBy,
            sortDir: s.sortDir,
        };
    }

    private formatDate(d: Date): string {
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, '0');
        const day = String(d.getDate()).padStart(2, '0');
        return `${y}-${m}-${day}`;
    }

    private patch(partial: Partial<RequestListState>): void {
        this.state.update((s) => ({ ...s, ...partial }));
    }
}