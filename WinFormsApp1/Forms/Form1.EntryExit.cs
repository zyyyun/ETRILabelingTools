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
            // E?§Ï? ?ôÏùº??Í∏∞Îä•: Entry ÎßàÏª§ ?§Ï†ï
            SetEntryMarker();
        }

        private void SetEntryMarker()
        {
            // Í∞ùÏ≤¥ ?†ÌÉù ?ÜÏù¥??Entry ?ÑÎ†à???§Ï†ï Í∞Ä??
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
                    System.Diagnostics.Debug.WriteLine("[Exit] YOLO ¿€æ˜ ¡¯«‡ ¡ﬂ: Exit µø¿€ ∫Ò»∞º∫»≠");
                    MessageBox.Show(
                        "YOLO √ﬂ¿˚ ∂«¥¬ ≈Ω¡ˆ∞° ¡¯«‡ ¡ﬂ¿‘¥œ¥Ÿ.\n¿€æ˜¿Ã øœ∑·µ… ∂ß±Ó¡ˆ ±‚¥Ÿ∑¡ ¡÷ººø‰.",
                        "¿€æ˜ ¡ﬂ",
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
                    $"Exit º≥¡§ ¡ﬂ ø¿∑˘ πﬂª˝:\n{ex.Message}",
                    "ø¿∑˘",
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
                        "Face ∆Æ∑°≈∑¿∫ Exit ∞Ê∑Œ(∂«¥¬ πˆ∆∞/¥‹√‡≈∞)∑Œ øœ∑·«œ¡ˆ æ Ω¿¥œ¥Ÿ.\n\n" +
                        "Face¥¬ R∑Œ a«¡∑π¿”¿ª º≥¡§«— µ⁄ Shift+T∑Œ ∫∏∞£«œººø‰.",
                        "æÀ∏≤",
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
                            $"Exit «¡∑π¿”¿∫ Entry «¡∑π¿” ¿Ã»ƒø©æﬂ «’¥œ¥Ÿ.\n\n" +
                            $"Entry: {entryTimeCheck:hh\\:mm\\:ss} (frame {entryFrameIndex.Value})\n" +
                            $"«ˆ¿Á: {currentTimeCheck:hh\\:mm\\:ss} (frame {currentFrameIndex})\n\n" +
                            "Entry∫∏¥Ÿ ∏’¿˙ Exit¿ª º≥¡§«“ ºˆ æ¯Ω¿¥œ¥Ÿ.",
                            "∞Ê∞Ì",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    var entryPersonBoxes = boundingBoxes.Where(b => b.FrameIndex == entryFrameIndex.Value && b.Label == "person").ToList();
                    var entryVehicleBoxes = boundingBoxes.Where(b => b.FrameIndex == entryFrameIndex.Value && b.Label == "vehicle").ToList();
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
                                "Entryø°º≠ Person, Vehicle, Event π⁄Ω∫ ¡ﬂ «œ≥™∞° ¿÷æÓæﬂ «’¥œ¥Ÿ.\n" +
                                "∂«¥¬ Entry~Exit ±∏∞£ø° Event π⁄Ω∫∞° ¿÷æÓæﬂ «’¥œ¥Ÿ.\n" +
                                "»Æ¿Œ »ƒ ¥ŸΩ√ Ω√µµ«ÿ ¡÷ººø‰.",
                                "∞Ê∞Ì",
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
                                    "Exit «¡∑π¿”¿∫ ±‚¡∏ Waypoint¿« Exit «¡∑π¿”∫∏¥Ÿ µ⁄∑Œ ø¨¿Â«“ ºˆ æ¯Ω¿¥œ¥Ÿ.\n\n" +
                                    $"∞¥√º: {GetCategoryName("person", personId)}\n" +
                                    $"±‚¡∏ Waypoint: Entry={TimeSpan.FromSeconds(overlappingWaypoint.EntryFrame / fps):hh\\:mm\\:ss}, Exit={oldExitTime:hh\\:mm\\:ss}\n" +
                                    $"ø‰√ª Exit: {currentExitTime:hh\\:mm\\:ss}\n\n" +
                                    "¡∏¿Á«œ¥¬ ±∏∞£¿ª »Æ¿Œ«œ∞Ì ¥ŸΩ√ º≥¡§«ÿ ¡÷ººø‰.",
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
                        int vehicleId = vehicleBox.VehicleId;

                        var overlappingWaypoint = WaypointOverlapHelper.FindOverlappingWaypoint(waypointMarkers, "vehicle", vehicleId, currentEntryFrame, currentExitFrame);

                        if (overlappingWaypoint != null)
                        {
                            if (currentExitFrame > overlappingWaypoint.ExitFrame)
                            {
                                TimeSpan currentExitTime = TimeSpan.FromSeconds(currentExitFrame / fps);
                                TimeSpan oldExitTime = TimeSpan.FromSeconds(overlappingWaypoint.ExitFrame / fps);
                                MessageBox.Show(
                                    "Exit «¡∑π¿”¿∫ ±‚¡∏ Waypoint¿« Exit «¡∑π¿”∫∏¥Ÿ µ⁄∑Œ ø¨¿Â«“ ºˆ æ¯Ω¿¥œ¥Ÿ.\n\n" +
                                    $"∞¥√º: {GetCategoryName("vehicle", vehicleId)}\n" +
                                    $"±‚¡∏ Waypoint: Entry={TimeSpan.FromSeconds(overlappingWaypoint.EntryFrame / fps):hh\\:mm\\:ss}, Exit={oldExitTime:hh\\:mm\\:ss}\n" +
                                    $"ø‰√ª Exit: {currentExitTime:hh\\:mm\\:ss}\n\n" +
                                    "¡∏¿Á«œ¥¬ ±∏∞£¿ª »Æ¿Œ«œ∞Ì ¥ŸΩ√ º≥¡§«ÿ ¡÷ººø‰.",
                                    "Warning",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                                continue;
                            }

                            System.Diagnostics.Debug.WriteLine($"[Vehicle Waypoint Overlap] VehicleId={vehicleId}: existing({overlappingWaypoint.EntryFrame}~{overlappingWaypoint.ExitFrame}) vs requested({currentEntryFrame}~{currentExitFrame})");
                            continue;
                        }

                        var waypoint = new WaypointMarker
                        {
                            EntryFrame = entryFrameIndex.Value,
                            ExitFrame = exitFrameIndex.Value,
                            MarkerColor = Color.FromArgb(107, 158, 255),
                            EntryTime = entryTime.ToString(@"hh\:mm\:ss"),
                            ExitTime = exitTime.ToString(@"hh\:mm\:ss"),
                            ObjectId = vehicleId,
                            Label = "vehicle"
                        };

                        waypointMarkers.Add(waypoint);
                        createdWaypoints.Add(waypoint);
                    }

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

                        string summary = $"{createdWaypoints.Count}∞≥¿« Waypoint∞° ª˝º∫µ«æ˙Ω¿¥œ¥Ÿ.\n" +
                                        $"(Person: {personWaypointCount}, Vehicle: {vehicleWaypointCount}, Event: {eventWaypointCount})";

                        var result = MessageBox.Show(
                            $"{summary}\n\n¡ÔΩ√ √ﬂ¿˚¿ª ¡¯«‡«“±Óø‰?",
                            "Waypoint ª˝º∫ »Æ¿Œ",
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
                    MessageBox.Show("∏’¿˙ Entry πˆ∆∞¿ª ¥≠∑Ø Ω√¿€ «¡∑π¿”¿ª ¡ˆ¡§«ÿ ¡÷ººø‰.", "æ»≥ª", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                AppendFaceDebugLog("ExitMarkerError", ex.ToString());
                MessageBox.Show(
                    $"Exit º≥¡§ ¡ﬂ ø¿∑˘ πﬂª˝:\n{ex.Message}",
                    "ø¿∑˘",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        #endregion
    }
}


