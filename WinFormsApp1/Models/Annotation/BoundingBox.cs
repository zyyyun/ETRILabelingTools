using System.Collections.Generic;
using System.Drawing;

namespace WinFormsApp1
{
    public class BoundingBox
    {
        public int FrameIndex { get; set; }
        public Rectangle Rectangle { get; set; }
        public string Label { get; set; }
        public int PersonId { get; set; }
        public int VehicleId { get; set; }
        public int EventId { get; set; }
        public string Action { get; set; }
        public string EventInstanceId { get; set; }
        public string VehicleName { get; set; }
        public string EventName { get; set; }
        public bool IsDeleted { get; set; }
        public Dictionary<string, object> PersonAttributes { get; set; }
        public List<List<double>> Skeleton3D { get; set; }
    }
}
