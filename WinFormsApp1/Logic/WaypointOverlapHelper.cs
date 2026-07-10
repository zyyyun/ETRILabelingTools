using System;
using System.Collections.Generic;
using System.Linq;

namespace WinFormsApp1
{
    public static class WaypointOverlapHelper
    {
        public static bool RangesOverlap(int requestedEntryFrame, int requestedExitFrame, int existingEntryFrame, int existingExitFrame)
        {
            return requestedEntryFrame <= existingExitFrame && requestedExitFrame >= existingEntryFrame;
        }

        public static WaypointMarker? FindOverlappingWaypoint(
            IEnumerable<WaypointMarker> waypoints,
            string label,
            int objectId,
            int requestedEntryFrame,
            int requestedExitFrame,
            string? eventInstanceId = null)
        {
            if (waypoints == null)
            {
                return null;
            }

            return waypoints.FirstOrDefault(waypoint =>
                string.Equals(waypoint.Label, label, StringComparison.OrdinalIgnoreCase) &&
                waypoint.ObjectId == objectId &&
                EventInstanceMatches(waypoint, label, eventInstanceId) &&
                RangesOverlap(requestedEntryFrame, requestedExitFrame, waypoint.EntryFrame, waypoint.ExitFrame));
        }

        private static bool EventInstanceMatches(WaypointMarker waypoint, string label, string? eventInstanceId)
        {
            if (!string.Equals(label, "event", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(eventInstanceId))
            {
                return string.IsNullOrWhiteSpace(waypoint.EventInstanceId);
            }

            return string.Equals(waypoint.EventInstanceId, eventInstanceId, StringComparison.Ordinal);
        }
    }
}

