using System.Collections.Generic;
using Newtonsoft.Json;

namespace WinFormsApp1
{
    public class ImageInfo
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("height")] public int Height { get; set; }
        [JsonProperty("width")] public int Width { get; set; }
        [JsonProperty("frame_number")] public int FrameNumber { get; set; }
        [JsonProperty("timestamp")] public string Timestamp { get; set; }
    }

    public class TrackEntry
    {
        [JsonProperty("frame")] public int Frame { get; set; }
        [JsonProperty("timestamp")] public string Timestamp { get; set; }
    }

    public class TrackInfo
    {
        [JsonProperty("entry")] public TrackEntry Entry { get; set; }
        [JsonProperty("exit")] public TrackEntry Exit { get; set; }
        [JsonProperty("current_clip_count")] public int CurrentClipCount { get; set; }
    }

    public class FaceLinkData
    {
        [JsonProperty("face_annotation_id")] public int FaceAnnotationId { get; set; }
        [JsonProperty("body_annotation_id")] public int BodyAnnotationId { get; set; }
    }

    public class PlateLinkData
    {
        [JsonProperty("plate_annotation_id")] public int PlateAnnotationId { get; set; }
        [JsonProperty("body_annotation_id")] public int BodyAnnotationId { get; set; }
    }

    public class AnnotationData
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("image_id")] public int ImageId { get; set; }
        [JsonProperty("category_id")] public int CategoryId { get; set; }
        [JsonProperty("bbox")] public int[] Bbox { get; set; }
        [JsonProperty("area")] public int Area { get; set; }
        [JsonProperty("iscrowd")] public int Iscrowd { get; set; }
        [JsonProperty("track_id")] public int TrackId { get; set; }
        [JsonProperty("vehicle_instance_id", NullValueHandling = NullValueHandling.Ignore)] public int? VehicleInstanceId { get; set; }
        [JsonProperty("track_info")] public TrackInfo TrackInfo { get; set; }
        [JsonProperty("interacting_object", NullValueHandling = NullValueHandling.Ignore)] public string InteractingObject { get; set; }
        [JsonProperty("event_instance_id", NullValueHandling = NullValueHandling.Ignore)] public string EventInstanceId { get; set; }
        [JsonProperty("person_attributes", NullValueHandling = NullValueHandling.Ignore)] public Dictionary<string, object> PersonAttributes { get; set; }
        [JsonProperty("attributes", NullValueHandling = NullValueHandling.Ignore)] public Dictionary<string, object> Attributes { get; set; }
        [JsonProperty("skeleton_3d", NullValueHandling = NullValueHandling.Ignore)] public List<List<double>> Skeleton3D { get; set; }
        [JsonProperty("keypoints_3d", NullValueHandling = NullValueHandling.Ignore)] public List<List<double>> Keypoints3D { get; set; }
    }

    public class CategoryData
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("supercategory")] public string Supercategory { get; set; }
        [JsonProperty("attributes", NullValueHandling = NullValueHandling.Include)] public Dictionary<string, object> Attributes { get; set; }
    }

    public class VideoInfoExtended
    {
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("version")] public string Version { get; set; }
        [JsonProperty("year")] public int Year { get; set; }
        [JsonProperty("date_created")] public string DateCreated { get; set; }
        [JsonProperty("video_file")] public string VideoFile { get; set; }
    }

    public class LabelingDataExtended
    {
        [JsonProperty("info")] public VideoInfoExtended Info { get; set; }
        [JsonProperty("licenses")] public List<object> Licenses { get; set; }
        [JsonProperty("images")] public List<ImageInfo> Images { get; set; }
        [JsonProperty("annotations")] public List<AnnotationData> Annotations { get; set; }
        [JsonProperty("categories")] public List<CategoryData> Categories { get; set; }
        [JsonProperty("failure_ranges")] public Dictionary<string, List<(int start, int end)>>? FailureRanges { get; set; }
        [JsonProperty("face_links", NullValueHandling = NullValueHandling.Ignore)] public List<FaceLinkData>? FaceLinks { get; set; }
        [JsonProperty("plate_links", NullValueHandling = NullValueHandling.Ignore)] public List<PlateLinkData>? PlateLinks { get; set; }
    }
}
