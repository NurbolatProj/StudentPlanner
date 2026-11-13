# Student Planner — Presentation Slide Content

## 1. Student Planner
- A responsive academic planning interface for university students
- Team members: Student 1 (backend) and Student 2 (frontend)
- Current delivery: Blazor Interactive Server frontend prototype

## 2. Project Problem
- Students coordinate assignments, courses, schedules and grades across separate tools
- Deadline visibility and progress tracking can be inconsistent

## 3. Aim and Objectives
- Provide one interface for planning coursework and reviewing academic progress
- Support course, assignment, grade, calendar and preference workflows
- Prepare stable service boundaries for a later database-backed implementation

## 4. Project Plan
- Student 2: UI, shared frontend models, mock services, state synchronization and frontend tests
- Student 1: SQLite, EF Core, migrations, backend rules and database tests
- Integration: replace mock service registrations with backend implementations

## 5. Existing Solution Comparison
- Spreadsheets: flexible but manual and disconnected from schedules
- Calendar tools: useful for dates, but lack course and grade context
- Student Planner prototype: combines these frontend workflows in one Blazor shell

## 6. Technology Stack
- C#, ASP.NET Core, Blazor Interactive Server
- Shared Razor components and scoped dependency injection
- Browser local storage for frontend preferences
- Planned backend: SQLite and EF Core (not implemented in this frontend)

## 7. Architecture
- Pages depend on asynchronous service interfaces
- Scoped mock service implementations provide circuit-level in-memory records
- Shared models define course, assignment, grade, schedule and settings contracts
- Change events refresh subscribed pages after mutations

## 8. Planned Database Design
- Courses are referenced by assignments and grades through CourseId
- Schedule events may optionally reference a course
- Restrict course deletion while dependent assignments or grades exist
- Database schema and migrations are planned for Student 1

## 9. UI and UX
- Existing dashboard, calendar, assignments, courses, grades and settings views retained
- Search, filters, sorting, forms and confirmation dialogs use shared services
- Desktop/tablet/mobile styling remains based on existing page-specific CSS

## 10. Core Features
- Course and assignment management
- Calendar event management and assignment deadline projection
- Grade entry, filtering and GPA analytics
- Persistent browser preferences

## 11. Advanced Features
- Dashboard statistics from shared service data
- GPA demonstration scales: 4, 5 and 100 point linear conversions
- Assessment weights within courses and course-credit weighting across GPA
- Interactive theme and cross-page state updates

## 12. Testing
- xUnit tests cover GPA math, mock CRUD, grade deletion and email validation
- Targeted .NET 10 test run is pending a .NET 10 SDK environment
- Browser interaction tests are not yet verified

## 13. Application Screenshots
- Add current application screenshots after .NET 10 build and browser review
- Do not present placeholder or unverified screenshots as runtime evidence

## 14. Development Methodology
- Preserve existing visual design while replacing page-owned data with shared contracts
- Verify changes through builds, automated tests and manual scenario checks
- Keep backend ownership separate from frontend integration contracts

## 15. Conclusion
- Frontend service contracts and shared models are prepared for backend integration
- Mock behavior supports early UI development
- .NET 10 build, testhost execution and browser verification remain pending
