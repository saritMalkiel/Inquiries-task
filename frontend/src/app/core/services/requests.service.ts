import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
    AggregationsDto,
    BulkStatusPayload,
    BulkStatusResult,
    PagedResult,
    RequestQuery,
    ServiceRequestDto,
    StatusHistoryDto,
    UpdateStatusPayload,
    UpdateStatusResult,
} from '../models/request.models';
import { environment } from '../../../environments/environment';

// שכבת גישה לנתונים בלבד. אין כאן לוגיקת UI — מי שקורא מחליט איך להציג שגיאות.
@Injectable({ providedIn: 'root' })
export class RequestsService {
    private readonly http = inject(HttpClient);
    private readonly baseUrl = `${environment.apiUrl}/requests`;

    getRequests(query: RequestQuery): Observable<PagedResult<ServiceRequestDto>> {
        return this.http.get<PagedResult<ServiceRequestDto>>(this.baseUrl, {
            params: this.toParams(query),
        });
    }

    getById(id: number): Observable<ServiceRequestDto> {
        return this.http.get<ServiceRequestDto>(`${this.baseUrl}/${id}`);
    }

    updateStatus(
        id: number,
        payload: UpdateStatusPayload,
    ): Observable<UpdateStatusResult> {
        return this.http.patch<UpdateStatusResult>(
            `${this.baseUrl}/${id}/status`,
            payload,
        );
    }

    bulkUpdateStatus(payload: BulkStatusPayload): Observable<BulkStatusResult> {
        return this.http.patch<BulkStatusResult>(
            `${this.baseUrl}/bulk-status`,
            payload,
        );
    }

    getHistory(id: number): Observable<StatusHistoryDto[]> {
        return this.http.get<StatusHistoryDto[]>(`${this.baseUrl}/${id}/history`);
    }

    getAggregations(): Observable<AggregationsDto> {
        return this.http.get<AggregationsDto>(`${this.baseUrl}/aggregations`);
    }

    // בונה HttpParams במפורש — מערכים נשלחים כ-CSV מופרד בפסיקים,
    // בהתאם לחוזה ה-API המתועד ב-README.
    private toParams(query: RequestQuery): HttpParams {
        let params = new HttpParams()
            .set('page', query.page ?? 1)
            .set('pageSize', query.pageSize ?? 25);

        if (query.search?.trim()) params = params.set('search', query.search.trim());
        if (query.status?.length) params = params.set('status', query.status.join(','));
        if (query.priority) params = params.set('priority', query.priority);
        if (query.organizationName?.trim())
            params = params.set('organizationName', query.organizationName.trim());
        if (query.assignedTo?.trim())
            params = params.set('assignedTo', query.assignedTo.trim());
        if (query.fromDate) params = params.set('fromDate', query.fromDate);
        if (query.toDate) params = params.set('toDate', query.toDate);
        if (query.sortBy) params = params.set('sortBy', query.sortBy);
        if (query.sortDir) params = params.set('sortDir', query.sortDir);

        return params;
    }
}