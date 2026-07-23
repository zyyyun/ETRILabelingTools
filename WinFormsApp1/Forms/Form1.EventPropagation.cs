using System;
using System.Drawing;
using System.Linq;

namespace WinFormsApp1
{
    public partial class Form1
    {
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
                            EventInstanceId = eventBox.EventInstanceId,
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
            if (box == null ||
                !string.Equals(box.Label, "vehicle", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(box.VehiclePartType, "plate", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int vehicleInstanceId = box.VehicleInstanceId > 0 ? box.VehicleInstanceId : box.VehicleId;
            bool alreadyExists = waypointMarkers.Any(waypoint =>
                string.Equals(waypoint.Label, "vehicle", StringComparison.OrdinalIgnoreCase) &&
                waypoint.ObjectId == vehicleInstanceId &&
                waypoint.EntryFrame <= box.FrameIndex &&
                waypoint.ExitFrame >= box.FrameIndex);

            if (alreadyExists)
            {
                return;
            }

            TimeSpan time = TimeSpan.FromSeconds(box.FrameIndex / fps);
            string timeString = time.ToString(@"hh\:mm\:ss");

            var waypoint = new WaypointMarker
            {
                EntryFrame = box.FrameIndex,
                ExitFrame = box.FrameIndex,
                MarkerColor = System.Drawing.Color.FromArgb(107, 158, 255),
                EntryTime = timeString,
                ExitTime = timeString,
                ObjectId = vehicleInstanceId,
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
            if (box?.Label == "event")
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Event Finalization Guard] Ignored eager CreateEventWaypoint call for frame {box.FrameIndex}, instance={box.EventInstanceId}");
            }
            return;

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
                EventInstanceId = box.EventInstanceId,
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

            var finalizedWaypoint = FindWaypointForBox(box);
            if (finalizedWaypoint == null || !string.Equals(finalizedWaypoint.Label, "event", StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Event Finalization Guard] Skipped PropagateEventBoxToEnd without a finalized event waypoint. frame={box.FrameIndex}, instance={box.EventInstanceId}");
                return;
            }

            PropagateEventBoxWithinRange(box, finalizedWaypoint.ExitFrame);
            return;

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
                        EventInstanceId = box.EventInstanceId,
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

            var finalizedWaypoint = FindWaypointForBox(box);
            if (finalizedWaypoint == null || !string.Equals(finalizedWaypoint.Label, "event", StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Event Lifetime Guard] Skipped PropagateEventBoxWithinRange without a finalized event waypoint. frame={box.FrameIndex}, instance={box.EventInstanceId}");
                return;
            }

            endFrame = Math.Min(endFrame, finalizedWaypoint.ExitFrame);
            int startFrame = box.FrameIndex + 1;
            if (startFrame > endFrame) return;

            for (int frame = startFrame; frame <= endFrame; frame++)
            {
                bool exists = boundingBoxes.Any(b =>
                    b.FrameIndex == frame &&
                    b.Label == "event" &&
                    !b.IsDeleted &&
                    string.Equals(b.EventInstanceId, box.EventInstanceId, StringComparison.Ordinal));

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
                        EventInstanceId = box.EventInstanceId,
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

            var scopedEventWaypoint = FindWaypointForBox(box);
            if (scopedEventWaypoint != null)
            {
                var scopedBoxesToRemove = EventFinalizationHelper.GetEventBoxesForWaypoint(
                    boundingBoxes,
                    scopedEventWaypoint,
                    startFrame: box.FrameIndex + 1);

                foreach (var boxToRemove in scopedBoxesToRemove)
                {
                    boundingBoxes.Remove(boxToRemove);
                }

                if (scopedBoxesToRemove.Count > 0)
                {
                    waypointMarkers.Remove(scopedEventWaypoint);
                    InvalidateBoxCache();
                    UpdateBoxCount();
                    UpdateBboxListDisplay();
                    UpdateWaypointListView();
                    panelTimeline.Invalidate();
                    System.Diagnostics.Debug.WriteLine($"[Event Lifetime Guard] Removed {scopedBoxesToRemove.Count} propagated boxes for instance {scopedEventWaypoint.EventInstanceId}");
                }

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
            if (box?.Label == "event")
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Event Lifetime Guard] Manual-tracking-first mode: PropagateEventBoxIfNeeded skipped for instance={box.EventInstanceId}, frame={box.FrameIndex}");
                return;
            }
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
                        EventInstanceId = box.EventInstanceId,
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
        private void PropagateEventBoxFromCurrentFrame(BoundingBox box, Rectangle sourceBefore)
        {
            if (box == null || !string.Equals(box.Label, "event", StringComparison.OrdinalIgnoreCase))
                return;

            var plan = EventWaypointBoxPropagationHelper.PlanPropagation(
                box, boundingBoxes, waypointMarkers, manuallyAdjustedFrames);
            if (plan.Updates.Count == 0 && plan.Additions.Count == 0)
                return;

            var batch = EventRectanglePropagationUndoHelper.CreateBatch(box, sourceBefore, plan);
            EventRectanglePropagationUndoHelper.ApplyForward(boundingBoxes, batch);
            AddUndoAction(new UndoAction
            {
                Type = UndoActionType.EventRectanglePropagation,
                EventRectanglePropagation = batch
            });

            InvalidateBoxCache();
            UpdateBoxCount();
            UpdateBboxListDisplay();
            return;

            if (box?.Label == "event")
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Event Lifetime Guard] Manual-tracking-first mode: PropagateEventBoxFromCurrentFrame skipped for instance={box.EventInstanceId}, frame={box.FrameIndex}");
                return;
            }
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
                            EventInstanceId = box.EventInstanceId,
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
    }
}
