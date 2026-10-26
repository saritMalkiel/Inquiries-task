import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      switch (error.status) {
        case 0:
          console.error('שגיאת רשת — אין חיבור לשרת', error);
          break;
        case 400:
          console.error('בקשה לא תקינה', error.error);
          break;
        case 401:
          router.navigate(['/login']);
          break;
        case 403:
          console.error('אין הרשאה לפעולה זו', error.error);
          break;
        case 404:
          console.error('הרשומה לא נמצאה', error.error);
          break;
        case 500:
        case 502:
        case 503:
          console.error('שגיאת שרת', error.error);
          break;
        default:
          console.error('שגיאה לא צפויה', error);
      }

      const message =
        (error.error && (error.error.message || error.error.title)) ||
        'אירעה שגיאה. נא לנסות שוב.';

      return throwError(() => ({ status: error.status, message }));
    }),
  );
};