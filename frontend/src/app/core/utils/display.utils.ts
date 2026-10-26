import { RequestPriority, RequestStatus } from '../models/request.models';

// Hebrew labels live in code rather than transloco for this screen's scope.
export const STATUS_LABELS: Record<RequestStatus, string> = {
  [RequestStatus.New]: 'חדשה',
  [RequestStatus.InProgress]: 'בטיפול',
  [RequestStatus.Waiting]: 'ממתינה',
  [RequestStatus.Completed]: 'הושלמה',
};

export const PRIORITY_LABELS: Record<RequestPriority, string> = {
  [RequestPriority.Low]: 'נמוכה',
  [RequestPriority.Medium]: 'בינונית',
  [RequestPriority.High]: 'גבוהה',
};

export const ALL_STATUSES = Object.values(RequestStatus);
export const ALL_PRIORITIES = Object.values(RequestPriority);

// Which transitions the UI offers. The server enforces the same rules —
// this only prevents the user from picking an option that would fail.
const ALLOWED_TRANSITIONS: Record<RequestStatus, RequestStatus[]> = {
  [RequestStatus.New]: [RequestStatus.InProgress, RequestStatus.Waiting],
  [RequestStatus.InProgress]: [RequestStatus.Waiting, RequestStatus.Completed],
  [RequestStatus.Waiting]: [RequestStatus.InProgress, RequestStatus.Completed],
  [RequestStatus.Completed]: [],
};

export function allowedTransitions(from: RequestStatus): RequestStatus[] {
  return ALLOWED_TRANSITIONS[from] ?? [];
}

export function statusLabel(status: RequestStatus | null): string {
  return status ? STATUS_LABELS[status] : '—';
}