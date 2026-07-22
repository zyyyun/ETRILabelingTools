namespace WinFormsApp1
{
    public class EventWaypointListRow
    {
        public string Entry { get; set; } = string.Empty;
        public string Exit { get; set; } = string.Empty;
        public string Object { get; set; } = string.Empty;
    }

    public static class EventWaypointListRowHelper
    {
        public static EventWaypointListRow Create(WaypointMarker waypoint, string eventName)
        {
            return new EventWaypointListRow
            {
                Entry = waypoint?.EntryTime ?? string.Empty,
                Exit = waypoint?.ExitTime ?? string.Empty,
                Object = eventName ?? string.Empty
            };
        }
    }
}
