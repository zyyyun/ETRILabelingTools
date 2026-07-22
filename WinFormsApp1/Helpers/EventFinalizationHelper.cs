using System;
using System.Collections.Generic;
using System.Linq;

namespace WinFormsApp1
{
    public static class EventFinalizationHelper
    {
        public static bool MatchesEventInstance(BoundingBox box, WaypointMarker waypoint)
        {
            if (box == null || waypoint == null)
            {
                return false;
            }

            if (!string.Equals(box.Label, "event", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(waypoint.Label, "event", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(box.EventInstanceId) &&
                !string.IsNullOrWhiteSpace(waypoint.EventInstanceId))
            {
                return string.Equals(box.EventInstanceId, waypoint.EventInstanceId, StringComparison.Ordinal);
            }

            return box.EventId == waypoint.ObjectId;
        }

        public static string FindPendingEventInstanceId(
            IEnumerable<BoundingBox> existingBoxes,
            BoundingBox candidateBox,
            int entryFrame,
            int currentFrame)
        {
            if (candidateBox == null ||
                !string.Equals(candidateBox.Label, "event", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return existingBoxes?
                .Where(box =>
                    box != null &&
                    !box.IsDeleted &&
                    string.Equals(box.Label, "event", StringComparison.OrdinalIgnoreCase) &&
                    box.EventId == candidateBox.EventId &&
                    box.FrameIndex >= entryFrame &&
                    box.FrameIndex <= currentFrame &&
                    !string.IsNullOrWhiteSpace(box.EventInstanceId))
                .OrderBy(box => box.FrameIndex)
                .Select(box => box.EventInstanceId)
                .FirstOrDefault();
        }

        public static void NormalizePendingEventInstanceIds(
            IList<BoundingBox> eventBoxesInRange,
            int entryFrame,
            int exitFrame)
        {
            if (eventBoxesInRange == null || eventBoxesInRange.Count == 0)
            {
                return;
            }

            var groups = eventBoxesInRange
                .Where(box =>
                    box != null &&
                    !box.IsDeleted &&
                    string.Equals(box.Label, "event", StringComparison.OrdinalIgnoreCase) &&
                    box.FrameIndex >= entryFrame &&
                    box.FrameIndex <= exitFrame)
                .GroupBy(box => box.EventId);

            foreach (var group in groups)
            {
                var canonicalId = group
                    .Where(box => !string.IsNullOrWhiteSpace(box.EventInstanceId))
                    .OrderBy(box => box.FrameIndex)
                    .Select(box => box.EventInstanceId)
                    .FirstOrDefault();

                if (string.IsNullOrWhiteSpace(canonicalId))
                {
                    canonicalId = group
                        .OrderBy(box => box.FrameIndex)
                        .Select(box => box.EventInstanceId = $"event-{Guid.NewGuid():N}")
                        .FirstOrDefault();
                }

                foreach (var box in group)
                {
                    box.EventInstanceId = canonicalId;
                }
            }
        }

        public static List<BoundingBox> GetEventBoxesForWaypoint(
            IEnumerable<BoundingBox> boxes,
            WaypointMarker waypoint,
            int? startFrame = null,
            bool overflowOnly = false)
        {
            if (boxes == null || waypoint == null || !string.Equals(waypoint.Label, "event", StringComparison.OrdinalIgnoreCase))
            {
                return new List<BoundingBox>();
            }

            int lowerBound = startFrame ?? waypoint.EntryFrame;

            return boxes
                .Where(box =>
                    box != null &&
                    !box.IsDeleted &&
                    string.Equals(box.Label, "event", StringComparison.OrdinalIgnoreCase) &&
                    MatchesEventInstance(box, waypoint) &&
                    box.FrameIndex >= lowerBound &&
                    (overflowOnly
                        ? box.FrameIndex > waypoint.ExitFrame
                        : box.FrameIndex <= waypoint.ExitFrame))
                .OrderBy(box => box.FrameIndex)
                .ToList();
        }

        public static int RemoveEventBoxesForWaypoint(
            IList<BoundingBox> boxes,
            WaypointMarker waypoint,
            int startFrame,
            bool overflowOnly = false)
        {
            if (boxes == null || waypoint == null)
            {
                return 0;
            }

            var boxesToRemove = GetEventBoxesForWaypoint(boxes, waypoint, startFrame, overflowOnly);
            if (boxesToRemove.Count == 0)
            {
                return 0;
            }

            foreach (var box in boxesToRemove)
            {
                boxes.Remove(box);
            }

            return boxesToRemove.Count;
        }

        public static int ClampEventBoxesToWaypointExit(
            IList<BoundingBox> boxes,
            WaypointMarker waypoint)
        {
            if (boxes == null || waypoint == null)
            {
                return 0;
            }

            return RemoveEventBoxesForWaypoint(
                boxes,
                waypoint,
                waypoint.ExitFrame + 1,
                overflowOnly: true);
        }

        public static int ClampAllEventBoxesToWaypoints(
            IList<BoundingBox> boxes,
            IEnumerable<WaypointMarker> waypoints)
        {
            if (boxes == null || waypoints == null)
            {
                return 0;
            }

            int removed = 0;
            foreach (var waypoint in waypoints
                         .Where(waypoint => string.Equals(waypoint.Label, "event", StringComparison.OrdinalIgnoreCase))
                         .ToList())
            {
                removed += ClampEventBoxesToWaypointExit(boxes, waypoint);
            }

            return removed;
        }
    }
}
