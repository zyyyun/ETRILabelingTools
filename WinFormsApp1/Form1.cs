using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
    public enum UndoActionType { AddBox, RemoveBox, ModifyBox, Tracking, EventIdChange, EventRectanglePropagation }

    public class UndoAction
    {
        public UndoActionType Type { get; set; }
        public BoundingBox Box { get; set; }
        public Rectangle OriginalRectangle { get; set; }
        public string OriginalLabel { get; set; }
        public int OriginalObjectId { get; set; }
        public List<BoundingBox> TrackedBoxes { get; set; }
        public List<EventIdChange> EventIdChanges { get; set; } = new List<EventIdChange>();
        public EventWaypointMarkerChange? EventWaypointMarkerChange { get; set; }
        public EventRectanglePropagationBatch EventRectanglePropagation { get; set; }
        public bool IsTombstone { get; set; }
    }


    // ? BBox 크기 조정 핸들 (4개 모서리만)
    public enum ResizeHandle
    {
        None,
        TopLeft,      // 좌상단 모서리
        TopRight,     // 우상단 모서리
        BottomLeft,   // 좌하단 모서리
        BottomRight   // 우하단 모서리
    }




    // Person 속성 관리 클래스
    public class PersonAttributeEntry
    {
        public string AttributeName { get; set; }
        public object Value { get; set; }
        public int WaypointEntryFrame { get; set; }
        public int ApplyFromFrame { get; set; }  // 이 속성이 적용되는 시작 프레임
        public int PersonId { get; set; }
    }

    public class PersonAttributeStore
    {
        // Global 속성: (videoFile, personId) -> attributes (영상별로 구분)
        private Dictionary<(string videoFile, int personId), Dictionary<string, object>> globalAttributes = new Dictionary<(string, int), Dictionary<string, object>>();
        
        // Global 속성 우선순위: person_id -> (attributeName -> priority) (호환성을 위해 유지, 사용 안함)
        private Dictionary<int, Dictionary<string, int>> globalAttributePriority = new Dictionary<int, Dictionary<string, int>>();
        
        // Waypoint-scoped 속성: person_id -> entries (EntryFrame 순으로 정렬)
        // waypointMarkers를 통해 현재 영상의 waypoint만 필터링되므로 영상별로 분리됨
        private Dictionary<int, List<PersonAttributeEntry>> waypointScopedAttributes = new Dictionary<int, List<PersonAttributeEntry>>();

        // 단일 선택 속성 목록 (ComboBox 사용)
        // 다중 선택 속성: 악세서리(HeadwearType, FacewearType, BagType, CarringItemType), 상의 색상(UpperClothColor), 하의 색상(LowerClothColor)
        // 나머지는 모두 단일 선택 속성
        public static readonly HashSet<string> singleSelectAttributeNames = new HashSet<string>
        {
            // 보임/가림/행동 탭
            "Occlusion", "BodyView", "ActionType", "Camouflage",
            // 생체 정보 탭
            "Age", "Gender", "Height", "Weight/BodyShape", "Face",
            // 머리/헤어 탭
            "HairLength", "HairStyle", "HairColor",
            // 상의 탭
            "UpperClothType", "UpperClothSleeve", "UpperClothPattern",
            // 하의 탭
            "LowerClothType", "LowerClothLegwear", "LowerClothLength", "LowerClothPattern", "LowerClothMaterial",
            // 신발 탭
            "FootwearType", "FootwearColor"
        };

        // 노란색 표시 속성 목록 (Waypoint-scoped)
        private static readonly HashSet<string> waypointScopedAttributeNames = new HashSet<string>
        {
            "Occlusion",      // View 탭의 노란색 표시 속성
            "BodyView",       // View 탭의 노란색 표시 속성
            "ActionType"      // Action 탭의 노란색 표시 속성
        };

        public static bool IsWaypointScoped(string attributeName)
        {
            return waypointScopedAttributeNames.Contains(attributeName);
        }

        // Global 속성 우선순위 계산
        private int CalculateGlobalPriority(int personId, int waypointEntryFrame, int applyFromFrame, List<WaypointMarker> waypointMarkers)
        {
            // 첫 번째 waypoint인지 확인
            var allWaypoints = waypointMarkers
                .Where(w => w.Label == "person" && w.ObjectId == personId)
                .OrderBy(w => w.EntryFrame)
                .ToList();
            
            if (!allWaypoints.Any())
                return 10; // waypoint가 없으면 낮은 우선순위
            
            bool isFirstWaypoint = allWaypoints[0].EntryFrame == waypointEntryFrame;
            
            if (isFirstWaypoint && applyFromFrame == waypointEntryFrame)
            {
                return 100; // 첫 waypoint의 EntryFrame: 최고 우선순위
            }
            else if (applyFromFrame == waypointEntryFrame)
            {
                return 50; // 다른 waypoint의 EntryFrame: 중간 우선순위
            }
            else
            {
                return 10; // 중간 프레임: 낮은 우선순위
            }
        }

        // 속성 읽기: 현재 프레임에 적용되는 속성 값 반환
        public object GetAttribute(int personId, int frameIndex, string attributeName, List<WaypointMarker> waypointMarkers, string videoFile = null)
        {
            // ? Weight나 BodyPosture를 Weight/BodyShape로 변환 (기존 데이터 호환성)
            string searchAttributeName = attributeName;
            if (attributeName == "Weight" || attributeName == "BodyPosture")
            {
                searchAttributeName = "Weight/BodyShape";
            }
            
            // 단일 선택 속성인지 확인
            bool isSingleSelect = singleSelectAttributeNames.Contains(attributeName);
            
            object rawValue = null;
            
            // ? applyFromFrame을 고려하여 현재 프레임에 적용되는 속성 찾기
            // 현재 프레임이 속한 waypoint 찾기
            var currentWaypoint = waypointMarkers
                .Where(w => w.Label == "person" && 
                           w.ObjectId == personId && 
                           frameIndex >= w.EntryFrame && 
                           frameIndex <= w.ExitFrame)
                .OrderByDescending(w => w.EntryFrame)
                .FirstOrDefault();
            
            if (waypointScopedAttributes.ContainsKey(personId) && currentWaypoint != null)
            {
                // 현재 waypoint에서 applyFromFrame <= frameIndex인 속성 중 가장 최근 것 찾기
                var entry = waypointScopedAttributes[personId]
                    .Where(e => e.AttributeName == searchAttributeName && 
                               e.WaypointEntryFrame == currentWaypoint.EntryFrame &&
                               e.ApplyFromFrame <= frameIndex)
                    .OrderByDescending(e => e.ApplyFromFrame)
                    .FirstOrDefault();
                
                if (entry != null)
                {
                    rawValue = entry.Value;
                }
            }
                    
            // fallback: 현재 waypoint에 해당 속성이 전혀 없는 경우에만 globalAttributes에서 확인
            // (현재 영상의 속성 우선, 없으면 다른 영상의 같은 person_id 속성 조회)
            if (rawValue == null && !string.IsNullOrEmpty(videoFile))
            {
                // 현재 waypoint에 해당 속성이 있는지 확인 (현재 waypoint에만 한정)
                bool hasWaypointScopedValue = currentWaypoint != null && 
                    waypointScopedAttributes.ContainsKey(personId) &&
                    waypointScopedAttributes[personId].Any(e => 
                        e.WaypointEntryFrame == currentWaypoint.EntryFrame &&
                        (e.AttributeName == searchAttributeName || 
                         (searchAttributeName == "Weight/BodyShape" && (e.AttributeName == "Weight" || e.AttributeName == "BodyPosture"))));
                
                // 현재 waypoint에 해당 속성이 전혀 없는 경우에만 global 속성 사용
                if (!hasWaypointScopedValue)
                {
                    var currentKey = (videoFile, personId);
                    // 1. 현재 영상의 속성 확인
                    if (globalAttributes.ContainsKey(currentKey))
                    {
                        // Weight/BodyShape 우선 확인
                        if (globalAttributes[currentKey].ContainsKey(searchAttributeName))
                        {
                            rawValue = globalAttributes[currentKey][searchAttributeName];
                        }
                        // 기존 Weight나 BodyPosture도 확인 (호환성)
                        else if (globalAttributes[currentKey].ContainsKey(attributeName))
            {
                            rawValue = globalAttributes[currentKey][attributeName];
                        }
                    }
                    
                    // 2. 현재 영상에 속성이 없으면 다른 영상의 같은 person_id 속성 조회
                    if (rawValue == null)
                    {
                        foreach (var kvp in globalAttributes)
                        {
                            // 같은 person_id이지만 다른 영상의 속성
                            if (kvp.Key.personId == personId && kvp.Key.videoFile != videoFile)
                            {
                                // Weight/BodyShape 우선 확인
                                if (kvp.Value.ContainsKey(searchAttributeName))
                                {
                                    rawValue = kvp.Value[searchAttributeName];
                                    break;
                                }
                                // 기존 Weight나 BodyPosture도 확인 (호환성)
                                else if (kvp.Value.ContainsKey(attributeName))
                                {
                                    rawValue = kvp.Value[attributeName];
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            
            if (rawValue == null)
            {
            return null;
        }

            // 단일 선택 속성은 단일 값 반환 (string 또는 null)
            if (isSingleSelect)
            {
                if (rawValue is List<string> listValue && listValue.Count > 0)
                {
                    // 배열인 경우 첫 번째 값만 반환 (기존 데이터 호환성)
                    return listValue[0];
                }
                else if (rawValue is string[] arrayValue && arrayValue.Length > 0)
                {
                    return arrayValue[0];
                }
                else if (rawValue is string stringValue)
                {
                    return stringValue;
                }
                else
                {
                    return rawValue.ToString();
                }
            }
            
            // 다중 선택 속성은 배열로 반환 (기존 단일 값도 배열로 변환)
            if (rawValue is List<string> listValue2)
            {
                return listValue2;
            }
            else if (rawValue is string[] arrayValue2)
            {
                return arrayValue2.ToList();
            }
            else if (rawValue is string stringValue2)
            {
                // 단일 값인 경우 배열로 변환 (기존 데이터 호환성)
                return new List<string> { stringValue2 };
            }
            else
            {
                // 기타 타입도 문자열로 변환하여 배열로 반환
                return new List<string> { rawValue.ToString() };
            }
        }

        // 속성 저장 (applyFromFrame + 우선순위 방식)
        public void SetAttribute(int personId, int waypointEntryFrame, int applyFromFrame, string attributeName, object value, List<WaypointMarker> waypointMarkers, string videoFile = null)
            {
            // ? Weight나 BodyPosture를 Weight/BodyShape로 변환
            string saveAttributeName = attributeName;
            if (attributeName == "Weight" || attributeName == "BodyPosture")
            {
                saveAttributeName = "Weight/BodyShape";
                }
                
            // ? applyFromFrame 정보를 유지하기 위해 waypointScopedAttributes에 저장 (모든 속성)
            if (value == null)
            {
                // null 값인 경우: 해당 속성 제거
                if (waypointScopedAttributes.ContainsKey(personId))
                {
                    waypointScopedAttributes[personId].RemoveAll(e => 
                        e.AttributeName == saveAttributeName || 
                        (saveAttributeName == "Weight/BodyShape" && (e.AttributeName == "Weight" || e.AttributeName == "BodyPosture")));
                }
                if (!string.IsNullOrEmpty(videoFile))
                {
                    var key = (videoFile, personId);
                    if (globalAttributes.ContainsKey(key))
                    {
                        globalAttributes[key][saveAttributeName] = null;
                        if (saveAttributeName == "Weight/BodyShape")
                        {
                            globalAttributes[key]["Weight"] = null;
                            globalAttributes[key]["BodyPosture"] = null;
                        }
                    }
                }
                if (globalAttributePriority.ContainsKey(personId))
                {
                    globalAttributePriority[personId].Remove(saveAttributeName);
                    if (saveAttributeName == "Weight/BodyShape")
                    {
                        globalAttributePriority[personId].Remove("Weight");
                        globalAttributePriority[personId].Remove("BodyPosture");
                    }
                }
            }
            else
            {
                // null이 아닌 값인 경우: waypointScopedAttributes에 저장 (applyFromFrame 정보 유지)
                    if (!waypointScopedAttributes.ContainsKey(personId))
                    {
                        waypointScopedAttributes[personId] = new List<PersonAttributeEntry>();
                    }
                    
                // 같은 waypoint, 같은 속성에서 applyFromFrame >= newApplyFromFrame인 모든 기존 항목 제거
                // (새로운 applyFromFrame 이후의 모든 프레임에 새 값이 적용되도록)
                    waypointScopedAttributes[personId].RemoveAll(e => 
                    (e.AttributeName == saveAttributeName || 
                     (saveAttributeName == "Weight/BodyShape" && (e.AttributeName == "Weight" || e.AttributeName == "BodyPosture"))) && 
                        e.WaypointEntryFrame == waypointEntryFrame &&
                    e.ApplyFromFrame >= applyFromFrame);
                    
                    // 새 항목 추가
                    waypointScopedAttributes[personId].Add(new PersonAttributeEntry
                    {
                    AttributeName = saveAttributeName,
                        Value = value,
                        WaypointEntryFrame = waypointEntryFrame,
                        ApplyFromFrame = applyFromFrame,
                        PersonId = personId
                    });
                    
                    // ApplyFromFrame 순으로 정렬
                    waypointScopedAttributes[personId].Sort((a, b) => 
                    {
                        int entryCompare = a.WaypointEntryFrame.CompareTo(b.WaypointEntryFrame);
                        if (entryCompare != 0) return entryCompare;
                        return a.ApplyFromFrame.CompareTo(b.ApplyFromFrame);
                    });
                
                // Global 속성에도 저장 (최신 값 유지용, 현재 영상의 속성만)
                if (!string.IsNullOrEmpty(videoFile))
                {
                    var key = (videoFile, personId);
                    if (!globalAttributes.ContainsKey(key))
                        globalAttributes[key] = new Dictionary<string, object>();
                    globalAttributes[key][saveAttributeName] = value;
                }
            }
        }

        // 일괄 속성 설정 (정렬 최소화 - JSON 로드 시 사용)
        public void SetAttributesBatch(int personId, List<(int waypointEntryFrame, int applyFromFrame, string attributeName, object value)> attributes, List<WaypointMarker> waypointMarkers, string videoFile = null)
        {
            if (attributes == null || attributes.Count == 0)
                return;
            
            bool needsSort = false;
            
            foreach (var (waypointEntryFrame, applyFromFrame, attributeName, value) in attributes)
            {
                // ? applyFromFrame 정보를 유지하기 위해 waypointScopedAttributes에 저장
                if (value == null)
                    {
                    // null 처리
                    if (waypointScopedAttributes.ContainsKey(personId))
                    {
                        waypointScopedAttributes[personId].RemoveAll(e => e.AttributeName == attributeName);
                    }
                    if (!string.IsNullOrEmpty(videoFile))
                    {
                        var key = (videoFile, personId);
                        if (globalAttributes.ContainsKey(key))
                        {
                            globalAttributes[key][attributeName] = null;
                        }
                    }
                    }
                    else
                    {
                    // null이 아닌 값인 경우: waypointScopedAttributes에 저장
                        if (!waypointScopedAttributes.ContainsKey(personId))
                        {
                            waypointScopedAttributes[personId] = new List<PersonAttributeEntry>();
                        }
                        
                    // 같은 waypoint, 같은 속성에서 applyFromFrame >= newApplyFromFrame인 모든 기존 항목 제거
                        waypointScopedAttributes[personId].RemoveAll(e => 
                            e.AttributeName == attributeName && 
                            e.WaypointEntryFrame == waypointEntryFrame &&
                        e.ApplyFromFrame >= applyFromFrame);
                        
                        waypointScopedAttributes[personId].Add(new PersonAttributeEntry
                        {
                            AttributeName = attributeName,
                            Value = value,
                            WaypointEntryFrame = waypointEntryFrame,
                            ApplyFromFrame = applyFromFrame,
                            PersonId = personId
                        });
                        
                    needsSort = true;
                    
                    // Global 속성에도 저장 (최신 값 유지용, 현재 영상의 속성만)
                    if (!string.IsNullOrEmpty(videoFile))
                    {
                        var key = (videoFile, personId);
                        if (!globalAttributes.ContainsKey(key))
                            globalAttributes[key] = new Dictionary<string, object>();
                        globalAttributes[key][attributeName] = value;
                    }
                }
            }
            
            // 정렬은 마지막에 한 번만 수행
            if (needsSort && waypointScopedAttributes.ContainsKey(personId))
            {
                        waypointScopedAttributes[personId].Sort((a, b) => 
                        {
                            int entryCompare = a.WaypointEntryFrame.CompareTo(b.WaypointEntryFrame);
                            if (entryCompare != 0) return entryCompare;
                            return a.ApplyFromFrame.CompareTo(b.ApplyFromFrame);
                        });
            }
        }

        // 모든 속성 가져오기 (현재 프레임 기준)
        public Dictionary<string, object> GetAllAttributes(int personId, int frameIndex, List<WaypointMarker> waypointMarkers, string videoFile = null)
        {
            var result = new Dictionary<string, object>();
            
            // ? applyFromFrame을 고려하여 현재 프레임에 적용되는 속성 찾기
            // 현재 프레임이 속한 waypoint 찾기
            var currentWaypoint = waypointMarkers
                .Where(w => w.Label == "person" && 
                           w.ObjectId == personId && 
                           frameIndex >= w.EntryFrame && 
                           frameIndex <= w.ExitFrame)
                .OrderByDescending(w => w.EntryFrame)
                .FirstOrDefault();
            
            if (currentWaypoint != null && waypointScopedAttributes.ContainsKey(personId))
            {
                // 현재 waypoint에서 applyFromFrame <= frameIndex인 속성들 찾기
                var entries = waypointScopedAttributes[personId]
                    .Where(e => e.WaypointEntryFrame == currentWaypoint.EntryFrame &&
                               e.ApplyFromFrame <= frameIndex);
                
                // 각 속성별로 가장 최근 ApplyFromFrame 값만 사용
                foreach (var entry in entries.GroupBy(e => e.AttributeName))
                {
                    var latestEntry = entry.OrderByDescending(e => e.ApplyFromFrame).First();
                    if (latestEntry.Value != null)
                    {
                        string attrName = latestEntry.AttributeName;
                        // ? Weight나 BodyPosture를 Weight/BodyShape로 변환
                        if (attrName == "Weight" || attrName == "BodyPosture")
                        {
                            attrName = "Weight/BodyShape";
                        }
                        result[attrName] = latestEntry.Value;
                    }
                }
            }
            
            // fallback: 현재 waypoint에 해당 속성이 전혀 없는 경우에만 globalAttributes에서 추가
            // 현재 영상의 속성 우선, 없으면 다른 영상의 같은 person_id 속성 조회
            if (!string.IsNullOrEmpty(videoFile))
            {
                var currentKey = (videoFile, personId);
                var fallbackAttributes = new Dictionary<string, object>();
                
                // 현재 waypoint에 있는 속성 이름 수집 (현재 waypoint에만 한정)
                var waypointScopedAttributeNames = new HashSet<string>();
                if (currentWaypoint != null && waypointScopedAttributes.ContainsKey(personId))
                {
                    foreach (var entry in waypointScopedAttributes[personId]
                        .Where(e => e.WaypointEntryFrame == currentWaypoint.EntryFrame))
                    {
                        string attrName = entry.AttributeName;
                        // Weight나 BodyPosture를 Weight/BodyShape로 변환
                        if (attrName == "Weight" || attrName == "BodyPosture")
                        {
                            attrName = "Weight/BodyShape";
                        }
                        waypointScopedAttributeNames.Add(attrName);
                    }
                }
                
                // 1. 현재 영상의 속성 확인 (waypointScopedAttributes에 없는 속성만)
                if (globalAttributes.ContainsKey(currentKey))
                {
                    foreach (var kvp in globalAttributes[currentKey])
                    {
                        if (kvp.Value != null)
                        {
                            string attrName = kvp.Key;
                            // ? Weight나 BodyPosture를 Weight/BodyShape로 변환
                            if (attrName == "Weight" || attrName == "BodyPosture")
                            {
                                attrName = "Weight/BodyShape";
                            }
                            // result에 없고, waypointScopedAttributes에도 없는 속성만 추가
                            if (!result.ContainsKey(attrName) && !waypointScopedAttributeNames.Contains(attrName))
                            {
                                result[attrName] = kvp.Value;
                            }
                        }
                    }
                }
                
                // 2. 현재 영상에 없는 속성은 다른 영상의 같은 person_id 속성에서 조회
                // (waypointScopedAttributes에 없는 속성만)
                foreach (var kvp in globalAttributes)
                {
                    // 같은 person_id이지만 다른 영상의 속성
                    if (kvp.Key.personId == personId && kvp.Key.videoFile != videoFile)
                    {
                        foreach (var attrKvp in kvp.Value)
                        {
                            if (attrKvp.Value != null)
                            {
                                string attrName = attrKvp.Key;
                                // ? Weight나 BodyPosture를 Weight/BodyShape로 변환
                                if (attrName == "Weight" || attrName == "BodyPosture")
                                {
                                    attrName = "Weight/BodyShape";
                                }
                                // result에 없고, waypointScopedAttributes에도 없고, fallbackAttributes에도 없으면 추가
                                if (!result.ContainsKey(attrName) && 
                                    !waypointScopedAttributeNames.Contains(attrName) && 
                                    !fallbackAttributes.ContainsKey(attrName))
                                {
                                    fallbackAttributes[attrName] = attrKvp.Value;
                                }
                            }
                        }
                    }
                }
                
                // fallback 속성을 result에 추가
                foreach (var kvp in fallbackAttributes)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            
            return result;
        }

        // person_id의 모든 속성 초기화
        public void ClearPersonAttributes(int personId, string videoFile = null)
        {
            if (!string.IsNullOrEmpty(videoFile))
            {
                var key = (videoFile, personId);
                if (globalAttributes.ContainsKey(key))
            {
                    globalAttributes[key].Clear();
                }
            }
            if (waypointScopedAttributes.ContainsKey(personId))
            {
                waypointScopedAttributes[personId].Clear();
            }
        }
    }

    // 드래그 가능한 Person 속성 창
    public class PersonAttributeWindow : Form
    {
        private int personId;
        private int frameIndex;
        private System.Drawing.Point dragOffset;
        private bool isDragging = false;
        private Label lblContent;
        private Button btnClose;

        public int PersonId => personId;
        public int FrameIndex => frameIndex;

        public PersonAttributeWindow(int personId, int frameIndex, Dictionary<string, object> attributes, System.Drawing.Point initialLocation)
        {
            this.personId = personId;
            this.frameIndex = frameIndex;

            // Form 설정
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.Size = new System.Drawing.Size(250, 200);
            this.StartPosition = FormStartPosition.Manual;
            this.Location = initialLocation;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.Opacity = 0.95;

            // 패널 (테두리 효과)
            Panel borderPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(2),
                BackColor = Color.FromArgb(100, 149, 237) // 파란색 테두리
            };
            this.Controls.Add(borderPanel);

            // 내부 패널
            Panel innerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            borderPanel.Controls.Add(innerPanel);

            // 제목 바
            Panel titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30,
                BackColor = Color.FromArgb(50, 50, 50),
                Cursor = Cursors.SizeAll
            };
            innerPanel.Controls.Add(titleBar);

            // 제목 레이블
            Label lblTitle = new Label
            {
                Text = $"Person {personId:D2}",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new System.Drawing.Point(10, 5),
                AutoSize = true
            };
            titleBar.Controls.Add(lblTitle);

            // 닫기 버튼
            btnClose = new Button
            {
                Text = "?",
                Size = new System.Drawing.Size(25, 25),
                Location = new System.Drawing.Point(220, 2),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();
            titleBar.Controls.Add(btnClose);

            // 드래그 이벤트
            titleBar.MouseDown += TitleBar_MouseDown;
            titleBar.MouseMove += TitleBar_MouseMove;
            titleBar.MouseUp += TitleBar_MouseUp;

            // 내용 레이블
            lblContent = new Label
            {
                Location = new System.Drawing.Point(10, 40),
                Size = new System.Drawing.Size(230, 150),
                ForeColor = Color.White,
                Font = new Font("Consolas", 9F),
                AutoSize = false
            };
            innerPanel.Controls.Add(lblContent);

            // 속성 텍스트 설정
            UpdateAttributes(attributes);
        }

        public void UpdateAttributes(Dictionary<string, object> attributes)
        {
            if (attributes == null || attributes.Count == 0)
            {
                lblContent.Text = "(속성 없음)";
                return;
            }

            var lines = new List<string>();
            foreach (var kvp in attributes)
            {
                string value = FormatAttributeValue(kvp.Value);
                lines.Add($"{kvp.Key}: {value}");
            }

            lblContent.Text = string.Join("\r\n", lines);
        }
        
        private string FormatAttributeValue(object value)
        {
            if (value == null)
                return "-";
            
            // 배열/리스트인 경우 처리
            if (value is List<string> listValue)
            {
                if (listValue.Count == 0)
                    return "-";
                // 한국어로 변환하여 표시
                var koreanValues = listValue.Select(v => PersonAttributesForm.GetAttributeValueKorean(v)).ToList();
                return string.Join(", ", koreanValues);
            }
            else if (value is string[] arrayValue)
            {
                if (arrayValue.Length == 0)
                    return "-";
                var koreanValues = arrayValue.Select(v => PersonAttributesForm.GetAttributeValueKorean(v)).ToList();
                return string.Join(", ", koreanValues);
            }
            else if (value is string stringValue)
            {
                // 단일 값인 경우 한국어로 변환
                return PersonAttributesForm.GetAttributeValueKorean(stringValue);
            }
            else
            {
                // 기타 타입은 문자열로 변환 후 한국어 변환 시도
                string strValue = value.ToString();
                return PersonAttributesForm.GetAttributeValueKorean(strValue);
            }
        }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                dragOffset = new System.Drawing.Point(e.X, e.Y);
            }
        }

        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                System.Drawing.Point currentScreenPos = PointToScreen(e.Location);
                this.Location = new System.Drawing.Point(
                    currentScreenPos.X - dragOffset.X,
                    currentScreenPos.Y - dragOffset.Y);
            }
        }

        private void TitleBar_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
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

        // ? BoundingBox에서 실제 COCO 카테고리 이름을 추출하는 메서드
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
            YoloTempFileHelper.CleanupStaleFiles();
            _tempImagePath = YoloTempFileHelper.CreateFramePath("yolo_tracking_frame");
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

        // ? 실패 구간을 반환하는 오버로드 메서드
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
            int fixedVehicleInstanceId = startBox.VehicleInstanceId;
            int? fixedLinkedVehicleInstanceId = startBox.LinkedVehicleInstanceId;
            string fixedVehiclePartType = startBox.VehiclePartType;
            int? fixedLinkedPersonId = startBox.LinkedPersonId;
            int fixedIdEvent = startBox.EventId;
            string fixedEventInstanceId = startBox.EventInstanceId;

            // ? 추적 대상의 기본 카테고리를 미리 추출합니다. 
            // vehicle의 경우 실제 종류(car, motorcycle 등)를 반환하여 COCO 데이터셋과 매칭
            string targetCategory = GetBaseCategoryFromBox(startBox);
            
            // ? vehicle_car인 경우 모든 4륜 자동차 종류(bus, truck 등)를 포함하도록 설정
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

            // ? 추적 실패 분석을 위한 통계 변수
            int totalFrames = endFrame - startFrame + 1;
            int successCount = 0;
            int failureCount = 0;
            int failureStartFrame = -1;
            int consecutiveFailures = 0;
            int maxConsecutiveFailures = 0;
            string lastFailureReason = "";
            
            // ? 30프레임 연속 실패 구간 추적
            const int FAILURE_THRESHOLD = 30;
            var localFailureRanges = new List<(int start, int end)>();
            int thresholdFailureStart = -1;
            
            // ? 성공 프레임 추적 (보간용)
            var successfulFrames = new Dictionary<int, Rectangle>(); // FrameIndex -> Rectangle
            
            // ? 고유 객체 추적: 카테고리별로 추적 중인 객체들의 마지막 위치를 저장
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
                    
                    // ? YOLO 탐지 결과 로그 출력


                    // ? 이전 박스와 IoU가 가장 큰 검출만 채택 (Label 필터링 적용)
                    double bestIou = 0.0;
                    Detection bestDetection = null;
                    const double MIN_IOU_THRESHOLD = 0.3; // IoU 최소 임계값
                    
                    int candidateCount = 0; // 같은 카테고리의 후보 수
                    double maxIouOfCategory = 0.0; // 같은 카테고리 중 최대 IoU
                    
                    foreach (var d in detections)
                    {
                        // ? 1. Label 필터링: 같은 기본 카테고리를 가진 객체만 후보로 고려합니다.
                        // YOLO 라이브러리가 "0: 'person'" 형식의 이름을 반환하므로, 순수한 이름만 추출합니다.
                        string rawDetectionName = d.Name.ToString();
                        string detectionName = rawDetectionName;
                        int firstQuote = rawDetectionName.IndexOf('\'');
                        int lastQuote = rawDetectionName.LastIndexOf('\'');
                        if (firstQuote != -1 && lastQuote > firstQuote)
                        {
                            detectionName = rawDetectionName.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                        }

                        // ? 고유 객체 카운트: 같은 객체가 여러 프레임에 걸쳐 추적되는 경우 1번만 카운트
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

                        // ? vehicle_car인 경우 car, bus, truck 모두 매칭
                        if (targetCategories.Contains(detectionName))
                        {
                            candidateCount++;
                            var detectionRect = new Rectangle((int)d.Bounds.X, (int)d.Bounds.Y, (int)d.Bounds.Width, (int)d.Bounds.Height);
                            double iou = ComputeIoU(previousRect, detectionRect);
                            
                            if (iou > maxIouOfCategory)
                                maxIouOfCategory = iou;

                            // ? 2. IoU 비교: 가장 많이 겹치는 객체를 찾습니다.
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
                        
                        // ? 성공 프레임 기록 (보간용)
                        successfulFrames[i] = previousRect;
                        
                        // ? 연속 실패가 끝났는지 체크
                        if (consecutiveFailures > 0)
                        {
                        Debug.WriteLine($"[BOX POSITION] Frame {i}: Previous({previousRect.X},{previousRect.Y}) -> New({bestDetection.Bounds.X},{bestDetection.Bounds.Y})");
                        
                        // ? 30프레임 이상 실패했다면 실패 구간 기록
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
                            VehicleInstanceId = fixedVehicleInstanceId,
                            LinkedVehicleInstanceId = fixedLinkedVehicleInstanceId,
                            VehiclePartType = fixedVehiclePartType,
                            LinkedPersonId = fixedLinkedPersonId,
                            EventId = fixedIdEvent,
                            EventInstanceId = fixedEventInstanceId,
                            Action = startBox.Action,
                            VehicleName = startBox.VehicleName,
                            EventName = startBox.EventName
                        });
                    }
                    else
                    {
                        // ? Detection 실패 분석
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
                        
                        // ? 30프레임 연속 실패 시작 체크
                        if (consecutiveFailures == FAILURE_THRESHOLD)
                        {
                            thresholdFailureStart = i - (FAILURE_THRESHOLD - 1);
                        }
                        
                        // ? Detection 실패 시 이전 위치 유지 (추적 완료 후 보간 처리)
                        trackedBoxes.Add(new BoundingBox
                    {
                        FrameIndex = i,
                            Rectangle = previousRect, // 이전 프레임의 박스 좌표 그대로 사용
                        Label = fixedLabel,
                        PersonId = fixedIdPerson,
                        VehicleId = fixedIdVehicle,
                        VehicleInstanceId = fixedVehicleInstanceId,
                        LinkedVehicleInstanceId = fixedLinkedVehicleInstanceId,
                        VehiclePartType = fixedVehiclePartType,
                        LinkedPersonId = fixedLinkedPersonId,
                        EventId = fixedIdEvent,
                            EventInstanceId = fixedEventInstanceId,
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

            // ? 추적 종료 요약 로그 (카테고리별 탐지 개수)
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

            YoloTempFileHelper.TryDelete(_tempImagePath);

            // ? 추적 종료 시 미종료된 실패 구간 처리
            if (consecutiveFailures >= FAILURE_THRESHOLD && thresholdFailureStart != -1)
            {
                localFailureRanges.Add((thresholdFailureStart, endFrame));
            }

            // ? YOLO 추적 완료 후 성공 프레임 간 보간으로 실패 프레임 채우기
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

            // ? 최종 추적 통계 출력
            double successRate = totalFrames > 0 ? (double)successCount / totalFrames * 100 : 0;
            Debug.WriteLine($"[추적 완료] 성공율: {successRate:F1}%, 성공: {successCount}, 실패: {failureCount}, 관성 보간: {inertiaAppliedCount}개");

            // ? 실패 구간을 out 파라미터에 할당
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
        private string currentJsonFile = "";  // 현재 로드된 JSON 파일 경로

        private enum DrawMode { None, Select, Draw }
        private DrawMode currentMode = DrawMode.Select;

        // ? 추적 상태 플래그 (추적 중에는 다른 키 입력 차단)
        private bool isTrackingInProgress = false;

        private List<BoundingBox> boundingBoxes = new List<BoundingBox>();
        private BoundingBox selectedBox = null;
        private BoundingBox drawingBox = null;
        private System.Drawing.Point drawStartPoint;
        private bool isDrawing = false;
        private bool isDragging = false;
        private System.Drawing.Point dragOffset;
        private Rectangle originalDragRect;
        private bool isWaitingForDoubleClick = false; // 더블 클릭 대기 플래그
        private System.Threading.Timer doubleClickTimer = null; // 더블 클릭 타이머
        private System.Drawing.Point lastClickPoint; // 마지막 클릭 위치

        // ? BBox 크기 조정 관련 변수
        private bool isResizing = false;
        private ResizeHandle currentResizeHandle = ResizeHandle.None;
        private System.Drawing.Point resizeStartPoint;
        private Rectangle originalResizeRect;
        private const int MIN_BBOX_SIZE = 10; // 최소 bbox 크기
        private const int HANDLE_SIZE = 8; // 핸들 크기

        private int? entryFrameIndex = null;
        private int? exitFrameIndex = null;

        private List<WaypointMarker> waypointMarkers = new List<WaypointMarker>();
        private WaypointMarker selectedWaypoint = null; // ? 선택된 Waypoint 추적
        
        // ? 실패 구간 저장 (Key: "Label_ObjectId", Value: List<(startFrame, endFrame)>)
        private Dictionary<string, List<(int start, int end)>> waypointFailureRanges = new Dictionary<string, List<(int, int)>>();
        
        // ? Person 속성 저장소
        private PersonAttributeStore personAttributeStore = new PersonAttributeStore();

        // ? 속성값 조회 토글 관련
        private bool isAttributeViewEnabled = false;
        private Dictionary<(int personId, int frameIndex), PersonAttributeWindow> attributeWindows = new Dictionary<(int, int), PersonAttributeWindow>();

        // 리스트뷰 MouseDown에서 이미 이동 처리한 경우 Click 핸들러 1회 무시
        private bool suppressWaypointClickOnce = false;
        
        // ? 관성 추적 활성화 상태 저장 (Key: "Label_ObjectId", Value: true/false)
        private Dictionary<string, bool> inertiaTrackingEnabled = new Dictionary<string, bool>();
        
        // ? 수동으로 수정된 프레임 추적 (Key: "Label_ObjectId", Value: List<수정된 프레임>)
        private Dictionary<string, List<int>> manuallyAdjustedFrames = new Dictionary<string, List<int>>();
        
        // ? Shift+E로 설정한 a프레임 저장 (Key: "Label_ObjectId", Value: a프레임)
        private Dictionary<string, int> forcedInertiaTrackingStartFrames = new Dictionary<string, int>();
        
        // ? 객체 사라짐 구간 추적 (Key: "Label_ObjectId", Value: List<(시작 프레임, 종료 프레임)>)
        // 사라짐 의도가 기록된 구간 (아직 종료 프레임이 확정되지 않음)
        private Dictionary<string, List<(int startFrame, int? endFrame)>> disappearedRanges = new Dictionary<string, List<(int, int?)>>();
        
        // ? 연속 박스 부재 감지용 임계값 (프레임 단위)
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

        // ? 창 이동 및 크기 조절 관련
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
        private string yoloModelPath = GetYoloModelPath();
        
        private static string GetYoloModelPath()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "yolov8n.onnx"),
                Path.Combine(Application.StartupPath, "yolov8n.onnx"),
                Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\..\..\yolov8n.onnx"))
            };

            return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
        }
        // SRT 자막 관련
        private string currentSrtFile = "";
        private List<SubtitleEntry> subtitleEntries = new List<SubtitleEntry>();
        private bool isFFmpegAvailable = false;
        private bool isSubtitleVisible = false; // 자막 표시 상태
        
        // ? YOLO 탐지 박스 표시 관련
        private bool showYoloDetections = false; // YOLO 탐지 박스 표시 여부
        private bool showSkeleton = false; // Skeleton 표시 여부
        private bool invertSkeletonY = true; // Skeleton Y축 반전 옵션 (필요 시 true)
        private Dictionary<int, List<YoloDetectionBox>> yoloDetectionCache = new Dictionary<int, List<YoloDetectionBox>>();
        private CancellationTokenSource yoloDetectionCancellationToken = null;
        private Task yoloDetectionTask = null;
        private readonly object yoloDetectionCacheLock = new object();
        private readonly object yoloDetectionTaskLock = new object(); // ? 탐지 작업 동기화용 락 추가
        private const int YOLO_DETECTION_RANGE = 100; // 현재 프레임 기준 앞뒤 탐지 범위
        private System.Threading.Timer detectionDebounceTimer = null; // 프레임 이동 디바운스 타이머
        private const int DETECTION_DEBOUNCE_MS = 300; // 프레임 이동 후 탐지 대기 시간 (ms) - 0.3초
        private int pendingDetectionFrame = -1; // ? 대기 중인 탐지 프레임 추적
        private SemaphoreSlim detectionSemaphore = new SemaphoreSlim(1, 1); // ? 동시 탐지 작업 제한 (최대 1개)
        private volatile bool isFormDisposed = false; // ? Form이 Dispose되었는지 확인
        
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
            
            // Event categories (25~32)
            {"contact", 25}, {"throw", 26}, {"final_exchange", 27}, {"get on", 28},
            {"get off", 29}, {"suspect", 30}, {"controlled_delivery", 31}, {"camouflage", 32}, {"cardboard box", 33},

            // Vehicle plate category (33)
            {"plate", 34}
        };


        public Form1()
        {
            InitializeComponent();
            ConfigureEventTypeSelector();
            UpdateBoxCount();
            
            // 자막 초기 상태를 닫힌 상태로 설정
            btnToggleSubtitle.Text = "자막 열기";
            btnToggleSubtitle.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
            if (labelSubtitleTimestamp != null)
            {
                labelSubtitleTimestamp.Visible = false;
            }

            // ? Timeline 패널에 더블 버퍼링 활성화 (깜빡임 방지)
            EnableDoubleBuffering(panelTimeline);

            // ? 헤더 드래그로 창 이동 기능 활성화
            SetupWindowDragHandlers();
        }

        private void ConfigureEventTypeSelector()
        {
            comboBoxEvent.Items.Clear();
            comboBoxEvent.Items.AddRange(LabelCatalogHelper.GetEventComboItems().Cast<object>().ToArray());
            comboBoxEvent.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxEvent.Font = new System.Drawing.Font("Segoe UI", 8F);
            comboBoxEvent.Location = new System.Drawing.Point(8, 70);
            comboBoxEvent.Size = new System.Drawing.Size(270, 25);
            comboBoxEvent.SelectedIndex = 0;
            comboBoxEvent.SelectedIndexChanged += (sender, args) =>
            {
                int eventId = LabelCatalogHelper.GetEventIdFromComboItem(comboBoxEvent.SelectedItem?.ToString());
                if (eventId > 0)
                {
                    currentAssignedId = eventId;
                }
            };
            comboBoxEvent.SelectionChangeCommitted += (sender, args) =>
            {
                currentSelectedLabel = "event";
                currentAssignedId = GetSelectedEventTypeId();
                btnLabelPerson.BackColor = System.Drawing.Color.FromArgb(252, 231, 243);
                btnLabelPerson.FlatAppearance.BorderSize = 2;
                btnLabelVehicle.BackColor = System.Drawing.Color.FromArgb(219, 234, 254);
                btnLabelVehicle.FlatAppearance.BorderSize = 2;
                btnLabelEvent.BackColor = System.Drawing.Color.FromArgb(34, 197, 94);
                btnLabelEvent.FlatAppearance.BorderSize = 3;
            };

            groupBoxLabels.Controls.Add(comboBoxEvent);
            UpdateLabelsLayoutAfterToggle();
        }

        private int GetSelectedEventTypeId()
        {
            int eventId = LabelCatalogHelper.GetEventIdFromComboItem(comboBoxEvent.SelectedItem?.ToString());
            return eventId > 0 ? eventId : 1;
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

            // ? ListView에서 스페이스바가 Form1_KeyDown으로 전달되도록 KeyDown 설정
            listViewPersonWaypoints.KeyDown += (s, ev) => HandleListViewKeyDown(s, ev);
            listViewVehicleWaypoints.KeyDown += (s, ev) => HandleListViewKeyDown(s, ev);
            listViewEventWaypoints.KeyDown += (s, ev) => HandleListViewKeyDown(s, ev);

            // YOLO 모델 초기화 시도
            InitializeYoloModel();

            // FFmpeg 경로 설정
            SetupFFmpegPath();

            // ? 창 상태 변경 시 최대화/복원 버튼 아이콘 업데이트
            this.Resize += Form1_Resize;
            UpdateMaximizeButtonIcon();
        }

        // ? ListView에서 키 이벤트를 Form1로 전달하는 핸들러
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
                string fullErrorDetails = $"에러 메시지: {ex.Message}\n\n스택 트레이스:\n{ex.StackTrace}";
                if (ex.InnerException != null)
                {
                    fullErrorDetails += $"\n\n내부 예외:\n{ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
                }
                System.Diagnostics.Debug.WriteLine($"[YOLO 초기화 실패] {fullErrorDetails}");

                var missingDlls = CudaEnvironmentHelper.GetMissingCudaDependencies();
                string reason;

                if (missingDlls.Count > 0)
                {
                    reason = $"CUDA 런타임 DLL이 누락되었습니다:\n - {string.Join("\n - ", missingDlls)}\n\n" +
                             "CUDA Toolkit 12.x 및 cuDNN 9.x가 설치되어 있는지 확인하세요.";
                }
                else if (ex.Message.Contains("CUDA") || ex.Message.Contains("cuda") ||
                         ex.Message.Contains("GPU") || ex.Message.Contains("gpu") ||
                         ex.Message.Contains("shared library"))
                {
                    reason = "CUDA 실행 환경을 로드할 수 없습니다.\n\n" +
                             "가능한 원인:\n" +
                             " - NVIDIA GPU가 장착되지 않은 PC\n" +
                             " - GPU 드라이버가 설치되지 않았거나 버전이 오래됨\n" +
                             " - CUDA DLL 버전 불일치";
                }
                else if (ex.Message.Contains("Opset"))
                {
                    reason = "YOLO 모델의 Opset 버전이 호환되지 않습니다.\n" +
                             "Python에서 model.export(format='onnx', opset=21)로 재변환하세요.";
                }
                else
                {
                    reason = ex.Message;
                }

                MessageBox.Show(
                    $"GPU 모드로 YOLO를 초기화할 수 없습니다.\n\n" +
                    $"[원인]\n{reason}\n\n" +
                    "CPU 모드로 전환하여 작업을 계속 진행합니다.\n" +
                    "모든 기능을 정상적으로 사용할 수 있으나, GPU 대비 처리 속도가 느릴 수 있습니다.",
                    "CPU 모드로 전환",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                isYoloAvailable = false;
            }
        }



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

    }


}



