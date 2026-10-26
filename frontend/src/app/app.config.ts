import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { provideHttpClient, withInterceptors, withFetch } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideNativeDateAdapter, MAT_DATE_LOCALE } from '@angular/material/core';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { errorInterceptor } from './core/interceptors/error.interceptor';

import { routes } from './app.routes';
import { HebrewPaginatorIntl } from './core/i18n/hebrew-paginator-intl';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),

    provideRouter(
      routes,
      withComponentInputBinding(),
      withInMemoryScrolling({ scrollPositionRestoration: 'top', anchorScrolling: 'enabled' })
    ),

    provideHttpClient(
      withFetch(),
      withInterceptors([errorInterceptor])
    ),

    provideAnimationsAsync(),
    provideNativeDateAdapter(),

     { provide: MAT_DATE_LOCALE, useValue: 'he-IL' },

     { provide: MatPaginatorIntl, useClass: HebrewPaginatorIntl  },
  ],
};