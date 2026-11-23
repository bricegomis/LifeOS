# Backend notes

- .NET 10, solution `LifeOS.slnx`. Layers point inward:
  `LifeOS.Domain` (entities, invariants, parsing; no EF or I/O) →
  `LifeOS.Application` (use cases, ports) → `LifeOS.Infrastructure` (EF Core
  `LifeOSDbContext`, Npgsql, repositories, file storage) → `LifeOS.Api`
  (minimal API endpoints, DTOs, validation, auth, error handling).
- Modules (ADR 0009): `Households` (core: households, members, profiles), `Food`
  (foods, recipes, reviews, stores, products and prices, receipts), `Sports` (session
  templates, logged sessions, weekly goals, daily challenges; ADR 0013; exercises and
  daily routines and their passes; ADR 0014, ADR 0015), `Tasks` (todos with checklist, household routines, projects; ADR 0017), `Health` (supplements, symptoms, analyses, body measures, documents, appointments; ADR 0018). Each has a folder in every project: `Domain/Food/Recipes` (entities and
  rules), `Infrastructure/Food/Recipes` (EF mapping), `Api/Food/Recipes` (endpoints and
  DTOs). Plain management endpoints use the DbContext directly on their own module's
  tables; actions with real rules, or called by another module, are use cases in
  Application, testable without HTTP or a database. A module never touches another
  module's tables or entities; the household core is the only shared reference.
- Endpoints take a `HouseholdContext` parameter (`Api/Households`): it resolves the
  caller's household once (auto-provisioned on first sight). Filter every query and
  command on `household.HouseholdId` and reject cross-household IDs. Do not wrap
  domain calls in `try/catch`: the global handler turns `ArgumentException` into 400
  and PostgreSQL unique / foreign key violations into 409.
- `tests/LifeOS.Architecture.Tests` fails the build when a module references another
  module, when a module endpoint resolves the household by hand, or when
  Domain/Application reference EF or HTTP. Add a new module's name to its list when
  you create the folder.
- Files (receipt photos `/data/receipts`, food and recipe images `/data/foods`, exercise
  images `/data/sports`, todo images `/data/tasks`, health files `/data/health`, encrypted at rest with `HealthFiles:EncryptionKey`
  through `EncryptedFileStorage`) live on durable volumes, only served through authenticated,
  household-checked routes. The storage code is shared, outside the modules
  (`Infrastructure/Files`, `Api/Files`); each module declares its own storage class
  (`FoodImageStorage`, `ReceiptPhotoStorage`, `SportImageStorage`, `TaskImageStorage`).
- A receipt is only a stored photo (ADR 0010): no OCR, no lines, no purchases.
- Sport (ADR 0013): every row carries a household profile (`SportProfiles` resolves the
  main one, the household's first profile, when none is given). Reports are computed on
  read by the published contract `Application/Sports/Contracts/SportReadContract`
  (week reports, challenge streaks with jokers, sport days); "today" is the Paris day
  (`SportClock`). Calories are typed, never computed.
- Routines (ADR 0014, ADR 0015): a `Routine` owns dated versions (days of the week as bits,
  jokers, ordered lines with targets and an optional load, null = bodyweight); a change
  applies from today and past days are judged with the version they had. A routine is done
  in passes (`RoutineSession`, several per day, one unfinished at a time); an
  `ExerciseCompletion` is one exercise in one pass with its sets done and optional effort
  (1-10) and discomfort (0-10). A pass is finished, never stored, when every line of that
  day has all its sets; a finished pass is not reopened. A day is done once one pass is
  finished. Days that are not planned are skipped by the streak. Profile feeling defaults
  live in `SportProfileSettings`.
- Tasks (ADR 0017): a `Todo` belongs to a household profile (`TaskProfiles`, main profile by
  default) and is `open`, `done` (with its day) or `abandoned`; an open todo is late after its
  due day until it is done, postponed (an update with a new due day) or abandoned, nothing slides
  by itself. Its checklist is an owned, ordered list; ticking every sub-task does not complete
  it. "Today" is the Paris day (`TaskClock`). Search (`TodoLibrary.SearchAsync`) runs in memory
  over the household's todos, ignoring case and accents. A household routine is a `TaskRoutine` (fixed dates or a delay,
  `Recurrence`) with `RoutinePass`es (done or skipped, each closing one due day); the next due day,
  lateness, streak and record are computed on read by `RoutineSchedule`, never stored; one open due
  day at a time, a pause never makes it late (`ResumedOn`). A `TaskProject` groups todos
  (`Todo.ProjectId`, composite FK in the household) and may sit under a parent project
  (`ParentId`, same composite FK; a tree of any depth, `MoveUnder` refuses cycles); its progress is
  computed on read over its whole sub-tree (done todos over todos not abandoned) and closing it never
  touches its todos or sub-projects; a todo only joins a project in
  progress. Management code is `TodoLibrary`, `TaskRoutineLibrary` and `TaskProjectLibrary`
  (registered in `Api/Tasks/TasksModule.cs`).
- Health (ADR 0018): private to an account, not only a household. Every row carries
  `HouseholdId`, `OwnerUserId` and `MemberProfileId` (`HealthOwners` resolves them from the
  caller) and every query filters on household **and** account, so another member of the same
  household sees nothing (tested). Files and images go to `HealthFileStorage` (AES-GCM, key
  from the configuration, never in the database); without the key, uploads are refused. A
  `Supplement` has a daily dose and unit, a package, purchase links and a `SupplementPeriod`
  (all year, or a cure repeated after a pause; a week is 7 days, a month 30); one
  `SupplementIntake` per day taken; monthly cost and streak are computed on read. Symptoms:
  `SymptomType` (the owner's list, seeded with defaults on first read), `SymptomEntry` (1-10)
  and `CalmDay` ("rien à signaler", dropped when a symptom is noted that day). A `Marker` has a
  lab range, a personal target, a frequency (0 = once) and panels; an `Analysis` holds one value
  per marker; status, trend and next due day are computed on read. `MeasureType` (defaults on
  first read) and `Measurement`. A `HealthDocument` (type, title, day, doctor, notes) owns its
  encrypted files (photos or PDF, 20 at most); an analysis may point to it (set null on delete). An
  `Appointment` has a day, optional time, booking link (https; LifeOS never calls the site) and an
  optional report document. Management code: `SupplementLibrary`, `SymptomLibrary`,
  `AnalysisLibrary`, `HealthDocumentLibrary`, `AppointmentLibrary` (registered in `Api/Health/HealthModule.cs`).

## Migrations
- In `src/LifeOS.Infrastructure/Persistence/Migrations`, applied at API startup unless
  `SkipDatabaseMigration=true`. Each module owns a PostgreSQL schema (`households`,
  `food`, `sports`, `tasks`, `health`; OpenIddict uses `auth`); `public` only holds `__EFMigrationsHistory`.
- `FoodModuleReset` (roadmap step 1, ADR 0010) replaced the whole previous history:
  it drops every table of `public` with its data. Never edit it; add a new migration:
  `dotnet ef migrations add <Name> --project src/LifeOS.Infrastructure --startup-project src/LifeOS.Api --output-dir Persistence/Migrations`
- `AccountsAndOAuth` (ADR 0011) truncates every household and its data, once, when
  accounts move from Supabase to LifeOS.
- From now on migrations are additive and preserve IDs, prices and snapshots; rollback
  is by restoring a backup, not a destructive down-migration.

## Tests
- `dotnet test` from `src/backend`. `tests/LifeOS.Domain.Tests` are pure unit tests;
  `tests/LifeOS.Application.Tests` test use cases with in-memory fakes of their data ports.
  `tests/LifeOS.Api.IntegrationTests` use `WebApplicationFactory` + Testcontainers
  PostgreSQL (Docker required) with `ASPNETCORE_ENVIRONMENT=Testing` and
  `TestAuthHandler`: simulate users with `client.AsUser(guid)` (`X-Test-Sub` header).
- New household-scoped data needs an isolation test (see
  `HouseholdIsolationTests.cs`); removed features stay covered by
  `RemovedFeaturesTests.cs`.
- Filter example:
  `dotnet test tests/LifeOS.Api.IntegrationTests --filter 'FullyQualifiedName~ReceiptPhotoTests'`
- Local run: `dotnet run --project src/LifeOS.Api` (http://localhost:5292,
  Scalar at `/scalar/v1` in Development); needs a Postgres connection string
  (`ConnectionStrings__Postgres` or `DATABASE_URL`); signing in also needs the `Auth__Google__*`
  settings (docs/google-oauth.md).
- Auth (ADR 0011): the API is its own OAuth server (OpenIddict, `Api/Authentication`),
  Google sign-in, accounts in `households.users`, OAuth tables in schema `auth`. Clients
  are registered at startup in `OAuthClients` (`lifeos-web`) or just in time from a
  trusted Claude metadata document (`ClientMetadataDocuments`); never dynamic registration.
- MCP (`/mcp`, `Api/Assistant`): stateless HTTP transport, policy `Assistant` (tokens
  issued for the `/mcp` resource). Tools are `[McpServerToolType]` classes in their
  module's Api folder (e.g. `Food/Foods/FoodAssistantTools.cs`), resolve the
  household with `AssistantRequest.HouseholdAsync` and call the same code as the
  endpoints: Application use cases, or the module's management class shared with the
  endpoints (`FoodLibrary`, `RecipeLibrary`, `StoreLibrary`, `ProductLibrary`,
  `ReviewLibrary`, registered in `Api/Food/FoodModule.cs`; the Sport libraries in
  `Api/Sports/SportsModule.cs`; the Tasks libraries in `Api/Tasks/TasksModule.cs`, tools in
  `Api/Tasks/TaskAssistantTools.cs`, household routines named `*_task_routine` apart from the sport routines; the Health libraries in
  `Api/Health/HealthModule.cs`, tools in `Api/Health/HealthAssistantTools.cs`, the caller's own health only, PDF files
  returned as an embedded blob resource). Never query the DbContext in
  a tool when an endpoint already does the same thing. `ArgumentException` and unique/FK
  violations reach the assistant as readable errors (call-tool filter in
  `AssistantSetup`); not found is `AssistantRequest.NotFound`. Images by URL go through
  `IRemoteImageSource` (https, public addresses only, 10 MB, 20 s).
  Tool descriptions are French documentation, not prompts.
