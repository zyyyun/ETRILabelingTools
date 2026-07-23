using System;

namespace WinFormsApp1
{
    public static class EventTombstoneUndoHelper
    {
        public static void ApplyUndo(BoundingBox tombstone)
        {
            if (tombstone == null) throw new ArgumentNullException(nameof(tombstone));
            tombstone.IsDeleted = false;
        }

        public static void ApplyRedo(BoundingBox tombstone)
        {
            if (tombstone == null) throw new ArgumentNullException(nameof(tombstone));
            tombstone.IsDeleted = true;
        }
    }
}
