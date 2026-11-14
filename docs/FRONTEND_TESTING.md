# Frontend Testing

## Automated tests

`StudentPlanner.Tests` uses xUnit and deterministic model/service assertions. It covers GPA weighting and scale conversion, assignment CRUD, grade deletion, and email validation. Run from the repository root with `dotnet test` using the .NET 10 SDK.

## Manual checklist

- Create, update, complete and delete assignments; check dashboard counts and upcoming deadlines.
- Create and edit a course; attempt deletion while it has assignments/grades and confirm the relationship restriction.
- Create, update and delete grades; verify GPA under each settings scale and empty-grade behavior.
- Create/edit/delete schedule events, navigate months, check time validation and assignment deadline projections.
- Save settings, refresh the browser, verify theme and GPA scale remain selected.
- Keyboard-test modal open/close, Escape handling, focus visibility and screen-reader labels.
- Check desktop, tablet and mobile layouts in a real browser.

## Verification status

The original target is `net10.0`. At implementation time the environment has only SDK `9.0.200`, so the target build, target test run and application startup cannot be verified here. A temporary local compatibility build may be used only as a partial syntax check; it does not establish .NET 10 compatibility. Browser/device testing has not been performed.
