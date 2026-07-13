using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace WinFormsApp1
{
    public readonly record struct TimelineSegmentLayout(WaypointMarker Waypoint, RectangleF Bounds)
    {
        public float Width => Bounds.Width;
    }

    public static class TimelineLayoutHelper
    {
        public const int HeaderHeight = 8;
        public const int RowHeight = 16;
        public const int RowPadding = 1;
        public const int LabelWidth = 16;
        public const int SegmentRadius = 3;
        public const int MinSegmentWidth = 4;
        public const int VideoControlsTopMargin = 16;
        public const int VideoControlsBottomMargin = 16;
        public const int ObjectInfoHeight = 100;
        public const int TimelinePanelTop = 70;

        public static Color BackgroundColor => Color.FromArgb(249, 250, 251);
        public static Color DividerColor => Color.FromArgb(229, 231, 235);
        public static Color PlayheadColor => Color.FromArgb(55, 65, 81);
        public static Color SelectionColor => Color.FromArgb(37, 99, 235);
        public static Color SegmentTextColor => Color.FromArgb(31, 41, 55);
        public static Color PlayheadHeadColor => Color.FromArgb(245, 158, 11);

        public static int GetTimelineRowY(string label)
        {
            return label switch
            {
                "person" => HeaderHeight,
                "vehicle" => HeaderHeight + RowHeight,
                "event" => HeaderHeight + RowHeight * 2,
                _ => HeaderHeight
            };
        }

        public static Color GetTimelineSegmentColor(string label)
        {
            return label switch
            {
                "person" => Color.FromArgb(247, 197, 197),
                "vehicle" => Color.FromArgb(191, 219, 254),
                "event" => Color.FromArgb(187, 247, 208),
                _ => Color.FromArgb(209, 213, 219)
            };
        }

        public static Color GetTimelineLabelColor(string label)
        {
            return label switch
            {
                "person" => Color.FromArgb(185, 28, 28),
                "vehicle" => Color.FromArgb(29, 78, 216),
                "event" => Color.FromArgb(21, 128, 61),
                _ => Color.FromArgb(75, 85, 99)
            };
        }

        public static string GetTimelineDisplayText(WaypointMarker waypoint)
        {
            string prefix = waypoint.Label switch
            {
                "person" => "P",
                "vehicle" => "V",
                "event" => "E",
                _ => "?"
            };

            return $"{prefix}{waypoint.ObjectId:D2}";
        }


        public static int GetRequiredTimelinePanelHeight()
        {
            return HeaderHeight + RowHeight * 3;
        }

        public static int GetMinimumVideoControlsHeight()
        {
            return Math.Max(VideoControlsTopMargin + ObjectInfoHeight + VideoControlsBottomMargin, TimelinePanelTop + GetRequiredTimelinePanelHeight() + 6);
        }
        public static int CalculateTimelineWidth(int panelTimelineLeft, int objectInfoLeft, int reservedGap, int minimumWidth)
        {
            return Math.Max(minimumWidth, objectInfoLeft - panelTimelineLeft - reservedGap);
        }

        public static TimelineSegmentLayout CreateSegmentLayout(WaypointMarker waypoint, int panelWidth, int totalFrames)
        {
            int safeTotalFrames = Math.Max(totalFrames, 1);
            int trackLeft = LabelWidth;
            int trackWidth = Math.Max(panelWidth - trackLeft, 1);
            int rowY = GetTimelineRowY(waypoint.Label);

            int startX = trackLeft + (int)(trackWidth * ((float)waypoint.EntryFrame / safeTotalFrames));
            int endX = trackLeft + (int)(trackWidth * ((float)waypoint.ExitFrame / safeTotalFrames));
            int width = Math.Max(endX - startX, MinSegmentWidth);

            var bounds = new RectangleF(
                startX,
                rowY + RowPadding,
                width,
                RowHeight - RowPadding * 2);

            return new TimelineSegmentLayout(waypoint, bounds);
        }

        public static int GetPlayheadX(int panelWidth, float timelineProgress)
        {
            int trackLeft = LabelWidth;
            int trackWidth = Math.Max(panelWidth - trackLeft, 1);
            float clamped = Math.Max(0f, Math.Min(1f, timelineProgress));
            return trackLeft + (int)(trackWidth * clamped);
        }

        public static int GetFrameX(int frameIndex, int panelWidth, int totalFrames)
        {
            int safeTotalFrames = Math.Max(totalFrames, 1);
            int trackLeft = LabelWidth;
            int trackWidth = Math.Max(panelWidth - trackLeft, 1);
            return trackLeft + (int)(trackWidth * ((float)frameIndex / safeTotalFrames));
        }

        public static int GetFrameFromMouseX(int mouseX, int panelWidth, int totalFrames)
        {
            if (totalFrames <= 0)
            {
                return 0;
            }

            int trackLeft = LabelWidth;
            int trackWidth = Math.Max(panelWidth - trackLeft, 1);
            int clampedX = Math.Max(trackLeft, Math.Min(panelWidth, mouseX));
            float clickPosition = (float)(clampedX - trackLeft) / trackWidth;
            clickPosition = Math.Max(0f, Math.Min(1f, clickPosition));

            int targetFrame = (int)(clickPosition * totalFrames);
            return Math.Max(0, Math.Min(totalFrames - 1, targetFrame));
        }

        public static WaypointMarker? TryHitWaypointSegment(IEnumerable<WaypointMarker> waypoints, int mouseX, int mouseY, int panelWidth, int totalFrames)
        {
            if (waypoints == null || totalFrames <= 0)
            {
                return null;
            }

            foreach (var waypoint in waypoints.Reverse())
            {
                var segment = CreateSegmentLayout(waypoint, panelWidth, totalFrames);
                if (segment.Bounds.Contains(mouseX, mouseY))
                {
                    return waypoint;
                }
            }

            return null;
        }
    }
}


