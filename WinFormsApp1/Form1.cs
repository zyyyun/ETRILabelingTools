using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Newtonsoft.Json;
using System.IO;
using OpenCvSharp.Tracking;
using Compunet.YoloSharp;
using Compunet.YoloSharp.Data;
using Compunet.YoloSharp.Plotting;
using FFMpegCore;
using FFMpegCore.Enums;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace WinFormsApp1
{
    public enum UndoActionType { AddBox, RemoveBox, ModifyBox, Tracking }

    public class UndoAction
    {
        public UndoActionType Type { get; set; }
        public BoundingBox Box { get; set; }
        public Rectangle OriginalRectangle { get; set; }
        public string OriginalLabel { get; set; }
        public int OriginalObjectId { get; set; }
        public List<BoundingBox> TrackedBoxes { get; set; }
    }

    public class WaypointMarker
    {
        public int EntryFrame { get; set; }
        public int ExitFrame { get; set; }
        public Color MarkerColor { get; set; }
        public string EntryTime { get; set; }
        public string ExitTime { get; set; }
        public int ObjectId { get; set; } // PersonId, VehicleId, EventId 중 하나
        public string Label { get; set; }
        public string InteractingObject { get; set; } // Event 전용: 객체(P/V) 텍스트
    }

    // ✅ BBox 크기 조정 핸들 (4개 모서리만)
    public enum ResizeHandle
    {
        None,
        TopLeft,      // 좌상단 모서리
        TopRight,     // 우상단 모서리
        BottomLeft,   // 좌하단 모서리
        BottomRight   // 우하단 모서리
    }

    public class CustomLabel
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public int Id { get; set; }
        public Panel Panel { get; set; }
        public Label Label { get; set; }
    }

    public class BoundingBox
    {
        public int FrameIndex { get; set; }
        public Rectangle Rectangle { get; set; }
        public string Label { get; set; }
        public int PersonId { get; set; }
        public int VehicleId { get; set; }
        public int EventId { get; set; }
        public string Action { get; set; }
        public string VehicleName { get; set; }
        public string EventName { get; set; }
        public bool IsDeleted { get; set; } // ✅ 삭제된 박스 표시 (흔적 유지)
        public Dictionary<string, object> PersonAttributes { get; set; } // Person 전용: 속성 정보
    }

    public class SubtitleEntry
    {
        public int Index { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Text { get; set; }
    }

    #region JSON Serialization Classes

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

    public class AnnotationData
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("image_id")] public int ImageId { get; set; }
        [JsonProperty("category_id")] public int CategoryId { get; set; }
        [JsonProperty("bbox")] public int[] Bbox { get; set; }
        [JsonProperty("area")] public int Area { get; set; }
        [JsonProperty("iscrowd")] public int Iscrowd { get; set; }
        [JsonProperty("track_id")] public int TrackId { get; set; }
        [JsonProperty("track_info")] public TrackInfo TrackInfo { get; set; }
        // Event 전용: 상호작용 객체 텍스트 (person/vehicle 등)
        [JsonProperty("interacting_object", NullValueHandling = NullValueHandling.Ignore)]
        public string InteractingObject { get; set; }
        // Person 전용: 속성 정보 (null 값도 포함)
        [JsonProperty("attributes")]
        public Dictionary<string, object> Attributes { get; set; }
    }

    public class CategoryData
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("supercategory")] public string Supercategory { get; set; }
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
    }

    #endregion

    // Person 속성 관리 클래스
    public class PersonAttributeEntry
    {
        public string AttributeName { get; set; }
        public object Value { get; set; }
        public int WaypointEntryFrame { get; set; }
        public int PersonId { get; set; }
    }

    public class PersonAttributeStore
    {
        // Global 속성: person_id -> attributes
        private Dictionary<int, Dictionary<string, object>> globalAttributes = new Dictionary<int, Dictionary<string, object>>();
        
        // Waypoint-scoped 속성: person_id -> entries (EntryFrame 순으로 정렬)
        private Dictionary<int, List<PersonAttributeEntry>> waypointScopedAttributes = new Dictionary<int, List<PersonAttributeEntry>>();

        // 노란색 표시 속성 목록 (Waypoint-scoped)
        private static readonly HashSet<string> waypointScopedAttributeNames = new HashSet<string>
        {
            "Occlusion",      // View 탭의 노란색 표시 속성
            "BodyView"        // View 탭의 노란색 표시 속성
        };

        public static bool IsWaypointScoped(string attributeName)
        {
            return waypointScopedAttributeNames.Contains(attributeName);
        }

        // 속성 읽기: 현재 프레임에 적용되는 속성 값 반환
        public object GetAttribute(int personId, int frameIndex, string attributeName, List<WaypointMarker> waypointMarkers)
        {
            // 먼저 waypoint-scoped 속성 확인 (Global 속성도 waypoint-scoped로 저장될 수 있음)
            var personWaypoints = waypointMarkers
                .Where(w => w.Label == "person" && w.ObjectId == personId)
                .OrderByDescending(w => w.EntryFrame)
                .ToList();

            foreach (var waypoint in personWaypoints)
            {
                if (frameIndex >= waypoint.EntryFrame)
                {
                    // 이 waypoint부터 적용되는 속성 찾기
                    if (waypointScopedAttributes.ContainsKey(personId))
                    {
                        var entry = waypointScopedAttributes[personId]
                            .Where(e => e.AttributeName == attributeName && e.WaypointEntryFrame == waypoint.EntryFrame)
                            .FirstOrDefault();
                        
                        if (entry != null)
                        {
                            return entry.Value;
                        }
                    }
                }
            }
            
            // Waypoint-scoped 항목이 없으면 Global 속성 확인
            if (globalAttributes.ContainsKey(personId) && globalAttributes[personId].ContainsKey(attributeName))
            {
                return globalAttributes[personId][attributeName];
            }
            
            return null;
        }

        // 속성 저장
        public void SetAttribute(int personId, int waypointEntryFrame, string attributeName, object value)
        {
            bool isWaypointScoped = IsWaypointScoped(attributeName);
            
            if (value == null)
            {
                // null 값인 경우: 전역적으로 설정 (모든 waypoint-scoped 항목 제거)
                if (!globalAttributes.ContainsKey(personId))
                {
                    globalAttributes[personId] = new Dictionary<string, object>();
                }
                globalAttributes[personId][attributeName] = null;
                
                // 모든 waypoint-scoped 항목에서 해당 속성 제거
                if (waypointScopedAttributes.ContainsKey(personId))
                {
                    waypointScopedAttributes[personId].RemoveAll(e => 
                        e.AttributeName == attributeName);
                }
            }
            else
            {
                // null이 아닌 값인 경우
                if (isWaypointScoped)
                {
                    // Waypoint-scoped 속성: 항상 waypoint-scoped로 저장
                    if (!waypointScopedAttributes.ContainsKey(personId))
                    {
                        waypointScopedAttributes[personId] = new List<PersonAttributeEntry>();
                    }
                    
                    // 기존 항목 제거 (같은 waypoint, 같은 속성)
                    waypointScopedAttributes[personId].RemoveAll(e => 
                        e.AttributeName == attributeName && e.WaypointEntryFrame == waypointEntryFrame);
                    
                    // 새 항목 추가
                    waypointScopedAttributes[personId].Add(new PersonAttributeEntry
                    {
                        AttributeName = attributeName,
                        Value = value,
                        WaypointEntryFrame = waypointEntryFrame,
                        PersonId = personId
                    });
                    
                    // EntryFrame 순으로 정렬
                    waypointScopedAttributes[personId].Sort((a, b) => a.WaypointEntryFrame.CompareTo(b.WaypointEntryFrame));
                }
                else
                {
                    // Global 속성인 경우
                    // Global에 값이 없거나 null인 경우: Global로 저장
                    // Global에 이미 값이 있는 경우: waypoint-scoped로 저장 (Global 값은 유지)
                    
                    bool hasExistingGlobal = globalAttributes.ContainsKey(personId) && 
                                             globalAttributes[personId].ContainsKey(attributeName) &&
                                             globalAttributes[personId][attributeName] != null;
                    
                    if (!hasExistingGlobal)
                    {
                        // 처음 설정 또는 null에서 설정: Global로 저장
                        if (!globalAttributes.ContainsKey(personId))
                        {
                            globalAttributes[personId] = new Dictionary<string, object>();
                        }
                        globalAttributes[personId][attributeName] = value;
                    }
                    else
                    {
                        // 이미 Global 값이 있음: waypoint-scoped로 저장 (Global 값은 유지)
                        if (!waypointScopedAttributes.ContainsKey(personId))
                        {
                            waypointScopedAttributes[personId] = new List<PersonAttributeEntry>();
                        }
                        
                        // 기존 항목 제거 (같은 waypoint, 같은 속성)
                        waypointScopedAttributes[personId].RemoveAll(e => 
                            e.AttributeName == attributeName && e.WaypointEntryFrame == waypointEntryFrame);
                        
                        // 새 항목 추가
                        waypointScopedAttributes[personId].Add(new PersonAttributeEntry
                        {
                            AttributeName = attributeName,
                            Value = value,
                            WaypointEntryFrame = waypointEntryFrame,
                            PersonId = personId
                        });
                        
                        // EntryFrame 순으로 정렬
                        waypointScopedAttributes[personId].Sort((a, b) => a.WaypointEntryFrame.CompareTo(b.WaypointEntryFrame));
                    }
                }
            }
        }

        // 모든 속성 가져오기 (현재 프레임 기준)
        public Dictionary<string, object> GetAllAttributes(int personId, int frameIndex, List<WaypointMarker> waypointMarkers)
        {
            var result = new Dictionary<string, object>();
            
            // 모든 가능한 속성 이름 목록 (나중에 정의)
            var allAttributeNames = new HashSet<string>();
            
            // Global 속성 추가
            if (globalAttributes.ContainsKey(personId))
            {
                foreach (var kvp in globalAttributes[personId])
                {
                    if (kvp.Value != null)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }
            }
            
            // Waypoint-scoped 속성 추가 (우선순위가 높으므로 나중에 덮어쓰기)
            var personWaypoints = waypointMarkers
                .Where(w => w.Label == "person" && w.ObjectId == personId)
                .OrderByDescending(w => w.EntryFrame)
                .ToList();

            foreach (var waypoint in personWaypoints)
            {
                if (frameIndex >= waypoint.EntryFrame)
                {
                    if (waypointScopedAttributes.ContainsKey(personId))
                    {
                        var entries = waypointScopedAttributes[personId]
                            .Where(e => e.WaypointEntryFrame == waypoint.EntryFrame);
                        
                        foreach (var entry in entries)
                        {
                            result[entry.AttributeName] = entry.Value;
                        }
                    }
                    break; // 첫 번째로 찾은 waypoint만 사용
                }
            }
            
            return result;
        }

        // person_id의 모든 속성 초기화
        public void ClearPersonAttributes(int personId)
        {
            if (globalAttributes.ContainsKey(personId))
            {
                globalAttributes[personId].Clear();
            }
            if (waypointScopedAttributes.ContainsKey(personId))
            {
                waypointScopedAttributes[personId].Clear();
            }
        }
    }

    public abstract class TrackingEngine
    {
        public abstract List<BoundingBox> TrackObjects(
            VideoCapture videoCapture,
            BoundingBox startBox,
            int startFrame,
            int endFrame,
            double fps);
    }

    public class YoloTrackingEngine : TrackingEngine
    {
        private YoloPredictor _predictor;
        private string _tempImagePath;

        private static string GetBaseCategory(string appLabel)
        {
            if (string.IsNullOrEmpty(appLabel)) return "";

            var parts = appLabel.Split('_');
            if (parts.Length > 0)
            {
                switch (parts[0])
                {
                    case "person":
                        return "person";
                    case "vehicle":
                        // "vehicle_0_car" -> "car"
                        return parts.Length > 2 ? parts[2] : "car"; 
                    case "event":
                        // "event_0_contact" -> "contact"
                        return parts.Length > 2 ? parts[2] : "event";
                }
            }
            return appLabel; // Fallback
        }

        // ✅ BoundingBox에서 실제 COCO 카테고리 이름을 추출하는 메서드
        private static string GetBaseCategoryFromBox(BoundingBox box)
        {
            if (box == null) return "";

            switch (box.Label)
            {
                case "person":
                    return "person";
                case "vehicle":
                    // VehicleName이 있으면 사용, 없으면 VehicleId로 매핑
                    if (!string.IsNullOrEmpty(box.VehicleName))
                    {
                        // VehicleName은 "car", "motorcycle", "e_scooter", "bicycle" 중 하나
                        // COCO 데이터셋 매핑: COCO에는 car, motorcycle, bus, truck, bicycle만 있음
                        // e_scooter는 COCO에 없으므로 motorcycle로 매핑
                        if (box.VehicleName == "e_scooter")
                            return "motorcycle"; // e_scooter -> motorcycle (COCO에 scooter 없음)
                        // 다른 vehicle 이름들(car, motorcycle, bicycle)은 그대로 사용
                        return box.VehicleName;
                    }
                    else if (box.VehicleId > 0)
                    {
                        // VehicleId로 매핑
                        switch (box.VehicleId)
                        {
                            case 1: return "car";
                            case 2: return "motorcycle";
                            case 3: return "motorcycle"; // e_scooter -> motorcycle
                            case 4: return "bicycle";
                            default: return "car";
                        }
                    }
                    return "car"; // 기본값
                case "event":
                    // event는 추적하지 않으므로 필요 없음
                    return "event";
                default:
                    return box.Label;
            }
        }

        public YoloTrackingEngine(string modelPath)
        {
            _predictor = new YoloPredictor(modelPath);
            
            // 임시 파일 경로 설정 및 디렉토리 확인
            string tempDir = Path.GetTempPath();
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }
            _tempImagePath = Path.Combine(tempDir, "yolo_frame.jpg");
        }

        public override List<BoundingBox> TrackObjects(
            VideoCapture videoCapture,
            BoundingBox startBox,
            int startFrame,
            int endFrame,
            double fps)
        {
            int _inertiaCount;
            Dictionary<string, int> _detectionCount;
            return TrackObjectsWithFailures(videoCapture, startBox, startFrame, endFrame, fps, out _, out _inertiaCount, out _detectionCount);
        }

        // ✅ 실패 구간을 반환하는 오버로드 메서드
        public List<BoundingBox> TrackObjectsWithFailures(
            VideoCapture videoCapture,
            BoundingBox startBox,
            int startFrame,
            int endFrame,
            double fps,
            out List<(int start, int end)> failureRanges,
            out int inertiaAppliedCount,
            out Dictionary<string, int> detectionCountByCategory)
        {
            failureRanges = new List<(int, int)>();
            inertiaAppliedCount = 0;
            detectionCountByCategory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            
            var trackedBoxes = new List<BoundingBox>();
            videoCapture.Set(VideoCaptureProperties.PosFrames, startFrame);
            Mat frame = new Mat();

            // 이전 프레임 박스 초기화: 사용자 지정 startBox로 시작
            Rectangle previousRect = startBox.Rectangle;
            string fixedLabel = startBox.Label;
            int fixedIdPerson = startBox.PersonId;
            int fixedIdVehicle = startBox.VehicleId;
            int fixedIdEvent = startBox.EventId;

            // ✅ 추적 대상의 기본 카테고리를 미리 추출합니다. 
            // vehicle의 경우 실제 종류(car, motorcycle 등)를 반환하여 COCO 데이터셋과 매칭
            string targetCategory = GetBaseCategoryFromBox(startBox);
            
            // ✅ vehicle_car인 경우 모든 4륜 자동차 종류(bus, truck 등)를 포함하도록 설정
            HashSet<string> targetCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (targetCategory == "car")
            {
                // vehicle_car는 모든 4륜 자동차 종류를 추적 대상으로 설정
                targetCategories.Add("car");
                targetCategories.Add("bus");
                targetCategories.Add("truck");
            }
            else
            {
                // 다른 vehicle 종류는 기존과 동일하게 단일 카테고리만 추적
                targetCategories.Add(targetCategory);
            }

            // ✅ 추적 실패 분석을 위한 통계 변수
            int totalFrames = endFrame - startFrame + 1;
            int successCount = 0;
            int failureCount = 0;
            int failureStartFrame = -1;
            int consecutiveFailures = 0;
            int maxConsecutiveFailures = 0;
            string lastFailureReason = "";
            
            // ✅ 30프레임 연속 실패 구간 추적
            const int FAILURE_THRESHOLD = 30;
            var localFailureRanges = new List<(int start, int end)>();
            int thresholdFailureStart = -1;
            
            // ✅ 성공 프레임 추적 (보간용)
            var successfulFrames = new Dictionary<int, Rectangle>(); // FrameIndex -> Rectangle
            
            // ✅ 고유 객체 추적: 카테고리별로 추적 중인 객체들의 마지막 위치를 저장
            // Key: 카테고리명, Value: List<(마지막 위치, 마지막 프레임)>
            var trackedObjectsByCategory = new Dictionary<string, List<(Rectangle lastRect, int lastFrame)>>(StringComparer.OrdinalIgnoreCase);
            const double OBJECT_MATCH_IOU_THRESHOLD = 0.5; // 같은 객체로 판단하는 IoU 임계값
            

            for (int i = startFrame; i <= endFrame; i++)
            {
                if (!videoCapture.Read(frame) || frame.Empty())
                    break;

                try
                {
                    // Mat을 임시 파일로 저장 (YoloSharp는 파일 입력을 요구)
                    Cv2.ImWrite(_tempImagePath, frame);

                    var detections = _predictor.Detect(_tempImagePath);
                    
                    // ✅ YOLO 탐지 결과 로그 출력


                    // ✅ 이전 박스와 IoU가 가장 큰 검출만 채택 (Label 필터링 적용)
                    double bestIou = 0.0;
                    Detection bestDetection = null;
                    const double MIN_IOU_THRESHOLD = 0.3; // IoU 최소 임계값
                    
                    int candidateCount = 0; // 같은 카테고리의 후보 수
                    double maxIouOfCategory = 0.0; // 같은 카테고리 중 최대 IoU
                    
                    foreach (var d in detections)
                    {
                        // ✅ 1. Label 필터링: 같은 기본 카테고리를 가진 객체만 후보로 고려합니다.
                        // YOLO 라이브러리가 "0: 'person'" 형식의 이름을 반환하므로, 순수한 이름만 추출합니다.
                        string rawDetectionName = d.Name.ToString();
                        string detectionName = rawDetectionName;
                        int firstQuote = rawDetectionName.IndexOf('\'');
                        int lastQuote = rawDetectionName.LastIndexOf('\'');
                        if (firstQuote != -1 && lastQuote > firstQuote)
                        {
                            detectionName = rawDetectionName.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                        }

                        // ✅ 고유 객체 카운트: 같은 객체가 여러 프레임에 걸쳐 추적되는 경우 1번만 카운트
                        if (!string.IsNullOrEmpty(detectionName))
                        {
                            var detectionRect = new Rectangle((int)d.Bounds.X, (int)d.Bounds.Y, (int)d.Bounds.Width, (int)d.Bounds.Height);
                            
                            // 해당 카테고리의 추적 중인 객체 목록 가져오기
                            if (!trackedObjectsByCategory.ContainsKey(detectionName))
                            {
                                trackedObjectsByCategory[detectionName] = new List<(Rectangle, int)>();
                            }
                            
                            var trackedObjects = trackedObjectsByCategory[detectionName];
                            bool isExistingObject = false;
                            
                            // 기존 추적 중인 객체와 IoU 계산하여 같은 객체인지 확인
                            for (int objIdx = 0; objIdx < trackedObjects.Count; objIdx++)
                            {
                                var (lastRect, lastFrame) = trackedObjects[objIdx];
                                double iou = ComputeIoU(lastRect, detectionRect);
                                
                                // IoU가 임계값보다 높으면 같은 객체로 판단
                                if (iou > OBJECT_MATCH_IOU_THRESHOLD)
                                {
                                    // 기존 객체의 위치 업데이트 (프레임 번호도 업데이트)
                                    trackedObjects[objIdx] = (detectionRect, i);
                                    isExistingObject = true;
                                    break;
                                }
                            }
                            
                            // 새로운 객체인 경우에만 카운트
                            if (!isExistingObject)
                            {
                                if (!detectionCountByCategory.ContainsKey(detectionName))
                                    detectionCountByCategory[detectionName] = 0;
                                detectionCountByCategory[detectionName]++;
                                
                                // 새로운 객체를 추적 목록에 추가
                                trackedObjects.Add((detectionRect, i));
                            }
                        }

                        // ✅ vehicle_car인 경우 car, bus, truck 모두 매칭
                        if (targetCategories.Contains(detectionName))
                        {
                            candidateCount++;
                            var detectionRect = new Rectangle((int)d.Bounds.X, (int)d.Bounds.Y, (int)d.Bounds.Width, (int)d.Bounds.Height);
                            double iou = ComputeIoU(previousRect, detectionRect);
                            
                            if (iou > maxIouOfCategory)
                                maxIouOfCategory = iou;

                            // ✅ 2. IoU 비교: 가장 많이 겹치는 객체를 찾습니다.
                            if (iou > bestIou && iou > MIN_IOU_THRESHOLD)
                        {
                            bestIou = iou;
                                bestDetection = d;
                            }
                        }
                    }

                    // 가장 일치하는 객체를 찾았으면 해당 객체로 박스를 업데이트합니다.
                    if (bestDetection != null)
                    {
                        previousRect = new Rectangle((int)bestDetection.Bounds.X, (int)bestDetection.Bounds.Y, (int)bestDetection.Bounds.Width, (int)bestDetection.Bounds.Height);
                        
                        successCount++;
                        
                        // ✅ 성공 프레임 기록 (보간용)
                        successfulFrames[i] = previousRect;
                        
                        // ✅ 연속 실패가 끝났는지 체크
                        if (consecutiveFailures > 0)
                        {
                        Debug.WriteLine($"[BOX POSITION] Frame {i}: Previous({previousRect.X},{previousRect.Y}) -> New({bestDetection.Bounds.X},{bestDetection.Bounds.Y})");
                        
                        // ✅ 30프레임 이상 실패했다면 실패 구간 기록
                        if (consecutiveFailures >= FAILURE_THRESHOLD && thresholdFailureStart != -1)
                        {
                            localFailureRanges.Add((thresholdFailureStart, i - 1));
                            thresholdFailureStart = -1;
                        }
                        
                        consecutiveFailures = 0;
                        }
                        
                        trackedBoxes.Add(new BoundingBox
                        {
                            FrameIndex = i,
                            Rectangle = previousRect,
                            Label = fixedLabel, // 라벨과 ID는 고정
                            PersonId = fixedIdPerson,
                            VehicleId = fixedIdVehicle,
                            EventId = fixedIdEvent,
                            Action = startBox.Action,
                            VehicleName = startBox.VehicleName,
                            EventName = startBox.EventName
                        });
                    }
                    else
                    {
                        // ✅ Detection 실패 분석
                        failureCount++;
                        consecutiveFailures++;
                        
                        // 실패 이유 결정
                        if (detections.Count == 0)
                        {
                            lastFailureReason = "탐지된 객체 없음";
                        }
                        else if (candidateCount == 0)
                        {
                            // vehicle_car인 경우 여러 카테고리를 추적 대상으로 설정했으므로 메시지에 표시
                            string targetCategoriesStr = targetCategory == "car" 
                                ? "car/bus/truck" 
                                : targetCategory;
                            lastFailureReason = $"같은 카테고리({targetCategoriesStr}) 없음 - 탐지된 카테고리: {string.Join(", ", detections.Take(3).Select(d => d.Name.ToString()))}";
                        }
                        else
                        {
                            lastFailureReason = $"IoU 임계값(0.3) 미만 - 최대 IoU: {maxIouOfCategory:F2}";
                        }
                        
                        // 실패 구간 시작 체크
                        if (failureStartFrame == -1)
                        {
                            failureStartFrame = i;
                        }
                        
                        // 최대 연속 실패 업데이트
                        if (consecutiveFailures > maxConsecutiveFailures)
                        {
                            maxConsecutiveFailures = consecutiveFailures;
                        }
                        
                        // ✅ 30프레임 연속 실패 시작 체크
                        if (consecutiveFailures == FAILURE_THRESHOLD)
                        {
                            thresholdFailureStart = i - (FAILURE_THRESHOLD - 1);
                        }
                        
                        // ✅ Detection 실패 시 이전 위치 유지 (추적 완료 후 보간 처리)
                        trackedBoxes.Add(new BoundingBox
                    {
                        FrameIndex = i,
                            Rectangle = previousRect, // 이전 프레임의 박스 좌표 그대로 사용
                        Label = fixedLabel,
                        PersonId = fixedIdPerson,
                        VehicleId = fixedIdVehicle,
                        EventId = fixedIdEvent,
                            Action = startBox.Action,
                            VehicleName = startBox.VehicleName,
                            EventName = startBox.EventName
                        });
                        // 추적을 계속 진행 (break 하지 않음)
                    }
                }
                catch (Exception ex)
                {
                    break; // 오류 발생 시 추적 중단
                }
            }

            // ✅ 추적 종료 요약 로그 (카테고리별 탐지 개수)
            try
            {
                var summary = string.Join(", ", detectionCountByCategory
                    .OrderBy(kv => kv.Key)
                    .Select(kv => $"{kv.Key}:{kv.Value}"));
                System.Diagnostics.Debug.WriteLine(
                    $"[Tracking Summary] Frames {startFrame}-{endFrame}, TotalCategories={detectionCountByCategory.Count} -> {summary}");
            }
            catch { /* 로그 실패 무시 */ }

            frame.Dispose();

            try
            {
                if (File.Exists(_tempImagePath))
                    File.Delete(_tempImagePath);
            }
            catch { }

            // ✅ 추적 종료 시 미종료된 실패 구간 처리
            if (consecutiveFailures >= FAILURE_THRESHOLD && thresholdFailureStart != -1)
            {
                localFailureRanges.Add((thresholdFailureStart, endFrame));
            }

            // ✅ YOLO 추적 완료 후 성공 프레임 간 보간으로 실패 프레임 채우기
            if (trackedBoxes.Count > 0 && successfulFrames.Count > 0)
            {
                // 성공 프레임 목록 정렬
                var sortedSuccessFrames = successfulFrames.Keys.OrderBy(f => f).ToList();
                
                // 각 실패 프레임에 대해 보간 적용
                for (int frameIdx = startFrame; frameIdx <= endFrame; frameIdx++)
                {
                    // 이미 성공 프레임이면 건너뛰기
                    if (successfulFrames.ContainsKey(frameIdx))
                        continue;
                    
                    // 해당 프레임의 박스 찾기
                    var box = trackedBoxes.FirstOrDefault(b => b.FrameIndex == frameIdx);
                    if (box == null)
                        continue;
                    
                    // 앞뒤 성공 프레임 찾기
                    int? prevSuccessFrame = null;
                    int? nextSuccessFrame = null;
                    
                    // 이전 성공 프레임 찾기
                    for (int i = sortedSuccessFrames.Count - 1; i >= 0; i--)
                    {
                        if (sortedSuccessFrames[i] < frameIdx)
                        {
                            prevSuccessFrame = sortedSuccessFrames[i];
                            break;
                        }
                    }
                    
                    // 다음 성공 프레임 찾기
                    for (int i = 0; i < sortedSuccessFrames.Count; i++)
                    {
                        if (sortedSuccessFrames[i] > frameIdx)
                        {
                            nextSuccessFrame = sortedSuccessFrames[i];
                            break;
                        }
                    }
                    
                    // 앞뒤 성공 프레임이 모두 있으면 보간 적용
                    if (prevSuccessFrame.HasValue && nextSuccessFrame.HasValue)
                    {
                        var prevRect = successfulFrames[prevSuccessFrame.Value];
                        var nextRect = successfulFrames[nextSuccessFrame.Value];
                        
                        int totalFramesBetween = nextSuccessFrame.Value - prevSuccessFrame.Value;
                        int currentOffset = frameIdx - prevSuccessFrame.Value;
                        
                        // 선형 보간 계산 (위치 및 크기 모두 보간)
                        double ratio = (double)currentOffset / totalFramesBetween;
                        
                        int interpolatedX = (int)(prevRect.X + (nextRect.X - prevRect.X) * ratio);
                        int interpolatedY = (int)(prevRect.Y + (nextRect.Y - prevRect.Y) * ratio);
                        int interpolatedWidth = (int)(prevRect.Width + (nextRect.Width - prevRect.Width) * ratio);
                        int interpolatedHeight = (int)(prevRect.Height + (nextRect.Height - prevRect.Height) * ratio);
                        
                        // 박스 위치 및 크기 업데이트
                        box.Rectangle = new Rectangle(interpolatedX, interpolatedY, interpolatedWidth, interpolatedHeight);
                        
                        inertiaAppliedCount++;
                    }
                    else if (prevSuccessFrame.HasValue)
                    {
                        // 앞 성공 프레임만 있는 경우 (실패 지점부터 exit까지 성공 프레임 없음) - 그 자리에 고정
                        var prevRect = successfulFrames[prevSuccessFrame.Value];
                        box.Rectangle = prevRect; // 이전 위치 및 크기 유지 (고정)
                        // per-frame log suppressed
                    }
                    else if (nextSuccessFrame.HasValue)
                    {
                        // 뒤 성공 프레임만 있는 경우 (시작 부분 실패)
                        var nextRect = successfulFrames[nextSuccessFrame.Value];
                        box.Rectangle = nextRect; // 다음 위치 및 크기로 설정
                        // per-frame log suppressed
                    }
                }
            }

            // ✅ 최종 추적 통계 출력
            double successRate = totalFrames > 0 ? (double)successCount / totalFrames * 100 : 0;
            Debug.WriteLine($"[추적 완료] 성공율: {successRate:F1}%, 성공: {successCount}, 실패: {failureCount}, 관성 보간: {inertiaAppliedCount}개");

            // ✅ 실패 구간을 out 파라미터에 할당
            failureRanges = localFailureRanges;
            return trackedBoxes;
        }

        private static double ComputeIoU(Rectangle a, Rectangle b)
        {
            int x1 = Math.Max(a.Left, b.Left);
            int y1 = Math.Max(a.Top, b.Top);
            int x2 = Math.Min(a.Right, b.Right);
            int y2 = Math.Min(a.Bottom, b.Bottom);

            int interW = Math.Max(0, x2 - x1);
            int interH = Math.Max(0, y2 - y1);
            double inter = interW * interH;

            double areaA = a.Width * a.Height;
            double areaB = b.Width * b.Height;
            double union = areaA + areaB - inter;
            if (union <= 0) return 0;
            return inter / union;
        }

        public void Dispose()
        {
            _predictor?.Dispose();
        }
    }

    public partial class Form1 : Form
    {
        private VideoCapture videoCapture;
        private Mat currentFrame;
        private bool isPlaying = false;
        private int currentFrameIndex = 0;
        private int totalFrames = 0;
        private double fps = 30.0;
        private string currentVideoFile = "";

        private enum DrawMode { None, Select, Draw }
        private DrawMode currentMode = DrawMode.Select;

        // ✅ 추적 상태 플래그 (추적 중에는 다른 키 입력 차단)
        private bool isTrackingInProgress = false;

        private List<BoundingBox> boundingBoxes = new List<BoundingBox>();
        private BoundingBox selectedBox = null;
        private BoundingBox drawingBox = null;
        private System.Drawing.Point drawStartPoint;
        private bool isDrawing = false;
        private bool isDragging = false;
        private System.Drawing.Point dragOffset;

        // ✅ BBox 크기 조정 관련 변수
        private bool isResizing = false;
        private ResizeHandle currentResizeHandle = ResizeHandle.None;
        private System.Drawing.Point resizeStartPoint;
        private Rectangle originalResizeRect;
        private const int MIN_BBOX_SIZE = 10; // 최소 bbox 크기
        private const int HANDLE_SIZE = 8; // 핸들 크기

        private int? entryFrameIndex = null;
        private int? exitFrameIndex = null;

        private List<WaypointMarker> waypointMarkers = new List<WaypointMarker>();
        private WaypointMarker selectedWaypoint = null; // ✅ 선택된 Waypoint 추적
        
        // ✅ 실패 구간 저장 (Key: "Label_ObjectId", Value: List<(startFrame, endFrame)>)
        private Dictionary<string, List<(int start, int end)>> waypointFailureRanges = new Dictionary<string, List<(int, int)>>();
        
        // ✅ Person 속성 저장소
        private PersonAttributeStore personAttributeStore = new PersonAttributeStore();

        // 리스트뷰 MouseDown에서 이미 이동 처리한 경우 Click 핸들러 1회 무시
        private bool suppressWaypointClickOnce = false;
        
        // ✅ 관성 추적 활성화 상태 저장 (Key: "Label_ObjectId", Value: true/false)
        private Dictionary<string, bool> inertiaTrackingEnabled = new Dictionary<string, bool>();
        
        // ✅ 수동으로 수정된 프레임 추적 (Key: "Label_ObjectId", Value: List<수정된 프레임>)
        private Dictionary<string, List<int>> manuallyAdjustedFrames = new Dictionary<string, List<int>>();
        
        // ✅ Shift+E로 설정한 a프레임 저장 (Key: "Label_ObjectId", Value: a프레임)
        private Dictionary<string, int> forcedInertiaTrackingStartFrames = new Dictionary<string, int>();
        
        // ✅ 객체 사라짐 구간 추적 (Key: "Label_ObjectId", Value: List<(시작 프레임, 종료 프레임)>)
        // 사라짐 의도가 기록된 구간 (아직 종료 프레임이 확정되지 않음)
        private Dictionary<string, List<(int startFrame, int? endFrame)>> disappearedRanges = new Dictionary<string, List<(int, int?)>>();
        
        // ✅ 연속 박스 부재 감지용 임계값 (프레임 단위)
        private const int DISAPPEARANCE_THRESHOLD = 5; // 5프레임 이상 연속 부재 시 사라짐으로 간주
        
        private Color[] markerColors = new Color[]
        {
            Color.FromArgb(59, 130, 246),
            Color.FromArgb(16, 185, 129),
            Color.FromArgb(139, 92, 246),
            Color.FromArgb(245, 158, 11),
            Color.FromArgb(236, 72, 153),
        };
        private int currentColorIndex = 0;

        private float timelineProgress = 0.25f;
        private bool isDarkMode = false;

        // JSON 로드 시 프레임 → timestamp 매핑 (images[].timestamp)
        private Dictionary<int, string> frameTimestampMap = new Dictionary<int, string>();

        
        private double playbackSpeed = 1.0;
        private long lastFrameTime = 0;
        private double msPerFrame = 0;
        private bool isTimelineDragging = false;

        // ✅ 창 이동 및 크기 조절 관련
        private bool isMovingWindow = false;
        private System.Drawing.Point windowMoveStartPoint;
        private const int RESIZE_BORDER_WIDTH = 8; // 크기 조절 감지 영역 너비


        private Stack<UndoAction> undoStack = new Stack<UndoAction>();
        private Stack<UndoAction> redoStack = new Stack<UndoAction>();
        private const int MAX_UNDO_STACK = 50;

        private List<CustomLabel> customLabels = new List<CustomLabel>();
        private int customLabelYPosition = 185;

        private List<string> videoFileList = new List<string>();
        private int currentVideoIndex = 0;
        private Form videoListForm = null;
        private ListView videoListView = null;

        private Dictionary<int, CategoryData> categoryMap = new Dictionary<int, CategoryData>();
        private int nextAnnotationId = 1;
        
        // ID 관리 변수 제거 - 사용자가 수동으로 Ctrl+1~14로 지정
        private int currentAssignedId = 1; // 현재 할당할 ID (Ctrl+1~14로 변경됨)

        private TrackingEngine trackingEngine = null;  
        private bool isYoloAvailable = false;
        private string yoloModelPath = Path.Combine(Application.StartupPath, @"..\..\..\..\yolov8n.onnx");

        // SRT 자막 관련
        private string currentSrtFile = "";
        private List<SubtitleEntry> subtitleEntries = new List<SubtitleEntry>();
        private bool isFFmpegAvailable = false;
        private bool isSubtitleVisible = false; // 자막 표시 상태
        
        // ✅ YOLO 탐지 박스 표시 관련
        private bool showYoloDetections = false; // YOLO 탐지 박스 표시 여부
        private Dictionary<int, List<YoloDetectionBox>> yoloDetectionCache = new Dictionary<int, List<YoloDetectionBox>>();
        private CancellationTokenSource yoloDetectionCancellationToken = null;
        private Task yoloDetectionTask = null;
        private readonly object yoloDetectionCacheLock = new object();
        private readonly object yoloDetectionTaskLock = new object(); // ✅ 탐지 작업 동기화용 락 추가
        private const int YOLO_DETECTION_RANGE = 100; // 현재 프레임 기준 앞뒤 탐지 범위
        private System.Threading.Timer detectionDebounceTimer = null; // 프레임 이동 디바운스 타이머
        private const int DETECTION_DEBOUNCE_MS = 300; // 프레임 이동 후 탐지 대기 시간 (ms) - 0.3초
        private int pendingDetectionFrame = -1; // ✅ 대기 중인 탐지 프레임 추적
        private SemaphoreSlim detectionSemaphore = new SemaphoreSlim(1, 1); // ✅ 동시 탐지 작업 제한 (최대 1개)
        private volatile bool isFormDisposed = false; // ✅ Form이 Dispose되었는지 확인
        
        // YOLO 탐지 결과 저장용 클래스
        private class YoloDetectionBox
        {
            public Rectangle Rectangle { get; set; }
            public string Label { get; set; }
            public float Confidence { get; set; }
        }
        
        // 현재 선택된 라벨 (person, vehicle, event)
        private string currentSelectedLabel = "person";
        
        // bbox 리스트 렌더링 최적화를 위한 변수
        private WaypointMarker? lastRenderedWaypoint = null;
        
        // Labels 패널 접기/펼치기 상태
        private bool isPersonExpanded = false;
        private bool isVehicleExpanded = false;
        private bool isEventExpanded = false;
        
        // 성능 최적화: 프레임별 박스 캐시
        private Dictionary<int, List<BoundingBox>> frameBoxCache = new Dictionary<int, List<BoundingBox>>();
        private int lastCachedFrameForPaint = -1;
        private List<BoundingBox> cachedCurrentFrameBoxes = new List<BoundingBox>();
        
        // 성능 최적화: 재사용 가능한 Font 객체
        private Font labelFont = new Font("Segoe UI", 10F, FontStyle.Bold);
        private Font attributeFont = new Font("Segoe UI", 7F, FontStyle.Regular);  // 속성 표시용 작은 폰트
        private Font yoloDetectionFont = new Font("Segoe UI", 8F, FontStyle.Regular);

        // 속성 값 한국어 매핑
        private static readonly Dictionary<string, string> AttributeValueKoreanMap = new Dictionary<string, string>
        {
            // View
            { "Person-Multi", "한명 이상 포함" },
            { "Person-FullyVisible", "대상 인물 전신 전체가 보임" },
            { "Person-PartiallyVisible", "전신 중 일부 가려짐" },
            { "OccludedPart-Head", "머리(전체)가 안보임" },
            { "OccludedPart-UpperBody", "상반신이 안보임(가려짐/잘림)" },
            { "OccludedPart-LowerBody", "하반신이 안보임" },
            { "OccludedPart-Feet", "양발이 다 안보임" },
            { "Occluded-byPerson", "대상 인물이 타인에 의해 가려짐" },
            { "BodyView-Back", "후면 (얼굴이 아닌 전신을 기준으로)" },
            { "BodyView-Front", "전면" },
            { "BodyView-Side", "측면" },
            
            // Biometric
            { "Age-Minor", "미성년자(어린이, 초중고)" },
            { "Age-Adult", "성인" },
            { "Age-Old", "노인" },
            { "Gender-Female", "여자" },
            { "Gender-Male", "남자" },
            { "Height-Short", "키작음(<145cm, 어린이, 초등학생정도)" },
            { "Height-Average", "키보통" },
            { "Height-Tall", "키큼(>180cm)" },
            { "Weight-Underweight", "체격_마름" },
            { "Weight-Average", "체격_보통" },
            { "Weight-Overweight", "체격_과체중(curvy한 체형)" },
            { "BodyPosture-Stooped", "등이 굽은 체형" },
            { "Face-Recognizable", "안면 인식이 가능한 정도" },
            
            // Head/Hair
            { "HairLength-Bald", "대머리(부분 대머리 포함)" },
            { "HairLength-Short", "짧은 머리" },
            { "HairLength-Medium", "단발 머리(어깨선 정도 길이)" },
            { "HairLength-Long", "긴 머리(어깨선 이하로)" },
            { "HairStyle-Ponytail", "묶은 머리형태" },
            { "HairColor-Dark", "Black, brown" },
            { "HairColor-Light", "Grey, white(흰머리)" },
            { "HairColor-Colored", "Red, Gold" },
            
            // UpperCloth
            { "Upper-Type-Tshirt", "긴팔/반팔 티셔츠, 캐주얼 폴로티, 민소매티" },
            { "Upper-Type-Shirt", "셔츠(카라, 버튼다운), 블라우스" },
            { "Upper-Type-Sweater", "니트 스웨터, 가디건, 맨투맨 스웻셔츠, 후드 스웻" },
            { "Upper-Type-Jacket", "캐주얼 겉옷(잠바, 트렌치코트, 봄버, 가죽자켓 등)" },
            { "Upper-Type-Blazer", "양복자켓, 콤비자켓 등" },
            { "Upper-Type-LongCoat", "허벅지 중간보다 긴 길이의 겉옷" },
            { "Upper-Type-Dress", "원피스" },
            { "Upper-Sleeve-Sleeveless", "민소매" },
            { "Upper-Sleeve-Short", "반팔 소매" },
            { "Upper-Sleeve-Long", "긴 소매" },
            { "Upper-Pattern-Solid", "무늬 없는 단색" },
            { "Upper-Pattern-Logo", "로고(브랜드 로고, 글자로고, 중앙/단일 그래픽, 캐릭터 등)" },
            { "Upper-Pattern-Plaid", "체크 무늬" },
            { "Upper-Pattern-Stripe", "줄 무늬(가로, 세로, 사선)" },
            { "Upper-Pattern-Splice", "배색 무늬(color-block)" },
            { "Upper-Pattern-Graphics", "상의전체 반복 패턴(폴카닷, 꽃무늬, 기하학 반복 무늬)" },
            { "Upper-Color-Black", "검정" },
            { "Upper-Color-Blue", "파랑" },
            { "Upper-Color-Brown", "갈색" },
            { "Upper-Color-Green", "초록" },
            { "Upper-Color-Grey", "회색" },
            { "Upper-Color-Orange", "주황" },
            { "Upper-Color-Pink", "분홍" },
            { "Upper-Color-Purple", "보라" },
            { "Upper-Color-Red", "빨강" },
            { "Upper-Color-White", "흰색" },
            { "Upper-Color-Yellow", "노랑" },
            
            // LowerCloth
            { "Lower-Type-Pants", "하의유형_바지" },
            { "Lower-Type-Skirt", "하의유형_치마" },
            { "Lower-Legwear-Tights", "하의_타이즈/레깅스 착용" },
            { "Lower-Length-Short", "하의길이_무릅 기준" },
            { "Lower-Length-MidCalf", "하의길이_정강이 중간 기준" },
            { "Lower-Length-Full", "하의길이_발목 기준" },
            { "Lower-Pattern-Solid", "하의무늬_단색(무늬 없음)" },
            { "Lower-Pattern-Plaid", "하의무늬_체크" },
            { "Lower-Pattern-Stripe", "하의무늬_줄무늬(가로, 세로, 사선 줄이 한 개 이상)" },
            { "Lower-Pattern-Graphics", "하의무늬_하의전체 반복(점, 꽃무늬, 군복위장무늬 등)" },
            { "Lower-Color-Black", "검정" },
            { "Lower-Color-Blue", "파랑" },
            { "Lower-Color-Brown", "갈색" },
            { "Lower-Color-Green", "초록" },
            { "Lower-Color-Grey", "회색" },
            { "Lower-Color-Pink", "분홍" },
            { "Lower-Color-Purple", "보라" },
            { "Lower-Color-Red", "빨강" },
            { "Lower-Color-White", "흰색" },
            { "Lower-Color-Yellow", "노랑" },
            { "Lower-Material-Denim", "데님소재(청바지, 청치마)" },
            
            // Footwear
            { "Footwear-Type-Boots", "부츠(발목 위~무릅까지 커버)" },
            { "Footwear-Type-Flats", "발등이 노출되는 구조의 신발" },
            { "Footwear-Type-Formal", "구두(가죽소재), 신사화, 여성용힐" },
            { "Footwear-Type-Sandals", "발가락, 뒷꿈치가 노출되는 구조의 실발(슬리퍼 포함)" },
            { "Footwear-Type-Sneakers", "운동화" },
            { "Footwear-Color-Black", "검정" },
            { "Footwear-Color-Brown", "갈색류" },
            { "Footwear-Color-White", "흰색" },
            
            // Accessory
            { "Headwear-Hat", "모자" },
            { "Headwear-Halmet", "헬맷(딱딱한 소재, 오토바이/자전거 헬맷)" },
            { "Headwear-Other", "다른 형태의 머리 전체를 커버하는 악세서리" },
            { "Facewear-Glasses", "안경착용" },
            { "Facewear-Sunglasses", "썬글라스착용" },
            { "Facewear-Mask", "마스크 착용" },
            { "Bag-Backpack", "백팩" },
            { "Bag-Handbag", "leather, plastic, paper bags worn by hands" },
            { "Bag-ShoulderBag", "한쪽 어깨에 걸치는 형태의 가방(메신저, 크로스백 등)" },
            { "Bag-Suitcase", "바퀴 달린 형태의 가방(여행용 캐리어, 쇼핑카트)" },
            { "Carrying-Phone", "휴대폰 소지" },
            { "Carrying-Umbrella", "우산(펼친 우산, 접은 우산) 소지" },
            { "Carrying-Drink", "음료수 컵, 생수병 등 소지" },
            { "Carrying-Box", "박스 소지" },
            { "Carrying-Stick", "지팡이, 등산스틱, 목발 등 소지" },
            { "HandsOccupied", "한손 또는 양손에 물건(가방, 소지품) 소지(빈손이 아님)" },
            
            // Action
            { "Standing", "서있음" },
            { "Walking", "걷고 있음" },
            { "Running", "뛰고 있음" },
            { "Riding", "타고 있음(자전거, 오토바이, 퀵보드 등)" },
            { "Sitting", "앉아 있음(모빌리티 제외한 의자, 고정형 구조물에)" },
            { "Pulling", "끌고 있음(유모차, 자전거, 카트, 캐리어 등)" }
        };

        // 영문 값을 한국어로 변환
        public static string GetAttributeValueKorean(string englishValue)
        {
            if (string.IsNullOrEmpty(englishValue))
                return englishValue;
            
            return AttributeValueKoreanMap.TryGetValue(englishValue, out string korean) ? korean : englishValue;
        }

        // 카테고리 ID 매핑 (스펙에 따른 고정 매핑)
        private static readonly Dictionary<string, int> CategoryIdMap = new Dictionary<string, int>
        {
            // Person categories (1~20)
            {"person_01", 1}, {"person_02", 2}, {"person_03", 3}, {"person_04", 4},
            {"person_05", 5}, {"person_06", 6}, {"person_07", 7}, {"person_08", 8},
            {"person_09", 9}, {"person_10", 10}, {"person_11", 11}, {"person_12", 12},
            {"person_13", 13}, {"person_14", 14}, {"person_15", 15}, {"person_16", 16},
            {"person_17", 17}, {"person_18", 18}, {"person_19", 19}, {"person_20", 20},
            
            // Vehicle categories (21~24)
            {"car", 21}, {"motorcycle", 22}, {"e_scooter", 23}, {"bicycle", 24},
            
            // Event categories (25~28)
            {"contact", 25}, {"exchange", 26}, {"board", 27}, {"final_exchange", 28}
        };


        public Form1()
        {
            InitializeComponent();
            UpdateBoxCount();
            
            // 자막 초기 상태를 닫힌 상태로 설정
            btnToggleSubtitle.Text = "자막 열기";
            btnToggleSubtitle.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
            if (labelSubtitleTimestamp != null)
            {
                labelSubtitleTimestamp.Visible = false;
            }

            // ✅ Timeline 패널에 더블 버퍼링 활성화 (깜빡임 방지)
            EnableDoubleBuffering(panelTimeline);

            // ✅ 헤더 드래그로 창 이동 기능 활성화
            SetupWindowDragHandlers();
        }

        /// <summary>
        /// Panel에 더블 버퍼링을 활성화하는 헬퍼 메서드
        /// </summary>
        private void EnableDoubleBuffering(Control control)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic,
                null, control, new object[] { true });
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Maximized;
            ApplyTheme();
            btnSelectFolder.Text = "파일 선택";
            
            // Form이 키 이벤트를 받을 수 있도록 포커스 설정
            this.Focus();
            this.Activate();

            // ✅ ListView에서 스페이스바가 Form1_KeyDown으로 전달되도록 KeyDown 설정
            listViewPersonWaypoints.KeyDown += (s, ev) => HandleListViewKeyDown(s, ev);
            listViewVehicleWaypoints.KeyDown += (s, ev) => HandleListViewKeyDown(s, ev);
            listViewEventWaypoints.KeyDown += (s, ev) => HandleListViewKeyDown(s, ev);

            // YOLO 모델 초기화 시도
            InitializeYoloModel();

            // FFmpeg 경로 설정
            SetupFFmpegPath();

            // ✅ 창 상태 변경 시 최대화/복원 버튼 아이콘 업데이트
            this.Resize += Form1_Resize;
            UpdateMaximizeButtonIcon();
        }

        // ✅ ListView에서 키 이벤트를 Form1로 전달하는 핸들러
        private void HandleListViewKeyDown(object sender, KeyEventArgs e)
        {
            // 스페이스바인 경우 Form1_KeyDown으로 전달
            if (e.KeyCode == Keys.Space)
            {
                // Form1_KeyDown을 직접 호출
                Form1_KeyDown(this, e);
                // Form1_KeyDown에서 처리했다면 ListView가 처리하지 않도록
                if (e.Handled)
                {
                    return;
                }
            }
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            UpdateMaximizeButtonIcon();
        }


        private void SetupFFmpegPath()
        {
            try
            {
                // FFmpeg가 시스템 PATH에 있는지 확인
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "ffmpeg",
                            Arguments = "-version",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    process.WaitForExit(3000); // 3초 타임아웃

                    if (process.ExitCode == 0)
                    {
                        // FFmpeg가 PATH에 있음
                        GlobalFFOptions.Configure(new FFOptions { BinaryFolder = "" });
                        isFFmpegAvailable = true;
                        return;
                    }
                }
                catch
                {
                    // PATH에서 FFmpeg를 찾지 못함 - 로컬 폴더에서 찾기
                }

                // 로컬 폴더에서 FFmpeg 찾기
                string localFFmpegPath = Path.Combine(Application.StartupPath, "ffmpeg");
                string ffmpegExe = Path.Combine(localFFmpegPath, "ffmpeg.exe");
                
                if (File.Exists(ffmpegExe))
                {
                    GlobalFFOptions.Configure(new FFOptions { BinaryFolder = localFFmpegPath });
                    isFFmpegAvailable = true;
                }
                else
                {
                    // FFmpeg를 찾을 수 없음 (조용히 처리, 외부 SRT 파일은 정상 작동)
                    isFFmpegAvailable = false;
                }
            }
            catch (Exception ex)
            {
                // FFmpeg 설정 중 오류 발생 (조용히 처리, 외부 SRT 파일은 정상 작동)
                isFFmpegAvailable = false;
            }
        }

        private void InitializeYoloModel()
        {
            try
            {
                if (!File.Exists(yoloModelPath))
                {
                    MessageBox.Show(
                        $"YOLO 모델을 찾을 수 없습니다: {yoloModelPath}\n\n" +
                        "yolov8n.onnx 파일을 실행 파일과 같은 폴더에 배치해주세요.",
                        "경고",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    isYoloAvailable = false;
                    return; // Application.Exit() 대신 return으로 변경
                }

                trackingEngine = new YoloTrackingEngine(yoloModelPath);
                isYoloAvailable = true;

                MessageBox.Show(
                    "YOLO 모델이 성공적으로 로드되었습니다.\n" +
                    "Ctrl+T로 추적을 시작할 때 YOLO 모드를 선택할 수 있습니다.",
                    "정보",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                string fullErrorDetails = $"에러 메시지: {ex.Message}\n\n스택 트레이스:\n{ex.StackTrace}";

                var missingDlls = CudaEnvironmentHelper.GetMissingCudaDependencies();
                if (missingDlls.Count > 0)
                {
                    errorMessage += "\n\n[누락된 CUDA DLL]\n - " + string.Join("\n - ", missingDlls);
                    errorMessage += "\n\nMicrosoft.ML.OnnxRuntime.Gpu 1.22.1은 CUDA 12.x(예: 12.3/12.4)와 cuDNN 9.x 런타임 DLL을 요구합니다. " +
                                    "NVIDIA CUDA Toolkit 12.x와 cuDNN 9.x를 설치한 뒤, 설치 경로의 bin 폴더를 PATH에 추가하거나 실행 폴더에 DLL을 복사하세요.";
                }
                
                // ✅ CUDA 관련 에러 감지 및 상세 정보 제공
                if (ex.Message.Contains("CUDA") || ex.Message.Contains("cuda") || 
                    ex.Message.Contains("GPU") || ex.Message.Contains("gpu") ||
                    ex.InnerException != null && (ex.InnerException.Message.Contains("CUDA") || 
                                                   ex.InnerException.Message.Contains("cuda")))
                {
                    errorMessage += "\n\n[CUDA 관련 에러 해결 방법]\n\n" +
                                  "1. NVIDIA 드라이버 확인:\n" +
                                  "   - nvidia-smi 명령어로 GPU 인식 여부 확인\n" +
                                  "   - 최신 드라이버 설치 권장\n\n" +
                                  "2. CUDA Toolkit 확인:\n" +
                                  "   - YoloSharp.Gpu 6.0.6은 일반적으로 CUDA 11.x 또는 12.x 필요\n" +
                                  "   - 시스템에 설치된 CUDA 버전 확인\n\n" +
                                  "3. cuDNN 확인:\n" +
                                  "   - CUDA 버전에 맞는 cuDNN 설치 필요\n" +
                                  "   - 환경 변수 PATH에 cuDNN 경로 추가\n\n" +
                                  "4. 환경 변수 확인:\n" +
                                  "   - CUDA_PATH 환경 변수 설정 확인\n" +
                                  "   - PATH에 CUDA bin 폴더 경로 포함 확인\n\n" +
                                  "5. 대안:\n" +
                                  "   - CPU 모드로 작동 (YoloSharp.Gpu 대신 YoloSharp 사용)\n" +
                                  "   - 또는 YOLO 기능 없이 계속 진행";
                    
                    // 내부 예외 정보도 포함
                    if (ex.InnerException != null)
                    {
                        fullErrorDetails += $"\n\n내부 예외:\n{ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
                    }
                }
                
                if (errorMessage.Contains("Opset 22"))
                {
                    errorMessage += "\n\n해결 방법:\n" +
                                  "1. YOLOv8 모델을 Opset 21로 다시 변환하세요\n" +
                                  "2. Python: model.export(format='onnx', opset=21)\n" +
                                  "3. 또는 YOLO 기능 없이 계속 진행하세요";
                }
                
                // 디버그 출력에 전체 에러 정보 기록
                System.Diagnostics.Debug.WriteLine($"[YOLO 초기화 실패] {fullErrorDetails}");
                
                MessageBox.Show(
                        $"YOLO 모델 로딩중 에러:\n\n{errorMessage}\n\n" +
                        "YOLO 기능 없이 계속 진행합니다.",
                        "경고",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                isYoloAvailable = false;
                // Application.Exit() 제거하여 프로그램이 계속 실행되도록 함
            }
        }


        #region Window Controls
        private void btnClose_Click(object sender, EventArgs e)
        {
            try
            {
                // ✅ 종료 시 YOLO 탐지 작업 중지
                System.Diagnostics.Debug.WriteLine("[애플리케이션 종료] YOLO 탐지 중지");
                StopYoloDetection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[애플리케이션 종료] YOLO 탐지 중지 오류: {ex.Message}");
                // 종료는 계속 진행
            }
            finally
            {
                this.Close();
            }
        }
        private void btnMaximize_Click(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            else
            {
                this.WindowState = FormWindowState.Maximized;
            }
        }

        private void btnMinimize_Click(object sender, EventArgs e) => this.WindowState = FormWindowState.Minimized;

        /// <summary>
        /// 최대화/복원 버튼 아이콘 업데이트
        /// </summary>
        private void UpdateMaximizeButtonIcon()
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                btnMaximize.Text = "❐"; // 복원 아이콘
            }
            else
            {
                btnMaximize.Text = "□"; // 최대화 아이콘
            }
        }

        private async void btnExportJson_Click(object sender, EventArgs e)
        {
            try
            {
                // ✅ YOLO 추적/탐지 중에는 저장 버튼 차단
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[저장 버튼 차단] YOLO 추적/탐지 중이므로 저장 불가");
                    MessageBox.Show(
                        "YOLO 추적 또는 탐지가 진행 중입니다.\n작업이 완료될 때까지 기다려주세요.",
                        "작업 중",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                
                if (string.IsNullOrEmpty(currentVideoFile))
                {
                    MessageBox.Show("먼저 비디오 파일을 로드해주세요.", "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

            if (boundingBoxes.Count == 0)
            {
                MessageBox.Show("저장할 라벨링 데이터가 없습니다.", "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 저장 중 폼 생성
            Form loadingForm = new Form
            {
                Width = 300,
                Height = 120,
                Text = "JSON 저장",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                TopMost = true
            };

            Label loadingLabel = new Label
            {
                Text = "저장 중...",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(100, 30)
            };

            loadingForm.Controls.Add(loadingLabel);
            loadingForm.Show();
            loadingForm.Refresh();

            try
            {
                // 이벤트 전파는 Exit 확정 시 완료되므로, 저장 시 별도 전파 작업을 수행하지 않습니다.
                InvalidateBoxCache();
                UpdateBoxCount();

                
                // 비동기로 저장 작업 수행
                await Task.Run(() => SaveCurrentLabelingData());
                
                loadingForm.Close();

                // ✅ JSON 저장 후 자동 재로드
                string videoDir = Path.GetDirectoryName(currentVideoFile);
                string labelsDir = Path.Combine(videoDir, "labels");
                string fileName = Path.GetFileNameWithoutExtension(currentVideoFile) + "_labels.json";
                string jsonFilePath = Path.Combine(labelsDir, fileName);
                
                if (File.Exists(jsonFilePath))
                {
                    await LoadLabelingData(currentVideoFile); // JSON 재로드
                }
                
                MessageBox.Show(
                    $"JSON 파일이 저장되었습니다.\n\n" +
                    $"위치: {labelsDir}\n" +
                    $"박스 개수: {boundingBoxes.Count}개",
                    "저장 완료",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    loadingForm.Close();
                    MessageBox.Show($"JSON 저장 중 오류가 발생했습니다:\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception outerEx)
            {
                System.Diagnostics.Debug.WriteLine($"[JSON 저장 버튼 오류] {outerEx.Message}\n{outerEx.StackTrace}");
                MessageBox.Show(
                    $"JSON 저장 중 외부 오류 발생:\n{outerEx.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        #endregion

        #region Theme
        private void btnTheme_Click(object sender, EventArgs e)
        {
            isDarkMode = !isDarkMode;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            Color bgColor = isDarkMode ? Color.FromArgb(17, 24, 39) : Color.FromArgb(243, 244, 246);
            Color surfaceColor = isDarkMode ? Color.FromArgb(31, 41, 55) : Color.White;
            Color textColor = isDarkMode ? Color.FromArgb(249, 250, 251) : Color.FromArgb(31, 41, 55);

            panelMainContainer.BackColor = bgColor;
            panelHeader.BackColor = surfaceColor;
            panelLeftSidebar.BackColor = surfaceColor;
            panelCenter.BackColor = bgColor;
            panelVideoControls.BackColor = surfaceColor;
            panelRightSidebar.BackColor = surfaceColor;

            labelTitle.ForeColor = textColor;
            labelBoxCount.ForeColor = textColor;
            labelTimeInfo.ForeColor = textColor;
        }
        #endregion

        #region Video Loading
        private async void btnSelectFolder_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Video Files|*.avi;*.mp4;*.mkv|All Files|*.*";
                ofd.Title = "Select Video Files";
                ofd.Multiselect = true;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    // 비디오 전환 시 자동 저장 제거 - 수동으로만 저장

                    videoFileList.Clear();
                    videoFileList.AddRange(ofd.FileNames);
                    currentVideoIndex = 0;

                    if (videoFileList.Count > 0)
                    {
                        await LoadVideoWithSubtitle(videoFileList[0]);
                    }
                }
            }
        }

        private async void btnSelectFolderPath_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select Folder containing Video Files";

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    // 비디오 전환 시 자동 저장 제거 - 수동으로만 저장

                    videoFileList.Clear();
                    string[] videoExtensions = { "*.avi", "*.mp4", "*.mkv", "*.mov", "*.flv", "*.wmv" };

                    foreach (string extension in videoExtensions)
                    {
                        string[] files = Directory.GetFiles(fbd.SelectedPath, extension, SearchOption.AllDirectories);
                        videoFileList.AddRange(files);
                    }

                    videoFileList.Sort();

                    if (videoFileList.Count == 0)
                    {
                        MessageBox.Show("선택한 폴더에서 영상 파일을 찾을 수 없습니다.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                // ✅ 폴더 선택 시: 데이터 백업 → 초기화 → 로드 (실패 시 복원)
                var backupBoxes = new List<BoundingBox>(boundingBoxes);
                var backupWaypoints = new List<WaypointMarker>(waypointMarkers);
                var backupSelectedBox = selectedBox;
                var backupUndoStack = new Stack<UndoAction>(undoStack.Reverse());
                var backupRedoStack = new Stack<UndoAction>(redoStack.Reverse());
                
                    boundingBoxes.Clear();
                    waypointMarkers.Clear();
                    selectedBox = null;
                undoStack.Clear();
                redoStack.Clear();
                lastRenderedWaypoint = null;
                
                try
                {
                    currentVideoIndex = 0;
                    await LoadVideoWithSubtitle(videoFileList[0]);
                    
                    // LoadVideoWithSubtitle 내부의 LoadLabelingData에서 새 데이터가 로드됨
                    UpdateBoxCount();
                    UpdateWaypointListView();
                    pictureBoxVideo.Invalidate();

                    MessageBox.Show($"총 {videoFileList.Count}개의 영상 파일을 불러왔습니다.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    // ❌ 로드 실패 시 이전 데이터 복원
                    boundingBoxes.Clear();
                    boundingBoxes.AddRange(backupBoxes);
                    waypointMarkers.Clear();
                    waypointMarkers.AddRange(backupWaypoints);
                    selectedBox = backupSelectedBox;
                    undoStack.Clear();
                    foreach (var action in backupUndoStack) undoStack.Push(action);
                    redoStack.Clear();
                    foreach (var action in backupRedoStack) redoStack.Push(action);
                    
                    UpdateBoxCount();
                    UpdateWaypointListView();
                    pictureBoxVideo.Invalidate();
                    
                    MessageBox.Show($"영상 로드 실패:\n{ex.Message}\n\n이전 작업 내용이 복원되었습니다.", 
                        "폴더 로드 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                }
            }
        }

        private async Task LoadVideoWithSubtitle(string filePath)
        {
            try
            {
                // 기존 비디오 로드
                await LoadVideo(filePath);

                // 1. 먼저 외부 SRT 파일이 있는지 확인
                string videoDir = Path.GetDirectoryName(filePath);
                string videoName = Path.GetFileNameWithoutExtension(filePath);
                string externalSrtPath = Path.Combine(videoDir, $"{videoName}.srt");

                if (File.Exists(externalSrtPath))
                {
                    // 외부 SRT 파일이 있으면 바로 로드
                    currentSrtFile = externalSrtPath;
                    await LoadSrtFile(externalSrtPath);
                    return;
                }

                // 2. 외부 SRT 파일이 없으면 비디오 내부에서 자막 추출 시도
                await ExtractSrtFromVideo(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"비디오 로드 중 오류가 발생했습니다:\n{ex.Message}", 
                    "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadVideo(string filePath)
        {
            try
            {
                // ✅ 비디오 로드 시 YOLO 탐지 캐시 초기화 및 탐지 중지
                try
                {
                    System.Diagnostics.Debug.WriteLine("[비디오 로드] YOLO 탐지 캐시 초기화 시작");
                    StopYoloDetection();
                    
                    // 디바운스 타이머도 확실히 정리
                    detectionDebounceTimer?.Dispose();
                    detectionDebounceTimer = null;
                    
                    lock (yoloDetectionCacheLock)
                    {
                        int cacheCount = yoloDetectionCache.Count;
                        yoloDetectionCache.Clear();
                        System.Diagnostics.Debug.WriteLine($"[비디오 로드] YOLO 탐지 캐시 초기화 완료 (기존 캐시: {cacheCount}개)");
                    }
                    showYoloDetections = false;
                    if (btnToggleYoloDetections != null)
                    {
                        btnToggleYoloDetections.Text = "YOLO 탐지";
                        btnToggleYoloDetections.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
                    }
                }
                catch (Exception yoloEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[비디오 로드] YOLO 탐지 초기화 오류: {yoloEx.Message}");
                    // 오류가 있어도 비디오 로드는 계속 진행
                }
                
                if (videoCapture != null)
                {
                    videoCapture.Release();
                    videoCapture.Dispose();
                }

                videoCapture = new VideoCapture(filePath);

                if (!videoCapture.IsOpened())
                {
                    MessageBox.Show("Failed to open video file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                currentVideoFile = filePath;
                totalFrames = (int)videoCapture.Get(VideoCaptureProperties.FrameCount);
                fps = videoCapture.Get(VideoCaptureProperties.Fps);
                currentFrameIndex = 0;


                LoadFrame(0);
                UpdateTimeLabels();

                labelTitle.Text = $"Form_AllDay - {Path.GetFileName(filePath)}";

                // Form이 키 이벤트를 받을 수 있도록 포커스 설정
                this.Focus();
                this.Activate();

                // 동일 파일명의 JSON 자동 로드
                await LoadLabelingData(filePath);
                
                // ✅ 영상 로드 후 자동 재생 시작
                if (!isPlaying)
                {
                    btnPlay_Click(null, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading video: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadFrame(int frameIndex)
        {
            try
            {
                // ✅ YOLO 추적/탐지 중에는 프레임 이동 차단 (중요!)
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine($"[프레임 이동 차단] YOLO 추적/탐지 중이므로 프레임 {frameIndex}로 이동 불가");
                    return;
                }
                
                // ✅ 프레임이 실제로 이동하는지 확인
                bool frameChanged = (currentFrameIndex != frameIndex);
                
                // ✅ YOLO 탐지 토글이 ON이고 프레임이 이동하면 자동으로 OFF로 변경
                if (showYoloDetections && frameChanged)
                {
                    System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 토글] 프레임 이동 감지 ({currentFrameIndex} -> {frameIndex}), 탐지 토글 자동 OFF");
                    showYoloDetections = false;
                    StopYoloDetection();
                    
                    // 디바운스 타이머도 정리
                    detectionDebounceTimer?.Dispose();
                    detectionDebounceTimer = null;
                    
                    // 캐시 정리
                    lock (yoloDetectionCacheLock)
                    {
                        yoloDetectionCache.Clear();
                    }
                    
                    // UI 업데이트
                    if (btnToggleYoloDetections != null)
                    {
                        btnToggleYoloDetections.Text = "YOLO 탐지";
                        btnToggleYoloDetections.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
                    }
                    pictureBoxVideo?.Invalidate();
                }
                
                if (videoCapture == null || !videoCapture.IsOpened())
                    return;

                if (frameIndex < 0 || frameIndex >= totalFrames)
                    return;

                videoCapture.Set(VideoCaptureProperties.PosFrames, frameIndex);

            if (currentFrame != null)
                currentFrame.Dispose();

            currentFrame = new Mat();
            videoCapture.Read(currentFrame);

            if (!currentFrame.Empty())
            {
                pictureBoxVideo.Image?.Dispose();
                pictureBoxVideo.Image = BitmapConverter.ToBitmap(currentFrame);
            }

            currentFrameIndex = frameIndex;
            
            // ✅ YOLO 탐지 토글이 ON일 경우 프레임 이동 시 자동으로 탐지 수행 (300ms 디바운싱)
            // (프레임이 이동하지 않았거나 이미 OFF로 변경되었을 수 있으므로 재확인)
            if (showYoloDetections && isYoloAvailable)
            {
                try
                {
                    lock (yoloDetectionCacheLock)
                    {
                        // 현재 프레임의 탐지 결과가 있으면 UI만 업데이트
                        if (yoloDetectionCache.ContainsKey(frameIndex))
                        {
                            pictureBoxVideo?.Invalidate();
                        }
                        else
                        {
                            // 탐지 결과가 없으면 디바운스 타이머로 지연 탐지
                            // ✅ 기존 타이머 안전하게 취소
                            System.Threading.Timer oldTimer = detectionDebounceTimer;
                            detectionDebounceTimer = null;
                            if (oldTimer != null)
                            {
                                try
                                {
                                    oldTimer.Change(Timeout.Infinite, Timeout.Infinite); // 타이머 중지
                                    oldTimer.Dispose();
                                }
                                catch (Exception timerDisposeEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 기존 타이머 해제 오류: {timerDisposeEx.Message}");
                                }
                            }
                            
                            // 새 타이머 시작 (디바운싱 - 300ms)
                            int targetFrame = frameIndex; // 프레임 인덱스 캡처
                            pendingDetectionFrame = targetFrame;
                            
                            // ✅ 타이머 콜백에서는 Task.Run으로 비동기 실행 (async void 방지)
                            detectionDebounceTimer = new System.Threading.Timer((state) =>
                            {
                                int checkFrame = targetFrame;
                                
                                // ✅ Task.Run으로 비동기 실행 (예외 처리 가능)
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        // ✅ 빠른 체크 (락 없이)
                                        if (checkFrame != currentFrameIndex || checkFrame != pendingDetectionFrame)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 디바운스 타이머: 프레임 {checkFrame} 탐지 취소 (프레임 변경됨)");
                                            return;
                                        }
                                        
                                        // ✅ 캐시 확인 (락 사용)
                                        bool needsDetection = false;
                                        lock (yoloDetectionCacheLock)
                                        {
                                            needsDetection = !yoloDetectionCache.ContainsKey(checkFrame);
                                        }
                                        
                                        if (!needsDetection)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 디바운스 타이머: 프레임 {checkFrame}은 이미 캐시에 있음");
                                            return;
                                        }
                                        
                                        // ✅ 비동기로 탐지 시작
                                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 디바운스(300ms) 후 탐지: 프레임 {checkFrame}");
                                        pendingDetectionFrame = -1; // 대기 프레임 초기화
                                        await DetectCurrentFrameOnlyAsync(); // ✅ 비동기 버전 사용
                                    }
                                    catch (Exception timerEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 디바운스 타이머 오류: {timerEx.Message}\n{timerEx.StackTrace}");
                                    }
                                });
                            }, null, DETECTION_DEBOUNCE_MS, Timeout.Infinite);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 프레임 이동 시 탐지 오류: {ex.Message}\n{ex.StackTrace}");
                    // 오류 시 계속 진행
                }
            }
            
            // ✅ 프레임 전환 시 선택 박스 재바인딩 또는 해제
            if (selectedBox != null && selectedBox.FrameIndex != frameIndex)
            {
                var selLabel = selectedBox.Label;
                int selId = GetBoxId(selectedBox);
                var rebound = boundingBoxes.FirstOrDefault(b => b.FrameIndex == frameIndex && b.Label == selLabel && GetBoxId(b) == selId && !b.IsDeleted);
                if (rebound != null)
                {
                    selectedBox = rebound;
                    HighlightSelectedBoxInSidebar();
                }
                else
                {
                    selectedBox = null;
                    ClearSidebarHighlights();
                }
            }
            UpdateTimeLabels();
            
            // Waypoint entry 프레임에서만 bbox 리스트 업데이트 (리소스 최적화)
            if (ShouldUpdateBboxList(frameIndex))
            {
                UpdateBboxListDisplay();
            }
            
            pictureBoxVideo.Invalidate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[프레임 로드 오류] {ex.Message}\n{ex.StackTrace}");
                // 오류 발생 시 현재 프레임 인덱스는 유지
            }
        }

        private void UpdateTimeLabels()
        {
            if (videoCapture == null || !videoCapture.IsOpened())
                return;

            double currentSeconds = currentFrameIndex / fps;
            double totalSeconds = totalFrames / fps;

            TimeSpan currentTime = TimeSpan.FromSeconds(currentSeconds);
            TimeSpan totalTime = TimeSpan.FromSeconds(totalSeconds);

            string speedText = playbackSpeed == 1.0 ? "" : $" ({playbackSpeed}x)";
            string subtitleText = isSubtitleVisible ? GetCurrentSubtitle() : "";
            
            // 자막이 표시 상태일 때만 타임스탬프 추출하여 우측 하단에 표시
            if (isSubtitleVisible)
            {
                string timestamp = ExtractTimestampFromSubtitle(subtitleText);
                if (!string.IsNullOrEmpty(timestamp))
                {
                    labelSubtitleTimestamp.Text = timestamp;
                    labelSubtitleTimestamp.Visible = true;
                }
                else
                {
                    labelSubtitleTimestamp.Visible = false;
                }
            }
            else
            {
                labelSubtitleTimestamp.Visible = false;
            }
            
            // x264 대신 실제 재생 속도(1.0x, 2.0x 등)로 표기
            string speedInfo = $"{playbackSpeed:0.##}x";
            if (!string.IsNullOrEmpty(subtitleText))
            {
                labelTimeInfo.Text = $"{currentTime:hh\\:mm\\:ss} / {totalTime:hh\\:mm\\:ss} {speedInfo}\n자막: {subtitleText}";
            }
            else
            {
                labelTimeInfo.Text = $"{currentTime:hh\\:mm\\:ss} / {totalTime:hh\\:mm\\:ss} {speedInfo}";
            }

            timelineProgress = totalFrames > 0 ? (float)currentFrameIndex / totalFrames : 0;
            panelTimeline.Invalidate();
        }
        #endregion

        #region Video Playback
        private void btnPlay_Click(object sender, EventArgs e)
        {
            try
            {
                // ✅ YOLO 추적/탐지 중에는 재생 버튼 차단
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[재생 버튼 차단] YOLO 추적/탐지 중이므로 재생 불가");
                    MessageBox.Show(
                        "YOLO 추적 또는 탐지가 진행 중입니다.\n작업이 완료될 때까지 기다려주세요.",
                        "작업 중",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                
                if (videoCapture == null || !videoCapture.IsOpened())
                {
                    MessageBox.Show("비디오 파일이 로드되지 않았습니다.\n먼저 파일을 선택해주세요.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

            isPlaying = !isPlaying;

            if (isPlaying)
            {
                // ✅ 재생 시 YOLO 탐지 토글 자동으로 꺼기
                if (showYoloDetections)
                {
                    System.Diagnostics.Debug.WriteLine("[재생 버튼] 재생 시작 시 YOLO 탐지 토글 자동 해제");
                    showYoloDetections = false;
                    StopYoloDetection();
                    lock (yoloDetectionCacheLock)
                    {
                        yoloDetectionCache.Clear();
                    }
                    if (btnToggleYoloDetections != null)
                    {
                        btnToggleYoloDetections.Text = "YOLO 탐지";
                        btnToggleYoloDetections.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
                    }
                    pictureBoxVideo?.Invalidate();
                }
                
                btnPlay.Text = "⏸";
                lastFrameTime = DateTime.Now.Ticks / 10000;
                msPerFrame = 1000.0 / fps;

                if (msPerFrame <= 0)
                {
                    MessageBox.Show($"FPS 오류: {fps}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    isPlaying = false;
                    btnPlay.Text = "▶";
                    return;
                }

                timerPlayback.Interval = 33;
                timerPlayback.Start();
            }
            else
            {
                btnPlay.Text = "▶";
                timerPlayback.Stop();
            }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[재생 버튼 오류] {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"재생 중 오류 발생:\n{ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                // 상태 복원
                isPlaying = false;
                if (btnPlay != null)
                {
                    btnPlay.Text = "▶";
                }
                timerPlayback?.Stop();
            }
        }

        private void timerPlayback_Tick(object sender, EventArgs e)
        {
            long currentTime = DateTime.Now.Ticks / 10000;
            long elapsedMs = currentTime - lastFrameTime;

            int framesToMove = (int)(elapsedMs * playbackSpeed / msPerFrame);

            if (framesToMove > 0)
            {
                int nextFrame = Math.Min(totalFrames - 1, currentFrameIndex + framesToMove);
                LoadFrame(nextFrame);
                lastFrameTime = currentTime;

                if (nextFrame >= totalFrames - 1)
                {
                    isPlaying = false;
                    btnPlay.Text = "▶";
                    timerPlayback.Stop();
                    playbackSpeed = 1.0;
                }
            }
        }

        private void btnRewind_Click(object sender, EventArgs e)
        {
            try
            {
                // ✅ YOLO 추적/탐지 중에는 5초 이동 차단 (중요!)
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[5초 이동 차단] YOLO 추적/탐지 중이므로 5초 뒤로 이동 불가");
                    MessageBox.Show(
                        "YOLO 추적 또는 탐지가 진행 중입니다.\n작업이 완료될 때까지 기다려주세요.",
                        "작업 중",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                
                int framesToMove = (int)(fps * 5);
                int newFrame = Math.Max(0, currentFrameIndex - framesToMove);
                LoadFrame(newFrame);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[5초 뒤로 이동 오류] {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"프레임 이동 중 오류 발생:\n{ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void btnForward_Click(object sender, EventArgs e)
        {
            try
            {
                // ✅ YOLO 추적/탐지 중에는 5초 이동 차단 (중요!)
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[5초 이동 차단] YOLO 추적/탐지 중이므로 5초 앞으로 이동 불가");
                    MessageBox.Show(
                        "YOLO 추적 또는 탐지가 진행 중입니다.\n작업이 완료될 때까지 기다려주세요.",
                        "작업 중",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                
                int framesToMove = (int)(fps * 5);
                int newFrame = Math.Min(totalFrames - 1, currentFrameIndex + framesToMove);
                LoadFrame(newFrame);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[5초 앞으로 이동 오류] {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"프레임 이동 중 오류 발생:\n{ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        #endregion

        #region Entry/Exit Markers
        private void btnEntry_Click(object sender, EventArgs e)
        {
            // E키와 동일한 기능: Entry 마커 설정
            SetEntryMarker();
        }

        private void SetEntryMarker()
        {
            // 객체 선택 없이도 Entry 프레임 설정 가능
            entryFrameIndex = currentFrameIndex;
            TimeSpan entryTime = TimeSpan.FromSeconds(currentFrameIndex / fps);
            btnEntry.Text = $"Entry: {entryTime:hh\\:mm\\:ss}";
            panelTimeline.Invalidate();
        }

        private void btnToggleSubtitle_Click(object sender, EventArgs e)
        {
            isSubtitleVisible = !isSubtitleVisible;
            btnToggleSubtitle.Text = isSubtitleVisible ? "자막 닫기" : "자막 열기";
            btnToggleSubtitle.BackColor = isSubtitleVisible 
                ? System.Drawing.Color.FromArgb(239, 68, 68) // 빨강 (닫기)
                : System.Drawing.Color.FromArgb(100, 116, 139); // 회색 (열기)
            
            // 자막 레이블 표시/숨기기
            if (labelSubtitleTimestamp != null)
            {
                labelSubtitleTimestamp.Visible = isSubtitleVisible;
            }
            
            // 시간 정보 업데이트하여 자막 텍스트 반영
            UpdateTimeLabels();
        }

        // ✅ YOLO 탐지 박스 토글 버튼 클릭 핸들러
        private void btnToggleYoloDetections_Click(object sender, EventArgs e)
        {
            try
            {
                if (btnToggleYoloDetections == null)
                {
                    System.Diagnostics.Debug.WriteLine("[YOLO 탐지 토글 오류] btnToggleYoloDetections가 null입니다.");
                    return;
                }
                
                showYoloDetections = !showYoloDetections;
                System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 토글] showYoloDetections = {showYoloDetections}");
                
                btnToggleYoloDetections.Text = showYoloDetections ? "YOLO 숨기기" : "YOLO 탐지";
                btnToggleYoloDetections.BackColor = showYoloDetections
                    ? System.Drawing.Color.FromArgb(34, 197, 94) // 녹색 (표시 중)
                    : System.Drawing.Color.FromArgb(100, 116, 139); // 회색 (숨김)
                
                if (showYoloDetections)
                {
                    // ✅ 재생 중이면 일시정지
                    if (isPlaying)
                    {
                        System.Diagnostics.Debug.WriteLine("[YOLO 탐지 토글] 재생 중이므로 일시정지");
                        isPlaying = false;
                        btnPlay.Text = "▶";
                        timerPlayback.Stop();
                    }
                    
                    // ✅ 현재 프레임만 탐지
                    if (isYoloAvailable && videoCapture != null && videoCapture.IsOpened())
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 토글] 현재 프레임({currentFrameIndex})만 탐지 시작");
                        DetectCurrentFrameOnly();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 토글] YOLO 사용 불가 - isYoloAvailable: {isYoloAvailable}, videoCapture: {(videoCapture != null ? "not null" : "null")}, IsOpened: {(videoCapture != null && videoCapture.IsOpened() ? "true" : "false")}");
                        MessageBox.Show(
                            "YOLO 모델을 사용할 수 없습니다.\n" +
                            "비디오가 로드되어 있는지 확인해주세요.",
                            "YOLO 탐지 불가",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        showYoloDetections = false;
                        if (btnToggleYoloDetections != null)
                        {
                            btnToggleYoloDetections.Text = "YOLO 탐지";
                            btnToggleYoloDetections.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
                        }
                    }
                }
                else
                {
                    // ✅ YOLO 탐지 중지
                    System.Diagnostics.Debug.WriteLine("[YOLO 탐지 토글] YOLO 탐지 중지");
                    StopYoloDetection();
                }
                
                if (pictureBoxVideo != null)
                {
                    pictureBoxVideo.Invalidate();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 토글 오류] {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"YOLO 탐지 토글 중 오류 발생:\n{ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                // 상태 복원
                showYoloDetections = false;
                if (btnToggleYoloDetections != null)
                {
                    try
                    {
                        btnToggleYoloDetections.Text = "YOLO 탐지";
                        btnToggleYoloDetections.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
                    }
                    catch (Exception restoreEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 토글] 상태 복원 오류: {restoreEx.Message}");
                    }
                }
            }
        }

        // ✅ 현재 프레임만 YOLO 탐지 수행 (동기 래퍼)
        private void DetectCurrentFrameOnly()
        {
            _ = DetectCurrentFrameOnlyAsync(); // 비동기로 실행 (fire-and-forget)
        }
        
        // ✅ 비동기 버전 (실제 작업 수행)
        private async Task DetectCurrentFrameOnlyAsync()
        {
            // ✅ Semaphore로 동시 실행 제한 (최대 1개, 블로킹 없이)
            if (!await detectionSemaphore.WaitAsync(0))
            {
                System.Diagnostics.Debug.WriteLine("[YOLO 탐지] 이미 탐지 작업이 진행 중이므로 스킵");
                return;
            }
            
            try
            {
                int targetFrame;
                bool shouldReturn = false;
                
                // ✅ 빠른 체크 (최소한의 락)
                lock (yoloDetectionTaskLock)
                {
                    targetFrame = currentFrameIndex;
                    
                    if (string.IsNullOrEmpty(currentVideoFile) || !File.Exists(currentVideoFile))
                    {
                        System.Diagnostics.Debug.WriteLine("[YOLO 탐지] 비디오 파일이 없거나 유효하지 않음");
                        shouldReturn = true;
                    }
                    
                    if (!shouldReturn && (videoCapture == null || !videoCapture.IsOpened()))
                    {
                        System.Diagnostics.Debug.WriteLine("[YOLO 탐지] 비디오 캡처가 열려있지 않음");
                        shouldReturn = true;
                    }
                }
                
                if (shouldReturn)
                    return;
                
                // ✅ 캐시 확인
                lock (yoloDetectionCacheLock)
                {
                    if (yoloDetectionCache.ContainsKey(targetFrame))
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 프레임 {targetFrame}은 이미 캐시에 있음");
                        SafeInvoke(() => pictureBoxVideo?.Invalidate());
                        return;
                    }
                }
                
                // ✅ 기존 작업 비동기 취소 (블로킹 없이)
                if (yoloDetectionTask != null && !yoloDetectionTask.IsCompleted)
                {
                    System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 기존 탐지 작업 취소 중... (프레임 {targetFrame} 탐지 대기)");
                    StopYoloDetection();
                    
                    // ✅ 비동기로 대기 (블로킹 없음)
                    try
                    {
                        await yoloDetectionTask.ContinueWith(t => { }, TaskContinuationOptions.OnlyOnRanToCompletion);
                    }
                    catch (Exception waitEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 기존 작업 대기 오류: {waitEx.Message}");
                    }
                }
                
                // ✅ 다시 한번 확인 (취소 대기 중에 프레임이 변경되었을 수 있음)
                if (targetFrame != currentFrameIndex)
                {
                    System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 프레임이 변경됨 ({targetFrame} -> {currentFrameIndex}), 탐지 취소");
                    return;
                }
                
                // ✅ 다시 캐시 확인
                lock (yoloDetectionCacheLock)
                {
                    if (yoloDetectionCache.ContainsKey(targetFrame))
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 프레임 {targetFrame}은 이미 캐시에 있음 (대기 중 추가됨)");
                        SafeInvoke(() => pictureBoxVideo?.Invalidate());
                        return;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 현재 프레임({targetFrame})만 탐지 시작");
                
                yoloDetectionCancellationToken = new CancellationTokenSource();
                var token = yoloDetectionCancellationToken.Token;
                
                // ✅ Task.Run으로 실행 (비동기 작업)
                yoloDetectionTask = Task.Run(async () =>
                    {
                        YoloPredictor predictor = null;
                        Mat frame = null;
                        string tempImagePath = null;
                        
                        try
                        {
                            // ✅ 취소 토큰 확인
                            token.ThrowIfCancellationRequested();
                            
                            // ✅ 프레임이 변경되었는지 확인
                            if (targetFrame != currentFrameIndex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 작업 시작 시 프레임이 변경됨 ({targetFrame} -> {currentFrameIndex})");
                                return;
                            }
                            
                            // YOLO Predictor 생성
                            try
                            {
                                System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 모델 로드 시작: {yoloModelPath}");
                                predictor = new YoloPredictor(yoloModelPath);
                                System.Diagnostics.Debug.WriteLine("[YOLO 탐지] 모델 로드 완료");
                            }
                            catch (Exception ex)
                            {
                                string errorDetails = $"[YOLO 탐지 오류] 모델 로드 실패: {ex.Message}\n{ex.StackTrace}";
                                if (ex.InnerException != null)
                                {
                                    errorDetails += $"\n\n내부 예외: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
                                }
                                System.Diagnostics.Debug.WriteLine(errorDetails);
                                
                                // CUDA 관련 에러인지 확인
                                string errorMessage = ex.Message;
                                if (ex.Message.Contains("CUDA") || ex.Message.Contains("cuda") || 
                                    ex.Message.Contains("GPU") || ex.Message.Contains("gpu") ||
                                    ex.InnerException != null && (ex.InnerException.Message.Contains("CUDA") || 
                                                                   ex.InnerException.Message.Contains("cuda")))
                                {
                                    errorMessage += "\n\n[CUDA 관련 에러]\n" +
                                                  "NVIDIA 드라이버, CUDA Toolkit, cuDNN 버전을 확인하세요.\n" +
                                                  "자세한 해결 방법은 프로그램 시작 시 표시된 에러 메시지를 참조하세요.";
                                }
                                
                                SafeInvoke(() =>
                                {
                                    MessageBox.Show(
                                        $"YOLO 모델 로드 실패:\n\n{errorMessage}",
                                        "YOLO 오류",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                                });
                                return;
                            }
                            
                            // ✅ 취소 토큰 재확인
                            token.ThrowIfCancellationRequested();
                            
                            tempImagePath = Path.Combine(Path.GetTempPath(), $"yolo_detection_frame_{targetFrame}_{DateTime.Now.Ticks}.jpg");
                            frame = new Mat();
                            
                            try
                            {
                                // ✅ 프레임 읽기 전 취소 확인
                                token.ThrowIfCancellationRequested();
                                
                                // 현재 프레임 읽기
                                lock (yoloDetectionTaskLock)
                                {
                                    if (videoCapture == null || !videoCapture.IsOpened())
                                    {
                                        System.Diagnostics.Debug.WriteLine("[YOLO 탐지] 비디오 캡처가 닫힘");
                                        return;
                                    }
                                    videoCapture.Set(VideoCaptureProperties.PosFrames, targetFrame);
                                    if (!videoCapture.Read(frame) || frame.Empty())
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 프레임 {targetFrame} 읽기 실패");
                                        return;
                                    }
                                }
                                
                                // ✅ 프레임이 여전히 유효한지 확인
                                if (targetFrame != currentFrameIndex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 프레임 읽기 후 변경 확인 ({targetFrame} -> {currentFrameIndex})");
                                    return;
                                }
                                
                                token.ThrowIfCancellationRequested();
                                
                                // Mat을 임시 파일로 저장
                                if (!Cv2.ImWrite(tempImagePath, frame))
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 프레임 {targetFrame} 임시 파일 저장 실패");
                                    return;
                                }
                                
                                token.ThrowIfCancellationRequested();
                                
                                // ✅ 프레임이 여전히 유효한지 재확인
                                if (targetFrame != currentFrameIndex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 탐지 전 프레임 변경 확인 ({targetFrame} -> {currentFrameIndex})");
                                    return;
                                }
                                
                                // YOLO 탐지 수행
                                var detections = predictor.Detect(tempImagePath);
                                
                                token.ThrowIfCancellationRequested();
                                
                                // ✅ 최종 프레임 확인
                                if (targetFrame != currentFrameIndex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 탐지 후 프레임 변경 확인 ({targetFrame} -> {currentFrameIndex})");
                                    return;
                                }
                                
                                // 탐지 결과를 YoloDetectionBox 리스트로 변환
                                var detectionBoxes = new List<YoloDetectionBox>();
                                foreach (var d in detections)
                                {
                                    try
                                    {
                                        // YOLO 라이브러리가 "0: 'person'" 형식의 이름을 반환하므로 파싱
                                        string rawDetectionName = d?.Name?.ToString() ?? "";
                                        string detectionName = rawDetectionName;
                                        
                                        if (!string.IsNullOrEmpty(rawDetectionName))
                                        {
                                            int firstQuote = rawDetectionName.IndexOf('\'');
                                            int lastQuote = rawDetectionName.LastIndexOf('\'');
                                            if (firstQuote != -1 && lastQuote > firstQuote)
                                            {
                                                detectionName = rawDetectionName.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                                            }
                                        }
                                        
                                        if (!string.IsNullOrEmpty(detectionName) && d != null)
                                        {
                                            detectionBoxes.Add(new YoloDetectionBox
                                            {
                                                Rectangle = new Rectangle(
                                                    (int)d.Bounds.X,
                                                    (int)d.Bounds.Y,
                                                    Math.Max(1, (int)d.Bounds.Width),
                                                    Math.Max(1, (int)d.Bounds.Height)
                                                ),
                                                Label = detectionName,
                                                Confidence = d.Confidence
                                            });
                                        }
                                    }
                                    catch (Exception detEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 탐지 객체 변환 오류: {detEx.Message}");
                                    }
                                }
                                
                                System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 완료] 프레임 {targetFrame}: {detectionBoxes.Count}개 객체 탐지");
                                
                                // ✅ 최종 프레임 확인 후 캐시 저장
                                if (targetFrame == currentFrameIndex)
                                {
                                    lock (yoloDetectionCacheLock)
                                    {
                                        // 한 번 더 확인 (다른 스레드에서 이미 캐시에 추가했을 수 있음)
                                        if (!yoloDetectionCache.ContainsKey(targetFrame))
                                        {
                                            yoloDetectionCache[targetFrame] = detectionBoxes;
                                        }
                                    }
                                    
                                    // ✅ UI 업데이트 (프레임이 여전히 유효할 때만, SafeInvoke 사용)
                                    SafeInvoke(() => pictureBoxVideo?.Invalidate());
                                }
                            }
                            finally
                                {
                                    // 리소스 정리
                                    try
                                    {
                                        frame?.Dispose();
                                        
                                        if (!string.IsNullOrEmpty(tempImagePath) && File.Exists(tempImagePath))
                                        {
                                            try
                                            {
                                                File.Delete(tempImagePath);
                                            }
                                            catch (Exception delEx)
                                            {
                                                System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 임시 파일 삭제 오류: {delEx.Message}");
                                            }
                                        }
                                    }
                                    catch (Exception cleanupEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 리소스 정리 오류: {cleanupEx.Message}");
                                    }
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                // ✅ 취소는 정상적인 상황이므로 로그만 남김
                                System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 프레임 {targetFrame} 탐지 취소됨");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 오류] 백그라운드 작업 전체 오류: {ex.Message}\n{ex.StackTrace}");
                                // ✅ UI 스레드에서만 메시지 박스 표시 (취소 오류 제외, SafeInvoke 사용)
                                if (!(ex is OperationCanceledException))
                                {
                                    SafeInvoke(() =>
                                    {
                                        MessageBox.Show(
                                            $"YOLO 탐지 중 오류 발생:\n{ex.Message}",
                                            "YOLO 탐지 오류",
                                            MessageBoxButtons.OK,
                                            MessageBoxIcon.Error);
                                    });
                                }
                            }
                            finally
                            {
                                // Predictor 정리
                                try
                                {
                                    predictor?.Dispose();
                                    predictor = null;
                                }
                                catch (Exception predEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] Predictor 정리 오류: {predEx.Message}");
                                }
                            }
                        }, token);
            }
            finally
            {
                // ✅ Semaphore 해제 (항상 해제)
                detectionSemaphore.Release();
            }
        }
        
        // ✅ 안전한 Invoke 헬퍼 (Form이 Dispose되지 않았는지 확인)
        private void SafeInvoke(Action action)
        {
            try
            {
                if (isFormDisposed || this.IsDisposed || this.Disposing)
                    return;
                    
                if (this.InvokeRequired)
                {
                    this.Invoke((MethodInvoker)(() =>
                    {
                        try
                        {
                            if (!isFormDisposed && !this.IsDisposed && !this.Disposing)
                                action?.Invoke();
                        }
                        catch (ObjectDisposedException)
                        {
                            // Form이 Dispose된 경우 무시
                        }
                        catch (InvalidOperationException)
                        {
                            // 핸들이 없는 경우 무시
                        }
                    }));
                }
                else
                {
                    if (!isFormDisposed && !this.IsDisposed && !this.Disposing)
                        action?.Invoke();
                }
            }
            catch (ObjectDisposedException)
            {
                // Form이 Dispose된 경우 무시
            }
            catch (InvalidOperationException)
            {
                // 핸들이 없는 경우 무시
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SafeInvoke 오류] {ex.Message}");
            }
        }

        // ✅ 백그라운드에서 YOLO 탐지 수행 (범위 탐지) - 현재 사용 안 함
        private void StartYoloDetectionInBackground()
        {
            try
            {
                // 기존 탐지 작업이 있으면 중지
                StopYoloDetection();
                
                if (string.IsNullOrEmpty(currentVideoFile) || !File.Exists(currentVideoFile))
                {
                    System.Diagnostics.Debug.WriteLine("[YOLO 탐지 시작] 비디오 파일이 없거나 유효하지 않음");
                    return;
                }
                
                yoloDetectionCancellationToken = new CancellationTokenSource();
                var token = yoloDetectionCancellationToken.Token;
                
                // 현재 프레임 기준 앞뒤 범위 계산
                int startFrame = Math.Max(0, currentFrameIndex - YOLO_DETECTION_RANGE);
                int endFrame = Math.Min(totalFrames - 1, currentFrameIndex + YOLO_DETECTION_RANGE);
                
                System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 시작] 프레임 범위: {startFrame} ~ {endFrame} (현재: {currentFrameIndex}, 총 프레임: {totalFrames})");
                
                yoloDetectionTask = Task.Run(async () =>
                {
                    YoloPredictor predictor = null;
                    VideoCapture videoCaptureCopy = null;
                    Mat frame = null;
                    string tempImagePath = null;
                    
                    try
                    {
                        // YOLO Predictor 생성 (추적 엔진과 별도)
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 모델 로드 시작: {yoloModelPath}");
                            predictor = new YoloPredictor(yoloModelPath);
                            System.Diagnostics.Debug.WriteLine("[YOLO 탐지] 모델 로드 완료");
                        }
                        catch (Exception ex)
                        {
                            string errorDetails = $"[YOLO 탐지 오류] 모델 로드 실패: {ex.Message}\n{ex.StackTrace}";
                            if (ex.InnerException != null)
                            {
                                errorDetails += $"\n\n내부 예외: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
                            }
                            System.Diagnostics.Debug.WriteLine(errorDetails);
                            
                            // CUDA 관련 에러인지 확인
                            string errorMessage = ex.Message;
                            if (ex.Message.Contains("CUDA") || ex.Message.Contains("cuda") || 
                                ex.Message.Contains("GPU") || ex.Message.Contains("gpu") ||
                                ex.InnerException != null && (ex.InnerException.Message.Contains("CUDA") || 
                                                               ex.InnerException.Message.Contains("cuda")))
                            {
                                errorMessage += "\n\n[CUDA 관련 에러]\n" +
                                              "NVIDIA 드라이버, CUDA Toolkit, cuDNN 버전을 확인하세요.\n" +
                                              "자세한 해결 방법은 프로그램 시작 시 표시된 에러 메시지를 참조하세요.";
                            }
                            
                            this.Invoke((MethodInvoker)(() =>
                            {
                                MessageBox.Show(
                                    $"YOLO 모델 로드 실패:\n\n{errorMessage}",
                                    "YOLO 오류",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                            }));
                            return;
                        }
                        
                        tempImagePath = Path.Combine(Path.GetTempPath(), "yolo_detection_frame.jpg");
                        frame = new Mat();
                        
                        try
                        {
                            videoCaptureCopy = new VideoCapture(currentVideoFile);
                            
                            if (!videoCaptureCopy.IsOpened())
                            {
                                System.Diagnostics.Debug.WriteLine("[YOLO 탐지 오류] 비디오 파일 열기 실패");
                                predictor?.Dispose();
                                return;
                            }
                            
                            System.Diagnostics.Debug.WriteLine("[YOLO 탐지] 비디오 파일 열기 완료");
                            
                            int processedFrames = 0;
                            int detectedObjectsTotal = 0;
                            
                            // 범위 내 프레임들을 순차적으로 탐지
                            for (int frameIdx = startFrame; frameIdx <= endFrame; frameIdx++)
                            {
                                if (token.IsCancellationRequested)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 취소됨 - 프레임 {frameIdx}에서 중단");
                                    break;
                                }
                                
                                try
                                {
                                    videoCaptureCopy.Set(VideoCaptureProperties.PosFrames, frameIdx);
                                    if (!videoCaptureCopy.Read(frame) || frame.Empty())
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 프레임 {frameIdx} 읽기 실패");
                                        continue;
                                    }
                                    
                                    try
                                    {
                                        // Mat을 임시 파일로 저장
                                        if (!Cv2.ImWrite(tempImagePath, frame))
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 프레임 {frameIdx} 임시 파일 저장 실패");
                                            continue;
                                        }
                                        
                                        // YOLO 탐지 수행
                                        var detections = predictor.Detect(tempImagePath);
                                        
                                        // 탐지 결과를 YoloDetectionBox 리스트로 변환
                                        var detectionBoxes = new List<YoloDetectionBox>();
                                        foreach (var d in detections)
                                        {
                                            try
                                            {
                                                // YOLO 라이브러리가 "0: 'person'" 형식의 이름을 반환하므로 파싱
                                                string rawDetectionName = d?.Name?.ToString() ?? "";
                                                string detectionName = rawDetectionName;
                                                
                                                if (!string.IsNullOrEmpty(rawDetectionName))
                                                {
                                                    int firstQuote = rawDetectionName.IndexOf('\'');
                                                    int lastQuote = rawDetectionName.LastIndexOf('\'');
                                                    if (firstQuote != -1 && lastQuote > firstQuote)
                                                    {
                                                        detectionName = rawDetectionName.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                                                    }
                                                }
                                                
                                                if (!string.IsNullOrEmpty(detectionName) && d != null)
                                                {
                                                    detectionBoxes.Add(new YoloDetectionBox
                                                    {
                                                        Rectangle = new Rectangle(
                                                            (int)d.Bounds.X,
                                                            (int)d.Bounds.Y,
                                                            Math.Max(1, (int)d.Bounds.Width),
                                                            Math.Max(1, (int)d.Bounds.Height)
                                                        ),
                                                        Label = detectionName,
                                                        Confidence = d.Confidence
                                                    });
                                                }
                                            }
                                            catch (Exception detEx)
                                            {
                                                System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 탐지 객체 변환 오류 (프레임 {frameIdx}): {detEx.Message}");
                                            }
                                        }
                                        
                                        detectedObjectsTotal += detectionBoxes.Count;
                                        
                                        // 캐시에 저장
                                        lock (yoloDetectionCacheLock)
                                        {
                                            yoloDetectionCache[frameIdx] = detectionBoxes;
                                        }
                                        
                                        processedFrames++;
                                        
                                        // 10프레임마다 진행 상황 로그
                                        if (processedFrames % 10 == 0)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 진행] {processedFrames}/{endFrame - startFrame + 1} 프레임 처리됨 (탐지 객체: {detectedObjectsTotal}개)");
                                        }
                                        
                                        // 현재 프레임이면 UI 업데이트
                                        if (frameIdx == currentFrameIndex)
                                        {
                                            this.Invoke((MethodInvoker)(() =>
                                            {
                                                try
                                                {
                                                    pictureBoxVideo?.Invalidate();
                                                }
                                                catch (Exception invEx)
                                                {
                                                    System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] UI 업데이트 오류: {invEx.Message}");
                                                }
                                            }));
                                        }
                                    }
                                    catch (Exception frameEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 오류] 프레임 {frameIdx} 처리 중 오류: {frameEx.Message}\n{frameEx.StackTrace}");
                                        // 개별 프레임 오류는 계속 진행
                                    }
                                    
                                    // 백그라운드 작업이므로 CPU 부하를 줄이기 위해 약간의 지연
                                    await Task.Delay(10, token);
                                }
                                catch (Exception readEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 오류] 프레임 {frameIdx} 읽기 오류: {readEx.Message}");
                                    continue;
                                }
                            }
                            
                            System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 완료] 총 {processedFrames} 프레임 처리, {detectedObjectsTotal}개 객체 탐지");
                        }
                        finally
                        {
                            // 리소스 정리
                            try
                            {
                                videoCaptureCopy?.Release();
                                videoCaptureCopy?.Dispose();
                                frame?.Dispose();
                                
                                if (!string.IsNullOrEmpty(tempImagePath) && File.Exists(tempImagePath))
                                {
                                    try
                                    {
                                        File.Delete(tempImagePath);
                                    }
                                    catch (Exception delEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 임시 파일 삭제 오류: {delEx.Message}");
                                    }
                                }
                            }
                            catch (Exception cleanupEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 리소스 정리 오류: {cleanupEx.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 오류] 백그라운드 작업 전체 오류: {ex.Message}\n{ex.StackTrace}");
                        this.Invoke((MethodInvoker)(() =>
                        {
                            try
                            {
                                MessageBox.Show(
                                    $"YOLO 탐지 중 오류 발생:\n{ex.Message}",
                                    "YOLO 탐지 오류",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                            }
                        catch (Exception msgEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 메시지 박스 표시 오류: {msgEx.Message}");
                        }
                        }));
                    }
                    finally
                    {
                        // Predictor 정리
                        try
                        {
                            predictor?.Dispose();
                        }
                        catch (Exception predEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] Predictor 정리 오류: {predEx.Message}");
                        }
                    }
                }, token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 시작 오류] {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"YOLO 탐지 시작 중 오류 발생:\n{ex.Message}",
                    "YOLO 탐지 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ✅ YOLO 탐지 중지
        private void StopYoloDetection()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[YOLO 탐지 중지] 탐지 작업 중지 시작");
                
                // 디바운스 타이머 취소
                detectionDebounceTimer?.Dispose();
                detectionDebounceTimer = null;
                
                if (yoloDetectionCancellationToken != null)
                {
                    try
                    {
                        yoloDetectionCancellationToken.Cancel();
                        System.Diagnostics.Debug.WriteLine("[YOLO 탐지 중지] 취소 토큰 신호 전송");
                    }
                    catch (Exception cancelEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 중지] 취소 토큰 오류: {cancelEx.Message}");
                    }
                    finally
                    {
                        try
                        {
                            yoloDetectionCancellationToken.Dispose();
                        }
                        catch (Exception dispEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 중지] 취소 토큰 정리 오류: {dispEx.Message}");
                        }
                        yoloDetectionCancellationToken = null;
                    }
                }
                
                if (yoloDetectionTask != null)
                {
                    try
                    {
                        if (!yoloDetectionTask.IsCompleted)
                        {
                            System.Diagnostics.Debug.WriteLine("[YOLO 탐지 중지] 작업 완료 대기 중 (최대 1초)");
                            bool completed = yoloDetectionTask.Wait(1000); // 1초 대기
                            if (!completed)
                            {
                                System.Diagnostics.Debug.WriteLine("[YOLO 탐지 중지] 작업이 1초 내에 완료되지 않음 - 강제 종료");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[YOLO 탐지 중지] 작업 완료됨");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[YOLO 탐지 중지] 작업이 이미 완료됨");
                        }
                    }
                    catch (Exception waitEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 중지] 작업 대기 오류: {waitEx.Message}");
                    }
                    finally
                    {
                        yoloDetectionTask = null;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("[YOLO 탐지 중지] 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 중지 오류] {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async void btnExit_Click(object sender, EventArgs e)
        {
            try
            {
                // ✅ YOLO 추적/탐지 중에는 Exit 버튼 차단
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[Exit 버튼 차단] YOLO 추적/탐지 중이므로 Exit 불가");
                    MessageBox.Show(
                        "YOLO 추적 또는 탐지가 진행 중입니다.\n작업이 완료될 때까지 기다려주세요.",
                        "작업 중",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                
                await SetExitMarkerAndCreateWaypoint();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Exit 버튼 오류] {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"Exit 설정 중 오류 발생:\n{ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async Task SetExitMarkerAndCreateWaypoint()
        {
            try
            {
                // ✅ EntryFrame이 설정되어 있지 않지만, 선택된 박스가 기존 waypoint에 속하는 경우
            if (!entryFrameIndex.HasValue && selectedBox != null)
            {
                var existingWaypoint = FindWaypointForBox(selectedBox);
                if (existingWaypoint != null)
                {
                    // 기존 waypoint의 ExitFrame만 갱신
                    if (currentFrameIndex <= existingWaypoint.EntryFrame)
                    {
                        TimeSpan currentTimeCheck = TimeSpan.FromSeconds(currentFrameIndex / fps);
                        TimeSpan entryTimeCheck = TimeSpan.FromSeconds(existingWaypoint.EntryFrame / fps);
                        
                        MessageBox.Show(
                            $"Exit 프레임은 Entry 프레임보다 뒤에 있어야 합니다.\n\n" +
                            $"Entry: {entryTimeCheck:hh\\:mm\\:ss} (프레임 {existingWaypoint.EntryFrame})\n" +
                            $"현재: {currentTimeCheck:hh\\:mm\\:ss} (프레임 {currentFrameIndex})\n\n" +
                            $"Entry 프레임 이후로 이동한 후 Exit를 설정해주세요.",
                            "Warning",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }
                    
                    // 기존 ExitFrame 저장
                    int oldExitFrame = existingWaypoint.ExitFrame;
                    
                    // ✅ ExitFrame이 늘어난 경우 차단 (기존 ExitFrame보다 뒤로 확장 불가)
                    if (currentFrameIndex > oldExitFrame)
                    {
                        TimeSpan currentTimeCheck = TimeSpan.FromSeconds(currentFrameIndex / fps);
                        TimeSpan oldExitTimeCheck = TimeSpan.FromSeconds(oldExitFrame / fps);
                        
                        MessageBox.Show(
                            $"ExitFrame을 기존 ExitFrame보다 뒤로 연장할 수 없습니다.\n\n" +
                            $"기존 Exit: {oldExitTimeCheck:hh\\:mm\\:ss} (프레임 {oldExitFrame})\n" +
                            $"현재: {currentTimeCheck:hh\\:mm\\:ss} (프레임 {currentFrameIndex})\n\n" +
                            $"ExitFrame은 기존 ExitFrame보다 앞이거나 같아야 합니다.",
                            "Warning",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }
                    
                    bool exitFrameShortened = currentFrameIndex < oldExitFrame; // ExitFrame이 짧아진 경우
                    
                    // ✅ ExitFrame이 짧아진 경우 (새로운 ExitFrame이 기존보다 작음)
                    if (exitFrameShortened)
                    {
                        // 새로운 ExitFrame 이후부터 기존 ExitFrame까지의 해당 ID 박스 삭제
                        int boxId = GetBoxId(selectedBox);
                        var boxesToDelete = boundingBoxes
                            .Where(b => 
                                b.Label == selectedBox.Label &&
                                GetBoxId(b) == boxId &&
                                b.FrameIndex > currentFrameIndex &&
                                b.FrameIndex <= oldExitFrame &&
                                !b.IsDeleted)
                            .ToList();
                        
                        if (boxesToDelete.Count > 0)
                        {
                            foreach (var box in boxesToDelete)
                            {
                                AddUndoAction(new UndoAction { Type = UndoActionType.RemoveBox, Box = CloneBoundingBox(box) });
                                boundingBoxes.Remove(box);
                            }
                            
                            InvalidateBoxCache();
                            UpdateBoxCount();
                            UpdateBboxListDisplay();
                            
                            System.Diagnostics.Debug.WriteLine($"[ExitFrame 갱신] {boxesToDelete.Count}개 박스 삭제됨 (프레임 {currentFrameIndex + 1}~{oldExitFrame})");
                        }
                    }
                    
                    // ExitFrame 갱신
                    existingWaypoint.ExitFrame = currentFrameIndex;
                    TimeSpan exitTime = TimeSpan.FromSeconds(currentFrameIndex / fps);
                    existingWaypoint.ExitTime = exitTime.ToString(@"hh\:mm\:ss");
                    
                    // Event 박스 전파 (Event인 경우)
                    if (selectedBox.Label == "event")
                    {
                        var eventBoxes = boundingBoxes
                            .Where(b => b.Label == "event" &&
                                       b.EventId == selectedBox.EventId &&
                                       b.FrameIndex >= existingWaypoint.EntryFrame &&
                                       b.FrameIndex <= existingWaypoint.ExitFrame)
                            .ToList();
                        
                        if (eventBoxes.Count > 0)
                        {
                            var entryEventBox = eventBoxes.OrderBy(b => b.FrameIndex).First();
                            PropagateEventBoxWithinRange(entryEventBox, existingWaypoint.ExitFrame);
                        }
                    }
                    
                    // JSON 저장
                    SaveCurrentLabelingData();
                    
                    // UI 업데이트
                    UpdateWaypointListView();
                    btnExit.Text = "Exit";
                    panelTimeline.Invalidate();
                    
                    // 추적 진행 여부 확인
                    var result = MessageBox.Show(
                        $"기존 Waypoint의 Exit 프레임이 갱신되었습니다.\n\n" +
                        $"객체: {GetCategoryName(selectedBox.Label, GetBoxId(selectedBox))}\n" +
                        $"Entry: {existingWaypoint.EntryTime} (프레임 {existingWaypoint.EntryFrame})\n" +
                        $"Exit: {existingWaypoint.ExitTime} (프레임 {existingWaypoint.ExitFrame})\n\n" +
                        $"추적을 수행하시겠습니까?",
                        "Exit 갱신 완료",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    
                    if (result == DialogResult.Yes)
                    {
                        await PerformTrackingForWaypointAsync(existingWaypoint);
                    }
                    
                    return;
                }
            }
            
            if (entryFrameIndex.HasValue)
            {
                // ✅ Exit 프레임이 Entry 프레임보다 앞에 있으면 막기
                if (currentFrameIndex <= entryFrameIndex.Value)
                {
                    TimeSpan currentTimeCheck = TimeSpan.FromSeconds(currentFrameIndex / fps);
                    TimeSpan entryTimeCheck = TimeSpan.FromSeconds(entryFrameIndex.Value / fps);
                    
                    MessageBox.Show(
                        $"Exit 프레임은 Entry 프레임보다 뒤에 있어야 합니다.\n\n" +
                        $"Entry: {entryTimeCheck:hh\\:mm\\:ss} (프레임 {entryFrameIndex.Value})\n" +
                        $"현재: {currentTimeCheck:hh\\:mm\\:ss} (프레임 {currentFrameIndex})\n\n" +
                        $"Entry 프레임 이후로 이동한 후 Exit를 설정해주세요.",
                        "Warning",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // Entry 프레임의 Person, Vehicle, Event 박스 찾기
                var entryPersonBoxes = boundingBoxes.Where(b => b.FrameIndex == entryFrameIndex.Value && b.Label == "person").ToList();
                var entryVehicleBoxes = boundingBoxes.Where(b => b.FrameIndex == entryFrameIndex.Value && b.Label == "vehicle").ToList();
                var entryEventBoxes = boundingBoxes.Where(b => b.FrameIndex == entryFrameIndex.Value && b.Label == "event").ToList();

                // 선택만 추적 기능 롤백: 항상 Entry 프레임의 모든 person/vehicle 대상으로 생성
                // Event는 Entry~Exit 범위 내에서 자동으로 처리되므로 Entry 프레임에 없어도 됨
                
                if (entryPersonBoxes.Count == 0 && entryVehicleBoxes.Count == 0 && entryEventBoxes.Count == 0)
                {
                    // Entry~Exit 범위 내에 Event 박스가 있는지 확인 (Event는 Entry 프레임에 없어도 범위 내에 있으면 생성 가능)
                    var eventBoxesInRangeCheck = boundingBoxes
                        .Where(b => b.Label == "event" &&
                                   b.FrameIndex >= entryFrameIndex.Value &&
                                   b.FrameIndex <= currentFrameIndex)
                        .ToList();
                    
                    if (eventBoxesInRangeCheck.Count == 0)
                    {
                        MessageBox.Show("Entry 프레임에 Person, Vehicle 또는 Event 박스가 없습니다.\n또는 Entry~Exit 범위 내에 Event 박스가 없습니다.\n박스를 그린 후 X키를 눌러주세요.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

            exitFrameIndex = currentFrameIndex;
            TimeSpan exitTime = TimeSpan.FromSeconds(currentFrameIndex / fps);
            TimeSpan entryTime = TimeSpan.FromSeconds(entryFrameIndex.Value / fps);

            btnExit.Text = $"Exit: {exitTime:hh\\:mm\\:ss}";

                // ✅ 생성된 Waypoint 리스트
                List<WaypointMarker> createdWaypoints = new List<WaypointMarker>();
                
                int currentEntryFrame = entryFrameIndex.Value;
                int currentExitFrame = exitFrameIndex.Value;

                // ✅ 1. Person 박스들에 대해 각각 개별 Waypoint 생성
                foreach (var personBox in entryPersonBoxes)
                {
                    int personId = personBox.PersonId;
                    
                    // ✅ 현재 Entry~Exit 범위와 겹치거나 포함되는 기존 waypoint 확인
                    var overlappingWaypoint = waypointMarkers.FirstOrDefault(w =>
                        w.Label == "person" &&
                        w.ObjectId == personId &&
                        // 범위가 겹치는 경우: 
                        // 1. 현재 Entry가 기존 Entry~Exit 범위 내에 있음
                        // 2. 현재 Exit가 기존 Entry~Exit 범위 내에 있음
                        // 3. 현재 범위가 기존 범위를 완전히 포함
                        ((currentEntryFrame >= w.EntryFrame && currentEntryFrame <= w.ExitFrame) ||
                         (currentExitFrame >= w.EntryFrame && currentExitFrame <= w.ExitFrame) ||
                         (currentEntryFrame <= w.EntryFrame && currentExitFrame >= w.ExitFrame)));
                    
                    if (overlappingWaypoint != null)
                    {
                        // ✅ ExitFrame 확장 차단: 새로운 ExitFrame이 기존 waypoint의 ExitFrame보다 뒤인 경우 차단
                        if (currentExitFrame > overlappingWaypoint.ExitFrame)
                        {
                            TimeSpan currentExitTime = TimeSpan.FromSeconds(currentExitFrame / fps);
                            TimeSpan oldExitTime = TimeSpan.FromSeconds(overlappingWaypoint.ExitFrame / fps);
                            
                            MessageBox.Show(
                                $"ExitFrame을 기존 ExitFrame보다 뒤로 연장할 수 없습니다.\n\n" +
                                $"객체: {GetCategoryName("person", personId)}\n" +
                                $"기존 Waypoint: Entry={TimeSpan.FromSeconds(overlappingWaypoint.EntryFrame / fps):hh\\:mm\\:ss}, Exit={oldExitTime:hh\\:mm\\:ss}\n" +
                                $"현재 Exit: {currentExitTime:hh\\:mm\\:ss}\n\n" +
                                $"ExitFrame은 기존 ExitFrame보다 앞이거나 같아야 합니다.",
                                "Warning",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                            continue;
                        }
                        
                        // 이미 같은 PersonId의 waypoint가 현재 Entry~Exit 범위와 겹치면 중복 생성하지 않음
                        System.Diagnostics.Debug.WriteLine($"[Person Waypoint 중복 방지] PersonId={personId}: 기존 waypoint({overlappingWaypoint.EntryFrame}~{overlappingWaypoint.ExitFrame})와 겹치는 범위({currentEntryFrame}~{currentExitFrame})여서 새로 생성하지 않음");
                        continue;
                    }
                    
                    var waypoint = new WaypointMarker
                    {
                        EntryFrame = entryFrameIndex.Value,
                        ExitFrame = exitFrameIndex.Value,
                        MarkerColor = System.Drawing.Color.FromArgb(255, 107, 107), // 빨강
                        EntryTime = entryTime.ToString(@"hh\:mm\:ss"),
                        ExitTime = exitTime.ToString(@"hh\:mm\:ss"),
                        ObjectId = personId,
                        Label = "person"
                    };
                    
                    waypointMarkers.Add(waypoint);
                    createdWaypoints.Add(waypoint);
                }

                // ✅ 2. Vehicle 박스들에 대해 각각 개별 Waypoint 생성
                foreach (var vehicleBox in entryVehicleBoxes)
                {
                    int vehicleId = vehicleBox.VehicleId;
                    
                    // ✅ 현재 Entry~Exit 범위와 겹치거나 포함되는 기존 waypoint 확인
                    var overlappingWaypoint = waypointMarkers.FirstOrDefault(w =>
                        w.Label == "vehicle" &&
                        w.ObjectId == vehicleId &&
                        // 범위가 겹치는 경우: 
                        // 1. 현재 Entry가 기존 Entry~Exit 범위 내에 있음
                        // 2. 현재 Exit가 기존 Entry~Exit 범위 내에 있음
                        // 3. 현재 범위가 기존 범위를 완전히 포함
                        ((currentEntryFrame >= w.EntryFrame && currentEntryFrame <= w.ExitFrame) ||
                         (currentExitFrame >= w.EntryFrame && currentExitFrame <= w.ExitFrame) ||
                         (currentEntryFrame <= w.EntryFrame && currentExitFrame >= w.ExitFrame)));
                    
                    if (overlappingWaypoint != null)
                    {
                        // ✅ ExitFrame 확장 차단: 새로운 ExitFrame이 기존 waypoint의 ExitFrame보다 뒤인 경우 차단
                        if (currentExitFrame > overlappingWaypoint.ExitFrame)
                        {
                            TimeSpan currentExitTime = TimeSpan.FromSeconds(currentExitFrame / fps);
                            TimeSpan oldExitTime = TimeSpan.FromSeconds(overlappingWaypoint.ExitFrame / fps);
                            
                            MessageBox.Show(
                                $"ExitFrame을 기존 ExitFrame보다 뒤로 연장할 수 없습니다.\n\n" +
                                $"객체: {GetCategoryName("vehicle", vehicleId)}\n" +
                                $"기존 Waypoint: Entry={TimeSpan.FromSeconds(overlappingWaypoint.EntryFrame / fps):hh\\:mm\\:ss}, Exit={oldExitTime:hh\\:mm\\:ss}\n" +
                                $"현재 Exit: {currentExitTime:hh\\:mm\\:ss}\n\n" +
                                $"ExitFrame은 기존 ExitFrame보다 앞이거나 같아야 합니다.",
                                "Warning",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                            continue;
                        }
                        
                        // 이미 같은 VehicleId의 waypoint가 현재 Entry~Exit 범위와 겹치면 중복 생성하지 않음
                        System.Diagnostics.Debug.WriteLine($"[Vehicle Waypoint 중복 방지] VehicleId={vehicleId}: 기존 waypoint({overlappingWaypoint.EntryFrame}~{overlappingWaypoint.ExitFrame})와 겹치는 범위({currentEntryFrame}~{currentExitFrame})여서 새로 생성하지 않음");
                        continue;
                    }
                    
                    var waypoint = new WaypointMarker
                    {
                        EntryFrame = entryFrameIndex.Value,
                        ExitFrame = exitFrameIndex.Value,
                        MarkerColor = System.Drawing.Color.FromArgb(107, 158, 255), // 파랑
                        EntryTime = entryTime.ToString(@"hh\:mm\:ss"),
                        ExitTime = exitTime.ToString(@"hh\:mm\:ss"),
                        ObjectId = vehicleId,
                        Label = "vehicle"
                    };

                    waypointMarkers.Add(waypoint);
                    createdWaypoints.Add(waypoint);
                }

                // ✅ 2.5. Entry~Exit 범위 내 Event 박스 처리 (추적 없음)
                // EventId별로 그룹화하여 각 EventId당 하나의 Waypoint만 생성
                var eventBoxesInRange = boundingBoxes
                    .Where(b => b.Label == "event" &&
                                b.FrameIndex >= entryFrameIndex.Value &&
                                b.FrameIndex <= exitFrameIndex.Value)
                    .ToList();

                // EventId별로 그룹화하여 가장 작은 FrameIndex를 EntryFrame으로 사용
                var eventGroups = eventBoxesInRange
                    .GroupBy(b => b.EventId)
                    .ToList();

                foreach (var eventGroup in eventGroups)
                {
                    int eventId = eventGroup.Key;
                    // 같은 EventId를 가진 박스 중 가장 작은 FrameIndex를 EntryFrame으로 사용
                    int minFrameIndex = eventGroup.Min(b => b.FrameIndex);
                    
                    // ✅ 현재 Entry~Exit 범위와 겹치거나 포함되는 기존 waypoint 확인
                    // 같은 EventId를 가진 waypoint 중에서 현재 Entry~Exit 범위가 기존 waypoint 범위와 겹치는 경우
                    var overlappingWaypoint = waypointMarkers.FirstOrDefault(w =>
                        w.Label == "event" &&
                        w.ObjectId == eventId &&
                        // 범위가 겹치는 경우: 
                        // 1. 현재 Entry가 기존 Entry~Exit 범위 내에 있음
                        // 2. 현재 Exit가 기존 Entry~Exit 범위 내에 있음
                        // 3. 현재 범위가 기존 범위를 완전히 포함
                        ((currentEntryFrame >= w.EntryFrame && currentEntryFrame <= w.ExitFrame) ||
                         (currentExitFrame >= w.EntryFrame && currentExitFrame <= w.ExitFrame) ||
                         (currentEntryFrame <= w.EntryFrame && currentExitFrame >= w.ExitFrame)));
                    
                    if (overlappingWaypoint != null)
                    {
                        // 이미 같은 EventId의 waypoint가 현재 Entry~Exit 범위와 겹치면 중복 생성하지 않음
                        System.Diagnostics.Debug.WriteLine($"[Event Waypoint 중복 방지] EventId={eventId}: 기존 waypoint({overlappingWaypoint.EntryFrame}~{overlappingWaypoint.ExitFrame})와 겹치는 범위({currentEntryFrame}~{currentExitFrame})여서 새로 생성하지 않음");
                        continue;
                    }
                    
                    // 새로운 waypoint 생성
                    // Entry 프레임의 Event 박스 찾기 (가장 작은 FrameIndex)
                    var entryEventBox = eventGroup.FirstOrDefault(b => b.FrameIndex == minFrameIndex);
                    if (entryEventBox != null)
                    {
                        var evWp = new WaypointMarker
                        {
                            EntryFrame = minFrameIndex,
                            ExitFrame = exitFrameIndex.Value,
                            MarkerColor = System.Drawing.Color.FromArgb(107, 255, 107),
                            EntryTime = TimeSpan.FromSeconds(minFrameIndex / fps).ToString(@"hh\:mm\:ss"),
                            ExitTime = exitTime.ToString(@"hh\:mm\:ss"),
                            ObjectId = eventId,
                            Label = "event",
                            InteractingObject = "" // BoundingBox에는 InteractingObject 속성이 없으므로 빈 문자열로 설정
                        };
                        waypointMarkers.Add(evWp);

                        PropagateEventBoxWithinRange(entryEventBox, exitFrameIndex.Value);
                    }
                }

                // ✅ 이벤트 Exit 확정 시 자동 JSON 저장
                SaveCurrentLabelingData();

                // ✅ 3. UI 업데이트 및 Entry/Exit 초기화
                UpdateWaypointListView();

            entryFrameIndex = null;
            exitFrameIndex = null;
                btnEntry.Text = "Entry";
                btnExit.Text = "Exit";

            panelTimeline.Invalidate();

                // ✅ 4. 자동 추적 확인 (생성된 Waypoint가 있을 때만)
                if (createdWaypoints.Count > 0)
                {
                    // ✅ 실제로 생성된 waypoint만 카운트
                    int personWaypointCount = createdWaypoints.Count(w => w.Label == "person");
                    int vehicleWaypointCount = createdWaypoints.Count(w => w.Label == "vehicle");
                    int eventWaypointCount = createdWaypoints.Count(w => w.Label == "event");
                    
                    string summary = $"{createdWaypoints.Count}개의 Waypoint가 생성되었습니다.\n" +
                                    $"(Person: {personWaypointCount}개, Vehicle: {vehicleWaypointCount}개, Event: {eventWaypointCount}개)";

            var result = MessageBox.Show(
                        $"{summary}\n\n자동 추적을 수행하시겠습니까?",
                        "Waypoint 생성 완료",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                        // ✅ 순차적으로 추적 실행 (동시 실행으로 인한 충돌 방지)
                        // async void 메서드는 fire-and-forget 방식으로 호출
                        PerformSequentialTracking(createdWaypoints);
                    }
                }
            }
            else
            {
                MessageBox.Show("먼저 Entry 프레임을 지정하고 객체를 선택해야 합니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Exit 마커 생성 오류] {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"Exit 설정 중 오류 발생:\n{ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void listViewPersonWaypoints_Click(object sender, EventArgs e)
        {
            if (suppressWaypointClickOnce)
            {
                suppressWaypointClickOnce = false;
                return;
            }
            // Person Waypoint 항목을 클릭하면 해당 Entry 프레임으로 이동
            if (listViewPersonWaypoints.SelectedItems.Count > 0)
            {
                var selectedItem = listViewPersonWaypoints.SelectedItems[0];
                var waypoint = selectedItem.Tag as WaypointMarker;

                if (waypoint != null)
                {
                    selectedWaypoint = waypoint; // ✅ 선택된 waypoint 저장
                    UpdateWaypointInfo(waypoint); // ✅ Waypoint 정보 표시
                    panelTimeline.Invalidate(); // ✅ Timeline 다시 그리기
                    LoadFrame(waypoint.EntryFrame);
                }
            }
            else
            {
                selectedWaypoint = null; // ✅ 선택 해제
                UpdateWaypointInfo(null); // ✅ Waypoint 정보 초기화
                panelTimeline.Invalidate();
            }
        }

        private void listViewVehicleWaypoints_Click(object sender, EventArgs e)
        {
            if (suppressWaypointClickOnce)
            {
                suppressWaypointClickOnce = false;
                return;
            }
            // Vehicle Waypoint 항목을 클릭하면 해당 프레임으로 이동 (단일 프레임)
            if (listViewVehicleWaypoints.SelectedItems.Count > 0)
            {
                var selectedItem = listViewVehicleWaypoints.SelectedItems[0];
                var waypoint = selectedItem.Tag as WaypointMarker;

                if (waypoint != null)
                {
                    selectedWaypoint = waypoint; // ✅ 선택된 waypoint 저장
                    panelTimeline.Invalidate(); // ✅ Timeline 다시 그리기
                    LoadFrame(waypoint.EntryFrame); // Entry = Exit (단일 프레임)
                }
            }
            else
            {
                selectedWaypoint = null; // ✅ 선택 해제
                UpdateWaypointInfo(null); // ✅ Waypoint 정보 초기화
                panelTimeline.Invalidate();
            }
        }

        private void listViewEventWaypoints_Click(object sender, EventArgs e)
        {
            // Event Waypoint 항목을 클릭하면 해당 Entry 프레임으로 이동
            if (suppressWaypointClickOnce)
            {
                suppressWaypointClickOnce = false;
                return;
            }
            if (listViewEventWaypoints.SelectedItems.Count > 0)
            {
                var selectedItem = listViewEventWaypoints.SelectedItems[0];
                var waypoint = selectedItem.Tag as WaypointMarker;

                if (waypoint != null)
                {
                    selectedWaypoint = waypoint; // ✅ 선택된 waypoint 저장
                    UpdateWaypointInfo(waypoint); // ✅ Waypoint 정보 표시
                    panelTimeline.Invalidate(); // ✅ Timeline 다시 그리기
                    LoadFrame(waypoint.EntryFrame);
                }
            }
            else
            {
                selectedWaypoint = null; // ✅ 선택 해제
                UpdateWaypointInfo(null); // ✅ Waypoint 정보 초기화
                panelTimeline.Invalidate();
            }
        }

        // 리스트뷰 항목 컬럼 기반 클릭에 따라 Entry/Exit로 이동 또는 객체 박스 선택
        private void listViewWaypoints_MouseDown(object sender, MouseEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null) return;

            var hit = listView.HitTest(e.Location);
            if (hit.Item == null) return;

            if (hit.Item.Tag is WaypointMarker waypoint)
            {
                selectedWaypoint = waypoint;
                UpdateWaypointInfo(waypoint); // ✅ Waypoint 정보 표시
                panelTimeline.Invalidate();

                // 컬럼 기반 클릭: SubItem 인덱스로 Entry/Exit 구분
                int targetFrame = waypoint.EntryFrame;
                bool shouldSelectBox = false;
                
                if (hit.SubItem != null)
                {
                    int subIndex = hit.Item.SubItems.IndexOf(hit.SubItem);
                    
                    // Person/Vehicle: 컬럼0(Entry)=Entry, 컬럼1(Exit)=Exit, 컬럼2(객체)=박스 선택
                    if (listView == listViewPersonWaypoints || listView == listViewVehicleWaypoints)
                    {
                        if (subIndex == 0) // Entry 컬럼
                            targetFrame = waypoint.EntryFrame;
                        else if (subIndex == 1) // Exit 컬럼
                            targetFrame = waypoint.ExitFrame;
                        else if (subIndex == 2) // 객체 컬럼: 현재 프레임에서 박스 선택만 (EntryFrame 이동 안 함)
                        {
                            shouldSelectBox = true;
                        }
                    }
                    // Event: 컬럼0(Event)=Entry, 컬럼1(시간)=Entry, 컬럼2(객체)=박스 선택
                    else if (listView == listViewEventWaypoints)
                    {
                        if (subIndex == 2) // 객체 컬럼: 현재 프레임에서 박스 선택만 (EntryFrame 이동 안 함)
                        {
                            shouldSelectBox = true;
                        }
                        else // 다른 컬럼은 Entry로 이동
                            targetFrame = waypoint.EntryFrame;
                    }
                }
                
                // ✅ 객체 컬럼이 아닐 때만 프레임 이동
                if (!shouldSelectBox)
                {
                    LoadFrame(targetFrame);
                }
                
                // 객체 컬럼 클릭 시 해당 객체의 박스 선택 (현재 프레임에서)
                if (shouldSelectBox)
                {
                    SelectBoxForWaypoint(waypoint);
                }
                
                // 클릭 이벤트 1회 무시
                suppressWaypointClickOnce = true;
            }
        }
        
        // Waypoint에 해당하는 객체 박스 선택
        private void SelectBoxForWaypoint(WaypointMarker waypoint)
        {
            // 현재 프레임에서 waypoint의 ObjectId와 Label에 해당하는 박스 찾기
            BoundingBox targetBox = null;
            
            if (waypoint.Label == "person")
            {
                targetBox = boundingBoxes
                    .FirstOrDefault(b => b.FrameIndex == currentFrameIndex &&
                                       b.Label == "person" &&
                                       b.PersonId == waypoint.ObjectId &&
                                       !b.IsDeleted);
            }
            else if (waypoint.Label == "vehicle")
            {
                targetBox = boundingBoxes
                    .FirstOrDefault(b => b.FrameIndex == currentFrameIndex &&
                                       b.Label == "vehicle" &&
                                       b.VehicleId == waypoint.ObjectId &&
                                       !b.IsDeleted);
            }
            else if (waypoint.Label == "event")
            {
                targetBox = boundingBoxes
                    .FirstOrDefault(b => b.FrameIndex == currentFrameIndex &&
                                       b.Label == "event" &&
                                       b.EventId == waypoint.ObjectId &&
                                       !b.IsDeleted);
            }
            
            if (targetBox != null)
            {
                selectedBox = targetBox;
                UpdateObjectInfo(selectedBox);
                UpdateBboxListDisplay();
                HighlightSelectedBoxInSidebar();
                pictureBoxVideo.Invalidate();
            }
        }

        // Event waypoint의 객체(P/V) 컬럼(3번째) 더블클릭 시 인라인 편집
        private TextBox eventListEditBox;
        private void listViewEventWaypoints_DoubleClick(object sender, EventArgs e)
        {
            var mouse = listViewEventWaypoints.PointToClient(Control.MousePosition);
            var hit = listViewEventWaypoints.HitTest(mouse);
            if (hit.Item == null || hit.SubItem == null) return;

            int subIndex = hit.Item.SubItems.IndexOf(hit.SubItem);
            if (subIndex != 2) return; // 객체(P/V) 컬럼만 편집 허용

            var waypoint = hit.Item.Tag as WaypointMarker;
            if (waypoint == null) return;

            if (eventListEditBox == null || eventListEditBox.IsDisposed)
            {
                eventListEditBox = new TextBox();
                eventListEditBox.Leave += (s, ev) => CommitEventListEdit();
                eventListEditBox.KeyDown += (s, ev) =>
                {
                    if (ev.KeyCode == Keys.Enter)
                    {
                        CommitEventListEdit();
                        ev.Handled = true;
                    }
                    else if (ev.KeyCode == Keys.Escape)
                    {
                        CancelEventListEdit();
                        ev.Handled = true;
                    }
                };
            }

            eventListEditBox.Tag = hit.Item; // ListViewItem 보관 (Waypoint는 Item.Tag에 있음)
            eventListEditBox.Bounds = hit.SubItem.Bounds;
            eventListEditBox.Text = waypoint.InteractingObject ?? string.Empty;
            listViewEventWaypoints.Controls.Add(eventListEditBox);
            eventListEditBox.Focus();
            eventListEditBox.SelectAll();
        }

        private void CommitEventListEdit()
        {
            if (eventListEditBox == null || eventListEditBox.Tag == null) return;
            var item = eventListEditBox.Tag as ListViewItem;
            if (item == null) { CancelEventListEdit(); return; }
            var waypoint = item.Tag as WaypointMarker;
            if (waypoint == null) { CancelEventListEdit(); return; }

            waypoint.InteractingObject = eventListEditBox.Text ?? string.Empty;
            if (item.SubItems.Count >= 3)
            {
                item.SubItems[2].Text = waypoint.InteractingObject;
            }
            listViewEventWaypoints.Controls.Remove(eventListEditBox);
            eventListEditBox.Tag = null;

            // 수정 즉시 저장
            SaveCurrentLabelingData();
        }

        private void CancelEventListEdit()
        {
            if (eventListEditBox == null) return;
            listViewEventWaypoints.Controls.Remove(eventListEditBox);
            eventListEditBox.Tag = null;
        }

        private void listViewEventWaypoints_MouseUp(object sender, MouseEventArgs e)
        {
            // 단일 클릭으로도 3번째 컬럼을 누르면 편집 시작
            var hit = listViewEventWaypoints.HitTest(e.Location);
            if (hit.Item == null || hit.SubItem == null) return;
            int subIndex = hit.Item.SubItems.IndexOf(hit.SubItem);
            if (subIndex != 2) return;

            // 이미 편집 중이면 무시
            if (eventListEditBox != null && eventListEditBox.Tag != null) return;

            // 편집 시작
            var waypoint = hit.Item.Tag as WaypointMarker;
            if (waypoint == null) return;

            if (eventListEditBox == null || eventListEditBox.IsDisposed)
            {
                eventListEditBox = new TextBox();
                eventListEditBox.Leave += (s, ev) => CommitEventListEdit();
                eventListEditBox.KeyDown += (s, ev) =>
                {
                    if (ev.KeyCode == Keys.Enter)
                    {
                        CommitEventListEdit();
                        ev.Handled = true;
                    }
                    else if (ev.KeyCode == Keys.Escape)
                    {
                        CancelEventListEdit();
                        ev.Handled = true;
                    }
                };
            }

            eventListEditBox.Tag = hit.Item;
            eventListEditBox.Bounds = hit.SubItem.Bounds;
            eventListEditBox.Text = waypoint.InteractingObject ?? string.Empty;
            listViewEventWaypoints.Controls.Add(eventListEditBox);
            eventListEditBox.Focus();
            eventListEditBox.SelectAll();
        }

        // ✅ 통합 waypoint 삭제 함수 (모든 타입 지원)
        private void btnDeleteSelectedWaypoint_Click(object sender, EventArgs e)
        {
            try
            {
                // 선택된 waypoint 확인 (Person, Vehicle, Event 모두 확인)
                WaypointMarker waypoint = null;
                ListViewItem selectedItem = null;
                string waypointType = "";

                if (listViewPersonWaypoints.SelectedItems.Count > 0)
                {
                    selectedItem = listViewPersonWaypoints.SelectedItems[0];
                    waypoint = selectedItem.Tag as WaypointMarker;
                    waypointType = "Person";
                }
                else if (listViewVehicleWaypoints.SelectedItems.Count > 0)
                {
                    selectedItem = listViewVehicleWaypoints.SelectedItems[0];
                    waypoint = selectedItem.Tag as WaypointMarker;
                    waypointType = "Vehicle";
                }
                else if (listViewEventWaypoints.SelectedItems.Count > 0)
                {
                    selectedItem = listViewEventWaypoints.SelectedItems[0];
                    waypoint = selectedItem.Tag as WaypointMarker;
                    waypointType = "Event";
                }
                else
                {
                    MessageBox.Show("삭제할 Waypoint를 선택해주세요.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (waypoint == null)
                {
                    MessageBox.Show("선택한 Waypoint 정보를 찾을 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var result = MessageBox.Show(
                    $"선택한 {waypointType} Waypoint를 삭제하시겠습니까?\n\n" +
                    $"Entry: {waypoint.EntryTime}\n" +
                    $"Exit: {waypoint.ExitTime}\n\n" +
                    $"⚠️ 주의: 해당 구간의 {waypointType} 박스가 삭제됩니다.",
                    $"{waypointType} Waypoint 삭제 확인",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                    return;

                // ✅ 해당 타입의 박스만 삭제 (ObjectId로 정확히 필터링)
                List<BoundingBox> boxesToDelete = new List<BoundingBox>();
                
                if (waypoint.Label == "person")
                {
                    boxesToDelete = boundingBoxes
                        .Where(b => 
                            b.Label == "person" &&
                            b.PersonId == waypoint.ObjectId &&
                            b.FrameIndex >= waypoint.EntryFrame && 
                            b.FrameIndex <= waypoint.ExitFrame)
                        .ToList();
                }
                else if (waypoint.Label == "vehicle")
                {
                    boxesToDelete = boundingBoxes
                        .Where(b => 
                            b.Label == "vehicle" &&
                            b.VehicleId == waypoint.ObjectId &&
                            b.FrameIndex >= waypoint.EntryFrame && 
                            b.FrameIndex <= waypoint.ExitFrame)
                        .ToList();
                }
                else if (waypoint.Label == "event")
                {
                    boxesToDelete = boundingBoxes
                        .Where(b => 
                            b.Label == "event" &&
                            b.EventId == waypoint.ObjectId &&
                            b.FrameIndex >= waypoint.EntryFrame && 
                            b.FrameIndex <= waypoint.ExitFrame)
                        .ToList();
                }

                foreach (var box in boxesToDelete)
                {
                    AddUndoAction(new UndoAction { Type = UndoActionType.RemoveBox, Box = CloneBoundingBox(box) });
                    boundingBoxes.Remove(box);
                }

                if (selectedBox != null && boxesToDelete.Contains(selectedBox))
                    selectedBox = null;

                // ✅ 선택된 waypoint가 삭제되면 선택 해제
                if (selectedWaypoint == waypoint)
                {
                    selectedWaypoint = null;
                    UpdateWaypointInfo(null);
                }

                waypointMarkers.Remove(waypoint);
                
                // ✅ ListView 업데이트 (모든 항목을 다시 그려서 동기화)
                UpdateWaypointListView();

                InvalidateBoxCache();
                UpdateBoxCount();
                UpdateBboxListDisplay();
                panelTimeline.Invalidate();
                pictureBoxVideo.Invalidate();

                MessageBox.Show(
                    $"✅ {waypointType} Waypoint가 삭제되었습니다.\n\n" +
                    $"삭제된 박스: {boxesToDelete.Count}개",
                    "삭제 완료",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Waypoint 삭제 오류] {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"Waypoint 삭제 중 오류 발생:\n{ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // 기존 삭제 함수들은 통합 함수로 대체됨 (하위 호환성 유지)
        private void btnDeletePersonWaypoint_Click(object sender, EventArgs e)
        {
            btnDeleteSelectedWaypoint_Click(sender, e);
        }

        private void btnDeleteVehicleWaypoint_Click(object sender, EventArgs e)
        {
            btnDeleteSelectedWaypoint_Click(sender, e);
        }

        private void btnDeleteEventWaypoint_Click(object sender, EventArgs e)
        {
            btnDeleteSelectedWaypoint_Click(sender, e);
        }
        #endregion

        #region Drawing Mode
        private void btnVideoList_Click(object sender, EventArgs e) => ShowVideoListForm();

        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            currentMode = DrawMode.Select;
            btnSelectAll.BackColor = Color.FromArgb(59, 130, 246);
            btnEdit.BackColor = SystemColors.Control;
            pictureBoxVideo.Cursor = Cursors.Hand;
            
            // ✅ 선택된 박스가 있으면 하이라이트 유지
            if (selectedBox != null)
            {
                HighlightSelectedBoxInSidebar();
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            currentMode = DrawMode.Draw;
            btnEdit.BackColor = Color.FromArgb(59, 130, 246);
            btnSelectAll.BackColor = SystemColors.Control;
            pictureBoxVideo.Cursor = Cursors.Cross;
            
            // ✅ Edit 모드로 전환 시 하이라이트 유지
            if (selectedBox != null)
            {
                HighlightSelectedBoxInSidebar();
            }
        }

        

        private void pictureBoxVideo_MouseDown(object sender, MouseEventArgs e)
        {
            // ✅ 우클릭: Person 박스 속성 편집
            if (e.Button == MouseButtons.Right)
            {
                var clickedBox = GetBoundingBoxAt(e.Location);
                if (clickedBox != null && clickedBox.Label == "person")
                {
                    var waypoint = FindWaypointForBox(clickedBox);
                    int waypointEntryFrame = waypoint != null ? waypoint.EntryFrame : currentFrameIndex;
                    
                    using (var form = new PersonAttributesForm(
                        clickedBox.PersonId,
                        waypointEntryFrame,
                        currentFrameIndex,
                        GetPersonAttribute,
                        SetPersonAttribute))
                    {
                        if (form.ShowDialog() == DialogResult.OK)
                        {
                            // 속성 저장 완료 (form에서 이미 저장됨)
                            UpdateBboxListDisplay();
                            pictureBoxVideo.Invalidate();
                        }
                    }
                    return;
                }
            }
            
            if (currentMode == DrawMode.Draw)
            {
                // Entry 마커 체크 제거 - YOLO 추적 시에만 필요
                isDrawing = true;
                drawStartPoint = e.Location; // 뷰 좌표로 저장

                // 이미지 좌표로 변환하여 저장
                var imagePoint = ViewToImage(new PointF(e.X, e.Y));
                
                drawingBox = new BoundingBox
                {
                    FrameIndex = currentFrameIndex,
                    Rectangle = new Rectangle((int)imagePoint.X, (int)imagePoint.Y, 0, 0),
                    Label = currentSelectedLabel,
                    PersonId = currentSelectedLabel == "person" ? currentAssignedId : 0,
                    VehicleId = currentSelectedLabel == "vehicle" ? currentAssignedId : 0,
                    EventId = currentSelectedLabel == "event" ? currentAssignedId : 0,
                    Action = "waypoint"
                };
            }
            else if (currentMode == DrawMode.Select)
            {
                // ✅ 먼저 선택된 박스의 크기 조정 핸들을 체크
                if (selectedBox != null)
                {
                    var viewRect = ImageToView(new RectangleF(selectedBox.Rectangle.X, selectedBox.Rectangle.Y, 
                        selectedBox.Rectangle.Width, selectedBox.Rectangle.Height));
                    
                    ResizeHandle handle = GetResizeHandleAtPoint(e.Location, viewRect);
                    
                    if (handle != ResizeHandle.None)
                    {
                        // 크기 조정 시작
                        isResizing = true;
                        currentResizeHandle = handle;
                        resizeStartPoint = e.Location;
                        originalResizeRect = selectedBox.Rectangle;
                        return;
                    }

                    // ✅ 선택 유지 개선: 선택된 박스 내부 클릭이더라도
                    // 동일 지점에 다른 후보 박스가 있으면 새 선택을 허용
                    if (viewRect.Contains(e.Location))
                    {
                        if (!HasAnotherHitCandidateAt(e.Location, selectedBox))
                        {
                            // 다른 후보가 없을 때만 드래그 시작
                            isDragging = true;
                            dragOffset = new System.Drawing.Point(e.X - (int)viewRect.X, e.Y - (int)viewRect.Y);
                            UpdateObjectInfo(selectedBox);
                            UpdateBboxListDisplay();
                            HighlightSelectedBoxInSidebar();
                            pictureBoxVideo.Invalidate();
                            return;
                        }
                        else
                        {
                            // ✅ 다른 후보가 있으면 현재 선택 다음 후보로 전환 (라벨 전환 보장)
                            var ordered = GetOrderedCandidatesAt(e.Location);
                            if (ordered.Count > 0)
                            {
                                int idx = ordered.IndexOf(selectedBox);
                                // 현재가 목록에 없으면 첫 후보, 있으면 다음 후보
                                BoundingBox next = (idx < 0)
                                    ? ordered[0]
                                    : ordered[(idx + 1) % ordered.Count];

                                if (next != selectedBox)
                                {
                                    selectedBox = next;
                                    UpdateObjectInfo(selectedBox);
                                    UpdateBboxListDisplay();
                                    HighlightSelectedBoxInSidebar();
                                    pictureBoxVideo.Invalidate();
                                    return;
                                }
                            }
                        }
                        // 후보 없음이면 아래 선택 로직으로 진행
                    }
                }
                
                // 핸들이 아니면 박스 선택 또는 드래그
                selectedBox = GetBoundingBoxAt(e.Location);

                if (selectedBox != null)
                {
                    isDragging = true;
                    // 뷰 좌표로 변환한 박스 위치 기준으로 드래그 오프셋 계산
                    var viewRect = ImageToView(new RectangleF(selectedBox.Rectangle.X, selectedBox.Rectangle.Y, 
                        selectedBox.Rectangle.Width, selectedBox.Rectangle.Height));
                    dragOffset = new System.Drawing.Point(e.X - (int)viewRect.X, e.Y - (int)viewRect.Y);
                    UpdateObjectInfo(selectedBox);
                    UpdateBboxListDisplay(); // 선택 후 우측 패널 동기화
                    HighlightSelectedBoxInSidebar(); // ✅ 우측 사이드바에서 선택된 박스 하이라이트
                    pictureBoxVideo.Invalidate();
                }
                else
                {
                    // ✅ 빈 공간 클릭 시 선택 해제
                    selectedBox = null;
                    ClearSidebarHighlights(); // 하이라이트 초기화
                    UpdateObjectInfo(null); // 객체 정보 초기화
                    UpdateBboxListDisplay(); // 우측 패널 동기화
                    pictureBoxVideo.Invalidate();
                }
            }
        }

        private void pictureBoxVideo_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDrawing && drawingBox != null)
            {
                // 뷰 좌표에서 이미지 좌표로 변환
                var startImagePoint = ViewToImage(new PointF(drawStartPoint.X, drawStartPoint.Y));
                var currentImagePoint = ViewToImage(new PointF(e.X, e.Y));

                int x = (int)Math.Min(startImagePoint.X, currentImagePoint.X);
                int y = (int)Math.Min(startImagePoint.Y, currentImagePoint.Y);
                int width = (int)Math.Abs(currentImagePoint.X - startImagePoint.X);
                int height = (int)Math.Abs(currentImagePoint.Y - startImagePoint.Y);

                drawingBox.Rectangle = new Rectangle(x, y, width, height);

                pictureBoxVideo.Invalidate();
            }
            else if (isResizing && selectedBox != null)
            {
                // ✅ 크기 조정 중
                PerformResize(e.Location);
                pictureBoxVideo.Invalidate();
            }
            else if (isDragging && selectedBox != null)
            {
                // 뷰 좌표에서 이미지 좌표로 변환
                var viewPos = new PointF(e.X - dragOffset.X, e.Y - dragOffset.Y);
                var imagePos = ViewToImage(viewPos);

                selectedBox.Rectangle = new Rectangle(
                    (int)imagePos.X,
                    (int)imagePos.Y,
                    selectedBox.Rectangle.Width,
                    selectedBox.Rectangle.Height
                );

                pictureBoxVideo.Invalidate();
            }
            else if (currentMode == DrawMode.Select && selectedBox != null)
            {
                // ✅ 크기 조정이 아닐 때 커서 변경
                var viewRect = ImageToView(new RectangleF(selectedBox.Rectangle.X, selectedBox.Rectangle.Y, 
                    selectedBox.Rectangle.Width, selectedBox.Rectangle.Height));
                
                ResizeHandle handle = GetResizeHandleAtPoint(e.Location, viewRect);
                UpdateCursorForHandle(handle);
            }
            else
            {
                // ✅ 모드에 맞는 커서 유지: DrawMode.Select는 Hand, DrawMode.Draw는 Cross
                if (currentMode == DrawMode.Select)
                {
                    pictureBoxVideo.Cursor = Cursors.Hand;
                }
                else if (currentMode == DrawMode.Draw)
                {
                    pictureBoxVideo.Cursor = Cursors.Cross;
                }
                else
                {
                    pictureBoxVideo.Cursor = Cursors.Default;
                }
            }
        }

        private void pictureBoxVideo_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDrawing && drawingBox != null)
            {
                if (drawingBox.Rectangle.Width > 10 && drawingBox.Rectangle.Height > 10)
                {
                    boundingBoxes.Add(drawingBox);
                    InvalidateBoxCache();
                    AddUndoAction(new UndoAction { Type = UndoActionType.AddBox, Box = drawingBox });

                    selectedBox = drawingBox;
                    UpdateObjectInfo(selectedBox);
                    UpdateBoxCount();
                    UpdateBboxListDisplay();
                    
                    // Event: 즉시 전파/웨이포인트 생성하지 않음. Exit 확정 시 처리
                    if (drawingBox.Label == "event")
                    {
                        // no-op
                    }
                }

                drawingBox = null;
                isDrawing = false;
                pictureBoxVideo.Invalidate();
            }
            else if (isResizing)
            {
                // ✅ 크기 조정 완료
                isResizing = false;
                currentResizeHandle = ResizeHandle.None;
                
                // Undo 스택에 추가 (원본 Rectangle 저장)
                var undoBox = CloneBoundingBox(selectedBox);
                undoBox.Rectangle = originalResizeRect;
                AddUndoAction(new UndoAction { Type = UndoActionType.ModifyBox, Box = undoBox });
                
                // ✅ 수동 수정 프레임 기록
                RecordManuallyAdjustedFrame(selectedBox);
                
                InvalidateBoxCache();
                UpdateObjectInfo(selectedBox);
                UpdateBboxListDisplay();
                
                // Event 박스 전파
                if (selectedBox != null && selectedBox.Label == "event")
                {
                    PropagateEventBoxFromCurrentFrame(selectedBox);
                }
                
                pictureBoxVideo.Cursor = Cursors.Default;
                pictureBoxVideo.Invalidate();
            }
            else if (isDragging)
            {
                isDragging = false;
                
                // ✅ 수동 수정 프레임 기록
                if (selectedBox != null)
                {
                    RecordManuallyAdjustedFrame(selectedBox);
                }
                
                // 박스 이동/수정 완료 시에도 Event 박스 전파
                if (selectedBox != null && selectedBox.Label == "event")
                {
                    PropagateEventBoxFromCurrentFrame(selectedBox);
                }
            }
        }

        private void pictureBoxVideo_Resize(object sender, EventArgs e)
        {
            // PictureBox 크기가 변경될 때 타임스탬프 Label 위치 조정 (좌측 하단)
            if (labelSubtitleTimestamp != null && pictureBoxVideo != null)
            {
                labelSubtitleTimestamp.Location = new System.Drawing.Point(
                    50,
                    pictureBoxVideo.Height - labelSubtitleTimestamp.Height - 30
                );
            }
        }

        private void panelVideoControls_Resize(object sender, EventArgs e)
        {
            // panelVideoControls 크기가 변경될 때 레이아웃 조정
            if (panelVideoControls != null)
            {
                // groupBoxObjectInfo: 오른쪽에 고정
                if (groupBoxObjectInfo != null)
                {
                    groupBoxObjectInfo.Location = new System.Drawing.Point(
                        panelVideoControls.Width - groupBoxObjectInfo.Width - 16, // 우측 여백 16px
                        16 // 상단 여백
                    );
                }
            }
        }

        private void pictureBoxVideo_Paint(object sender, PaintEventArgs e)
        {
            if (pictureBoxVideo.Image == null)
                return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 성능 최적화: 현재 프레임의 박스를 캐시에서 가져오기
            if (lastCachedFrameForPaint != currentFrameIndex)
            {
                cachedCurrentFrameBoxes = boundingBoxes.Where(b => b.FrameIndex == currentFrameIndex).ToList();
                lastCachedFrameForPaint = currentFrameIndex;
            }

            foreach (var box in cachedCurrentFrameBoxes)
            {
                // 현재 프레임의 박스만 표시 (box.FrameIndex == currentFrameIndex)
                // currentFrameBoxes에서 이미 필터링되었으므로 추가 체크 불필요

                // 이미지 좌표를 뷰 좌표로 변환
                var viewRect = ImageToView(new RectangleF(box.Rectangle.X, box.Rectangle.Y, 
                    box.Rectangle.Width, box.Rectangle.Height));

                Color boxColor = GetColorForLabel(box.Label);
                
                // ✅ 삭제된 박스는 얇고 옅게 표시
                if (box.IsDeleted)
                {
                    Color fadedColor = Color.FromArgb(150, boxColor.R, boxColor.G, boxColor.B);
                    using (Pen pen = new Pen(fadedColor, 2))
                    {
                        g.DrawRectangle(pen, viewRect.X, viewRect.Y, viewRect.Width, viewRect.Height);
                    }
                }
                else
                {
                    // 정상 박스는 기존 로직대로
                    using (Pen pen = new Pen(boxColor, 3))
                    {
                        if (box == selectedBox)
                            pen.Width = 5;
                        g.DrawRectangle(pen, viewRect.X, viewRect.Y, viewRect.Width, viewRect.Height);
                    }

                    // 라벨 텍스트 생성 (성능 최적화: 캐싱된 배열 사용)
                    string labelText = GetBoxLabelText(box);
                    
                    // 성능 최적화: 재사용 가능한 Font 사용
                    SizeF textSize = g.MeasureString(labelText, labelFont);
                    RectangleF labelBg = new RectangleF(
                        viewRect.X,
                        
                        viewRect.Y - textSize.Height - 4,
                        textSize.Width + 8,
                        textSize.Height + 4
                    );

                    using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(200, boxColor)))
                        g.FillRectangle(bgBrush, labelBg);

                    using (SolidBrush textBrush = new SolidBrush(Color.White))
                        g.DrawString(labelText, labelFont, textBrush, viewRect.X + 4, viewRect.Y - textSize.Height - 2);
                    
                    // ✅ 선택된 박스에 크기 조정 핸들 표시 (4개 엣지만)
                    if (box == selectedBox)
                    {
                        DrawResizeHandles(g, viewRect);
                        
                        // ✅ Person 박스인 경우 속성 정보 표시
                        if (box.Label == "person" && currentMode == DrawMode.Select)
                        {
                            DrawPersonAttributes(g, box, viewRect);
                        }
                    }
                }
            }

            if (isDrawing && drawingBox != null)
            {
                // 이미지 좌표를 뷰 좌표로 변환
                var viewRect = ImageToView(new RectangleF(drawingBox.Rectangle.X, drawingBox.Rectangle.Y, 
                    drawingBox.Rectangle.Width, drawingBox.Rectangle.Height));

                Color boxColor = GetColorForLabel(drawingBox.Label);
                using (Pen pen = new Pen(boxColor, 3) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(pen, viewRect.X, viewRect.Y, viewRect.Width, viewRect.Height);
            }

            // ✅ YOLO 탐지 박스 표시 (토글이 켜져 있을 때만)
            if (showYoloDetections && isYoloAvailable)
            {
                try
                {
                    List<YoloDetectionBox> detections = null;
                    bool cacheAvailable = false;
                    
                    lock (yoloDetectionCacheLock)
                    {
                        cacheAvailable = yoloDetectionCache.ContainsKey(currentFrameIndex);
                        if (cacheAvailable)
                        {
                            detections = yoloDetectionCache[currentFrameIndex];
                        }
                    }
                    
                    if (cacheAvailable && detections != null && detections.Count > 0)
                    {
                        foreach (var detection in detections)
                        {
                            try
                            {
                                if (detection == null || detection.Rectangle.IsEmpty)
                                    continue;
                                
                                // 이미지 좌표를 뷰 좌표로 변환
                                var viewRect = ImageToView(new RectangleF(detection.Rectangle.X, detection.Rectangle.Y,
                                    detection.Rectangle.Width, detection.Rectangle.Height));

                                // 유효한 좌표인지 확인
                                if (viewRect.Width <= 0 || viewRect.Height <= 0 || 
                                    viewRect.X < -1000 || viewRect.Y < -1000 || 
                                    viewRect.X > 10000 || viewRect.Y > 10000)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 렌더링] 유효하지 않은 좌표: {viewRect}");
                                    continue;
                                }

                                // ✅ YOLO 탐지 박스는 원래 바운딩 박스와 동일한 색깔이되 얇은 두께로 표시
                                // 라벨을 COCO 형식에서 애플리케이션 형식으로 변환
                                string appLabel = ConvertCocoLabelToAppLabel(detection.Label);
                                Color boxColor = GetColorForLabel(appLabel);
                                
                                // 얇은 두께(1px)로 표시하여 구분 가능하게
                                using (Pen pen = new Pen(boxColor, 1))
                                {
                                    g.DrawRectangle(pen, viewRect.X, viewRect.Y, viewRect.Width, viewRect.Height);
                                }

                                // 라벨 텍스트 표시 (재사용 가능한 Font 사용)
                                if (!string.IsNullOrEmpty(detection.Label) && yoloDetectionFont != null)
                                {
                                    try
                                    {
                                        string labelText = $"{detection.Label} ({detection.Confidence:P0})";
                                        SizeF textSize = g.MeasureString(labelText, yoloDetectionFont);
                                        
                                        if (textSize.Width > 0 && textSize.Height > 0)
                                        {
                                            RectangleF labelBg = new RectangleF(
                                                viewRect.X,
                                                viewRect.Y - textSize.Height - 2,
                                                textSize.Width + 4,
                                                textSize.Height + 2
                                            );

                                            // 원래 색상의 반투명 배경 사용
                                            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(200, boxColor)))
                                                g.FillRectangle(bgBrush, labelBg);

                                            using (SolidBrush textBrush = new SolidBrush(Color.White))
                                                g.DrawString(labelText, yoloDetectionFont, textBrush, viewRect.X + 2, viewRect.Y - textSize.Height);
                                        }
                                    }
                                    catch (Exception textEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 렌더링] 텍스트 표시 오류: {textEx.Message}");
                                    }
                                }
                            }
                            catch (Exception detectionEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 렌더링] 개별 박스 렌더링 오류: {detectionEx.Message}");
                                // 개별 박스 오류는 계속 진행
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 렌더링 오류] {ex.Message}\n{ex.StackTrace}");
                    // 렌더링 오류는 전체 UI에 영향 주지 않도록 무시
                }
            }
        }

        // ✅ 실패 박스 판별 함수
        private bool IsTrackingFailed(BoundingBox box)
        {
            string key = $"{box.Label}_{GetBoxId(box)}";
            if (!waypointFailureRanges.ContainsKey(key))
                return false;
            
            return waypointFailureRanges[key].Any(range => 
                box.FrameIndex >= range.start && box.FrameIndex <= range.end);
        }
        
        // ✅ 우측 사이드바에서 선택된 박스 하이라이트
        private void HighlightSelectedBoxInSidebar()
        {
            if (selectedBox == null) return;
            
            // 모든 패널의 하이라이트 초기화
            ClearSidebarHighlights();
            
            // 선택된 박스의 라벨에 따라 해당 패널에서 하이라이트
            switch (selectedBox.Label.ToLower())
            {
                case "person":
                    HighlightBoxInPanel(panelPersonList, selectedBox);
                    break;
                case "vehicle":
                    HighlightBoxInPanel(panelVehicleList, selectedBox);
                    break;
                case "event":
                    HighlightBoxInPanel(panelEventList, selectedBox);
                    break;
            }
        }
        
        // ✅ 패널에서 특정 박스 하이라이트
        private void HighlightBoxInPanel(Panel panel, BoundingBox targetBox)
        {
            // 카테고리별 하이라이트 색상 결정
            Color highlightColor;
            if (panel == panelPersonList)
                highlightColor = Color.FromArgb(252, 231, 243); // 연한 분홍색
            else if (panel == panelVehicleList)
                highlightColor = Color.FromArgb(219, 234, 254); // 연한 파란색
            else if (panel == panelEventList)
                highlightColor = Color.FromArgb(220, 252, 231); // 연한 녹색
            else
                highlightColor = Color.FromArgb(200, 255, 200); // 기본 연한 녹색
            
            foreach (Control ctrl in panel.Controls)
            {
                if (ctrl is Panel itemPanel)
                {
                    // 패널의 Tag에서 박스 정보 가져오기
                    if (itemPanel.Tag is BoundingBox box && box == targetBox)
                    {
                        itemPanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
                        itemPanel.BackColor = highlightColor; // 카테고리별 색상으로 하이라이트
                    }
                    else
                    {
                        itemPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
                        itemPanel.BackColor = SystemColors.Control;
                    }
                }
            }
        }
        
        // ✅ 사이드바 하이라이트 초기화
        private void ClearSidebarHighlights()
        {
            ClearPanelHighlights(panelPersonList);
            ClearPanelHighlights(panelVehicleList);
            ClearPanelHighlights(panelEventList);
        }
        
        // ✅ 패널 하이라이트 초기화
        private void ClearPanelHighlights(Panel panel)
        {
            // 카테고리별 기본 색상 결정
            Color defaultColor;
            if (panel == panelPersonList)
                defaultColor = Color.FromArgb(252, 231, 243); // 연한 분홍색
            else if (panel == panelVehicleList)
                defaultColor = Color.FromArgb(219, 234, 254); // 연한 파란색
            else if (panel == panelEventList)
                defaultColor = Color.FromArgb(220, 252, 231); // 연한 녹색
            else
                defaultColor = SystemColors.Control; // 기본 색상
            
            foreach (Control ctrl in panel.Controls)
            {
                if (ctrl is Panel itemPanel)
                {
                    itemPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
                    itemPanel.BackColor = defaultColor; // 카테고리별 기본 색상으로 복원
                }
            }
        }
        
        private System.Drawing.Point lastClickViewPoint;
        private List<BoundingBox> lastHitCandidates = new List<BoundingBox>();
        private int lastHitIndex = -1;

        private BoundingBox GetBoundingBoxAt(System.Drawing.Point location)
        {
            // 뷰 좌표를 이미지 좌표로 변환
            var imageLocation = ViewToImage(new PointF(location.X, location.Y));
            
            // ✅ 현재 프레임에 해당하는 박스들을 필터링 (삭제되지 않은 박스만)
            var currentFrameBoxes = boundingBoxes.Where(b => b.FrameIndex == currentFrameIndex && !b.IsDeleted).ToList();

            // ✅ 히트 테스트 마진(이미지 좌표 기준)으로 경계 클릭 허용
            const int hitMargin = 4;

            // 후보 수집
            var candidates = new List<(BoundingBox box, bool inActiveWaypoint, int labelPri, double dist, int area, int zIndex)>();

            foreach (var box in currentFrameBoxes)
            {
                // 선택 단계에서는 Waypoint 범위 제한을 적용하지 않음 (편집 시에만 제한)

                // 히트마진 적용한 이미지 좌표 내 포함 여부
                var r = box.Rectangle;
                r.Inflate(hitMargin, hitMargin);
                if (!r.Contains((int)imageLocation.X, (int)imageLocation.Y))
                    continue;

                bool inActiveWaypoint = false;
                if (selectedWaypoint != null)
                {
                    inActiveWaypoint = (box.Label == selectedWaypoint.Label &&
                                        GetBoxId(box) == selectedWaypoint.ObjectId &&
                                        currentFrameIndex >= selectedWaypoint.EntryFrame &&
                                        currentFrameIndex <= selectedWaypoint.ExitFrame);
                }

                int labelPri = GetLabelPriority(box.Label);
                var centerX = box.Rectangle.X + box.Rectangle.Width / 2.0;
                var centerY = box.Rectangle.Y + box.Rectangle.Height / 2.0;
                var dx = centerX - imageLocation.X;
                var dy = centerY - imageLocation.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                int area = Math.Max(1, box.Rectangle.Width * box.Rectangle.Height);
                int zIndex = boundingBoxes.IndexOf(box); // 더 뒤쪽(큰 인덱스)이 화면상 위에 있다고 가정

                candidates.Add((box, inActiveWaypoint, labelPri, dist, area, zIndex));
            }

            if (candidates.Count == 0)
                return null;

            // ✅ 가중치 기반 정렬: 활성 웨이포인트 > 라벨 우선순위 > 중심거리↓ > 면적↓(작은 것 우선) > Z-Order
            var ordered = candidates
                .OrderByDescending(c => c.inActiveWaypoint)
                .ThenByDescending(c => c.labelPri)
                .ThenBy(c => c.dist)
                .ThenBy(c => c.area)
                .ThenByDescending(c => c.zIndex)
                .ToList();

            // 선택 사이클링을 위해 후보 및 클릭 위치 저장
            lastHitCandidates = ordered.Select(c => c.box).ToList();
            lastHitIndex = 0;
            lastClickViewPoint = location;

            return lastHitCandidates[0];
        }

        private int GetLabelPriority(string label)
        {
            switch ((label ?? string.Empty).ToLower())
            {
                case "person": return 3;
                case "vehicle": return 2;
                case "event": return 1;
                default: return 0;
            }
        }

        // 현재 클릭 지점에 선택된 박스 외의 다른 후보가 존재하는지 검사
        private bool HasAnotherHitCandidateAt(System.Drawing.Point viewLocation, BoundingBox exclude)
        {
            var imageLocation = ViewToImage(new PointF(viewLocation.X, viewLocation.Y));
            var currentFrameBoxes = boundingBoxes.Where(b => b.FrameIndex == currentFrameIndex && !b.IsDeleted);
            const int hitMargin = 4;

            foreach (var box in currentFrameBoxes)
            {
                if (box == exclude) continue;
                // 선택 단계에서는 Waypoint 범위 제한을 적용하지 않음

                var r = box.Rectangle;
                r.Inflate(hitMargin, hitMargin);
                if (r.Contains((int)imageLocation.X, (int)imageLocation.Y))
                    return true;
            }
            return false;
        }

        private List<BoundingBox> GetOrderedCandidatesAt(System.Drawing.Point viewLocation)
        {
            var imageLocation = ViewToImage(new PointF(viewLocation.X, viewLocation.Y));
            var currentFrameBoxes = boundingBoxes.Where(b => b.FrameIndex == currentFrameIndex && !b.IsDeleted).ToList();
            const int hitMargin = 4;

            var candidates = new List<(BoundingBox box, bool inActiveWaypoint, int labelPri, double dist, int area, int zIndex)>();

            foreach (var box in currentFrameBoxes)
            {
                // 선택 단계에서는 Waypoint 범위 제한을 적용하지 않음

                var r = box.Rectangle;
                r.Inflate(hitMargin, hitMargin);
                if (!r.Contains((int)imageLocation.X, (int)imageLocation.Y))
                    continue;

                bool inActiveWaypoint = false;
                if (selectedWaypoint != null)
                {
                    inActiveWaypoint = (box.Label == selectedWaypoint.Label &&
                                        GetBoxId(box) == selectedWaypoint.ObjectId &&
                                        currentFrameIndex >= selectedWaypoint.EntryFrame &&
                                        currentFrameIndex <= selectedWaypoint.ExitFrame);
                }

                int labelPri = GetLabelPriority(box.Label);
                var centerX = box.Rectangle.X + box.Rectangle.Width / 2.0;
                var centerY = box.Rectangle.Y + box.Rectangle.Height / 2.0;
                var dx = centerX - imageLocation.X;
                var dy = centerY - imageLocation.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                int area = Math.Max(1, box.Rectangle.Width * box.Rectangle.Height);
                int zIndex = boundingBoxes.IndexOf(box);

                candidates.Add((box, inActiveWaypoint, labelPri, dist, area, zIndex));
            }

            var ordered = candidates
                .OrderByDescending(c => c.inActiveWaypoint)
                .ThenByDescending(c => c.labelPri)
                .ThenBy(c => c.dist)
                .ThenBy(c => c.area)
                .ThenByDescending(c => c.zIndex)
                .Select(c => c.box)
                .ToList();

            return ordered;
        }

        // 현재 선택(exclude)을 제외하고 동일 지점의 최적 후보 반환
        private BoundingBox GetBestCandidateAtExcluding(System.Drawing.Point viewLocation, BoundingBox exclude)
        {
            var imageLocation = ViewToImage(new PointF(viewLocation.X, viewLocation.Y));
            var currentFrameBoxes = boundingBoxes.Where(b => b.FrameIndex == currentFrameIndex && !b.IsDeleted && b != exclude).ToList();
            const int hitMargin = 2;

            var candidates = new List<(BoundingBox box, bool inActiveWaypoint, int labelPri, double dist, int area, int zIndex)>();

            foreach (var box in currentFrameBoxes)
            {
                var waypoint = waypointMarkers.FirstOrDefault(w =>
                    w.ObjectId == GetBoxId(box) &&
                    w.Label == box.Label);

                if (waypoint != null)
                {
                    if (currentFrameIndex < waypoint.EntryFrame || currentFrameIndex > waypoint.ExitFrame)
                        continue;
                }

                var r = box.Rectangle;
                r.Inflate(hitMargin, hitMargin);
                if (!r.Contains((int)imageLocation.X, (int)imageLocation.Y))
                    continue;

                bool inActiveWaypoint = false;
                if (selectedWaypoint != null)
                {
                    inActiveWaypoint = (box.Label == selectedWaypoint.Label &&
                                        GetBoxId(box) == selectedWaypoint.ObjectId &&
                                        currentFrameIndex >= selectedWaypoint.EntryFrame &&
                                        currentFrameIndex <= selectedWaypoint.ExitFrame);
                }

                int labelPri = GetLabelPriority(box.Label);
                var centerX = box.Rectangle.X + box.Rectangle.Width / 2.0;
                var centerY = box.Rectangle.Y + box.Rectangle.Height / 2.0;
                var dx = centerX - imageLocation.X;
                var dy = centerY - imageLocation.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                int area = Math.Max(1, box.Rectangle.Width * box.Rectangle.Height);
                int zIndex = boundingBoxes.IndexOf(box);

                candidates.Add((box, inActiveWaypoint, labelPri, dist, area, zIndex));
            }

            if (candidates.Count == 0)
                return null;

            var ordered = candidates
                .OrderByDescending(c => c.inActiveWaypoint)
                .ThenByDescending(c => c.labelPri)
                .ThenBy(c => c.dist)
                .ThenBy(c => c.area)
                .ThenByDescending(c => c.zIndex)
                .ToList();

            return ordered[0].box;
        }

        // Tab/Shift+Tab 선택 사이클링
        private void CycleSelection(bool reverse)
        {
            if (lastHitCandidates == null || lastHitCandidates.Count == 0)
                return;

            if (reverse)
            {
                lastHitIndex = (lastHitIndex - 1 + lastHitCandidates.Count) % lastHitCandidates.Count;
            }
            else
            {
                lastHitIndex = (lastHitIndex + 1) % lastHitCandidates.Count;
            }

            selectedBox = lastHitCandidates[lastHitIndex];
            HighlightSelectedBoxInSidebar();
            UpdateObjectInfo(selectedBox);
            UpdateBboxListDisplay();
            pictureBoxVideo.Invalidate();
        }

        // ✅ 크기 조정 핸들 그리기 (4개 모서리만)
        private void DrawResizeHandles(Graphics g, RectangleF rect)
        {
            Color handleColor = Color.White;
            Color borderColor = Color.Black;
            
            // 4개 모서리 좌표 계산
            PointF topLeft = new PointF(rect.X, rect.Y);
            PointF topRight = new PointF(rect.X + rect.Width, rect.Y);
            PointF bottomLeft = new PointF(rect.X, rect.Y + rect.Height);
            PointF bottomRight = new PointF(rect.X + rect.Width, rect.Y + rect.Height);
            
            // 핸들 그리기
            DrawHandle(g, topLeft, handleColor, borderColor);
            DrawHandle(g, topRight, handleColor, borderColor);
            DrawHandle(g, bottomLeft, handleColor, borderColor);
            DrawHandle(g, bottomRight, handleColor, borderColor);
        }
        
        private void DrawHandle(Graphics g, PointF center, Color fillColor, Color borderColor)
        {
            float halfSize = HANDLE_SIZE / 2f;
            RectangleF handleRect = new RectangleF(
                center.X - halfSize,
                center.Y - halfSize,
                HANDLE_SIZE,
                HANDLE_SIZE
            );
            
            using (SolidBrush brush = new SolidBrush(fillColor))
                g.FillRectangle(brush, handleRect);
            
            using (Pen pen = new Pen(borderColor, 2))
                g.DrawRectangle(pen, handleRect.X, handleRect.Y, handleRect.Width, handleRect.Height);
        }

        // ✅ Person 박스 속성 정보 표시
        private void DrawPersonAttributes(Graphics g, BoundingBox box, RectangleF viewRect)
        {
            try
            {
                // 속성 가져오기
                var attributes = personAttributeStore.GetAllAttributes(box.PersonId, box.FrameIndex, waypointMarkers);
                if (attributes == null || attributes.Count == 0)
                    return;

                // 값이 있는 속성만 필터링
                var nonNullAttributes = attributes.Where(kvp => kvp.Value != null).ToList();
                if (nonNullAttributes.Count == 0)
                    return;

                // 속성 텍스트 생성 (한국어로 표시)
                var attributeTexts = nonNullAttributes.Select(kvp => 
                {
                    string attrName = kvp.Key;
                    string englishValue = kvp.Value?.ToString() ?? "";
                    string koreanValue = GetAttributeValueKorean(englishValue);
                    
                    return $"{attrName}:{koreanValue}";
                }).ToList();

                if (attributeTexts.Count == 0)
                    return;

                // 텍스트 크기 측정
                string sampleText = attributeTexts[0];
                SizeF textSize = g.MeasureString(sampleText, attributeFont);
                float lineHeight = textSize.Height + 2; // 줄 간격
                float totalHeight = lineHeight * attributeTexts.Count;
                float maxWidth = attributeTexts.Max(t => g.MeasureString(t, attributeFont).Width);

                // 오른쪽에 표시할 공간 확인
                float rightSpace = pictureBoxVideo.Width - (viewRect.X + viewRect.Width);
                float leftSpace = viewRect.X;
                bool showOnRight = rightSpace >= maxWidth + 10; // 여유 공간 포함

                float startX = showOnRight ? viewRect.X + viewRect.Width + 5 : viewRect.X - maxWidth - 5;
                float startY = viewRect.Y;

                // 배경 그리기
                RectangleF bgRect = new RectangleF(
                    startX - 2,
                    startY - 2,
                    maxWidth + 4,
                    totalHeight + 4
                );

                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(220, Color.Black)))
                    g.FillRectangle(bgBrush, bgRect);

                // 속성 텍스트 그리기
                using (SolidBrush textBrush = new SolidBrush(Color.White))
                {
                    for (int i = 0; i < attributeTexts.Count; i++)
                    {
                        g.DrawString(attributeTexts[i], attributeFont, textBrush, startX, startY + i * lineHeight);
                    }
                }
            }
            catch (Exception ex)
            {
                // 속성 표시 중 오류 발생 시 조용히 처리
                System.Diagnostics.Debug.WriteLine($"[DrawPersonAttributes 오류] {ex.Message}");
            }
        }
        
        // ✅ 마우스 위치에서 크기 조정 핸들 감지 (4개 모서리만)
        private ResizeHandle GetResizeHandleAtPoint(System.Drawing.Point viewPoint, RectangleF viewRect)
        {
            float tolerance = HANDLE_SIZE / 2f + 2; // 클릭 허용 범위
            
            // 좌상단 모서리
            PointF topLeft = new PointF(viewRect.X, viewRect.Y);
            if (Distance(viewPoint, topLeft) <= tolerance)
                return ResizeHandle.TopLeft;
            
            // 우상단 모서리
            PointF topRight = new PointF(viewRect.X + viewRect.Width, viewRect.Y);
            if (Distance(viewPoint, topRight) <= tolerance)
                return ResizeHandle.TopRight;
            
            // 좌하단 모서리
            PointF bottomLeft = new PointF(viewRect.X, viewRect.Y + viewRect.Height);
            if (Distance(viewPoint, bottomLeft) <= tolerance)
                return ResizeHandle.BottomLeft;
            
            // 우하단 모서리
            PointF bottomRight = new PointF(viewRect.X + viewRect.Width, viewRect.Y + viewRect.Height);
            if (Distance(viewPoint, bottomRight) <= tolerance)
                return ResizeHandle.BottomRight;
            
            return ResizeHandle.None;
        }
        
        // 두 점 사이의 거리 계산
        private float Distance(PointF p1, PointF p2)
        {
            float dx = p1.X - p2.X;
            float dy = p1.Y - p2.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
        
        // ✅ 크기 조정 수행 (10x10 최소 크기 적용, 모서리로 너비+높이 동시 조절)
        private void PerformResize(System.Drawing.Point currentViewPoint)
        {
            if (selectedBox == null || currentResizeHandle == ResizeHandle.None)
                return;
            
            // 뷰 좌표 차이 계산
            int deltaX = currentViewPoint.X - resizeStartPoint.X;
            int deltaY = currentViewPoint.Y - resizeStartPoint.Y;
            
            // 뷰 좌표 차이를 이미지 좌표 차이로 변환
            var viewDelta = new PointF(deltaX, deltaY);
            var imageDelta = ViewToImageDistance(viewDelta);
            
            // 원본 Rectangle로 시작
            Rectangle newRect = originalResizeRect;
            
            switch (currentResizeHandle)
            {
                case ResizeHandle.TopLeft:
                    // 좌상단 모서리: X, Y, Width, Height 모두 변경
                    int newLeft = originalResizeRect.X + (int)imageDelta.X;
                    int newTop = originalResizeRect.Y + (int)imageDelta.Y;
                    int newWidth = originalResizeRect.Right - newLeft;
                    int newHeight = originalResizeRect.Bottom - newTop;
                    
                    if (newWidth >= MIN_BBOX_SIZE && newHeight >= MIN_BBOX_SIZE)
                    {
                        newRect.X = newLeft;
                        newRect.Y = newTop;
                        newRect.Width = newWidth;
                        newRect.Height = newHeight;
                    }
                    else
                    {
                        // 최소 크기 유지
                        if (newWidth < MIN_BBOX_SIZE)
                        {
                            newRect.X = originalResizeRect.Right - MIN_BBOX_SIZE;
                            newRect.Width = MIN_BBOX_SIZE;
                        }
                        else
                        {
                            newRect.X = newLeft;
                            newRect.Width = newWidth;
                        }
                        
                        if (newHeight < MIN_BBOX_SIZE)
                        {
                            newRect.Y = originalResizeRect.Bottom - MIN_BBOX_SIZE;
                            newRect.Height = MIN_BBOX_SIZE;
                        }
                        else
                        {
                            newRect.Y = newTop;
                            newRect.Height = newHeight;
                        }
                    }
                    break;
                    
                case ResizeHandle.TopRight:
                    // 우상단 모서리: Y, Width, Height 변경
                    newTop = originalResizeRect.Y + (int)imageDelta.Y;
                    newWidth = originalResizeRect.Width + (int)imageDelta.X;
                    newHeight = originalResizeRect.Bottom - newTop;
                    
                    if (newWidth >= MIN_BBOX_SIZE && newHeight >= MIN_BBOX_SIZE)
                    {
                        newRect.Y = newTop;
                        newRect.Width = newWidth;
                        newRect.Height = newHeight;
                    }
                    else
                    {
                        newRect.Width = Math.Max(newWidth, MIN_BBOX_SIZE);
                        
                        if (newHeight < MIN_BBOX_SIZE)
                        {
                            newRect.Y = originalResizeRect.Bottom - MIN_BBOX_SIZE;
                            newRect.Height = MIN_BBOX_SIZE;
                        }
                        else
                        {
                            newRect.Y = newTop;
                            newRect.Height = newHeight;
                        }
                    }
                    break;
                    
                case ResizeHandle.BottomLeft:
                    // 좌하단 모서리: X, Width, Height 변경
                    newLeft = originalResizeRect.X + (int)imageDelta.X;
                    newWidth = originalResizeRect.Right - newLeft;
                    newHeight = originalResizeRect.Height + (int)imageDelta.Y;
                    
                    if (newWidth >= MIN_BBOX_SIZE && newHeight >= MIN_BBOX_SIZE)
                    {
                        newRect.X = newLeft;
                        newRect.Width = newWidth;
                        newRect.Height = newHeight;
                    }
                    else
                    {
                        if (newWidth < MIN_BBOX_SIZE)
                        {
                            newRect.X = originalResizeRect.Right - MIN_BBOX_SIZE;
                            newRect.Width = MIN_BBOX_SIZE;
                        }
                        else
                        {
                            newRect.X = newLeft;
                            newRect.Width = newWidth;
                        }
                        
                        newRect.Height = Math.Max(newHeight, MIN_BBOX_SIZE);
                    }
                    break;
                    
                case ResizeHandle.BottomRight:
                    // 우하단 모서리: Width, Height만 변경
                    newWidth = originalResizeRect.Width + (int)imageDelta.X;
                    newHeight = originalResizeRect.Height + (int)imageDelta.Y;
                    
                    newRect.Width = Math.Max(newWidth, MIN_BBOX_SIZE);
                    newRect.Height = Math.Max(newHeight, MIN_BBOX_SIZE);
                    break;
            }
            
            selectedBox.Rectangle = newRect;
        }
        
        // ✅ 뷰 좌표 거리를 이미지 좌표 거리로 변환
        private PointF ViewToImageDistance(PointF viewDistance)
        {
            if (pictureBoxVideo.Image == null)
                return viewDistance;
            
            float scaleX = (float)pictureBoxVideo.Image.Width / pictureBoxVideo.ClientSize.Width;
            float scaleY = (float)pictureBoxVideo.Image.Height / pictureBoxVideo.ClientSize.Height;
            
            return new PointF(viewDistance.X * scaleX, viewDistance.Y * scaleY);
        }
        
        // ✅ 핸들에 따라 커서 변경 (모서리용 대각선 커서)
        private void UpdateCursorForHandle(ResizeHandle handle)
        {
            pictureBoxVideo.Cursor = handle switch
            {
                ResizeHandle.TopLeft => Cursors.SizeNWSE,      // ↖↘ 좌상-우하 대각선
                ResizeHandle.BottomRight => Cursors.SizeNWSE,  // ↖↘ 좌상-우하 대각선
                ResizeHandle.TopRight => Cursors.SizeNESW,     // ↗↙ 우상-좌하 대각선
                ResizeHandle.BottomLeft => Cursors.SizeNESW,   // ↗↙ 우상-좌하 대각선
                _ => Cursors.Default
            };
        }

        private Color GetColorForLabel(string label)
        {
            return label.ToLower() switch
            {
                "person" => Color.FromArgb(236, 72, 153),
                "vehicle" => Color.FromArgb(59, 130, 246),
                "event" => Color.FromArgb(34, 197, 94),
                _ => Color.Yellow
            };
        }

        // ✅ COCO 라벨을 애플리케이션 라벨로 변환 (car, motorcycle, bus 등 → vehicle)
        private string ConvertCocoLabelToAppLabel(string cocoLabel)
        {
            if (string.IsNullOrEmpty(cocoLabel))
                return "person"; // 기본값
            
            string lowerLabel = cocoLabel.ToLower();
            
            // Person 관련
            if (lowerLabel == "person")
                return "person";
            
            // Vehicle 관련 (car, motorcycle, bus, truck 등)
            if (lowerLabel == "car" || lowerLabel == "motorcycle" || lowerLabel == "bus" || 
                lowerLabel == "truck" || lowerLabel == "bicycle" || lowerLabel == "train" ||
                lowerLabel == "boat" || lowerLabel == "airplane")
                return "vehicle";
            
            // Event 관련은 기본적으로 event로 유지
            if (lowerLabel == "event")
                return "event";
            
            // 기본값은 person
            return "person";
        }

        private void UpdateBoxCount()
        {
            // ✅ 삭제되지 않은 박스만 카운트
            int activeCount = boundingBoxes.Count(b => !b.IsDeleted);
            labelBoxCount.Text = $"박스 개수: {activeCount}";
        }

        private string FormatFrameTime(int frameIndex)
        {
            TimeSpan time = TimeSpan.FromSeconds(frameIndex / fps);
            return time.ToString(@"hh\:mm\:ss");
        }

        private void UpdateWaypointListView()
        {
            listViewPersonWaypoints.Items.Clear();
            listViewVehicleWaypoints.Items.Clear();
            listViewEventWaypoints.Items.Clear();

            foreach (var waypoint in waypointMarkers)
            {
                var item = new ListViewItem(waypoint.EntryTime);
                item.SubItems.Add(waypoint.ExitTime);
                
                // Label별로 category name 표시
                if (waypoint.Label == "person")
                {
                    // ✅ waypoint의 ObjectId를 직접 사용 (PersonId가 이미 저장되어 있음)
                    string categoryName = GetCategoryName("person", waypoint.ObjectId);
                    item.SubItems.Add(categoryName);
                
                    item.ForeColor = waypoint.MarkerColor;
                    item.Tag = waypoint;
                    listViewPersonWaypoints.Items.Add(item);
                }
                else if (waypoint.Label == "vehicle")
                {
                    // Vehicle: 단일 프레임의 Vehicle category name 표시
                    var vehicleBox = boundingBoxes
                        .FirstOrDefault(b => b.Label == "vehicle" && b.FrameIndex == waypoint.EntryFrame);
                    
                    if (vehicleBox != null)
                    {
                        // ✅ 고유 번호 형식으로 표시 (car, motorcycle, e_scooter, bicycle)
                        string categoryName = GetCategoryName("vehicle", vehicleBox.VehicleId);
                        item.SubItems.Add(categoryName);
                    }
                    else
                    {
                        item.SubItems.Add("car");
                    }
                    
                    item.ForeColor = waypoint.MarkerColor;
                    item.Tag = waypoint;
                    listViewVehicleWaypoints.Items.Add(item);
                }
                else if (waypoint.Label == "event")
                {
                    // Event: [Event, Frame Time, 객체(P/V)] 형식으로 표시
                    var eventBox = boundingBoxes
                        .FirstOrDefault(b => b.Label == "event" && 
                                           b.FrameIndex >= waypoint.EntryFrame && 
                                           b.FrameIndex <= waypoint.ExitFrame);

                    string eventName = "contact";
                    if (eventBox != null)
                    {
                        eventName = GetCategoryName("event", eventBox.EventId);
                    }

                    // 첫 컬럼: Event 이름
                    item = new ListViewItem(eventName);
                    // 두 번째: timestamp (JSON images[].timestamp에서 복원, 없으면 자막/EntryTime)
                    string ts = null;
                    if (frameTimestampMap.TryGetValue(waypoint.EntryFrame, out var jsonTs))
                        ts = jsonTs;
                    if (string.IsNullOrEmpty(ts))
                        ts = GetSubtitleTimestampForFrame(waypoint.EntryFrame);
                    item.SubItems.Add(!string.IsNullOrEmpty(ts) ? ts : waypoint.EntryTime);
                    // 세 번째: 객체(P/V) 텍스트
                    item.SubItems.Add(waypoint.InteractingObject ?? "");

                    item.ForeColor = waypoint.MarkerColor;
                    item.Tag = waypoint;
                    listViewEventWaypoints.Items.Add(item);
                }
            }
            
            // ✅ Waypoint 수에 따라 패널 높이 동적 조정
            UpdateWaypointPanelHeights();
        }
        
        // ✅ Waypoint 패널 높이를 동적으로 조정
        private void UpdateWaypointPanelHeights()
        {
            const int MAX_LISTVIEW_HEIGHT = 220; // 최대 ListView 높이
            const int MAX_GROUPBOX_HEIGHT = 250; // 최대 GroupBox 높이
            const int ITEM_HEIGHT = 23; // ListView 항목 높이 (대략)
            const int HEADER_HEIGHT = 23; // ListView 헤더 높이 (항목 높이와 동일하게)
            const int PADDING = 30; // GroupBox 내부 여백 (상단 25 + 하단 5)
            const int MIN_VISIBLE_ITEMS = 3; // 최소 보여질 항목 수
            
            // Person Waypoint 패널 높이 조정
            int personItemCount = listViewPersonWaypoints.Items.Count;
            int personListViewHeight;
            if (personItemCount == 0)
            {
                // ✅ waypoint가 없어도 3개 항목이 보이는 공간 설정
                personListViewHeight = HEADER_HEIGHT + (MIN_VISIBLE_ITEMS * ITEM_HEIGHT);
            }
            else if (personItemCount < MIN_VISIBLE_ITEMS)
            {
                // 3개 미만일 때는 최소 3개가 보이는 크기로 설정 (헤더 + 3개 항목)
                personListViewHeight = HEADER_HEIGHT + (MIN_VISIBLE_ITEMS * ITEM_HEIGHT);
            }
            else
            {
                // 3개 이상일 때는 실제 항목 수에 맞게 높이 설정, 최대값 제한
                personListViewHeight = Math.Min(HEADER_HEIGHT + (personItemCount * ITEM_HEIGHT), MAX_LISTVIEW_HEIGHT);
            }
            
            int personGroupBoxHeight = personListViewHeight + PADDING;
            personGroupBoxHeight = Math.Min(personGroupBoxHeight, MAX_GROUPBOX_HEIGHT);
            
            listViewPersonWaypoints.Height = personListViewHeight;
            groupBoxPersonWaypoint.Height = personGroupBoxHeight;
            
            // Vehicle Waypoint 패널 높이 조정
            int vehicleItemCount = listViewVehicleWaypoints.Items.Count;
            int vehicleListViewHeight;
            if (vehicleItemCount == 0)
            {
                // ✅ waypoint가 없어도 3개 항목이 보이는 공간 설정
                vehicleListViewHeight = HEADER_HEIGHT + (MIN_VISIBLE_ITEMS * ITEM_HEIGHT);
            }
            else if (vehicleItemCount < MIN_VISIBLE_ITEMS)
            {
                // 3개 미만일 때는 최소 3개가 보이는 크기로 설정 (헤더 + 3개 항목)
                vehicleListViewHeight = HEADER_HEIGHT + (MIN_VISIBLE_ITEMS * ITEM_HEIGHT);
            }
            else
            {
                // 3개 이상일 때는 실제 항목 수에 맞게 높이 설정, 최대값 제한
                vehicleListViewHeight = Math.Min(HEADER_HEIGHT + (vehicleItemCount * ITEM_HEIGHT), MAX_LISTVIEW_HEIGHT);
            }
            
            int vehicleGroupBoxHeight = vehicleListViewHeight + PADDING;
            vehicleGroupBoxHeight = Math.Min(vehicleGroupBoxHeight, MAX_GROUPBOX_HEIGHT);
            
            listViewVehicleWaypoints.Height = vehicleListViewHeight;
            groupBoxVehicleWaypoint.Height = vehicleGroupBoxHeight;
            
            // Event Waypoint 패널 높이 조정
            int eventItemCount = listViewEventWaypoints.Items.Count;
            int eventListViewHeight;
            if (eventItemCount == 0)
            {
                // ✅ waypoint가 없어도 3개 항목이 보이는 공간 설정
                eventListViewHeight = HEADER_HEIGHT + (MIN_VISIBLE_ITEMS * ITEM_HEIGHT);
            }
            else if (eventItemCount < MIN_VISIBLE_ITEMS)
            {
                // 3개 미만일 때는 최소 3개가 보이는 크기로 설정 (헤더 + 3개 항목)
                eventListViewHeight = HEADER_HEIGHT + (MIN_VISIBLE_ITEMS * ITEM_HEIGHT);
            }
            else
            {
                // 3개 이상일 때는 실제 항목 수에 맞게 높이 설정, 최대값 제한
                eventListViewHeight = Math.Min(HEADER_HEIGHT + (eventItemCount * ITEM_HEIGHT), MAX_LISTVIEW_HEIGHT);
            }
            
            int eventGroupBoxHeight = eventListViewHeight + PADDING;
            eventGroupBoxHeight = Math.Min(eventGroupBoxHeight, MAX_GROUPBOX_HEIGHT);
            
            listViewEventWaypoints.Height = eventListViewHeight;
            groupBoxEventWaypoint.Height = eventGroupBoxHeight;
            
            // ✅ 다음 패널들의 위치 업데이트 (상단 기준 정렬)
            int currentY = 0; // 시작 Y 위치 (패널 Padding이 있으므로 0으로 시작)
            
            // Person Waypoint 위치
            groupBoxPersonWaypoint.Location = new System.Drawing.Point(12, currentY);
            currentY += groupBoxPersonWaypoint.Height + 20; // 패널 높이 + 여백
            
            // Vehicle Waypoint 위치
            groupBoxVehicleWaypoint.Location = new System.Drawing.Point(12, currentY);
            currentY += groupBoxVehicleWaypoint.Height + 20; // 패널 높이 + 여백
            
            // Event Waypoint 위치
            groupBoxEventWaypoint.Location = new System.Drawing.Point(12, currentY);
            currentY += groupBoxEventWaypoint.Height + 20; // 패널 높이 + 여백
            
            // 삭제 버튼 위치
            btnDeleteEventWaypoint.Location = new System.Drawing.Point(12, currentY);
            currentY += btnDeleteEventWaypoint.Height + 20; // 버튼 높이 + 여백
            
            // Labels 패널 위치
            groupBoxLabels.Location = new System.Drawing.Point(12, currentY);
        }

        private void UpdateObjectInfo(BoundingBox box)
        {
            if (box == null)
            {
                // ✅ 박스가 null일 때 waypoint 정보 확인
                if (selectedWaypoint != null)
                {
                    UpdateWaypointInfo(selectedWaypoint);
                }
                else
                {
                    labelObjectLabel.Text = "Label: -";
                    labelPrevWaypoint.Text = "Previous Waypoint: -";
                    labelNextWaypoint.Text = "Next Waypoint: -";
                }
                return;
            }
            
            string labelText = "";
            if (box.Label == "person")
            {
                labelText = $"Label: person_{box.PersonId:D2}";
            }
            else if (box.Label == "vehicle")
            {
                string[] vehicleTypes = { "car", "motorcycle", "e_scooter", "bicycle" };
                if (box.VehicleId > 0 && box.VehicleId <= vehicleTypes.Length)
                    labelText = $"Label: vehicle_{vehicleTypes[box.VehicleId - 1]}";
                else
                    labelText = $"Label: vehicle_{box.VehicleId}";
            }
            else if (box.Label == "event")
            {
                string[] eventTypes = { "contact", "exchange", "board", "final_exchange", "throw" };
                if (box.EventId > 0 && box.EventId <= eventTypes.Length)
                    labelText = $"Label: event_{eventTypes[box.EventId - 1]}";
                else
                    labelText = $"Label: event_{box.EventId}";
            }
            
            // ✅ Person인 경우 속성 정보 추가 표시 (선택사항)
            if (box.Label == "person")
            {
                var attributes = personAttributeStore.GetAllAttributes(box.PersonId, box.FrameIndex, waypointMarkers);
                if (attributes != null && attributes.Count > 0)
                {
                    var nonNullAttributes = attributes.Where(kvp => kvp.Value != null).ToList();
                    if (nonNullAttributes.Count > 0)
                    {
                        string attrSummary = string.Join(", ", nonNullAttributes.Take(3).Select(kvp => $"{kvp.Key}:{kvp.Value}"));
                        if (nonNullAttributes.Count > 3)
                        {
                            attrSummary += $" ... (+{nonNullAttributes.Count - 3} more)";
                        }
                        labelText += $"\n속성: {attrSummary}";
                    }
                }
            }
            
            labelObjectLabel.Text = labelText;
            
            // ✅ 선택된 박스가 속한 waypoint 정보 표시
            if (selectedWaypoint != null)
            {
                UpdateWaypointInfo(selectedWaypoint);
            }
            else
            {
                // 박스가 있지만 waypoint가 선택되지 않은 경우 이전/다음 waypoint만 표시
                UpdateWaypointInfo(null);
            }
        }
        
        // ✅ Waypoint 정보 표시 함수 (이전/다음 waypoint 포함)
        private void UpdateWaypointInfo(WaypointMarker waypoint)
        {
            if (waypoint == null)
            {
                // Waypoint가 선택되지 않은 경우 이전/다음 waypoint 정보만 표시
                labelPrevWaypoint.Text = "Previous Waypoint: -";
                labelNextWaypoint.Text = "Next Waypoint: -";
                return;
            }
            
            // ✅ 현재 waypoint의 라벨 표시 (이미 labelObjectLabel에 표시되어 있으면 그대로 유지)
            string categoryName = GetCategoryName(waypoint.Label, waypoint.ObjectId);
            if (selectedBox == null)
            {
                // 박스가 선택되지 않은 경우 waypoint 라벨 표시
                labelObjectLabel.Text = $"Label: {categoryName}";
            }
            
            // ✅ 같은 타입의 waypoint들 중에서 이전/다음 waypoint 찾기
            var sameTypeWaypoints = waypointMarkers
                .Where(w => w.Label == waypoint.Label && w.ObjectId == waypoint.ObjectId)
                .OrderBy(w => w.EntryFrame)
                .ToList();
            
            int currentIndex = sameTypeWaypoints.FindIndex(w => 
                w.EntryFrame == waypoint.EntryFrame && 
                w.ExitFrame == waypoint.ExitFrame);
            
            if (currentIndex >= 0)
            {
                // 이전 waypoint 찾기
                if (currentIndex > 0)
                {
                    var prevWaypoint = sameTypeWaypoints[currentIndex - 1];
                    string prevCategoryName = GetCategoryName(prevWaypoint.Label, prevWaypoint.ObjectId);
                    labelPrevWaypoint.Text = $"Previous Waypoint: {prevCategoryName}, {prevWaypoint.EntryTime} - {prevWaypoint.ExitTime}";
                }
                else
                {
                    labelPrevWaypoint.Text = "Previous Waypoint: -";
                }
                
                // 다음 waypoint 찾기
                if (currentIndex < sameTypeWaypoints.Count - 1)
                {
                    var nextWaypoint = sameTypeWaypoints[currentIndex + 1];
                    string nextCategoryName = GetCategoryName(nextWaypoint.Label, nextWaypoint.ObjectId);
                    labelNextWaypoint.Text = $"Next Waypoint: {nextCategoryName}, {nextWaypoint.EntryTime} - {nextWaypoint.ExitTime}";
                }
                else
                {
                    labelNextWaypoint.Text = "Next Waypoint: -";
                }
            }
            else
            {
                labelPrevWaypoint.Text = "Previous Waypoint: -";
                labelNextWaypoint.Text = "Next Waypoint: -";
            }
        }

        private void ShowVideoListForm()
        {
            if (videoFileList.Count == 0)
            {
                MessageBox.Show("로드된 비디오 파일이 없습니다.\n먼저 파일을 선택해주세요.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (videoListForm == null || videoListForm.IsDisposed)
            {
                videoListForm = new Form()
                {
                    Text = "Video List",
                    Width = 400,
                    Height = 500,
                    StartPosition = FormStartPosition.CenterScreen,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                videoListView = new ListView()
                {
                    Dock = DockStyle.Fill,
                    View = View.Details,
                    FullRowSelect = true,
                    GridLines = true
                };

                videoListView.Columns.Add("File Name", 350);
                videoListView.DoubleClick += VideoListView_DoubleClick;

                RefreshVideoListView();

                videoListForm.Controls.Add(videoListView);
                videoListForm.Show();
            }
            else
            {
                videoListForm.BringToFront();
            }
        }

        private void RefreshVideoListView()
        {
            if (videoListView == null)
                return;

            videoListView.Items.Clear();

            for (int i = 0; i < videoFileList.Count; i++)
            {
                string fileName = Path.GetFileName(videoFileList[i]);
                ListViewItem item = new ListViewItem(fileName);
                item.Tag = i;

                if (i == currentVideoIndex)
                {
                    item.BackColor = Color.FromArgb(59, 130, 246);
                    item.ForeColor = Color.White;
                }

                videoListView.Items.Add(item);
            }
        }

        private async void VideoListView_DoubleClick(object sender, EventArgs e)
        {
            if (videoListView.SelectedItems.Count == 0)
                return;

            int selectedIndex = (int)videoListView.SelectedItems[0].Tag;

            if (selectedIndex != currentVideoIndex)
            {
                // ✅ 비디오 전환 시: 데이터 백업 → 초기화 → 로드 (실패 시 복원)
                var backupBoxes = new List<BoundingBox>(boundingBoxes);
                var backupWaypoints = new List<WaypointMarker>(waypointMarkers);
                var backupSelectedBox = selectedBox;
                var backupUndoStack = new Stack<UndoAction>(undoStack.Reverse());
                var backupRedoStack = new Stack<UndoAction>(redoStack.Reverse());
                
                boundingBoxes.Clear();
                waypointMarkers.Clear();
                selectedBox = null;
                undoStack.Clear();
                redoStack.Clear();
                lastRenderedWaypoint = null;
                
                try
                {
                    currentVideoIndex = selectedIndex;
                    await LoadVideoWithSubtitle(videoFileList[currentVideoIndex]);
                    
                    // LoadVideoWithSubtitle 내부의 LoadLabelingData에서 새 데이터가 로드됨
                UpdateBoxCount();
                UpdateWaypointListView();
                pictureBoxVideo.Invalidate();
                RefreshVideoListView();
                }
                catch (Exception ex)
                {
                    // ❌ 로드 실패 시 이전 데이터 복원
                    boundingBoxes.Clear();
                    boundingBoxes.AddRange(backupBoxes);
                    waypointMarkers.Clear();
                    waypointMarkers.AddRange(backupWaypoints);
                    selectedBox = backupSelectedBox;
                    undoStack.Clear();
                    foreach (var action in backupUndoStack) undoStack.Push(action);
                    redoStack.Clear();
                    foreach (var action in backupRedoStack) redoStack.Push(action);
                    
                    UpdateBoxCount();
                    UpdateWaypointListView();
                    pictureBoxVideo.Invalidate();
                    
                    MessageBox.Show($"영상 로드 실패:\n{ex.Message}\n\n이전 작업 내용이 복원되었습니다.", 
                        "영상 전환 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void panelLabelPerson_Click(object sender, EventArgs e)
        {
            currentSelectedLabel = "person";
            
            if (selectedBox != null)
            {
                ApplyLabelChange("person", currentAssignedId, selectedBox.Label, GetBoxId(selectedBox), selectedBox.Rectangle);
            }
            else
            {
                MessageBox.Show($"Person 라벨 선택됨. 현재 ID: {currentAssignedId}", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void panelLabelVehicle_Click(object sender, EventArgs e)
        {
            currentSelectedLabel = "vehicle";
            
            if (selectedBox != null)
            {
                ApplyLabelChange("vehicle", currentAssignedId, selectedBox.Label, GetBoxId(selectedBox), selectedBox.Rectangle);
            }
            else
            {
                MessageBox.Show($"Vehicle 라벨 선택됨. 현재 ID: {currentAssignedId}", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void panelLabelEvent_Click(object sender, EventArgs e)
        {
            currentSelectedLabel = "event";
            
            if (selectedBox != null)
            {
                ApplyLabelChange("event", currentAssignedId, selectedBox.Label, GetBoxId(selectedBox), selectedBox.Rectangle);
            }
            else
            {
                MessageBox.Show($"Event 라벨 선택됨. 현재 ID: {currentAssignedId}", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // 라벨 타입 선택 버튼 핸들러
        private void btnLabelPerson_Click(object sender, EventArgs e)
        {
            try
            {
                // ✅ YOLO 추적/탐지 중에는 라벨 선택 차단
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[라벨 버튼 차단] YOLO 추적/탐지 중이므로 라벨 선택 불가");
                    MessageBox.Show(
                        "YOLO 추적 또는 탐지가 진행 중입니다.\n작업이 완료될 때까지 기다려주세요.",
                        "작업 중",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                
                // 토글: 같은 버튼이 이미 선택되어 있으면 선택 해제
                if (currentSelectedLabel == "person")
                {
                    currentSelectedLabel = "";
                    btnLabelPerson.BackColor = System.Drawing.Color.FromArgb(252, 231, 243);
                    btnLabelPerson.FlatAppearance.BorderSize = 2;
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Person 라벨 버튼 오류] {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"라벨 선택 중 오류 발생:\n{ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            
            currentSelectedLabel = "person";
            currentAssignedId = 1; // 기본값 person_01
            
            // 버튼 시각적 상태 업데이트: person 선택, 나머지 해제
            btnLabelPerson.BackColor = System.Drawing.Color.FromArgb(236, 72, 153);
            btnLabelPerson.FlatAppearance.BorderSize = 3;
            btnLabelVehicle.BackColor = System.Drawing.Color.FromArgb(219, 234, 254);
            btnLabelVehicle.FlatAppearance.BorderSize = 2;
            btnLabelEvent.BackColor = System.Drawing.Color.FromArgb(220, 252, 231);
            btnLabelEvent.FlatAppearance.BorderSize = 2;
            
            // 선택된 bbox가 있으면 해당 박스를 person으로 변경
            if (selectedBox != null)
            {
                string oldLabel = selectedBox.Label;
                
                // ✅ Event → Person 변경 시 전파된 Event 박스 삭제
                if (oldLabel == "event")
                {
                    RemovePropagatedEventBoxes(selectedBox);
                }
                
                selectedBox.Label = "person";
                SetBoxId(selectedBox, "person", 1); // 기본값 person_01
                
                AddUndoAction(new UndoAction
                {
                    Type = UndoActionType.ModifyBox,
                    Box = CloneBoundingBox(selectedBox),
                    OriginalLabel = oldLabel
                });
                
                InvalidateBoxCache();
                pictureBoxVideo.Invalidate();
                UpdateBboxListDisplay();
                UpdateObjectInfo(selectedBox);
            }
        }

        private void btnLabelVehicle_Click(object sender, EventArgs e)
        {
            try
            {
                // ✅ YOLO 추적/탐지 중에는 라벨 선택 차단
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[라벨 버튼 차단] YOLO 추적/탐지 중이므로 라벨 선택 불가");
                    MessageBox.Show(
                        "YOLO 추적 또는 탐지가 진행 중입니다.\n작업이 완료될 때까지 기다려주세요.",
                        "작업 중",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                
                // 토글: 같은 버튼이 이미 선택되어 있으면 선택 해제
                if (currentSelectedLabel == "vehicle")
                {
                    currentSelectedLabel = "";
                    btnLabelVehicle.BackColor = System.Drawing.Color.FromArgb(219, 234, 254);
                    btnLabelVehicle.FlatAppearance.BorderSize = 2;
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Vehicle 라벨 버튼 오류] {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"라벨 선택 중 오류 발생:\n{ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            
            currentSelectedLabel = "vehicle";
            currentAssignedId = 1; // 기본값 vehicle_car
            
            // 버튼 시각적 상태 업데이트: vehicle 선택, 나머지 해제
            btnLabelPerson.BackColor = System.Drawing.Color.FromArgb(252, 231, 243);
            btnLabelPerson.FlatAppearance.BorderSize = 2;
            btnLabelVehicle.BackColor = System.Drawing.Color.FromArgb(59, 130, 246);
            btnLabelVehicle.FlatAppearance.BorderSize = 3;
            btnLabelEvent.BackColor = System.Drawing.Color.FromArgb(220, 252, 231);
            btnLabelEvent.FlatAppearance.BorderSize = 2;
            
            // 선택된 bbox가 있으면 해당 박스를 vehicle로 변경
            if (selectedBox != null)
            {
                string oldLabel = selectedBox.Label;
                
                // ✅ Event → Vehicle 변경 시 전파된 Event 박스 삭제
                if (oldLabel == "event")
                {
                    RemovePropagatedEventBoxes(selectedBox);
                }
                
                selectedBox.Label = "vehicle";
                SetBoxId(selectedBox, "vehicle", 1); // 기본값 vehicle_car
                
                AddUndoAction(new UndoAction
                {
                    Type = UndoActionType.ModifyBox,
                    Box = CloneBoundingBox(selectedBox),
                    OriginalLabel = oldLabel
                });
                
                InvalidateBoxCache();
                pictureBoxVideo.Invalidate();
                UpdateBboxListDisplay();
                UpdateObjectInfo(selectedBox);
            }
        }

        private void btnLabelEvent_Click(object sender, EventArgs e)
        {
            try
            {
                // ✅ YOLO 추적/탐지 중에는 라벨 선택 차단
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[라벨 버튼 차단] YOLO 추적/탐지 중이므로 라벨 선택 불가");
                    MessageBox.Show(
                        "YOLO 추적 또는 탐지가 진행 중입니다.\n작업이 완료될 때까지 기다려주세요.",
                        "작업 중",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                
                // 토글: 같은 버튼이 이미 선택되어 있으면 선택 해제
                if (currentSelectedLabel == "event")
                {
                    currentSelectedLabel = "";
                    btnLabelEvent.BackColor = System.Drawing.Color.FromArgb(220, 252, 231);
                    btnLabelEvent.FlatAppearance.BorderSize = 2;
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Event 라벨 버튼 오류] {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"라벨 선택 중 오류 발생:\n{ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            
            currentSelectedLabel = "event";
            currentAssignedId = 1; // 기본값 event_contact
            
            // 버튼 시각적 상태 업데이트: event 선택, 나머지 해제
            btnLabelPerson.BackColor = System.Drawing.Color.FromArgb(252, 231, 243);
            btnLabelPerson.FlatAppearance.BorderSize = 2;
            btnLabelVehicle.BackColor = System.Drawing.Color.FromArgb(219, 234, 254);
            btnLabelVehicle.FlatAppearance.BorderSize = 2;
            btnLabelEvent.BackColor = System.Drawing.Color.FromArgb(34, 197, 94);
            btnLabelEvent.FlatAppearance.BorderSize = 3;
            
            // 선택된 bbox가 있으면 해당 박스를 event로 변경
            if (selectedBox != null)
            {
                string oldLabel = selectedBox.Label;
                
                selectedBox.Label = "event";
                SetBoxId(selectedBox, "event", 1); // 기본값 event_contact
                
                AddUndoAction(new UndoAction
                {
                    Type = UndoActionType.ModifyBox,
                    Box = CloneBoundingBox(selectedBox),
                    OriginalLabel = oldLabel
                });
                
                InvalidateBoxCache();
                pictureBoxVideo.Invalidate();
                UpdateBboxListDisplay();
                UpdateObjectInfo(selectedBox);
                
                // Event: 즉시 전파/웨이포인트 생성하지 않음. Exit 확정 시 처리
            }
        }

        // Labels 패널 접기/펼치기 토글 함수들
        private void TogglePersonPanel(object sender, EventArgs e)
        {
            isPersonExpanded = !isPersonExpanded;
            panelPersonList.Visible = isPersonExpanded;
            labelPersonList.Text = isPersonExpanded ? "▽ person" : "> person";
            
            // 패널이 펼쳐질 때만 내용 업데이트
            if (isPersonExpanded)
            {
                UpdatePersonListDisplay();
            }
            
            // 레이아웃 재계산 (아래 요소들의 위치 조정)
            UpdateLabelsLayoutAfterToggle();
        }

        private void ToggleVehiclePanel(object sender, EventArgs e)
        {
            isVehicleExpanded = !isVehicleExpanded;
            panelVehicleList.Visible = isVehicleExpanded;
            labelVehicleList.Text = isVehicleExpanded ? "▽ vehicle" : "> vehicle";
            
            // 패널이 펼쳐질 때만 내용 업데이트
            if (isVehicleExpanded)
            {
                UpdateVehicleListDisplay();
            }
            
            // 레이아웃 재계산 (아래 요소들의 위치 조정)
            UpdateLabelsLayoutAfterToggle();
        }

        private void ToggleEventPanel(object sender, EventArgs e)
        {
            isEventExpanded = !isEventExpanded;
            panelEventList.Visible = isEventExpanded;
            labelEventList.Text = isEventExpanded ? "▽ event" : "> event";
            
            // 패널이 펼쳐질 때만 내용 업데이트
            if (isEventExpanded)
            {
                UpdateEventListDisplay();
            }
            
            // 레이아웃 재계산 (아래 요소들의 위치 조정)
            UpdateLabelsLayoutAfterToggle();
        }
        
        // Labels 패널 레이아웃 업데이트 (토글 시 요소들의 위치 동적 조정)
        private void UpdateLabelsLayoutAfterToggle()
        {
            const int startY = 70;
            const int toggleHeight = 30;
            const int panelHeight = 100;
            int currentY = startY;
            
            // Person 섹션
            currentY += toggleHeight; // Person 토글 버튼 다음
            if (isPersonExpanded)
                currentY += panelHeight; // Person 패널이 펼쳐져 있으면 그 높이만큼 추가
            
            // Vehicle 섹션
            labelVehicleList.Location = new System.Drawing.Point(8, currentY);
            currentY += toggleHeight;
            panelVehicleList.Location = new System.Drawing.Point(8, currentY);
            if (isVehicleExpanded)
                currentY += panelHeight;
            
            // Event 섹션
            labelEventList.Location = new System.Drawing.Point(8, currentY);
            currentY += toggleHeight;
            panelEventList.Location = new System.Drawing.Point(8, currentY);
            if (isEventExpanded)
                currentY += panelHeight;
            
            // 하단 버튼들
            currentY += 5; // 약간의 여백
            btnDeleteLabel.Location = new System.Drawing.Point(8, currentY);
            currentY += 42; // 삭제 버튼 높이 + 여백
            btnExportJsonInLabels.Location = new System.Drawing.Point(8, currentY);
            
            // Labels 패널 전체 높이 조정
            currentY += 50; // JSON 저장 버튼 높이 + 여백
            groupBoxLabels.Height = Math.Max(260, currentY);
        }

        // 성능 최적화: 박스 라벨 텍스트 생성 (재사용 가능한 배열 사용)
        private static readonly string[] VehicleTypes = { "car", "motorcycle", "e_scooter", "bicycle" };
        private static readonly string[] EventTypes = { "contact", "exchange", "board", "final_exchange" };
        
        private string GetBoxLabelText(BoundingBox box)
        {
            if (box.Label == "person")
            {
                return $"person_{box.PersonId:D2}";
            }
            else if (box.Label == "vehicle")
            {
                if (box.VehicleId > 0 && box.VehicleId <= VehicleTypes.Length)
                    return $"vehicle_{VehicleTypes[box.VehicleId - 1]}";
                else
                    return $"vehicle_{box.VehicleId}";
            }
            else if (box.Label == "event")
            {
                if (box.EventId > 0 && box.EventId <= EventTypes.Length)
                    return $"event_{EventTypes[box.EventId - 1]}";
                else
                    return $"event_{box.EventId}";
            }
            return "";
        }
        
        // 성능 최적화: 박스 데이터가 변경되면 캐시 무효화
        private void InvalidateBoxCache()
        {
            lastCachedFrameForPaint = -1;
            cachedCurrentFrameBoxes.Clear();
        }
        
        // bbox 리스트 업데이트가 필요한지 확인 (리소스 최적화)
        private bool ShouldUpdateBboxList(int frameIndex)
        {
            // 현재 프레임이 속한 waypoint 찾기
            var currentWaypoint = waypointMarkers.FirstOrDefault(w =>
                frameIndex >= w.EntryFrame &&
                frameIndex <= w.ExitFrame);
            
            // Waypoint가 변경되었는지 확인
            bool waypointChanged = false;
            
            if (currentWaypoint == null && lastRenderedWaypoint == null)
            {
                // 둘 다 null이면 변경 없음
                waypointChanged = false;
            }
            else if (currentWaypoint == null || lastRenderedWaypoint == null)
            {
                // 하나만 null이면 변경됨
                waypointChanged = true;
            }
            else
            {
                // 둘 다 null이 아니면 EntryFrame과 ExitFrame으로 비교
                waypointChanged = (currentWaypoint.EntryFrame != lastRenderedWaypoint.EntryFrame ||
                                  currentWaypoint.ExitFrame != lastRenderedWaypoint.ExitFrame);
            }
            
            if (waypointChanged)
            {
                lastRenderedWaypoint = currentWaypoint;
                return true;
            }
            
            return false;
        }
        
        // 3개 bbox 목록을 동적으로 생성하여 표시 (현재 프레임 기준)
        private void UpdateBboxListDisplay()
        {
            // 펼쳐진 패널만 업데이트 (성능 최적화)
            if (isPersonExpanded)
                UpdatePersonListDisplay();
            if (isVehicleExpanded)
                UpdateVehicleListDisplay();
            if (isEventExpanded)
                UpdateEventListDisplay();
        }
        
        // Person 리스트 표시 (현재 프레임의 Person bbox)
        private void UpdatePersonListDisplay()
        {
            panelPersonList.Controls.Clear();
            
            // ✅ 패널 자체 클릭 시 선택 해제
            panelPersonList.Click += (s, e) =>
            {
                selectedBox = null;
                ClearSidebarHighlights();
                UpdateObjectInfo(null);
                pictureBoxVideo.Invalidate();
            };
            
            var currentBoxes = boundingBoxes
                .Where(b => b.FrameIndex == currentFrameIndex && b.Label == "person" && !b.IsDeleted)
                .ToList();
            
            if (currentBoxes.Count == 0)
            {
                Label emptyLabel = new Label
                {
                    Text = "현재 프레임에 Person 없음",
                    Font = new System.Drawing.Font("Segoe UI", 8F),
                    ForeColor = System.Drawing.Color.Gray,
                    Location = new System.Drawing.Point(5, 5),
                    AutoSize = true
                };
                panelPersonList.Controls.Add(emptyLabel);
                return;
            }
            
            int yPos = 5;
            foreach (var box in currentBoxes)
            {
                var currentBox = box;
                
                Panel itemPanel = new Panel
                {
                    Location = new System.Drawing.Point(5, yPos),
                    Size = new System.Drawing.Size(260, 65),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackColor = System.Drawing.Color.FromArgb(252, 231, 243),
                    Cursor = Cursors.Hand,
                    Tag = currentBox // ✅ 박스 정보를 Tag에 저장
                };
                
                Label itemLabel = new Label
                {
                    Text = $"person_{currentBox.PersonId:D2}",
                    Location = new System.Drawing.Point(8, 8),
                    Size = new System.Drawing.Size(244, 20),
                    Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.FromArgb(157, 23, 77),
                    BackColor = System.Drawing.Color.Transparent
                };
                
                ComboBox comboBox = new ComboBox
                {
                    Location = new System.Drawing.Point(8, 32),
                    Size = new System.Drawing.Size(244, 25),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new System.Drawing.Font("Segoe UI", 8F)
                };
                
                // ComboBox 호버 시 스크롤 방지
                comboBox.MouseWheel += (s, e) => ((HandledMouseEventArgs)e).Handled = true;
                
                for (int i = 1; i <= 30; i++)
                {
                    comboBox.Items.Add($"person_{i:D2}");
                }
                comboBox.SelectedItem = $"person_{currentBox.PersonId:D2}";
                
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    if (comboBox.SelectedItem != null)
                    {
                        string selected = comboBox.SelectedItem.ToString();
                        if (selected.StartsWith("person_"))
                        {
                            int newId = int.Parse(selected.Substring(7));
                            int oldId = currentBox.PersonId;
                            
                            // ✅ 해당 박스가 속한 waypoint 찾기
                            var waypoint = FindWaypointForBox(currentBox);
                            
                            if (waypoint != null && waypoint.Label == "person")
                            {
                                // ✅ waypoint 범위 내의 모든 person 박스의 PersonId 변경
                                var boxesToUpdate = boundingBoxes
                                    .Where(b => b.Label == "person" &&
                                               b.PersonId == oldId &&
                                               b.FrameIndex >= waypoint.EntryFrame &&
                                               b.FrameIndex <= waypoint.ExitFrame &&
                                               !b.IsDeleted)
                                    .ToList();
                                
                                foreach (var box in boxesToUpdate)
                                {
                                    SetBoxId(box, "person", newId);
                                    AddUndoAction(new UndoAction
                                    {
                                        Type = UndoActionType.ModifyBox,
                                        Box = CloneBoundingBox(box),
                                        OriginalLabel = "person",
                                        OriginalObjectId = oldId
                                    });
                                }
                                
                                // ✅ waypoint의 ObjectId도 변경
                                waypoint.ObjectId = newId;
                                
                                // ✅ waypoint 리스트 업데이트
                                UpdateWaypointListView();
                            }
                            else
                            {
                                // waypoint에 속하지 않은 경우 현재 박스만 변경
                                SetBoxId(currentBox, "person", newId);
                                AddUndoAction(new UndoAction
                                {
                                    Type = UndoActionType.ModifyBox,
                                    Box = CloneBoundingBox(currentBox),
                                    OriginalLabel = "person",
                                    OriginalObjectId = oldId
                                });
                            }
                            
                            UpdateObjectInfo(currentBox);
                            UpdateBboxListDisplay();
                            pictureBoxVideo.Invalidate();
                        }
                    }
                };
                
                itemPanel.Controls.Add(itemLabel);
                itemPanel.Controls.Add(comboBox);
                
                EventHandler clickHandler = (s, e) =>
                {
                    selectedBox = currentBox;
                    UpdateObjectInfo(selectedBox);
                    HighlightSelectedBoxInSidebar(); // ✅ 하이라이트 업데이트
                    pictureBoxVideo.Invalidate();
                };
                
                itemPanel.Click += clickHandler;
                itemLabel.Click += clickHandler;
                
                panelPersonList.Controls.Add(itemPanel);
                yPos += 70;
            }
        }
        
        // Vehicle 리스트 표시 (현재 프레임의 Vehicle bbox)
        private void UpdateVehicleListDisplay()
        {
            panelVehicleList.Controls.Clear();
            
            // ✅ 패널 자체 클릭 시 선택 해제
            panelVehicleList.Click += (s, e) =>
            {
                selectedBox = null;
                ClearSidebarHighlights();
                UpdateObjectInfo(null);
                pictureBoxVideo.Invalidate();
            };
            
            var currentBoxes = boundingBoxes
                .Where(b => b.FrameIndex == currentFrameIndex && b.Label == "vehicle" && !b.IsDeleted)
                .ToList();
            
            if (currentBoxes.Count == 0)
            {
                Label emptyLabel = new Label
                {
                    Text = "현재 프레임에 Vehicle 없음",
                    Font = new System.Drawing.Font("Segoe UI", 8F),
                    ForeColor = System.Drawing.Color.Gray,
                    Location = new System.Drawing.Point(5, 5),
                    AutoSize = true
                };
                panelVehicleList.Controls.Add(emptyLabel);
                return;
            }
            
            int yPos = 5;
            foreach (var box in currentBoxes)
            {
                var currentBox = box;
                string[] vehicleTypes = { "car", "motorcycle", "e_scooter", "bicycle" };
                string vehicleName = currentBox.VehicleId > 0 && currentBox.VehicleId <= vehicleTypes.Length 
                    ? vehicleTypes[currentBox.VehicleId - 1] 
                    : currentBox.VehicleId.ToString();
                
                Panel itemPanel = new Panel
                {
                    Location = new System.Drawing.Point(5, yPos),
                    Size = new System.Drawing.Size(260, 65),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackColor = System.Drawing.Color.FromArgb(219, 234, 254),
                    Cursor = Cursors.Hand,
                    Tag = currentBox // ✅ 박스 정보를 Tag에 저장
                };
                
                Label itemLabel = new Label
                {
                    Text = $"vehicle_{vehicleName}",
                    Location = new System.Drawing.Point(8, 8),
                    Size = new System.Drawing.Size(244, 20),
                    Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.FromArgb(30, 64, 175),
                    BackColor = System.Drawing.Color.Transparent
                };
                
                ComboBox comboBox = new ComboBox
                {
                    Location = new System.Drawing.Point(8, 32),
                    Size = new System.Drawing.Size(244, 25),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new System.Drawing.Font("Segoe UI", 8F)
                };
                
                // ComboBox 호버 시 스크롤 방지
                comboBox.MouseWheel += (s, e) => ((HandledMouseEventArgs)e).Handled = true;
                
                comboBox.Items.AddRange(new object[] { "vehicle_car", "vehicle_motorcycle", "vehicle_e_scooter", "vehicle_bicycle" });
                comboBox.SelectedItem = $"vehicle_{vehicleName}";
                
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    if (comboBox.SelectedItem != null)
                    {
                        string selected = comboBox.SelectedItem.ToString();
                        if (selected.StartsWith("vehicle_"))
                        {
                            string vType = selected.Substring(8);
                            int newVehicleId = Array.IndexOf(vehicleTypes, vType) + 1;
                            if (newVehicleId > 0)
                            {
                                int oldVehicleId = currentBox.VehicleId;
                                
                                // ✅ 해당 박스가 속한 waypoint 찾기
                                var waypoint = FindWaypointForBox(currentBox);
                                
                                if (waypoint != null && waypoint.Label == "vehicle")
                                {
                                    // ✅ waypoint 범위 내의 모든 vehicle 박스의 VehicleId 변경
                                    var boxesToUpdate = boundingBoxes
                                        .Where(b => b.Label == "vehicle" &&
                                                   b.VehicleId == oldVehicleId &&
                                                   b.FrameIndex >= waypoint.EntryFrame &&
                                                   b.FrameIndex <= waypoint.ExitFrame &&
                                                   !b.IsDeleted)
                                        .ToList();
                                    
                                    foreach (var box in boxesToUpdate)
                                    {
                                        SetBoxId(box, "vehicle", newVehicleId);
                                        AddUndoAction(new UndoAction
                                        {
                                            Type = UndoActionType.ModifyBox,
                                            Box = CloneBoundingBox(box),
                                            OriginalLabel = "vehicle",
                                            OriginalObjectId = oldVehicleId
                                        });
                                    }
                                    
                                    // ✅ waypoint의 ObjectId도 변경
                                    waypoint.ObjectId = newVehicleId;
                                    
                                    // ✅ waypoint 리스트 업데이트
                                    UpdateWaypointListView();
                                }
                                else
                                {
                                    // waypoint에 속하지 않은 경우 현재 박스만 변경
                                    SetBoxId(currentBox, "vehicle", newVehicleId);
                                    AddUndoAction(new UndoAction
                                    {
                                        Type = UndoActionType.ModifyBox,
                                        Box = CloneBoundingBox(currentBox),
                                        OriginalLabel = "vehicle",
                                        OriginalObjectId = oldVehicleId
                                    });
                                }
                                
                                UpdateObjectInfo(currentBox);
                                UpdateBboxListDisplay();
                                pictureBoxVideo.Invalidate();
                            }
                        }
                    }
                };
                
                itemPanel.Controls.Add(itemLabel);
                itemPanel.Controls.Add(comboBox);
                
                EventHandler clickHandler = (s, e) =>
                {
                    selectedBox = currentBox;
                    UpdateObjectInfo(selectedBox);
                    HighlightSelectedBoxInSidebar(); // ✅ 하이라이트 업데이트
                    pictureBoxVideo.Invalidate();
                };
                
                itemPanel.Click += clickHandler;
                itemLabel.Click += clickHandler;
                
                panelVehicleList.Controls.Add(itemPanel);
                yPos += 70;
            }
        }
        
        // Event 리스트 표시 (현재 프레임의 Event bbox)
        private void UpdateEventListDisplay()
        {
            panelEventList.Controls.Clear();
            
            // ✅ 패널 자체 클릭 시 선택 해제
            panelEventList.Click += (s, e) =>
            {
                selectedBox = null;
                ClearSidebarHighlights();
                UpdateObjectInfo(null);
                pictureBoxVideo.Invalidate();
            };
            
            var currentBoxes = boundingBoxes
                .Where(b => b.FrameIndex == currentFrameIndex && b.Label == "event" && !b.IsDeleted)
                .ToList();
            
            if (currentBoxes.Count == 0)
            {
                Label emptyLabel = new Label
                {
                    Text = "현재 프레임에 Event 없음",
                    Font = new System.Drawing.Font("Segoe UI", 8F),
                    ForeColor = System.Drawing.Color.Gray,
                    Location = new System.Drawing.Point(5, 5),
                    AutoSize = true
                };
                panelEventList.Controls.Add(emptyLabel);
                        return;
            }
            
            int yPos = 5;
            foreach (var box in currentBoxes)
            {
                var currentBox = box;
                string[] eventTypes = { "contact", "exchange", "board", "final_exchange", "throw" };
                string eventName = currentBox.EventId > 0 && currentBox.EventId <= eventTypes.Length 
                    ? eventTypes[currentBox.EventId - 1] 
                    : currentBox.EventId.ToString();
                
                Panel itemPanel = new Panel
                {
                    Location = new System.Drawing.Point(5, yPos),
                    Size = new System.Drawing.Size(260, 65),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackColor = System.Drawing.Color.FromArgb(220, 252, 231),
                    Cursor = Cursors.Hand,
                    Tag = currentBox // ✅ 박스 정보를 Tag에 저장
                };
                
                Label itemLabel = new Label
                {
                    Text = $"event_{eventName}",
                    Location = new System.Drawing.Point(8, 8),
                    Size = new System.Drawing.Size(244, 20),
                    Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.FromArgb(20, 83, 45),
                    BackColor = System.Drawing.Color.Transparent
                };
                
                ComboBox comboBox = new ComboBox
                {
                    Location = new System.Drawing.Point(8, 32),
                    Size = new System.Drawing.Size(244, 25),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new System.Drawing.Font("Segoe UI", 8F)
                };
                
                // ComboBox 호버 시 스크롤 방지
                comboBox.MouseWheel += (s, e) => ((HandledMouseEventArgs)e).Handled = true;
                
                comboBox.Items.AddRange(new object[] { "event_contact", "event_exchange", "event_board", "event_final_exchange", "event_throw" });
                comboBox.SelectedItem = $"event_{eventName}";
                
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    if (comboBox.SelectedItem != null)
                    {
                        string selected = comboBox.SelectedItem.ToString();
                        if (selected.StartsWith("event_"))
                        {
                            string eType = selected.Substring(6);
                            int eventId = Array.IndexOf(eventTypes, eType) + 1;
                        if (eventId > 0)
                        {
                                int oldEventId = currentBox.EventId;
                                SetBoxId(currentBox, "event", eventId);
                            
                                // Event 타입 변경 시 동일한 EventId와 Rectangle을 가진 박스만 업데이트
                                var waypoint = waypointMarkers.FirstOrDefault(w =>
                                    currentBox.FrameIndex >= w.EntryFrame &&
                                    currentBox.FrameIndex <= w.ExitFrame);
                                
                                if (waypoint != null)
                                {
                                    var relatedBoxes = boundingBoxes.Where(b =>
                                        b.Label == "event" &&
                                        b.EventId == oldEventId &&
                                        b.Rectangle.X == currentBox.Rectangle.X &&
                                        b.Rectangle.Y == currentBox.Rectangle.Y &&
                                        b.Rectangle.Width == currentBox.Rectangle.Width &&
                                        b.Rectangle.Height == currentBox.Rectangle.Height &&
                                        b.FrameIndex >= waypoint.EntryFrame &&
                                        b.FrameIndex <= waypoint.ExitFrame).ToList();
                                    
                                    foreach (var relatedBox in relatedBoxes)
                                    {
                                        SetBoxId(relatedBox, "event", eventId);
                                    }
                                }
                                
                                UpdateObjectInfo(currentBox);
                    UpdateBboxListDisplay();
                    pictureBoxVideo.Invalidate();
                            }
                        }
                    }
                };
                
                itemPanel.Controls.Add(itemLabel);
                itemPanel.Controls.Add(comboBox);
                
                EventHandler clickHandler = (s, e) =>
                {
                    selectedBox = currentBox;
                    UpdateObjectInfo(selectedBox);
                    HighlightSelectedBoxInSidebar(); // ✅ 하이라이트 업데이트
                    pictureBoxVideo.Invalidate();
                };
                
                itemPanel.Click += clickHandler;
                itemLabel.Click += clickHandler;
                
                panelEventList.Controls.Add(itemPanel);
                yPos += 70;
            }
        }
        
        // 선택한 bbox 삭제 버튼 핸들러
        private void btnDeleteLabel_Click(object sender, EventArgs e)
        {
            if (selectedBox == null)
            {
                MessageBox.Show("삭제할 bbox를 먼저 선택해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            AddUndoAction(new UndoAction { Type = UndoActionType.RemoveBox, Box = CloneBoundingBox(selectedBox) });
            
            // ✅ 삭제 플래그 설정 (실제 제거 안 함, 흔적 유지)
            selectedBox.IsDeleted = true;
            
            selectedBox = null;
            ClearSidebarHighlights(); // ✅ 하이라이트 초기화
            UpdateBoxCount();
            UpdateBboxListDisplay();
            pictureBoxVideo.Invalidate();
        }

        // 박스의 현재 라벨에 해당하는 ID 가져오기
        private int GetBoxId(BoundingBox box)
        {
            if (box.Label == "person") return box.PersonId;
            if (box.Label == "vehicle") return box.VehicleId;
            if (box.Label == "event") return box.EventId;
            return 0;
        }
        
        // 선택된 박스가 속한 기존 Waypoint 찾기
        private WaypointMarker FindWaypointForBox(BoundingBox box)
        {
            if (box == null) return null;
            
            int boxId = GetBoxId(box);
            
            // 현재 프레임에 해당하는 waypoint 찾기
            // 같은 Label과 ObjectId를 가진 waypoint 중에서
            // 현재 프레임이 EntryFrame과 ExitFrame 사이에 있는 경우
            var waypoint = waypointMarkers.FirstOrDefault(w =>
                w.Label == box.Label &&
                w.ObjectId == boxId &&
                box.FrameIndex >= w.EntryFrame &&
                box.FrameIndex <= w.ExitFrame);
            
            return waypoint;
        }

        // Person 속성 읽기 메서드
        private object GetPersonAttribute(int personId, int frameIndex, string attributeName)
        {
            return personAttributeStore.GetAttribute(personId, frameIndex, attributeName, waypointMarkers);
        }

        // Person 속성 저장 메서드
        private void SetPersonAttribute(int personId, int waypointEntryFrame, string attributeName, object value)
        {
            personAttributeStore.SetAttribute(personId, waypointEntryFrame, attributeName, value);
        }
        
        // 박스 수정 완료 시 수정된 프레임 기록
        private void RecordManuallyAdjustedFrame(BoundingBox box)
        {
            if (box == null) return;
            
            // waypoint 내부에서만 기록
            var waypoint = FindWaypointForBox(box);
            if (waypoint == null) return;
            
            string key = $"{box.Label}_{GetBoxId(box)}";
            
            if (!manuallyAdjustedFrames.ContainsKey(key))
            {
                manuallyAdjustedFrames[key] = new List<int>();
            }
            
            // 중복 제거 및 정렬 유지
            if (!manuallyAdjustedFrames[key].Contains(box.FrameIndex))
            {
                manuallyAdjustedFrames[key].Add(box.FrameIndex);
                manuallyAdjustedFrames[key].Sort();
            }
        }
        
        // ✅ 박스 삭제 시 사라짐 의도 기록
        private void RecordDisappearanceIntent(BoundingBox box)
        {
            if (box == null) return;
            
            // waypoint 내부에서만 기록
            var waypoint = FindWaypointForBox(box);
            if (waypoint == null) return;
            
            string key = $"{box.Label}_{GetBoxId(box)}";
            
            if (!disappearedRanges.ContainsKey(key))
            {
                disappearedRanges[key] = new List<(int, int?)>();
            }
            
            // 이미 해당 프레임에서 시작하는 사라짐 기록이 있는지 확인
            bool exists = disappearedRanges[key].Any(r => r.startFrame == box.FrameIndex && !r.endFrame.HasValue);
            
            if (!exists)
            {
                // 사라짐 시작 프레임 기록 (종료 프레임은 아직 미확정)
                disappearedRanges[key].Add((box.FrameIndex, null));
                System.Diagnostics.Debug.WriteLine($"[사라짐 의도 기록] {key}: 프레임 {box.FrameIndex}에서 사라짐 시작");
            }
        }
        
        // 박스의 특정 라벨 타입에 ID 설정
        private void SetBoxId(BoundingBox box, string label, int id)
        {
            if (label == "person") box.PersonId = id;
            else if (label == "vehicle") box.VehicleId = id;
            else if (label == "event") box.EventId = id;
        }

        // 스펙에 맞는 Category ID를 반환 (JSON 내보내기용)
        private int GetCategoryId(string label, int boxId)
        {
            string categoryName = GetCategoryName(label, boxId);
            
            if (CategoryIdMap.ContainsKey(categoryName))
                return CategoryIdMap[categoryName];
            
            // 기본값 처리 (매핑되지 않은 경우)
            if (label == "person") return Math.Min(boxId, 20); // 1~20
            if (label == "vehicle") return Math.Min(21 + (boxId - 1), 24); // 21~24
            if (label == "event") return Math.Min(25 + (boxId - 1), 28); // 25~28 (4개)
            
            return boxId;
        }

        // 스펙에 맞는 Category Name을 반환 (JSON 내보내기용)
        private string GetCategoryName(string label, int boxId)
        {
            if (label == "person")
            {
                // person은 person_01 ~ person_20 형식
                return $"person_{boxId:D2}";
            }
            else if (label == "vehicle")
            {
                // vehicle은 고유 이름 매핑 (ID 21~24)
                switch (boxId)
                {
                    case 1: return "car";           // ID: 21
                    case 2: return "motorcycle";    // ID: 22
                    case 3: return "e_scooter";     // ID: 23
                    case 4: return "bicycle";       // ID: 24
                    default: return "car"; // 기본값
                }
            }
            else if (label == "event")
            {
                // event는 고유 이름 매핑 (ID 25~28)
                switch (boxId)
                {
                    case 1: return "contact";         // ID: 25
                    case 2: return "exchange";        // ID: 26
                    case 3: return "board";           // ID: 27
                    case 4: return "final_exchange";  // ID: 28
                    default: return "contact"; // 기본값
                }
            }
            
            return $"{label}_{boxId:D2}";
        }

        private void ApplyLabelChange(string newLabel, int newId, string oldLabel, int oldId, Rectangle oldRect)
        {
            if (selectedBox != null)
            {
                selectedBox.Label = newLabel;
                SetBoxId(selectedBox, newLabel, newId);
                labelObjectLabel.Text = $"Label: {newLabel}_{newId:D2}";

                AddUndoAction(new UndoAction
                {
                    Type = UndoActionType.ModifyBox,
                    Box = CloneBoundingBox(selectedBox),
                    OriginalLabel = oldLabel,
                    OriginalObjectId = oldId,
                    OriginalRectangle = oldRect
                });

                UpdateBboxListDisplay();
                pictureBoxVideo.Invalidate();
            }
        }

        private void btnAddLabel_Click(object sender, EventArgs e)
        {
            string labelName = ShowInputDialog("새 라벨 추가", "라벨 이름을 입력하세요 (예: person_02):");

            if (string.IsNullOrWhiteSpace(labelName))
                return;

            if (customLabels.Any(l => l.Name == labelName))
            {
                MessageBox.Show("이미 존재하는 라벨입니다.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string labelType;
            int labelId = 0;
            Color labelColor;

            if (labelName.StartsWith("person_"))
            {
                labelType = "person";
                string idPart = labelName.Substring(7);
                if (int.TryParse(idPart, out int parsedId))
                    labelId = parsedId;
                labelColor = Color.FromArgb(252, 231, 243);
            }
            else if (labelName.StartsWith("vehicle_") || labelName == "vehicle")
            {
                labelType = "vehicle";
                labelColor = Color.FromArgb(219, 234, 254);
            }
            else if (labelName.StartsWith("event_") || labelName == "event")
            {
                labelType = "event";
                labelColor = Color.FromArgb(220, 252, 231);
            }
            else
            {
                labelType = "custom";
                labelColor = Color.FromArgb(254, 243, 199);
            }

            Panel labelPanel = new Panel();
            labelPanel.Location = new System.Drawing.Point(12, customLabelYPosition);
            labelPanel.Size = new System.Drawing.Size(260, 40);
            labelPanel.BackColor = labelColor;
            labelPanel.Cursor = Cursors.Hand;

            Label label = new Label();
            label.Text = labelName;
            label.Font = new System.Drawing.Font("Segoe UI", 9F, FontStyle.Bold);
            label.ForeColor = Color.FromArgb(30, 30, 30);
            label.Location = new System.Drawing.Point(12, 10);
            label.Size = new System.Drawing.Size(200, 20);
            label.Cursor = Cursors.Hand;

            labelPanel.Controls.Add(label);

            var customLabel = new CustomLabel
            {
                Name = labelName,
                Type = labelType,
                Id = labelId,
                Panel = labelPanel,
                Label = label
            };
            customLabels.Add(customLabel);

            labelPanel.Click += (s, args) => CustomLabel_Click(customLabel);
            label.Click += (s, args) => CustomLabel_Click(customLabel);

            groupBoxLabels.Controls.Add(labelPanel);
            customLabelYPosition += 50;

            if (customLabelYPosition > groupBoxLabels.Height - 50)
            {
                groupBoxLabels.Height = customLabelYPosition + 50;
            }
        }

        private void CustomLabel_Click(CustomLabel customLabel)
        {
            if (selectedBox != null)
            {
                string oldLabel = selectedBox.Label;
                int oldPersonId = GetBoxId(selectedBox);
                Rectangle oldRect = selectedBox.Rectangle;

                selectedBox.Label = customLabel.Type;
                SetBoxId(selectedBox, customLabel.Type, customLabel.Id);

                labelObjectLabel.Text = $"Label: {customLabel.Name}";

                AddUndoAction(new UndoAction
                {
                    Type = UndoActionType.ModifyBox,
                    Box = CloneBoundingBox(selectedBox),
                    OriginalLabel = oldLabel,
                    OriginalObjectId = oldPersonId,
                    OriginalRectangle = oldRect
                });

                pictureBoxVideo.Invalidate();
            }
            else
            {
                MessageBox.Show("먼저 BBox를 선택해주세요.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private string ShowInputDialog(string title, string promptText)
        {
            Form prompt = new Form()
            {
                Width = 400,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = title,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label textLabel = new Label() { Left = 20, Top = 20, Width = 350, Text = promptText };
            TextBox textBox = new TextBox() { Left = 20, Top = 50, Width = 340 };
            Button confirmation = new Button() { Text = "확인", Left = 200, Width = 80, Top = 80, DialogResult = DialogResult.OK };
            Button cancel = new Button() { Text = "취소", Left = 290, Width = 80, Top = 80, DialogResult = DialogResult.Cancel };

            confirmation.Click += (sender, e) => { prompt.Close(); };
            cancel.Click += (sender, e) => { prompt.Close(); };

            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(cancel);
            prompt.AcceptButton = confirmation;
            prompt.CancelButton = cancel;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }
        #endregion

        #region Timeline
        private void panelTimeline_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            // ✅ Entry 마커가 위로 올라가도록 클리핑 영역 확장
            Rectangle clipRect = e.ClipRectangle;
            clipRect.Inflate(0, 15); // 위아래로 15픽셀 확장
            g.SetClip(clipRect);
            
            int width = panelTimeline.Width;
            int height = panelTimeline.Height;

            // 배경색 (노란색)
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(250, 204, 21)))
            {
                g.FillRectangle(bgBrush, 0, 0, width, height);
            }

            if (totalFrames > 0)
            {
                // ✅ Waypoint 구간별 색상 표시 (항상 원래 색상 유지)
                foreach (var waypoint in waypointMarkers)
                {
                    int startX = (int)(width * ((float)waypoint.EntryFrame / totalFrames));
                    int endX = (int)(width * ((float)waypoint.ExitFrame / totalFrames));
                    int segmentWidth = endX - startX;

                    // 반투명 원래 색상으로 구간 표시
                    Color segmentColor = Color.FromArgb(180, waypoint.MarkerColor);
                    
                    using (SolidBrush markerBrush = new SolidBrush(segmentColor))
                    {
                        g.FillRectangle(markerBrush, startX, 0, segmentWidth, height);
                    }
                }

                // ✅ Entry/Exit 마커 표시 (작은 사각형)
                foreach (var waypoint in waypointMarkers)
                {
                    int entryX = (int)(width * ((float)waypoint.EntryFrame / totalFrames));
                    int exitX = (int)(width * ((float)waypoint.ExitFrame / totalFrames));
                    
                    int markerSize = 8; // 마커 크기
                    int entryMarkerY = 0; // Entry 마커: 상단 정렬
                    int exitMarkerY = height - markerSize; // Exit 마커: 하단 정렬

                    // ✅ 선택된 waypoint인 경우 진한 빨간색으로 표시
                    bool isSelected = (selectedWaypoint != null && 
                                      waypoint.Label == selectedWaypoint.Label && 
                                      waypoint.ObjectId == selectedWaypoint.ObjectId &&
                                      waypoint.EntryFrame == selectedWaypoint.EntryFrame &&
                                      waypoint.ExitFrame == selectedWaypoint.ExitFrame);
                    
                    Color markerColor = isSelected ? Color.FromArgb(220, 38, 38) : waypoint.MarkerColor;

                    // Entry 마커 (진한 색상, 상단)
                    using (SolidBrush entryBrush = new SolidBrush(markerColor))
                    {
                        g.FillRectangle(entryBrush, entryX - markerSize / 2, entryMarkerY, markerSize, markerSize);
                    }
                    
                    // Entry 마커 테두리
                    using (Pen entryPen = new Pen(Color.White, 2))
                    {
                        g.DrawRectangle(entryPen, entryX - markerSize / 2, entryMarkerY, markerSize, markerSize);
                    }

                    // Exit 마커 (진한 색상, 하단)
                    using (SolidBrush exitBrush = new SolidBrush(markerColor))
                    {
                        g.FillRectangle(exitBrush, exitX - markerSize / 2, exitMarkerY, markerSize, markerSize);
                    }
                    
                    // Exit 마커 테두리
                    using (Pen exitPen = new Pen(Color.White, 2))
                    {
                        g.DrawRectangle(exitPen, exitX - markerSize / 2, exitMarkerY, markerSize, markerSize);
                    }
                }
            }

            // ✅ 현재 재생 위치 표시 (흰색 선) - Entry 선보다 먼저 그려서 Entry가 위에 표시되도록
            if (totalFrames > 0)
            {
                int currentX = (int)(width * timelineProgress);
                using (Pen pen = new Pen(Color.White, 3))
                {
                    g.DrawLine(pen, currentX, 0, currentX, height);
                }
            }

            // ✅ Entry 설정 중일 때 빨간 선 표시 (위로 올라온 형태)
            if (entryFrameIndex.HasValue && !exitFrameIndex.HasValue && totalFrames > 0)
            {
                int entryX = (int)(width * ((float)entryFrameIndex.Value / totalFrames));
                int markerHeight = 8; // Entry 마커 높이
                
                // Entry 빨간 선 (전체 높이)
                using (Pen pen = new Pen(Color.Red, 3))
                {
                    g.DrawLine(pen, entryX, 0, entryX, height);
                }
                
                // Entry 마커 (위쪽 삼각형 - 패널 상단에 표시)
                int triangleBase = 8; // 삼각형 밑변 너비
                System.Drawing.Point[] trianglePoints = new System.Drawing.Point[]
                {
                    new System.Drawing.Point(entryX, 0), // 상단 꼭짓점
                    new System.Drawing.Point(entryX - triangleBase / 2, markerHeight), // 왼쪽 밑변
                    new System.Drawing.Point(entryX + triangleBase / 2, markerHeight) // 오른쪽 밑변
                };
                using (SolidBrush brush = new SolidBrush(Color.Red))
                {
                    g.FillPolygon(brush, trianglePoints);
                }
                using (Pen pen = new Pen(Color.DarkRed, 2))
                {
                    g.DrawPolygon(pen, trianglePoints);
                }
            }
        }

        private void panelTimeline_MouseDown(object sender, MouseEventArgs e)
        {
            try
            {
                // ✅ YOLO 추적/탐지 중에는 타임라인 클릭 차단 (중요!)
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[타임라인 클릭 차단] YOLO 추적/탐지 중이므로 타임라인 클릭 무시");
                    return;
                }
                
                if (totalFrames == 0) return;

                // ✅ 먼저 마커 클릭 여부 확인 (Entry/Exit 프레임으로 이동)
                if (TryNavigateToMarker(e.X, e.Y))
                {
                    return; // 마커를 클릭했으면 드래그 시작하지 않음
                }

                // 마커가 아니면 기존 타임라인 드래그 시작
                isTimelineDragging = true;
                UpdateFrameFromMousePosition(e.X);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[타임라인 클릭 오류] {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 클릭 위치가 마커 영역인지 확인하고, 맞으면 해당 프레임으로 이동
        /// </summary>
        private bool TryNavigateToMarker(int mouseX, int mouseY)
        {
            if (totalFrames == 0) return false;

            int width = panelTimeline.Width;
            int height = panelTimeline.Height;
            int markerSize = 8;
            int clickTolerance = 6; // 클릭 허용 범위 (마커보다 약간 넓게)

            foreach (var waypoint in waypointMarkers)
            {
                // Entry 마커 위치 계산 (상단)
                int entryX = (int)(width * ((float)waypoint.EntryFrame / totalFrames));
                int entryLeft = entryX - markerSize / 2;
                int entryRight = entryX + markerSize / 2;
                int entryTop = 0;
                int entryBottom = markerSize;

                // Exit 마커 위치 계산 (하단)
                int exitX = (int)(width * ((float)waypoint.ExitFrame / totalFrames));
                int exitLeft = exitX - markerSize / 2;
                int exitRight = exitX + markerSize / 2;
                int exitTop = height - markerSize;
                int exitBottom = height;

                // Entry 마커 클릭 확인 (상단)
                if (mouseX >= entryLeft - clickTolerance && mouseX <= entryRight + clickTolerance &&
                    mouseY >= entryTop - clickTolerance && mouseY <= entryBottom + clickTolerance)
                {
                    LoadFrame(waypoint.EntryFrame);
                    return true;
                }

                // Exit 마커 클릭 확인 (하단)
                if (mouseX >= exitLeft - clickTolerance && mouseX <= exitRight + clickTolerance &&
                    mouseY >= exitTop - clickTolerance && mouseY <= exitBottom + clickTolerance)
                {
                    LoadFrame(waypoint.ExitFrame);
                    return true;
                }
            }

            return false; // 마커가 아님
        }

        private void panelTimeline_MouseMove(object sender, MouseEventArgs e)
        {
            if (isTimelineDragging && totalFrames > 0)
            {
                UpdateFrameFromMousePosition(e.X);
            }
        }

        private void panelTimeline_MouseUp(object sender, MouseEventArgs e)
        {
            isTimelineDragging = false;
        }

        private void UpdateFrameFromMousePosition(int mouseX)
        {
            try
            {
                // ✅ YOLO 추적/탐지 중에는 타임라인 클릭으로 프레임 이동 차단 (중요!)
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[타임라인 클릭 차단] YOLO 추적/탐지 중이므로 타임라인 클릭 무시");
                    return;
                }
                
                if (totalFrames == 0) return;

                float clickPosition = (float)mouseX / panelTimeline.Width;
                clickPosition = Math.Max(0, Math.Min(1, clickPosition));

                int targetFrame = (int)(clickPosition * totalFrames);
                targetFrame = Math.Max(0, Math.Min(totalFrames - 1, targetFrame));

                LoadFrame(targetFrame);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[타임라인 클릭 오류] {ex.Message}\n{ex.StackTrace}");
            }
        }
        #endregion

        #region Undo/Redo System
        private void AddUndoAction(UndoAction action)
        {
            undoStack.Push(action);

            if (undoStack.Count > MAX_UNDO_STACK)
            {
                var tempList = undoStack.ToList();
                tempList.RemoveAt(tempList.Count - 1);
                undoStack = new Stack<UndoAction>(tempList.AsEnumerable().Reverse());
            }

            redoStack.Clear();
        }

        private void Undo()
        {
            if (undoStack.Count == 0)
            {
                MessageBox.Show("되돌릴 작업이 없습니다.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var action = undoStack.Pop();

            switch (action.Type)
            {
                case UndoActionType.AddBox:
                    boundingBoxes.Remove(action.Box);
                    InvalidateBoxCache();
                    if (selectedBox == action.Box)
                        selectedBox = null;
                    break;

                case UndoActionType.RemoveBox:
                    boundingBoxes.Add(action.Box);
                    InvalidateBoxCache();
                    break;

                case UndoActionType.ModifyBox:
                    var boxToModify = boundingBoxes.FirstOrDefault(b =>
                        b.FrameIndex == action.Box.FrameIndex &&
                        GetBoxId(b) == GetBoxId(action.Box) &&
                        b.Label == action.Box.Label);

                    if (boxToModify != null)
                    {
                        boxToModify.Rectangle = action.OriginalRectangle;
                        boxToModify.Label = action.OriginalLabel;
                        SetBoxId(boxToModify, action.OriginalLabel, action.OriginalObjectId);
                        InvalidateBoxCache();
                    }
                    break;

                case UndoActionType.Tracking:
                    foreach (var box in action.TrackedBoxes)
                    {
                        boundingBoxes.Remove(box);
                    }
                    InvalidateBoxCache();
                    break;
            }

            redoStack.Push(action);
            UpdateBoxCount();
            UpdateBboxListDisplay();
            pictureBoxVideo.Invalidate();
        }

        private void Redo()
        {
            if (redoStack.Count == 0)
            {
                MessageBox.Show("다시 실행할 작업이 없습니다.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var action = redoStack.Pop();

            switch (action.Type)
            {
                case UndoActionType.AddBox:
                    boundingBoxes.Add(action.Box);
                    InvalidateBoxCache();
                    break;

                case UndoActionType.RemoveBox:
                    boundingBoxes.Remove(action.Box);
                    InvalidateBoxCache();
                    if (selectedBox == action.Box)
                        selectedBox = null;
                    break;

                case UndoActionType.ModifyBox:
                    var boxToModify = boundingBoxes.FirstOrDefault(b =>
                        b.FrameIndex == action.Box.FrameIndex &&
                        GetBoxId(b) == action.OriginalObjectId &&
                        b.Label == action.OriginalLabel);

                    if (boxToModify != null)
                    {
                        boxToModify.Rectangle = action.Box.Rectangle;
                        boxToModify.Label = action.Box.Label;
                        SetBoxId(boxToModify, action.Box.Label, GetBoxId(action.Box));
                        InvalidateBoxCache();
                    }
                    break;

                case UndoActionType.Tracking:
                    foreach (var box in action.TrackedBoxes)
                    {
                        boundingBoxes.Add(box);
                    }
                    InvalidateBoxCache();
                    break;
            }

            undoStack.Push(action);
            UpdateBoxCount();
            UpdateBboxListDisplay();
            pictureBoxVideo.Invalidate();
        }
        #endregion

        #region Event Box Propagation
        /// <summary>
        /// Entry 프레임에 있는 모든 Event 박스를 Exit 프레임까지 자동 전파
        /// (Exit 마커 설정 시 호출됨)
        /// </summary>
        private void PropagateAllEventBoxesInRange(int entryFrame, int exitFrame)
        {
            // Entry 프레임의 모든 Event 박스 찾기
            var eventBoxesAtEntry = boundingBoxes
                .Where(b => b.FrameIndex == entryFrame && b.Label == "event")
                .ToList();

            if (eventBoxesAtEntry.Count == 0)
                return; // Event 박스가 없으면 전파할 필요 없음

            int totalPropagated = 0;

            foreach (var eventBox in eventBoxesAtEntry)
            {
                // Entry 다음 프레임부터 Exit까지 전파
                for (int frame = entryFrame + 1; frame <= exitFrame; frame++)
                {
                    // 이미 해당 프레임에 동일한 Event 박스가 있는지 확인
                    bool exists = boundingBoxes.Any(b =>
                        b.FrameIndex == frame &&
                        b.Label == "event" &&
                        b.EventId == eventBox.EventId);

                    if (!exists)
                    {
                        var newBox = new BoundingBox
                        {
                            Rectangle = eventBox.Rectangle,
                            Label = eventBox.Label,
                            FrameIndex = frame,
                            PersonId = eventBox.PersonId,
                            VehicleId = eventBox.VehicleId,
                            EventId = eventBox.EventId,
                            Action = "waypoint"
                        };
                        boundingBoxes.Add(newBox);
                        totalPropagated++;
                    }
                }
            }

            if (totalPropagated > 0)
            {
                InvalidateBoxCache();
            }
        }

        /// <summary>
        /// Vehicle bbox 생성 시 자동으로 Waypoint 추가 (단일 프레임)
        /// </summary>
        private void CreateVehicleWaypoint(BoundingBox box)
        {
            if (box.Label != "vehicle") return;

            TimeSpan time = TimeSpan.FromSeconds(box.FrameIndex / fps);
            string timeString = time.ToString(@"hh\:mm\:ss");

            var waypoint = new WaypointMarker
            {
                EntryFrame = box.FrameIndex,
                ExitFrame = box.FrameIndex, // 단일 프레임
                MarkerColor = System.Drawing.Color.FromArgb(107, 158, 255), // 파랑
                EntryTime = timeString,
                ExitTime = timeString,
                ObjectId = box.VehicleId, // ✅ VehicleId를 ObjectId로 설정
                Label = "vehicle"
            };

            waypointMarkers.Add(waypoint);
            UpdateWaypointListView();
            panelTimeline.Invalidate();
        }

        /// <summary>
        /// Event bbox 생성 시 자동으로 Waypoint 추가 (현재 프레임 ~ 영상 끝 또는 Q키 종료 시점)
        /// </summary>
        private void CreateEventWaypoint(BoundingBox box)
        {
            if (box.Label != "event") return;

            TimeSpan entryTime = TimeSpan.FromSeconds(box.FrameIndex / fps);
            // Exit은 나중에 확정되므로 초기값은 동일 프레임으로 설정
            TimeSpan exitTime = TimeSpan.FromSeconds(box.FrameIndex / fps);

            // ✅ Event Waypoint 생성 (초록 색상, EventId 저장)
            var waypoint = new WaypointMarker
            {
                EntryFrame = box.FrameIndex,
                ExitFrame = box.FrameIndex,
                MarkerColor = System.Drawing.Color.FromArgb(107, 255, 107), // 초록
                EntryTime = entryTime.ToString(@"hh\:mm\:ss"),
                ExitTime = exitTime.ToString(@"hh\:mm\:ss"),
                ObjectId = box.EventId,
                Label = "event"
            };

            waypointMarkers.Add(waypoint);
            UpdateWaypointListView();
            panelTimeline.Invalidate();
        }

        /// <summary>
        /// Event bbox를 현재 프레임부터 영상 끝까지 자동 전파
        /// </summary>
        private void PropagateEventBoxToEnd(BoundingBox box)
        {
            if (box.Label != "event") return;

            int startFrame = box.FrameIndex + 1;
            int endFrame = totalFrames - 1;

            if (startFrame > endFrame) return;


            for (int frame = startFrame; frame <= endFrame; frame++)
            {
                // 같은 EventId와 위치를 가진 박스가 이미 있는지 확인
                bool exists = boundingBoxes.Any(b =>
                    b.FrameIndex == frame &&
                    b.Label == "event" &&
                    b.EventId == box.EventId &&
                    b.Rectangle.X == box.Rectangle.X &&
                    b.Rectangle.Y == box.Rectangle.Y &&
                    b.Rectangle.Width == box.Rectangle.Width &&
                    b.Rectangle.Height == box.Rectangle.Height);

                if (!exists)
                {
                    var copiedBox = new BoundingBox
                    {
                        FrameIndex = frame,
                        Rectangle = new Rectangle(box.Rectangle.X, box.Rectangle.Y, box.Rectangle.Width, box.Rectangle.Height),
                        Label = "event",
                        PersonId = 0,
                        VehicleId = 0,
                        EventId = box.EventId,
                        Action = box.Action,
                        VehicleName = box.VehicleName,
                        EventName = box.EventName
                    };
                    boundingBoxes.Add(copiedBox);
                }
            }

            InvalidateBoxCache();
            UpdateBoxCount();
            UpdateBboxListDisplay();
        }

        /// <summary>
        /// Event 박스를 생성 프레임 다음부터 지정 종료 프레임까지 전파
        /// </summary>
        private void PropagateEventBoxWithinRange(BoundingBox box, int endFrame)
        {
            if (box.Label != "event") return;

            int startFrame = box.FrameIndex + 1;
            if (startFrame > endFrame) return;

            for (int frame = startFrame; frame <= endFrame; frame++)
            {
                bool exists = boundingBoxes.Any(b =>
                    b.FrameIndex == frame &&
                    b.Label == "event" &&
                    b.EventId == box.EventId &&
                    b.Rectangle.X == box.Rectangle.X &&
                    b.Rectangle.Y == box.Rectangle.Y &&
                    b.Rectangle.Width == box.Rectangle.Width &&
                    b.Rectangle.Height == box.Rectangle.Height);

                if (!exists)
                {
                    var copiedBox = new BoundingBox
                    {
                        FrameIndex = frame,
                        Rectangle = new Rectangle(box.Rectangle.X, box.Rectangle.Y, box.Rectangle.Width, box.Rectangle.Height),
                        Label = "event",
                        PersonId = 0,
                        VehicleId = 0,
                        EventId = box.EventId,
                        Action = box.Action,
                        VehicleName = box.VehicleName,
                        EventName = box.EventName
                    };
                    boundingBoxes.Add(copiedBox);
                }
            }

            InvalidateBoxCache();
            UpdateBoxCount();
            UpdateBboxListDisplay();
        }

        /// <summary>
        /// Event → Person/Vehicle 변경 시 전파된 Event 박스 삭제
        /// </summary>
        private void RemovePropagatedEventBoxes(BoundingBox box)
        {
            if (box.Label != "event")
            {
                return;
            }

            // 현재 프레임 이후의 같은 EventId, Rectangle을 가진 박스들 찾기
            var boxesToRemove = boundingBoxes.Where(b =>
                b.Label == "event" &&
                b.EventId == box.EventId &&
                b.Rectangle.X == box.Rectangle.X &&
                b.Rectangle.Y == box.Rectangle.Y &&
                b.Rectangle.Width == box.Rectangle.Width &&
                b.Rectangle.Height == box.Rectangle.Height &&
                b.FrameIndex > box.FrameIndex).ToList();

            if (boxesToRemove.Count > 0)
            {
                
                foreach (var boxToRemove in boxesToRemove)
                {
                    boundingBoxes.Remove(boxToRemove);
                }

                // 관련된 Event Waypoint 삭제
                var eventWaypoint = waypointMarkers.FirstOrDefault(w =>
                    w.Label == "event" &&
                    w.EntryFrame == box.FrameIndex);

                if (eventWaypoint != null)
                {
                    waypointMarkers.Remove(eventWaypoint);
                }

                InvalidateBoxCache();
                UpdateBoxCount();
                UpdateBboxListDisplay();
                UpdateWaypointListView();
                panelTimeline.Invalidate();
                System.Diagnostics.Debug.WriteLine($"[전파 박스 삭제 완료] {boxesToRemove.Count}개 박스 삭제됨");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[전파 박스 삭제] 삭제할 박스 없음");
            }
        }

        /// <summary>
        /// Event 박스가 새로 생성되었을 때 현재 프레임부터 Waypoint Exit까지 전파
        /// (Waypoint 중간 프레임에서 생성된 Event도 자동으로 Exit까지 전파됨)
        /// </summary>
        private void PropagateEventBoxIfNeeded(BoundingBox box)
        {
            System.Diagnostics.Debug.WriteLine($"[Event 전파 시작] FrameIndex={box.FrameIndex}, Label={box.Label}, EventId={box.EventId}");
            
            // Event 라벨이 아니면 전파하지 않음
            if (box.Label != "event")
            {
                System.Diagnostics.Debug.WriteLine($"[Event 전파 중단] Event 라벨이 아님: {box.Label}");
                return;
            }

            // 현재 Waypoint 목록 확인
            System.Diagnostics.Debug.WriteLine($"[Event 전파] 현재 Waypoint 개수: {waypointMarkers.Count}");
            foreach (var wm in waypointMarkers)
            {
                System.Diagnostics.Debug.WriteLine($"  - Waypoint: Entry={wm.EntryFrame}, Exit={wm.ExitFrame}");
            }

            // 현재 박스가 속한 Waypoint 찾기
            var waypoint = waypointMarkers.FirstOrDefault(w =>
                box.FrameIndex >= w.EntryFrame &&
                box.FrameIndex <= w.ExitFrame);

            if (waypoint == null)
            {
                // Waypoint가 없으면 전파 불가 (Entry/Exit 마커가 아직 설정되지 않음)
                System.Diagnostics.Debug.WriteLine($"[Event 전파 중단] 프레임 {box.FrameIndex}에 해당하는 Waypoint가 없어 전파하지 않음");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[Event 전파] Waypoint 발견: Entry={waypoint.EntryFrame}, Exit={waypoint.ExitFrame}");

            int startFrame = box.FrameIndex + 1; // 다음 프레임부터
            int endFrame = waypoint.ExitFrame;

            // 현재 프레임이 Exit 프레임이면 전파할 필요 없음
            if (box.FrameIndex >= endFrame)
            {
                System.Diagnostics.Debug.WriteLine($"[Event 전파 중단] 프레임 {box.FrameIndex}가 Exit 프레임({endFrame})이므로 전파 불필요");
                return;
            }


            int createdCount = 0;
            for (int frame = startFrame; frame <= endFrame; frame++)
            {
                // 이미 동일한 EventId와 Rectangle을 가진 박스가 존재하는지 확인
                bool exists = boundingBoxes.Any(b =>
                    b.FrameIndex == frame &&
                    b.Label == "event" &&
                    b.EventId == box.EventId &&
                    b.Rectangle.X == box.Rectangle.X &&
                    b.Rectangle.Y == box.Rectangle.Y &&
                    b.Rectangle.Width == box.Rectangle.Width &&
                    b.Rectangle.Height == box.Rectangle.Height);

                if (!exists)
                {
                    var newBox = new BoundingBox
                    {
                        Rectangle = box.Rectangle,
                        Label = box.Label,
                        FrameIndex = frame,
                        PersonId = box.PersonId,
                        VehicleId = box.VehicleId,
                        EventId = box.EventId,
                        Action = "waypoint"
                    };
                    boundingBoxes.Add(newBox);
                    createdCount++;
                }
            }

            if (createdCount > 0)
            {
                InvalidateBoxCache();
                UpdateBoxCount();
                System.Diagnostics.Debug.WriteLine($"[Event 생성 전파 완료] {createdCount}개 프레임에 박스 생성됨 ({startFrame}~{endFrame})");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Event 생성 전파] 생성할 박스 없음 (이미 존재)");
            }
        }

        /// <summary>
        /// Event 박스가 중간 프레임에서 수정되었을 때 해당 프레임부터 Exit까지 전파
        /// </summary>
        private void PropagateEventBoxFromCurrentFrame(BoundingBox box)
        {
            // Event 라벨이 아니면 전파하지 않음
            if (box.Label != "event")
                return;

            // 현재 박스가 속한 Waypoint 찾기
            var waypoint = waypointMarkers.FirstOrDefault(w =>
                box.FrameIndex >= w.EntryFrame &&
                box.FrameIndex <= w.ExitFrame);

            if (waypoint == null)
            {
                System.Diagnostics.Debug.WriteLine($"[Event 전파 실패] 프레임 {box.FrameIndex}에 해당하는 Waypoint를 찾을 수 없습니다.");
                return;
            }

            int startFrame = box.FrameIndex + 1; // 다음 프레임부터
            int endFrame = waypoint.ExitFrame;

            // 현재 프레임이 Exit 프레임이면 전파할 필요 없음
            if (box.FrameIndex >= endFrame)
                return;

            System.Diagnostics.Debug.WriteLine($"[Event 전파] 프레임 {box.FrameIndex}에서 수정 감지, {startFrame}~{endFrame}까지 전파 시작");

            // 현재 프레임 이후의 동일한 Event 박스들을 찾아서 업데이트
            var boxesToUpdate = boundingBoxes.Where(b =>
                b.FrameIndex > box.FrameIndex &&
                b.FrameIndex <= endFrame &&
                b.Label == "event" &&
                b.EventId == box.EventId).ToList();

            int updatedCount = 0;
            foreach (var targetBox in boxesToUpdate)
            {
                targetBox.Rectangle = box.Rectangle;
                updatedCount++;
            }

            // 업데이트된 박스가 없으면 새로 생성
            if (updatedCount == 0)
            {
                for (int frame = startFrame; frame <= endFrame; frame++)
                {
                    bool exists = boundingBoxes.Any(b =>
                        b.FrameIndex == frame &&
                        b.Label == "event" &&
                        b.EventId == box.EventId);

                    if (!exists)
                    {
                        var newBox = new BoundingBox
                        {
                            Rectangle = box.Rectangle,
                            Label = box.Label,
                            FrameIndex = frame,
                            PersonId = box.PersonId,
                            VehicleId = box.VehicleId,
                            EventId = box.EventId,
                            Action = "waypoint"
                        };
                        boundingBoxes.Add(newBox);
                        updatedCount++;
                    }
                }
            }

            if (updatedCount > 0)
            {
                InvalidateBoxCache();
                UpdateBoxCount();
                UpdateBboxListDisplay();
                
                System.Diagnostics.Debug.WriteLine($"[Event 전파 완료] {updatedCount}개 프레임 업데이트됨 ({startFrame}~{endFrame})");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Event 전파] 업데이트할 박스 없음 (이미 존재하거나 범위 밖)");
            }
        }
        #endregion

        #region Forced Inertia Tracking (Shift+T)
        
        /// <summary>
        /// 강제 관성 추적: a 프레임부터 b 프레임까지 수동 수정 프레임을 기준으로 보간
        /// </summary>
        private void PerformForcedInertiaTracking(BoundingBox selectedBox, int aFrame, int bFrame)
        {
            if (selectedBox == null || aFrame >= bFrame)
            {
                MessageBox.Show("잘못된 프레임 범위입니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            int boxId = GetBoxId(selectedBox);
            string key = $"{selectedBox.Label}_{boxId}";
            
            // a 프레임의 박스 찾기
            var boxA = boundingBoxes.FirstOrDefault(b =>
                b.FrameIndex == aFrame &&
                b.Label == selectedBox.Label &&
                GetBoxId(b) == boxId &&
                !b.IsDeleted);
            
            if (boxA == null)
            {
                MessageBox.Show($"프레임 {aFrame}에서 해당 박스를 찾을 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            // 성공 프레임 딕셔너리 생성
            Dictionary<int, Rectangle> successFrames = new Dictionary<int, Rectangle>();
            
            // 1. a 프레임 추가 (최우선)
            successFrames[aFrame] = boxA.Rectangle;
            
            // 2. 수동 수정 프레임들 추가 (a 이후부터 b까지)
            if (manuallyAdjustedFrames.ContainsKey(key))
            {
                foreach (int adjustedFrame in manuallyAdjustedFrames[key])
                {
                    if (adjustedFrame > aFrame && adjustedFrame <= bFrame)
                    {
                        var boxAtFrame = boundingBoxes.FirstOrDefault(b =>
                            b.FrameIndex == adjustedFrame &&
                            b.Label == selectedBox.Label &&
                            GetBoxId(b) == boxId &&
                            !b.IsDeleted);
                        
                        if (boxAtFrame != null)
                        {
                            successFrames[adjustedFrame] = boxAtFrame.Rectangle;
                        }
                    }
                }
            }
            
            // 3. b 프레임 박스 확인 및 추가 (Shift+T를 누른 시점의 박스)
            var boxB = boundingBoxes.FirstOrDefault(b =>
                b.FrameIndex == bFrame &&
                b.Label == selectedBox.Label &&
                GetBoxId(b) == boxId &&
                !b.IsDeleted);
            
            // b 프레임에 박스가 없으면 현재 선택된 박스를 사용 (Shift+T를 누른 시점의 박스)
            if (boxB == null && selectedBox.FrameIndex == bFrame)
            {
                boxB = selectedBox;
            }
            
            if (boxB != null)
            {
                // b 프레임도 성공 프레임으로 추가 (최우선)
                successFrames[bFrame] = boxB.Rectangle;
            }
            else
            {
                // b 프레임에 박스가 없으면 a 프레임의 박스를 복사하여 사용
                var boxBFromA = CloneBoundingBox(boxA);
                boxBFromA.FrameIndex = bFrame;
                boxBFromA.Rectangle = boxA.Rectangle;
                
                // 박스 추가
                boundingBoxes.Add(boxBFromA);
                boxB = boxBFromA;
                successFrames[bFrame] = boxB.Rectangle;
                
                System.Diagnostics.Debug.WriteLine($"[강제 관성 추적] b프레임({bFrame})에 박스가 없어 a프레임 박스를 복사하여 생성");
            }
            
            // 성공 프레임이 2개 미만이면 보간 불가
            if (successFrames.Count < 2)
            {
                MessageBox.Show(
                    $"보간할 수 있는 성공 프레임이 부족합니다.\n\n" +
                    $"a 프레임: {aFrame}\n" +
                    $"b 프레임: {bFrame}\n" +
                    $"성공 프레임: {successFrames.Count}개\n\n" +
                    $"최소 2개의 성공 프레임이 필요합니다.",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            
            // 성공 프레임 목록 정렬
            var sortedSuccessFrames = successFrames.Keys.OrderBy(f => f).ToList();
            
            // Undo 스택에 추가
            var boxesToModify = new List<BoundingBox>();
            for (int frameIdx = aFrame + 1; frameIdx < bFrame; frameIdx++)
            {
                if (!successFrames.ContainsKey(frameIdx))
                {
                    var box = FindOrCreateBoxAtFrame(frameIdx, selectedBox);
                    boxesToModify.Add(box);
                }
            }
            
            if (boxesToModify.Count > 0)
            {
                var undoAction = new UndoAction
                {
                    Type = UndoActionType.ModifyBox,
                    Box = CloneBoundingBox(boxesToModify[0]), // 첫 번째 박스만 저장 (대표)
                    TrackedBoxes = boxesToModify.Select(b => CloneBoundingBox(b)).ToList()
                };
                AddUndoAction(undoAction);
            }
            
            // 보간 적용
            int interpolatedCount = 0;
            for (int frameIdx = aFrame + 1; frameIdx < bFrame; frameIdx++)
            {
                // 이미 성공 프레임이면 건너뛰기
                if (successFrames.ContainsKey(frameIdx))
                    continue;
                
                // 해당 프레임의 박스 찾기 또는 생성
                var box = FindOrCreateBoxAtFrame(frameIdx, selectedBox);
                
                // 앞뒤 성공 프레임 찾기
                int? prevSuccess = FindPreviousSuccessFrame(frameIdx, sortedSuccessFrames);
                int? nextSuccess = FindNextSuccessFrame(frameIdx, sortedSuccessFrames);
                
                if (prevSuccess.HasValue && nextSuccess.HasValue)
                {
                    // 양방향 보간
                    box.Rectangle = InterpolateRect(
                        successFrames[prevSuccess.Value],
                        successFrames[nextSuccess.Value],
                        prevSuccess.Value,
                        nextSuccess.Value,
                        frameIdx
                    );
                    interpolatedCount++;
                }
                else if (prevSuccess.HasValue)
                {
                    // 이전 성공 프레임만 있으면 고정
                    box.Rectangle = successFrames[prevSuccess.Value];
                    interpolatedCount++;
                }
                else if (nextSuccess.HasValue)
                {
                    // 다음 성공 프레임만 있으면 그 위치로 설정
                    box.Rectangle = successFrames[nextSuccess.Value];
                    interpolatedCount++;
                }
            }
            
            // UI 업데이트
            InvalidateBoxCache();
            UpdateBoxCount();
            UpdateBboxListDisplay();
            pictureBoxVideo.Invalidate();
            
            MessageBox.Show(
                $"강제 관성 추적이 완료되었습니다.\n\n" +
                $"범위: 프레임 {aFrame} ~ {bFrame}\n" +
                $"성공 프레임: {successFrames.Count}개\n" +
                $"보간된 박스: {interpolatedCount}개",
                "완료",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        
        /// <summary>
        /// 정렬된 성공 프레임 리스트에서 이전 성공 프레임 찾기
        /// </summary>
        private int? FindPreviousSuccessFrame(int currentFrame, List<int> sortedSuccessFrames)
        {
            for (int i = sortedSuccessFrames.Count - 1; i >= 0; i--)
            {
                if (sortedSuccessFrames[i] < currentFrame)
                    return sortedSuccessFrames[i];
            }
            return null;
        }
        
        /// <summary>
        /// 정렬된 성공 프레임 리스트에서 다음 성공 프레임 찾기
        /// </summary>
        private int? FindNextSuccessFrame(int currentFrame, List<int> sortedSuccessFrames)
        {
            foreach (var frame in sortedSuccessFrames)
            {
                if (frame > currentFrame)
                    return frame;
            }
            return null;
        }
        
        /// <summary>
        /// 선형 보간 계산 (위치 및 크기 모두 보간)
        /// </summary>
        private Rectangle InterpolateRect(Rectangle prev, Rectangle next, int prevFrame, int nextFrame, int currentFrame)
        {
            double ratio = (double)(currentFrame - prevFrame) / (nextFrame - prevFrame);
            
            int x = (int)(prev.X + (next.X - prev.X) * ratio);
            int y = (int)(prev.Y + (next.Y - prev.Y) * ratio);
            int width = (int)(prev.Width + (next.Width - prev.Width) * ratio);
            int height = (int)(prev.Height + (next.Height - prev.Height) * ratio);
            
            return new Rectangle(x, y, width, height);
        }
        
        /// <summary>
        /// 해당 프레임의 박스 찾기 또는 생성
        /// </summary>
        private BoundingBox FindOrCreateBoxAtFrame(int frameIndex, BoundingBox templateBox)
        {
            int boxId = GetBoxId(templateBox);
            
            // 먼저 기존 박스 찾기
            var existing = boundingBoxes.FirstOrDefault(b =>
                b.FrameIndex == frameIndex &&
                b.Label == templateBox.Label &&
                GetBoxId(b) == boxId &&
                !b.IsDeleted);
            
            if (existing != null)
                return existing;
            
            // 없으면 생성
            var newBox = new BoundingBox
            {
                FrameIndex = frameIndex,
                Label = templateBox.Label,
                PersonId = templateBox.PersonId,
                VehicleId = templateBox.VehicleId,
                EventId = templateBox.EventId,
                Rectangle = templateBox.Rectangle, // 임시값, 보간으로 업데이트됨
                Action = "waypoint"
            };
            
            boundingBoxes.Add(newBox);
            return newBox;
        }
        
        #endregion

        #region Disappearance Handling
        
        /// <summary>
        /// 특정 프레임에서 사라짐 구간 처리 (재추적 시작 시점에서 호출)
        /// </summary>
        private void ProcessDisappearedRangesAtFrame(WaypointMarker waypoint, int returnFrame)
        {
            int boxId = waypoint.ObjectId;
            string key = $"{waypoint.Label}_{boxId}";
            
            if (!disappearedRanges.ContainsKey(key))
                return;
            
            // 미확정 사라짐 구간 처리 (endFrame이 null인 것들)
            var pendingRanges = disappearedRanges[key]
                .Where(r => !r.endFrame.HasValue && r.startFrame < returnFrame)
                .ToList();
            
            foreach (var pendingRange in pendingRanges)
            {
                int aFrame = pendingRange.startFrame;
                int endFrame = returnFrame - 1; // 복귀 프레임 직전까지
                
                if (endFrame >= aFrame)
                {
                    // 사라진 구간 확정: a ~ (returnFrame-1)
                    var index = disappearedRanges[key].IndexOf(pendingRange);
                    disappearedRanges[key][index] = (aFrame, endFrame);
                    
                    System.Diagnostics.Debug.WriteLine($"[재추적 시점 복귀 감지] {key}: 프레임 {aFrame}~{endFrame} (복귀: {returnFrame})");
                    
                    // a ~ endFrame 구간의 박스 삭제
                    DeleteBoxesInRange(key, waypoint, aFrame, endFrame);
                }
            }
        }
        
        /// <summary>
        /// 사라짐 의도가 기록된 구간을 처리 (복귀 시점 감지 및 사라진 구간 확정)
        /// </summary>
        private void ProcessDisappearedRanges(WaypointMarker waypoint)
        {
            int boxId = waypoint.ObjectId;
            string key = $"{waypoint.Label}_{boxId}";
            
            if (!disappearedRanges.ContainsKey(key))
                return;
            
            // waypoint 범위 내의 박스 찾기
            var boxesInWaypoint = boundingBoxes
                .Where(b => 
                    b.Label == waypoint.Label &&
                    GetBoxId(b) == boxId &&
                    b.FrameIndex >= waypoint.EntryFrame &&
                    b.FrameIndex <= waypoint.ExitFrame &&
                    !b.IsDeleted)
                .OrderBy(b => b.FrameIndex)
                .ToList();
            
            // 미확정 사라짐 구간 처리 (endFrame이 null인 것들)
            var pendingRanges = disappearedRanges[key]
                .Where(r => !r.endFrame.HasValue)
                .ToList();
            
            foreach (var pendingRange in pendingRanges)
            {
                int aFrame = pendingRange.startFrame;
                
                // a 프레임 이후 첫 번째 성공 프레임(b) 찾기
                int? bFrame = boxesInWaypoint
                    .Where(b => b.FrameIndex > aFrame)
                    .Select(b => (int?)b.FrameIndex)
                    .FirstOrDefault();
                
                if (bFrame.HasValue && bFrame.Value > aFrame)
                {
                    // 사라진 구간 확정: a ~ (b-1)
                    int endFrame = bFrame.Value - 1;
                    
                    // 기존 항목 업데이트
                    var index = disappearedRanges[key].IndexOf(pendingRange);
                    disappearedRanges[key][index] = (aFrame, endFrame);
                    
                    System.Diagnostics.Debug.WriteLine($"[사라짐 구간 확정] {key}: 프레임 {aFrame}~{endFrame} (복귀: {bFrame.Value})");
                    
                    // a ~ endFrame 구간의 박스 삭제
                    DeleteBoxesInRange(key, waypoint, aFrame, endFrame);
                }
            }
        }
        
        /// <summary>
        /// 연속 박스 부재 구간 자동 감지 및 처리
        /// </summary>
        private void DetectContinuousAbsence(WaypointMarker waypoint)
        {
            int boxId = waypoint.ObjectId;
            string key = $"{waypoint.Label}_{boxId}";
            
            // waypoint 범위 내의 박스 찾기
            var boxesInWaypoint = boundingBoxes
                .Where(b => 
                    b.Label == waypoint.Label &&
                    GetBoxId(b) == boxId &&
                    b.FrameIndex >= waypoint.EntryFrame &&
                    b.FrameIndex <= waypoint.ExitFrame &&
                    !b.IsDeleted)
                .Select(b => b.FrameIndex)
                .OrderBy(f => f)
                .ToList();
            
            // 빈 프레임 구간 찾기
            List<(int start, int end)> emptyRanges = new List<(int, int)>();
            int currentStart = -1;
            
            for (int frame = waypoint.EntryFrame; frame <= waypoint.ExitFrame; frame++)
            {
                bool hasBox = boxesInWaypoint.Contains(frame);
                
                if (!hasBox && currentStart == -1)
                {
                    // 빈 구간 시작
                    currentStart = frame;
                }
                else if (hasBox && currentStart != -1)
                {
                    // 빈 구간 종료
                    int end = frame - 1;
                    if (end >= currentStart)
                    {
                        emptyRanges.Add((currentStart, end));
                    }
                    currentStart = -1;
                }
            }
            
            // 마지막 빈 구간 처리
            if (currentStart != -1)
            {
                emptyRanges.Add((currentStart, waypoint.ExitFrame));
            }
            
            // 임계값 이상의 연속 부재 구간만 처리
            foreach (var emptyRange in emptyRanges)
            {
                int duration = emptyRange.end - emptyRange.start + 1;
                
                if (duration >= DISAPPEARANCE_THRESHOLD)
                {
                    // 이미 처리된 구간인지 확인
                    bool alreadyProcessed = disappearedRanges.ContainsKey(key) &&
                        disappearedRanges[key].Any(r => 
                            r.startFrame == emptyRange.start && 
                            r.endFrame.HasValue && 
                            r.endFrame.Value == emptyRange.end);
                    
                    if (!alreadyProcessed)
                    {
                        System.Diagnostics.Debug.WriteLine($"[연속 부재 감지] {key}: 프레임 {emptyRange.start}~{emptyRange.end} ({duration}프레임)");
                        
                        // 사라진 구간으로 기록
                        if (!disappearedRanges.ContainsKey(key))
                        {
                            disappearedRanges[key] = new List<(int, int?)>();
                        }
                        disappearedRanges[key].Add((emptyRange.start, emptyRange.end));
                        
                        // 해당 구간의 박스 삭제
                        DeleteBoxesInRange(key, waypoint, emptyRange.start, emptyRange.end);
                    }
                }
            }
        }
        
        /// <summary>
        /// 지정된 구간의 박스 삭제
        /// </summary>
        private void DeleteBoxesInRange(string key, WaypointMarker waypoint, int startFrame, int endFrame)
        {
            int boxId = waypoint.ObjectId;
            int deletedCount = 0;
            
            var boxesToDelete = boundingBoxes
                .Where(b => 
                    b.Label == waypoint.Label &&
                    GetBoxId(b) == boxId &&
                    b.FrameIndex >= startFrame &&
                    b.FrameIndex <= endFrame &&
                    !b.IsDeleted)
                .ToList();
            
            foreach (var box in boxesToDelete)
            {
                box.IsDeleted = true;
                deletedCount++;
            }
            
            if (deletedCount > 0)
            {
                InvalidateBoxCache();
                UpdateBoxCount();
                UpdateBboxListDisplay();
                System.Diagnostics.Debug.WriteLine($"[박스 삭제] {key}: 프레임 {startFrame}~{endFrame}에서 {deletedCount}개 박스 삭제");
            }
        }
        
        #endregion

        #region Tracking Algorithm
        
        // ✅ 여러 Waypoint를 순차적으로 추적 (동시 실행 방지)
        private async void PerformSequentialTracking(List<WaypointMarker> waypoints)
        {
            // ✅ waypoint가 비어있으면 추적하지 않음
            if (waypoints == null || waypoints.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[순차 추적] 추적할 waypoint가 없습니다.");
                return;
            }
            
            // ✅ 순차 추적 시작 시 플래그 설정
            isTrackingInProgress = true;
            System.Diagnostics.Debug.WriteLine($"[순차 추적 시작] {waypoints.Count}개 waypoint 추적 시작, isTrackingInProgress = true");
            
            try
            {
                int totalCount = waypoints.Count;
                int currentIndex = 0;
                int totalBoxesAdded = 0;

                foreach (var waypoint in waypoints)
                {
                    currentIndex++;
                    System.Diagnostics.Debug.WriteLine($"[순차 추적] {currentIndex}/{totalCount} - {waypoint.Label} ID={waypoint.ObjectId}");
                    
                    int beforeCount = boundingBoxes.Count;
                    
                    try
                    {
                        // ✅ 각 Waypoint를 순차적으로 추적 (await으로 대기)
                        await PerformTrackingForWaypointAsync(waypoint, true);
                        
                        int afterCount = boundingBoxes.Count;
                        totalBoxesAdded += (afterCount - beforeCount);
                    }
                    catch (InvalidOperationException ex)
                    {
                        // 추적 불가 상황 (YOLO 작업 중 등)인 경우 해당 waypoint만 건너뜀
                        System.Diagnostics.Debug.WriteLine($"[순차 추적 건너뜀] {waypoint.Label} ID={waypoint.ObjectId}: {ex.Message}");
                        // 사용자에게 알리지 않고 계속 진행 (다른 waypoint는 추적 가능할 수 있음)
                        continue;
                    }
                    catch (Exception ex)
                    {
                        // 다른 예외는 로그만 남기고 계속 진행
                        System.Diagnostics.Debug.WriteLine($"[순차 추적 오류] {waypoint.Label} ID={waypoint.ObjectId}: {ex.Message}");
                        // 사용자에게 알리지 않고 계속 진행
                        continue;
                    }
                }

                // ✅ 모든 추적 완료 후 JSON 저장 및 재로드 (한 번만)
                if (!string.IsNullOrEmpty(currentVideoFile))
                {
                    string videoDir = Path.GetDirectoryName(currentVideoFile);
                    string saveDir = Path.Combine(videoDir, "labels");
                    
                    if (!Directory.Exists(saveDir))
                    {
                        Directory.CreateDirectory(saveDir);
                    }
                    
                    string fileName = Path.GetFileNameWithoutExtension(currentVideoFile) + "_labels.json";
                    string jsonFilePath = Path.Combine(saveDir, fileName);
                    
                    // JSON 저장
                    await Task.Run(() => ExportToJsonExtended(jsonFilePath));
                    
                    // JSON 재로드하여 추적 데이터 기반으로 표시
                    if (File.Exists(jsonFilePath))
                    {
                        await LoadLabelingData(jsonFilePath);
                    }

                    MessageBox.Show(
                        $"✅ {totalCount}개 Waypoint 추적 완료!\n\n" +
                        $"총 {totalBoxesAdded}개 BBox 추가됨\n" +
                        $"💾 JSON 저장 및 재로드 완료\n\n" +
                        $"저장 위치: {jsonFilePath}",
                        "추적 완료",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"✅ {totalCount}개 Waypoint 추적 완료!\n\n" +
                        $"총 {totalBoxesAdded}개 BBox 추가됨",
                        "추적 완료",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"순차 추적 중 오류 발생:\n\n{ex.Message}\n\n{ex.StackTrace}",
                        "오류",
                        MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                // ✅ 순차 추적 종료 시 플래그 해제 (예외 발생 시에도 반드시 해제)
                isTrackingInProgress = false;
                System.Diagnostics.Debug.WriteLine("[순차 추적 종료] isTrackingInProgress = false");
            }
        }

        private async Task PerformTrackingForWaypointAsync(WaypointMarker waypoint, bool useYolo = false)
        {
            try
            {
                // ✅ 추적 중에는 다른 추적 작업 차단 (단순 탐지는 차단하지 않음)
                // 단, PerformSequentialTracking에서 호출되는 경우(useYolo=true)는 이미 isTrackingInProgress가 true이므로 허용
                // 외부에서 직접 호출되는 경우(useYolo=false)에만 중복 추적 차단
                if (!useYolo && isTrackingInProgress)
                {
                    System.Diagnostics.Debug.WriteLine("[추적 작업 차단] 이미 추적이 진행 중이므로 새 추적 작업 불가");
                    MessageBox.Show(
                        "추적이 이미 진행 중입니다.\n기존 추적이 완료될 때까지 기다려주세요.",
                        "작업 중",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    // ⚠️ early return 시에도 호출자에게 알려야 하지만, 
                    // PerformTrackingForWaypointAsync는 void Task이므로 예외를 throw해야 함
                    throw new InvalidOperationException("추적이 진행 중이어서 새 추적을 시작할 수 없습니다.");
                }
                
                // ✅ Entry 프레임에서 waypoint의 ObjectId와 Label에 해당하는 박스만 찾기
                List<BoundingBox> startBoxes = new List<BoundingBox>();
                
                if (waypoint.Label == "person")
                {
                    startBoxes = boundingBoxes
                        .Where(b => b.FrameIndex == waypoint.EntryFrame && 
                                   b.Label == "person" && 
                                   b.PersonId == waypoint.ObjectId)
                        .ToList();
                }
                else if (waypoint.Label == "vehicle")
                {
                    startBoxes = boundingBoxes
                        .Where(b => b.FrameIndex == waypoint.EntryFrame && 
                                   b.Label == "vehicle" && 
                                   b.VehicleId == waypoint.ObjectId)
                        .ToList();
                }
                else if (waypoint.Label == "event")
                {
                    startBoxes = boundingBoxes
                        .Where(b => b.FrameIndex == waypoint.EntryFrame && 
                                   b.Label == "event" && 
                                   b.EventId == waypoint.ObjectId)
                        .ToList();
                }
                
                System.Diagnostics.Debug.WriteLine($"[추적 시작] {waypoint.Label} ID={waypoint.ObjectId}, startBoxes={startBoxes.Count}");

                if (startBoxes.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[추적 스킵] {waypoint.Label} ID={waypoint.ObjectId} - Entry 프레임에 박스 없음");
                    return;
                }

                // ✅ Event는 자동 추적 안함 (전파 기능만 사용)
                if (waypoint.Label == "event")
                {
                    System.Diagnostics.Debug.WriteLine($"[추적 스킵] Event는 자동 추적 안함");
                    return;
                }

                // 추적 중 로딩 폼 생성
                Form loadingForm = new Form
                {
                    Width = 350,
                    Height = 120,
                    Text = useYolo ? "YOLO 추적 중" : "OpenCV 추적 중",
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    TopMost = true
                };

                Label loadingLabel = new Label
                {
                    Text = useYolo ? "YOLO 추적 중... 잠시만 기다려주세요." : "OpenCV 추적 중... 잠시만 기다려주세요.",
                    AutoSize = true,
                    Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold),
                    Location = new System.Drawing.Point(40, 35)
                };

                loadingForm.Controls.Add(loadingLabel);
                loadingForm.Show();
                loadingForm.Refresh();

                List<BoundingBox> allTrackedBoxes = new List<BoundingBox>();

                if (useYolo && isYoloAvailable)
                {
                    
                    // 각 startBox에 대해 개별적으로 YOLO 추적 수행
                    foreach (var startBox in startBoxes)
                    {
                        // ✅ 실패 구간을 받아오는 오버로드 메서드 호출
                        if (trackingEngine is YoloTrackingEngine yoloEngine)
                        {
                            var result = await Task.Run(() =>
                            {
                                var boxes = yoloEngine.TrackObjectsWithFailures(
                                    videoCapture,
                                    startBox,
                                    waypoint.EntryFrame,
                                    waypoint.ExitFrame,
                                    fps,
                                    out List<(int start, int end)> failures,
                                    out int inertiaCount,
                                    out Dictionary<string, int> detectionCount);
                                return new { Boxes = boxes, Failures = failures, InertiaCount = inertiaCount, DetectionCount = detectionCount };
                            });
                            
                            allTrackedBoxes.AddRange(result.Boxes);
                            
                            // ✅ 실패 구간 저장
                            string key = $"{waypoint.Label}_{waypoint.ObjectId}";
                            if (result.Failures != null && result.Failures.Count > 0)
                            {
                                waypointFailureRanges[key] = result.Failures;
                                System.Diagnostics.Debug.WriteLine($"[실패 구간 저장] {key}: {result.Failures.Count}개 구간");
                            }
                            
                            // ✅ YOLO 추적 완료 시 waypoint 구간 동안 탐지한 객체 종류별 로그 출력
                            if (result.DetectionCount != null && result.DetectionCount.Count > 0)
                            {
                                var detectionSummary = string.Join(", ", result.DetectionCount
                                    .OrderBy(kv => kv.Key)
                                    .Select(kv => $"{kv.Key}: {kv.Value}"));
                                System.Diagnostics.Debug.WriteLine(
                                    $"[YOLO 탐지 통계] Waypoint ({waypoint.Label} ID={waypoint.ObjectId}, 프레임 {waypoint.EntryFrame}~{waypoint.ExitFrame}): {detectionSummary}");
                            }
                        }
                        else
                        {
                            var trackedBoxes = await Task.Run(() => trackingEngine.TrackObjects(
                                videoCapture,
                                startBox,
                                waypoint.EntryFrame,
                                waypoint.ExitFrame,
                                fps));
                            
                            allTrackedBoxes.AddRange(trackedBoxes);
                        }
                    }
                }
                else 
                {
                    loadingForm.Close();
                    MessageBox.Show(
                        "YOLO 모델을 사용할 수 없습니다.\n" +
                        $"useYolo: {useYolo}\n" +
                        $"isYoloAvailable: {isYoloAvailable}",
                        "정보",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                loadingForm.Close();

                // 추적된 박스 추가
                foreach (var box in allTrackedBoxes)
                {
                    boundingBoxes.Add(box);
                }
                InvalidateBoxCache();

                // 추적이 성공적으로 완료되면 Entry 프레임의 사용자가 지정한 초기 박스 삭제
                if (allTrackedBoxes.Count > 0)
                {
                    foreach (var startBox in startBoxes)
                    {
                        if (boundingBoxes.Contains(startBox))
                        {
                            boundingBoxes.Remove(startBox);
                        }
                    }
                    
                    InvalidateBoxCache();
                    AddUndoAction(new UndoAction { Type = UndoActionType.Tracking, TrackedBoxes = allTrackedBoxes });
                }
                
                // ✅ 추적 완료 후 사라짐 구간 처리
                ProcessDisappearedRanges(waypoint);
                
                // ✅ 추적 완료 후 연속 박스 부재 구간 자동 감지
                DetectContinuousAbsence(waypoint);

                UpdateBoxCount();
                UpdateBboxListDisplay();

                // ✅ 개별 waypoint 추적 완료 로그
                System.Diagnostics.Debug.WriteLine($"[추적 완료] {waypoint.Label} ID={waypoint.ObjectId}, BBox 추가={allTrackedBoxes.Count}개");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[추적 오류] {waypoint.Label} ID={waypoint.ObjectId}: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"추적 중 오류 발생:\n\n" +
                    $"Waypoint: {waypoint.Label} ID={waypoint.ObjectId}\n" +
                    $"Error: {ex.Message}\n\n" +
                    $"StackTrace:\n{ex.StackTrace}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                throw; // 예외를 상위로 전파하여 순차 추적이 중단되도록
            }
        }

        // ✅ 부분 재추적 함수 (특정 프레임부터 Exit까지)
        private async Task PerformPartialRetrackingAsync(WaypointMarker waypoint, int startFrame)
        {
            // ✅ 추적 시작 시 플래그 설정
            isTrackingInProgress = true;
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"[부분 재추적 시작] {waypoint.Label} ID={waypoint.ObjectId}, Frame {startFrame}~{waypoint.ExitFrame}");

                // ✅ 1. 먼저 startBox 찾기 (삭제 전에 찾아야 함)
                string key = $"{waypoint.Label}_{waypoint.ObjectId}";
                
                // ✅ 재추적 시작 시 해당 객체의 실패 구간 정보 초기화
                if (waypointFailureRanges.ContainsKey(key))
                {
                    waypointFailureRanges.Remove(key);
                    System.Diagnostics.Debug.WriteLine($"[재추적] {key}의 실패 구간 정보 초기화됨");
                }
                
                // ✅ 재추적 시작 시점에서 사라짐 구간 처리 (복귀 시점으로 간주)
                // 현재 프레임(startFrame)이 복귀 시점(b)일 수 있으므로, 이전 사라짐 구간 확정
                ProcessDisappearedRangesAtFrame(waypoint, startFrame);
                
                // ✅ 2. 현재 프레임의 박스를 startBox로 사용 (삭제 전에 찾아야 함)
                BoundingBox startBox = null;
                
                // selectedBox가 올바른 박스인지 확인
                if (selectedBox != null && 
                    selectedBox.FrameIndex == startFrame &&
                    selectedBox.Label == waypoint.Label && 
                    GetBoxId(selectedBox) == waypoint.ObjectId)
                {
                    startBox = selectedBox;
                }
                
                // startBox를 찾지 못했으면 현재 프레임에서 찾기
                if (startBox == null)
                {
                    startBox = boundingBoxes.FirstOrDefault(b =>
                        b.FrameIndex == startFrame &&
                        b.Label == waypoint.Label &&
                        GetBoxId(b) == waypoint.ObjectId);
                }

                if (startBox == null)
                {
                    MessageBox.Show($"Frame {startFrame}에 해당하는 박스를 찾을 수 없습니다.\n\n박스를 선택한 후 재추적을 실행해주세요.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // ✅ 3. 기존 데이터 삭제 (재추적 범위) - startBox는 제외
                int removedCount = 0;
                
                // startFrame부터 waypoint.ExitFrame까지의 기존 박스 삭제 (startBox 제외)
                var boxesToRemove = boundingBoxes.Where(b =>
                    b.Label == waypoint.Label &&
                    GetBoxId(b) == waypoint.ObjectId &&
                    b.FrameIndex >= startFrame &&
                    b.FrameIndex <= waypoint.ExitFrame &&
                    b != startBox).ToList(); // startBox는 삭제하지 않음

                foreach (var box in boxesToRemove)
                {
                    boundingBoxes.Remove(box);
                    removedCount++;
                }

                System.Diagnostics.Debug.WriteLine($"[부분 재추적] {removedCount}개 기존 박스 삭제됨");

                // ✅ 4. 재추적 수행
                if (!isYoloAvailable)
                {
                    MessageBox.Show("YOLO 모델을 사용할 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 로딩 폼
                Form loadingForm = new Form
                {
                    Width = 350,
                    Height = 120,
                    Text = "재추적 중",
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    TopMost = true
                };

                Label loadingLabel = new Label
                {
                    Text = "재추적 중... 잠시만 기다려주세요.",
                    AutoSize = true,
                    Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold),
                    Location = new System.Drawing.Point(40, 35)
                };

                loadingForm.Controls.Add(loadingLabel);
                loadingForm.Show();
                loadingForm.Refresh();

                List<BoundingBox> newTrackedBoxes = new List<BoundingBox>();
                int inertiaAppliedFrames = 0; // ✅ 관성 추적 적용된 프레임 수

                if (trackingEngine is YoloTrackingEngine yoloEngine)
                {
                    var result = await Task.Run(() =>
                    {
                        var boxes = yoloEngine.TrackObjectsWithFailures(
                            videoCapture,
                            startBox,
                            startFrame,
                            waypoint.ExitFrame,
                            fps,
                            out List<(int start, int end)> failures,
                            out int inertiaCount,
                            out Dictionary<string, int> detectionCount);
                        return new { Boxes = boxes, Failures = failures, InertiaCount = inertiaCount, DetectionCount = detectionCount };
                    });

                    newTrackedBoxes = result.Boxes;
                    inertiaAppliedFrames = result.InertiaCount;

                    // ✅ 실패 구간 업데이트
                    if (result.Failures != null && result.Failures.Count > 0)
                    {
                        waypointFailureRanges[key] = result.Failures;
                        System.Diagnostics.Debug.WriteLine($"[재추적 실패 구간] {key}: {result.Failures.Count}개 구간");
                    }
                    
                    // ✅ 재추적 완료 시 waypoint 구간 동안 탐지한 객체 종류별 로그 출력
                    if (result.DetectionCount != null && result.DetectionCount.Count > 0)
                    {
                        var detectionSummary = string.Join(", ", result.DetectionCount
                            .OrderBy(kv => kv.Key)
                            .Select(kv => $"{kv.Key}: {kv.Value}"));
                        System.Diagnostics.Debug.WriteLine(
                            $"[YOLO 탐지 통계] 재추적 Waypoint ({waypoint.Label} ID={waypoint.ObjectId}, 프레임 {startFrame}~{waypoint.ExitFrame}): {detectionSummary}");
                    }
                }

                loadingForm.Close();

                // ✅ 5. 새 데이터 추가 전 중복 제거 확인
                int duplicateCount = 0;
                var framesToCheck = newTrackedBoxes.Select(b => b.FrameIndex).Distinct().ToList();
                
                // 새로 추가할 박스의 프레임에 이미 존재하는 동일한 객체 박스 제거
                foreach (var frame in framesToCheck)
                {
                    var existingBoxes = boundingBoxes.Where(b =>
                        b.FrameIndex == frame &&
                        b.Label == waypoint.Label &&
                        GetBoxId(b) == waypoint.ObjectId).ToList();
                    
                    foreach (var existingBox in existingBoxes)
                    {
                        boundingBoxes.Remove(existingBox);
                        duplicateCount++;
                    }
                }
                
                if (duplicateCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[재추적 중복 제거] {duplicateCount}개 중복 박스 삭제됨");
                }

                // ✅ 6. 새 데이터 추가
                foreach (var box in newTrackedBoxes)
                {
                    boundingBoxes.Add(box);
                }

                // ✅ 7. 정렬
                boundingBoxes.Sort((a, b) => a.FrameIndex.CompareTo(b.FrameIndex));

                InvalidateBoxCache();
                UpdateBoxCount();
                UpdateBboxListDisplay();

                // ✅ 8. 재추적 완료 후 전체 범위(EntryFrame~ExitFrame)에서 재보간 수행
                // 재추적 이전 마지막 성공 프레임과 재추적 후 첫 성공 프레임 사이도 보간하기 위함
                int additionalInterpolatedFrames = await Task.Run(() =>
                {
                    return ApplyInertialInterpolationForWaypoint(waypoint, startFrame);
                });

                System.Diagnostics.Debug.WriteLine($"[부분 재추적 완료] {waypoint.Label} ID={waypoint.ObjectId}, {newTrackedBoxes.Count}개 박스 추가됨, 재추적 범위 보간: {inertiaAppliedFrames}개, 전체 범위 재보간: {additionalInterpolatedFrames}개");

                // ✅ 재보간 후 UI 업데이트
                if (additionalInterpolatedFrames > 0)
                {
                    InvalidateBoxCache();
                    UpdateBoxCount();
                    UpdateBboxListDisplay();
                }

                // ✅ 9. 현재 프레임 새로고침
                pictureBoxVideo.Invalidate();

                // ✅ 10. JSON 자동 저장
                if (!string.IsNullOrEmpty(currentVideoFile))
                {
                    string videoDir = Path.GetDirectoryName(currentVideoFile);
                    string saveDir = Path.Combine(videoDir, "labels");
                    
                    if (!Directory.Exists(saveDir))
                    {
                        Directory.CreateDirectory(saveDir);
                    }
                    
                    string fileName = Path.GetFileNameWithoutExtension(currentVideoFile) + "_labels.json";
                    string jsonFilePath = Path.Combine(saveDir, fileName);
                    
                    // JSON 저장 (재로드하지 않음 - 메모리 상태가 이미 최신)
                    await Task.Run(() => ExportToJsonExtended(jsonFilePath));
                }

                int totalInterpolated = inertiaAppliedFrames + additionalInterpolatedFrames;
                
                // ✅ 재추적 완료 후 사라짐 구간 처리
                ProcessDisappearedRanges(waypoint);
                
                // ✅ 재추적 완료 후 연속 박스 부재 구간 자동 감지
                DetectContinuousAbsence(waypoint);
                
                MessageBox.Show($"재추적이 완료되었습니다.\n추가된 박스: {newTrackedBoxes.Count}개\n\n관성 보간:\n- 재추적 범위: {inertiaAppliedFrames}개 프레임\n- 전체 범위 재보간: {additionalInterpolatedFrames}개 프레임\n- 총 보간: {totalInterpolated}개 프레임\n\n💾 JSON 저장 완료", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[부분 재추적 오류] {waypoint.Label} ID={waypoint.ObjectId}: {ex.Message}");
                MessageBox.Show($"재추적 중 오류 발생:\n\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // ✅ 추적 종료 시 플래그 해제
                isTrackingInProgress = false;
            }
        }

        // ✅ 재추적 완료 후 전체 범위에서 재보간 수행
        private int ApplyInertialInterpolationForWaypoint(WaypointMarker waypoint, int retrackingStartFrame)
        {
            int interpolatedCount = 0;
            
            try
            {
                // waypoint의 전체 범위에서 해당 객체의 모든 박스 찾기
                List<BoundingBox> allBoxes = boundingBoxes
                    .Where(b => b.FrameIndex >= waypoint.EntryFrame && 
                               b.FrameIndex <= waypoint.ExitFrame &&
                               b.Label == waypoint.Label &&
                               GetBoxId(b) == waypoint.ObjectId)
                    .OrderBy(b => b.FrameIndex)
                    .ToList();
                
                if (allBoxes.Count == 0)
                    return 0;
                
                // ✅ 성공 프레임 찾기: 연속된 프레임에서 위치가 변경된 프레임 = Detection 성공 프레임
                var successfulFrames = new Dictionary<int, Rectangle>();
                BoundingBox prevBox = null;
                
                foreach (var box in allBoxes)
                {
                    if (prevBox == null)
                    {
                        // 첫 프레임은 항상 성공으로 간주 (시작 박스)
                        successfulFrames[box.FrameIndex] = box.Rectangle;
                    }
                    else
                    {
                        // 이전 박스와 위치가 다르면 성공 프레임으로 간주
                        if (box.Rectangle.X != prevBox.Rectangle.X || 
                            box.Rectangle.Y != prevBox.Rectangle.Y ||
                            box.Rectangle.Width != prevBox.Rectangle.Width ||
                            box.Rectangle.Height != prevBox.Rectangle.Height)
                        {
                            successfulFrames[box.FrameIndex] = box.Rectangle;
                        }
                        // 재추적 시작 프레임은 항상 성공 프레임으로 간주 (재추적 시작점)
                        else if (box.FrameIndex == retrackingStartFrame)
                        {
                            successfulFrames[box.FrameIndex] = box.Rectangle;
                        }
                    }
                    prevBox = box;
                }
                
                if (successfulFrames.Count < 2)
                    return 0; // 성공 프레임이 2개 미만이면 보간 불가
                
                // ✅ 성공 프레임 목록 정렬
                var sortedSuccessFrames = successfulFrames.Keys.OrderBy(f => f).ToList();
                
                System.Diagnostics.Debug.WriteLine($"[전체 범위 재보간] {waypoint.Label} ID={waypoint.ObjectId}: EntryFrame={waypoint.EntryFrame}~ExitFrame={waypoint.ExitFrame}, 성공 프레임 {successfulFrames.Count}개");
                
                // ✅ 각 실패 프레임에 대해 보간 적용
                for (int frameIdx = waypoint.EntryFrame; frameIdx <= waypoint.ExitFrame; frameIdx++)
                {
                    // 이미 성공 프레임이면 건너뛰기
                    if (successfulFrames.ContainsKey(frameIdx))
                        continue;
                    
                    // 해당 프레임의 박스 찾기
                    var box = allBoxes.FirstOrDefault(b => b.FrameIndex == frameIdx);
                    if (box == null)
                        continue;
                    
                    // 앞뒤 성공 프레임 찾기
                    int? prevSuccessFrame = null;
                    int? nextSuccessFrame = null;
                    
                    // 이전 성공 프레임 찾기
                    for (int i = sortedSuccessFrames.Count - 1; i >= 0; i--)
                    {
                        if (sortedSuccessFrames[i] < frameIdx)
                        {
                            prevSuccessFrame = sortedSuccessFrames[i];
                            break;
                        }
                    }
                    
                    // 다음 성공 프레임 찾기
                    for (int i = 0; i < sortedSuccessFrames.Count; i++)
                    {
                        if (sortedSuccessFrames[i] > frameIdx)
                        {
                            nextSuccessFrame = sortedSuccessFrames[i];
                            break;
                        }
                    }
                    
                    // 앞뒤 성공 프레임이 모두 있으면 보간 적용
                    if (prevSuccessFrame.HasValue && nextSuccessFrame.HasValue)
                    {
                        var prevRect = successfulFrames[prevSuccessFrame.Value];
                        var nextRect = successfulFrames[nextSuccessFrame.Value];
                        
                        int totalFramesBetween = nextSuccessFrame.Value - prevSuccessFrame.Value;
                        int currentOffset = frameIdx - prevSuccessFrame.Value;
                        
                        // 선형 보간 계산 (위치 및 크기 모두 보간)
                        double ratio = (double)currentOffset / totalFramesBetween;
                        
                        int interpolatedX = (int)(prevRect.X + (nextRect.X - prevRect.X) * ratio);
                        int interpolatedY = (int)(prevRect.Y + (nextRect.Y - prevRect.Y) * ratio);
                        int interpolatedWidth = (int)(prevRect.Width + (nextRect.Width - prevRect.Width) * ratio);
                        int interpolatedHeight = (int)(prevRect.Height + (nextRect.Height - prevRect.Height) * ratio);
                        
                        // 박스 위치 및 크기 업데이트
                        box.Rectangle = new Rectangle(interpolatedX, interpolatedY, interpolatedWidth, interpolatedHeight);
                        
                        interpolatedCount++;
                        
                        // per-frame interpolation log suppressed
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"[전체 범위 재보간 완료] {waypoint.Label} ID={waypoint.ObjectId}: {interpolatedCount}개 프레임 보간됨");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[전체 범위 재보간 오류] {waypoint.Label} ID={waypoint.ObjectId}: {ex.Message}");
            }
            
            return interpolatedCount;
        }

        #endregion

        #region JSON Load/Export
        private async Task LoadLabelingData(string videoFilePath)
        {
            Form loadingForm = null;
            Label loadingLabel = null;
            ProgressBar progressBar = null;
            
            try
            {
                string videoDir = Path.GetDirectoryName(videoFilePath);
                if (string.IsNullOrEmpty(videoDir) || !Directory.Exists(videoDir))
                    return;

                string saveDir = Path.Combine(videoDir, "labels");
                if (!Directory.Exists(saveDir))
                    return;

                string fileName = Path.GetFileNameWithoutExtension(videoFilePath) + "_labels.json";
                string loadPath = Path.Combine(saveDir, fileName);

                if (!File.Exists(loadPath))
                    return;

                // ✅ 1. 파일 크기 체크 및 경고
                FileInfo fileInfo = new FileInfo(loadPath);
                long fileSizeMB = fileInfo.Length / (1024 * 1024);
                const long WARNING_SIZE_MB = 100;

                if (fileSizeMB > WARNING_SIZE_MB)
                {
                    var result = MessageBox.Show(
                        $"대용량 JSON 파일을 로드하려고 합니다.\n\n" +
                        $"파일 크기: {fileSizeMB:N0} MB\n" +
                        $"권장 크기: {WARNING_SIZE_MB} MB 이하\n\n" +
                        $"계속하시겠습니까?",
                        "대용량 파일 경고",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result != DialogResult.Yes)
                        return;
                }

                // ✅ 2. 백업 파일 생성
                string backupPath = loadPath + ".backup";
                try
                {
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                    File.Copy(loadPath, backupPath);
                    System.Diagnostics.Debug.WriteLine($"[JSON 로드] 백업 파일 생성: {backupPath}");
                }
                catch (Exception backupEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[JSON 로드] 백업 파일 생성 실패: {backupEx.Message}");
                    // 백업 실패해도 로드는 계속 진행
                }

                // ✅ 3. 로딩 폼 생성 (진행률 표시)
                loadingForm = new Form
                {
                    Width = 400,
                    Height = 150,
                    Text = "JSON 로드 중",
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    TopMost = true
                };

                loadingLabel = new Label
                {
                    Text = $"JSON 파일 로드 중... ({fileSizeMB:N0} MB)",
                    AutoSize = true,
                    Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold),
                    Location = new System.Drawing.Point(20, 20)
                };

                progressBar = new ProgressBar
                {
                    Location = new System.Drawing.Point(20, 50),
                    Size = new System.Drawing.Size(350, 23),
                    Style = ProgressBarStyle.Marquee
                };

                loadingForm.Controls.Add(loadingLabel);
                loadingForm.Controls.Add(progressBar);
                loadingForm.Show();
                loadingForm.Refresh();

                // ✅ 4. FileStream + 버퍼링으로 메모리 효율적 로드
                LabelingDataExtended labelingData = null;
                
                await Task.Run(() =>
                {
                    try
                    {
                        // FileStream을 사용하여 버퍼링된 읽기
                        using (FileStream fileStream = new FileStream(loadPath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192))
                        using (StreamReader streamReader = new StreamReader(fileStream, System.Text.Encoding.UTF8, true, 8192))
                        {
                            string json = streamReader.ReadToEnd();
                            
                            // UI 스레드에서 진행률 업데이트
                            if (loadingForm != null && loadingForm.InvokeRequired)
                            {
                                loadingForm.Invoke(new Action(() =>
                                {
                                    loadingLabel.Text = "JSON 파싱 중...";
                                    progressBar.Style = ProgressBarStyle.Marquee;
                                }));
                            }

                            labelingData = JsonConvert.DeserializeObject<LabelingDataExtended>(json);
                        }
                    }
                    catch (OutOfMemoryException oomEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[JSON 로드] 메모리 부족: {oomEx.Message}");
                        throw new Exception($"메모리 부족으로 파일을 로드할 수 없습니다.\n파일이 너무 큽니다 ({fileSizeMB:N0} MB).\n\n백업 파일에서 복구를 시도하시겠습니까?", oomEx);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[JSON 로드] 파일 읽기 오류: {ex.Message}");
                        throw;
                    }
                });

                if (labelingData == null || labelingData.Annotations == null)
                {
                    if (loadingForm != null)
                        loadingForm.Close();
                    return;
                }

                // ✅ 5. 트랜잭션 방식: 임시 변수에 데이터 저장 (로드 성공 시에만 반영)
                var tempBoundingBoxes = new List<BoundingBox>();
                var tempWaypointMarkers = new List<WaypointMarker>();
                var tempCategoryMap = new Dictionary<int, CategoryData>();
                var tempFrameTimestampMap = new Dictionary<int, string>();
                var tempWaypointFailureRanges = new Dictionary<string, List<(int start, int end)>>();
                int tempNextAnnotationId = 1;

                // ✅ 실패 구간 정보 복원 (임시)
                if (labelingData.FailureRanges != null)
                {
                    tempWaypointFailureRanges = labelingData.FailureRanges;
                    System.Diagnostics.Debug.WriteLine($"[JSON 로드] 실패 구간 정보 복원됨: {tempWaypointFailureRanges.Count}개 객체");
                }

                // ImageId → FrameNumber 매핑 생성 및 FrameNumber → Timestamp 매핑 생성 (임시)
                var imageIdToFrameNumber = new Dictionary<int, int>();
                if (labelingData.Images != null)
                {
                    foreach (var image in labelingData.Images)
                    {
                        imageIdToFrameNumber[image.Id] = image.FrameNumber;
                        if (!string.IsNullOrEmpty(image.Timestamp))
                        {
                            tempFrameTimestampMap[image.FrameNumber] = image.Timestamp;
                        }
                    }
                }

                if (labelingData.Categories != null)
                {
                    foreach (var category in labelingData.Categories)
                    {
                        tempCategoryMap[category.Id] = category;
                    }
                }

                foreach (var annotation in labelingData.Annotations)
                {
                    if (annotation.Bbox == null || annotation.Bbox.Length < 4)
                        continue;

                    int trackId = annotation.TrackId;
                    string label = "person";

                    // CategoryId 범위로 라벨 결정 (더 정확함)
                    int catId = annotation.CategoryId;
                    if (catId >= 1 && catId <= 20)
                    {
                        label = "person";
                    }
                    else if (catId >= 21 && catId <= 24)
                    {
                        label = "vehicle";
                    }
                    else if (catId >= 25 && catId <= 28)
                    {
                        label = "event";
                    }
                    else if (categoryMap.ContainsKey(catId))
                    {
                        // fallback: 카테고리 이름으로 판단
                        string categoryName = categoryMap[catId].Name;
                        if (categoryName.Contains("car") || categoryName.Contains("motorcycle") || 
                            categoryName.Contains("scooter") || categoryName.Contains("bicycle"))
                            label = "vehicle";
                        else if (categoryName.Contains("contact") || categoryName.Contains("exchange") || 
                                 categoryName.Contains("board") || categoryName.Contains("final"))
                            label = "event";
                        else if (categoryName.StartsWith("person"))
                            label = "person";
                    }

                    // ImageId로 실제 프레임 번호 찾기
                    int frameNumber = annotation.ImageId; // 기본값
                    if (imageIdToFrameNumber.ContainsKey(annotation.ImageId))
                    {
                        frameNumber = imageIdToFrameNumber[annotation.ImageId];
                    }

                    // EventId 계산 로직 수정: CategoryId에서 역산
                    int personId = 0;
                    int vehicleId = 0;
                    int eventId = 0;
                    
                    if (label == "person")
                    {
                        personId = trackId;
                    }
                    else if (label == "vehicle")
                    {
                        // Vehicle: CategoryId 21~24 → VehicleId 1~4
                        vehicleId = catId >= 21 && catId <= 24 ? (catId - 20) : trackId;
                    }
                    else if (label == "event")
                    {
                        // Event: CategoryId 25~28 → EventId 1~4
                        eventId = catId >= 25 && catId <= 28 ? (catId - 24) : trackId;
                    }

                    var box = new BoundingBox
                    {
                        FrameIndex = frameNumber, // 실제 프레임 번호 사용
                        Rectangle = new Rectangle(annotation.Bbox[0], annotation.Bbox[1], annotation.Bbox[2], annotation.Bbox[3]),
                        Label = label,
                        PersonId = personId,
                        VehicleId = vehicleId,
                        EventId = eventId,
                        Action = "waypoint"
                    };

                    tempBoundingBoxes.Add(box);

                    if (annotation.Id >= tempNextAnnotationId)
                        tempNextAnnotationId = annotation.Id + 1;

                    // ✅ 웨이포인트 정보 복원 (Label + ObjectId별로 분리)
                    if (annotation.TrackInfo != null && 
                        annotation.TrackInfo.Entry != null && 
                        annotation.TrackInfo.Exit != null)
                    {
                        int entryFrame = annotation.TrackInfo.Entry.Frame;
                        int exitFrame = annotation.TrackInfo.Exit.Frame;

                        // ObjectId 결정 (Label에 따라)
                        int objectId = 0;
                        if (box.Label == "person") objectId = box.PersonId;
                        else if (box.Label == "vehicle") objectId = box.VehicleId;
                        else if (box.Label == "event") objectId = box.EventId;

                        // ✅ 같은 Label, ObjectId, Entry, Exit를 가진 Waypoint가 이미 있는지 확인 (임시 리스트에서)
                        bool waypointExists = tempWaypointMarkers.Any(w => 
                            w.Label == box.Label &&
                            w.ObjectId == objectId &&
                            w.EntryFrame == entryFrame && 
                            w.ExitFrame == exitFrame);

                        if (!waypointExists)
                        {
                            System.Drawing.Color waypointColor;
                            
                            // Label별로 색상 지정
                            if (box.Label == "person")
                            {
                                waypointColor = System.Drawing.Color.FromArgb(255, 107, 107); // 빨강
                            }
                            else if (box.Label == "vehicle")
                            {
                                waypointColor = System.Drawing.Color.FromArgb(107, 158, 255); // 파랑
                            }
                            else if (box.Label == "event")
                            {
                                waypointColor = System.Drawing.Color.FromArgb(107, 255, 107); // 초록
                            }
                            else
                            {
                                waypointColor = markerColors[tempWaypointMarkers.Count % markerColors.Length];
                            }
                            
                            var waypoint = new WaypointMarker
                            {
                                ObjectId = objectId, // ✅ PersonId/VehicleId/EventId 저장
                                Label = box.Label, // Person/Vehicle/Event 라벨 유지
                                EntryFrame = entryFrame,
                                ExitFrame = exitFrame,
                                EntryTime = FormatFrameTime(entryFrame),
                                ExitTime = FormatFrameTime(exitFrame),
                                MarkerColor = waypointColor,
                                InteractingObject = (box.Label == "event") ? (annotation.InteractingObject ?? "") : null
                            };

                            tempWaypointMarkers.Add(waypoint);
                        }
                        else
                        {
                            // 이미 있는 웨이포인트에 대해, event라면 비어있을 때만 interacting_object를 보완
                            if (box.Label == "event" && !string.IsNullOrWhiteSpace(annotation.InteractingObject))
                            {
                                var existing = tempWaypointMarkers.First(w =>
                                    w.Label == box.Label && w.ObjectId == objectId &&
                                    w.EntryFrame == entryFrame && w.ExitFrame == exitFrame);
                                if (string.IsNullOrWhiteSpace(existing.InteractingObject))
                                {
                                    existing.InteractingObject = annotation.InteractingObject;
                                }
                            }
                        }
                    }
                }

                // ✅ Person attributes 복원 (waypoint 생성 후)
                foreach (var annotation in labelingData.Annotations)
                {
                    if (annotation.Bbox == null || annotation.Bbox.Length < 4)
                        continue;

                    // CategoryId로 라벨 결정
                    int catId = annotation.CategoryId;
                    string label = "person";
                    if (catId >= 1 && catId <= 20)
                    {
                        label = "person";
                    }
                    else if (catId >= 21 && catId <= 24)
                    {
                        label = "vehicle";
                    }
                    else if (catId >= 25 && catId <= 28)
                    {
                        label = "event";
                    }

                    if (label == "person" && annotation.Attributes != null && annotation.Attributes.Count > 0)
                    {
                        int personId = annotation.TrackId;
                        
                        // ImageId로 실제 프레임 번호 찾기
                        int frameNumber = annotation.ImageId;
                        if (imageIdToFrameNumber.ContainsKey(annotation.ImageId))
                        {
                            frameNumber = imageIdToFrameNumber[annotation.ImageId];
                        }
                        
                        // 현재 박스가 속한 waypoint 찾기 (임시 리스트에서)
                        var matchingWaypoint = tempWaypointMarkers.FirstOrDefault(w =>
                            w.Label == "person" &&
                            w.ObjectId == personId &&
                            frameNumber >= w.EntryFrame &&
                            frameNumber <= w.ExitFrame);
                        
                        int waypointEntryFrame = matchingWaypoint != null ? matchingWaypoint.EntryFrame : frameNumber;
                        
                        // 각 속성을 저장
                        foreach (var kvp in annotation.Attributes)
                        {
                            if (kvp.Value != null)
                            {
                                personAttributeStore.SetAttribute(personId, waypointEntryFrame, kvp.Key, kvp.Value);
                            }
                        }
                    }
                }

                // ✅ 6. 트랜잭션 커밋: 모든 데이터가 성공적으로 로드되었으므로 실제 데이터 구조에 반영
                // UI 스레드에서 실행되어야 함
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        // 기존 데이터 초기화
                        boundingBoxes.Clear();
                        waypointMarkers.Clear();
                        categoryMap.Clear();
                        selectedBox = null;
                        undoStack.Clear();
                        redoStack.Clear();
                        lastRenderedWaypoint = null;
                        nextAnnotationId = tempNextAnnotationId;

                        // 임시 데이터를 실제 데이터 구조에 복사
                        boundingBoxes.AddRange(tempBoundingBoxes);
                        waypointMarkers.AddRange(tempWaypointMarkers);
                        categoryMap = tempCategoryMap;
                        frameTimestampMap = tempFrameTimestampMap;
                        waypointFailureRanges = tempWaypointFailureRanges;

                        // UI 전체 갱신
                        UpdateWaypointListView();
                        UpdateBboxListDisplay();
                        InvalidateBoxCache();
                        UpdateBoxCount();
                        pictureBoxVideo.Invalidate();
                    }));
                }
                else
                {
                    // 기존 데이터 초기화
                    boundingBoxes.Clear();
                    waypointMarkers.Clear();
                    categoryMap.Clear();
                    selectedBox = null;
                    undoStack.Clear();
                    redoStack.Clear();
                    lastRenderedWaypoint = null;
                    nextAnnotationId = tempNextAnnotationId;

                    // 임시 데이터를 실제 데이터 구조에 복사
                    boundingBoxes.AddRange(tempBoundingBoxes);
                    waypointMarkers.AddRange(tempWaypointMarkers);
                    categoryMap = tempCategoryMap;
                    frameTimestampMap = tempFrameTimestampMap;
                    waypointFailureRanges = tempWaypointFailureRanges;

                    // UI 전체 갱신
                    UpdateWaypointListView();
                    UpdateBboxListDisplay();
                    InvalidateBoxCache();
                    UpdateBoxCount();
                    pictureBoxVideo.Invalidate();
                }

                // 로딩 폼 닫기
                if (loadingForm != null)
                {
                    loadingForm.Close();
                    loadingForm.Dispose();
                }
                
            }
            catch (OutOfMemoryException oomEx)
            {
                // 로딩 폼 닫기
                if (loadingForm != null)
                {
                    loadingForm.Close();
                    loadingForm.Dispose();
                }

                string videoDir = Path.GetDirectoryName(videoFilePath);
                string saveDir = Path.Combine(videoDir, "labels");
                string fileName = Path.GetFileNameWithoutExtension(videoFilePath) + "_labels.json.backup";
                string backupPath = Path.Combine(saveDir, fileName);

                var result = MessageBox.Show(
                    $"메모리 부족으로 파일을 로드할 수 없습니다.\n\n" +
                    $"오류: {oomEx.Message}\n\n" +
                    $"백업 파일에서 복구를 시도하시겠습니까?\n" +
                    $"(백업 파일: {backupPath})",
                    "메모리 부족 오류",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Error);

                if (result == DialogResult.Yes && File.Exists(backupPath))
                {
                    try
                    {
                        string originalPath = backupPath.Replace(".backup", "");
                        File.Copy(backupPath, originalPath, true);
                        MessageBox.Show("백업 파일에서 복구되었습니다. 다시 시도해주세요.", "복구 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception restoreEx)
                    {
                        MessageBox.Show($"백업 파일 복구 실패: {restoreEx.Message}", "복구 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                // 로딩 폼 닫기
                if (loadingForm != null)
                {
                    loadingForm.Close();
                    loadingForm.Dispose();
                }

                string errorMessage = $"라벨링 데이터 로드 오류: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $"\n\n상세 정보: {ex.InnerException.Message}";
                }

                System.Diagnostics.Debug.WriteLine($"[JSON 로드 오류] {errorMessage}\n{ex.StackTrace}");

                MessageBox.Show(errorMessage, "로드 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveCurrentLabelingData()
        {
            if (string.IsNullOrEmpty(currentVideoFile) || boundingBoxes.Count == 0)
                return;

            try
            {
                string videoDir = Path.GetDirectoryName(currentVideoFile);
                if (string.IsNullOrEmpty(videoDir) || !Directory.Exists(videoDir))
                {
                    MessageBox.Show("비디오 파일의 디렉토리를 찾을 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string saveDir = Path.Combine(videoDir, "labels");
                
                // 디렉토리 생성 시 예외 처리
                try
                {
                    if (!Directory.Exists(saveDir))
                    {
                        Directory.CreateDirectory(saveDir);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"라벨 저장 디렉토리 생성 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string fileName = Path.GetFileNameWithoutExtension(currentVideoFile) + "_labels.json";
                string savePath = Path.Combine(saveDir, fileName);

                ExportToJsonExtended(savePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"라벨링 데이터 저장 중 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 현재 비디오에 대한 JSON 파일 삭제 및 UI 초기화
        /// </summary>
        private void DeleteJsonFileForCurrentVideo()
        {
            try
            {
                if (string.IsNullOrEmpty(currentVideoFile))
                {
                    MessageBox.Show("현재 열려있는 영상이 없습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string videoDir = Path.GetDirectoryName(currentVideoFile);
                if (string.IsNullOrEmpty(videoDir) || !Directory.Exists(videoDir))
                    return;

                string saveDir = Path.Combine(videoDir, "labels");
                string fileName = Path.GetFileNameWithoutExtension(currentVideoFile) + "_labels.json";
                string jsonPath = Path.Combine(saveDir, fileName);

                if (!File.Exists(jsonPath))
                {
                    MessageBox.Show("삭제할 JSON 파일이 존재하지 않습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 삭제 확인
                DialogResult result = MessageBox.Show(
                    $"현재 영상의 라벨링 데이터를 삭제하시겠습니까?\n\n파일: {fileName}\n\n이 작업은 되돌릴 수 없습니다.",
                    "JSON 삭제 확인",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    File.Delete(jsonPath);
                    
                    // UI 초기화: 메모리의 모든 라벨링 데이터 삭제
                    boundingBoxes.Clear();
                    waypointMarkers.Clear();
                    selectedBox = null;
                    selectedWaypoint = null;
                    entryFrameIndex = null;
                    undoStack.Clear();
                    redoStack.Clear();
                    
                    // UI 업데이트
                    UpdateWaypointListView();
                    UpdateBboxListDisplay();
                    UpdateBoxCount();
                    pictureBoxVideo.Invalidate();
                    panelTimeline.Invalidate();
                    
                    MessageBox.Show(
                        $"✅ JSON 파일과 현재 작업 데이터가 모두 삭제되었습니다.\n\n파일: {fileName}",
                        "삭제 완료",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"JSON 파일 삭제 중 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// JSON 삭제 버튼 클릭 이벤트
        /// </summary>
        private void btnDeleteJson_Click(object sender, EventArgs e)
        {
            DeleteJsonFileForCurrentVideo();
        }

        private void ExportToJsonExtended(string filePath)
        {
            try
            {
                var images = new List<ImageInfo>();
                var annotations = new List<AnnotationData>();
                var categories = new Dictionary<int, CategoryData>();

                // ✅ 삭제되지 않은 박스만 JSON에 저장
                var frameGroups = boundingBoxes.Where(b => !b.IsDeleted).GroupBy(b => b.FrameIndex).OrderBy(g => g.Key);
                int imageId = 0;

                foreach (var frameGroup in frameGroups)
                {
                    double frameSeconds = frameGroup.Key / fps;
                    DateTime frameTime = DateTime.Now.AddSeconds(frameSeconds);

                    // 자막에서 타임스탬프 추출 시도
                    string subtitleTimestamp = GetSubtitleTimestampForFrame(frameGroup.Key);
                    string timestamp = subtitleTimestamp ?? frameTime.ToString("yyyy-MM-ddTHH:mm:ss.fff");

                    var imageInfo = new ImageInfo
                    {
                        Id = imageId,
                        Height = (int)videoCapture.Get(VideoCaptureProperties.FrameHeight),
                        Width = (int)videoCapture.Get(VideoCaptureProperties.FrameWidth),
                        FrameNumber = frameGroup.Key,
                        Timestamp = timestamp
                    };

                    images.Add(imageInfo);

                    foreach (var box in frameGroup)
                    {
                        // 박스의 라벨 타입에 맞는 ID 가져오기
                        int boxId = GetBoxId(box);
                        
                        // 스펙에 맞는 Category ID와 Name 사용
                        int categoryId = GetCategoryId(box.Label, boxId);
                        string categoryName = GetCategoryName(box.Label, boxId);
                        
                        if (!categories.ContainsKey(categoryId))
                        {
                            categories[categoryId] = new CategoryData
                            {
                                Id = categoryId,
                                Name = categoryName,
                                Supercategory = box.Label
                            };
                        }

                        // ✅ 수정: 현재 박스가 속한 특정 Waypoint를 찾아서 그 Entry/Exit 사용
                        int entryFrame = box.FrameIndex;
                        int exitFrame = box.FrameIndex;
                        
                        // 현재 박스가 속한 Waypoint 찾기
                        var matchingWaypoint = waypointMarkers.FirstOrDefault(w => 
                            w.Label == box.Label &&
                            w.ObjectId == boxId &&
                            box.FrameIndex >= w.EntryFrame &&
                            box.FrameIndex <= w.ExitFrame);
                        
                        if (matchingWaypoint != null)
                        {
                            // Waypoint가 있으면 그 Entry/Exit 사용
                            entryFrame = matchingWaypoint.EntryFrame;
                            exitFrame = matchingWaypoint.ExitFrame;
                        }
                        else
                        {
                            // Waypoint가 없으면 같은 ObjectId의 모든 박스 범위 사용
                            var sameObjectBoxes = boundingBoxes
                            .Where(b => b.Label == box.Label && GetBoxId(b) == boxId)
                                .ToList();
                            
                            if (sameObjectBoxes.Any())
                            {
                                entryFrame = sameObjectBoxes.Min(b => b.FrameIndex);
                                exitFrame = sameObjectBoxes.Max(b => b.FrameIndex);
                            }
                        }

                        // 자막에서 타임스탬프 추출 시도
                        string entryTimestamp = GetSubtitleTimestampForFrame(entryFrame);
                        string exitTimestamp = GetSubtitleTimestampForFrame(exitFrame);

                        // 자막 타임스탬프가 없으면 계산된 시간 사용
                        double entrySeconds = entryFrame / fps;
                        double exitSeconds = exitFrame / fps;
                        DateTime entryTime = DateTime.Now.AddSeconds(entrySeconds);
                        DateTime exitTime = DateTime.Now.AddSeconds(exitSeconds);

                        var annotation = new AnnotationData
                        {
                            Id = nextAnnotationId++,
                            ImageId = imageId,
                            CategoryId = categoryId,
                            Bbox = new int[] { box.Rectangle.X, box.Rectangle.Y, box.Rectangle.Width, box.Rectangle.Height },
                            Area = box.Rectangle.Width * box.Rectangle.Height,
                            Iscrowd = 0,
                            TrackId = boxId,
                            TrackInfo = new TrackInfo
                            {
                                Entry = new TrackEntry
                                {
                                    Frame = entryFrame,
                                    Timestamp = entryTimestamp ?? entryTime.ToString("yyyy-MM-ddTHH:mm:ss.fff")
                                },
                                Exit = new TrackEntry
                                {
                                    Frame = exitFrame,
                                    Timestamp = exitTimestamp ?? exitTime.ToString("yyyy-MM-ddTHH:mm:ss.fff")
                                },
                                CurrentClipCount = 1
                            }
                        };

                        // Event인 경우 상호작용 객체 텍스트 포함 (해당 박스가 속한 웨이포인트에서 가져옴)
                        if (box.Label == "event" && matchingWaypoint != null && !string.IsNullOrWhiteSpace(matchingWaypoint.InteractingObject))
                        {
                            annotation.InteractingObject = matchingWaypoint.InteractingObject;
                        }

                        // Person인 경우 attributes 포함 (모든 속성 포함, null도 포함)
                        if (box.Label == "person")
                        {
                            // 모든 속성 목록 정의
                            var allAttributeNames = new HashSet<string>
                            {
                                // View
                                "Occlusion", "BodyView",
                                // Biometric
                                "Age", "Gender", "Height", "Weight", "BodyPosture", "Face",
                                // Head/Hair
                                "HairLength", "HairStyle", "HairColor",
                                // UpperCloth
                                "UpperClothType", "UpperClothSleeve", "UpperClothPattern", "UpperClothColor",
                                // LowerCloth
                                "LowerClothType", "LowerClothLegwear", "LowerClothLength", "LowerClothPattern", "LowerClothColor", "LowerClothMaterial",
                                // Footwear
                                "FootwearType", "FootwearColor",
                                // Accessory
                                "HeadwearType", "FacewearType", "BagType", "CarringItemType",
                                // Action
                                "ActionType"
                            };
                            
                            // 현재 프레임의 속성 가져오기
                            var currentAttributes = personAttributeStore.GetAllAttributes(box.PersonId, box.FrameIndex, waypointMarkers);
                            
                            // 모든 속성을 포함하는 Dictionary 생성 (없는 속성은 null로)
                            var allAttributes = new Dictionary<string, object>();
                            foreach (string attrName in allAttributeNames)
                            {
                                if (currentAttributes != null && currentAttributes.ContainsKey(attrName))
                                {
                                    allAttributes[attrName] = currentAttributes[attrName]; // 값이 null이어도 포함
                                }
                                else
                                {
                                    allAttributes[attrName] = null; // 설정되지 않은 속성도 null로 포함
                                }
                            }
                            
                            annotation.Attributes = allAttributes;
                        }

                        annotations.Add(annotation);
                    }

                    imageId++;
                }

                var labelingData = new LabelingDataExtended
                {
                    Info = new VideoInfoExtended
                    {
                        Description = "Extended COCO with Tracking",
                        Version = "1.0",
                        Year = DateTime.Now.Year,
                        DateCreated = DateTime.Now.ToString("yyyy-MM-dd"),
                        VideoFile = Path.GetFileName(currentVideoFile)
                    },
                    Licenses = new List<object>(),
                    Images = images,
                    Annotations = annotations,
                    Categories = categories.Values.ToList(),
                    FailureRanges = waypointFailureRanges
                };

                // null 값도 포함하여 직렬화
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Include,
                    Formatting = Formatting.Indented
                };
                string json = JsonConvert.SerializeObject(labelingData, settings);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"JSON 내보내기 오류: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        /// <summary>
        /// Q키: Event 종료 - 현재 프레임부터 Exit까지 Event 박스 삭제
        /// </summary>
        private void TerminateEventFromCurrentFrame()
        {
            // 현재 프레임에 Event 박스가 있는지 확인
            var eventBoxesAtFrame = boundingBoxes
                .Where(b => b.FrameIndex == currentFrameIndex && b.Label == "event")
                .ToList();
            
            if (eventBoxesAtFrame.Count == 0)
            {
                MessageBox.Show(
                    "현재 프레임에 Event 박스가 없습니다.\n\n" +
                    "Event 박스가 있는 프레임으로 이동 후 Q키를 눌러주세요.",
                    "Event 없음",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            
            // 여러 Event가 있을 경우 선택 다이얼로그
            BoundingBox targetEvent = null;
            if (eventBoxesAtFrame.Count == 1)
            {
                targetEvent = eventBoxesAtFrame[0];
            }
            else
            {
                // 여러 Event 중 선택
                string[] eventTypes = { "contact", "exchange", "board", "final_exchange", "throw" };
                var eventNames = eventBoxesAtFrame.Select(b => 
                {
                    string name = b.EventId > 0 && b.EventId <= eventTypes.Length 
                        ? eventTypes[b.EventId - 1] 
                        : b.EventId.ToString();
                    return $"{name}_{b.EventId:D2}";
                }).ToArray();
                
                // 간단하게 첫 번째 것 선택 (나중에 다이얼로그로 변경 가능)
                targetEvent = eventBoxesAtFrame[0];
            }
            
            // 현재 Event의 Waypoint 찾기 (Event Label, EventId로 매칭)
            var eventWaypoint = waypointMarkers.FirstOrDefault(w =>
                w.Label == "event" &&
                currentFrameIndex >= w.EntryFrame &&
                currentFrameIndex <= w.ExitFrame);
            
            if (eventWaypoint == null)
            {
                MessageBox.Show(
                    "Event Waypoint를 찾을 수 없습니다.",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            
            // 현재 프레임부터 영상 끝까지 삭제 (Q키로 종료)
            var boxesToDelete = boundingBoxes
                .Where(b => 
                    b.Label == "event" &&
                    b.EventId == targetEvent.EventId &&
                    b.Rectangle.X == targetEvent.Rectangle.X &&
                    b.Rectangle.Y == targetEvent.Rectangle.Y &&
                    b.Rectangle.Width == targetEvent.Rectangle.Width &&
                    b.Rectangle.Height == targetEvent.Rectangle.Height &&
                    b.FrameIndex >= currentFrameIndex)
                .ToList();
            
            if (boxesToDelete.Count == 0)
            {
                MessageBox.Show(
                    "삭제할 Event 박스가 없습니다.",
                    "알림",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            
            // 삭제 확인
            string[] eventTypes2 = { "contact", "exchange", "board", "final_exchange", "throw" };
            string eventName = targetEvent.EventId > 0 && targetEvent.EventId <= eventTypes2.Length 
                ? eventTypes2[targetEvent.EventId - 1] 
                : targetEvent.EventId.ToString();
            
            var result = MessageBox.Show(
                $"'{eventName}' Event를 현재 프레임({currentFrameIndex})에서 종료하시겠습니까?\n\n" +
                $"삭제될 프레임: {currentFrameIndex} ~ 영상 끝 ({boxesToDelete.Count}개)",
                "Event 종료",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            
            if (result == DialogResult.Yes)
            {
                foreach (var box in boxesToDelete)
                {
                    boundingBoxes.Remove(box);
                }
                
                // Event Waypoint의 ExitFrame을 현재 프레임 -1로 업데이트
                eventWaypoint.ExitFrame = currentFrameIndex - 1;
                TimeSpan exitTime = TimeSpan.FromSeconds((currentFrameIndex - 1) / fps);
                eventWaypoint.ExitTime = exitTime.ToString(@"hh\:mm\:ss");
                
                InvalidateBoxCache();
                UpdateBoxCount();
                UpdateBboxListDisplay();
                UpdateWaypointListView(); // Waypoint ListView 업데이트
                panelTimeline.Invalidate();
                pictureBoxVideo.Invalidate();
                
                MessageBox.Show(
                    $"'{eventName}' Event가 프레임 {currentFrameIndex}에서 종료되었습니다.\n\n" +
                    $"삭제된 박스: {boxesToDelete.Count}개",
                    "완료",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        #endregion

        #region Keyboard Shortcuts
        
        /// <summary>
        /// 방향키 등 특수 키를 Form 레벨에서 먼저 처리하여 패널 포커스 문제 해결
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            try
            {
                // ✅ YOLO 추적/탐지 중에는 방향키(5초 이동) 차단 (중요!)
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[방향키 차단] YOLO 추적/탐지 중이므로 방향키 입력 무시");
                    return true; // 이벤트 처리 완료 (차단)
                }
                
                // 영상이 로드되지 않은 경우에도 Ctrl 조합은 처리
                bool isVideoLoaded = videoCapture != null && videoCapture.IsOpened();
                
                // 방향키: 영상 로드된 경우만 처리
                if (isVideoLoaded)
                {
                    if (keyData == Keys.Left)
                    {
                        // 5초씩 뒤로 이동
                        int framesToMove = (int)(fps * 5);
                        int newFrame = Math.Max(0, currentFrameIndex - framesToMove);
                        LoadFrame(newFrame);
                        return true; // 이벤트 처리 완료
                    }
                    else if (keyData == Keys.Right)
                    {
                        // 5초씩 앞으로 이동
                        int framesToMove = (int)(fps * 5);
                        int newFrame = Math.Min(totalFrames - 1, currentFrameIndex + framesToMove);
                        LoadFrame(newFrame);
                        return true; // 이벤트 처리 완료
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[방향키 처리 오류] {ex.Message}\n{ex.StackTrace}");
                // 오류 발생 시 기본 동작 수행
            }
            
            // 처리하지 못한 키는 기본 동작 수행
            return base.ProcessCmdKey(ref msg, keyData);
        }
        
        // ✅ YOLO 추적 중인지 확인하는 메서드
        // 단순 탐지(DetectCurrentFrameOnly)는 추적과 별개이므로 추적 중일 때만 true 반환
        private bool IsYoloOperationInProgress()
        {
            try
            {
                // YOLO 추적 중인지 확인 (단순 탐지는 추적과 별개이므로 제외)
                if (isTrackingInProgress)
                    return true;
                
                // ✅ 단순 탐지 작업은 추적과 별개이므로 차단하지 않음
                // 탐지는 추적을 방해하지 않으며, 추적도 탐지를 방해하지 않음
                // if (yoloDetectionTask != null && !yoloDetectionTask.IsCompleted)
                //     return true;
                
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YOLO 작업 확인 오류] {ex.Message}");
                // 오류 발생 시 안전하게 false 반환
                return false;
            }
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // ✅ 입력 컨트롤(TextBox, ComboBox 등)에 포커스가 있으면 단축키 무시
                Control focusedControl = this.ActiveControl;
                if (focusedControl != null)
                {
                    // TextBox나 ComboBox에 포커스가 있으면 단축키 처리하지 않음
                    if (focusedControl is TextBox || focusedControl is ComboBox)
                    {
                        // Enter, Escape는 입력 컨트롤에서 처리하도록 허용
                        if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Escape)
                        {
                            return;
                        }
                    }
                }
                
                // ✅ YOLO 추적/탐지 중에는 모든 키 입력 무시 (작업 보호)
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[키 입력 차단] YOLO 추적/탐지 중이므로 키 입력 무시");
                    e.Handled = true;
                    return;
                }
                
                // ✅ 추적 중에는 모든 키 입력 무시 (추적 작업 보호)
                if (isTrackingInProgress)
                {
                    e.Handled = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[키 입력 처리 오류] {ex.Message}\n{ex.StackTrace}");
                // 오류 발생 시에도 기본 동작 계속
            }

            // F1/F2/F3: Person/Vehicle/Event 라벨 선택 (영상 로드 여부와 무관)
            if (!e.Control && !e.Shift && !e.Alt)
            {
                if (e.KeyCode == Keys.F1)
                {
                    btnLabelPerson_Click(sender, e);
                    e.Handled = true;
                    return;
                }
                else if (e.KeyCode == Keys.F2)
                {
                    btnLabelVehicle_Click(sender, e);
                    e.Handled = true;
                    return;
                }
                else if (e.KeyCode == Keys.F3)
                {
                    btnLabelEvent_Click(sender, e);
                    e.Handled = true;
                    return;
                }
            }
            
            // ✅ 영상 로드 여부와 무관하게 동작하는 ID 설정 단축키들
            // (영상 로드 체크보다 앞에 위치하여 항상 동작)
            
            // Ctrl+1~10: Person ID 수동 지정 (1~10) - Person만
            if (e.Control && !e.Shift && !e.Alt && currentSelectedLabel == "person")
            {
                int? assignedId = null;
                
                if (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1) assignedId = 1;
                else if (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2) assignedId = 2;
                else if (e.KeyCode == Keys.D3 || e.KeyCode == Keys.NumPad3) assignedId = 3;
                else if (e.KeyCode == Keys.D4 || e.KeyCode == Keys.NumPad4) assignedId = 4;
                else if (e.KeyCode == Keys.D5 || e.KeyCode == Keys.NumPad5) assignedId = 5;
                else if (e.KeyCode == Keys.D6 || e.KeyCode == Keys.NumPad6) assignedId = 6;
                else if (e.KeyCode == Keys.D7 || e.KeyCode == Keys.NumPad7) assignedId = 7;
                else if (e.KeyCode == Keys.D8 || e.KeyCode == Keys.NumPad8) assignedId = 8;
                else if (e.KeyCode == Keys.D9 || e.KeyCode == Keys.NumPad9) assignedId = 9;
                else if (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0) assignedId = 10;
                
                if (assignedId.HasValue)
                {
                    // ✅ 선택된 person 박스가 있으면 현재 박스의 ID를 변경
                    if (selectedBox != null && selectedBox.Label == "person")
                    {
                        int oldId = selectedBox.PersonId;
                        int newId = assignedId.Value;
                        
                        // ✅ 해당 박스가 속한 waypoint 찾기
                        var waypoint = FindWaypointForBox(selectedBox);
                        
                        if (waypoint != null && waypoint.Label == "person")
                        {
                            // ✅ waypoint 범위 내의 모든 person 박스의 PersonId 변경
                            var boxesToUpdate = boundingBoxes
                                .Where(b => b.Label == "person" &&
                                           b.PersonId == oldId &&
                                           b.FrameIndex >= waypoint.EntryFrame &&
                                           b.FrameIndex <= waypoint.ExitFrame &&
                                           !b.IsDeleted)
                                .ToList();
                            
                            foreach (var box in boxesToUpdate)
                            {
                                SetBoxId(box, "person", newId);
                                AddUndoAction(new UndoAction
                                {
                                    Type = UndoActionType.ModifyBox,
                                    Box = CloneBoundingBox(box),
                                    OriginalLabel = "person",
                                    OriginalObjectId = oldId
                                });
                            }
                            
                            // ✅ waypoint의 ObjectId도 변경
                            waypoint.ObjectId = newId;
                            
                            // ✅ waypoint 리스트 업데이트
                            UpdateWaypointListView();
                        }
                        else
                        {
                            // waypoint에 속하지 않은 경우 현재 박스만 변경
                            SetBoxId(selectedBox, "person", newId);
                            AddUndoAction(new UndoAction
                            {
                                Type = UndoActionType.ModifyBox,
                                Box = CloneBoundingBox(selectedBox),
                                OriginalLabel = "person",
                                OriginalObjectId = oldId
                            });
                        }
                        
                        UpdateObjectInfo(selectedBox);
                        UpdateBboxListDisplay();
                        pictureBoxVideo.Invalidate();
                    }
                    else
                    {
                        // 선택된 박스가 없으면 기존처럼 다음 ID 값만 설정
                        currentAssignedId = assignedId.Value;
                    }
                    
                    e.Handled = true;
                    return;
                }
            }
            
            // Alt+1~0으로 11~20 지정 (Person만)
            if (!e.Control && !e.Shift && e.Alt && currentSelectedLabel == "person")
            {
                int? assignedId = null;
                
                if (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1) assignedId = 11;
                else if (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2) assignedId = 12;
                else if (e.KeyCode == Keys.D3 || e.KeyCode == Keys.NumPad3) assignedId = 13;
                else if (e.KeyCode == Keys.D4 || e.KeyCode == Keys.NumPad4) assignedId = 14;
                else if (e.KeyCode == Keys.D5 || e.KeyCode == Keys.NumPad5) assignedId = 15;
                else if (e.KeyCode == Keys.D6 || e.KeyCode == Keys.NumPad6) assignedId = 16;
                else if (e.KeyCode == Keys.D7 || e.KeyCode == Keys.NumPad7) assignedId = 17;
                else if (e.KeyCode == Keys.D8 || e.KeyCode == Keys.NumPad8) assignedId = 18;
                else if (e.KeyCode == Keys.D9 || e.KeyCode == Keys.NumPad9) assignedId = 19;
                else if (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0) assignedId = 20;
                
                if (assignedId.HasValue)
                {
                    // ✅ 선택된 person 박스가 있으면 현재 박스의 ID를 변경
                    if (selectedBox != null && selectedBox.Label == "person")
                    {
                        int oldId = selectedBox.PersonId;
                        int newId = assignedId.Value;
                        
                        // ✅ 해당 박스가 속한 waypoint 찾기
                        var waypoint = FindWaypointForBox(selectedBox);
                        
                        if (waypoint != null && waypoint.Label == "person")
                        {
                            // ✅ waypoint 범위 내의 모든 person 박스의 PersonId 변경
                            var boxesToUpdate = boundingBoxes
                                .Where(b => b.Label == "person" &&
                                           b.PersonId == oldId &&
                                           b.FrameIndex >= waypoint.EntryFrame &&
                                           b.FrameIndex <= waypoint.ExitFrame &&
                                           !b.IsDeleted)
                                .ToList();
                            
                            foreach (var box in boxesToUpdate)
                            {
                                SetBoxId(box, "person", newId);
                                AddUndoAction(new UndoAction
                                {
                                    Type = UndoActionType.ModifyBox,
                                    Box = CloneBoundingBox(box),
                                    OriginalLabel = "person",
                                    OriginalObjectId = oldId
                                });
                            }
                            
                            // ✅ waypoint의 ObjectId도 변경
                            waypoint.ObjectId = newId;
                            
                            // ✅ waypoint 리스트 업데이트
                            UpdateWaypointListView();
                        }
                        else
                        {
                            // waypoint에 속하지 않은 경우 현재 박스만 변경
                            SetBoxId(selectedBox, "person", newId);
                            AddUndoAction(new UndoAction
                            {
                                Type = UndoActionType.ModifyBox,
                                Box = CloneBoundingBox(selectedBox),
                                OriginalLabel = "person",
                                OriginalObjectId = oldId
                            });
                        }
                        
                        UpdateObjectInfo(selectedBox);
                        UpdateBboxListDisplay();
                        pictureBoxVideo.Invalidate();
                    }
                    else
                    {
                        // 선택된 박스가 없으면 기존처럼 다음 ID 값만 설정
                        currentAssignedId = assignedId.Value;
                    }
                    
                    e.Handled = true;
                    return;
                }
            }
            
            // Shift+1~0으로 21~30 지정 (Person만)
            if (!e.Control && e.Shift && !e.Alt && currentSelectedLabel == "person")
            {
                int? assignedId = null;
                
                if (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1) assignedId = 21;
                else if (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2) assignedId = 22;
                else if (e.KeyCode == Keys.D3 || e.KeyCode == Keys.NumPad3) assignedId = 23;
                else if (e.KeyCode == Keys.D4 || e.KeyCode == Keys.NumPad4) assignedId = 24;
                else if (e.KeyCode == Keys.D5 || e.KeyCode == Keys.NumPad5) assignedId = 25;
                else if (e.KeyCode == Keys.D6 || e.KeyCode == Keys.NumPad6) assignedId = 26;
                else if (e.KeyCode == Keys.D7 || e.KeyCode == Keys.NumPad7) assignedId = 27;
                else if (e.KeyCode == Keys.D8 || e.KeyCode == Keys.NumPad8) assignedId = 28;
                else if (e.KeyCode == Keys.D9 || e.KeyCode == Keys.NumPad9) assignedId = 29;
                else if (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0) assignedId = 30;
                
                if (assignedId.HasValue)
                {
                    // ✅ 선택된 person 박스가 있으면 현재 박스의 ID를 변경
                    if (selectedBox != null && selectedBox.Label == "person")
                    {
                        int oldId = selectedBox.PersonId;
                        int newId = assignedId.Value;
                        
                        // ✅ 해당 박스가 속한 waypoint 찾기
                        var waypoint = FindWaypointForBox(selectedBox);
                        
                        if (waypoint != null && waypoint.Label == "person")
                        {
                            // ✅ waypoint 범위 내의 모든 person 박스의 PersonId 변경
                            var boxesToUpdate = boundingBoxes
                                .Where(b => b.Label == "person" &&
                                           b.PersonId == oldId &&
                                           b.FrameIndex >= waypoint.EntryFrame &&
                                           b.FrameIndex <= waypoint.ExitFrame &&
                                           !b.IsDeleted)
                                .ToList();
                            
                            foreach (var box in boxesToUpdate)
                            {
                                SetBoxId(box, "person", newId);
                                AddUndoAction(new UndoAction
                                {
                                    Type = UndoActionType.ModifyBox,
                                    Box = CloneBoundingBox(box),
                                    OriginalLabel = "person",
                                    OriginalObjectId = oldId
                                });
                            }
                            
                            // ✅ waypoint의 ObjectId도 변경
                            waypoint.ObjectId = newId;
                            
                            // ✅ waypoint 리스트 업데이트
                            UpdateWaypointListView();
                        }
                        else
                        {
                            // waypoint에 속하지 않은 경우 현재 박스만 변경
                            SetBoxId(selectedBox, "person", newId);
                            AddUndoAction(new UndoAction
                            {
                                Type = UndoActionType.ModifyBox,
                                Box = CloneBoundingBox(selectedBox),
                                OriginalLabel = "person",
                                OriginalObjectId = oldId
                            });
                        }
                        
                        UpdateObjectInfo(selectedBox);
                        UpdateBboxListDisplay();
                        pictureBoxVideo.Invalidate();
                    }
                    else
                    {
                        // 선택된 박스가 없으면 기존처럼 다음 ID 값만 설정
                        currentAssignedId = assignedId.Value;
                    }
                    
                    e.Handled = true;
                    return;
                }
            }
            
            // 영상이 로드되지 않은 경우 키 이벤트 무시
            if (videoCapture == null || !videoCapture.IsOpened())
                return;

            // 방향키는 ProcessCmdKey에서 처리하므로 여기서는 제외
            // Tab/Shift+Tab: 선택 사이클링
            if (e.KeyCode == Keys.Tab)
            {
                CycleSelection(e.Shift);
                e.Handled = true;
                return;
            }
            // Space bar - 재생/일시정지
            if (e.KeyCode == Keys.Space)
            {
                btnPlay_Click(sender, e);
                e.Handled = true;
            }
            // C 키 - 자막 토글
            else if (e.KeyCode == Keys.C && !e.Control && !e.Shift && !e.Alt)
            {
                btnToggleSubtitle_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Shift && e.KeyCode == Keys.OemPeriod) // Shift + > (> 키)
            {
                // 속도 빠르게 (1.0x → 16.0x)
                if (playbackSpeed < 1.0) playbackSpeed = 1.0;
                else if (playbackSpeed < 4.0) playbackSpeed = 4.0;
                else if (playbackSpeed < 8.0) playbackSpeed = 8.0;
                else if (playbackSpeed < 16.0) playbackSpeed = 16.0;
                
                if (isPlaying)
                    lastFrameTime = DateTime.Now.Ticks / 10000;
                UpdateTimeLabels();
                e.Handled = true;
            }
            else if (e.Shift && e.KeyCode == Keys.Oemcomma) // Shift + < (< 키)
            {
                // 속도 느리게 (16.0x → 1.0x)
                if (playbackSpeed > 8.0) playbackSpeed = 8.0;
                else if (playbackSpeed > 4.0) playbackSpeed = 4.0;
                else if (playbackSpeed > 1.0) playbackSpeed = 1.0;
                else playbackSpeed = 0.5;
                
                if (isPlaying)
                    lastFrameTime = DateTime.Now.Ticks / 10000;
                UpdateTimeLabels();
                e.Handled = true;
            }
            else if (e.Shift && e.KeyCode == Keys.OemQuestion) // Shift + / 키
            {
                // 속도 초기화
                playbackSpeed = 1.0;
                if (isPlaying)
                    lastFrameTime = DateTime.Now.Ticks / 10000;
                UpdateTimeLabels();
                MessageBox.Show("재생 속도를 1.0x로 초기화했습니다.", "속도 초기화", MessageBoxButtons.OK, MessageBoxIcon.Information);
                e.Handled = true;
            }
            else if (selectedBox != null && !e.Control && (e.KeyCode == Keys.W || e.KeyCode == Keys.A || e.KeyCode == Keys.S || e.KeyCode == Keys.D))
            {
                int moveAmount = e.Shift ? 10 : 2;
                Rectangle rect = selectedBox.Rectangle;

                switch (e.KeyCode)
                {
                    case Keys.W: rect.Y -= moveAmount; break;
                    case Keys.A: rect.X -= moveAmount; break;
                    case Keys.S: rect.Y += moveAmount; break;
                    case Keys.D: rect.X += moveAmount; break;
                }

                selectedBox.Rectangle = rect;
                pictureBoxVideo.Invalidate();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.G && selectedBox != null)
            {
                AddUndoAction(new UndoAction { Type = UndoActionType.RemoveBox, Box = CloneBoundingBox(selectedBox) });
                
                // ✅ 삭제 플래그 설정 (실제 제거 안 함, 흔적 유지)
                selectedBox.IsDeleted = true;
                
                // ✅ 사라짐 의도 기록
                RecordDisappearanceIntent(selectedBox);
                
                selectedBox = null;
                UpdateBoxCount();
                UpdateBboxListDisplay();
                pictureBoxVideo.Invalidate();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                // ✅ 박스가 선택되어 있으면 박스 삭제 우선
                if (selectedBox != null)
                {
                    AddUndoAction(new UndoAction { Type = UndoActionType.RemoveBox, Box = CloneBoundingBox(selectedBox) });
                    
                    // ✅ 삭제 플래그 설정 (실제 제거 안 함, 흔적 유지)
                    selectedBox.IsDeleted = true;
                
                    // ✅ 사라짐 의도 기록
                    RecordDisappearanceIntent(selectedBox);
                    
                    selectedBox = null;
                    UpdateBoxCount();
                    UpdateBboxListDisplay();
                    pictureBoxVideo.Invalidate();
                    e.Handled = true;
                    return;
                }
                
                // 박스가 선택되어 있지 않으면 waypoint 삭제
                if (listViewPersonWaypoints.SelectedItems.Count > 0 || 
                    listViewVehicleWaypoints.SelectedItems.Count > 0 || 
                    listViewEventWaypoints.SelectedItems.Count > 0)
                {
                    btnDeleteSelectedWaypoint_Click(sender, e);
                    e.Handled = true;
                    return;
                }
                
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Z)
            {
                if (e.Shift)
                {
                    // Ctrl+Shift+Z: Redo
                    Redo();
                    e.Handled = true;
                }
                else
                {
                    // Ctrl+Z: Undo
                    Undo();
                    e.Handled = true;
                }
            }
            else if (e.Control && e.KeyCode == Keys.Y)
            {
                // Ctrl+Y: Redo
                Redo();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.E && !e.Control && !e.Alt)
            {
                SetEntryMarker();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.X && !e.Control && !e.Alt)
            {
                _ = SetExitMarkerAndCreateWaypoint();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Q && !e.Control && !e.Alt)
            {
                // Q키: Event 종료 (현재 프레임부터 Exit까지 삭제)
                TerminateEventFromCurrentFrame();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.R && !e.Shift && !e.Control && !e.Alt)
            {
                // ✅ R: 강제 관성 추적을 위한 a프레임 설정
                if (selectedBox != null)
                {
                    var waypoint = FindWaypointForBox(selectedBox);
                    if (waypoint != null)
                    {
                        int boxId = GetBoxId(selectedBox);
                        string key = $"{selectedBox.Label}_{boxId}";
                        
                        // a프레임 저장 (현재 프레임)
                        forcedInertiaTrackingStartFrames[key] = currentFrameIndex;
                        
                        MessageBox.Show(
                            $"a프레임이 설정되었습니다.\n\n" +
                            $"객체: {GetCategoryName(selectedBox.Label, boxId)}\n" +
                            $"a프레임: {currentFrameIndex}\n\n" +
                            $"이제 b프레임으로 이동한 후\n" +
                            $"Shift+T를 눌러 강제 관성 추적을 실행하세요.\n" +
                            $"(Shift+T를 누른 시점이 b프레임이 됩니다)",
                            "a프레임 설정 완료",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        e.Handled = true;
                    }
                    else
                    {
                        MessageBox.Show("현재 박스에 해당하는 Waypoint를 찾을 수 없습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        e.Handled = true;
                    }
                }
                else
                {
                    MessageBox.Show("a프레임을 설정할 박스를 먼저 선택해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    e.Handled = true;
                }
            }
            else if (e.Shift && e.KeyCode == Keys.T && !e.Control && !e.Alt)
            {
                // ✅ Shift+T: 강제 관성 추적 (현재 프레임을 b프레임으로 사용)
                if (selectedBox != null)
                {
                    var waypoint = FindWaypointForBox(selectedBox);
                    if (waypoint != null)
                    {
                        int boxId = GetBoxId(selectedBox);
                        string key = $"{selectedBox.Label}_{boxId}";
                        
                        // a 프레임: R키로 설정한 a프레임 우선 사용, 없으면 현재 프레임
                        int aFrame;
                        if (forcedInertiaTrackingStartFrames.ContainsKey(key))
                        {
                            aFrame = forcedInertiaTrackingStartFrames[key];
                        }
                        else
                        {
                            // 설정된 a프레임이 없으면 현재 프레임 사용
                            aFrame = currentFrameIndex;
                        }
                        
                        // ✅ b 프레임: Shift+T를 누른 시점(현재 프레임)
                        int bFrame = currentFrameIndex;
                        
                        // b 프레임이 waypoint 범위를 넘지 않도록 제한
                        if (bFrame > waypoint.ExitFrame)
                        {
                            bFrame = waypoint.ExitFrame;
                        }
                        
                        // ✅ 프레임 범위 유효성 검증
                        // aFrame이 waypoint 범위를 벗어나는 경우
                        if (aFrame < waypoint.EntryFrame || aFrame > waypoint.ExitFrame)
                        {
                            MessageBox.Show(
                                $"a프레임({aFrame})이 waypoint 범위({waypoint.EntryFrame}~{waypoint.ExitFrame})를 벗어났습니다.\n\n" +
                                $"a프레임은 waypoint Entry~Exit 범위 내에 있어야 합니다.",
                                "오류",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                            e.Handled = true;
                            return;
                        }
                        
                        // bFrame이 waypoint 범위를 벗어나는 경우
                        if (bFrame < waypoint.EntryFrame || bFrame > waypoint.ExitFrame)
                        {
                            MessageBox.Show(
                                $"b프레임({bFrame})이 waypoint 범위({waypoint.EntryFrame}~{waypoint.ExitFrame})를 벗어났습니다.\n\n" +
                                $"b프레임은 waypoint Entry~Exit 범위 내에 있어야 합니다.",
                                "오류",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                            e.Handled = true;
                            return;
                        }
                        
                        // aFrame >= bFrame인 경우 (보간 불가)
                        if (aFrame >= bFrame)
                        {
                            MessageBox.Show(
                                $"프레임 범위가 유효하지 않습니다.\n\n" +
                                $"a프레임: {aFrame}\n" +
                                $"b프레임: {bFrame}\n" +
                                $"waypoint 범위: {waypoint.EntryFrame}~{waypoint.ExitFrame}\n\n" +
                                $"a프레임은 b프레임보다 작아야 합니다.\n" +
                                $"현재 b프레임이 a프레임과 같거나 작습니다.\n\n" +
                                $"해결 방법:\n" +
                                $"R키로 a프레임을 더 작은 값으로 설정하세요.",
                                "오류",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                            e.Handled = true;
                            return;
                        }
                        
                        // a프레임이 현재 선택된 박스의 프레임과 다르면 해당 프레임의 박스로 전환
                        BoundingBox boxForTracking = selectedBox;
                        if (aFrame != currentFrameIndex)
                        {
                            boxForTracking = boundingBoxes.FirstOrDefault(b =>
                                b.FrameIndex == aFrame &&
                                b.Label == selectedBox.Label &&
                                GetBoxId(b) == boxId &&
                                !b.IsDeleted);
                            
                            if (boxForTracking == null)
                            {
                                MessageBox.Show(
                                    $"a프레임({aFrame})에서 해당 박스를 찾을 수 없습니다.",
                                    "오류",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                                e.Handled = true;
                                return;
                            }
                        }
                        
                        // 강제 관성 추적 실행
                        PerformForcedInertiaTracking(boxForTracking, aFrame, bFrame);
                        e.Handled = true;
                    }
                    else
                    {
                        MessageBox.Show("현재 박스에 해당하는 Waypoint를 찾을 수 없습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        e.Handled = true;
                    }
                }
                else
                {
                    MessageBox.Show("추적할 박스를 먼저 선택해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    e.Handled = true;
                }
            }
            else if (e.Control && e.KeyCode == Keys.T)
            {
                // ✅ Ctrl+T: 부분 재추적 (기존 로직)
                if (selectedBox != null)
                {
                    // selectedBox의 waypoint 찾기
                    var waypoint = waypointMarkers.FirstOrDefault(w => 
                        w.Label == selectedBox.Label &&
                        w.ObjectId == GetBoxId(selectedBox) &&
                        currentFrameIndex >= w.EntryFrame && 
                        currentFrameIndex <= w.ExitFrame);
                    
                    if (waypoint != null)
                    {
                        // 부분 재추적: 현재 프레임부터 ExitFrame까지
                        _ = PerformPartialRetrackingAsync(waypoint, currentFrameIndex);
                    }
                    else
                    {
                        MessageBox.Show("현재 박스에 해당하는 Waypoint를 찾을 수 없습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else if (selectedWaypoint != null)
                {
                    // 기존 로직: Waypoint 전체 재추적
                    _ = PerformTrackingForWaypointAsync(selectedWaypoint, useYolo: true);
                }
                else
                {
                    MessageBox.Show("추적할 박스나 웨이포인트를 먼저 선택해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else if (e.KeyCode == Keys.D1 && !e.Control && !e.Alt)
            {
                btnSelectAll_Click(sender, e);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.D2 && !e.Control && !e.Alt)
            {
                btnEdit_Click(sender, e);
                e.Handled = true;
            }
            else if (selectedBox != null && selectedBox.Label == "person" && e.Control && !e.Shift && !e.Alt)
            {
                // Ctrl+1~9: Person ID 1~9 지정
                if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9)
            {
                int id = e.KeyCode - Keys.D0;
                AssignPersonId(id);
                e.Handled = true;
            }
                else if (e.KeyCode >= Keys.NumPad1 && e.KeyCode <= Keys.NumPad9)
            {
                int id = e.KeyCode - Keys.NumPad0;
                AssignPersonId(id);
                e.Handled = true;
            }
                // Ctrl+0: Person ID 10 지정
                else if (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0)
                {
                    AssignPersonId(10);
                    e.Handled = true;
                }
            }
            else if (selectedBox != null && selectedBox.Label == "person" && !e.Control && !e.Shift && e.Alt)
            {
                // Alt+1~0: Person ID 11~20 지정
                if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9)
            {
                int id = (e.KeyCode - Keys.D0) + 10;
                AssignPersonId(id);
                e.Handled = true;
            }
                else if (e.KeyCode >= Keys.NumPad1 && e.KeyCode <= Keys.NumPad9)
            {
                int id = (e.KeyCode - Keys.NumPad0) + 10;
                AssignPersonId(id);
                e.Handled = true;
                }
                else if (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0)
                {
                    AssignPersonId(20);
                    e.Handled = true;
                }
            }
            else if (e.Control && e.KeyCode == Keys.S)
            {
                // Ctrl+S: JSON 저장 및 추출
                btnExportJson_Click(sender, e);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                // ✅ Entry 설정 해제
                if (entryFrameIndex.HasValue)
                {
                    entryFrameIndex = null;
                    btnEntry.Text = "Entry";
                    panelTimeline.Invalidate();
                    System.Diagnostics.Debug.WriteLine("[Entry 해제] ESC 키로 Entry 설정이 해제되었습니다.");
                }
                
                selectedBox = null;
                ClearSidebarHighlights(); // ✅ 하이라이트 초기화
                pictureBoxVideo.Invalidate();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Y && !e.Control && !e.Shift && !e.Alt)
            {
                // ✅ Y 키: YOLO 탐지 토글
                if (btnToggleYoloDetections != null)
                {
                    btnToggleYoloDetections_Click(sender, e);
                    e.Handled = true;
                }
            }
            else if (e.KeyCode == Keys.Oemcomma) // ',' 키
            {
                try
                {
                    // ✅ YOLO 추적/탐지 중에는 한 프레임 이동 차단 (중요!)
                    if (IsYoloOperationInProgress())
                    {
                        System.Diagnostics.Debug.WriteLine("[한 프레임 이동 차단] YOLO 추적/탐지 중이므로 이전 프레임 이동 불가");
                        MessageBox.Show(
                            "YOLO 추적 또는 탐지가 진행 중입니다.\n작업이 완료될 때까지 기다려주세요.",
                            "작업 중",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        e.Handled = true;
                        return;
                    }
                    
                    // ✅ 이전 프레임으로 이동
                    if (currentFrameIndex > 0)
                    {
                        LoadFrame(currentFrameIndex - 1);
                        e.Handled = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[이전 프레임 이동 오류] {ex.Message}\n{ex.StackTrace}");
                    MessageBox.Show(
                        $"프레임 이동 중 오류 발생:\n{ex.Message}",
                        "오류",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    e.Handled = true;
                }
            }
            else if (e.KeyCode == Keys.OemPeriod) // '.' 키
            {
                try
                {
                    // ✅ YOLO 추적/탐지 중에는 한 프레임 이동 차단 (중요!)
                    if (IsYoloOperationInProgress())
                    {
                        System.Diagnostics.Debug.WriteLine("[한 프레임 이동 차단] YOLO 추적/탐지 중이므로 다음 프레임 이동 불가");
                        MessageBox.Show(
                            "YOLO 추적 또는 탐지가 진행 중입니다.\n작업이 완료될 때까지 기다려주세요.",
                            "작업 중",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        e.Handled = true;
                        return;
                    }
                    
                    // ✅ 다음 프레임으로 이동
                    if (currentFrameIndex < totalFrames - 1)
                    {
                        LoadFrame(currentFrameIndex + 1);
                        e.Handled = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[다음 프레임 이동 오류] {ex.Message}\n{ex.StackTrace}");
                    MessageBox.Show(
                        $"프레임 이동 중 오류 발생:\n{ex.Message}",
                        "오류",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    e.Handled = true;
                }
            }
        }

        private void AssignPersonId(int id)
        {
            if (selectedBox == null)
                return;

            int oldPersonId = GetBoxId(selectedBox);
            string oldLabel = selectedBox.Label;
            Rectangle oldRect = selectedBox.Rectangle;

            SetBoxId(selectedBox, selectedBox.Label, id);

            AddUndoAction(new UndoAction
            {
                Type = UndoActionType.ModifyBox,
                Box = CloneBoundingBox(selectedBox),
                OriginalObjectId = oldPersonId,
                OriginalLabel = oldLabel,
                OriginalRectangle = oldRect
            });

            // ✅ UI 업데이트: 선택된 박스의 정보도 갱신
            UpdateObjectInfo(selectedBox);
            UpdateBboxListDisplay();
            labelObjectLabel.Text = $"Label: {selectedBox.Label}_{id:D2}";
            pictureBoxVideo.Invalidate();

            currentMode = DrawMode.Draw;
            btnEdit.BackColor = Color.FromArgb(59, 130, 246);
            btnSelectAll.BackColor = SystemColors.Control;
            pictureBoxVideo.Cursor = Cursors.Cross;

            // ❌ selectedBox를 null로 초기화하지 않음 (선택 상태 유지)
        }
        #endregion

        #region SRT Subtitle Extraction
        private async Task<bool> ExtractSrtFromVideo(string videoPath)
        {
            // FFmpeg가 없으면 자막 추출 건너뛰기
            if (!isFFmpegAvailable)
            {
                return false;
            }

            try
            {
                string videoDir = Path.GetDirectoryName(videoPath);
                string videoName = Path.GetFileNameWithoutExtension(videoPath);
                string srtPath = Path.Combine(videoDir, $"{videoName}.srt");

                // 기존 SRT 파일이 있으면 삭제
                if (File.Exists(srtPath))
                {
                    File.Delete(srtPath);
                }

                // FFmpeg를 사용하여 자막 추출 (비디오 내부에 포함된 자막 스트림)
                var ffTask = FFMpegArguments
                    .FromFileInput(videoPath)
                    .OutputToFile(srtPath, true, options => options
                        .WithCustomArgument("-loglevel error")  // 경고 메시지 숨기기
                        .WithCustomArgument("-map 0:s:0")  // 첫 번째 자막 스트림 선택
                        .WithCustomArgument("-c:s srt")    // SRT 형식으로 출력
                    )
                    .ProcessAsynchronously();

                var result = await ffTask;

                if (result && File.Exists(srtPath))
                {
                    currentSrtFile = srtPath;
                    await LoadSrtFile(srtPath);
                    return true;
                }
                else
                {
                    // 자막 스트림이 없는 경우 (정상 상황)
                    return false;
                }
            }
            catch (Exception ex)
            {
                // 자막이 없거나 추출 실패 시 조용히 실패
                return false;
            }
        }

        private async Task LoadSrtFile(string srtPath)
        {
            try
            {
                subtitleEntries.Clear();
                string[] lines = await File.ReadAllLinesAsync(srtPath);

                for (int i = 0; i < lines.Length; i++)
                {
                    // 빈 줄 건너뛰기
                    if (string.IsNullOrWhiteSpace(lines[i]))
                        continue;

                    // 인덱스 번호 확인
                    if (int.TryParse(lines[i], out int index))
                    {
                        i++; // 다음 줄로 이동

                        // 시간 정보 파싱
                        if (i < lines.Length && lines[i].Contains("-->"))
                        {
                            string[] timeParts = lines[i].Split(new[] { " --> " }, StringSplitOptions.None);
                            if (timeParts.Length == 2)
                            {
                                TimeSpan startTime = ParseSrtTime(timeParts[0]);
                                TimeSpan endTime = ParseSrtTime(timeParts[1]);

                                i++; // 다음 줄로 이동

                                // 자막 텍스트 수집
                                List<string> textLines = new List<string>();
                                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
                                {
                                    textLines.Add(lines[i]);
                                    i++;
                                }

                                if (textLines.Count > 0)
                                {
                                    subtitleEntries.Add(new SubtitleEntry
                                    {
                                        Index = index,
                                        StartTime = startTime,
                                        EndTime = endTime,
                                        Text = string.Join(" ", textLines)
                                    });
                                }
                            }
                        }
                    }
                }

                MessageBox.Show($"자막 파일을 성공적으로 로드했습니다.\n총 {subtitleEntries.Count}개의 자막 항목을 찾았습니다.", 
                    "자막 로드 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"자막 파일 로드 중 오류가 발생했습니다:\n{ex.Message}", 
                    "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private TimeSpan ParseSrtTime(string timeString)
        {
            // SRT 시간 형식: 00:00:00,000
            string[] parts = timeString.Split(',');
            if (parts.Length == 2)
            {
                string[] timeParts = parts[0].Split(':');
                if (timeParts.Length == 3)
                {
                    int hours = int.Parse(timeParts[0]);
                    int minutes = int.Parse(timeParts[1]);
                    int seconds = int.Parse(timeParts[2]);
                    int milliseconds = int.Parse(parts[1]);

                    return new TimeSpan(0, hours, minutes, seconds, milliseconds);
                }
            }
            return TimeSpan.Zero;
        }

        private string GetCurrentSubtitle()
        {
            if (subtitleEntries.Count == 0)
                return "";

            double currentSeconds = currentFrameIndex / fps;
            TimeSpan currentTime = TimeSpan.FromSeconds(currentSeconds);

            var currentSubtitle = subtitleEntries.FirstOrDefault(s => 
                currentTime >= s.StartTime && currentTime <= s.EndTime);

            return currentSubtitle?.Text ?? "";
        }

        // 자막에서 날짜-시간 형식(YYYY-MM-DD HH:mm:ss) 추출
        private string ExtractTimestampFromSubtitle(string subtitleText)
        {
            if (string.IsNullOrEmpty(subtitleText))
                return null;

            // 정규식 패턴: YYYY-MM-DD HH:mm:ss
            var regex = new System.Text.RegularExpressions.Regex(@"\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}");
            var match = regex.Match(subtitleText);

            if (match.Success)
            {
                // ISO 8601 형식으로 변환 (YYYY-MM-DDTHH:mm:ss)
                string timestamp = match.Value.Replace(" ", "T");
                return timestamp;
            }

            return null;
        }

        // 특정 프레임의 자막 타임스탬프 가져오기
        private string GetSubtitleTimestampForFrame(int frameIndex)
        {
            if (subtitleEntries.Count == 0)
                return null;

            double frameSeconds = frameIndex / fps;
            TimeSpan frameTime = TimeSpan.FromSeconds(frameSeconds);

            var subtitle = subtitleEntries.FirstOrDefault(s => 
                frameTime >= s.StartTime && frameTime <= s.EndTime);

            if (subtitle != null)
            {
                return ExtractTimestampFromSubtitle(subtitle.Text);
            }

            return null;
        }
        #endregion

        #region Coordinate Transformation
        // PictureBox의 Zoom 모드에서 실제 이미지가 표시되는 영역 계산
        private RectangleF GetImageDisplayRectangle()
        {
            if (pictureBoxVideo.Image == null)
                return RectangleF.Empty;

            float imageAspect = (float)pictureBoxVideo.Image.Width / pictureBoxVideo.Image.Height;
            float controlAspect = (float)pictureBoxVideo.Width / pictureBoxVideo.Height;

            float renderWidth, renderHeight;
            float renderX = 0, renderY = 0;

            if (imageAspect > controlAspect)
            {
                // 이미지가 더 넓음 - 좌우에 맞춤
                renderWidth = pictureBoxVideo.Width;
                renderHeight = pictureBoxVideo.Width / imageAspect;
                renderY = (pictureBoxVideo.Height - renderHeight) / 2f;
            }
            else
            {
                // 이미지가 더 높음 - 상하에 맞춤
                renderHeight = pictureBoxVideo.Height;
                renderWidth = pictureBoxVideo.Height * imageAspect;
                renderX = (pictureBoxVideo.Width - renderWidth) / 2f;
            }

            return new RectangleF(renderX, renderY, renderWidth, renderHeight);
        }

        // 이미지 좌표 → 뷰(PictureBox) 좌표 변환
        private PointF ImageToView(PointF imagePoint)
        {
            if (pictureBoxVideo.Image == null)
                return imagePoint;

            var displayRect = GetImageDisplayRectangle();
            
            float scaleX = displayRect.Width / pictureBoxVideo.Image.Width;
            float scaleY = displayRect.Height / pictureBoxVideo.Image.Height;

            return new PointF(
                displayRect.X + imagePoint.X * scaleX,
                displayRect.Y + imagePoint.Y * scaleY
            );
        }

        private RectangleF ImageToView(RectangleF imageRect)
        {
            var topLeft = ImageToView(new PointF(imageRect.X, imageRect.Y));
            var bottomRight = ImageToView(new PointF(imageRect.Right, imageRect.Bottom));
            
            return new RectangleF(
                topLeft.X,
                topLeft.Y,
                bottomRight.X - topLeft.X,
                bottomRight.Y - topLeft.Y
            );
        }

        // 뷰(PictureBox) 좌표 → 이미지 좌표 변환
        private PointF ViewToImage(PointF viewPoint)
        {
            if (pictureBoxVideo.Image == null)
                return viewPoint;

            var displayRect = GetImageDisplayRectangle();
            
            float scaleX = pictureBoxVideo.Image.Width / displayRect.Width;
            float scaleY = pictureBoxVideo.Image.Height / displayRect.Height;

            return new PointF(
                (viewPoint.X - displayRect.X) * scaleX,
                (viewPoint.Y - displayRect.Y) * scaleY
            );
        }

        private RectangleF ViewToImage(RectangleF viewRect)
        {
            var topLeft = ViewToImage(new PointF(viewRect.X, viewRect.Y));
            var bottomRight = ViewToImage(new PointF(viewRect.Right, viewRect.Bottom));
            
            return new RectangleF(
                topLeft.X,
                topLeft.Y,
                bottomRight.X - topLeft.X,
                bottomRight.Y - topLeft.Y
            );
        }
        #endregion

        #region Helper Methods
        private BoundingBox CloneBoundingBox(BoundingBox box)
        {
            return new BoundingBox
            {
                FrameIndex = box.FrameIndex,
                Rectangle = new Rectangle(box.Rectangle.Location, box.Rectangle.Size),
                Label = box.Label,
                PersonId = box.PersonId,
                VehicleId = box.VehicleId,
                EventId = box.EventId,
                Action = box.Action,
                VehicleName = box.VehicleName,
                EventName = box.EventName
            };
        }
        #endregion

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            // 프로그램 종료 시 자동 저장 제거 - 수동으로만 저장

            if (videoCapture != null)
            {
                videoCapture.Release();
                videoCapture.Dispose();
            }

            if (currentFrame != null)
                currentFrame.Dispose();

            // YOLO 엔진 정리
            if (trackingEngine is YoloTrackingEngine yoloEngine)
                yoloEngine.Dispose();

            if (videoListForm != null && !videoListForm.IsDisposed)
                videoListForm.Dispose();
        }

        #region 창 이동 및 크기 조절

        /// <summary>
        /// 헤더 드래그로 창 이동 기능 설정
        /// </summary>
        private void SetupWindowDragHandlers()
        {
            // panelHeader를 드래그하면 창이 이동
            panelHeader.MouseDown += PanelHeader_MouseDown;
            panelHeader.MouseMove += PanelHeader_MouseMove;
            panelHeader.MouseUp += PanelHeader_MouseUp;
            panelHeader.DoubleClick += PanelHeader_DoubleClick;

            // labelTitle도 드래그 가능하게
            labelTitle.MouseDown += PanelHeader_MouseDown;
            labelTitle.MouseMove += PanelHeader_MouseMove;
            labelTitle.MouseUp += PanelHeader_MouseUp;
            labelTitle.DoubleClick += PanelHeader_DoubleClick;
        }

        private void PanelHeader_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 최대화 상태가 아닐 때만 이동 가능
                if (this.WindowState != FormWindowState.Maximized)
                {
                    isMovingWindow = true;
                    windowMoveStartPoint = e.Location;
                }
            }
        }

        private void PanelHeader_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMovingWindow)
            {
                // 마우스 이동량 계산
                System.Drawing.Point currentScreenPoint = Control.MousePosition;
                System.Drawing.Point offset = new System.Drawing.Point(
                    currentScreenPoint.X - windowMoveStartPoint.X,
                    currentScreenPoint.Y - windowMoveStartPoint.Y
                );

                this.Location = offset;
            }
        }

        private void PanelHeader_MouseUp(object sender, MouseEventArgs e)
        {
            isMovingWindow = false;
        }

        /// <summary>
        /// 헤더 더블클릭으로 최대화/복원 토글
        /// </summary>
        private void PanelHeader_DoubleClick(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            else
            {
                this.WindowState = FormWindowState.Maximized;
            }
            // Resize 이벤트가 자동으로 UpdateMaximizeButtonIcon을 호출함
        }

        /// <summary>
        /// Windows 메시지를 가로채서 창 테두리 크기 조절 기능 추가
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTCLIENT = 1;
            const int HTLEFT = 10;
            const int HTRIGHT = 11;
            const int HTTOP = 12;
            const int HTTOPLEFT = 13;
            const int HTTOPRIGHT = 14;
            const int HTBOTTOM = 15;
            const int HTBOTTOMLEFT = 16;
            const int HTBOTTOMRIGHT = 17;

            if (m.Msg == WM_NCHITTEST && this.WindowState != FormWindowState.Maximized)
            {
                base.WndProc(ref m);

                // 마우스 위치 가져오기
                System.Drawing.Point pos = this.PointToClient(new System.Drawing.Point(m.LParam.ToInt32()));

                int width = this.ClientSize.Width;
                int height = this.ClientSize.Height;

                // 모서리 및 테두리 영역 감지
                if (pos.X <= RESIZE_BORDER_WIDTH && pos.Y <= RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTTOPLEFT;
                }
                else if (pos.X >= width - RESIZE_BORDER_WIDTH && pos.Y <= RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTTOPRIGHT;
                }
                else if (pos.X <= RESIZE_BORDER_WIDTH && pos.Y >= height - RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTBOTTOMLEFT;
                }
                else if (pos.X >= width - RESIZE_BORDER_WIDTH && pos.Y >= height - RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTBOTTOMRIGHT;
                }
                else if (pos.X <= RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTLEFT;
                }
                else if (pos.X >= width - RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTRIGHT;
                }
                else if (pos.Y <= RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTTOP;
                }
                else if (pos.Y >= height - RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTBOTTOM;
                }
            }
            else
            {
                base.WndProc(ref m);
            }
        }

        #endregion
    }

    #region Legacy JSON Classes (호환성 유지)

    public class LabelingData
    {
        [JsonProperty("info")] public VideoInfo Info { get; set; }
        [JsonProperty("categories")] public List<Category> Categories { get; set; }
        [JsonProperty("items")] public List<Item> Items { get; set; }
    }

    public class VideoInfo
    {
        [JsonProperty("video_file")] public string VideoFile { get; set; }
        [JsonProperty("date_created")] public string DateCreated { get; set; }
        [JsonProperty("cctv_id")] public string CctvId { get; set; }
    }

    public class Category
    {
        [JsonProperty("labels")] public List<LabelDef> Labels { get; set; }
    }

    public class LabelDef
    {
        [JsonProperty("name")] public string Name { get; set; }
    }

    public class Item
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("annotations")] public List<Annotation> Annotations { get; set; }
    }

    public class Annotation
    {
        [JsonProperty("image_id")] public int ImageId { get; set; }
        [JsonProperty("attributes")] public Dictionary<string, object> Attributes { get; set; }
        [JsonProperty("label_id")] public int LabelId { get; set; }
        [JsonProperty("attr")] public Dictionary<string, object> Attr { get; set; }
    }

    #endregion
}