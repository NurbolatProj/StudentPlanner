# Study Insights

Route: `/insights` (authenticated).

The forecast supports 7, 14, and 30 days. It uses only the current user's unfinished assignments with positive estimates and scheduled events in the selected horizon. Existing event durations reduce each day's capacity. The default daily study capacity is 120 minutes and can be changed in Settings (30–720 minutes).

`WorkloadPlanner.Analyze` is a pure deterministic function. It orders tasks by earliest deadline, higher priority, then ordinal title; allocates available minutes across days no later than each deadline; and reports unmet minutes when capacity is insufficient. Explainable risk rules flag over-capacity calendar days, groups of at least two deadlines on one date, high-priority work due within three days, and unplaced work. The chart uses actual plan values and does not infer grades or student outcomes.

Tests cover capacity limits, deadline grouping, priority order, deterministic output, unmet work, and empty input. Verify filters, displayed charts, and settings persistence in a browser before presentation.
