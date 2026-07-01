using System.Drawing;

namespace WinFormsApp1
{
    public class WaypointMarker
    {
        public int EntryFrame { get; set; }
        public int ExitFrame { get; set; }
        public Color MarkerColor { get; set; }
        public string EntryTime { get; set; }
        public string ExitTime { get; set; }
        public int ObjectId { get; set; }
        public string EventInstanceId { get; set; }
        public string Label { get; set; }
        public string InteractingObject { get; set; }
    }
}
