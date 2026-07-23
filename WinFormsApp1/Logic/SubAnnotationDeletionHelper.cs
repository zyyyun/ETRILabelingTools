using System;
using System.Collections.Generic;
using System.Linq;

namespace WinFormsApp1
{
    public static class SubAnnotationDeletionHelper
    {
        public static bool IsSubAnnotation(BoundingBox box)
        {
            return TrackingIdentityHelper.IsFace(box) || TrackingIdentityHelper.IsPlate(box);
        }

        public static IReadOnlyList<BoundingBox> GetWaypointScopedSubAnnotations(
            IEnumerable<BoundingBox> boxes,
            IEnumerable<WaypointMarker> waypoints,
            BoundingBox selectedBox)
        {
            if (boxes == null || waypoints == null || !IsSubAnnotation(selectedBox))
            {
                return Array.Empty<BoundingBox>();
            }

            var containingWaypoints = waypoints
                .Where(waypoint =>
                    waypoint != null &&
                    TrackingIdentityHelper.MatchesWaypoint(selectedBox, waypoint) &&
                    selectedBox.FrameIndex >= waypoint.EntryFrame &&
                    selectedBox.FrameIndex <= waypoint.ExitFrame)
                .ToList();

            if (containingWaypoints.Count != 1)
            {
                return Array.Empty<BoundingBox>();
            }

            var parentWaypoint = containingWaypoints[0];
            bool isFace = TrackingIdentityHelper.IsFace(selectedBox);

            return boxes
                .Where(box =>
                    box != null &&
                    !box.IsDeleted &&
                    (isFace ? TrackingIdentityHelper.IsFace(box) : TrackingIdentityHelper.IsPlate(box)) &&
                    TrackingIdentityHelper.MatchesWaypoint(box, parentWaypoint) &&
                    box.FrameIndex >= parentWaypoint.EntryFrame &&
                    box.FrameIndex <= parentWaypoint.ExitFrame)
                .ToList();
        }
    }
}
