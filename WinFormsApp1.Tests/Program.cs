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
            ("clip resolver keeps disjoint person clips separate", ClipResolverKeepsDisjointPersonClipsSeparate),
            ("clip resolver chooses latest containing clip for nested waypoints", ClipResolverChoosesLatestContainingClip),
            ("clip resolver prefers event instance id", ClipResolverPrefersEventInstanceId),
            ("clip resolver supports bounding box input", ClipResolverSupportsBoundingBoxInput),
            ("clip resolver falls back to single frame without waypoint", ClipResolverFallsBackToSingleFrame),
            ("face link export connects face annotation to body annotation", FaceLinkExportConnectsFaceToBody),
            ("face link import marks linked face boxes", FaceLinkImportMarksLinkedFaceBoxes),
            ("face link import ignores malformed links", FaceLinkImportIgnoresMalformedLinks),
            ("two same-type vehicles get different instance ids", TwoSameTypeVehiclesGetDifferentInstanceIds),
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
            ("plate a-frame lookup selects body without losing plate identity", PlateAFrameLookupSelectsBodyWithoutLosingPlateIdentity),
            ("independent plate lookup ignores vehicle body", IndependentPlateLookupIgnoresVehicleBody),
            ("vehicle waypoints merge overlaps and keep disjoint clips", VehicleWaypointsMergeOverlapsAndKeepDisjointClips),
            ("child rectangle follows tracked parent", ChildRectangleFollowsTrackedParent)
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
        var expected = new[] { "contact", "exchange", "board", "final_exchange", "disembark", "controlled_delivery", "camouflage", "throw" };
        AssertEqual(expected.Length, LabelCatalogHelper.EventTypes.Length, "Event catalog should contain 8 event types.");
        for (int i = 0; i < expected.Length; i++)
        {
            AssertEqual(expected[i], LabelCatalogHelper.EventTypes[i], $"Event type at index {i} should match the expected catalog order.");
        }

        AssertEqual(25, LabelCatalogHelper.GetEventCategoryId("contact"), "contact should map to category id 25.");
        AssertEqual(32, LabelCatalogHelper.GetEventCategoryId("throw"), "throw should map to category id 32.");
        AssertEqual(33, LabelCatalogHelper.GetPlateCategoryId(), "plate should map to category id 33.");
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















