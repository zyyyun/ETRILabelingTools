namespace WinFormsApp1
{
    public static class TrackingResultMergeHelper
    {
        public static List<BoundingBox> ExcludeExistingVehicleExitBoxes(
            IEnumerable<BoundingBox> trackedBoxes,
            IEnumerable<BoundingBox> existingBoxes,
            WaypointMarker waypoint)
        {
            if (trackedBoxes == null || waypoint == null ||
                !string.Equals(waypoint.Label, "vehicle", StringComparison.OrdinalIgnoreCase))
            {
                return trackedBoxes?.ToList() ?? new List<BoundingBox>();
            }

            var existingExitBoxes = (existingBoxes ?? Enumerable.Empty<BoundingBox>())
                .Where(box =>
                    box != null &&
                    !box.IsDeleted &&
                    box.FrameIndex == waypoint.ExitFrame &&
                    string.Equals(box.Label, "vehicle", StringComparison.OrdinalIgnoreCase) &&
                    !TrackingIdentityHelper.IsPlate(box))
                .ToList();

            return trackedBoxes
                .Where(box =>
                    box == null ||
                    box.FrameIndex != waypoint.ExitFrame ||
                    TrackingIdentityHelper.IsPlate(box) ||
                    !existingExitBoxes.Any(existing => TrackingIdentityHelper.AreSameTrackingTarget(box, existing)))
                .ToList();
        }
    }
}
