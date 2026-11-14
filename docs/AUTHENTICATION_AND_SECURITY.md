# Authentication and Security

## Identity storage

ASP.NET Core Identity is registered with `ApplicationUser` and the existing `AppDbContext`. Its standard `AspNet*` tables and planner tables use the configured SQLite database; there is no second account database. Passwords are hashed by Identity's password hasher. Registration enforces unique email, a 10-character password with uppercase, lowercase, digit, and symbol, and Identity sign-in lockout is enabled after five failed attempts for ten minutes.

## HTTP authentication flow

`/register` and `/login` are static Razor Pages with server validation and antiforgery tokens. Registration signs the new user in using the current HTTP response; login uses `SignInManager.PasswordSignInAsync` and supports Remember Me. The application cookie is HTTP-only, same-site Lax, sliding, and lasts up to 14 days when persistent. Production HTTPS requests receive a secure cookie; local HTTP development uses a non-secure cookie. Data Protection keys persist under `%LOCALAPPDATA%/StudentPlanner/DataProtectionKeys` so cookies can be validated after an application restart; `DataProtection:KeysPath` can override that path. The directory must be writable at startup; startup fails with a diagnostic instead of falling back to ephemeral keys. Keys inherit the operating system's directory permissions. This configuration does not encrypt the key files at rest; production hosts should apply appropriate OS ACLs and platform-specific key-at-rest protection when required. `/logout` is an authorized Razor Page accepting POST only for sign-out and redirects to Login. Cookie issue and revocation do not happen from an already established Blazor circuit.

`/account` is an authorized Blazor page. It lets the current user edit the display name through `UserManager`; email is read-only because no verified email-change delivery flow is configured. Login and registration intentionally do not offer password reset or email confirmation.

## Route protection and current user

Dashboard, Calendar, Assignments, Courses, Grades, Settings, and Account are marked `[Authorize]` and routed through `AuthorizeRouteView`. Anonymous navigation redirects to `/login` with a local return URL. `CurrentUserService` obtains the current principal from the scoped Blazor `AuthenticationStateProvider` and requires its Identity user ID. It fails closed when no authenticated ID exists.

## Record ownership

Courses, assignments, grades, and schedule events have nullable `OwnerId` values and foreign keys to `AspNetUsers`. EF services set the owner from `ICurrentUserService` on creation and include it in every list, update, and delete query. Assignment, grade, and schedule services require linked courses to have the same owner. Calendar overlap checks and dashboard aggregates are owner-scoped. The browser never supplies an owner ID as authorization evidence. Account deletion is not exposed; owner foreign keys use restrictive deletion behavior.

Ownership enforcement is in the EF service layer, with relational user foreign keys and owner indexes. SQLite does not have a composite tenant foreign key for CourseId and OwnerId; callers must use the EF services, and future direct database write paths must add the same ownership validation.

## Migration and legacy records

`AddIdentityAndUserOwnership` adds Identity tables, settings, owner indexes, and nullable owner columns without deleting or rewriting existing academic data. Existing rows retain `OwnerId = NULL`; all authenticated service queries exclude them. New writes always use the authenticated owner. Development sample records inserted before accounts existed likewise remain hidden.

There is no automatic first-user claim or self-service claim endpoint. To import legacy records, an administrator must verify the record owner's identity out-of-band, take a database backup, map only verified rows to that account in a controlled migration/import, and record the action. Do not assign all legacy data to the first registrant. The automated migration test starts from the checked-in initial schema in an isolated SQLite database, inserts legacy records, applies the ownership migration, and verifies row/link preservation and invisibility.

## Settings isolation

`IdentitySettingsService` persists theme, GPA scale, language, time zone, semester, academic preferences, and notifications in `UserPlannerSettings`, keyed by the authenticated Identity ID. Full name and email are read from `AspNetUsers`; settings saves cannot overwrite profile identity. The old shared localStorage key is no longer loaded. It may remain in existing browser storage, but the application ignores it. Logging out therefore leaves no active account-specific preferences in the browser.

## Security tests and limitations

`BackendIntegrationTests` exercises actual HTTP registration, cookie login/logout, antiforgery, password hashing/validation, anonymous route protection, user A/B isolation, cross-owner course references, dashboard scoping, settings isolation, and legacy preservation using disposable SQLite databases.

This implementation has not received an external security review. Email ownership verification, password reset, email changes, MFA, distributed rate limiting, and administrator tooling are not implemented. Localhost browser QA was blocked by the execution browser's connection timeout; HTTP integration tests provide automated server-side coverage.
