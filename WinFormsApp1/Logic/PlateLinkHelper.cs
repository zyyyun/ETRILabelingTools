using System;
using System.Collections.Generic;
using System.Linq;

namespace WinFormsApp1
{
    public static class PlateLinkHelper
    {
        public static PlateLinkData? TryCreatePlateLink(
            BoundingBox plateBox,
            IEnumerable<AnnotationData> frameAnnotations,
            IReadOnlyDictionary<BoundingBox, AnnotationData> annotationsByBox)
        {
            if (plateBox == null || frameAnnotations == null || annotationsByBox == null)
            {
                return null;
            }

            if (!string.Equals(plateBox.Label, "vehicle", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(plateBox.VehiclePartType, "plate", StringComparison.OrdinalIgnoreCase) ||
                !plateBox.LinkedVehicleInstanceId.HasValue)
            {
                return null;
            }

            if (!annotationsByBox.TryGetValue(plateBox, out var plateAnnotation))
            {
                return null;
            }

            var bodyPair = annotationsByBox.FirstOrDefault(kvp =>
                !ReferenceEquals(kvp.Key, plateBox) &&
                string.Equals(kvp.Key.Label, "vehicle", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(kvp.Key.VehiclePartType, "plate", StringComparison.OrdinalIgnoreCase) &&
                kvp.Key.FrameIndex == plateBox.FrameIndex &&
                kvp.Key.VehicleInstanceId == plateBox.LinkedVehicleInstanceId.Value);

            if (bodyPair.Key == null)
            {
                return null;
            }

            return new PlateLinkData
            {
                PlateAnnotationId = plateAnnotation.Id,
                BodyAnnotationId = bodyPair.Value.Id
            };
        }

        public static void ApplyPlateLinks(
            IEnumerable<PlateLinkData>? plateLinks,
            IDictionary<int, BoundingBox> boxesByAnnotationId,
            IDictionary<int, AnnotationData> annotationsById)
        {
            if (plateLinks == null)
            {
                return;
            }

            foreach (var plateLink in plateLinks)
            {
                if (plateLink == null ||
                    !boxesByAnnotationId.TryGetValue(plateLink.PlateAnnotationId, out var plateBox) ||
                    !boxesByAnnotationId.TryGetValue(plateLink.BodyAnnotationId, out var bodyBox) ||
                    !annotationsById.TryGetValue(plateLink.PlateAnnotationId, out var plateAnnotation) ||
                    !annotationsById.TryGetValue(plateLink.BodyAnnotationId, out var bodyAnnotation))
                {
                    continue;
                }

                if (!string.Equals(plateBox.Label, "vehicle", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(bodyBox.Label, "vehicle", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(bodyBox.VehiclePartType, "plate", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bodyBox.VehiclePartType ??= "body";
                plateBox.VehiclePartType = "plate";
                plateBox.LinkedVehicleInstanceId = bodyBox.VehicleInstanceId;
                plateBox.VehicleInstanceId = bodyBox.VehicleInstanceId;
                plateBox.VehicleId = bodyBox.VehicleId;
                plateBox.BoxEntryFrame = plateAnnotation.TrackInfo?.Entry?.Frame ?? plateBox.FrameIndex;
                plateBox.BoxExitFrame = plateAnnotation.TrackInfo?.Exit?.Frame ?? plateBox.FrameIndex;

                bodyBox.BoxEntryFrame ??= bodyAnnotation.TrackInfo?.Entry?.Frame ?? bodyBox.FrameIndex;
                bodyBox.BoxExitFrame ??= bodyAnnotation.TrackInfo?.Exit?.Frame ?? bodyBox.FrameIndex;
            }
        }
    }
}
