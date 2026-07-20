using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinFormsApp1
{
    public partial class Form1
    {
        #region Entry/Exit Markers
        private void btnEntry_Click(object sender, EventArgs e)
        {
            // E?ㅼ? ?숈씪??湲곕뒫: Entry 留덉빱 ?ㅼ젙
            SetEntryMarker();
        }

        private void SetEntryMarker()
        {
            // 媛앹껜 ?좏깮 ?놁씠??Entry ?꾨젅???ㅼ젙 媛??
            entryFrameIndex = currentFrameIndex;
            TimeSpan entryTime = TimeSpan.FromSeconds(currentFrameIndex / fps);
            btnEntry.Text = $"Entry: {entryTime:hh\\:mm\\:ss}";
            panelTimeline.Invalidate();
        }

        private async void btnExit_Click(object sender, EventArgs e)
        {
            try
            {
                // Block Exit action while YOLO is running.
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[Exit] YOLO 작업 진행 중: Exit 동작 비활성화");
                    MessageBox.Show(
                        "YOLO 추적 또는 탐지가 진행 중입니다.\n작업이 완료될 때까지 기다려 주세요.",
                        "작업 중",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                AppendFaceDebugLog("ExitButton", $"Clicked: frame={currentFrameIndex}, selectedLabel={selectedBox?.Label}, selectedPersonPartType={selectedBox?.PersonPartType}, selectedPersonId={selectedBox?.PersonId}, linkedPersonId={selectedBox?.LinkedPersonId}, entryFrame={selectedBox?.BoxEntryFrame}, exitFrame={selectedBox?.BoxExitFrame}");
                await SetExitMarkerAndCreateWaypoint();
            }
            catch (Exception ex)
            {
                AppendFaceDebugLog("ExitButtonError", ex.ToString());
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
                if (selectedBox != null &&
                    selectedBox.Label == "person" &&
                    string.Equals(selectedBox.PersonPartType, "face", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        "Face 트래킹은 Exit 경로(또는 버튼/단축키)로 완료하지 않습니다.\n\n" +
                        "Face는 R로 a프레임을 설정한 뒤 Shift+T로 보간하세요.",
                        "알림",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                if (entryFrameIndex.HasValue)
                {
                    // Exit time must be after Entry and then create waypoint data normally.
                    if (currentFrameIndex <= entryFrameIndex.Value)
                    {
                        TimeSpan currentTimeCheck = TimeSpan.FromSeconds(currentFrameIndex / fps);
                        TimeSpan entryTimeCheck = TimeSpan.FromSeconds(entryFrameIndex.Value / fps);

                        MessageBox.Show(
                            $"Exit 프레임은 Entry 프레임 이후여야 합니다.\n\n" +
                            $"Entry: {entryTimeCheck:hh\\:mm\\:ss} (frame {entryFrameIndex.Value})\n" +
                            $"현재: {currentTimeCheck:hh\\:mm\\:ss} (frame {currentFrameIndex})\n\n" +
                            "Entry보다 먼저 Exit을 설정할 수 없습니다.",
                            "경고",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    WaypointNormalizer.NormalizeInPlace(waypointMarkers);
                    var entryPersonBoxes = boundingBoxes.Where(b => b.FrameIndex == entryFrameIndex.Value && b.Label == "person").ToList();
                    var entryVehicleBoxes = WaypointGroupingHelper.GetVehicleWaypointBodies(boundingBoxes, entryFrameIndex.Value);
                    var entryEventBoxes = boundingBoxes.Where(b => b.FrameIndex == entryFrameIndex.Value && b.Label == "event").ToList();

                    if (entryPersonBoxes.Count == 0 && entryVehicleBoxes.Count == 0 && entryEventBoxes.Count == 0)
                    {
                        var eventBoxesInRangeCheck = boundingBoxes
                            .Where(b => b.Label == "event" &&
                                       b.FrameIndex >= entryFrameIndex.Value &&
                                       b.FrameIndex <= currentFrameIndex)
                            .ToList();

                        if (eventBoxesInRangeCheck.Count == 0)
                        {
                            MessageBox.Show(
                                "Entry에서 Person, Vehicle, Event 박스 중 하나가 있어야 합니다.\n" +
                                "또는 Entry~Exit 구간에 Event 박스가 있어야 합니다.\n" +
                                "확인 후 다시 시도해 주세요.",
                                "경고",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                            return;
                        }
                    }

                    exitFrameIndex = currentFrameIndex;
                    TimeSpan exitTime = TimeSpan.FromSeconds(currentFrameIndex / fps);
                    TimeSpan entryTime = TimeSpan.FromSeconds(entryFrameIndex.Value / fps);
                    btnExit.Text = $"Exit: {exitTime:hh\\:mm\\:ss}";

                    List<WaypointMarker> createdWaypoints = new List<WaypointMarker>();
                    int currentEntryFrame = entryFrameIndex.Value;
                    int currentExitFrame = exitFrameIndex.Value;

                    foreach (var personBox in entryPersonBoxes)
                    {
                        int personId = personBox.PersonId;

                        var overlappingWaypoint = WaypointOverlapHelper.FindOverlappingWaypoint(waypointMarkers, "person", personId, currentEntryFrame, currentExitFrame);

                        if (overlappingWaypoint != null)
                        {
                            if (currentExitFrame > overlappingWaypoint.ExitFrame)
                            {
                                TimeSpan currentExitTime = TimeSpan.FromSeconds(currentExitFrame / fps);
                                TimeSpan oldExitTime = TimeSpan.FromSeconds(overlappingWaypoint.ExitFrame / fps);
                                MessageBox.Show(
                                    "Exit 프레임은 기존 Waypoint의 Exit 프레임보다 뒤로 연장할 수 없습니다.\n\n" +
                                    $"객체: {GetCategoryName("person", personId)}\n" +
                                    $"기존 Waypoint: Entry={TimeSpan.FromSeconds(overlappingWaypoint.EntryFrame / fps):hh\\:mm\\:ss}, Exit={oldExitTime:hh\\:mm\\:ss}\n" +
                                    $"요청 Exit: {currentExitTime:hh\\:mm\\:ss}\n\n" +
                                    "존재하는 구간을 확인하고 다시 설정해 주세요.",
                                    "Warning",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                                continue;
                            }

                            System.Diagnostics.Debug.WriteLine($"[Person Waypoint Overlap] PersonId={personId}: existing({overlappingWaypoint.EntryFrame}~{overlappingWaypoint.ExitFrame}) vs requested({currentEntryFrame}~{currentExitFrame})");
                            continue;
                        }

                        var waypoint = new WaypointMarker
                        {
                            EntryFrame = entryFrameIndex.Value,
                            ExitFrame = exitFrameIndex.Value,
                            MarkerColor = Color.FromArgb(255, 107, 107),
                            EntryTime = entryTime.ToString(@"hh\:mm\:ss"),
                            ExitTime = exitTime.ToString(@"hh\:mm\:ss"),
                            ObjectId = personId,
                            Label = "person"
                        };

                        waypointMarkers.Add(waypoint);
                        createdWaypoints.Add(waypoint);
                    }

                    foreach (var vehicleBox in entryVehicleBoxes)
                    {
                        int vehicleInstanceId = TrackingIdentityHelper.GetNumericIdentity(vehicleBox);
                        if (vehicleInstanceId <= 0)
                        {
                            continue;
                        }

                        var overlappingWaypoint = WaypointOverlapHelper.FindOverlappingWaypoint(
                            waypointMarkers,
                            "vehicle",
                            vehicleInstanceId,
                            currentEntryFrame,
                            currentExitFrame);

                        if (overlappingWaypoint != null)
                        {
                            bool changed = false;
                            if (currentEntryFrame < overlappingWaypoint.EntryFrame)
                            {
                                overlappingWaypoint.EntryFrame = currentEntryFrame;
                                overlappingWaypoint.EntryTime = entryTime.ToString(@"hh\:mm\:ss");
                                changed = true;
                            }

                            if (currentExitFrame > overlappingWaypoint.ExitFrame)
                            {
                                overlappingWaypoint.ExitFrame = currentExitFrame;
                                overlappingWaypoint.ExitTime = exitTime.ToString(@"hh\:mm\:ss");
                                changed = true;
                            }

                            if (changed)
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    $"[Vehicle Waypoint Merge] instance={vehicleInstanceId}, range={overlappingWaypoint.EntryFrame}~{overlappingWaypoint.ExitFrame}");
                            }

                            if (!createdWaypoints.Contains(overlappingWaypoint))
                            {
                                createdWaypoints.Add(overlappingWaypoint);
                            }

                            continue;
                        }

                        var waypoint = new WaypointMarker
                        {
                            EntryFrame = currentEntryFrame,
                            ExitFrame = currentExitFrame,
                            MarkerColor = Color.FromArgb(107, 158, 255),
                            EntryTime = entryTime.ToString(@"hh\:mm\:ss"),
                            ExitTime = exitTime.ToString(@"hh\:mm\:ss"),
                            ObjectId = vehicleInstanceId,
                            Label = "vehicle"
                        };

                        waypointMarkers.Add(waypoint);
                        createdWaypoints.Add(waypoint);
                    }

                    WaypointNormalizer.NormalizeInPlace(waypointMarkers);

                    var eventBoxesInRange = boundingBoxes
                        .Where(b => b.Label == "event" &&
                                    b.FrameIndex >= entryFrameIndex.Value &&
                                    b.FrameIndex <= exitFrameIndex.Value)
                        .ToList();

                    var eventGroups = eventBoxesInRange
                        .GroupBy(b => string.IsNullOrWhiteSpace(b.EventInstanceId)
                            ? $"eid:{b.EventId}"
                            : $"eidinst:{b.EventInstanceId}")
                        .ToList();

                    foreach (var eventGroup in eventGroups)
                    {
                        var referenceBox = eventGroup.FirstOrDefault();
                        if (referenceBox == null) continue;

                        int eventId = referenceBox.EventId;
                        string eventInstanceId = referenceBox.EventInstanceId;
                        int minFrameIndex = eventGroup.Min(b => b.FrameIndex);

                        var overlappingWaypoint = WaypointOverlapHelper.FindOverlappingWaypoint(waypointMarkers, "event", eventId, currentEntryFrame, currentExitFrame, eventInstanceId);

                        if (overlappingWaypoint != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Event Waypoint Overlap] EventId={eventId}: existing({overlappingWaypoint.EntryFrame}~{overlappingWaypoint.ExitFrame}) vs requested({currentEntryFrame}~{currentExitFrame})");
                            continue;
                        }

                        var entryEventBox = eventGroup.FirstOrDefault(b => b.FrameIndex == minFrameIndex);
                        if (entryEventBox != null)
                        {
                            var evWp = new WaypointMarker
                            {
                                EntryFrame = minFrameIndex,
                                ExitFrame = exitFrameIndex.Value,
                                MarkerColor = Color.FromArgb(107, 255, 107),
                                EntryTime = TimeSpan.FromSeconds(minFrameIndex / fps).ToString(@"hh\:mm\:ss"),
                                ExitTime = exitTime.ToString(@"hh\:mm\:ss"),
                                ObjectId = eventId,
                                EventInstanceId = eventInstanceId,
                                Label = "event",
                                InteractingObject = ""
                            };
                            waypointMarkers.Add(evWp);
                            PropagateEventBoxWithinRange(entryEventBox, exitFrameIndex.Value);
                        }
                    }

                    SaveCurrentLabelingData();
                    UpdateWaypointListView();

                    entryFrameIndex = null;
                    exitFrameIndex = null;
                    btnEntry.Text = "Entry";
                    btnExit.Text = "Exit";
                    panelTimeline.Invalidate();

                    if (createdWaypoints.Count > 0)
                    {
                        int personWaypointCount = createdWaypoints.Count(w => w.Label == "person");
                        int vehicleWaypointCount = createdWaypoints.Count(w => w.Label == "vehicle");
                        int eventWaypointCount = createdWaypoints.Count(w => w.Label == "event");

                        string summary = $"{createdWaypoints.Count}개의 Waypoint가 생성되었습니다.\n" +
                                        $"(Person: {personWaypointCount}, Vehicle: {vehicleWaypointCount}, Event: {eventWaypointCount})";

                        var result = MessageBox.Show(
                            $"{summary}\n\n즉시 추적을 진행할까요?",
                            "Waypoint 생성 확인",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (result == DialogResult.Yes)
                        {
                            PerformSequentialTracking(createdWaypoints);
                        }
                    }
                }
                else
                {
                    MessageBox.Show("먼저 Entry 버튼을 눌러 시작 프레임을 지정해 주세요.", "안내", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                AppendFaceDebugLog("ExitMarkerError", ex.ToString());
                MessageBox.Show(
                    $"Exit 설정 중 오류 발생:\n{ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        #endregion
    }
}


