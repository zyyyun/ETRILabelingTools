using System;
using System.Collections.Generic;
using System.Linq;

namespace WinFormsApp1
{
    public class EventIdChange
    {
        public BoundingBox Box { get; set; } = null!;
        public int OriginalEventId { get; set; }
        public int NewEventId { get; set; }
    }

    public class EventWaypointScope
    {
        public WaypointMarker Waypoint { get; set; } = null!;
        public List<BoundingBox> Boxes { get; set; } = new List<BoundingBox>();
    }

    public class EventWaypointMarkerChange
    {
        public WaypointMarker Waypoint { get; set; } = null!;
        public int OriginalObjectId { get; set; }
        public int NewObjectId { get; set; }
        public string OriginalEventInstanceId { get; set; } = string.Empty;
        public string NewEventInstanceId { get; set; } = string.Empty;
    }

    public static class EventWaypointUpdateHelper
    {
        public static List<EventIdChange> CreateEventIdChanges(
            IEnumerable<BoundingBox> boxes,
            int newEventId)
        {
            if (boxes == null || newEventId <= 0)
            {
                return new List<EventIdChange>();
            }

            return boxes
                .Where(box => box != null &&
                    !box.IsDeleted &&
                    string.Equals(box.Label, "event", StringComparison.OrdinalIgnoreCase) &&
                    box.EventId != newEventId)
                .Select(box => new EventIdChange
                {
                    Box = box,
                    OriginalEventId = box.EventId,
                    NewEventId = newEventId
                })
                .ToList();
        }

        public static List<BoundingBox> ResolveActiveBoxes(
            BoundingBox selectedBox,
            IEnumerable<BoundingBox> boxes,
            IEnumerable<WaypointMarker> waypoints)
        {
            return ResolveActiveScope(selectedBox, boxes, waypoints)?.Boxes ?? new List<BoundingBox>();
        }

        public static EventWaypointScope? ResolveActiveScope(
            BoundingBox selectedBox,
            IEnumerable<BoundingBox> boxes,
            IEnumerable<WaypointMarker> waypoints)
        {
            if (selectedBox == null ||
                !string.Equals(selectedBox.Label, "event", StringComparison.OrdinalIgnoreCase) ||
                boxes == null || waypoints == null)
                return null;

            var eventWaypoints = waypoints
                .Where(waypoint => waypoint != null &&
                    string.Equals(waypoint.Label, "event", StringComparison.OrdinalIgnoreCase) &&
                    selectedBox.FrameIndex >= waypoint.EntryFrame &&
                    selectedBox.FrameIndex <= waypoint.ExitFrame)
                .ToList();

            if (!string.IsNullOrWhiteSpace(selectedBox.EventInstanceId))
            {
                var instanceWaypoints = eventWaypoints
                    .Where(waypoint => string.Equals(
                        waypoint.EventInstanceId,
                        selectedBox.EventInstanceId,
                        StringComparison.Ordinal))
                    .ToList();

                if (instanceWaypoints.Count != 1)
                {
                    var mixedWaypoints = eventWaypoints
                        .Where(waypoint => string.IsNullOrWhiteSpace(waypoint.EventInstanceId) &&
                            waypoint.ObjectId == selectedBox.EventId)
                        .ToList();

                    if (mixedWaypoints.Count != 1)
                        return null;

                    return CreateScope(boxes, mixedWaypoints[0], box =>
                        string.Equals(box.EventInstanceId, selectedBox.EventInstanceId, StringComparison.Ordinal));
                }

                return CreateScope(boxes, instanceWaypoints[0], box =>
                    string.Equals(box.EventInstanceId, selectedBox.EventInstanceId, StringComparison.Ordinal));
            }

            var legacyWaypoints = eventWaypoints
                .Where(waypoint => string.IsNullOrWhiteSpace(waypoint.EventInstanceId) &&
                    waypoint.ObjectId == selectedBox.EventId)
                .ToList();

            if (legacyWaypoints.Count != 1)
            {
                return null;
            }

            return CreateScope(boxes, legacyWaypoints[0], box =>
                string.IsNullOrWhiteSpace(box.EventInstanceId) &&
                box.EventId == selectedBox.EventId);
        }

        public static BoundingBox? FindDisplayBox(
            IEnumerable<BoundingBox> boxes,
            WaypointMarker waypoint)
        {
            if (boxes == null || waypoint == null)
                return null;

            return boxes.FirstOrDefault(box => box != null &&
                !box.IsDeleted &&
                string.Equals(box.Label, "event", StringComparison.OrdinalIgnoreCase) &&
                box.FrameIndex >= waypoint.EntryFrame &&
                box.FrameIndex <= waypoint.ExitFrame &&
                TrackingIdentityHelper.MatchesWaypoint(box, waypoint));
        }

        private static EventWaypointScope CreateScope(
            IEnumerable<BoundingBox> boxes,
            WaypointMarker waypoint,
            Func<BoundingBox, bool> matchesWaypoint)
        {
            return new EventWaypointScope
            {
                Waypoint = waypoint,
                Boxes = GetActiveEventBoxes(boxes, waypoint, matchesWaypoint)
            };
        }

        private static List<BoundingBox> GetActiveEventBoxes(
            IEnumerable<BoundingBox> boxes,
            WaypointMarker waypoint,
            Func<BoundingBox, bool> matchesWaypoint)
        {
            return boxes
                .Where(box => box != null &&
                    !box.IsDeleted &&
                    string.Equals(box.Label, "event", StringComparison.OrdinalIgnoreCase) &&
                    box.FrameIndex >= waypoint.EntryFrame &&
                    box.FrameIndex <= waypoint.ExitFrame &&
                    matchesWaypoint(box))
                .ToList();
        }
    }
}
