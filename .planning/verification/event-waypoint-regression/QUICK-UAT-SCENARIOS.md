---
status: ready_for_manual_uat
scope: gsd-quick changes through 2026-07-23
---

# Quick Change UAT Scenarios

## Purpose

Verify the quick changes that were delivered outside the numbered milestone
phases. Run these after the milestone UAT scenarios in `04-UAT.md`.

## Prerequisites

1. Start the x64 Debug application with a video that has at least one Event
   waypoint whose EntryFrame and ExitFrame differ.
2. Keep one legacy JSON export created before the event-catalog rename.
3. Prepare a blank copy of that JSON or a temporary output path for save tests.

## Q-01: Korean UI Text Renders Correctly

**Covers:** UI text encoding recovery

1. Open a video with a labeled person.
2. Select the person bounding box.
3. Inspect the Object Info panel and the person/vehicle/event label panels.

**Expected:** Korean labels and attribute captions render as Korean text; no
question-mark replacement characters or mojibake are visible in the inspected
controls.

## Q-02: Event Waypoint Exit Navigation

**Covers:** `bf1c457`

1. In the Event waypoint panel, locate a row with different Entry and Exit
   times.
2. Click the Entry cell and note the displayed frame.
3. Click the Exit cell of the same row.
4. Click the Object cell of the same row.

**Expected:** Entry moves to `EntryFrame`; Exit moves to `ExitFrame`; Object
keeps the existing behavior of selecting the event box at its entry frame.

## Q-03: New Event Catalog Display And Save

**Covers:** `d118f23`, `453ab82`

1. Open the Event label dropdown.
2. Confirm the list is exactly in this order:
   `event_contact`, `event_throw`, `event_final_exchange`, `event_get on`,
   `event_get off`, `event_suspect`, `event_controlled_delivery`,
   `event_camouflage`.
3. Create or select one box for each available event type and save JSON.
4. Inspect the saved JSON `categories` entries and related annotation
   `category_id` values.

**Expected:** The save contains the new names and IDs 25 through 32 in the
listed order. `event_camouflage` is present as ID 32 and no `event_exchange`,
`event_board`, or `event_disembark` category is newly saved.

## Q-04: Legacy JSON Event Catalog Migration

**Covers:** `d118f23`, `453ab82`

1. Open a JSON created before the catalog rename.
2. Inspect event boxes and waypoint rows for the legacy categories below.
3. Save to a new JSON file, then reopen that new file.

**Expected conversion:**

| Legacy category | Displayed and newly saved category |
|---|---|
| `event_contact` | `event_contact` |
| `event_exchange` | `event_throw` |
| `event_board` | `event_get on` |
| `event_final_exchange` | `event_final_exchange` |
| `event_disembark` | `event_get off` |
| `event_controlled_delivery` | `event_controlled_delivery` |
| `event_camouflage` | `event_camouflage` |
| `event_throw` | `event_throw` |

The re-saved JSON must use the Q-03 category IDs. Event meaning is resolved
from `categories[].name` first, so legacy numeric category positions do not
override a present legacy name.

## Evidence To Capture

- Screenshot of Q-01 Object Info and label panels.
- Screenshot or frame numbers for Q-02 Entry and Exit clicks.
- Saved JSON excerpt for Q-03 categories and annotations.
- Before/after JSON excerpts for Q-04, including `event_exchange` and
  `event_camouflage` when available.
