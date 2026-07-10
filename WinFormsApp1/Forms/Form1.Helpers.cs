using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;

namespace WinFormsApp1
{
    public partial class Form1
    {
        #region Helper Methods
        private void AppendFaceDebugLog(string source, string message)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "faceid-debug.log");
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}] {message}{Environment.NewLine}";
                File.AppendAllText(logPath, line);
                Debug.WriteLine(line);
            }
            catch
            {
                // Logging must never break the app flow.
            }
        }

        private BoundingBox CloneBoundingBox(BoundingBox box)
        {
            return new BoundingBox
            {
                FrameIndex = box.FrameIndex,
                Rectangle = new Rectangle(box.Rectangle.Location, box.Rectangle.Size),
                Label = box.Label,
                PersonId = box.PersonId,
                VehicleId = box.VehicleId,
                EventId = box.EventId,
                EventInstanceId = box.EventInstanceId,
                Action = box.Action,
                VehicleName = box.VehicleName,
                EventName = box.EventName,
                PersonPartType = box.PersonPartType,
                LinkedPersonId = box.LinkedPersonId,
                BoxEntryFrame = box.BoxEntryFrame,
                BoxExitFrame = box.BoxExitFrame
            };
        }

        private WaypointMarker FindActivePersonWaypointForCurrentFrame(int frameIndex)
        {
            var activeWaypoints = waypointMarkers
                .Where(w => w.Label == "person" && frameIndex >= w.EntryFrame && frameIndex <= w.ExitFrame)
                .ToList();

            if (activeWaypoints.Count == 0)
            {
                return null;
            }

            if (selectedBox != null &&
                selectedBox.Label == "person" &&
                IsBodySubTypeCandidate(selectedBox))
            {
                var selectedBoxWaypoint = activeWaypoints.FirstOrDefault(w => w.ObjectId == selectedBox.PersonId);
                if (selectedBoxWaypoint != null)
                {
                    return selectedBoxWaypoint;
                }
            }

            if (selectedWaypoint != null &&
                selectedWaypoint.Label == "person" &&
                frameIndex >= selectedWaypoint.EntryFrame &&
                frameIndex <= selectedWaypoint.ExitFrame)
            {
                return selectedWaypoint;
            }

            foreach (var waypoint in activeWaypoints.OrderBy(w => w.EntryFrame))
            {
                bool hasBodyAtFrame = boundingBoxes.Any(b =>
                    !b.IsDeleted &&
                    b.Label == "person" &&
                    b.FrameIndex == frameIndex &&
                    b.PersonId == waypoint.ObjectId &&
                    IsBodySubTypeCandidate(b));

                if (hasBodyAtFrame)
                {
                    return waypoint;
                }
            }

            return activeWaypoints.OrderBy(w => w.EntryFrame).FirstOrDefault();
        }

        private BoundingBox FindActivePersonBodyForCurrentFrame(int frameIndex)
        {
            return GetActivePersonBodyCandidates(frameIndex).FirstOrDefault();
        }

        private List<BoundingBox> GetActivePersonBodyCandidates(int frameIndex)
        {
            var activeWaypointIds = waypointMarkers
                .Where(w => w.Label == "person" && frameIndex >= w.EntryFrame && frameIndex <= w.ExitFrame)
                .Select(w => w.ObjectId)
                .Distinct()
                .ToHashSet();

            if (activeWaypointIds.Count == 0)
            {
                return new List<BoundingBox>();
            }

            var candidates = boundingBoxes
                .Where(b => !b.IsDeleted &&
                    b.Label == "person" &&
                    b.FrameIndex == frameIndex &&
                    activeWaypointIds.Contains(b.PersonId) &&
                    IsBodySubTypeCandidate(b))
                .OrderBy(b => selectedBox != null && b == selectedBox ? 0 : 1)
                .ThenBy(b => FindActivePersonWaypointForCurrentFrame(frameIndex)?.ObjectId == b.PersonId ? 0 : 1)
                .ThenBy(b => b.PersonId)
                .ToList();

            return candidates;
        }

        private bool IsBodySubTypeCandidate(BoundingBox box)
        {
            return string.IsNullOrWhiteSpace(box.PersonPartType) ||
                string.Equals(box.PersonPartType, "body", StringComparison.OrdinalIgnoreCase);
        }

        private int GetDrawingIdentityId(BoundingBox box)
        {
            if (box == null) return 0;

            if (string.Equals(box.Label, "person", StringComparison.OrdinalIgnoreCase))
            {
                if (IsFaceBox(box))
                {
                    return box.LinkedPersonId.GetValueOrDefault(box.PersonId);
                }

                return box.PersonId;
            }

            return GetBoxId(box);
        }

        private string GetDrawingIdentityKey(BoundingBox box)
        {
            if (box == null) return "unknown_unknown";

            if (string.Equals(box.Label, "person", StringComparison.OrdinalIgnoreCase))
            {
                if (IsFaceBox(box))
                {
                    return $"person_face_{GetDrawingIdentityId(box):D2}";
                }

                return $"person_body_{GetDrawingIdentityId(box):D2}";
            }

            return $"{box.Label}_{GetDrawingIdentityId(box)}";
        }

        private bool AreDrawingIdentitiesEqual(BoundingBox first, BoundingBox second)
        {
            if (first == null || second == null)
                return false;

            return string.Equals(GetDrawingIdentityKey(first), GetDrawingIdentityKey(second), StringComparison.Ordinal);
        }

        private bool IsBoxInWaypoint(BoundingBox box, WaypointMarker waypoint)
        {
            if (box == null || waypoint == null)
                return false;

            if (!string.Equals(box.Label, waypoint.Label, StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(box.Label, "person", StringComparison.OrdinalIgnoreCase))
            {
                return GetDrawingIdentityId(box) == waypoint.ObjectId;
            }

            if (string.Equals(box.Label, "vehicle", StringComparison.OrdinalIgnoreCase))
            {
                return box.VehicleId == waypoint.ObjectId;
            }

            if (string.Equals(box.Label, "event", StringComparison.OrdinalIgnoreCase))
            {
                return box.EventId == waypoint.ObjectId;
            }

            return false;
        }

        private BoundingBox FindBestMatchingBodyForFace(BoundingBox faceBox)
        {
            if (faceBox == null)
            {
                return null;
            }

            var candidates = GetActivePersonBodyCandidates(faceBox.FrameIndex);
            if (candidates.Count == 0)
            {
                return null;
            }

            var ranked = candidates
                .Select(body => new
                {
                    Body = body,
                    OverlapArea = GetIntersectionArea(faceBox.Rectangle, body.Rectangle),
                    CenterDistance = GetCenterDistance(faceBox.Rectangle, body.Rectangle)
                })
                .OrderByDescending(x => x.OverlapArea)
                .ThenBy(x => x.CenterDistance)
                .ToList();

            var bestMatch = ranked.FirstOrDefault();
            if (bestMatch == null || bestMatch.OverlapArea <= 0)
            {
                return null;
            }

            return bestMatch.Body;
        }

        private int GetIntersectionArea(Rectangle first, Rectangle second)
        {
            int left = Math.Max(first.Left, second.Left);
            int top = Math.Max(first.Top, second.Top);
            int right = Math.Min(first.Right, second.Right);
            int bottom = Math.Min(first.Bottom, second.Bottom);

            if (right <= left || bottom <= top)
            {
                return 0;
            }

            return (right - left) * (bottom - top);
        }

        private double GetCenterDistance(Rectangle first, Rectangle second)
        {
            double firstCenterX = first.X + first.Width / 2.0;
            double firstCenterY = first.Y + first.Height / 2.0;
            double secondCenterX = second.X + second.Width / 2.0;
            double secondCenterY = second.Y + second.Height / 2.0;

            double dx = firstCenterX - secondCenterX;
            double dy = firstCenterY - secondCenterY;
            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        /// <summary>
        /// COCO 17개 관절 연결 구조 반환
        /// 0: nose, 1: left_eye, 2: right_eye, 3: left_ear, 4: right_ear
        /// 5: left_shoulder, 6: right_shoulder, 7: left_elbow, 8: right_elbow
        /// 9: left_wrist, 10: right_wrist, 11: left_hip, 12: right_hip
        /// 13: left_knee, 14: right_knee, 15: left_ankle, 16: right_ankle
        /// </summary>
        private (int, int)[] GetSkeletonConnections()
        {
            return new (int, int)[]
            {
                (0, 1), (0, 2), (1, 3), (2, 4),
                (5, 6), (5, 7), (7, 9), (6, 8), (8, 10), (5, 11), (6, 12),
                (11, 12), (11, 13), (13, 15), (12, 14), (14, 16)
            };
        }
        #endregion
    }
}

