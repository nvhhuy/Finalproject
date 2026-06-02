# Tech Mapping — Existing Codebase → EduConnect Features

**Last updated:** 2026-06-01  
**Purpose:** Show exactly which existing code to reuse for each new EduConnect feature, so AI-assisted development is as fast as possible.

---

## Core Principle

> Every new EduConnect feature follows an existing pattern in the codebase.  
> Never build from scratch what already exists in a different domain.

---

## What Already Exists (Base Codebase)

```
src/Backend/
├── Domain/                   # Core domain entities
├── Application/
│   ├── Common/               # MediatR pipeline, validators, behaviors
│   ├── Contracts/            # Interfaces (IApplicationDbContext, etc.)
│   └── Modules/              # Feature domains
│       ├── Auth/             # Login, register, OTP, Google OAuth
│       ├── Billing/          # Subscriptions, plans, invoices
│       ├── Tenants/          # Multi-tenant management
│       ├── Users/            # User profile, 2FA
│       ├── Notifications/    # In-app + email notifications
│       └── Support/          # Support tickets
├── Infrastructure/
│   ├── Modules/
│   │   ├── Billing/          # VNPay + PayOS webhook handlers
│   │   ├── Email/            # SMTP sender, templates
│   │   └── Notifications/    # SignalR dispatcher, delivery processor
│   └── Persistence/          # EF Core DbContexts, interceptors
└── Api/
    ├── Controllers/          # REST endpoints
    └── Realtime/             # SignalR hubs

src/Frontend/app/
├── app/                      # Authenticated pages
│   ├── page.tsx              # Dashboard (useQuery pattern)
│   ├── wallet/               # Wallet + payment (useMutation pattern)
│   ├── support/              # Support tickets (search + filter)
│   ├── tenant/               # Tenant management
│   ├── subscribe/            # Subscription flows
│   ├── media/                # File upload (useMutation + polling)
│   └── notifications/        # Notification list + realtime
├── pricing/                  # Public pricing page
└── register/                 # Onboarding flow

src/Admin/src/features/
├── users/                    # User management
├── contact/                  # Contact messages
├── support/                  # Support ticket review
├── settings/                 # Platform settings
└── messaging/                # DLQ management
```

---

## Feature → Reuse Mapping

### Class Management Domain

```
New: ClassesController + domain
Copy pattern from: TenantsController + Tenant domain
─────────────────────────────────────────────────────
Entity pattern:    Tenant.cs → Class.cs
Command pattern:   CreateTenantCommand → CreateClassCommand
Query pattern:     GetTenantQuery → GetClassQuery
Controller:        TenantsController → ClassesController
Frontend list:     app/tenant/page.tsx → app/classes/page.tsx
Frontend form:     app/tenant/page.tsx modal → app/classes/new/page.tsx
DB migration:      AddTenantTable → AddClassTable
```

**Prompt for AI:**
```
"Add a Class domain to the existing codebase.
 Follow the same patterns as the Tenant domain.
 Class entity fields: Name (string), Subject (string), Schedule (JSON), 
 MonthlyFeeVND (decimal), MaxStudents (int), IsActive (bool), TenantId (Guid).
 Generate: entity, migration, command handlers, query handlers, controller, and 
 basic React frontend list + create form following the existing pattern in 
 app/tenant/page.tsx"
```

---

### Student Management Domain

```
New: StudentsController
Copy pattern from: Users management in Admin
─────────────────────────────────────────────────────
Entity:     User → ClassStudent (but no auth account)
Belonging:  Standalone user → Belongs to Class (FK)
Access:     Admin only → Teacher only (tenant-scoped)
```

---

### Attendance Domain

```
New: SessionsController + AttendanceController
Copy pattern from: Support tickets domain
─────────────────────────────────────────────────────
SupportTicket   → ClassSession
TicketMessage   → AttendanceRecord
Status enum     → AttendanceStatus (Present/Absent/Late)
Reply command   → RecordAttendanceCommand
Timeline query  → GetSessionAttendanceQuery
```

**Email after attendance saved:**
```
Reuse: EmailSendConsumerService (already handles queued emails)
Add new template: "attendance-report"
Add new email type: AttendanceReportEmail
Trigger: AttendanceRecordedDomainEvent → handler queues email via outbox
─────────────────────────────────────────────────────
Copy pattern from: existing domain event handlers in Application/Modules/Billing/
EmailType enum: Add AttendanceReport
Template file: src/Backend/Infrastructure/Email/Templates/attendance-report.html
```

---

### Tuition Collection Domain

```
New: TuitionController + payment integration
Reuse: EVERYTHING in the billing module
─────────────────────────────────────────────────────
Subscription billing    → Monthly tuition record generation
VNPay checkout          → Tuition payment checkout link
PayOS checkout          → Alternative payment method
Payment webhook         → TuitionPaidWebhookHandler
Wallet service          → Teacher wallet credits after fee deduction
Quartz scheduler        → Monthly tuition generation job

Key difference:
  Billing: tenant pays platform (B2B)
  Tuition: parent pays teacher through platform (B2B2C)
  Platform keeps 1.5% from the inbound flow
```

**Transaction fee deduction:**
```
Webhook receives: amount = 2,000,000
Calculate fee:   fee = 2,000,000 × 0.015 = 30,000
Record tuition paid: TuitionRecord.Status = Paid, PaidAmount = 2,000,000
Credit teacher wallet: WalletService.Credit(teacherId, 2,000,000 - 30,000)
Record platform revenue: PlatformRevenue.Add(30,000, reason: "TuitionFee")

Existing wallet pattern: src/Backend/Application/Modules/Billing/ + WalletController
```

---

### Parent Portal (Tokenized, No Auth)

```
New: ParentPortalController (unauthenticated)
Similar to: Email verification flow (token-based)
─────────────────────────────────────────────────────
Email verification:  /verify-email?token=xxx
Parent portal:       /parent?token=xxx

ParentPortalToken entity:
  StudentId, Token (UUID), ExpiresAt (90 days), LastAccessedAt
  
Token validation middleware: same pattern as email token validation
Rate limiting: same IPRateLimitFilter already configured

Frontend route:
  src/Frontend/app/parent/[token]/page.tsx
  No auth wrapper (public route)
  Fetch /api/parent-portal/{token}/student-summary
```

---

### Anonymous Q&A Board

```
New: AnonBoardController
No auth required for students (IP-tracked only)
─────────────────────────────────────────────────────
Pattern: like support ticket but without auth on create
Upvote: session cookie + IP hash (no user account)
Rate limit: existing IPRateLimitFilter, add /anon-board to limits

Entity: AnonQuestion (ClassId, Content, AnonAlias, AskedAt, UpvoteCount)
Entity: AnonAnswer (QuestionId, TeacherId, Content, AnsweredAt)

Frontend: src/Frontend/app/board/[classCode]/page.tsx
  → No auth check, uses class join code as access control
  → Separate layout from authenticated /app/ area
```

---

### Course Materials Upload

```
Reuse: 100% of existing media upload system
─────────────────────────────────────────────────────
MediaController     → already handles upload + S3
InitiateUpload cmd  → reuse unchanged
UploadCompleted evt → add ClassMaterial record
MediaLab page       → adapt as "class materials" tab

New entity: ClassMaterial
  ClassId, SessionId (nullable), Title, FileUrl, FileType, UploadedAt, AccessCount

Reuse: src/Frontend/app/app/media/page.tsx (upload flow with polling)
       Already has useMutation + refetchInterval for status polling
```

---

### Tutor Profile System

```
New: TutorProfilesController
Similar to: User profile domain
─────────────────────────────────────────────────────
UserProfile         → TutorProfile (public-facing)
Profile update cmd  → UpdateTutorProfileCommand
Profile view query  → GetPublicTutorProfileQuery (no auth)
Media upload        → Profile photo + intro video

Admin verification:
  Same pattern as support ticket admin review
  Queue: GET /admin/tutor-profiles/pending
  Action: POST /admin/tutor-profiles/{id}/verify
  Notification: same notification system → "profile approved"
```

---

### Search (TutorMatch)

```
Phase 1: PostgreSQL full-text search (sufficient for < 5,000 profiles)
─────────────────────────────────────────────────────
EF Core pattern already in: ListUsersQuery (pagination + filtering)
Add: tsvector column on TutorProfile.SearchVector
     GIN index on SearchVector
     Full-text query in ListTutorProfilesQuery

Phase 3+ upgrade path: Meilisearch or Elasticsearch if > 50,000 profiles
(Docker Compose already structured to add new services)
```

---

### Booking System

```
New: BookingController
Reuse: Support ticket + payment flow pattern
─────────────────────────────────────────────────────
SupportTicket lifecycle  → Booking lifecycle
  Open → Accepted/Declined
  Resolved → Completed

Payment: VNPay checkout for 30k deposit (same as subscription flow)
Notification: same INotificationDispatcher

BookingRequest entity:
  ProfileId, ParentName, ParentPhone, ParentEmail,
  PreferredDate, Message, DepositTransactionId, Status
```

---

### Zalo OA Integration

```
New: ZaloNotificationChannel implementing INotificationChannel
─────────────────────────────────────────────────────
Existing: INotificationDispatcher → SignalRNotificationDispatcher
New channel: ZaloNotificationDispatcher
Pattern: IEmailSender → IZaloSender

Zalo ZNS API (Zalo Notification Service):
  POST https://business.openapi.zalo.me/message/template
  Headers: access_token, secret_key
  Body: { phone, template_id, template_data }

Add to DI: services.AddZaloOA(configuration)
Add to NotificationDeliveryProcessor: try Zalo first if parent opted in, fallback to email
Config: ZaloOA:AccessToken, ZaloOA:SecretKey, ZaloOA:TemplateIds
```

---

## Database Migration Strategy

All new EduConnect tables use the **same migration pattern**:

```bash
# Generate migration
dotnet ef migrations add AddClassesTable \
  --project src/Backend/Infrastructure \
  --startup-project src/Backend/Api \
  --context ApplicationDbContext

# Apply
dotnet ef database update \
  --project src/Backend/Infrastructure \
  --startup-project src/Backend/Api

# For read context (if adding read views/projections)
dotnet ef migrations add AddClassesReadView \
  --context ApplicationReadDbContext
```

**Table naming convention** (follow existing):
```
Classes, ClassStudents, ClassSessions, AttendanceRecords,
TuitionRecords, ParentPortalTokens, AnonQuestions, AnonAnswers,
ClassMaterials, TutorProfiles, TutorCredentials, BookingRequests, TutorReviews
```

---

## Feature Flag Configuration

Add each new feature to `appsettings.json` and enable per tenant:

```json
{
  "FeatureFlags": {
    "Flags": {
      "classhub.enabled": { "Default": false },
      "tutormatch.enabled": { "Default": false },
      "classhub.tuition-collection": { "Default": false },
      "classhub.parent-portal": { "Default": false },
      "classhub.anon-board": { "Default": false },
      "tutormatch.booking": { "Default": false },
      "tutormatch.boost": { "Default": false }
    }
  }
}
```

Enable for beta tenants via admin console tenant settings page (already exists).

---

## API Versioning Approach

All new endpoints under:
```
/api/v1/classes/...
/api/v1/students/...
/api/v1/sessions/...
/api/v1/tuition/...
/api/v1/parent-portal/...    (unauthenticated)
/api/v1/anon-board/...       (unauthenticated)
/api/v1/tutor-profiles/...   (mixed: read = public, write = auth)
/api/v1/bookings/...
/api/v1/admin/tutor-profiles/... (admin only)
```

Version strategy: keep v1 until breaking change; then add v2 route alongside.

---

## AI Coding Prompt Templates

### New Domain Prompt
```
"Add a [DomainName] domain to the existing .NET 8 ASP.NET Core codebase.
 Follow the EXACT same patterns as the [ExistingDomain] domain.
 
 Files to reference:
   Entity: src/Backend/Domain/[ExistingDomain]/[Entity].cs
   Commands: src/Backend/Application/Modules/[ExistingDomain]/[Name]Commands.cs
   Queries: src/Backend/Application/Modules/[ExistingDomain]/[Name]Queries.cs
   Controller: src/Backend/Api/Controllers/[ExistingDomain]/[Name]Controller.cs
 
 New entity fields: [list all fields with types]
 Commands needed: [list]
 Queries needed: [list]
 Controller endpoints: [list with HTTP method + path]
 Relationships: [describe FKs]"
```

### New Frontend Page Prompt
```
"Add a [PageName] page to the Next.js 15 frontend.
 Follow the EXACT same patterns as src/Frontend/app/app/[existing-page]/page.tsx.
 
 Data to fetch: [describe query]
 Actions available: [describe mutations]
 API endpoints: [list]
 Form fields: [if create/edit form]
 Use: @tanstack/react-query useQuery + useMutation
 Use: react-hook-form + zod for forms
 Use: existing UI components from src/Frontend/components/"
```
