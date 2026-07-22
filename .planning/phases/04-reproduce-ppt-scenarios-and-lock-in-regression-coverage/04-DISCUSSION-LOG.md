# Phase 4: Reproduce PPT Scenarios And Lock In Regression Coverage - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions captured in CONTEXT.md; this log preserves the discussion.

**Date:** 2026-07-22
**Phase:** 04-reproduce-ppt-scenarios-and-lock-in-regression-coverage
**Mode:** discuss
**Areas discussed:** Verification unit, Result judgment, Evidence format

## Discussion Summary

### Verification unit
- Options considered:
  - PPT numbering as the base unit plus grouped scenario runs
  - feature-area regrouping only
  - implementation-phase grouping only
- User selected:
  - keep PPT numbering as the base unit
  - allow grouped scenarios for related root-cause flows

### Result judgment
- Options considered:
  - `Fixed / Partial / Blocked`
  - `Pass / Fail`
  - looser review-oriented labels
- User selected:
  - `Fixed / Partial / Blocked`

### Evidence format
- Options considered:
  - structured verification document
  - plain checklist
  - mixed summary with detailed exceptions
- User selected:
  - structured verification document with per-item evidence fields
