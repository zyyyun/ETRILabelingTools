using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace WinFormsApp1
{
    public sealed record EventRectangleChange(BoundingBox Box, Rectangle Before, Rectangle After);

    public sealed class EventRectanglePropagationBatch
    {
        public EventRectanglePropagationBatch(EventRectangleChange source, IReadOnlyList<EventRectangleChange> updatedTargets, IReadOnlyList<BoundingBox> createdBoxes)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            UpdatedTargets = updatedTargets ?? Array.Empty<EventRectangleChange>();
            CreatedBoxes = createdBoxes ?? Array.Empty<BoundingBox>();
        }

        public EventRectangleChange Source { get; }
        public IReadOnlyList<EventRectangleChange> UpdatedTargets { get; }
        public IReadOnlyList<BoundingBox> CreatedBoxes { get; }
    }

    public static class EventRectanglePropagationUndoHelper
    {
        public static EventRectanglePropagationBatch CreateBatch(BoundingBox source, Rectangle sourceBefore, EventWaypointBoxPropagationPlan plan)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            return new EventRectanglePropagationBatch(
                new EventRectangleChange(source, sourceBefore, source.Rectangle),
                plan.Updates.Select(update => new EventRectangleChange(update.Box, update.Box.Rectangle, update.Rectangle)).ToList().AsReadOnly(),
                plan.Additions.Select(addition => addition.Box).ToList().AsReadOnly());
        }

        public static void ApplyForward(IList<BoundingBox> boxes, EventRectanglePropagationBatch batch)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            batch.Source.Box.Rectangle = batch.Source.After;
            foreach (var change in batch.UpdatedTargets) change.Box.Rectangle = change.After;
            foreach (var created in batch.CreatedBoxes) if (!boxes.Contains(created)) boxes.Add(created);
        }

        public static void ApplyUndo(IList<BoundingBox> boxes, EventRectanglePropagationBatch batch)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            batch.Source.Box.Rectangle = batch.Source.Before;
            foreach (var change in batch.UpdatedTargets) change.Box.Rectangle = change.Before;
            foreach (var created in batch.CreatedBoxes) boxes.Remove(created);
        }
    }
}
