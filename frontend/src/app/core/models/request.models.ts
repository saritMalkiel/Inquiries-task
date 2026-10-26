// Enums mirror the backend values exactly (string enums on the wire).
export enum RequestStatus {
    New = 'New',
    InProgress = 'InProgress',
    Waiting = 'Waiting',
    Completed = 'Completed',
}

export enum RequestPriority {
    Low = 'Low',
    Medium = 'Medium',
    High = 'High',
}

// A single request row as returned by the API.
export interface ServiceRequestDto {
    id: number;
    title: string;
    organizationName: string;
    status: RequestStatus;
    priority: RequestPriority;
    assignedTo: string | null;
    createdAt: string;
    updatedAt: string;
    rowVersion: string;
}

// Generic paged envelope. Kept generic so it can be reused by other endpoints.
export interface PagedResult<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
}

// Query object sent to GET /api/requests. All fields optional — the server
// only applies filters that were actually provided.
export interface RequestQuery {
    page: number;
    pageSize: number;
    search?: string;
    status?: RequestStatus[];
    priority?: RequestPriority;
    organizationName?: string;
    assignedTo?: string;
    fromDate?: string; // ISO date (yyyy-MM-dd)
    toDate?: string;
    sortBy?: RequestSortField;
    sortDir?: 'asc' | 'desc';
}

export type RequestSortField =
    | 'createdAt'
    | 'updatedAt'
    | 'priority'
    | 'title'
    |'status';

// Body of PATCH /api/requests/{id}/status
export interface UpdateStatusPayload {
    status: RequestStatus;
    rowVersion: string;
}

export interface UpdateStatusResult {
    id: number;
    status: RequestStatus;
    updatedAt: string;
    rowVersion: string;
}

// Body / response for the bulk endpoint (partial-success semantics).
export interface BulkStatusPayload {
    ids: number[];
    status: RequestStatus;
    rowVersions: Record<number, string>;
}

export interface BulkItemResult {
    id: number;
    success: boolean;
    errorCode: 'NotFound' | 'Conflict' | 'InvalidTransition' | null;
}

export interface BulkStatusResult {
    succeeded: number;
    failed: number;
    items: BulkItemResult[];
}

// One audit row from GET /api/requests/{id}/history
export interface StatusHistoryDto {
    requestId: number;
    previousStatus: RequestStatus | null;
    newStatus: RequestStatus;
    changedAt: string;
    changedBy: string;
}

// Aggregations endpoint payload.
export interface AggregationsDto {
    totalRequests: number;
    byStatus: Record<string, number>;
    byPriority: Record<string, number>;
    generatedAt: string;
}