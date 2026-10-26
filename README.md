# ניהול פניות — מערכת לניהול בקשות מארגונים ומעסיקים

## סקירה
מערכת Full Stack לניהול פניות: שליפה עם סינון/מיון/דפדוף בצד השרת, עדכון סטטוס עם
Optimistic Concurrency, היסטוריית שינויים, עדכון Bulk, נתונים מסכמים ו-Cache.
כוללת 100,000 רשומות בדיקה הניתנות ליצירה חוזרת.

## טכנולוגיות וגרסאות

| רכיב | טכנולוגיה | גרסה |
|---|---|---|
| Backend | ASP.NET Core Web API | 8.0 |
| ORM | Entity Framework Core (SqlServer) | 8.0 |
| Frontend | Angular | 19 |
| UI | Angular Material | 19 |
| Async | RxJS | 7.8 |
| Node | Node.js | 20 LTS |
| DB | SQL Server | 2022 |
| Containers | Docker / Docker Compose | 24+ |
| Tests | xUnit + FluentAssertions, WebApplicationFactory, Jasmine/Karma | 8.x / 17 |

## הרצה מלאה עם Docker (מומלץ)
```bash
docker compose up --build
```
- API: http://localhost:5000  (Swagger: http://localhost:5000/swagger)
- UI: http://localhost:4200
- SQL Server: localhost,1433 (`sa` / `Your_strong_Pass123`)

## Seed — יצירת נתוני בדיקה
```bash
curl.exe -X POST "http://localhost:5000/api/dev/seed?count=100000&reset=true"

```
- `count` — מספר הרשומות (ברירת מחדל 100,000)
- `reset=true` — מוחק נתונים קיימים לפני היצירה; הרצה חוזרת מחזירה מצב התחלתי זהה.

## הרצה מקומית בלי Docker (לפיתוח)
```bash
cd backend && dotnet run
cd frontend && npm start
```
ברירת מחדל מקומית: LocalDB. להצביע ל-SQL Server חיצוני:
```powershell
$env:ConnectionStrings__Db="Server=localhost,1433;Database=RequestsDb;User Id=sa;Password=Your_strong_Pass123;TrustServerCertificate=True"
```

## בדיקות
```bash
dotnet test tests\tests.csproj
```

## CI
מוגדר ב-`.github/workflows/ci.yml`:
1. `dotnet test` על כל הפרויקטים, כולל Integration Tests מול SQL Server כ-Service Container.
2. `npm ci` → `npm test` (ChromeHeadless) → `npm run build`.

## API

| Method | Route | תיאור |
|---|---|---|
| GET | `/api/requests` | שליפה עם דפדוף, סינון, חיפוש ומיון |
| GET | `/api/requests/{id}` | פנייה בודדת |
| PATCH | `/api/requests/{id}/status` | עדכון סטטוס (דורש `rowVersion`) |
| PATCH | `/api/requests/bulk-status` | עדכון סטטוס לעד 100 פניות |
| GET | `/api/requests/{id}/history` | היסטוריית שינויי סטטוס |
| GET | `/api/requests/aggregations` | נתונים מסכמים (Cache) |
| POST | `/api/dev/seed` | יצירת נתוני בדיקה |

שליפה:
```
GET /api/requests?page=1&pageSize=20&search=עיריית&status=New,InProgress
    &priority=High&assignedTo=&fromDate=2026-01-01&toDate=2026-12-31
    &sortBy=createdAt&sortDir=desc
```

עדכון סטטוס:
```json
{ "status": "InProgress", "rowVersion": "AAAAAAAAB9E=" }
```

## תכנון עבודה וחלוקה למשימות

האפיון פורק ל-4 Features, שמהן נגזרו 18 משימות. 

### מיפוי דרישות המבדק למשימות

| דרישה | סעיף | משימות | תוצר |
|---|---|---|---|
| שליפת נתונים | 1 | T02, T04, T05, T06, T07 | `GET /api/requests` |
| עדכון סטטוס ותחרותיות | 2 | T03, T08 | `PATCH /status` + 409 |
| היסטוריית שינויים | 3 | T09 | `GET /{id}/history` |
| פעולה Bulk | 4 | T10 | `PATCH /bulk-status` |
| Angular | 5 | T12–T15 | מסך רשימה + פרטים |
| ביצועים | 6 | T11, T17 | תוכניות ביצוע ואינדקסים |
| Cache | 7 | T16 | Aggregations במטמון |
| בדיקות אוטומטיות | 8 | T18–T19, T14ב | Unit + Integration |
| תכנון ואיכות | 9–10 | T01–T03, T20 | מבנה שכבות |
| תיעוד | 11 | T21, T22 | README + החלטות |

### Feature A — תשתית ומודל נתונים

| # | משימה | מה נדרש | תוצר | מאמץ | תלוי ב- |
|---|---|---|---|---|---|
| T01 | Bootstrap הפתרון | 3 פרויקטים (Api/Core/Infrastructure), DI, קונפיגורציה, Docker Compose | שלד עולה ומגיב ל-`/health` | 1 | — |
| T02 | מודל `ServiceRequest` | הישות, מזהים, Enums (`Status`, `Priority`), `RowVersion` | Migration ראשונה | 1 | T01 |
| T03 | תכנון בסיס הנתונים ואינדקסים | ניתוח תרחישי חיפוש, בחירת אינדקסים, Skript | מסמך החלטה + Migrations של אינדקסים | 1.5 | T02 |
| T04 | Seed ל-100,000 רשומות | יצירה ב-Bulk, אידמפוטנטית, עם `reset` | `POST /api/dev/seed` | 1 | T02 |

### Feature B — שכבת הקריאה

| # | משימה | מה נדרש | תוצר | מאמץ | תלוי ב- |
|---|---|---|---|---|---|
| T05 | חוזה ה-API ו-DTOs | Query object, PagedResult, RequestDto, Validation של Pirating | מסמך חוזה + DTOs | 1 | T02 |
| T06 | שכבת שליפה | סינון משולב, חיפוש טקסטואלי, מיון דינמי, דפדוף בשרת | `GET /api/requests` | 2.5 | T05 |
| T07 | Service מרוכז | מעבר מ-Controller לאכוף Validation, CancellationToken | שכבת Service עם בדיקות | 1.5 | T06 |
| T08 | עדכון סטטוס + Concurrency | RowVersion, מעברי סטטוס חוקיים, 404/409 | `PATCH /status` | 2 | T02 |
| T09 | Audit | טבלת היסטוריה, רישום בכל שינוי, Endpoint שליפה | `GET /{id}/history` | 1.5 | T08 |
| T10 | Bulk Partial Success | עד 100 פניות, דוח פר-פריט | `PATCH /bulk-status` | 2 | T08, T09 |
| T11 | Aggregations + Cache | שאילתות סיכום, `IMemoryCache`, Invalidation | `GET /aggregations` | 2 | T06, T08 |

### Feature C — Frontend

| # | משימה | מה נדרש | תוצר | מאמץ | תלוי ב- |
|---|---|---|---|---|---|
| T12 | שכבת נתונים | Models, Service, Interceptor, Error Handling | `core/` | 1.5 | T06 |
| T13 | מסך רשימה | טבלה, פילטרים, עימוד בשרת, Debounce, `switchMap` | `features/requests` | 3 | T12 |
| T14 | מצבי UI | Loading / Empty / Error / `409` | קומפוננטות משותפות + בדיקות | 1.5 | T13 |
| T15 | מסך פרטים והיסטוריה | צפייה, פתיחה במודל, Aggregations | `features/detail` | 2 | T13, T09 |

### Feature D — איכות, ביצועים והגשה

| # | משימה | מה נדרש | תוצר | מאמץ | תלוי ב- |
|---|---|---|---|---|---|
| T16 | ביצועים | תוכניות ביצוע על 100,000, Bottleneck, שיפורים | פרק ב-README | 1.5 | T06, T04 |
| T17 | בדיקות אוטומטיות | 4+ בדיקות: תקין, Validation, Conflict, Integration | `Tests.*` | 3 | T06–T10 |
| T18 | CI | GitHub Actions, SQL Server Service Container | `ci.yml` ירוק | 1 | T17 |
| T19 | תיעוד והגשה | README, החלטות, חלופות, מגבלות, שימוש ב-AI | README סופי + ריפו ציבורי | 1.5 | הכל |

### סדר ביצוע ותלויות

```
T01 → T02 → T03
        ├→ T04 → T16
        ├→ T05 → T06 → T07
        └→ T08 → T09 → T10 → T17
                     └→ T11
T06 → T12 → T13 → T14 → T15
T17 → T18 → T19
```

הנתיב הקריטי: **T01 → T02 → T05 → T06 → T08 → T09 → T10 → T17 → T19** (19 מנות).

### הערות
- מאמץ התיעוד נפרד בכוונה מ-T01; תיעוד נכתב לאורך העבודה ונסגר ב-T19.
- בדיקות ה-Frontend (T14) משולבות במשימה עצמה ולא ביחידה נפרדת, מפאת גודלן.
- ביצועים (T16) תלוי ב-Seed אמיתי — לא ניתן לתעד תוכנית ביצוע על נתונים מדומים.

## מבנה הפתרון
```
backend/
├─ RequestHub.Api/             Controllers, DTOs, Filters, Middleware
├─ RequestHub.Core/            Entities, Interfaces, Services, En
