---
phase: 2
title: "Overview view redesign"
status: pending
priority: P2
effort: 3h
dependencies: [1]
---

# Phase 2: Overview View Redesign

## Overview
Replace the current student overview cards, class filters, and exam grid with a teacher-style greeting block, two metric cards, and a recent results table.

## Requirements
- Functional: remove the current exam cards, class filter buttons, and large exam list from the overview page.
- Functional: render a clean dashboard with exactly two summary cards and a recent-results table.
- Functional: restyle the greeting header so it feels aligned with [Views/Teacher/Index.cshtml](c:/OnlineExamWeb/OnlineExam/OnlineExam/Views/Teacher/Index.cshtml#L13-L35).
- Non-functional: keep the layout responsive and preserve the app shell from [Views/Shared/_Layout.cshtml](c:/OnlineExamWeb/OnlineExam/OnlineExam/Views/Shared/_Layout.cshtml#L141-L145).

## Architecture
The current view in [Views/Student/Index.cshtml](c:/OnlineExamWeb/OnlineExam/OnlineExam/Views/Student/Index.cshtml#L8-L170) is still organized around `class-filters`, `exam-list`, and per-exam action cards. The redesign should replace that whole surface with a compact hero, two summary cards, and a results table that consumes the new dashboard fields from phase 1. No client-side filtering JS should remain on the overview route because the page is no longer a filterable exam list.

## Related Code Files
- Modify: [Views/Student/Index.cshtml](c:/OnlineExamWeb/OnlineExam/OnlineExam/Views/Student/Index.cshtml)

## Implementation Steps
1. Rebuild the greeting section with the same visual weight and typography rhythm used by the teacher dashboard, but tuned to the student context.
2. Add two metric cards at the top of the page, using the shared brand colors and compact card geometry.
3. Replace the exam grid with a recent results table that surfaces the most useful submission fields and a clear action link.
4. Remove the class filter buttons, exam-card markup, chart block, and any filter JavaScript that is no longer needed.
5. Add an empty state for users with no submissions so the table area still feels intentional.

## Success Criteria
- [ ] No `class-filters`, `.filter-btn`, `.exam-card`, or `#exam-list` elements remain on the student overview page.
- [ ] The greeting header visually matches the teacher dashboard tone, font size, and spacing.
- [ ] The page reads as a dashboard first, not as a list of exam tiles.

## Risk Assessment
- High likelihood, medium impact: visual drift from the teacher dashboard could make the student page feel disconnected. Mitigation: reuse the same spacing, heading scale, and card hierarchy rather than inventing a new pattern.
- Medium likelihood, low impact: removing the chart could make the overview feel too sparse if the table is empty. Mitigation: add a compact empty state and rely on the summary cards for top-level context.

## Rollback
Restore the previous overview markup and JavaScript if the new dashboard pattern fails visual or usability checks.
