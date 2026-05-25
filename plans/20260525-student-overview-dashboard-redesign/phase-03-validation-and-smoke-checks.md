---
phase: 3
title: "Validation and smoke checks"
status: pending
priority: P2
effort: 1h
dependencies: [1, 2]
---

# Phase 3: Validation and Smoke Checks

## Overview
Validate that the redesign ships cleanly, the build stays green, and the student overview no longer exposes the removed exam-grid behavior.

## Requirements
- Functional: confirm the student overview route renders the new dashboard structure.
- Non-functional: verify the solution still builds successfully after the data and view changes.
- Non-functional: confirm the shared layout header still behaves normally for student routes.

## Architecture
This phase is validation-only. It should not introduce new runtime behavior or new files. The target is to verify the controller/view contract from phase 1 and the markup changes from phase 2 against the actual app shell in [Views/Shared/_Layout.cshtml](c:/OnlineExamWeb/OnlineExam/OnlineExam/Views/Shared/_Layout.cshtml#L141-L145).

## Related Code Files
- None. Validation only.

## Implementation Steps
1. Run `dotnet build` against the solution and confirm the build remains green.
2. Smoke-check the student overview route to confirm the two-card summary and recent results table render.
3. Confirm the removed exam grid and filter controls do not appear anywhere on the page.
4. Sanity-check `MyExams` and `Results` routes to ensure the backward-compatible VM changes did not spill over.

## Success Criteria
- [ ] Build passes.
- [ ] Student overview matches the new pattern.
- [ ] No regression appears in the other student routes.

## Risk Assessment
- Medium likelihood, medium impact: build errors can appear from mismatched VM property names. Mitigation: validate immediately after the first two phases and keep the legacy properties intact.
- Low likelihood, medium impact: shared header styling may look slightly off after the redesign. Mitigation: compare against the teacher dashboard and keep any layout adjustments isolated to the overview page unless absolutely necessary.

## Rollback
If validation fails, revert phase 2 first; if the failure is data-related, revert phase 1 and restore the previous overview flow.
