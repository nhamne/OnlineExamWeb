---
title: "Student overview dashboard redesign"
description: "Replace the student overview exam grid with summary metrics and a recent results table."
status: pending
priority: P2
effort: 6h
branch: dev-nham
tags: [student, dashboard, ui, mvc]
created: 2026-05-25
---

# Student Overview Dashboard Redesign

## Scope
Refactor the student landing page into a compact dashboard that matches the teacher dashboard tone and layout direction, while keeping the rest of the student area stable.

## Verified anchors
- Current student overview still renders the greeting, class filters, exam grid, and filter JS in [Views/Student/Index.cshtml](c:/OnlineExamWeb/OnlineExam/OnlineExam/Views/Student/Index.cshtml#L8).
- The overview data is built in [Controllers/StudentController.cs](c:/OnlineExamWeb/OnlineExam/OnlineExam/Controllers/StudentController.cs#L54).
- The dashboard VM still carries exam, class, and score history collections in [ViewModels/StudentDashboardVM.cs](c:/OnlineExamWeb/OnlineExam/OnlineExam/ViewModels/StudentDashboardVM.cs#L6).
- The shared layout already routes student pages under the common top header in [Views/Shared/_Layout.cshtml](c:/OnlineExamWeb/OnlineExam/OnlineExam/Views/Shared/_Layout.cshtml#L141).
- The teacher dashboard is the closest visual reference for greeting scale, card rhythm, and table treatment in [Views/Teacher/Index.cshtml](c:/OnlineExamWeb/OnlineExam/OnlineExam/Views/Teacher/Index.cshtml#L13).

## Dependency Graph
1. Phase 1 updates the data contract and controller query shape.
2. Phase 2 consumes the new contract to rebuild the page UI.
3. Phase 3 validates build and smoke-tests the new layout.

## File Ownership
- Phase 1: [Controllers/StudentController.cs](c:/OnlineExamWeb/OnlineExam/OnlineExam/Controllers/StudentController.cs), [ViewModels/StudentDashboardVM.cs](c:/OnlineExamWeb/OnlineExam/OnlineExam/ViewModels/StudentDashboardVM.cs)
- Phase 2: [Views/Student/Index.cshtml](c:/OnlineExamWeb/OnlineExam/OnlineExam/Views/Student/Index.cshtml)
- Phase 3: no code ownership, validation only

## Backwards Compatibility
Keep the legacy `Exams`, `JoinedClasses`, and `ScoreHistory` properties on `StudentDashboardVM` so `MyExams` and results-related views stay untouched while the overview switches to the new summary/table model.

## Rollback Plan
- Revert phase 2 to restore the old overview markup if the new table or header alignment regresses.
- Revert phase 1 if the new query shape causes loading issues; the old dashboard view can continue to use the legacy collections.
- Leave `MyExams` and `Results` unchanged so rollback does not cascade into other student routes.

## Test Matrix
- Unit/integration: controller projection for summary metrics and recent results ordering.
- UI smoke: no `class-filters`, no `.exam-card`, and no `#exam-list` on the student overview route.
- Visual check: greeting block and summary cards should follow the teacher dashboard spacing and typography scale.
- Build: `dotnet build` must remain green after the change.

## Success Criteria
- Student overview shows exactly two summary cards and a recent results table.
- No exam cards, class filter buttons, or large exam list remain on the overview page.
- Greeting header tone and sizing align with the teacher dashboard reference.
- Build stays green.
