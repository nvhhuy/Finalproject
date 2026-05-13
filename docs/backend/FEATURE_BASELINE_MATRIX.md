# Backend Feature Baseline Matrix

This matrix tracks what the base code currently supports out-of-the-box.

## Authentication and session

| Feature | Status | Notes |
|---|---|---|
| JWT access token | Implemented | `AuthController` + `JwtGenerator` + bearer auth middleware. |
| Refresh token rotation | Implemented | Refresh tokens persisted hashed; `POST api/auth/refresh`. |
| Login/Register | Implemented | Register can now be controlled by app setting toggle. |
| Google sign-in | Implemented | Via ID token validator abstraction. |
| Two-factor login/setup | Implemented | Begin/confirm setup and complete login with TOTP. |
| Forgot password | Implemented (base) | `POST api/auth/forgot-password`, reset token via cache + email sender abstraction. |
| Reset password (from token) | Implemented (base) | `POST api/auth/reset-password`. |
| Email verification | Pending | Planned next: verify token flow + `EmailVerified` state gate on sign-in. |

## Admin user management

| Feature | Status | Notes |
|---|---|---|
| List users | Implemented | `GET api/admin/users`. |
| Create user | Implemented | `POST api/admin/users`. |
| Edit user info/role | Implemented | `PUT api/admin/users/{userId}`. |
| Delete user | Implemented (soft delete) | `DELETE api/admin/users/{userId}` calls `MarkDeleted()`. |
| Lock/Unlock user | Implemented | Existing commands and endpoints. |
| Impersonation | Implemented | Existing endpoint returns token pair. |
| Reset 2FA | Implemented | `POST api/admin/users/{userId}/2fa/reset`. |
| Reset password | Implemented | `POST api/admin/users/{userId}/password/reset`. |

## Application settings

| Feature | Status | Notes |
|---|---|---|
| Persistent app settings table | Implemented | `AppSettings` table + EF configuration + migration. |
| Admin API toggle example | Implemented | `allow_public_register` get/set endpoints under `api/admin/settings`. |
| Register gate by setting | Implemented | Register handler blocks when disabled. |

## Worker foundation

| Feature | Status | Notes |
|---|---|---|
| Cron worker base | Implemented | `ICronJob` + `CronJobSchedulerHostedService`. |
| Queue worker base | Implemented | `QueueConsumerBackgroundService<T>` scaffold. |
| Real broker adapter (Rabbit/Kafka) | Pending | Null consumer/publisher currently used until adapter is added. |
