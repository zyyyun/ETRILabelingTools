using System;

namespace WinFormsApp1
{
    public static class TrackingIdentityHelper
    {
        public static bool IsFace(BoundingBox box) => box != null && string.Equals(box.Label, "person", StringComparison.OrdinalIgnoreCase) && string.Equals(box.PersonPartType, "face", StringComparison.OrdinalIgnoreCase);
        public static bool IsPlate(BoundingBox box) => box != null && string.Equals(box.Label, "vehicle", StringComparison.OrdinalIgnoreCase) && string.Equals(box.VehiclePartType, "plate", StringComparison.OrdinalIgnoreCase);

        public static int GetNumericIdentity(BoundingBox box)
        {
            if (box == null) return 0;
            if (string.Equals(box.Label, "person", StringComparison.OrdinalIgnoreCase))
                return IsFace(box) ? box.LinkedPersonId.GetValueOrDefault(box.PersonId) : box.PersonId;
            if (string.Equals(box.Label, "vehicle", StringComparison.OrdinalIgnoreCase))
            {
                if (IsPlate(box)) return box.LinkedVehicleInstanceId.GetValueOrDefault(box.VehicleInstanceId);
                return box.VehicleInstanceId > 0 ? box.VehicleInstanceId : box.VehicleId;
            }
            return string.Equals(box.Label, "event", StringComparison.OrdinalIgnoreCase) ? box.EventId : 0;
        }

        public static bool AreSameTrackingTarget(BoundingBox first, BoundingBox second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            return string.Equals(first.Label, second.Label, StringComparison.OrdinalIgnoreCase) &&
                GetNumericIdentity(first) == GetNumericIdentity(second);
        }
        public static BoundingBox FindVehicleBodyAtFrame(
            IEnumerable<BoundingBox> boxes,
            int frameIndex,
            BoundingBox referenceBox)
        {
            if (boxes == null || referenceBox == null)
            {
                return null;
            }

            return boxes.FirstOrDefault(box =>
                box != null &&
                !box.IsDeleted &&
                box.FrameIndex == frameIndex &&
                string.Equals(box.Label, "vehicle", StringComparison.OrdinalIgnoreCase) &&
                !IsPlate(box) &&
                AreSameTrackingTarget(referenceBox, box));
        }
        public static BoundingBox FindPlateAtFrame(
            IEnumerable<BoundingBox> boxes,
            int frameIndex,
            BoundingBox referenceBox)
        {
            if (boxes == null || referenceBox == null)
            {
                return null;
            }

            int identity = GetNumericIdentity(referenceBox);
            return boxes.FirstOrDefault(box =>
                box != null &&
                !box.IsDeleted &&
                box.FrameIndex == frameIndex &&
                IsPlate(box) &&
                GetNumericIdentity(box) == identity);
        }
        public static string GetIdentityKey(BoundingBox box)
        {
            if (box == null) return "unknown_unknown";
            if (IsFace(box)) return $"person_face_{GetNumericIdentity(box)}";
            if (string.Equals(box.Label, "person", StringComparison.OrdinalIgnoreCase)) return $"person_body_{GetNumericIdentity(box)}";
            if (IsPlate(box)) return $"vehicle_plate_{GetNumericIdentity(box)}";
            if (string.Equals(box.Label, "event", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(box.EventInstanceId)) return $"event_instance_{box.EventInstanceId}";
            return $"{box.Label}_{GetNumericIdentity(box)}";
        }

        // A selected sub-box must not hide its parent body during painting.
        public static bool ShouldSkipForSelection(BoundingBox box, BoundingBox selectedBox)
        {
            return ReferenceEquals(box, selectedBox);
        }

        public static bool MatchesWaypoint(BoundingBox box, WaypointMarker waypoint)
        {
            if (box == null || waypoint == null || !string.Equals(box.Label, waypoint.Label, StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(box.Label, "event", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(box.EventInstanceId) && !string.IsNullOrWhiteSpace(waypoint.EventInstanceId))
                return string.Equals(box.EventInstanceId, waypoint.EventInstanceId, StringComparison.Ordinal);
            return GetNumericIdentity(box) == waypoint.ObjectId;
        }
    }
}