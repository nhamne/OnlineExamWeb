---
phase: 1
title: "Data contract and controller shape"
status: pending
priority: P2
effort: 2h
dependencies: []
---

# Phase 1: Data Contract and Controller Shape

## Overview
Rework the student dashboard data source so the overview can render a compact summary plus a recent results table without depending on the old exam-grid payload.

## Requirements
- Functional: compute two summary metrics and a recent-results row set for the dashboard.
- Functional: keep legacy dashboard collections intact for `MyExams` and other student routes.
- Non-functional: avoid extra controller work in the view; project only the fields the dashboard needs.

## Architecture
`Index()` currently loads joined classes, exam sessions, and submissions, then fills `StudentDashboardVM` with `PendingExamsCount`, `Exams`, `JoinedClasses`, and `ScoreHistory` from [Controllers/StudentController.cs](c:/OnlineExamWeb/OnlineExam/OnlineExam/Controllers/StudentController.cs#L54-L130). The new shape should keep the expired-exam auto-submit path, then add a dashboard-specific projection from submissions into a compact results list and summary counts. Keep `StudentDashboardVM` backward-compatible by adding fields instead of removing the existing ones.

## Related Code Files
- Modify: [Controllers/StudentController.cs](c:/OnlineExamWeb/OnlineExam/OnlineExam/Controllers/StudentController.cs)
- Modify: [ViewModels/StudentDashboardVM.cs](c:/OnlineExamWeb/OnlineExam/OnlineExam/ViewModels/StudentDashboardVM.cs)

## Implementation Steps
1. Add a small dashboard row VM for recent results, with exam name, class name, submitted time, score, and status/action fields.
2. Extend `StudentDashboardVM` with dashboard-specific properties such as recent results and the two summary values.
3. Update `StudentController.Index()` to query submissions once, derive the summary metrics, and sort recent rows by submission time descending.
4. Leave `MyExams()` and `Results()` data flows untouched so the redesign does not break the rest of the student area.

## Success Criteria
- [ ] The controller returns all data needed for the new overview without UI-side filtering logic.
- [ ] Existing properties remain available for other views.
- [ ] The new projection is deterministic and sorted correctly.

## Risk Assessment
- Medium likelihood, medium impact: projection changes can break the view if a field is missing. Mitigation: add the new fields without removing the old ones and keep null-safe defaults.
- Medium likelihood, low impact: duplicate query work could appear if the new projection is bolted on instead of replacing the old `Index()` flow. Mitigation: reuse the already-loaded student context and submissions query path.

## Rollback
Revert the controller and viewmodel additions only; the old view can continue to render against `Exams`, `JoinedClasses`, and `ScoreHistory` if needed.
