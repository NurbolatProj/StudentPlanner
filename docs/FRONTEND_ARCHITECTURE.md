# Frontend Architecture

## Structure

- `Components/Layout` contains the application shell, navigation, theme and reconnect UI.
- `Components/Pages` contains the six route pages. Pages own transient form, search, filter and modal state.
- `Components/Common` is reserved for shared UI components; repeated confirmation patterns are candidates for extraction.
- `Models/PlannerModels.cs` contains the shared frontend contracts, enums and the centralized GPA calculator.
- `Services/Interfaces` defines asynchronous application service contracts.
- `Services/Mock` contains scoped, in-memory mock implementations. Collections are instance scoped and callers receive copies.
- `Services/BrowserSettingsService.cs` stores user preferences in browser local storage. It initializes only after the interactive render so prerender does not invoke JavaScript.

## State and data flow

`Program.cs` registers mock services as scoped dependencies. A Blazor Server circuit therefore owns its mock collections; mutable static global data is not used. Pages load data through services and subscribe to each service's `Changed` event. CRUD calls notify subscribers, which reload from the same service instance. `MainLayout` loads persisted theme preferences after the first interactive render. Settings changes notify the layout and grades/dashboard views.

The dashboard reads courses, assignments, grades, settings and schedule records through `IDashboardService` and the underlying services. Assignment deadline cards are projections of assignment records, not separate saved records.

## Replacing the mock layer

Keep the interfaces in `Services/Interfaces` stable. Add EF Core implementations in a backend-owned namespace, register those implementations in `Program.cs`, and remove the mock registrations. Components should continue to call the same interfaces. Preserve the `Changed` notification contract or replace it with a documented shared-state mechanism so cross-page views refresh.

## Frontend-only limits

Mock records live for one active circuit and reset when the circuit ends. Browser local storage is used only for preferences. Database persistence, authentication, multi-user isolation, server-side validation and migrations are outside this frontend implementation.
