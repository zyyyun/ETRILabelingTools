using System;
using System.Collections.Generic;
using System.Linq;

namespace WinFormsApp1
{
    public static class VehicleInstanceAssignmentHelper
    {
        public static int ResolveNewBodyInstanceId(
            IEnumerable<BoundingBox> boxes,
            BoundingBox selectedBox,
            int currentFrame,
            int? entryFrame,
            int vehicleTypeId,
            int fallbackInstanceId)
        {
            if (IsMatchingBody(selectedBox, vehicleTypeId) &&
                (selectedBox.FrameIndex == currentFrame ||
                 (entryFrame.HasValue && selectedBox.FrameIndex == entryFrame.Value)))
            {
                return TrackingIdentityHelper.GetNumericIdentity(selectedBox);
            }

            if (!entryFrame.HasValue || boxes == null)
            {
                return fallbackInstanceId;
            }

            var entryInstances = boxes
                .Where(box =>
                    IsMatchingBody(box, vehicleTypeId) &&
                    !box.IsDeleted &&
                    box.FrameIndex == entryFrame.Value)
                .Select(TrackingIdentityHelper.GetNumericIdentity)
                .Where(instanceId => instanceId > 0)
                .Distinct()
                .ToList();

            return entryInstances.Count == 1 ? entryInstances[0] : fallbackInstanceId;
        }

        private static bool IsMatchingBody(BoundingBox box, int vehicleTypeId)
        {
            return box != null &&
                string.Equals(box.Label, "vehicle", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(box.VehiclePartType, "plate", StringComparison.OrdinalIgnoreCase) &&
                box.VehicleId == vehicleTypeId;
        }
    }
}
