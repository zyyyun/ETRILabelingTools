using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace WinFormsApp1
{
    public static class WaypointNormalizer
    {
        public static List<WaypointMarker> NormalizeVehicleWaypoints(IEnumerable<WaypointMarker> waypoints)
        {
            if (waypoints == null)
            {
                return new List<WaypointMarker>();
            }

            var normalized = new List<WaypointMarker>();
            var vehicleGroups = waypoints
                .Where(w => w != null && string.Equals(w.Label, "vehicle", StringComparison.OrdinalIgnoreCase))
                .GroupBy(w => w.ObjectId);

            foreach (var group in vehicleGroups)
            {
                var ordered = group.OrderBy(w => w.EntryFrame).ThenBy(w => w.ExitFrame).ToList();
                WaypointMarker merged = null;

                foreach (var waypoint in ordered)
                {
                    if (merged == null)
                    {
                        merged = waypoint;
                        continue;
                    }

                    bool canMerge = waypoint.EntryFrame <= merged.ExitFrame + 1;
                    if (!canMerge)
                    {
                        normalized.Add(merged);
                        merged = waypoint;
                        continue;
                    }

                    bool extendsExit = waypoint.ExitFrame > merged.ExitFrame;
                    merged.ExitFrame = Math.Max(merged.ExitFrame, waypoint.ExitFrame);
                    if (extendsExit)
                    {
                        merged.ExitTime = waypoint.ExitTime;
                    }
                }

                if (merged != null)
                {
                    normalized.Add(merged);
                }
            }

            return normalized
                .OrderBy(w => w.EntryFrame)
                .ThenBy(w => w.ObjectId)
                .ToList();
        }

        public static void NormalizeInPlace(List<WaypointMarker> waypoints)
        {
            if (waypoints == null)
            {
                return;
            }

            var nonVehicle = waypoints
                .Where(w => w != null && !string.Equals(w.Label, "vehicle", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var vehicle = NormalizeVehicleWaypoints(waypoints);
            waypoints.Clear();
            waypoints.AddRange(nonVehicle);
            waypoints.AddRange(vehicle);
        }

        public static Rectangle MapChildRectangle(Rectangle parentBefore, Rectangle parentAfter, Rectangle childBefore)
        {
            if (parentBefore.Width <= 0 || parentBefore.Height <= 0)
            {
                return childBefore;
            }

            double scaleX = (double)parentAfter.Width / parentBefore.Width;
            double scaleY = (double)parentAfter.Height / parentBefore.Height;

            return new Rectangle(
                parentAfter.X + (int)Math.Round((childBefore.X - parentBefore.X) * scaleX),
                parentAfter.Y + (int)Math.Round((childBefore.Y - parentBefore.Y) * scaleY),
                Math.Max(1, (int)Math.Round(childBefore.Width * scaleX)),
                Math.Max(1, (int)Math.Round(childBefore.Height * scaleY)));
        }
    }
}