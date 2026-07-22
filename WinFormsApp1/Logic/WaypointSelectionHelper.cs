namespace WinFormsApp1
{
    public static class WaypointSelectionHelper
    {
        public static string ResolveActiveListOwner(
            string currentOwner,
            bool personSelected,
            bool vehicleSelected,
            bool eventSelected)
        {
            return currentOwner switch
            {
                "person" when personSelected => "person",
                "vehicle" when vehicleSelected => "vehicle",
                "event" when eventSelected => "event",
                _ => personSelected ? "person" : vehicleSelected ? "vehicle" : eventSelected ? "event" : null
            };
        }
    }
}
