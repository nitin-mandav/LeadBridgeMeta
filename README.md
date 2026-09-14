# LeadBridge Meta

Meta Lead Ads → GoHighLevel integration. Multi-tenant: each tenant (agency) connects one or more Meta ad accounts
and one or more GHL sub-accounts (locations), maps Meta lead forms to a GHL location, and new form submissions are
pushed into GHL as contacts in real time via Meta's leadgen webhook.

## Stack

- **API**: ASP.NET Core 10 Web API (`src/LeadBridgeMeta.Api`), Clean-ish layering:
  - `LeadBridgeMeta.Domain` — entities only, no dependencies.
  - `LeadBridgeMeta.Application` — interfaces/contracts (`IMetaGraphClient`, `IGhlClient`, `ILeadProcessingService`, `IAppDbContext`, ...).
  - `LeadBridgeMeta.Infrastructure` — EF Core + SQL Server, ASP.NET Identity, the real Meta/GHL HTTP clients, JWT issuing, token encryption, the webhook background queue.
  - `LeadBridgeMeta.Api` — controllers, auth wiring, Program.cs.
- **Frontend**: Angular 19 standalone app (`web/`).
- **Database**: SQL Server via EF Core.

## Status

Verified working end-to-end in this environment: `dotnet build`, `dotnet ef` migrations, `dotnet run`, and the
Angular app registering/logging in against the real API backed by a real SQL Server database (Smart App Control
was blocking all of this earlier and has since been turned off). What's still needed before leads actually flow:
a real Meta App and GHL Marketplace app (see below) — the "Connect Meta account" / "Connect GHL location" buttons
work, but need real `Meta:AppId`/`Ghl:ClientId` etc. to redirect anywhere meaningful.

The database, migration, JWT signing key, and webhook verify token already exist locally (via `dotnet user-secrets`,
so the real key isn't in `appsettings.json` or git). If you ever need to redo this on another machine:

```bash
cd src/LeadBridgeMeta.Api
dotnet ef migrations add InitialCreate --project ../LeadBridgeMeta.Infrastructure --startup-project . --output-dir Persistence/Migrations
dotnet ef database update --project ../LeadBridgeMeta.Infrastructure --startup-project .
dotnet user-secrets init
dotnet user-secrets set "Jwt:SigningKey" "<a long random string, 32+ chars>"
dotnet user-secrets set "Meta:WebhookVerifyToken" "<a random string you also paste into the Meta dashboard>"
```

## Running it

### 1. API secrets

Real Meta/GHL credentials still need to be added once you've registered those apps (see below):

```bash
cd src/LeadBridgeMeta.Api
dotnet user-secrets set "Meta:AppId" "<your Meta App ID>"
dotnet user-secrets set "Meta:AppSecret" "<your Meta App Secret>"
dotnet user-secrets set "Ghl:ClientId" "<your GHL Marketplace app Client ID>"
dotnet user-secrets set "Ghl:ClientSecret" "<your GHL Marketplace app Client Secret>"
```

### 2. Run the API

```bash
cd src/LeadBridgeMeta.Api
dotnet run
```

Swagger UI is available at `/swagger` in development.

### 3. Run the frontend

```bash
cd web
npm start
```

Opens at `http://localhost:4200`. It's already wired to call the API at `https://localhost:5443/api`
(`web/src/environments/environment.development.ts`) — adjust if your API port differs.

## Connecting Meta and GHL (requires a public HTTPS URL)

Both Meta's OAuth redirect / webhook and GHL's OAuth redirect need to reach your API from the internet. For local
dev, run something like `ngrok http https://localhost:5443` and use the `https://<random>.ngrok-free.app` URL as
`App:ApiBaseUrl` in `appsettings.json` (or an override) instead of `https://localhost:5443`.

### Meta App setup (developers.facebook.com)

1. Create an app → type "Business". Add the **Facebook Login for Business** and **Webhooks** products.
2. Facebook Login → Settings → add `https://<public-host>/api/meta/callback` as a valid OAuth redirect URI.
3. Webhooks → Page → Callback URL = `https://<public-host>/api/webhooks/meta`, Verify token = whatever you set as
   `Meta:WebhookVerifyToken`. Subscribe to the **leadgen** field.
4. App Review: for production use with pages you don't personally admin, you need Advanced Access on
   `leads_retrieval`, `pages_show_list`, `pages_manage_metadata`, `pages_read_engagement`. Standard Access is
   enough to test with pages/ad accounts you already administer.
5. Copy the App ID / App Secret into the API's secrets (above).

### GHL Marketplace app setup (marketplace.gohighlevel.com/developer)

1. Create an app, choose **Sub-Account (Location)** distribution (or Agency, per your model).
2. Redirect URL = `https://<public-host>/api/ghl/callback`.
3. Scopes: `contacts.readonly`, `contacts.write`, `locations.readonly`.
4. Copy the Client ID / Client Secret into the API's secrets (above).
5. Each client's GHL sub-account clicks "Connect GHL location" in the app (or installs from the Marketplace),
   authorizes, and gets redirected back — that's the OAuth flow `GhlController` implements.

## How a lead flows through the system

1. Someone submits a Meta lead ad form → Meta POSTs to `POST /api/webhooks/meta` (signature-verified via
   `X-Hub-Signature-256`).
2. The webhook controller enqueues the notification (`IMetaWebhookQueue`) and returns `200` immediately — Meta
   expects a fast ack and retries aggressively otherwise.
3. `MetaWebhookProcessingWorker` (background service) dequeues it and calls `LeadProcessingService`, which:
   - Fetches the full lead answers from the Graph API using the Page's access token.
   - Looks up the form's field mappings (form-specific, falling back to the tenant's defaults, falling back to a
     small built-in heuristic for common keys like `email`/`phone_number`/`full_name`).
   - Upserts a GHL contact via `contacts/upsert`, refreshing the GHL access token first if it's near expiry.
   - Records the outcome (`LeadEvent`) either way, so failures are visible and retryable from the "Leads" screen.

## Tests

```bash
dotnet test tests/LeadBridgeMeta.Tests
```

21 tests, all passing: `AuthEndpointsTests` (integration, via `WebApplicationFactory<Program>` against an
in-memory database — register/login/duplicate-email/wrong-password/JWT-protected-endpoint/tenant-isolation),
`LeadProcessingServiceTests` (unit — mapping heuristics, explicit field-mapping overrides, unmapped-form skip,
duplicate-webhook skip, GHL-failure handling, retry, lazy GHL token refresh), and `MetaWebhookControllerTests`
(unit — signature verification, challenge response, leadgen vs. other webhook fields).

Writing the integration tests caught a real bug: `Program.cs` was reading `Jwt:SigningKey` into a local variable
*before* `builder.Build()`, so `JwtBearerOptions` got configured with whatever was in `appsettings.json` at that
point, while `JwtTokenGenerator` correctly picked up the live value via `IOptions<JwtOptions>` at request time.
In production this was invisible as long as the signing key never changed after startup, but it would have broken
the instant that value came from a source that updates after boot (e.g. a secrets manager reload) — and it broke
outright under `WebApplicationFactory`, which is exactly what surfaced it. Fixed by binding
`JwtBearerOptions.TokenValidationParameters` from `IOptions<JwtOptions>` at options-resolution time instead of
reading configuration eagerly (see `Program.cs`).

## Known simplifications (by design, for a first version)

- No automated tests yet.
- No password reset / email verification flow.
- GHL location friendly name isn't fetched from `GET /locations/{id}` yet — `GhlConnection.LocationName` is set to
  the raw location ID until that's added.
- No retry/backoff policy for transient GHL API errors (429s) — failed leads sit as `Failed` for manual retry.
- Meta long-lived user tokens (~60 days) and GHL refresh tokens aren't proactively renewed by a scheduled job yet;
  GHL tokens refresh lazily on use, Meta tokens will need a periodic re-auth reminder before they expire.
