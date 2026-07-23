using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace WinFormsApp1
{
    public sealed record EventRectangleChange(BoundingBox Box, Rectangle Before, Rectangle After);

    public sealed record EventManualFrameProvenance(string IdentityKey, int FrameIndex, bool WasManuallyAdjustedBeforeEdit);

    public sealed class EventRectanglePropagationBatch
    {
        public EventRectanglePropagationBatch(EventRectangleChange source, IReadOnlyList<EventRectangleChange> updatedTargets, IReadOnlyList<BoundingBox> createdBoxes, EventManualFrameProvenance manualFrameProvenance = null)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            UpdatedTargets = updatedTargets ?? Array.Empty<EventRectangleChange>();
            CreatedBoxes = createdBoxes ?? Array.Empty<BoundingBox>();
            ManualFrameProvenance = manualFrameProvenance;
        }

        public EventRectangleChange Source { get; }
        public IReadOnlyList<EventRectangleChange> UpdatedTargets { get; }
        public IReadOnlyList<BoundingBox> CreatedBoxes { get; }
        public EventManualFrameProvenance ManualFrameProvenance { get; }
    }

    public static class EventRectanglePropagationUndoHelper
    {
        public static EventRectanglePropagationBatch CreateBatch(BoundingBox source, Rectangle sourceBefore, EventWaypointBoxPropagationPlan plan)
        {
            return CreateBatch(source, sourceBefore, plan, null, source?.FrameIndex ?? 0, false);
        }

        public static EventRectanglePropagationBatch CreateBatch(BoundingBox source, Rectangle sourceBefore, EventWaypointBoxPropagationPlan plan, string manualFrameIdentityKey, int manualFrameIndex, bool wasManuallyAdjustedBeforeEdit)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            return new EventRectanglePropagationBatch(
                new EventRectangleChange(source, sourceBefore, source.Rectangle),
                plan.Updates.Select(update => new EventRectangleChange(update.Box, update.Box.Rectangle, update.Rectangle)).ToList().AsReadOnly(),
                plan.Additions.Select(addition => addition.Box).ToList().AsReadOnly(),
                string.IsNullOrWhiteSpace(manualFrameIdentityKey)
                    ? null
                    : new EventManualFrameProvenance(manualFrameIdentityKey, manualFrameIndex, wasManuallyAdjustedBeforeEdit));
        }

        public static bool HasGeometryChanged(Rectangle before, Rectangle after) => before != after;

        public static void ApplyForward(IList<BoundingBox> boxes, EventRectanglePropagationBatch batch)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            batch.Source.Box.Rectangle = batch.Source.After;
            foreach (var change in batch.UpdatedTargets) change.Box.Rectangle = change.After;
            foreach (var created in batch.CreatedBoxes) if (!boxes.Contains(created)) boxes.Add(created);
        }

        public static void ApplyForward(IList<BoundingBox> boxes, EventRectanglePropagationBatch batch, IDictionary<string, List<int>> manuallyAdjustedFrames)
        {
            ApplyForward(boxes, batch);
            RestoreManualFrameProvenance(batch, manuallyAdjustedFrames, manuallyAdjusted: true);
        }

        public static void ApplyUndo(IList<BoundingBox> boxes, EventRectanglePropagationBatch batch)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            batch.Source.Box.Rectangle = batch.Source.Before;
            foreach (var change in batch.UpdatedTargets) change.Box.Rectangle = change.Before;
            foreach (var created in batch.CreatedBoxes) boxes.Remove(created);
        }

        public static void ApplyUndo(IList<BoundingBox> boxes, EventRectanglePropagationBatch batch, IDictionary<string, List<int>> manuallyAdjustedFrames)
        {
            ApplyUndo(boxes, batch);
            RestoreManualFrameProvenance(batch, manuallyAdjustedFrames, batch.ManualFrameProvenance?.WasManuallyAdjustedBeforeEdit == true);
        }

        private static void RestoreManualFrameProvenance(EventRectanglePropagationBatch batch, IDictionary<string, List<int>> manuallyAdjustedFrames, bool manuallyAdjusted)
        {
            if (batch?.ManualFrameProvenance == null || manuallyAdjustedFrames == null)
            {
                return;
            }

            var provenance = batch.ManualFrameProvenance;
            if (manuallyAdjusted)
            {
                if (!manuallyAdjustedFrames.TryGetValue(provenance.IdentityKey, out var frames))
                {
                    frames = new List<int>();
                    manuallyAdjustedFrames[provenance.IdentityKey] = frames;
                }

                if (!frames.Contains(provenance.FrameIndex))
                {
                    frames.Add(provenance.FrameIndex);
                    frames.Sort();
                }
                return;
            }

            if (manuallyAdjustedFrames.TryGetValue(provenance.IdentityKey, out var existingFrames))
            {
                existingFrames.Remove(provenance.FrameIndex);
                if (existingFrames.Count == 0)
                {
                    manuallyAdjustedFrames.Remove(provenance.IdentityKey);
                }
            }
        }
    }
}
