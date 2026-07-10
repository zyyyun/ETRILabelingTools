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
                        ("timeline width stops before object info", TimelineWidthStopsBeforeObjectInfo),
            ("clip resolver keeps disjoint person clips separate", ClipResolverKeepsDisjointPersonClipsSeparate),
            ("clip resolver chooses latest containing clip for nested waypoints", ClipResolverChoosesLatestContainingClip),
            ("clip resolver prefers event instance id", ClipResolverPrefersEventInstanceId),
            ("clip resolver supports bounding box input", ClipResolverSupportsBoundingBoxInput),
            ("clip resolver falls back to single frame without waypoint", ClipResolverFallsBackToSingleFrame)
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





