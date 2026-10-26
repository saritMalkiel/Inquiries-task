// src/app/features/requests/request-filters.model.ts
import { RequestPriority, RequestStatus } from './request.models';

export interface FilterValue {
    search: string;
    statuses: RequestStatus[];
    priority: RequestPriority | null;
    organizationName: string;
    assignedTo: string;
    fromDate: Date | null;
    toDate: Date | null;
}

export const EMPTY_FILTERS: FilterValue = {
    search: '',
    statuses: [],
    priority: null,
    organizationName: '',
    assignedTo: '',
    fromDate: null,
    toDate: null,
};