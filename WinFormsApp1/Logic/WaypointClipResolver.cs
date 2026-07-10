using System;
using System.Collections.Generic;
using System.Linq;

namespace WinFormsApp1
{
    public readonly record struct WaypointClipRange(int EntryFrame, int ExitFrame, WaypointMarker? Waypoint);

    public static class WaypointClipResolver
    {
        public static WaypointClipRange ResolveClipRange(BoundingBox box, IEnumerable<WaypointMarker> waypoints)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            int objectId = GetObjectId(box);
            return ResolveClipRange(box.Label, objectId, box.FrameIndex, waypoints, box.EventInstanceId);
        }

        public static WaypointClipRange ResolveClipRange(
            string label,
            int objectId,
            int frameIndex,
            IEnumerable<WaypointMarker> waypoints,
            string? eventInstanceId = null)
        {
            if (waypoints == null)
            {
                return new WaypointClipRange(frameIndex, frameIndex, null);
            }

            IEnumerable<WaypointMarker> candidates = waypoints.Where(waypoint =>
                string.Equals(waypoint.Label, label, StringComparison.OrdinalIgnoreCase) &&
                frameIndex >= waypoint.EntryFrame &&
                frameIndex <= waypoint.ExitFrame);

            if (string.Equals(label, "event", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(eventInstanceId))
            {
                candidates = candidates.Where(waypoint => string.Equals(waypoint.EventInstanceId, eventInstanceId, StringComparison.Ordinal));
            }
            else
            {
                candidates = candidates.Where(waypoint => waypoint.ObjectId == objectId);
            }

            var match = candidates
                .OrderByDescending(waypoint => waypoint.EntryFrame)
                .FirstOrDefault();

            return match == null
                ? new WaypointClipRange(frameIndex, frameIndex, null)
                : new WaypointClipRange(match.EntryFrame, match.ExitFrame, match);
        }

        private static int GetObjectId(BoundingBox box)
        {
            if (string.Equals(box.Label, "person", StringComparison.OrdinalIgnoreCase))
            {
                return box.PersonId;
            }

            if (string.Equals(box.Label, "vehicle", StringComparison.OrdinalIgnoreCase))
            {
                return box.VehicleId;
            }

            if (string.Equals(box.Label, "event", StringComparison.OrdinalIgnoreCase))
            {
                return box.EventId;
            }

            return 0;
        }
    }
}
