using System;
using System.Collections.Generic;
using System.Linq;

namespace WinFormsApp1
{
    public static class FaceLinkHelper
    {
        public static FaceLinkData? TryCreateFaceLink(
            BoundingBox faceBox,
            IEnumerable<AnnotationData> frameAnnotations,
            IReadOnlyDictionary<BoundingBox, AnnotationData> annotationsByBox)
        {
            if (faceBox == null || frameAnnotations == null || annotationsByBox == null)
            {
                return null;
            }

            if (!string.Equals(faceBox.Label, "person", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(faceBox.PersonPartType, "face", StringComparison.OrdinalIgnoreCase) ||
                !faceBox.LinkedPersonId.HasValue)
            {
                return null;
            }

            if (!annotationsByBox.TryGetValue(faceBox, out var faceAnnotation))
            {
                return null;
            }

            var bodyPair = annotationsByBox.FirstOrDefault(kvp =>
                !ReferenceEquals(kvp.Key, faceBox) &&
                string.Equals(kvp.Key.Label, "person", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(kvp.Key.PersonPartType, "face", StringComparison.OrdinalIgnoreCase) &&
                kvp.Key.FrameIndex == faceBox.FrameIndex &&
                kvp.Value.TrackId == faceBox.LinkedPersonId.Value);

            if (bodyPair.Key == null)
            {
                return null;
            }

            return new FaceLinkData
            {
                FaceAnnotationId = faceAnnotation.Id,
                BodyAnnotationId = bodyPair.Value.Id
            };
        }

        public static void ApplyFaceLinks(
            IEnumerable<FaceLinkData>? faceLinks,
            IDictionary<int, BoundingBox> boxesByAnnotationId,
            IDictionary<int, AnnotationData> annotationsById)
        {
            if (faceLinks == null)
            {
                return;
            }

            foreach (var faceLink in faceLinks)
            {
                if (faceLink == null ||
                    !boxesByAnnotationId.TryGetValue(faceLink.FaceAnnotationId, out var faceBox) ||
                    !boxesByAnnotationId.TryGetValue(faceLink.BodyAnnotationId, out var bodyBox) ||
                    !annotationsById.TryGetValue(faceLink.FaceAnnotationId, out var faceAnnotation) ||
                    !annotationsById.TryGetValue(faceLink.BodyAnnotationId, out var bodyAnnotation))
                {
                    continue;
                }

                if (!string.Equals(faceBox.Label, "person", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(bodyBox.Label, "person", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(bodyBox.PersonPartType, "face", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bodyBox.PersonPartType ??= "body";
                faceBox.PersonPartType = "face";
                faceBox.LinkedPersonId = bodyBox.PersonId;
                faceBox.PersonId = bodyBox.PersonId;
                faceBox.BoxEntryFrame = faceAnnotation.TrackInfo?.Entry?.Frame ?? faceBox.FrameIndex;
                faceBox.BoxExitFrame = faceAnnotation.TrackInfo?.Exit?.Frame ?? faceBox.FrameIndex;

                bodyBox.BoxEntryFrame ??= bodyAnnotation.TrackInfo?.Entry?.Frame ?? bodyBox.FrameIndex;
                bodyBox.BoxExitFrame ??= bodyAnnotation.TrackInfo?.Exit?.Frame ?? bodyBox.FrameIndex;
            }
        }
    }
}
