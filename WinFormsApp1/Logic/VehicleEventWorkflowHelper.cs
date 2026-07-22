using System;

namespace WinFormsApp1
{
    public static class VehicleEventWorkflowHelper
    {
        public static bool ShouldSkipVehicleWaypointSideEffects(
            string currentSelectedLabel,
            int entryEventBoxCount)
        {
            return entryEventBoxCount > 0 &&
                string.Equals(currentSelectedLabel, "event", StringComparison.OrdinalIgnoreCase);
        }
    }
}
