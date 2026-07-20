using System;
using System.Collections.Generic;
using System.Linq;

namespace WinFormsApp1
{
    public static class WaypointGroupingHelper
    {
        public static List<BoundingBox> GetVehicleWaypointBodies(
            IEnumerable<BoundingBox> boxes,
            int frameIndex)
        {
            if (boxes == null)
            {
                return new List<BoundingBox>();
            }

            return boxes
                .Where(box =>
                    box != null &&
                    !box.IsDeleted &&
                    string.Equals(box.Label, "vehicle", StringComparison.OrdinalIgnoreCase) &&
                    box.FrameIndex == frameIndex &&
                    !string.Equals(box.VehiclePartType, "plate", StringComparison.OrdinalIgnoreCase))
                .GroupBy(TrackingIdentityHelper.GetNumericIdentity)
                .Select(group => group.First())
                .OrderBy(TrackingIdentityHelper.GetNumericIdentity)
                .ToList();
        }
    }
}