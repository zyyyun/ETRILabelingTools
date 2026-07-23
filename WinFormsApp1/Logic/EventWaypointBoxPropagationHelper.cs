using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace WinFormsApp1
{
    public sealed record EventWaypointBoxUpdate(BoundingBox Box, Rectangle Rectangle);

    public sealed record EventWaypointBoxAddition(BoundingBox Box);

    public sealed class EventWaypointBoxPropagationPlan
    {
        public EventWaypointBoxPropagationPlan(
            IReadOnlyList<EventWaypointBoxUpdate> updates,
            IReadOnlyList<EventWaypointBoxAddition> additions)
        {
            Updates = updates ?? Array.Empty<EventWaypointBoxUpdate>();
            Additions = additions ?? Array.Empty<EventWaypointBoxAddition>();
        }

        public IReadOnlyList<EventWaypointBoxUpdate> Updates { get; }
        public IReadOnlyList<EventWaypointBoxAddition> Additions { get; }
    }

    public static class EventWaypointBoxPropagationHelper
    {
        public static EventWaypointBoxPropagationPlan PlanPropagation(
            BoundingBox source,
            IEnumerable<BoundingBox> boxes,
            IEnumerable<WaypointMarker> waypoints,
            IReadOnlyDictionary<string, List<int>> manuallyAdjustedFrames)
        {
            if (source == null || boxes == null || waypoints == null ||
                source.IsDeleted ||
                !string.Equals(source.Label, "event", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(source.EventInstanceId))
            {
                return EmptyPlan();
            }

            var allBoxes = boxes.Where(box => box != null).ToList();
            var scope = EventWaypointUpdateHelper.ResolveActiveScope(source, allBoxes, waypoints);
            if (scope == null || scope.Waypoint == null || source.FrameIndex >= scope.Waypoint.ExitFrame)
            {
                return EmptyPlan();
            }

            string identityKey = TrackingIdentityHelper.GetIdentityKey(source);
            var manuallyAdjusted = manuallyAdjustedFrames != null &&
                manuallyAdjustedFrames.TryGetValue(identityKey, out var adjustedFrames) &&
                adjustedFrames != null
                    ? new HashSet<int>(adjustedFrames)
                    : new HashSet<int>();

            var sameInstanceBoxes = allBoxes
                .Where(box =>
                    string.Equals(box.Label, "event", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(box.EventInstanceId, source.EventInstanceId, StringComparison.Ordinal))
                .GroupBy(box => box.FrameIndex)
                .ToDictionary(group => group.Key, group => group.ToList());

            var updates = new List<EventWaypointBoxUpdate>();
            var additions = new List<EventWaypointBoxAddition>();
            for (int frame = source.FrameIndex + 1; frame <= scope.Waypoint.ExitFrame; frame++)
            {
                if (manuallyAdjusted.Contains(frame))
                {
                    continue;
                }

                sameInstanceBoxes.TryGetValue(frame, out var existingBoxes);
                if (existingBoxes != null && existingBoxes.Any(box => box.IsDeleted))
                {
                    continue;
                }

                var activeBoxes = existingBoxes?.Where(box => !box.IsDeleted).ToList();
                if (activeBoxes != null && activeBoxes.Count > 0)
                {
                    updates.AddRange(activeBoxes.Select(box => new EventWaypointBoxUpdate(box, source.Rectangle)));
                    continue;
                }

                additions.Add(new EventWaypointBoxAddition(new BoundingBox
                {
                    FrameIndex = frame,
                    Rectangle = source.Rectangle,
                    Label = source.Label,
                    PersonId = source.PersonId,
                    VehicleId = source.VehicleId,
                    EventId = source.EventId,
                    EventInstanceId = source.EventInstanceId,
                    Action = source.Action
                }));
            }

            return new EventWaypointBoxPropagationPlan(updates.AsReadOnly(), additions.AsReadOnly());
        }

        private static EventWaypointBoxPropagationPlan EmptyPlan()
        {
            return new EventWaypointBoxPropagationPlan(
                Array.Empty<EventWaypointBoxUpdate>(),
                Array.Empty<EventWaypointBoxAddition>());
        }
    }
}
