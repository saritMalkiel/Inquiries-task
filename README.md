# ניהול פניות — מערכת לניהול בקשות מארגונים ומעסיקים

## סקירה

מערכת Full Stack לניהול פניות: שליפה עם סינון/מיון/דפדוף בצד השרת, עדכון סטטוס עם
Optimistic Concurrency (אכיפה בבסיס הנתונים דרך `rowVersion`), היסטוריית שינויים (Audit),
עדכון Bulk עם Partial Success, נתונים מסכמים ב-Cache ו-Invalidation.
כוללת 100,000 רשומות בדיקה הניתנות ליצירה חוזרת בצורה דטרמיניסטית.

## טכנולוגיות וגרסאות

| רכיב | טכנולוגיה | גרסה |
|---|---|---|
| Backend | ASP.NET Core Web API | 8.0 |
| ORM | Entity Framework Core (SqlServer + InMemory לבדיקות) | 8.0 |
| Frontend | Angular | 19 |
| UI | Angular Material | 19 |
| Async | RxJS | 7.8 |
| Node | Node.js | 20 LTS |
| DB | SQL Server | 2022 |
| Containers | Docker / Docker Compose | 24+ |
| Backend Tests | xUnit + FluentAssertions + WebApplicationFactory | 8.x |
| Frontend Tests | Jasmine / Karma (ChromeHeadless) | 17 |

> כל פרויקטי ה-Backend (כולל פרויקט הבדיקות) הם **`net8.0`** וכלולים ב-`requests-docker.sln`.

## דרישות מוקדמות

| תרחיש | נדרש |
|---|---|
| Docker | Docker Desktop / Docker Engine עם Docker Compose v2 |
| הרצה מקומית (Windows) | .NET 8 SDK, Node.js 20, SQL Server LocalDB או SQL Server |
| הרצה מקומית (Linux/macOS) | .NET 8 SDK, Node.js 20, SQL Server (אין LocalDB) |

---

## הרצה מלאה עם Docker (מומלץ)

```bash
docker compose up --build
```

| שירות | כתובת |
|---|---|
| API | http://localhost:5000 |
| Swagger | http://localhost:5000/swagger |
| UI | http://localhost:4200 |
| SQL Server | `localhost,1433` (`sa` / `Your_strong_Pass123`) |

מה קורה בהרצה:
1. `docker compose` בונה את `backend/Dockerfile` (multi-stage: restore → publish → runtime) ואת ה-Frontend.
2. ה-API ממתין ל-SQL Server (`depends_on: condition: service_healthy`).
3. ה-API קורא ל-`EnsureCreated()` בעליית התהליך — הסכימה נוצרת אוטומטית, כולל אינדקסים.
4. אם הסביבה היא `Development` והטבלה ריקה, ה-API מזריק אוטומטית את נתוני הבדיקה הראשוניים (100,000 רשומות, דטרמיניסטיים).
5. ה-UI נגיש ב-4200 ומצביע ל-`http://localhost:5000`.

משתני הסביבה ב-`docker-compose.yml`:

```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Development
  - ASPNETCORE_URLS=http://+:5000
  - ConnectionStrings__DefaultConnection=Server=sqlserver,1433;Database=RequestsDb;User Id=sa;Password=Your_strong_Pass123;TrustServerCertificate=True
```

> **שים לב:** מפתח משתנה הסביבה הוא `ConnectionStrings__DefaultConnection` — בדיוק כשם
> המפתח ב-`appsettings.json` (`ConnectionStrings:DefaultConnection`). אי-התאמה בשם המפתח
> תגרום לנפילה חוזרת ל-LocalDB ולכשל בהרצה ב-Docker.

### קבצי תשתית ב-`backend/`

| קובץ | תפקיד |
|---|---|
| `Dockerfile` | build רב-שלבי של ה-API |
| `.dockerignore` | מניעת העברת `bin/`, `obj/`, `node_modules/` להקשר ה-Build |
| `Properties/launchSettings.json` | `ASPNETCORE_ENVIRONMENT=Development`, `applicationUrl=http://localhost:5000`, `launchUrl=swagger` |

---

## Seed — יצירת נתוני בדיקה

במצב `Development` היישום מאתחל את מסד הנתונים אוטומטית אם הוא ריק, ולכן בדרך כלל אין צורך להפעיל את ה-Seed ידנית לאחר העלייה הראשונה.
ה-Endpoint עדיין קיים לשימוש ידני במקרים של ריסטר/איפוס מחדש של הנתונים:

```bash
curl.exe -X POST "http://localhost:5000/api/dev/seed?count=100000&reset=true"
```

| פרמטר | ברירת מחדל | תיאור |
|---|---|---|
| `count` | 100,000 | מספר הרשומות. מוגבל לערך מקסימלי מוגדר (`MaxSeedCount`) ומאומת ב-Validation |
| `reset` | `false` | `true` — מחיקת הנתונים הקיימים לפני היצירה |

ה-Seed **דטרמיניסטי**: משתמש ב-`Random(42)` וב**תאריך בסיס קבוע** (לא `DateTime.UtcNow`),
ולכן הרצה חוזרת עם `reset=true` מחזירה בדיוק את אותו מצב התחלתי.
ה-Endpoint פתוח **רק בסביבת `Development`** (בשאר הסביבות מחזיר 404).

> תזכורת: בהרצה הראשונה, אם הטבלה ריקה, השרת יתמלא אוטומטית בנתוני בדיקה. השימוש ב-`/api/dev/seed` מיועד בעיקר לאיפוס ידני של הסביבה או ליצירה חוזרת של מצב התחלתי.

---

## הרצה מקומית בלי Docker (לפיתוח)

דרישות: .NET 8 SDK · Node.js 20 · SQL Server (LocalDB זמין רק ב-Windows).

```bash
# Backend
cd backend && dotnet run

# Frontend (טרמינל נוסף)
cd frontend && npm install && npm start
```

- ה-API יעלה על `http://localhost:5000` (מתוך `launchSettings.json`), במצב `Development`, עם Swagger.
- ברירת מחדל מקומית: LocalDB.

למי שאין LocalDB (או להצביע ל-SQL Server חיצוני):

```powershell
# PowerShell
$env:ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=RequestsDb;User Id=sa;Password=Your_strong_Pass123;TrustServerCertificate=True"
```

```bash
# Bash
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=RequestsDb;User Id=sa;Password=Your_strong_Pass123;TrustServerCertificate=True"
```

אם תרצה לאפס את הנתונים ידנית, ניתן להפעיל שוב את ה-Seed:

```bash
curl.exe -X POST "http://localhost:5000/api/dev/seed?count=100000&reset=true"
```

---

## בדיקות

```bash
# כל הפתרון (Backend + Tests)
dotnet test requests-docker.sln

# Frontend
cd frontend && npm test
```

### מה נבדק (בדיקות אינטגרציה אמיתיות דרך `WebApplicationFactory`)

| תרחיש | סוג |
|---|---|
| שליפה עם סינון, מיון ודפדוף בצד השרת | Integration |
| קלט לא תקין (pageSize/validation) → `400` | Integration |
| פנייה שאינה קיימת → `404` | Integration |
| עדכון מתחרה (rowVersion ישן) → `409` | Integration (Concurrency) |
| עדכון סטטוס מוצלח + רישום Audit | Integration |
| Bulk מעל 100 → `400` | Integration |
| Bulk עם Partial Success (כשל בודד לא מפיל את האצווה) | Integration |
| Invalidation של ה-Cache לאחר עדכון | Integration |

הבדיקות רצות מול `IServiceProvider` אמיתי של ה-API. מכיוון ש-`InMemory` אינו מייצר
`rowversion`, מוגדר `SaveChangesInterceptor` שמדמה קידום גרסה בכל עדכון — כך שנאכפת
התנהגות Concurrency זהה לזו שב-SQL Server.

## CI — `.github/workflows/ci.yml`

1. `dotnet test requests-docker.sln --no-build` (SDK **8.0.x**) — כולל Integration Tests מול SQL Server ב-Service Container.
2. `npm ci` → `npm test` (ChromeHeadless) → `npm run build`.

---

## API

| Method | Route | תיאור |
|---|---|---|
| GET | `/api/requests` | שליפה עם דפדוף, סינון, חיפוש ומיון |
| GET | `/api/requests/{id}` | פנייה בודדת |
| PATCH | `/api/requests/{id}/status` | עדכון סטטוס — `rowVersion` **חובה** |
| PATCH | `/api/requests/bulk-status` | עדכון סטטוס לעד **100** פניות (Partial Success) |
| GET | `/api/requests/{id}/history` | היסטוריית שינויי סטטוס |
| GET | `/api/requests/aggregations` | נתונים מסכמים (Cache) |
| POST | `/api/dev/seed` | יצירת נתוני בדיקה — **Development בלבד** |

### שליפה

```
GET /api/requests?page=1&pageSize=20&search=עיריית&status=New,InProgress
    &priority=High&assignedTo=&organizationName=עיריית&fromDate=2026-01-01&toDate=2026-12-31
    &sortBy=createdAt&sortDir=desc
```

- `page` ≥ 1, `pageSize` מוגבל (ברירת מחדל 20, מקסימום מוגדר ומוחזר ב-`400` אם חורג).
- `sortBy`: `createdAt` · `updatedAt` · `priority` · `title` · `organizationName`; `sortDir`: `asc`/`desc`.
- כל הסינון, המיון והדפדוף מתבצעים בשאילתת SQL (`IQueryable`) — **לא** נטענות כלל הרשומות לזיכרון.
- ה-`CancellationToken` מועבר מהבקשה לאורך כל השרשרת (Controller → Service → EF).

### סטטוסים ומעברים חוקיים

`StatusTransitions` מוגדר לפי הסטטוסים של המבדק — `New`, `InProgress`, `Waiting`, `Completed` —
והוא **נקרא בפועל** הן ב-`UpdateStatusAsync` והן ב-Bulk. מעבר לא חוקי מחזיר `400` עם הודעה ברורה.

| מ- | אל |
|---|---|
| `New` | `InProgress`, `Waiting` |
| `InProgress` | `Waiting`, `Completed` |
| `Waiting` | `InProgress`, `Completed` |
| `Completed` | — (מצב סופי) |

### עדכון סטטוס בודד (Optimistic Concurrency)

```json
{ "status": "InProgress", "rowVersion": "AAAAAAAAB9E=" }
```

| מקרה | תשובה |
|---|---|
| פנייה לא קיימת | `404` |
| `rowVersion` חסר | `400` (ProblemDetails) |
| מעבר סטטוס לא חוקי | `400` (ProblemDetails עם הודעה) |
| `rowVersion` ישן | `409` — רק עדכון אחד מצליח, האחרים נדחים |
| הצלחה | `200` — `updatedAt` ו-`rowVersion` מתעדכנים בעקביות |

ה-`rowVersion` **חובה** בכל עדכון בודד, ולכן Lost Update אינו אפשרי. האכיפה מתבצעת ב-SQL Server.

### Bulk — Partial Success

```http
PATCH /api/requests/bulk-status
```

```json
{
  "ids": [1, 2, 3],
  "status": "InProgress",
  "rowVersions": { "1": "AAAAAAAAB9E=", "2": "AAAAAAAAB9E=" }
}
```

תשובה:

```json
{
  "updated": [1, 3],
  "failed": [
    { "id": 2, "reason": "Conflict" },
    { "id": 7, "reason": "NotFound" }
  ]
}
```

- מקסימום **100** פניות (`MaxBulkIds`) → חריגה מחזירה `400`.
- **הבחירה: Partial Success** — כל פנייה מעודכנת בטרנזקציה משל עצמה. פנייה עם Concurrency
  או מעבר לא חוקי מוחזרת ב-`failed` ולא מפילה את שאר האצווה. הנימוק: Bulk משמש לסגירה
  המונית של פניות, וכשל של רשומה בודדת (למשל מישהו עדכן אותה בינתיים) לא אמור למנוע
  עדכון של 99 פניות תקינות. הלקוח מקבל דוח פר-פריט ומציג אותו למשתמש.

### היסטוריית שינויים (Audit)

```json
[
  {
    "requestId": 12,
    "previousStatus": "New",
    "newStatus": "InProgress",
    "changedAt": "2026-10-27T09:15:00Z",
    "changedBy": "system"
  }
]
```

כל שינוי סטטוס — בודד או Bulk — רושם רשומת Audit בטבלת `StatusHistory` בתוך אותה טרנזקציה.

### Aggregations

`GET /api/requests/aggregations` מחזיר ספירות לפי סטטוס וספירות לפי עדיפות,
נשמר ב-`IMemoryCache` ל-`TtlSeconds` שניות ומתבטל (invalidation) בכל עדכון סטטוס, כולל Bulk.

### טיפול בשגיאות

כל השגיאות מוחזרות כ-`ProblemDetails` תקני (`AddProblemDetails()` + `UseExceptionHandler()`),
ללא חשיפת פרטים פנימיים. שגיאות Validation מחזירות `400`, ישויות חסרות `404`,
והתנגשויות `409`. לוגים נרשמים בנקודות משמעותיות ללא מידע רגיש.

---

## תכנון עבודה וחלוקה למשימות

האפיון פורק ל-4 Features ו-**19 משימות** (T01–T19).

### מיפוי דרישות המבדק למשימות

| דרישה (סעיף) | משימות | תוצר |
|---|---|---|
| שליפת נתונים (1) | T05, T06, T07 | `GET /api/requests` |
| עדכון סטטוס ותחרותיות (2) | T02, T08 | `PATCH /status` + `404/409` |
| היסטוריית שינויים (3) | T09 | `GET /{id}/history` |
| פעולה Bulk (4) | T10 | `PATCH /bulk-status` |
| Angular (5) | T12–T15 | מסך רשימה + פרטים |
| ביצועים (6) | T03, T16 | תוכניות ביצוע ואינדקסים |
| Cache (7) | T11 | Aggregations במטמון + Invalidation |
| בדיקות אוטומטיות (8) | T17, T14 | Integration + Unit |
| תכנון ואיכות קוד (9–10) | T01–T03, T18 | מבנה שכבות, DI, Error Handling |
| תיעוד והגשה (11) | T18, T19 | README + החלטות |

### Feature A — תשתית ומודל נתונים

| # | משימה | מה נדרש | תוצר | מאמץ (ימים) | תלוי ב- |
|---|---|---|---|---|---|
| T01 | Bootstrap הפתרון | 3 פרויקטים (Api/Core/Infrastructure), DI, קונפיגורציה, Dockerfile + Compose, `EnsureCreated` | שלד עולה ומגיב | 1.5 | — |
| T02 | מודל `ServiceRequest` | הישות, Enums (`Status`, `Priority`), `RowVersion`, טבלת `StatusHistory` | Migration ראשונה | 1 | T01 |
| T03 | תכנון בסיס הנתונים ואינדקסים | ניתוח תרחישי חיפוש, בחירת אינדקסים | Migrations של אינדקסים + פרק ב-README | 1.5 | T02 |
| T04 | Seed ל-100,000 רשומות | יצירה ב-Bulk, אידמפוטנטית, דטרמיניסטית, עם `reset` | `POST /api/dev/seed` | 1 | T02 |

### Feature B — שכבת הקריאה והעדכון

| # | משימה | מה נדרש | תוצר | מאמץ (ימים) | תלוי ב- |
|---|---|---|---|---|---|
| T05 | חוזה ה-API ו-DTOs | Query object, PagedResult, RequestDto, Validation של פרמטרים | מסמך חוזה + DTOs | 1 | T02 |
| T06 | שכבת שליפה | סינון משולב, חיפוש טקסטואלי, מיון דינמי, דפדוף בשרת, `CancellationToken` | `GET /api/requests` | 2.5 | T05 |
| T07 | Service מרוכז | מעבר מ-Controller ל-Service, אכיפת Validation | שכבת Service עם בדיקות | 1.5 | T06 |
| T08 | עדכון סטטוס + Concurrency | `RowVersion` חובה, `StatusTransitions`, `404/409` | `PATCH /status` | 2 | T02 |
| T09 | Audit | טבלת היסטוריה, רישום בכל שינוי, Endpoint שליפה | `GET /{id}/history` | 1.5 | T08 |
| T10 | Bulk Partial Success | עד 100 פניות, דוח פר-פריט, כשל בודד לא מפיל אצווה | `PATCH /bulk-status` | 2 | T08, T09 |
| T11 | Aggregations + Cache | שאילתות סיכום, `IMemoryCache`, Invalidation | `GET /aggregations` | 2 | T06, T08 |

### Feature C — Frontend

| # | משימה | מה נדרש | תוצר | מאמץ (ימים) | תלוי ב- |
|---|---|---|---|---|---|
| T12 | שכבת נתונים | Models, Service, Interceptor (כולל `ProblemDetails.detail`), Error Handling | `core/` | 1.5 | T06 |
| T13 | מסך רשימה | טבלה, פילטרים, עימוד בשרת, Debounce, **`Subject` + `switchMap`** | `features/requests` | 3 | T12 |
| T14 | מצבי UI + בדיקות | Loading / Empty / Error / `409`, קומפוננטות משותפות, בדיקות | קומפוננטות + specs | 1.5 | T13 |
| T15 | מסך פרטים והיסטוריה | צפייה, פתיחה במודל, Aggregations | `features/detail` | 2 | T13, T09 |

### Feature D — איכות, ביצועים והגשה

| # | משימה | מה נדרש | תוצר | מאמץ (ימים) | תלוי ב- |
|---|---|---|---|---|---|
| T16 | ביצועים | תוכניות ביצוע על 100,000 רשומות, Bottleneck, שיפורים | פרק ב-README | 1.5 | T06, T04 |
| T17 | בדיקות אוטומטיות | 8 תרחישי Integration דרך `WebApplicationFactory` + Interceptor ל-`rowversion` | פרויקט `Tests` | 3 | T06–T10 |
| T18 | CI והקשחת סביבה | GitHub Actions, SQL Server Service Container, הגבלת Dev/Performance ל-Development, Global Error Handling | `ci.yml` ירוק | 1 | T17 |
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

**הנתיב הקריטי:** T01 → T02 → T05 → T06 → T08 → T09 → T10 → T17 → T19 (18 ימי עבודה).
סה"כ מאמץ מוערך: **כ-36 ימי עבודה** (21 Backend, 8 Frontend, 7 איכות/ביצועים/תיעוד).

### הערות
- התיעוד נכתב לאורך העבודה ונסגר ב-T19.
- בדיקות ה-Frontend (T14) משולבות במשימה עצמה ולא ביחידה נפרדת, מפאת גודלן.
- ביצועים (T16) תלוי ב-Seed אמיתי — לא ניתן לתעד תוכנית ביצוע על נתונים מדומים.

---

## מבנה הפתרון

```
Inquiries-task/
├─ requests-docker.sln            # כולל Api, Core, Infrastructure ו-Tests
├─ docker-compose.yml
├─ backend/
│  ├─ Dockerfile
│  ├─ .dockerignore
│  ├─ Properties/launchSettings.json
│  ├─ RequestHub.Api/             # Controllers, DTOs, Filters, Middleware, Program.cs
│  ├─ RequestHub.Core/            # Entities, Enums, StatusTransitions, Interfaces, Services, DTOs
│  ├─ RequestHub.Infrastructure/  # AppDb, Migrations, Repositories, Seed, Cache
│  └─ docs/performance.sql        # סקריפט מדידת ביצועים
├─ tests/                         # xUnit + WebApplicationFactory (net8.0)
├─ frontend/
│  └─ src/app/
│     ├─ core/                    # models, services, interceptors
│     └─ features/
│        ├─ requests/             # list, filters, store (Subject + switchMap)
│        └─ detail/               # פרטים + היסטוריה
└─ .github/workflows/ci.yml
```

הפרדת שכבות: `Api` תלוי ב-`Core` ו-`Infrastructure`; `Core` מחזיק את הלוגיקה והחוזים ואינו
תלוי ב-EF; `Infrastructure` מממש את ה-Repositories. ה-API מחזיר **DTOs בלבד**, לא ישויות.

---

## החלטות טכנולוגיות

### 1. SQL Server + EF Core (מול MongoDB)

| חלופה | שיקולים |
|---|---|
| **SQL Server** (נבחר) | תמיכה מובנית ב-`rowversion` — אכיפת Optimistic Concurrency ברמת בסיס הנתונים, לא בקוד; אינדקסים מורכבים שמכסים סינון+מיון+דפדוף; Aggregations ב-`GROUP BY`; טרנזקציות ל-Bulk ול-Audit |
| MongoDB | גמישות סכימה, אך אין מקבילה טבעית ל-`rowversion` (נדרש `findOneAndUpdate` עם תנאי גרסה בכל כתיבה), ואין joins — ההיסטוריה הייתה נדרשת כשדה מוטמע עם בעיות גדילה |

ב-MongoDB, אלא אם כל כתיבה עוברת דרך פילטר גרסה, Lost Update חוזר להיות באחריות הקוד.
ב-SQL Server האכיפה מובנית: `WHERE Id = @id AND RowVersion = @rv`, ועדכון אפס שורות ⇒ `409`.

### 2. Bulk כ-Partial Success (מול Nothing-or-All)

| חלופה | שיקולים |
|---|---|
| **Partial Success** (נבחר) | דוח פר-פריט `{updated, failed:[{id, reason}]}`; פנייה מתנגשת לא מפילה 99 תקינות; מתאים לפעולה המונית על רשימה שנטענה לפני רגע |
| Nothing-or-All | טרנזקציה יחידה, עקביות מוחלטת — אך כשל יחיד (מישהו עדכן פנייה אחת בינתיים) מחזיר את כל האצווה, והמשתמש נאלץ לנסות שוב ושוב |

הבחירה נומקה מול תרחיש השימוש: Bulk לסגירת פניות בסוף יום. התוצאה מוחזרת עם פירוט מלא לכל פריט.

### 3. Cache ב-`IMemoryCache` על ה-Aggregations

נשמרים נתונים מסכמים בלבד (ספירות לפי סטטוס ועדיפות) — קריאה כבדה, נפח קטן, סובלנות מסוימת לעיכוב.
`Expiration`: `AbsoluteExpirationRelativeToNow` של `TtlSeconds` (מפתח `Aggregations:TtlSeconds` — **תואם לקוד**).
`Invalidation`: כל עדכון סטטוס (בודד או Bulk) מסיר את המפתח מה-Cache.

**Scale-out:** `IMemoryCache` הוא per-process. בהרצה עם מספר מופעי API, מופע A שמעדכן פנייה
לא יבטל את ה-Cache של מופע B, ולכן B יציג נתונים מסכמים מיושנים עד לפקיעת ה-TTL. לפתרון מלא
נדרש Cache משותף (Redis) או Pub/Sub לביטול. ה-TTL הקצר מגביל את חלון חוסר-העקביות.

---

## ביצועים

נמדד על 100,000 רשומות (Seed אמיתי, לא נתונים מדומים), עם `SET STATISTICS IO, TIME ON`
ו-Execution Plans. הסקריפט: `backend/docs/performance.sql`.

**הפעולה 1 — שליפה מסוננת ומדורגת:**

```sql
SELECT Id, Title, OrganizationName, Status, Priority, CreatedAt
FROM ServiceRequests
WHERE Status IN (...) AND Priority = ...
ORDER BY CreatedAt DESC
OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY;
```

- תוכנית: `Index Seek` על אינדקס מורכב + `Key Lookup`/`INCLUDE` מכסה, ללא `Sort` (האינדקס מסודר לפי `CreatedAt`).
- לפני האינדקס: `Clustered Index Scan` + `Sort` על כל הטבלה. אחרי: קריאת עמודים בודדים בלבד.

**הפעולה 2 — Aggregations (`GROUP BY Status`):**

- תוכנית: `Index Scan` על אינדקס צר המכסה את `Status` בלבד (Non-Clustered), ללא גישה לטבלה הבסיסית.
- לפני: `Table Scan` על כל העמודים הרוחביים.

**אינדקסים שנוספו:**

| אינדקס | עמודות | סיבת ההוספה |
|---|---|---|
| `IX_ServiceRequests_Status_CreatedAt` | `(Status, CreatedAt DESC) INCLUDE (Title, OrganizationName, Priority, AssignedTo)` | הסינון הנפוץ ביותר + דפדוף מדורג לפי תאריך; ה-INCLUDE מונע Key Lookup |
| `IX_ServiceRequests_OrganizationName` | `(OrganizationName) INCLUDE (Status, Priority, CreatedAt)` | סינון/חיפוש לפי ארגון |
| `IX_ServiceRequests_Priority_CreatedAt` | `(Priority, CreatedAt DESC)` | סינון+מיון לפי עדיפות |
| `IX_ServiceRequests_AssignedTo` | `(AssignedTo)` | "הפניות שלי" |
| `IX_StatusHistory_RequestId_ChangedAt` | `(RequestId, ChangedAt DESC)` | שליפת היסטוריה לפנייה בודדת |

**Bottleneck שזוהה:** חיפוש טקסטואלי חופשי ב-`Title`/`OrganizationName` עם `LIKE '%...%'` מוביל
ל-`Scan` ואינו משתמש באינדקס (leading wildcard).

**הצעת שיפור:** Full-Text Index על `Title` ו-`OrganizationName` (`CONTAINS`/`FREETEXT`), או `LIKE 'term%'`
כאשר מדובר בתחילית. בנוסף, טעינה ללא `AsNoTracking` מייקרת כל שליפה — הקריאות מוגדרות
`AsNoTracking`, וכל הדפדוף מתבצע ב-SQL (`Skip`/`Take`), כך שאף פעם לא נטענת קבוצת נתונים גדולה לזיכרון.

---

## Concurrency ו-Bulk

- `rowVersion` נשלח מהלקוח, נדרש בכל עדכון (ב`400` אם חסר), ומשמש כ-`WHERE` בעדכון ב-SQL Server.
- עדכון שמשפיע על 0 שורות ⇒ `409 Conflict` עם ה-`rowVersion` העדכני, והלקוח מרענן את הפנייה.
- `updatedAt` מתעדכן באותה כתיבה, ולכן עקבי עם הגרסה.
- כל עדכון סטטוס — בודד או Bulk — רושם Audit באותה טרנזקציה.
- בדיקות ה-Integration מאמתות: שני עדכונים עם אותו `rowVersion` — האחד `200`, האחר `409`.

---

## Cache, Invalidation ו-Scale-out — להרחבה

ראו בסעיף "החלטות טכנולוגיות" 3. בקצרה: `Aggregations` נשמר ב-`IMemoryCache` עם TTL מוגדר,
מתבטל בכל עדכון סטטוס (בודד/Bulk), ולכן לא מוצג מידע מיושן לאחר שינוי. בסביבת ריבוי מופעים
נדרש Backplane משותף (Redis) — מגבלה מוכרת ומתועדת.

---


## שימוש בכלי AI

- **נעשה שימוש ב-AI** לכתיבת שלד ה-README, לניסוח פרק תכנון העבודה, ולבדיקת קוד חוזרת (Code Review) על שכבת ה-Controller.
- **נבדק ושונה ידנית:** לוגיקת `StatusTransitions`, אכיפת ה-`rowVersion`, לוגיקת ה-Bulk עם Partial Success
  ואסטרטגיית ה-Invalidation — כל אלה נכתבו/תוקנו ידנית ונבדקו בבדיקות Integration.
- **נבדק ידנית:** תוכניות הביצוע והמספרים בפרק הביצועים הורצו בפועל על 100,000 רשומות.
- **נדחה מה-AI:** הצעות שהובילו ל-Over Engineering (למשל שכבת Repository נוספת מעל EF, או MediatR
  לשלוש פעולות) לא נכנסו לפתרון.
