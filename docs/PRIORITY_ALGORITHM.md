# Smart Assignment Priority

`AssignmentPriorityService` returns the highest scoring unfinished assignments, including a human-readable reason. It has no database or clock dependency: callers pass the assignment sequence and `DateOnly today`, so the same input always produces the same result.

## Score

```text
score = urgency + importance + workload
```

| Factor | Rule |
|---|---|
| Overdue urgency | 80 + 5 per overdue day, capped at 100 |
| Due today | 75 |
| Due tomorrow | 70 |
| Due in 2–3 days | 55 |
| Due in 4–7 days | 40 |
| Later deadline | max(5, 35 − 0.5 × days after day 7) |
| Importance | High 35, Medium 20, Low 5 |
| Workload | 3 points per estimated hour, capped at 15 |

Scores round to two decimals. Completed assignments are excluded. Recommendations sort by score descending, then due date ascending, then ID ascending for deterministic tie-breaking. The returned reason reports deadline state, priority, and substantial workload. Weights are bound from the `AssignmentPriority` section in `appsettings.json`, with defaults held in `AssignmentPriorityWeights`, so instructors can tune them without changing the frontend contract.

The score is a recommendation heuristic, not a guarantee or a replacement for student choice. It is integrated as a compact “Recommended next” panel in the existing Dashboard assignment card.
