using System;
using System.Collections.Generic;
using System.Linq;

namespace WinFormsApp1
{
    public static class EventWaypointUpdateHelper
    {
        public static List<BoundingBox> ResolveActiveBoxes(
            BoundingBox selectedBox,
            IEnumerable<BoundingBox> boxes,
            IEnumerable<WaypointMarker> waypoints)
        {
            if (selectedBox == null ||
                !string.Equals(selectedBox.Label, "event", StringComparison.OrdinalIgnoreCase) ||
                boxes == null ||
                waypoints == null)
            {
                return new List<BoundingBox>();
            }

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
                    return new List<BoundingBox>();
                }

                return GetActiveEventBoxes(boxes, instanceWaypoints[0], box =>
                    string.Equals(box.EventInstanceId, selectedBox.EventInstanceId, StringComparison.Ordinal));
            }

            var legacyWaypoints = eventWaypoints
                .Where(waypoint => string.IsNullOrWhiteSpace(waypoint.EventInstanceId) &&
                    waypoint.ObjectId == selectedBox.EventId)
                .ToList();

            if (legacyWaypoints.Count != 1)
            {
                return new List<BoundingBox>();
            }

            return GetActiveEventBoxes(boxes, legacyWaypoints[0], box =>
                string.IsNullOrWhiteSpace(box.EventInstanceId) &&
                box.EventId == selectedBox.EventId);
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
