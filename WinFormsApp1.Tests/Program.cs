using System;
using System.Collections.Generic;
using WinFormsApp1;

static class Program
{
    static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("overlap detects entry inside existing range", OverlapDetectsInnerRange),
            ("overlap detects full cover range", OverlapDetectsCoveringRange),
            ("event overlap ignores different instance ids", EventOverlapIgnoresDifferentInstanceIds),
            ("event overlap matches empty instance ids", EventOverlapMatchesEmptyInstanceIds),
            ("video load request cancels previous request", VideoLoadRequestCancelsPreviousRequest),
            ("video load request tracks latest request", VideoLoadRequestTracksLatestRequest),
            ("timeline layout maps labels to separate rows", TimelineLayoutMapsLabelsToRows),
            ("timeline segments keep a minimum visible width", TimelineSegmentsKeepMinimumWidth),
            ("timeline hit testing returns matching waypoint", TimelineHitTestingReturnsMatchingWaypoint),
            ("video controls height preserves object info", VideoControlsHeightPreservesObjectInfo),
            ("timeline panel height fits all rows", TimelinePanelHeightFitsAllRows),
            ("timeline width stops before object info", TimelineWidthStopsBeforeObjectInfo),
            ("waypoint exit column navigates to exit frame", WaypointExitColumnNavigatesToExitFrame),
            ("pending event range reuses existing event instance id", PendingEventRangeReusesExistingInstanceId),
            ("pending event range ignores different event ids", PendingEventRangeIgnoresDifferentEventIds),
            ("event clamp trims only matching instance overflow", EventClampTrimsOnlyMatchingInstanceOverflow),
            ("event clamp can trim all waypoint overflows", EventClampCanTrimAllWaypointOverflows),
            ("clip resolver keeps disjoint person clips separate", ClipResolverKeepsDisjointPersonClipsSeparate),
            ("clip resolver chooses latest containing clip for nested waypoints", ClipResolverChoosesLatestContainingClip),
            ("clip resolver prefers event instance id", ClipResolverPrefersEventInstanceId),
            ("clip resolver supports bounding box input", ClipResolverSupportsBoundingBoxInput),
            ("clip resolver falls back to single frame without waypoint", ClipResolverFallsBackToSingleFrame),
            ("face link export connects face annotation to body annotation", FaceLinkExportConnectsFaceToBody),
            ("face link import marks linked face boxes", FaceLinkImportMarksLinkedFaceBoxes),
            ("face link import ignores malformed links", FaceLinkImportIgnoresMalformedLinks),
            ("two same-type vehicles get different instance ids", TwoSameTypeVehiclesGetDifferentInstanceIds),
            ("vehicle exit box reuses selected entry instance", VehicleExitBoxReusesSelectedEntryInstance),
            ("vehicle exit box reuses unambiguous entry instance", VehicleExitBoxReusesUnambiguousEntryInstance),
            ("vehicle exit box avoids ambiguous entry instance", VehicleExitBoxAvoidsAmbiguousEntryInstance),
            ("vehicle tracking preserves an existing exit box", VehicleTrackingPreservesExistingExitBox),
            ("legacy vehicle track id remains the type id", LegacyVehicleTrackIdRemainsTypeId),
            ("plate link export connects plate annotation to body annotation", PlateLinkExportConnectsPlateToBody),
            ("plate link import marks linked plate boxes", PlateLinkImportMarksLinkedPlateBoxes),
            ("plate link import ignores malformed links", PlateLinkImportIgnoresMalformedLinks),
            ("event catalog includes new values and preserves order", EventCatalogIncludesNewValues),
            ("event combo items include every catalog event", EventComboItemsIncludeEveryCatalogEvent),
            ("vehicle display labels include instance and plate relationship", VehicleDisplayLabelsIncludeInstanceAndPlateRelationship),
            ("vehicle waypoint candidates exclude plates and deduplicate instances", VehicleWaypointCandidatesExcludePlatesAndDeduplicateInstances),
            ("camouflage attribute is exposed exactly once with no duplicate-key crash", CamouflageAttributeExposedOnce),
            ("line width constants reflect reduced thickness", LineWidthConstantsReflectReducedThickness),
            ("selected face or plate does not hide parent body", SelectedSubBoxDoesNotHideParentBody),
            ("vehicle tracking comparison uses effective identity", VehicleTrackingComparisonUsesEffectiveIdentity),
            ("vehicle waypoint matching includes body and linked plate", VehicleWaypointMatchingIncludesBodyAndLinkedPlate),
            ("event workflow skips vehicle waypoint side effects", EventWorkflowSkipsVehicleWaypointSideEffects),
            ("event type update resolves active boxes by event instance", EventTypeUpdateResolvesActiveBoxesByEventInstance),
            ("event type update legacy fallback stays inside one waypoint", EventTypeUpdateLegacyFallbackStaysInsideOneWaypoint),
            ("event type update creates reversible snapshots", EventTypeUpdateCreatesReversibleSnapshots),
            ("event type update resolves mixed marker metadata", EventTypeUpdateResolvesMixedMarkerMetadata),
            ("event type update rejects ambiguous mixed markers", EventTypeUpdateRejectsAmbiguousMixedMarkers),
            ("event waypoint display selects matching instance", EventWaypointDisplaySelectsMatchingInstance),
            ("event waypoint list row uses video time and event name", EventWaypointListRowUsesVideoTimeAndEventName),
            ("active list owner prefers current list selection", ActiveListOwnerPrefersCurrentListSelection),
            ("active list owner falls back to remaining selected list", ActiveListOwnerFallsBackToRemainingSelection),
            ("plate a-frame lookup selects body without losing plate identity", PlateAFrameLookupSelectsBodyWithoutLosingPlateIdentity),
            ("independent plate lookup ignores vehicle body", IndependentPlateLookupIgnoresVehicleBody),
            ("vehicle waypoints merge overlaps and keep disjoint clips", VehicleWaypointsMergeOverlapsAndKeepDisjointClips),
            ("child rectangle follows tracked parent", ChildRectangleFollowsTrackedParent)
            ,("face deletion stays within linked parent waypoint", FaceDeletionStaysWithinLinkedParentWaypoint)
            ,("plate deletion stays within linked parent waypoint", PlateDeletionStaysWithinLinkedParentWaypoint)
            ,("sub annotation deletion rejects ambiguous parent waypoints", SubAnnotationDeletionRejectsAmbiguousParentWaypoints)
            ,("deleted child annotations cannot create export links", DeletedChildAnnotationsCannotCreateExportLinks)
            ,("event box propagation updates only the forward same-instance range", EventBoxPropagationUpdatesForwardSameInstanceRange)
            ,("event box propagation respects later manual adjustments", EventBoxPropagationRespectsLaterManualAdjustments)
            ,("event box propagation preserves deleted tombstones", EventBoxPropagationPreservesDeletedTombstones)
            ,("event box propagation isolates different event instances", EventBoxPropagationIsolatesDifferentEventInstances)
            ,("event box propagation rejects ambiguous scopes without removals", EventBoxPropagationRejectsAmbiguousScopesWithoutRemovals)
            ,("event rectangle propagation undo redo is atomic", EventRectanglePropagationUndoRedoIsAtomic)
            ,("event edit requires a geometry change", EventEditRequiresGeometryChange)
            ,("event rectangle propagation restores manual source provenance", EventRectanglePropagationRestoresManualSourceProvenance)
            ,("event tombstone undo redo retains the original box", EventTombstoneUndoRedoRetainsOriginalBox)
        };

        try
        {
            foreach (var (name, run) in tests)
            {
                run();
                Console.WriteLine($"PASS {name}");
            }

            Console.WriteLine($"Executed {tests.Length} tests.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void SelectedSubBoxDoesNotHideParentBody()
    {
        var body = new BoundingBox { Label = "vehicle", VehicleInstanceId = 1, VehiclePartType = "body" };
        var plate = new BoundingBox
        {
            Label = "vehicle",
            VehicleInstanceId = 1,
            LinkedVehicleInstanceId = 1,
            VehiclePartType = "plate"
        };

        AssertTrue(!TrackingIdentityHelper.ShouldSkipForSelection(body, plate), "A vehicle body must remain renderable when its plate is selected.");
        AssertTrue(TrackingIdentityHelper.ShouldSkipForSelection(plate, plate), "Only the selected plate should be skipped from the normal paint pass.");

        var personBody = new BoundingBox { Label = "person", PersonId = 3, PersonPartType = "body" };
        var face = new BoundingBox { Label = "person", PersonId = 3, LinkedPersonId = 3, PersonPartType = "face" };
        AssertTrue(!TrackingIdentityHelper.ShouldSkipForSelection(personBody, face), "A person body must remain renderable when its face is selected.");
    }

    private static void WaypointExitColumnNavigatesToExitFrame()
    {
        int targetFrame = WaypointListNavigationHelper.ResolveTargetFrame(1, 12, 48, out bool selectObject);

        AssertEqual(48, targetFrame, "The Exit column must navigate to the waypoint exit frame.");
        AssertTrue(!selectObject, "The Exit column must not select an object box.");

        targetFrame = WaypointListNavigationHelper.ResolveTargetFrame(2, 12, 48, out selectObject);
        AssertEqual(12, targetFrame, "The object column retains entry-frame navigation for box selection.");
        AssertTrue(selectObject, "The object column must select the matching object box.");
    }
    private static void VehicleTrackingComparisonUsesEffectiveIdentity()
    {
        var bodyAtA = new BoundingBox
        {
            Label = "vehicle",
            VehicleId = 3,
            VehicleInstanceId = 7,
            VehiclePartType = "body"
        };
        var bodyAtB = new BoundingBox
        {
            Label = "vehicle",
            VehicleId = 3,
            VehicleInstanceId = 7,
            VehiclePartType = "body"
        };
        var plate = new BoundingBox
        {
            Label = "vehicle",
            VehicleId = 3,
            VehicleInstanceId = 7,
            LinkedVehicleInstanceId = 7,
            VehiclePartType = "plate"
        };

        AssertTrue(TrackingIdentityHelper.AreSameTrackingTarget(bodyAtA, bodyAtB), "Vehicle body boxes with the same instance must match.");
        AssertEqual(7, TrackingIdentityHelper.GetNumericIdentity(plate), "Plate tracking identity must resolve to its linked vehicle instance.");
    }
    private static void VehicleWaypointMatchingIncludesBodyAndLinkedPlate()
    {
        var waypoint = new WaypointMarker
        {
            Label = "vehicle",
            ObjectId = 7,
            EntryFrame = 10,
            ExitFrame = 20
        };
        var body = new BoundingBox
        {
            Label = "vehicle",
            VehicleId = 3,
            VehicleInstanceId = 7,
            VehiclePartType = "body",
            FrameIndex = 12
        };
        var plate = new BoundingBox
        {
            Label = "vehicle",
            VehicleId = 3,
            VehicleInstanceId = 7,
            LinkedVehicleInstanceId = 7,
            VehiclePartType = "plate",
            FrameIndex = 12
        };
        var legacyBody = new BoundingBox
        {
            Label = "vehicle",
            VehicleId = 3,
            VehicleInstanceId = 0,
            VehiclePartType = "body",
            FrameIndex = 12
        };
        var legacyWaypoint = new WaypointMarker
        {
            Label = "vehicle",
            ObjectId = 3,
            EntryFrame = 10,
            ExitFrame = 20
        };

        AssertTrue(TrackingIdentityHelper.MatchesWaypoint(body, waypoint), "Current vehicle body must match by instance id.");
        AssertTrue(TrackingIdentityHelper.MatchesWaypoint(plate, waypoint), "Linked plate must match its vehicle waypoint.");
        AssertTrue(TrackingIdentityHelper.MatchesWaypoint(legacyBody, legacyWaypoint), "Legacy vehicle data must match by VehicleId fallback.");
    }

    private static void EventWorkflowSkipsVehicleWaypointSideEffects()
    {
        AssertTrue(
            VehicleEventWorkflowHelper.ShouldSkipVehicleWaypointSideEffects("event", 1),
            "Event-driven workflows with entry event boxes should not mutate vehicle waypoint state as a side effect.");
        AssertTrue(
            !VehicleEventWorkflowHelper.ShouldSkipVehicleWaypointSideEffects("vehicle", 1),
            "Vehicle workflows should still process vehicle waypoint state normally.");
        AssertTrue(
            !VehicleEventWorkflowHelper.ShouldSkipVehicleWaypointSideEffects("event", 0),
            "Without event boxes in range, vehicle side effects should not be blocked by this helper.");
    }

    private static void EventTypeUpdateResolvesActiveBoxesByEventInstance()
    {
        var selected = new BoundingBox
        {
            Label = "event",
            EventId = 1,
            EventInstanceId = "event-a",
            FrameIndex = 12,
            Rectangle = new Rectangle(0, 0, 10, 10)
        };
        var sameWaypointDifferentRectangle = new BoundingBox
        {
            Label = "event",
            EventId = 1,
            EventInstanceId = "event-a",
            FrameIndex = 13,
            Rectangle = new Rectangle(20, 20, 30, 30)
        };
        var deleted = new BoundingBox
        {
            Label = "event",
            EventId = 1,
            EventInstanceId = "event-a",
            FrameIndex = 14,
            IsDeleted = true
        };
        var otherWaypoint = new BoundingBox
        {
            Label = "event",
            EventId = 1,
            EventInstanceId = "event-b",
            FrameIndex = 12
        };
        var waypoint = new WaypointMarker
        {
            Label = "event",
            EventInstanceId = "event-a",
            ObjectId = 1,
            EntryFrame = 10,
            ExitFrame = 20
        };

        var resolved = EventWaypointUpdateHelper.ResolveActiveBoxes(
            selected,
            new[] { selected, sameWaypointDifferentRectangle, deleted, otherWaypoint },
            new[] { waypoint });

        AssertEqual(2, resolved.Count, "Only active boxes with the selected EventInstanceId should be updated.");
        AssertTrue(resolved.Contains(selected), "The selected event box should be included.");
        AssertTrue(resolved.Contains(sameWaypointDifferentRectangle), "Rectangle changes must not split an event waypoint.");
        AssertTrue(!resolved.Contains(deleted), "Deleted event boxes must remain unchanged.");
        AssertTrue(!resolved.Contains(otherWaypoint), "Same-type boxes in another event instance must remain unchanged.");
    }

    private static void EventTypeUpdateLegacyFallbackStaysInsideOneWaypoint()
    {
        var selected = new BoundingBox { Label = "event", EventId = 2, FrameIndex = 35 };
        var sameLegacyWaypoint = new BoundingBox { Label = "event", EventId = 2, FrameIndex = 36 };
        var outsideRange = new BoundingBox { Label = "event", EventId = 2, FrameIndex = 60 };
        var differentPriorType = new BoundingBox { Label = "event", EventId = 3, FrameIndex = 37 };
        var modernBox = new BoundingBox { Label = "event", EventId = 2, EventInstanceId = "modern", FrameIndex = 38 };
        var waypoint = new WaypointMarker
        {
            Label = "event",
            ObjectId = 2,
            EntryFrame = 30,
            ExitFrame = 40
        };

        var resolved = EventWaypointUpdateHelper.ResolveActiveBoxes(
            selected,
            new[] { selected, sameLegacyWaypoint, outsideRange, differentPriorType, modernBox },
            new[] { waypoint });

        AssertEqual(2, resolved.Count, "Legacy updates should stay in the selected waypoint range and prior EventId.");
        AssertTrue(resolved.Contains(selected), "The selected legacy event box should be included.");
        AssertTrue(resolved.Contains(sameLegacyWaypoint), "Matching legacy boxes inside the range should be included.");
        AssertTrue(!resolved.Contains(outsideRange), "Legacy boxes outside the waypoint range must not be updated.");
        AssertTrue(!resolved.Contains(differentPriorType), "A different prior EventId must not be updated.");
        AssertTrue(!resolved.Contains(modernBox), "Legacy fallback must not update a modern event instance.");
    }

    private static void EventTypeUpdateCreatesReversibleSnapshots()
    {
        var first = new BoundingBox { Label = "event", EventId = 1, EventInstanceId = "event-a" };
        var second = new BoundingBox { Label = "event", EventId = 1, EventInstanceId = "event-a" };

        var changes = EventWaypointUpdateHelper.CreateEventIdChanges(new[] { first, second }, 4);

        AssertEqual(2, changes.Count, "Every selected active box needs one event-id snapshot.");
        AssertTrue(changes.TrueForAll(change => change.OriginalEventId == 1 && change.NewEventId == 4), "Snapshots must preserve both the original and chosen event IDs.");
        AssertTrue(changes.TrueForAll(change => ReferenceEquals(change.Box, first) || ReferenceEquals(change.Box, second)), "Snapshots must retain stable box references for undo and redo.");
        AssertEqual(1, first.EventId, "Creating snapshots must not mutate EventId before the UI applies the change.");
        AssertEqual(1, second.EventId, "Creating snapshots must not mutate EventId before the UI applies the change.");
        AssertEqual("event-a", first.EventInstanceId, "Changing EventId must not alter EventInstanceId.");
        AssertEqual("event-a", second.EventInstanceId, "Changing EventId must not alter EventInstanceId.");
    }

    private static void EventTypeUpdateResolvesMixedMarkerMetadata()
    {
        var selected = new BoundingBox { Label = "event", EventId = 2, EventInstanceId = "event-a", FrameIndex = 35 };
        var sameWaypoint = new BoundingBox { Label = "event", EventId = 2, EventInstanceId = "event-a", FrameIndex = 36 };
        var deleted = new BoundingBox { Label = "event", EventId = 2, EventInstanceId = "event-a", FrameIndex = 37, IsDeleted = true };
        var other = new BoundingBox { Label = "event", EventId = 2, EventInstanceId = "event-b", FrameIndex = 35 };
        var marker = new WaypointMarker { Label = "event", ObjectId = 2, EntryFrame = 30, ExitFrame = 40 };

        var scope = EventWaypointUpdateHelper.ResolveActiveScope(selected, new[] { selected, sameWaypoint, deleted, other }, new[] { marker });

        AssertTrue(scope != null, "One blank-instance marker with the prior EventId should resolve mixed metadata.");
        AssertTrue(ReferenceEquals(marker, scope!.Waypoint), "The resolved marker must be available for synchronized waypoint updates.");
        AssertEqual(2, scope.Boxes.Count, "Mixed metadata must update only active boxes in the selected event instance.");
        AssertTrue(!scope.Boxes.Contains(deleted) && !scope.Boxes.Contains(other), "Deleted and different-instance boxes must remain outside the scope.");
    }

    private static void EventTypeUpdateRejectsAmbiguousMixedMarkers()
    {
        var selected = new BoundingBox { Label = "event", EventId = 2, EventInstanceId = "event-a", FrameIndex = 35 };
        var first = new WaypointMarker { Label = "event", ObjectId = 2, EntryFrame = 30, ExitFrame = 40 };
        var second = new WaypointMarker { Label = "event", ObjectId = 2, EntryFrame = 34, ExitFrame = 45 };

        var scope = EventWaypointUpdateHelper.ResolveActiveScope(selected, new[] { selected }, new[] { first, second });

        AssertTrue(scope == null, "Ambiguous blank-instance markers must fail closed.");
    }

    private static void EventWaypointDisplaySelectsMatchingInstance()
    {
        var waypoint = new WaypointMarker { Label = "event", ObjectId = 3, EventInstanceId = "event-b", EntryFrame = 10, ExitFrame = 20 };
        var wrong = new BoundingBox { Label = "event", EventId = 3, EventInstanceId = "event-a", FrameIndex = 12 };
        var deleted = new BoundingBox { Label = "event", EventId = 3, EventInstanceId = "event-b", FrameIndex = 13, IsDeleted = true };
        var matching = new BoundingBox { Label = "event", EventId = 3, EventInstanceId = "event-b", FrameIndex = 14 };

        var display = EventWaypointUpdateHelper.FindDisplayBox(new[] { wrong, deleted, matching }, waypoint);

        AssertTrue(ReferenceEquals(matching, display), "The event row must use the active box from its own EventInstanceId.");
    }

    private static void EventWaypointListRowUsesVideoTimeAndEventName()
    {
        var waypoint = new WaypointMarker
        {
            Label = "event",
            EntryTime = "00:06:05",
            ExitTime = "00:07:01",
            InteractingObject = "vehicle_car_01"
        };

        var row = EventWaypointListRowHelper.Create(waypoint, "contact");

        AssertEqual("00:06:05", row.Entry, "Event Entry must use the waypoint video-time value.");
        AssertEqual("00:07:01", row.Exit, "Event Exit must use the waypoint video-time value.");
        AssertEqual("contact", row.Object, "The Object column must display the event name.");
        AssertEqual("vehicle_car_01", waypoint.InteractingObject, "Hiding interacting-object data must not remove it from the waypoint.");
    }

    private static void ActiveListOwnerPrefersCurrentListSelection()
    {
        string owner = WaypointSelectionHelper.ResolveActiveListOwner(
            currentOwner: "vehicle",
            personSelected: true,
            vehicleSelected: true,
            eventSelected: false);

        AssertEqual("vehicle", owner, "The currently active list should stay authoritative when it still has a selection.");
    }

    private static void ActiveListOwnerFallsBackToRemainingSelection()
    {
        string owner = WaypointSelectionHelper.ResolveActiveListOwner(
            currentOwner: "person",
            personSelected: false,
            vehicleSelected: true,
            eventSelected: false);

        AssertEqual("vehicle", owner, "When the old owner is cleared, ownership should fall back to the remaining selected list.");

        string none = WaypointSelectionHelper.ResolveActiveListOwner(
            currentOwner: "event",
            personSelected: false,
            vehicleSelected: false,
            eventSelected: false);

        AssertTrue(none == null, "If no list has a selection, there should be no active owner.");
    }
    private static void PlateAFrameLookupSelectsBodyWithoutLosingPlateIdentity()
    {
        var body = new BoundingBox
        {
            Label = "vehicle",
            FrameIndex = 10,
            VehicleId = 1,
            VehicleInstanceId = 7,
            VehiclePartType = "body"
        };
        var plate = new BoundingBox
        {
            Label = "vehicle",
            FrameIndex = 10,
            VehicleId = 1,
            VehicleInstanceId = 7,
            LinkedVehicleInstanceId = 7,
            VehiclePartType = "plate"
        };

        var candidate = TrackingIdentityHelper.FindVehicleBodyAtFrame(
            new[] { plate, body },
            10,
            plate);

        AssertTrue(ReferenceEquals(body, candidate), "Plate a-frame lookup must resolve the vehicle body.");
        AssertTrue(TrackingIdentityHelper.IsPlate(plate), "The original plate seed must remain a plate.");
        AssertEqual(7, TrackingIdentityHelper.GetNumericIdentity(plate), "Plate seed identity must remain linked to the same vehicle instance.");
    }
    private static void IndependentPlateLookupIgnoresVehicleBody()
    {
        var body = new BoundingBox
        {
            Label = "vehicle",
            FrameIndex = 20,
            VehicleInstanceId = 9,
            VehiclePartType = "body"
        };
        var plate = new BoundingBox
        {
            Label = "vehicle",
            FrameIndex = 20,
            VehicleInstanceId = 9,
            LinkedVehicleInstanceId = 9,
            VehiclePartType = "plate"
        };

        var result = TrackingIdentityHelper.FindPlateAtFrame(new[] { body, plate }, 20, plate);
        AssertTrue(ReferenceEquals(plate, result), "Independent plate lookup must not select the vehicle body.");
    }
    private static void VehicleWaypointsMergeOverlapsAndKeepDisjointClips()
    {
        var waypoints = new List<WaypointMarker>
        {
            new() { Label = "vehicle", ObjectId = 4, EntryFrame = 10, ExitFrame = 20 },
            new() { Label = "vehicle", ObjectId = 4, EntryFrame = 21, ExitFrame = 30 },
            new() { Label = "vehicle", ObjectId = 4, EntryFrame = 50, ExitFrame = 60 },
            new() { Label = "vehicle", ObjectId = 8, EntryFrame = 10, ExitFrame = 20 }
        };

        var normalized = WaypointNormalizer.NormalizeVehicleWaypoints(waypoints);
        AssertEqual(3, normalized.Count, "Overlapping/adjacent same-instance clips should merge while disjoint and different-instance clips remain.");
        AssertEqual(10, normalized[0].EntryFrame, "Merged clip should preserve the earliest entry.");
        AssertEqual(30, normalized[0].ExitFrame, "Merged clip should extend through the latest adjacent exit.");
        AssertEqual(8, normalized[1].ObjectId, "Different vehicle instance should remain separate.");
        AssertEqual(50, normalized[2].EntryFrame, "Disjoint clip should remain separate.");
    }

    private static void ChildRectangleFollowsTrackedParent()
    {
        var mapped = WaypointNormalizer.MapChildRectangle(
            new System.Drawing.Rectangle(10, 10, 100, 100),
            new System.Drawing.Rectangle(20, 30, 200, 150),
            new System.Drawing.Rectangle(35, 40, 20, 30));

        AssertEqual(70, mapped.X, "Child X should follow parent translation and scale.");
        AssertEqual(75, mapped.Y, "Child Y should follow parent translation and scale.");
        AssertEqual(40, mapped.Width, "Child width should follow parent scale.");
        AssertEqual(45, mapped.Height, "Child height should follow parent scale.");
    }
    private static void OverlapDetectsInnerRange()
    {
        AssertTrue(WaypointOverlapHelper.RangesOverlap(15, 20, 10, 25), "Expected overlapping ranges.");
    }

    private static void OverlapDetectsCoveringRange()
    {
        AssertTrue(WaypointOverlapHelper.RangesOverlap(5, 30, 10, 25), "Expected covering range to overlap.");
    }

    private static void EventOverlapIgnoresDifferentInstanceIds()
    {
        var waypoints = new List<WaypointMarker>
        {
            new() { Label = "event", ObjectId = 3, EntryFrame = 10, ExitFrame = 30, EventInstanceId = "evt-a" }
        };

        var overlapping = WaypointOverlapHelper.FindOverlappingWaypoint(waypoints, "event", 3, 15, 20, "evt-b");
        AssertTrue(overlapping == null, "Different event instance ids should not overlap.");
    }

    private static void EventOverlapMatchesEmptyInstanceIds()
    {
        var waypoints = new List<WaypointMarker>
        {
            new() { Label = "event", ObjectId = 7, EntryFrame = 10, ExitFrame = 30 }
        };

        var overlapping = WaypointOverlapHelper.FindOverlappingWaypoint(waypoints, "event", 7, 15, 20);
        AssertTrue(overlapping != null && overlapping.ObjectId == 7, "Empty event instance ids should still match by event id.");
    }

    private static void VideoLoadRequestCancelsPreviousRequest()
    {
        using var coordinator = new VideoLoadRequestCoordinator();
        var first = coordinator.BeginNewRequest();
        var second = coordinator.BeginNewRequest();

        AssertTrue(first.CancellationToken.IsCancellationRequested, "Starting a new request should cancel the previous token.");
        AssertTrue(!second.CancellationToken.IsCancellationRequested, "Latest request token should remain active.");
    }

    private static void VideoLoadRequestTracksLatestRequest()
    {
        using var coordinator = new VideoLoadRequestCoordinator();
        var first = coordinator.BeginNewRequest();
        var second = coordinator.BeginNewRequest();

        AssertTrue(!coordinator.IsCurrent(first.RequestId), "First request should no longer be current.");
        AssertTrue(coordinator.IsCurrent(second.RequestId), "Second request should be current.");
    }

    private static void TimelineLayoutMapsLabelsToRows()
    {
        AssertEqual(8, TimelineLayoutHelper.GetTimelineRowY("person"), "Person row Y should match AOL layout.");
        AssertEqual(24, TimelineLayoutHelper.GetTimelineRowY("vehicle"), "Vehicle row Y should be below person row.");
        AssertEqual(40, TimelineLayoutHelper.GetTimelineRowY("event"), "Event row Y should be below vehicle row.");
    }

    private static void TimelineSegmentsKeepMinimumWidth()
    {
        var waypoint = new WaypointMarker
        {
            Label = "person",
            ObjectId = 1,
            EntryFrame = 10,
            ExitFrame = 10
        };

        var segment = TimelineLayoutHelper.CreateSegmentLayout(waypoint, panelWidth: 120, totalFrames: 200);
        AssertTrue(segment.Width >= 4, "Single-frame segments should remain visible.");
    }

    private static void TimelineHitTestingReturnsMatchingWaypoint()
    {
        var target = new WaypointMarker
        {
            Label = "vehicle",
            ObjectId = 2,
            EntryFrame = 20,
            ExitFrame = 40
        };

        var other = new WaypointMarker
        {
            Label = "person",
            ObjectId = 7,
            EntryFrame = 20,
            ExitFrame = 40
        };

        var hit = TimelineLayoutHelper.TryHitWaypointSegment(
            new List<WaypointMarker> { other, target },
            mouseX: 70,
            mouseY: 30,
            panelWidth: 200,
            totalFrames: 100);

        AssertTrue(hit == target, "Hit testing should return the waypoint in the clicked row.");
    }


    private static void VideoControlsHeightPreservesObjectInfo()
    {
        AssertEqual(132, TimelineLayoutHelper.GetMinimumVideoControlsHeight(), "Video controls height should leave room for object info without clipping.");
    }

    private static void TimelinePanelHeightFitsAllRows()
    {
        AssertEqual(56, TimelineLayoutHelper.GetRequiredTimelinePanelHeight(), "Timeline panel height should fit header plus three rows.");
    }
    private static void TimelineWidthStopsBeforeObjectInfo()
    {
        int width = TimelineLayoutHelper.CalculateTimelineWidth(panelTimelineLeft: 70, objectInfoLeft: 1230, reservedGap: 8, minimumWidth: 100);
        AssertEqual(1152, width, "Timeline width should stop before object info with reserved gap.");
    }

    private static void PendingEventRangeReusesExistingInstanceId()
    {
        var existing = new BoundingBox
        {
            Label = "event",
            EventId = 4,
            EventInstanceId = "event-a",
            FrameIndex = 100
        };

        var exitFrameCandidate = new BoundingBox
        {
            Label = "event",
            EventId = 4,
            EventInstanceId = "event-b",
            FrameIndex = 140
        };

        string reused = EventFinalizationHelper.FindPendingEventInstanceId(
            new[] { existing },
            exitFrameCandidate,
            entryFrame: 100,
            currentFrame: 140);

        AssertEqual("event-a", reused, "Exit-frame event boxes should reuse the existing pending event instance id.");
    }

    private static void PendingEventRangeIgnoresDifferentEventIds()
    {
        var existing = new BoundingBox
        {
            Label = "event",
            EventId = 4,
            EventInstanceId = "event-a",
            FrameIndex = 100
        };

        var differentEvent = new BoundingBox
        {
            Label = "event",
            EventId = 7,
            EventInstanceId = "event-b",
            FrameIndex = 140
        };

        string reused = EventFinalizationHelper.FindPendingEventInstanceId(
            new[] { existing },
            differentEvent,
            entryFrame: 100,
            currentFrame: 140);

        AssertTrue(reused == null, "Pending instance reuse must ignore boxes from other event ids.");
    }

    private static void EventClampTrimsOnlyMatchingInstanceOverflow()
    {
        var waypoint = new WaypointMarker
        {
            Label = "event",
            ObjectId = 4,
            EventInstanceId = "event-a",
            EntryFrame = 100,
            ExitFrame = 120
        };

        var keptInRange = new BoundingBox { Label = "event", EventId = 4, EventInstanceId = "event-a", FrameIndex = 120 };
        var removedOverflow = new BoundingBox { Label = "event", EventId = 4, EventInstanceId = "event-a", FrameIndex = 121 };
        var siblingInstance = new BoundingBox { Label = "event", EventId = 4, EventInstanceId = "event-b", FrameIndex = 125 };

        var boxes = new List<BoundingBox> { keptInRange, removedOverflow, siblingInstance };
        int removed = EventFinalizationHelper.ClampEventBoxesToWaypointExit(boxes, waypoint);

        AssertEqual(1, removed, "Only the overflowing boxes from the matching event instance should be trimmed.");
        AssertTrue(boxes.Contains(keptInRange), "The last valid frame should remain.");
        AssertTrue(!boxes.Contains(removedOverflow), "Overflow box should be removed.");
        AssertTrue(boxes.Contains(siblingInstance), "Sibling event instances must remain untouched.");
    }

    private static void EventClampCanTrimAllWaypointOverflows()
    {
        var waypointA = new WaypointMarker
        {
            Label = "event",
            ObjectId = 4,
            EventInstanceId = "event-a",
            EntryFrame = 100,
            ExitFrame = 120
        };
        var waypointB = new WaypointMarker
        {
            Label = "event",
            ObjectId = 7,
            EventInstanceId = "event-b",
            EntryFrame = 200,
            ExitFrame = 205
        };

        var boxes = new List<BoundingBox>
        {
            new() { Label = "event", EventId = 4, EventInstanceId = "event-a", FrameIndex = 121 },
            new() { Label = "event", EventId = 7, EventInstanceId = "event-b", FrameIndex = 206 },
            new() { Label = "event", EventId = 7, EventInstanceId = "event-b", FrameIndex = 205 }
        };

        int removed = EventFinalizationHelper.ClampAllEventBoxesToWaypoints(boxes, new[] { waypointA, waypointB });

        AssertEqual(2, removed, "All waypoint-specific event overflow boxes should be trimmed.");
        AssertEqual(1, boxes.Count, "Only in-range event boxes should remain.");
        AssertEqual(205, boxes[0].FrameIndex, "The in-range event box should be preserved.");
    }

    private static void ClipResolverKeepsDisjointPersonClipsSeparate()
    {
        var waypoints = new List<WaypointMarker>
        {
            new() { Label = "person", ObjectId = 1, EntryFrame = 100, ExitFrame = 200 },
            new() { Label = "person", ObjectId = 1, EntryFrame = 300, ExitFrame = 500 }
        };

        var clip = WaypointClipResolver.ResolveClipRange("person", 1, 320, waypoints);
        AssertEqual(300, clip.EntryFrame, "Later disjoint clip should keep its own entry frame.");
        AssertEqual(500, clip.ExitFrame, "Later disjoint clip should keep its own exit frame.");
    }


    private static void ClipResolverChoosesLatestContainingClip()
    {
        var waypoints = new List<WaypointMarker>
        {
            new() { Label = "person", ObjectId = 1, EntryFrame = 100, ExitFrame = 500 },
            new() { Label = "person", ObjectId = 1, EntryFrame = 300, ExitFrame = 400 }
        };

        var clip = WaypointClipResolver.ResolveClipRange("person", 1, 320, waypoints);
        AssertEqual(300, clip.EntryFrame, "Nested clip should prefer the latest containing entry frame.");
        AssertEqual(400, clip.ExitFrame, "Nested clip should prefer the latest containing exit frame.");
    }

    private static void ClipResolverPrefersEventInstanceId()
    {
        var waypoints = new List<WaypointMarker>
        {
            new() { Label = "event", ObjectId = 4, EntryFrame = 100, ExitFrame = 200, EventInstanceId = "evt-a" },
            new() { Label = "event", ObjectId = 4, EntryFrame = 120, ExitFrame = 180, EventInstanceId = "evt-b" }
        };

        var clip = WaypointClipResolver.ResolveClipRange("event", 4, 150, waypoints, "evt-b");
        AssertEqual(120, clip.EntryFrame, "Event clip should match by instance id before shared object id.");
        AssertEqual(180, clip.ExitFrame, "Event clip should keep the matched instance range.");
    }

    private static void ClipResolverSupportsBoundingBoxInput()
    {
        var waypoints = new List<WaypointMarker>
        {
            new() { Label = "vehicle", ObjectId = 2, EntryFrame = 50, ExitFrame = 75 }
        };

        var box = new BoundingBox
        {
            Label = "vehicle",
            VehicleId = 2,
            VehicleInstanceId = 2,
            FrameIndex = 60
        };

        var clip = WaypointClipResolver.ResolveClipRange(box, waypoints);
        AssertEqual(50, clip.EntryFrame, "BoundingBox overload should resolve vehicle entry frame.");
        AssertEqual(75, clip.ExitFrame, "BoundingBox overload should resolve vehicle exit frame.");
    }
    private static void ClipResolverFallsBackToSingleFrame()
    {
        var clip = WaypointClipResolver.ResolveClipRange("person", 1, 320, new List<WaypointMarker>());
        AssertEqual(320, clip.EntryFrame, "Missing waypoint should fall back to the current frame entry.");
        AssertEqual(320, clip.ExitFrame, "Missing waypoint should fall back to the current frame exit.");
    }


    private static void FaceLinkExportConnectsFaceToBody()
    {
        var body = new BoundingBox
        {
            Label = "person",
            PersonId = 3,
            FrameIndex = 120,
            PersonPartType = "body"
        };

        var face = new BoundingBox
        {
            Label = "person",
            PersonId = 3,
            FrameIndex = 120,
            PersonPartType = "face",
            LinkedPersonId = 3
        };

        var annotations = new Dictionary<BoundingBox, AnnotationData>
        {
            [body] = new() { Id = 101, TrackId = 3 },
            [face] = new() { Id = 102, TrackId = 3 }
        };

        var link = FaceLinkHelper.TryCreateFaceLink(face, annotations.Values, annotations);
        AssertTrue(link != null, "Face link should be created when face and body annotations share the frame.");
        AssertEqual(102, link!.FaceAnnotationId, "Face link should point to the face annotation id.");
        AssertEqual(101, link.BodyAnnotationId, "Face link should point to the linked body annotation id.");
    }

    private static void FaceLinkImportMarksLinkedFaceBoxes()
    {
        var body = new BoundingBox
        {
            Label = "person",
            PersonId = 7,
            FrameIndex = 30
        };

        var face = new BoundingBox
        {
            Label = "person",
            PersonId = 99,
            FrameIndex = 30
        };

        var boxesByAnnotationId = new Dictionary<int, BoundingBox>
        {
            [101] = body,
            [102] = face
        };

        var annotationsById = new Dictionary<int, AnnotationData>
        {
            [101] = new() { Id = 101, TrackId = 7, TrackInfo = new TrackInfo { Entry = new TrackEntry { Frame = 20 }, Exit = new TrackEntry { Frame = 40 } } },
            [102] = new() { Id = 102, TrackId = 99, TrackInfo = new TrackInfo { Entry = new TrackEntry { Frame = 30 }, Exit = new TrackEntry { Frame = 30 } } }
        };

        FaceLinkHelper.ApplyFaceLinks(
            new List<FaceLinkData> { new() { FaceAnnotationId = 102, BodyAnnotationId = 101 } },
            boxesByAnnotationId,
            annotationsById);

        AssertEqual("body", body.PersonPartType, "Linked body should be marked as body.");
        AssertEqual("face", face.PersonPartType, "Linked face should be marked as face.");
        AssertEqual(7, face.PersonId, "Face should inherit the linked body person id.");
        AssertEqual(7, face.LinkedPersonId, "Face should retain the linked body id.");
        AssertEqual(30, face.BoxEntryFrame, "Face should keep its track entry frame.");
        AssertEqual(30, face.BoxExitFrame, "Face should keep its track exit frame.");
    }

    private static void FaceLinkImportIgnoresMalformedLinks()
    {
        var body = new BoundingBox
        {
            Label = "person",
            PersonId = 7,
            FrameIndex = 30
        };

        var boxesByAnnotationId = new Dictionary<int, BoundingBox>
        {
            [101] = body
        };

        var annotationsById = new Dictionary<int, AnnotationData>
        {
            [101] = new() { Id = 101, TrackId = 7 }
        };

        FaceLinkHelper.ApplyFaceLinks(
            new List<FaceLinkData> { new() { FaceAnnotationId = 999, BodyAnnotationId = 101 } },
            boxesByAnnotationId,
            annotationsById);

        AssertTrue(string.IsNullOrEmpty(body.PersonPartType), "Malformed links should leave unrelated boxes unchanged.");
    }

    private static void LegacyVehicleTrackIdRemainsTypeId()
    {
        var legacy = new BoundingBox { Label = "vehicle", VehicleId = 2, VehicleInstanceId = 0 };
        var current = new BoundingBox { Label = "vehicle", VehicleId = 2, VehicleInstanceId = 9 };
        AssertEqual(2, TrackingIdentityHelper.GetNumericIdentity(legacy), "Legacy vehicle identity should fall back to VehicleId.");
        AssertEqual(9, TrackingIdentityHelper.GetNumericIdentity(current), "Current vehicle identity should use VehicleInstanceId.");
        var annotation = new AnnotationData { TrackId = legacy.VehicleId, VehicleInstanceId = current.VehicleInstanceId };
        AssertEqual(2, annotation.TrackId, "JSON track_id must retain the legacy vehicle type id.");
        AssertEqual(9, annotation.VehicleInstanceId.GetValueOrDefault(), "New instance id should use the optional field.");
    }
    private static void TwoSameTypeVehiclesGetDifferentInstanceIds()
    {
        var carOne = new BoundingBox { Label = "vehicle", VehicleId = 1, VehicleInstanceId = 1, FrameIndex = 10 };
        var carTwo = new BoundingBox { Label = "vehicle", VehicleId = 1, VehicleInstanceId = 2, FrameIndex = 10 };

        AssertEqual(carOne.VehicleId, carTwo.VehicleId, "Both vehicles should share the same type id.");
        AssertTrue(carOne.VehicleInstanceId != carTwo.VehicleInstanceId, "Same-type vehicles should have different instance ids.");
    }

    private static void VehicleExitBoxReusesSelectedEntryInstance()
    {
        var entryCar = new BoundingBox { Label = "vehicle", VehicleId = 1, VehicleInstanceId = 1, VehiclePartType = "body", FrameIndex = 10 };
        var laterCar = new BoundingBox { Label = "vehicle", VehicleId = 1, VehicleInstanceId = 5, VehiclePartType = "body", FrameIndex = 40 };

        int instanceId = VehicleInstanceAssignmentHelper.ResolveNewBodyInstanceId(
            new[] { entryCar, laterCar }, entryCar, currentFrame: 30, entryFrame: 10, vehicleTypeId: 1, fallbackInstanceId: 6);

        AssertEqual(1, instanceId, "An exit-frame box should reuse the selected entry vehicle instance.");
    }

    private static void VehicleExitBoxReusesUnambiguousEntryInstance()
    {
        var entryCar = new BoundingBox { Label = "vehicle", VehicleId = 1, VehicleInstanceId = 3, VehiclePartType = "body", FrameIndex = 10 };

        int instanceId = VehicleInstanceAssignmentHelper.ResolveNewBodyInstanceId(
            new[] { entryCar }, selectedBox: null, currentFrame: 30, entryFrame: 10, vehicleTypeId: 1, fallbackInstanceId: 4);

        AssertEqual(3, instanceId, "An exit-frame box should reuse the only matching entry vehicle when selection is unavailable.");
    }

    private static void VehicleExitBoxAvoidsAmbiguousEntryInstance()
    {
        var firstCar = new BoundingBox { Label = "vehicle", VehicleId = 1, VehicleInstanceId = 1, VehiclePartType = "body", FrameIndex = 10 };
        var secondCar = new BoundingBox { Label = "vehicle", VehicleId = 1, VehicleInstanceId = 2, VehiclePartType = "body", FrameIndex = 10 };

        int instanceId = VehicleInstanceAssignmentHelper.ResolveNewBodyInstanceId(
            new[] { firstCar, secondCar }, selectedBox: null, currentFrame: 30, entryFrame: 10, vehicleTypeId: 1, fallbackInstanceId: 3);

        AssertEqual(3, instanceId, "Ambiguous same-type entry vehicles must not be merged automatically.");
    }

    private static void VehicleTrackingPreservesExistingExitBox()
    {
        var existingExitBox = new BoundingBox
        {
            Label = "vehicle",
            VehicleId = 1,
            VehicleInstanceId = 3,
            VehiclePartType = "body",
            FrameIndex = 30
        };
        var trackedInteriorBox = new BoundingBox
        {
            Label = "vehicle",
            VehicleId = 1,
            VehicleInstanceId = 3,
            VehiclePartType = "body",
            FrameIndex = 20
        };
        var trackedExitBox = new BoundingBox
        {
            Label = "vehicle",
            VehicleId = 1,
            VehicleInstanceId = 3,
            VehiclePartType = "body",
            FrameIndex = 30
        };
        var waypoint = new WaypointMarker { Label = "vehicle", ObjectId = 3, EntryFrame = 10, ExitFrame = 30 };

        var boxesToAdd = TrackingResultMergeHelper.ExcludeExistingVehicleExitBoxes(
            new[] { trackedInteriorBox, trackedExitBox },
            new[] { existingExitBox },
            waypoint);

        AssertEqual(1, boxesToAdd.Count, "The duplicate vehicle tracking box at exit must be excluded.");
        AssertTrue(ReferenceEquals(trackedInteriorBox, boxesToAdd[0]), "Tracking should still add boxes before the exit frame.");
    }

    private static void PlateLinkExportConnectsPlateToBody()
    {
        var body = new BoundingBox
        {
            Label = "vehicle",
            VehicleId = 1,
            VehicleInstanceId = 5,
            FrameIndex = 120,
            VehiclePartType = "body"
        };

        var plate = new BoundingBox
        {
            Label = "vehicle",
            VehicleId = 1,
            VehicleInstanceId = 5,
            FrameIndex = 120,
            VehiclePartType = "plate",
            LinkedVehicleInstanceId = 5
        };

        var annotations = new Dictionary<BoundingBox, AnnotationData>
        {
            [body] = new() { Id = 201, TrackId = 5 },
            [plate] = new() { Id = 202, TrackId = 5 }
        };

        var link = PlateLinkHelper.TryCreatePlateLink(plate, annotations.Values, annotations);
        AssertTrue(link != null, "Plate link should be created when plate and body annotations share the frame.");
        AssertEqual(202, link!.PlateAnnotationId, "Plate link should point to the plate annotation id.");
        AssertEqual(201, link.BodyAnnotationId, "Plate link should point to the linked body annotation id.");
    }

    private static void PlateLinkImportMarksLinkedPlateBoxes()
    {
        var body = new BoundingBox
        {
            Label = "vehicle",
            VehicleId = 1,
            VehicleInstanceId = 7,
            FrameIndex = 30
        };

        var plate = new BoundingBox
        {
            Label = "vehicle",
            VehicleId = 1,
            VehicleInstanceId = 99,
            FrameIndex = 30
        };

        var boxesByAnnotationId = new Dictionary<int, BoundingBox>
        {
            [201] = body,
            [202] = plate
        };

        var annotationsById = new Dictionary<int, AnnotationData>
        {
            [201] = new() { Id = 201, TrackId = 7, TrackInfo = new TrackInfo { Entry = new TrackEntry { Frame = 20 }, Exit = new TrackEntry { Frame = 40 } } },
            [202] = new() { Id = 202, TrackId = 99, TrackInfo = new TrackInfo { Entry = new TrackEntry { Frame = 30 }, Exit = new TrackEntry { Frame = 30 } } }
        };

        PlateLinkHelper.ApplyPlateLinks(
            new List<PlateLinkData> { new() { PlateAnnotationId = 202, BodyAnnotationId = 201 } },
            boxesByAnnotationId,
            annotationsById);

        AssertEqual("body", body.VehiclePartType, "Linked body should be marked as body.");
        AssertEqual("plate", plate.VehiclePartType, "Linked plate should be marked as plate.");
        AssertEqual(7, plate.VehicleInstanceId, "Plate should inherit the linked body vehicle instance id.");
        AssertEqual(7, plate.LinkedVehicleInstanceId, "Plate should retain the linked body instance id.");
        AssertEqual(1, plate.VehicleId, "Plate should inherit the linked body vehicle type id.");
        AssertEqual(30, plate.BoxEntryFrame, "Plate should keep its track entry frame.");
        AssertEqual(30, plate.BoxExitFrame, "Plate should keep its track exit frame.");
    }

    private static void PlateLinkImportIgnoresMalformedLinks()
    {
        var body = new BoundingBox
        {
            Label = "vehicle",
            VehicleId = 1,
            VehicleInstanceId = 7,
            FrameIndex = 30
        };

        var boxesByAnnotationId = new Dictionary<int, BoundingBox>
        {
            [201] = body
        };

        var annotationsById = new Dictionary<int, AnnotationData>
        {
            [201] = new() { Id = 201, TrackId = 7 }
        };

        PlateLinkHelper.ApplyPlateLinks(
            new List<PlateLinkData> { new() { PlateAnnotationId = 999, BodyAnnotationId = 201 } },
            boxesByAnnotationId,
            annotationsById);

        AssertTrue(string.IsNullOrEmpty(body.VehiclePartType), "Malformed links should leave unrelated boxes unchanged.");
    }

    private static void EventCatalogIncludesNewValues()
    {
        var expected = new[] { "contact", "throw", "final_exchange", "get on", "get off", "suspect", "controlled_delivery", "camouflage" };
        AssertEqual(expected.Length, LabelCatalogHelper.EventTypes.Length, "Event catalog should contain 8 event types.");
        for (int i = 0; i < expected.Length; i++)
        {
            AssertEqual(expected[i], LabelCatalogHelper.EventTypes[i], $"Event type at index {i} should match the expected catalog order.");
        }

        AssertEqual(25, LabelCatalogHelper.GetEventCategoryId("contact"), "contact should map to category id 25.");
        AssertEqual(26, LabelCatalogHelper.GetEventCategoryId("throw"), "throw should map to category id 26.");
        AssertEqual(31, LabelCatalogHelper.GetEventCategoryId("controlled_delivery"), "controlled_delivery should map to category id 31.");
        AssertEqual(32, LabelCatalogHelper.GetEventCategoryId("camouflage"), "camouflage should map to category id 32.");
        AssertEqual(33, LabelCatalogHelper.GetPlateCategoryId(), "plate should map to category id 33.");

        AssertEqual(2, LabelCatalogHelper.GetEventIdFromImportedCategory(26, "exchange"), "Legacy exchange should migrate to throw.");
        AssertEqual(4, LabelCatalogHelper.GetEventIdFromImportedCategory(27, "board"), "Legacy board should migrate to get on.");
        AssertEqual(5, LabelCatalogHelper.GetEventIdFromImportedCategory(29, "disembark"), "Legacy disembark should migrate to get off.");
        AssertEqual(8, LabelCatalogHelper.GetEventIdFromImportedCategory(31, "camouflage"), "Legacy camouflage should remain camouflage.");
        AssertEqual(2, LabelCatalogHelper.GetEventIdFromImportedCategory(32, "throw"), "Legacy throw should retain its event meaning.");
        AssertEqual(2, LabelCatalogHelper.GetEventIdFromImportedCategory(26, "throw"), "New category names must override legacy numeric positions.");
        AssertEqual(4, LabelCatalogHelper.GetEventIdFromComboItem("event_get on"), "The event creation selector should resolve get on.");
        AssertEqual(6, LabelCatalogHelper.GetEventIdFromComboItem("event_suspect"), "The event creation selector should resolve suspect.");
    }

    private static void EventComboItemsIncludeEveryCatalogEvent()
    {
        var items = LabelCatalogHelper.GetEventComboItems();
        AssertEqual(LabelCatalogHelper.EventTypes.Length, items.Length, "Event combo should expose every catalog event.");

        for (int i = 0; i < items.Length; i++)
        {
            AssertEqual(
                "event_" + LabelCatalogHelper.EventTypes[i],
                items[i],
                "Event combo item " + i + " should match the catalog.");
        }
    }

    private static void VehicleDisplayLabelsIncludeInstanceAndPlateRelationship()
    {
        AssertEqual(
            "vehicle_car_01",
            LabelCatalogHelper.GetVehicleDisplayLabel(1, 1, "body", null),
            "Vehicle body label should include type and instance id.");

        AssertEqual(
            "vehicle_plate->body_car_01",
            LabelCatalogHelper.GetVehicleDisplayLabel(1, 1, "plate", 1),
            "Plate label should identify its linked vehicle body.");
    }
    private static void VehicleWaypointCandidatesExcludePlatesAndDeduplicateInstances()
    {
        var boxes = new List<BoundingBox>
        {
            new() { Label = "vehicle", VehicleInstanceId = 4, VehicleId = 1, FrameIndex = 10, VehiclePartType = "body" },
            new() { Label = "vehicle", VehicleInstanceId = 4, VehicleId = 1, FrameIndex = 10, VehiclePartType = "plate" },
            new() { Label = "vehicle", VehicleInstanceId = 4, VehicleId = 1, FrameIndex = 10, VehiclePartType = "body" },
            new() { Label = "vehicle", VehicleInstanceId = 5, VehicleId = 1, FrameIndex = 10, VehiclePartType = "body" }
        };

        var candidates = WaypointGroupingHelper.GetVehicleWaypointBodies(boxes, 10);

        AssertEqual(2, candidates.Count, "Each vehicle instance should produce one body waypoint candidate.");
        AssertTrue(candidates.TrueForAll(box => !string.Equals(box.VehiclePartType, "plate", StringComparison.OrdinalIgnoreCase)), "Plate annotations must not create vehicle waypoints.");
        AssertTrue(candidates.Exists(box => box.VehicleInstanceId == 4), "Instance 4 body should remain.");
        AssertTrue(candidates.Exists(box => box.VehicleInstanceId == 5), "Instance 5 body should remain.");

        var legacyCandidates = WaypointGroupingHelper.GetVehicleWaypointBodies(
            new List<BoundingBox>
            {
                new() { Label = "vehicle", VehicleId = 2, VehicleInstanceId = 0, FrameIndex = 10, VehiclePartType = "body" }
            },
            10);
        AssertEqual(1, legacyCandidates.Count, "Legacy vehicles without an instance id should still create a waypoint candidate.");
        AssertEqual(2, TrackingIdentityHelper.GetNumericIdentity(legacyCandidates[0]), "Legacy waypoint identity should fall back to VehicleId.");
    }
    private static void CamouflageAttributeExposedOnce()
    {
        AssertTrue(PersonAttributeStore.singleSelectAttributeNames.Contains("Camouflage"), "Camouflage should be registered as a single-select attribute.");
        AssertTrue(!PersonAttributeStore.IsWaypointScoped("Camouflage"), "Camouflage should not be waypoint-scoped.");

        // Accessing the static Korean value map triggers its static constructor; a duplicate
        // dictionary key would throw here instead of returning a value.
        string korean = PersonAttributesForm.GetAttributeValueKorean("camouflage");
        AssertEqual("변장 상태", korean, "camouflage value should map to the expected Korean display text.");
    }

    private static void LineWidthConstantsReflectReducedThickness()
    {
        AssertEqual(2f, DrawingStyleHelper.DefaultBoxPenWidth, "Normal box pen width should be reduced to 2.");
        AssertEqual(3f, DrawingStyleHelper.SelectedBoxPenWidth, "Selected box pen width should be reduced to 3.");
        AssertEqual(2f, DrawingStyleHelper.DraftBoxPenWidth, "Draft box pen width should be reduced to 2.");
    }

    private static void FaceDeletionStaysWithinLinkedParentWaypoint()
    {
        var selectedFace = new BoundingBox { Label = "person", PersonPartType = "face", PersonId = 5, LinkedPersonId = 5, FrameIndex = 12 };
        var sameFaceLater = new BoundingBox { Label = "person", PersonPartType = "face", PersonId = 5, LinkedPersonId = 5, FrameIndex = 15 };
        var sameFaceOutside = new BoundingBox { Label = "person", PersonPartType = "face", PersonId = 5, LinkedPersonId = 5, FrameIndex = 21 };
        var otherFace = new BoundingBox { Label = "person", PersonPartType = "face", PersonId = 6, LinkedPersonId = 6, FrameIndex = 12 };
        var body = new BoundingBox { Label = "person", PersonPartType = "body", PersonId = 5, FrameIndex = 12 };
        var scope = SubAnnotationDeletionHelper.GetWaypointScopedSubAnnotations(
            new[] { selectedFace, sameFaceLater, sameFaceOutside, otherFace, body },
            new[] { new WaypointMarker { Label = "person", ObjectId = 5, EntryFrame = 10, ExitFrame = 20 } },
            selectedFace);

        AssertEqual(2, scope.Count, "Face deletion should include only same-person faces inside the parent waypoint.");
        AssertTrue(scope.Contains(selectedFace) && scope.Contains(sameFaceLater), "Selected and later same-person faces should be included.");
        AssertTrue(!scope.Contains(sameFaceOutside) && !scope.Contains(otherFace) && !scope.Contains(body), "Faces outside the range, other identities, and bodies must remain excluded.");
    }

    private static void PlateDeletionStaysWithinLinkedParentWaypoint()
    {
        var selectedPlate = new BoundingBox { Label = "vehicle", VehiclePartType = "plate", VehicleInstanceId = 7, LinkedVehicleInstanceId = 7, FrameIndex = 12 };
        var samePlateLater = new BoundingBox { Label = "vehicle", VehiclePartType = "plate", VehicleInstanceId = 7, LinkedVehicleInstanceId = 7, FrameIndex = 18 };
        var otherPlate = new BoundingBox { Label = "vehicle", VehiclePartType = "plate", VehicleInstanceId = 8, LinkedVehicleInstanceId = 8, FrameIndex = 12 };
        var body = new BoundingBox { Label = "vehicle", VehiclePartType = "body", VehicleInstanceId = 7, FrameIndex = 12 };
        var scope = SubAnnotationDeletionHelper.GetWaypointScopedSubAnnotations(
            new[] { selectedPlate, samePlateLater, otherPlate, body },
            new[] { new WaypointMarker { Label = "vehicle", ObjectId = 7, EntryFrame = 10, ExitFrame = 20 } },
            selectedPlate);

        AssertEqual(2, scope.Count, "Plate deletion should include only same-vehicle plates inside the parent waypoint.");
        AssertTrue(scope.Contains(selectedPlate) && scope.Contains(samePlateLater), "Selected and later same-vehicle plates should be included.");
        AssertTrue(!scope.Contains(otherPlate) && !scope.Contains(body), "Other vehicle plates and the vehicle body must remain excluded.");
    }

    private static void SubAnnotationDeletionRejectsAmbiguousParentWaypoints()
    {
        var face = new BoundingBox { Label = "person", PersonPartType = "face", PersonId = 5, LinkedPersonId = 5, FrameIndex = 12 };
        var scope = SubAnnotationDeletionHelper.GetWaypointScopedSubAnnotations(
            new[] { face },
            new[]
            {
                new WaypointMarker { Label = "person", ObjectId = 5, EntryFrame = 10, ExitFrame = 20 },
                new WaypointMarker { Label = "person", ObjectId = 5, EntryFrame = 11, ExitFrame = 21 }
            },
            face);

        AssertEqual(0, scope.Count, "Ambiguous parent waypoints must fail closed without a deletion scope.");
    }

    private static void DeletedChildAnnotationsCannotCreateExportLinks()
    {
        var body = new BoundingBox { Label = "person", PersonPartType = "body", PersonId = 5, FrameIndex = 12 };
        var deletedFace = new BoundingBox { Label = "person", PersonPartType = "face", PersonId = 5, LinkedPersonId = 5, FrameIndex = 12, IsDeleted = true };
        var activeBoxes = new[] { body, deletedFace }.Where(box => !box.IsDeleted).ToList();
        var annotationsByBox = new Dictionary<BoundingBox, AnnotationData>
        {
            [body] = new AnnotationData { Id = 101, TrackId = 5 }
        };

        AssertEqual(1, activeBoxes.Count, "Only the active parent body should reach export annotation construction.");
        AssertTrue(activeBoxes.Contains(body), "The parent body annotation must remain exportable.");
        AssertTrue(FaceLinkHelper.TryCreateFaceLink(deletedFace, annotationsByBox.Values, annotationsByBox) == null, "A deleted face without an exported annotation must not create a face link.");
    }

    private static void EventBoxPropagationUpdatesForwardSameInstanceRange()
    {
        var source = CreateEventBox(10, "instance-a", new System.Drawing.Rectangle(10, 20, 30, 40));
        var beforeSource = CreateEventBox(9, "instance-a", new System.Drawing.Rectangle(1, 2, 3, 4));
        var existingTarget = CreateEventBox(11, "instance-a", new System.Drawing.Rectangle(5, 6, 7, 8));
        var boxes = new List<BoundingBox> { beforeSource, source, existingTarget };

        var plan = EventWaypointBoxPropagationHelper.PlanPropagation(
            source,
            boxes,
            new[] { CreateEventWaypoint("instance-a", 8, 13) },
            new Dictionary<string, List<int>>());

        AssertEqual(1, plan.Updates.Count, "Only the existing same-instance box after the source frame should update.");
        AssertTrue(ReferenceEquals(existingTarget, plan.Updates[0].Box), "Updates must retain stable target box references.");
        AssertEqual(source.Rectangle, plan.Updates[0].Rectangle, "The planned update must use the edited source rectangle.");
        AssertEqual(2, plan.Additions.Count, "Missing forward frames through the exit frame should receive derived boxes.");
        AssertTrue(plan.Additions.All(addition => addition.Box.FrameIndex is 12 or 13), "Only missing frames after the source through exit should be created.");
        AssertTrue(plan.Updates.All(update => update.Box.FrameIndex > source.FrameIndex), "No frame before or at the source frame may be updated.");
        AssertEqual(new System.Drawing.Rectangle(1, 2, 3, 4), beforeSource.Rectangle, "Earlier frames must remain unchanged.");
    }

    private static void EventBoxPropagationRespectsLaterManualAdjustments()
    {
        var source = CreateEventBox(10, "instance-a", new System.Drawing.Rectangle(10, 20, 30, 40));
        var manualTarget = CreateEventBox(11, "instance-a", new System.Drawing.Rectangle(80, 81, 82, 83));
        var plan = EventWaypointBoxPropagationHelper.PlanPropagation(
            source,
            new[] { source, manualTarget },
            new[] { CreateEventWaypoint("instance-a", 10, 12) },
            new Dictionary<string, List<int>>
            {
                [TrackingIdentityHelper.GetIdentityKey(source)] = new List<int> { 11 }
            });

        AssertTrue(plan.Updates.All(update => update.Box.FrameIndex != 11), "A later manually adjusted frame must not be updated.");
        AssertTrue(plan.Additions.All(addition => addition.Box.FrameIndex != 11), "A later manually adjusted frame must not be recreated.");
        AssertEqual(new System.Drawing.Rectangle(80, 81, 82, 83), manualTarget.Rectangle, "Manual target rectangles must remain unchanged.");
        AssertTrue(plan.Additions.Any(addition => addition.Box.FrameIndex == 12), "Unprotected later frames should remain eligible for creation.");
    }

    private static void EventBoxPropagationPreservesDeletedTombstones()
    {
        var source = CreateEventBox(10, "instance-a", new System.Drawing.Rectangle(10, 20, 30, 40));
        var tombstone = CreateEventBox(11, "instance-a", new System.Drawing.Rectangle(1, 1, 1, 1));
        tombstone.IsDeleted = true;
        var plan = EventWaypointBoxPropagationHelper.PlanPropagation(
            source,
            new[] { source, tombstone },
            new[] { CreateEventWaypoint("instance-a", 10, 12) },
            new Dictionary<string, List<int>>());

        AssertTrue(plan.Updates.All(update => !ReferenceEquals(update.Box, tombstone)), "Deleted tombstones must not be updated.");
        AssertTrue(plan.Additions.All(addition => addition.Box.FrameIndex != 11), "Deleted tombstones must block replacement creation at their frame.");
        AssertTrue(plan.Additions.Any(addition => addition.Box.FrameIndex == 12), "A deleted frame must not suppress propagation for later frames.");
        AssertTrue(tombstone.IsDeleted, "Planning must not clear a tombstone.");
    }

    private static void EventBoxPropagationIsolatesDifferentEventInstances()
    {
        var source = CreateEventBox(10, "instance-a", new System.Drawing.Rectangle(10, 20, 30, 40));
        var differentInstance = CreateEventBox(11, "instance-b", new System.Drawing.Rectangle(90, 91, 92, 93));
        var plan = EventWaypointBoxPropagationHelper.PlanPropagation(
            source,
            new[] { source, differentInstance },
            new[] { CreateEventWaypoint("instance-a", 10, 12) },
            new Dictionary<string, List<int>>());

        AssertTrue(plan.Updates.All(update => !ReferenceEquals(update.Box, differentInstance)), "An EventId-only match must never update another EventInstanceId.");
        AssertEqual(new System.Drawing.Rectangle(90, 91, 92, 93), differentInstance.Rectangle, "Different event instances must retain their rectangles.");
        AssertTrue(plan.Additions.Any(addition => addition.Box.FrameIndex == 11), "A different instance must not block creation for the selected instance.");
    }

    private static void EventBoxPropagationRejectsAmbiguousScopesWithoutRemovals()
    {
        var source = CreateEventBox(10, "instance-a", new System.Drawing.Rectangle(10, 20, 30, 40));
        var plan = EventWaypointBoxPropagationHelper.PlanPropagation(
            source,
            new[] { source },
            new[]
            {
                CreateEventWaypoint("instance-a", 10, 12),
                CreateEventWaypoint("instance-a", 10, 13)
            },
            new Dictionary<string, List<int>>());

        AssertEqual(0, plan.Updates.Count, "Ambiguous event waypoint scopes must fail closed.");
        AssertEqual(0, plan.Additions.Count, "Ambiguous event waypoint scopes must not create boxes.");
        AssertTrue(typeof(EventWaypointBoxPropagationPlan).GetProperty("Removals") == null, "D-05: propagation plans must not expose a deletion operation.");
    }

    private static void EventRectanglePropagationUndoRedoIsAtomic()
    {
        var source = CreateEventBox(10, "instance-a", new System.Drawing.Rectangle(1, 2, 30, 40));
        var target = CreateEventBox(11, "instance-a", new System.Drawing.Rectangle(5, 6, 30, 40));
        var created = CreateEventBox(12, "instance-a", new System.Drawing.Rectangle(50, 60, 30, 40));
        var boxes = new List<BoundingBox> { source, target };
        var sourceBefore = source.Rectangle;
        var targetBefore = target.Rectangle;
        source.Rectangle = new System.Drawing.Rectangle(10, 20, 30, 40);
        var plan = new EventWaypointBoxPropagationPlan(
            new[] { new EventWaypointBoxUpdate(target, source.Rectangle) },
            new[] { new EventWaypointBoxAddition(created) });
        var batch = EventRectanglePropagationUndoHelper.CreateBatch(source, sourceBefore, plan);

        EventRectanglePropagationUndoHelper.ApplyForward(boxes, batch);
        AssertEqual(source.Rectangle, target.Rectangle, "Redo application must update the existing target with the source rectangle.");
        AssertTrue(boxes.Contains(created), "Redo application must add the derived box.");

        EventRectanglePropagationUndoHelper.ApplyUndo(boxes, batch);
        AssertEqual(sourceBefore, source.Rectangle, "One undo must restore the source rectangle.");
        AssertEqual(targetBefore, target.Rectangle, "One undo must restore the existing target rectangle.");
        AssertTrue(!boxes.Contains(created), "One undo must remove the derived box without a second history action.");

        EventRectanglePropagationUndoHelper.ApplyForward(boxes, batch);
        AssertEqual(new System.Drawing.Rectangle(10, 20, 30, 40), source.Rectangle, "One redo must reapply the source rectangle.");
        AssertEqual(source.Rectangle, target.Rectangle, "One redo must reapply the existing target rectangle.");
        AssertTrue(boxes.Contains(created), "One redo must restore the same derived box reference.");
    }

    private static void EventRectanglePropagationRestoresManualSourceProvenance()
    {
        var source = CreateEventBox(10, "instance-a", new System.Drawing.Rectangle(1, 2, 30, 40));
        var sourceBefore = source.Rectangle;
        source.Rectangle = new System.Drawing.Rectangle(10, 20, 30, 40);
        var manuallyAdjustedFrames = new Dictionary<string, List<int>>
        {
            [TrackingIdentityHelper.GetIdentityKey(source)] = new List<int> { source.FrameIndex }
        };
        var batch = EventRectanglePropagationUndoHelper.CreateBatch(
            source,
            sourceBefore,
            new EventWaypointBoxPropagationPlan(
                Array.Empty<EventWaypointBoxUpdate>(),
                Array.Empty<EventWaypointBoxAddition>()),
            TrackingIdentityHelper.GetIdentityKey(source),
            source.FrameIndex,
            wasManuallyAdjustedBeforeEdit: false);
        var boxes = new List<BoundingBox> { source };

        EventRectanglePropagationUndoHelper.ApplyUndo(boxes, batch, manuallyAdjustedFrames);
        AssertEqual(sourceBefore, source.Rectangle, "A source-only batch must restore the edited source rectangle.");
        AssertTrue(!manuallyAdjustedFrames.ContainsKey(TrackingIdentityHelper.GetIdentityKey(source)), "Undo must remove the source manual-frame marker created by the edit.");

        EventRectanglePropagationUndoHelper.ApplyForward(boxes, batch, manuallyAdjustedFrames);
        AssertEqual(new System.Drawing.Rectangle(10, 20, 30, 40), source.Rectangle, "Redo must reapply a source-only edit.");
        AssertTrue(manuallyAdjustedFrames.TryGetValue(TrackingIdentityHelper.GetIdentityKey(source), out var frames) && frames.Contains(source.FrameIndex), "Redo must restore the source manual-frame marker with the rectangle batch.");
    }

    private static void EventEditRequiresGeometryChange()
    {
        var original = new System.Drawing.Rectangle(1, 2, 30, 40);
        AssertTrue(!EventRectanglePropagationUndoHelper.HasGeometryChanged(original, original), "A click without movement or resize must not become an event edit.");
        AssertTrue(EventRectanglePropagationUndoHelper.HasGeometryChanged(original, new System.Drawing.Rectangle(2, 2, 30, 40)), "A changed rectangle must remain eligible for an event edit.");
    }

    private static void EventTombstoneUndoRedoRetainsOriginalBox()
    {
        var tombstone = CreateEventBox(10, "instance-a", new System.Drawing.Rectangle(1, 2, 30, 40));
        var boxes = new List<BoundingBox> { tombstone };
        tombstone.IsDeleted = true;

        EventTombstoneUndoHelper.ApplyUndo(tombstone);
        AssertTrue(ReferenceEquals(tombstone, boxes.Single()), "Undo must restore the original tombstone object rather than add a clone.");
        AssertTrue(!tombstone.IsDeleted, "Undo must reactivate the original event tombstone.");

        EventTombstoneUndoHelper.ApplyRedo(tombstone);
        AssertTrue(ReferenceEquals(tombstone, boxes.Single()), "Redo must retain the original tombstone object.");
        AssertTrue(tombstone.IsDeleted, "Redo must tombstone the original event box again.");
    }

    private static BoundingBox CreateEventBox(int frameIndex, string eventInstanceId, System.Drawing.Rectangle rectangle)
    {
        return new BoundingBox
        {
            Label = "event",
            EventId = 3,
            EventInstanceId = eventInstanceId,
            Action = "waypoint",
            FrameIndex = frameIndex,
            Rectangle = rectangle
        };
    }

    private static WaypointMarker CreateEventWaypoint(string eventInstanceId, int entryFrame, int exitFrame)
    {
        return new WaypointMarker
        {
            Label = "event",
            ObjectId = 3,
            EventInstanceId = eventInstanceId,
            EntryFrame = entryFrame,
            ExitFrame = exitFrame
        };
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}, Actual: {actual}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}















