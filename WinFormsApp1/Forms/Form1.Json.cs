using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenCvSharp;

namespace WinFormsApp1
{
    public partial class Form1
    {
        #region JSON Load/Export
        private async Task LoadLabelingData(string videoFilePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await LoadLabelingData(videoFilePath);
            cancellationToken.ThrowIfCancellationRequested();
        }
        private async Task LoadLabelingData(string videoFilePath)
        {
            Form loadingForm = null;
            Label loadingLabel = null;
            ProgressBar progressBar = null;
            
            try
            {
                string videoDir = Path.GetDirectoryName(videoFilePath);
                if (string.IsNullOrEmpty(videoDir) || !Directory.Exists(videoDir))
                {
                    currentJsonFile = "";
                    UpdateCurrentJsonFileLabel();
                    return;
                }

                string saveDir = Path.Combine(videoDir, "labels");
                if (!Directory.Exists(saveDir))
                {
                    currentJsonFile = "";
                    UpdateCurrentJsonFileLabel();
                    return;
                }

                // ✅ 우선순위: _labels_skeleton.json > _labels.json
                string baseFileName = Path.GetFileNameWithoutExtension(videoFilePath);
                string skeletonFileName = baseFileName + "_labels_skeleton.json";
                string normalFileName = baseFileName + "_labels.json";

                string skeletonPath = Path.Combine(saveDir, skeletonFileName);
                string normalPath = Path.Combine(saveDir, normalFileName);

                string loadPath;
                if (File.Exists(skeletonPath))
                {
                    loadPath = skeletonPath;
                    System.Diagnostics.Debug.WriteLine($"[JSON 로드] skeleton 파일 우선 로드: {skeletonPath}");
                }
                else if (File.Exists(normalPath))
                {
                    loadPath = normalPath;
                    System.Diagnostics.Debug.WriteLine($"[JSON 로드] 일반 파일 로드: {normalPath}");
                }
                else
                {
                    currentJsonFile = "";
                    UpdateCurrentJsonFileLabel();
                    return; // 파일 없음
                }

                // 로드된 파일 경로 저장
                currentJsonFile = loadPath;

                // ✅ 상단 헤더에 현재 JSON 파일명 표시
                UpdateCurrentJsonFileLabel();

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

                // ✅ 4. FileStream + 버퍼링으로 메모리 효율적 로드 (최적화: 큰 버퍼 사용)
                LabelingDataExtended labelingData = null;
                
                await Task.Run(() =>
                {
                    try
                    {
                        // FileStream을 사용하여 버퍼링된 읽기
                        using (FileStream fileStream = new FileStream(loadPath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192))
                        using (StreamReader streamReader = new StreamReader(fileStream, System.Text.Encoding.UTF8, true, 8192))
                        {
                            // UI 스레드에서 진행률 업데이트
                            if (loadingForm != null && loadingForm.InvokeRequired)
                            {
                                loadingForm.Invoke(new Action(() =>
                                {
                                    loadingLabel.Text = "JSON 파일 읽는 중...";
                                    progressBar.Style = ProgressBarStyle.Marquee;
                                }));
                            }
                            
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

                            // JSON 파싱 (기본 설정 사용)
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
                var tempAnnotationsById = new Dictionary<int, AnnotationData>();
                var tempBoxesByAnnotationId = new Dictionary<int, BoundingBox>();
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

                // ✅ Categories에서 속성 스키마 읽기 (person 카테고리)
                Dictionary<string, object> attributeSchema = null;
                if (labelingData.Categories != null)
                {
                    foreach (var category in labelingData.Categories)
                    {
                        tempCategoryMap[category.Id] = category;
                        
                        // Person 카테고리에서 속성 스키마 추출
                        if (category.Supercategory == "person" && category.Attributes != null)
                        {
                            attributeSchema = category.Attributes;
                        }
                    }
                }

                // ✅ Annotations 순차 처리 (단순하고 빠른 처리)
                var waypointKeySet = new Dictionary<string, WaypointMarker>(); // 중복 체크용
                var legacyEventGroupsByWaypoint = new Dictionary<string, List<(string EventInstanceId, Rectangle AnchorRect, int LastFrame)>>(StringComparer.Ordinal);

                string NormalizeLegacyInteractingObject(string value)
                {
                    return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
                }

                bool IsLikelySameLegacyEventGeometry(Rectangle rectA, int frameA, Rectangle rectB, int frameB)
                {
                    int frameGap = Math.Abs(frameA - frameB);
                    if (frameGap > 12)
                        return false;

                    int x1 = Math.Max(rectA.Left, rectB.Left);
                    int y1 = Math.Max(rectA.Top, rectB.Top);
                    int x2 = Math.Min(rectA.Right, rectB.Right);
                    int y2 = Math.Min(rectA.Bottom, rectB.Bottom);
                    int overlapWidth = Math.Max(0, x2 - x1);
                    int overlapHeight = Math.Max(0, y2 - y1);
                    int overlapArea = overlapWidth * overlapHeight;

                    double areaA = Math.Max(1, rectA.Width * rectA.Height);
                    double areaB = Math.Max(1, rectB.Width * rectB.Height);
                    double overlapRatio = overlapArea / Math.Min(areaA, areaB);

                    if (overlapRatio >= 0.25)
                        return true;

                    float centerAx = rectA.X + (rectA.Width / 2f);
                    float centerAy = rectA.Y + (rectA.Height / 2f);
                    float centerBx = rectB.X + (rectB.Width / 2f);
                    float centerBy = rectB.Y + (rectB.Height / 2f);
                    float centerDistance = (float)Math.Sqrt(Math.Pow(centerAx - centerBx, 2) + Math.Pow(centerAy - centerBy, 2));

                    float maxDimension = Math.Max(Math.Max(rectA.Width, rectA.Height), Math.Max(rectB.Width, rectB.Height));
                    if (centerDistance <= (maxDimension * 2.0f) && overlapRatio >= 0.08)
                        return true;

                    return false;
                }

                if (loadingForm != null && loadingForm.InvokeRequired)
                {
                    loadingForm.Invoke(new Action(() =>
                    {
                        loadingLabel.Text = "데이터 처리 중...";
                    }));
                }

                // ✅ null 체크
                if (labelingData.Annotations == null)
                {
                    if (loadingForm != null)
                        loadingForm.Close();
                    return;
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
                    else if (catId >= 25 && catId <= 32)
                    {
                        label = "event";
                    }
                    else if (catId == 33 && tempCategoryMap.TryGetValue(catId, out var category33) &&
                             string.Equals(category33.Name, "cardboard box", StringComparison.OrdinalIgnoreCase))
                    {
                        label = "event";
                    }
                    else if (catId == 33 || catId == LabelCatalogHelper.GetPlateCategoryId())
                    {
                        label = "vehicle"; // plate
                    }
                    else if (tempCategoryMap.ContainsKey(catId)) // ✅ tempCategoryMap 사용
                    {
                        // fallback: 카테고리 이름으로 판단
                        string categoryName = tempCategoryMap[catId].Name;
                        if (categoryName.Contains("car") || categoryName.Contains("motorcycle") || 
                            categoryName.Contains("scooter") || categoryName.Contains("bicycle") ||
                            categoryName.Contains("plate"))
                            label = "vehicle";
                        else if (categoryName.Contains("contact") || categoryName.Contains("exchange") || 
                                 categoryName.Contains("board") || categoryName.Contains("final") ||
                                 categoryName.Contains("disembark") || categoryName.Contains("controlled_delivery") ||
                                  categoryName.Contains("camouflage") || categoryName.Contains("throw") ||
                                  categoryName.Contains("cardboard box"))
                            label = "event";
                        else if (categoryName.StartsWith("person"))
                            label = "person";
                    }

                    // ImageId로 실제 프레임 번호 찾기
                    int frameNumber = annotation.ImageId; // 기본값
                    int actualFrameNumber = frameNumber;
                    if (imageIdToFrameNumber.ContainsKey(annotation.ImageId))
                    {
                        actualFrameNumber = imageIdToFrameNumber[annotation.ImageId];
                    }

                    // EventId 계산 로직 수정: CategoryId에서 역산
                    int personId = 0;
                    int vehicleId = 0;
                    int vehicleInstanceId = annotation.VehicleInstanceId.GetValueOrDefault();
                    string vehiclePartType = null;
                    int eventId = 0;
                    
                    if (label == "person")
                    {
                        personId = trackId;
                    }
                    else if (label == "vehicle")
                    {
                        // Vehicle: CategoryId 21~24 → VehicleId 1~4 (type), 33 → plate
                        vehicleId = catId >= 21 && catId <= 24 ? (catId - 20) : trackId;
                        // Legacy JSON used track_id for VehicleId; use it as an instance fallback only when the new field is absent.
                        if (vehicleInstanceId == 0) vehicleInstanceId = trackId;
                        vehiclePartType = catId == 33 || catId == LabelCatalogHelper.GetPlateCategoryId() ? "plate" : "body";
                    }
                    else if (label == "event")
                    {
                        // Event: CategoryId 25~32 → EventId 1~8
                        string categoryName = tempCategoryMap.TryGetValue(catId, out var category)
                            ? category.Name
                            : null;
                        eventId = LabelCatalogHelper.GetEventIdFromImportedCategory(catId, categoryName);
                        if (eventId == 0)
                        {
                            eventId = trackId;
                        }
                    }

                    var box = new BoundingBox
                    {
                        FrameIndex = actualFrameNumber, // 실제 프레임 번호 사용
                        Rectangle = new Rectangle(annotation.Bbox[0], annotation.Bbox[1], annotation.Bbox[2], annotation.Bbox[3]),
                        Label = label,
                        PersonId = personId,
                        VehicleId = vehicleId,
                        VehicleInstanceId = vehicleInstanceId,
                        VehiclePartType = vehiclePartType,
                        EventId = eventId,
                        EventInstanceId = annotation.EventInstanceId,
                        Action = "waypoint",
                        Skeleton3D = annotation.Skeleton3D ?? annotation.Keypoints3D // Skeleton 데이터 로드
                    };

                    tempBoundingBoxes.Add(box);
                    tempAnnotationsById[annotation.Id] = annotation;
                    tempBoxesByAnnotationId[annotation.Id] = box;

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
                        else if (box.Label == "vehicle") objectId = TrackingIdentityHelper.GetNumericIdentity(box);
                        else if (box.Label == "event") objectId = box.EventId;

                        // ✅ Dictionary 기반 중복 체크 (O(1) 조회) - Race Condition 방지
                        // Event waypoint grouping: prefer EventInstanceId when available
                        string waypointKey;
                        if (box.Label == "event" && string.IsNullOrWhiteSpace(box.EventInstanceId))
                        {
                            string legacyWaypointKey = string.Format("{0}_{1}_{2}_{3}_{4}", box.Label, objectId, entryFrame, exitFrame, NormalizeLegacyInteractingObject(annotation.InteractingObject));
                            string legacyEventInstanceId = null;

                            if (!legacyEventGroupsByWaypoint.TryGetValue(legacyWaypointKey, out var legacyEventGroups))
                            {
                                legacyEventGroups = new List<(string EventInstanceId, Rectangle AnchorRect, int LastFrame)>();
                                legacyEventGroupsByWaypoint[legacyWaypointKey] = legacyEventGroups;
                            }

                            for (int i = 0; i < legacyEventGroups.Count; i++)
                            {
                                var candidate = legacyEventGroups[i];
                                if (IsLikelySameLegacyEventGeometry(candidate.AnchorRect, candidate.LastFrame, box.Rectangle, actualFrameNumber))
                                {
                                    legacyEventInstanceId = candidate.EventInstanceId;
                                    legacyEventGroups[i] = (candidate.EventInstanceId, box.Rectangle, actualFrameNumber);
                                    break;
                                }
                            }

                            if (string.IsNullOrWhiteSpace(legacyEventInstanceId))
                            {
                                legacyEventInstanceId = CreateEventInstanceId();
                                legacyEventGroups.Add((legacyEventInstanceId, box.Rectangle, actualFrameNumber));
                            }

                            box.EventInstanceId = legacyEventInstanceId;
                        }

                        if (box.Label == "event" && !string.IsNullOrWhiteSpace(box.EventInstanceId))
                        {
                            waypointKey = string.Format("event_{0}", box.EventInstanceId);
                        }
                        else
                        {
                            waypointKey = string.Format("{0}_{1}_{2}_{3}", box.Label, objectId, entryFrame, exitFrame);
                        }

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
                            // 색상은 나중에 결정
                            waypointColor = System.Drawing.Color.Black;
                            }
                            
                            var waypoint = new WaypointMarker
                            {
                            ObjectId = objectId,
                            Label = box.Label,
                                EntryFrame = entryFrame,
                                ExitFrame = exitFrame,
                                EntryTime = FormatFrameTime(entryFrame),
                                ExitTime = FormatFrameTime(exitFrame),
                                MarkerColor = waypointColor,
                                EventInstanceId = box.Label == "event" ? box.EventInstanceId : null,
                                InteractingObject = (box.Label == "event") ? (annotation.InteractingObject ?? "") : null
                            };

                        // ✅ Dictionary 기반 중복 체크 (O(1) 조회)
                        if (!waypointKeySet.ContainsKey(waypointKey))
                        {
                            waypointKeySet[waypointKey] = waypoint;
                            tempWaypointMarkers.Add(waypoint);
                        }
                        else
                        {
                            // 이미 존재하는 경우 - event의 interacting_object 업데이트
                            if (box.Label == "event" && !string.IsNullOrWhiteSpace(annotation.InteractingObject))
                            {
                                var existing = waypointKeySet[waypointKey];
                                if (string.IsNullOrWhiteSpace(existing.InteractingObject))
                                {
                                    existing.InteractingObject = annotation.InteractingObject;
                                }
                            }
                        }
                    }
                }

                // ✅ Waypoint 색상 보정 (나중에 색상이 결정되지 않은 경우)
                int waypointIndex = 0;
                foreach (var waypoint in tempWaypointMarkers)
                {
                    if (waypoint.MarkerColor == System.Drawing.Color.Black)
                    {
                        waypoint.MarkerColor = markerColors[waypointIndex % markerColors.Length];
                    }
                    waypointIndex++;
                }

                WaypointNormalizer.NormalizeInPlace(tempWaypointMarkers);

                FaceLinkHelper.ApplyFaceLinks(labelingData.FaceLinks, tempBoxesByAnnotationId, tempAnnotationsById);
                PlateLinkHelper.ApplyPlateLinks(labelingData.PlateLinks, tempBoxesByAnnotationId, tempAnnotationsById);

                // ✅ Person attributes 복원 (최적화: waypoint 매핑 미리 생성 + 일괄 처리)
                // Waypoint 매핑을 미리 생성하여 반복 검색 제거
                var waypointMap = new Dictionary<(string label, int objectId, int frameNumber), int>();
                foreach (var waypoint in tempWaypointMarkers)
                {
                    for (int frame = waypoint.EntryFrame; frame <= waypoint.ExitFrame; frame++)
                    {
                        waypointMap[(waypoint.Label, waypoint.ObjectId, frame)] = waypoint.EntryFrame;
                    }
                }

                // Person attributes를 일괄 수집 (정렬 없이)
                var attributesByPerson = new Dictionary<int, List<(int waypointEntryFrame, int applyFromFrame, string attributeName, object value)>>();

                if (loadingForm != null && loadingForm.InvokeRequired)
                {
                    loadingForm.Invoke(new Action(() =>
                    {
                        loadingLabel.Text = "속성 복원 중...";
                    }));
                }

                int attributeProcessedCount = 0;
                int attributeTotalCount = labelingData.Annotations
                    .Count(a => a.Bbox != null && a.Bbox.Length >= 4 && 
                                a.CategoryId >= 1 && a.CategoryId <= 20 && 
                                (a.PersonAttributes != null && a.PersonAttributes.Count > 0 || 
                                 a.Attributes != null && a.Attributes.Count > 0));

                // Person별로 초기 프레임의 속성 추적 (초기 프레임 판단용)
                var initialFrameAttributesByPerson = new Dictionary<int, Dictionary<string, object>>();

                foreach (var annotation in labelingData.Annotations)
                {
                    if (annotation.Bbox == null || annotation.Bbox.Length < 4)
                        continue;

                    // CategoryId로 라벨 결정
                    int catId = annotation.CategoryId;
                    if (catId < 1 || catId > 20) continue; // person만 처리
                    
                    int personId = annotation.TrackId;
                    
                    // ImageId로 실제 프레임 번호 찾기
                    int frameNumber = annotation.ImageId;
                    if (imageIdToFrameNumber.ContainsKey(annotation.ImageId))
                    {
                        frameNumber = imageIdToFrameNumber[annotation.ImageId];
                    }
                    
                    // Waypoint EntryFrame 찾기 (매핑 사용)
                    int waypointEntryFrame = frameNumber;
                    if (waypointMap.TryGetValue(("person", personId, frameNumber), out int entryFrame))
                    {
                        waypointEntryFrame = entryFrame;
                    }

                    // person_attributes 또는 기존 attributes 필드 처리
                    Dictionary<string, object> attributesToProcess = null;
                    bool isInitialFrame = false;
                    
                    if (annotation.PersonAttributes != null && annotation.PersonAttributes.Count > 0)
                    {
                        attributesToProcess = annotation.PersonAttributes;
                        // 초기 프레임 판단: 모든 속성이 포함되어 있고 null도 포함되어 있으면 초기 프레임
                        // 또는 waypointEntryFrame과 frameNumber가 같으면 초기 프레임
                        isInitialFrame = (waypointEntryFrame == frameNumber) || 
                                       (attributeSchema != null && attributesToProcess.Count == attributeSchema.Count);
                    }
                    else if (annotation.Attributes != null && annotation.Attributes.Count > 0)
                        {
                        // 기존 Attributes 필드 호환성 처리
                        attributesToProcess = annotation.Attributes;
                        isInitialFrame = (waypointEntryFrame == frameNumber);
                    }
                    
                    if (attributesToProcess == null || attributesToProcess.Count == 0)
                        continue;
                    
                    if (!attributesByPerson.ContainsKey(personId))
                    {
                        attributesByPerson[personId] = new List<(int, int, string, object)>();
                        }
                        
                    // Weight/BodyShape를 Weight와 BodyPosture로 분리 (기존 데이터 호환성)
                    foreach (var kvp in attributesToProcess)
                    {
                        string attrName = kvp.Key;
                        object attrValue = kvp.Value;
                        
                        if (attrValue == null)
                            continue;
                        
                        // JSON에서 배열로 저장된 경우 List<string>으로 변환
                        object processedValue = attrValue;
                        
                        // "보임/가림/행동" 탭 속성은 단일 값 유지
                        if (attrName != "Occlusion" && attrName != "BodyView" && attrName != "ActionType")
                        {
                            // 배열인지 확인하고 List<string>으로 변환
                            if (attrValue is Newtonsoft.Json.Linq.JArray jArray)
                            {
                                processedValue = jArray.ToObject<List<string>>();
                            }
                            else if (attrValue is List<object> objectList)
                            {
                                processedValue = objectList.Select(x => x?.ToString()).Where(x => x != null).ToList();
                            }
                            else if (attrValue is object[] objectArray)
                            {
                                processedValue = objectArray.Select(x => x?.ToString()).Where(x => x != null).ToList();
                            }
                            // 단일 값인 경우는 그대로 유지 (기존 데이터 호환성)
                        }
                        
                        // Weight/BodyShape를 그대로 저장 (Weight와 BodyPosture로 분리하지 않음)
                        attributesByPerson[personId].Add((waypointEntryFrame, frameNumber, attrName, processedValue));
                    }
                    
                    // 초기 프레임인 경우 속성 스키마와 병합하여 저장
                    if (isInitialFrame && attributeSchema != null)
                    {
                        var mergedAttributes = new Dictionary<string, object>(attributeSchema);
                        foreach (var kvp in attributesToProcess)
                        {
                            mergedAttributes[kvp.Key] = kvp.Value;
                        }
                        initialFrameAttributesByPerson[personId] = mergedAttributes;
                    }
                    
                    attributeProcessedCount++;
                    if (attributeProcessedCount % 100 == 0 && loadingForm != null && loadingForm.InvokeRequired)
                            {
                        loadingForm.Invoke(new Action(() =>
                        {
                            loadingLabel.Text = $"속성 복원 중... ({attributeProcessedCount}/{attributeTotalCount})";
                        }));
                    }
                }

                // ✅ 일괄 처리: PersonId별로 그룹화하여 한 번에 처리 (정렬 최소화)
                if (loadingForm != null && loadingForm.InvokeRequired)
                {
                    loadingForm.Invoke(new Action(() =>
                    {
                        loadingLabel.Text = "속성 저장 중...";
                    }));
                }

                int personProcessedCount = 0;
                foreach (var kvp in attributesByPerson)
                {
                    personAttributeStore.SetAttributesBatch(kvp.Key, kvp.Value, tempWaypointMarkers, videoFilePath);
                    
                    personProcessedCount++;
                    if (personProcessedCount % 10 == 0 && loadingForm != null && loadingForm.InvokeRequired)
                    {
                        loadingForm.Invoke(new Action(() =>
                        {
                            loadingLabel.Text = $"속성 저장 중... ({personProcessedCount}/{attributesByPerson.Count})";
                        }));
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

                // ✅ 백업 파일 찾기 (skeleton 우선)
                string baseFileName = Path.GetFileNameWithoutExtension(videoFilePath);
                string skeletonBackup = Path.Combine(saveDir, baseFileName + "_labels_skeleton.json.backup");
                string normalBackup = Path.Combine(saveDir, baseFileName + "_labels.json.backup");

                string backupPath = File.Exists(skeletonBackup) ? skeletonBackup : normalBackup;

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

                private async void btnExportJson_Click(object sender, EventArgs e)
        {
            try
            {
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
                    InvalidateBoxCache();
                    UpdateBoxCount();
                    await Task.Run(() => SaveCurrentLabelingData());
                    loadingForm.Close();

                    if (!string.IsNullOrEmpty(currentJsonFile) && File.Exists(currentJsonFile))
                    {
                        await LoadLabelingData(currentVideoFile);
                    }

                    string savedFileName = !string.IsNullOrEmpty(currentJsonFile) ? Path.GetFileName(currentJsonFile) : "labels.json";
                    string labelsDir = Path.GetDirectoryName(currentJsonFile) ?? Path.Combine(Path.GetDirectoryName(currentVideoFile), "labels");

                    MessageBox.Show(
                        $"JSON 파일이 저장되었습니다.\n\n" +
                        $"파일: {savedFileName}\n" +
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
        private bool SaveCurrentLabelingData()
        {
            if (string.IsNullOrEmpty(currentVideoFile))
                return false;

            try
            {
                EventFinalizationHelper.ClampAllEventBoxesToWaypoints(
                    boundingBoxes,
                    waypointMarkers);

                string videoDir = Path.GetDirectoryName(currentVideoFile);
                if (string.IsNullOrEmpty(videoDir) || !Directory.Exists(videoDir))
                {
                    MessageBox.Show("비디오 파일의 디렉토리를 찾을 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
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
                    return false;
                }

                // ✅ 기존에 로드된 파일에 저장, 없으면 _labels.json으로 생성
                string savePath;
                if (!string.IsNullOrEmpty(currentJsonFile) && File.Exists(currentJsonFile))
                {
                    savePath = currentJsonFile;
                    System.Diagnostics.Debug.WriteLine($"[JSON 저장] 기존 파일에 저장: {savePath}");
                }
                else
                {
                    string fileName = Path.GetFileNameWithoutExtension(currentVideoFile) + "_labels.json";
                    savePath = Path.Combine(saveDir, fileName);
                    currentJsonFile = savePath;
                    System.Diagnostics.Debug.WriteLine($"[JSON 저장] 새 파일 생성: {savePath}");
                }

                WaypointNormalizer.NormalizeInPlace(waypointMarkers);
                if (!ExportToJsonExtended(savePath))
                    return false;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"라벨링 데이터 저장 중 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
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

                // ✅ 현재 로드된 파일 또는 존재하는 파일 찾기
                string jsonPath;
                string fileName;
                if (!string.IsNullOrEmpty(currentJsonFile) && File.Exists(currentJsonFile))
                {
                    jsonPath = currentJsonFile;
                    fileName = Path.GetFileName(currentJsonFile);
                }
                else
                {
                    // skeleton 파일 우선 확인
                    string baseFileName = Path.GetFileNameWithoutExtension(currentVideoFile);
                    string skeletonPath = Path.Combine(saveDir, baseFileName + "_labels_skeleton.json");
                    string normalPath = Path.Combine(saveDir, baseFileName + "_labels.json");

                    if (File.Exists(skeletonPath))
                    {
                        jsonPath = skeletonPath;
                        fileName = Path.GetFileName(skeletonPath);
                    }
                    else if (File.Exists(normalPath))
                    {
                        jsonPath = normalPath;
                        fileName = Path.GetFileName(normalPath);
                    }
                    else
                    {
                        MessageBox.Show("삭제할 JSON 파일이 존재하지 않습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
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
                    currentJsonFile = "";  // 삭제 후 경로 초기화

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
                    UpdateCurrentJsonFileLabel();  // 상단 JSON 파일명 초기화
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

        // 속성 값 비교 (배열 비교 지원)
        private bool AreAttributeValuesEqualForExport(object current, object previous)
        {
            // null 비교
            if (current == null && previous == null) return true;
            if (current == null || previous == null) return false;
            
            // 배열 비교
            List<string> currentList = new List<string>();
            List<string> previousList = new List<string>();
            
            if (current is List<string> currentListValue)
            {
                currentList = currentListValue;
            }
            else if (current is string[] currentArrayValue)
            {
                currentList = currentArrayValue.ToList();
            }
            else if (current is string currentString)
            {
                currentList = new List<string> { currentString };
            }
            else
            {
                currentList = new List<string> { current.ToString() };
            }
            
            if (previous is List<string> previousListValue)
            {
                previousList = previousListValue;
            }
            else if (previous is string[] previousArrayValue)
            {
                previousList = previousArrayValue.ToList();
            }
            else if (previous is string previousString)
            {
                previousList = new List<string> { previousString };
            }
            else
            {
                previousList = new List<string> { previous.ToString() };
            }
            
            // 정렬 후 비교
            return currentList.OrderBy(x => x).SequenceEqual(previousList.OrderBy(x => x));
        }

        private bool ExportToJsonExtended(string filePath)
        {
            try
            {
                var images = new List<ImageInfo>();
                var annotations = new List<AnnotationData>();
                var faceLinks = new List<FaceLinkData>();
                var plateLinks = new List<PlateLinkData>();
                var categories = new Dictionary<int, CategoryData>();

                // 모든 속성 목록 정의 (Weight/BodyShape로 통합)
                var allAttributeNames = new HashSet<string>
                {
                    // View
                    "Occlusion", "BodyView", "Camouflage",
                    // Biometric
                    "Age", "Gender", "Height", "Weight/BodyShape", "Face",
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

                // ✅ 삭제되지 않은 박스만 JSON에 저장
                var frameGroups = boundingBoxes.Where(b => !b.IsDeleted).GroupBy(b => b.FrameIndex).OrderBy(g => g.Key);
                int imageId = 0;
                bool personCategoryAttributesSet = false; // Categories에 속성 스키마 저장 여부
                
                // Person별로 이전 프레임의 속성을 추적 (변경 감지용)
                var previousAttributesByPerson = new Dictionary<int, Dictionary<string, object>>();
                
                // ✅ Person별 첫 번째 waypoint entry frame의 속성 수집 (categories에 저장할 초기 속성)
                var initialAttributesByPerson = new Dictionary<int, Dictionary<string, object>>();
                var personIds = boundingBoxes
                    .Where(b => b.Label == "person" && !b.IsDeleted)
                    .Select(b => b.PersonId)
                    .Distinct()
                    .ToList();
                
                foreach (var personId in personIds)
                {
                    var firstWaypoint = waypointMarkers
                        .Where(w => w.Label == "person" && w.ObjectId == personId)
                        .OrderBy(w => w.EntryFrame)
                        .FirstOrDefault();
                    
                    if (firstWaypoint != null)
                    {
                        var attributes = personAttributeStore.GetAllAttributes(
                            personId, firstWaypoint.EntryFrame, waypointMarkers, currentVideoFile);
                        if (attributes != null && attributes.Count > 0)
                        {
                            initialAttributesByPerson[personId] = attributes;
                        }
                    }
                }

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
                    var currentFrameAnnotations = new Dictionary<BoundingBox, AnnotationData>();

                    foreach (var box in frameGroup)
                    {
                        // 박스의 라벨 타입에 맞는 ID 가져오기
                        int boxId = GetBoxId(box);
                        int categoryBoxId = box.Label == "vehicle" ? box.VehicleId : boxId;
                        // 스펙에 맞는 Category ID와 Name 사용 (vehicle plate는 VehiclePartType 기준으로 별도 처리)
                        int categoryId;
                        string categoryName;
                        if (string.Equals(box.Label, "vehicle", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(box.VehiclePartType, "plate", StringComparison.OrdinalIgnoreCase))
                        {
                            categoryId = LabelCatalogHelper.GetPlateCategoryId();
                            categoryName = LabelCatalogHelper.GetVehicleCategoryName(box.VehicleId, box.VehiclePartType);
                        }
                        else
                        {
                            categoryId = GetCategoryId(box.Label, categoryBoxId);
                            categoryName = GetCategoryName(box.Label, categoryBoxId);
                        }
                        
                        if (!categories.ContainsKey(categoryId))
                        {
                            categories[categoryId] = new CategoryData
                            {
                                Id = categoryId,
                                Name = categoryName,
                                Supercategory = box.Label
                            };
                            
                            // Person 카테고리에 속성 스키마 저장 (한 번만)
                            if (box.Label == "person" && !personCategoryAttributesSet)
                            {
                                categories[categoryId].Attributes = new Dictionary<string, object>();
                                
                                // ✅ 초기 속성들을 병합하여 categories에 저장
                                foreach (string attrName in allAttributeNames)
                                {
                                    object mergedValue = null;
                                    
                                    // 모든 person의 초기 속성에서 해당 속성 이름의 값 수집
                                    var valuesForAttribute = new List<object>();
                                    foreach (var personAttrs in initialAttributesByPerson.Values)
                                    {
                                        if (personAttrs.ContainsKey(attrName) && personAttrs[attrName] != null)
                                        {
                                            valuesForAttribute.Add(personAttrs[attrName]);
                                        }
                                    }
                                    
                                    if (valuesForAttribute.Count > 0)
                                    {
                                        // 모든 person이 같은 값을 가지는지 확인
                                        bool allSame = true;
                                        object firstValue = valuesForAttribute[0];
                                        
                                        foreach (var value in valuesForAttribute)
                                        {
                                            if (!AreAttributeValuesEqualForExport(firstValue, value))
                                            {
                                                allSame = false;
                                                break;
                                            }
                                        }
                                        
                                        // 모든 person이 같은 값을 가지는 경우에만 저장
                                        if (allSame)
                                        {
                                            // 단일/다중 선택에 따라 형식 변환
                                            if (PersonAttributeStore.singleSelectAttributeNames.Contains(attrName))
                                            {
                                                // 단일 선택 속성: 단일 값(string) 또는 null
                                                if (firstValue is List<string> listValue && listValue.Count > 0)
                                                {
                                                    mergedValue = listValue[0];
                                                }
                                                else if (firstValue is string[] arrayValue && arrayValue.Length > 0)
                                                {
                                                    mergedValue = arrayValue[0];
                                                }
                                                else if (firstValue is string stringValue)
                                                {
                                                    mergedValue = stringValue;
                                                }
                                                else
                                                {
                                                    mergedValue = firstValue?.ToString();
                                                }
                                            }
                                            else
                                            {
                                                // 다중 선택 속성: 배열(List<string>) 또는 null
                                                if (firstValue is List<string> listValue2)
                                                {
                                                    mergedValue = listValue2.Count > 0 ? listValue2 : null;
                                                }
                                                else if (firstValue is string[] arrayValue2)
                                                {
                                                    mergedValue = arrayValue2.Length > 0 ? arrayValue2.ToList() : null;
                                                }
                                                else if (firstValue is string stringValue2)
                                                {
                                                    mergedValue = new List<string> { stringValue2 };
                                                }
                                                else
                                                {
                                                    mergedValue = firstValue != null ? new List<string> { firstValue.ToString() } : null;
                                                }
                                            }
                                        }
                                        // 값이 다른 경우 null로 설정 (또는 첫 번째 person의 값 사용)
                                        // 플랜에 따라 null로 설정
                                    }
                                    
                                    categories[categoryId].Attributes[attrName] = mergedValue;
                                }
                                
                                personCategoryAttributesSet = true;
                            }
                        }

                        // 현재 박스가 속한 실제 waypoint clip을 기준으로 track range를 저장합니다.
                        var clipRange = WaypointClipResolver.ResolveClipRange(box, waypointMarkers);
                        int entryFrame = clipRange.EntryFrame;
                        int exitFrame = clipRange.ExitFrame;
                        WaypointMarker matchingWaypoint = clipRange.Waypoint;

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
                            TrackId = box.Label == "vehicle" ? box.VehicleId : GetDrawingIdentityId(box),
                        VehicleInstanceId = box.Label == "vehicle" && box.VehicleInstanceId > 0 ? box.VehicleInstanceId : (int?)null,
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

                        if (box.Label == "event" && !string.IsNullOrWhiteSpace(box.EventInstanceId))
                        {
                            annotation.EventInstanceId = box.EventInstanceId;
                        }

                        // Event인 경우 상호작용 객체 텍스트 포함 (해당 박스가 속한 웨이포인트에서 가져옴)
                        if (box.Label == "event" && matchingWaypoint != null && !string.IsNullOrWhiteSpace(matchingWaypoint.InteractingObject))
                        {
                            annotation.InteractingObject = matchingWaypoint.InteractingObject;
                        }

                        // Person인 경우 person_attributes 저장
                        if (box.Label == "person")
                        {
                            // 현재 프레임의 속성 가져오기
                            var currentAttributes = personAttributeStore.GetAllAttributes(box.PersonId, box.FrameIndex, waypointMarkers, currentVideoFile);
                            
                            // Weight와 BodyPosture를 Weight/BodyShape로 통합
                            var mergedAttributes = new Dictionary<string, object>();
                            if (currentAttributes != null)
                            {
                                foreach (var kvp in currentAttributes)
                                {
                                    string attrName = kvp.Key;
                                    object attrValue = kvp.Value;
                                    
                                    // Weight와 BodyPosture를 Weight/BodyShape로 통합
                                    if (attrName == "Weight" || attrName == "BodyPosture")
                                    {
                                        if (!mergedAttributes.ContainsKey("Weight/BodyShape"))
                                        {
                                            mergedAttributes["Weight/BodyShape"] = attrValue;
                                        }
                                        else if (attrValue != null)
                                        {
                                            // 두 값이 모두 있으면 BodyPosture 값을 우선 (Stooped)
                                            if (attrName == "BodyPosture" && attrValue != null)
                                            {
                                                mergedAttributes["Weight/BodyShape"] = attrValue;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        mergedAttributes[attrName] = attrValue;
                                    }
                                }
                            }
                            
                            // 속성 값을 JSON 저장 형식으로 변환 (단일/배열 구분)
                            var formattedAttributes = new Dictionary<string, object>();
                            foreach (var kvp in mergedAttributes)
                            {
                                string attrName = kvp.Key;
                                object attrValue = kvp.Value;
                                
                                // 단일 선택 속성은 단일 값으로 저장
                                if (PersonAttributeStore.singleSelectAttributeNames.Contains(attrName))
                                {
                                    // 단일 선택 속성: 단일 값(string) 또는 null
                                    if (attrValue == null)
                                    {
                                        formattedAttributes[attrName] = null;
                                    }
                                    else if (attrValue is List<string> listValue && listValue.Count > 0)
                                    {
                                        // 배열인 경우 첫 번째 값만 사용 (기존 데이터 호환성)
                                        formattedAttributes[attrName] = listValue[0];
                                    }
                                    else if (attrValue is string[] arrayValue && arrayValue.Length > 0)
                                    {
                                        formattedAttributes[attrName] = arrayValue[0];
                                    }
                                    else if (attrValue is string stringValue)
                                    {
                                        formattedAttributes[attrName] = stringValue;
                                    }
                                    else
                                    {
                                        formattedAttributes[attrName] = attrValue.ToString();
                                    }
                                }
                                else
                                {
                                    // 다중 선택 속성은 배열로 저장
                                    if (attrValue == null)
                                    {
                                        formattedAttributes[attrName] = null;
                                    }
                                    else if (attrValue is List<string> listValue)
                                    {
                                        // 빈 배열은 null로 저장
                                        formattedAttributes[attrName] = listValue.Count > 0 ? listValue : null;
                                    }
                                    else if (attrValue is string[] arrayValue)
                                    {
                                        formattedAttributes[attrName] = arrayValue.Length > 0 ? arrayValue.ToList() : null;
                                    }
                                    else if (attrValue is string stringValue)
                                    {
                                        // 단일 값인 경우 배열로 변환 (기존 데이터 호환성)
                                        formattedAttributes[attrName] = new List<string> { stringValue };
                                    }
                                    else
                                    {
                                        formattedAttributes[attrName] = new List<string> { attrValue.ToString() };
                                    }
                                }
                            }
                            
                            // 초기 프레임 판단: Waypoint EntryFrame과 현재 프레임이 같으면 초기 프레임
                            bool isInitialFrame = (matchingWaypoint != null && box.FrameIndex == matchingWaypoint.EntryFrame);
                            
                            if (isInitialFrame)
                            {
                                // 초기 프레임: 모든 속성 저장 (null 포함)
                            var allAttributes = new Dictionary<string, object>();
                            foreach (string attrName in allAttributeNames)
                            {
                                    if (formattedAttributes.ContainsKey(attrName))
                                {
                                        allAttributes[attrName] = formattedAttributes[attrName];
                                }
                                else
                                {
                                        allAttributes[attrName] = null;
                                    }
                                }
                                annotation.PersonAttributes = allAttributes;
                                
                                // 이전 속성 업데이트 (다음 프레임 비교용)
                                previousAttributesByPerson[box.PersonId] = new Dictionary<string, object>(allAttributes);
                            }
                            else
                            {
                                // 변경 프레임: 이전 프레임과 비교하여 변경된 속성만 저장 (null 제외)
                                var changedAttributes = new Dictionary<string, object>();
                                
                                // 이전 프레임의 속성 가져오기
                                Dictionary<string, object> previousAttributes = null;
                                if (previousAttributesByPerson.ContainsKey(box.PersonId))
                                {
                                    previousAttributes = previousAttributesByPerson[box.PersonId];
                                }
                                
                                // 모든 속성 이름에 대해 비교
                                foreach (string attrName in allAttributeNames)
                                {
                                    object currentValue = formattedAttributes.ContainsKey(attrName) ? formattedAttributes[attrName] : null;
                                    object previousValue = previousAttributes != null && previousAttributes.ContainsKey(attrName) ? previousAttributes[attrName] : null;
                                    
                                    // 값이 다르고 현재 값이 null이 아니면 변경된 것으로 간주 (배열 비교 지원)
                                    if (!AreAttributeValuesEqualForExport(currentValue, previousValue) && currentValue != null)
                                    {
                                        changedAttributes[attrName] = currentValue;
                                }
                            }
                            
                                if (changedAttributes.Count > 0)
                                {
                                    annotation.PersonAttributes = changedAttributes;
                                }
                                
                                // ✅ 이전 속성 업데이트: 현재 프레임의 모든 속성을 업데이트 (다음 프레임 비교용)
                                // 변경되지 않은 속성도 유지해야 정확한 비교 가능
                                if (!previousAttributesByPerson.ContainsKey(box.PersonId))
                                {
                                    previousAttributesByPerson[box.PersonId] = new Dictionary<string, object>();
                                }
                                
                                // 현재 프레임의 모든 속성을 이전 속성에 반영
                                foreach (string attrName in allAttributeNames)
                                {
                                    object currentValue = formattedAttributes.ContainsKey(attrName) ? formattedAttributes[attrName] : null;
                                    if (currentValue != null)
                                    {
                                        previousAttributesByPerson[box.PersonId][attrName] = currentValue;
                                    }
                                    else if (previousAttributesByPerson[box.PersonId].ContainsKey(attrName))
                                    {
                                        // null로 변경된 경우도 반영 (속성이 제거된 경우)
                                        previousAttributesByPerson[box.PersonId][attrName] = null;
                                    }
                                }
                            }
                        }

                        annotations.Add(annotation);
                        currentFrameAnnotations[box] = annotation;
                    }

                    foreach (var faceBox in frameGroup.Where(box => string.Equals(box.Label, "person", StringComparison.OrdinalIgnoreCase) && string.Equals(box.PersonPartType, "face", StringComparison.OrdinalIgnoreCase)))
                    {
                        var faceLink = FaceLinkHelper.TryCreateFaceLink(faceBox, currentFrameAnnotations.Values, currentFrameAnnotations);
                        if (faceLink != null)
                        {
                            faceLinks.Add(faceLink);
                        }
                        else if (!faceBox.LinkedPersonId.HasValue)
                        {
                            System.Diagnostics.Debug.WriteLine($"[FaceLinkExport] Missing linked body id for face box at frame {faceBox.FrameIndex}.");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[FaceLinkExport] Failed to match body annotation for linked face box at frame {faceBox.FrameIndex}.");
                        }
                    }

                    foreach (var plateBox in frameGroup.Where(box => string.Equals(box.Label, "vehicle", StringComparison.OrdinalIgnoreCase) && string.Equals(box.VehiclePartType, "plate", StringComparison.OrdinalIgnoreCase)))
                    {
                        var plateLink = PlateLinkHelper.TryCreatePlateLink(plateBox, currentFrameAnnotations.Values, currentFrameAnnotations);
                        if (plateLink != null)
                        {
                            plateLinks.Add(plateLink);
                        }
                        else if (!plateBox.LinkedVehicleInstanceId.HasValue)
                        {
                            System.Diagnostics.Debug.WriteLine($"[PlateLinkExport] Missing linked body id for plate box at frame {plateBox.FrameIndex}.");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[PlateLinkExport] Failed to match body annotation for linked plate box at frame {plateBox.FrameIndex}.");
                        }
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
                    FailureRanges = waypointFailureRanges,
                    FaceLinks = faceLinks.Count > 0 ? faceLinks : null,
                    PlateLinks = plateLinks.Count > 0 ? plateLinks : null
                };

                // null 값도 포함하여 직렬화
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Include,
                    Formatting = Formatting.Indented
                };
                string json = JsonConvert.SerializeObject(labelingData, settings);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"JSON 내보내기 오류: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
        /// <summary>
        /// Q키: Event 종료 - 현재 프레임부터 Exit까지 Event 박스 삭제
        /// </summary>
        private void TerminateEventFromCurrentFrame()
        {
            // 현재 프레임에 Event 박스가 있는지 확인
            var eventBoxesAtFrame = boundingBoxes
                .Where(b => b.FrameIndex == currentFrameIndex && b.Label == "event" && !b.IsDeleted)
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

            if (selectedBox != null &&
                string.Equals(selectedBox.Label, "event", StringComparison.OrdinalIgnoreCase) &&
                selectedBox.FrameIndex == currentFrameIndex &&
                !selectedBox.IsDeleted)
            {
                targetEvent = selectedBox;
            }
            else
            {
                // 여러 Event 중 선택
                var eventTypes = LabelCatalogHelper.EventTypes;
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
            var eventWaypoint = targetEvent != null
                ? FindWaypointForBox(targetEvent)
                : null;
            
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
            int newExitFrame = currentFrameIndex - 1;
            var boxesToDelete = EventFinalizationHelper.GetEventBoxesForWaypoint(
                boundingBoxes,
                eventWaypoint,
                startFrame: currentFrameIndex)
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
            var eventTypes2 = LabelCatalogHelper.EventTypes;
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
                eventWaypoint.ExitFrame = newExitFrame;
                TimeSpan exitTime = TimeSpan.FromSeconds(newExitFrame / fps);
                eventWaypoint.ExitTime = exitTime.ToString(@"hh\:mm\:ss");

                EventFinalizationHelper.ClampEventBoxesToWaypointExit(
                    boundingBoxes,
                    eventWaypoint);
                
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

    }
}





