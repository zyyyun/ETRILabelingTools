using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace WinFormsApp1
{
    public partial class Form1
    {
        private bool IsFaceBox(BoundingBox box)
        {
            return box != null &&
                box.Label == "person" &&
                string.Equals(box.PersonPartType, "face", StringComparison.OrdinalIgnoreCase);
        }

        private int GetTrackedPersonIdentity(BoundingBox box)
        {
            if (box == null)
                return 0;

            if (!IsFaceBox(box))
                return box.PersonId;

            return box.LinkedPersonId.GetValueOrDefault(box.PersonId);
        }

        private bool IsPlateBox(BoundingBox box)
        {
            return box != null &&
                box.Label == "vehicle" &&
                string.Equals(box.VehiclePartType, "plate", StringComparison.OrdinalIgnoreCase);
        }

        private int GetTrackedVehicleIdentity(BoundingBox box)
        {
            if (box == null)
                return 0;

            if (!IsPlateBox(box))
                return box.VehicleInstanceId > 0 ? box.VehicleInstanceId : box.VehicleId;

            return box.LinkedVehicleInstanceId.GetValueOrDefault(
                box.VehicleInstanceId > 0 ? box.VehicleInstanceId : box.VehicleId);
        }

        private string GetTrackedBoxKey(BoundingBox box)
        {
            if (box == null)
                return $"{box?.Label}_unknown";

            if (IsFaceBox(box))
            {
                int linkedPersonId = GetTrackedPersonIdentity(box);
                return $"{box.Label}_face_{linkedPersonId}";
            }

            if (IsPlateBox(box))
            {
                int linkedVehicleInstanceId = GetTrackedVehicleIdentity(box);
                return $"{box.Label}_plate_{linkedVehicleInstanceId}";
            }

            return $"{box.Label}_{GetBoxId(box)}";
        }

        private bool IsSameTrackedBox(BoundingBox templateBox, BoundingBox candidateBox)
        {
            if (templateBox == null || candidateBox == null)
                return false;

            if (templateBox.Label != candidateBox.Label)
                return false;

            if (templateBox.Label == "person")
            {
                if (IsFaceBox(templateBox) || IsFaceBox(candidateBox))
                {
                    return IsFaceBox(templateBox) && IsFaceBox(candidateBox) &&
                           GetTrackedPersonIdentity(templateBox) == GetTrackedPersonIdentity(candidateBox);
                }

                return templateBox.PersonId == candidateBox.PersonId;
            }

            if (templateBox.Label == "vehicle")
            {                if (IsPlateBox(templateBox) || IsPlateBox(candidateBox))
                {
                    return IsPlateBox(templateBox) && IsPlateBox(candidateBox) &&
                           TrackingIdentityHelper.AreSameTrackingTarget(templateBox, candidateBox);
                }

                return TrackingIdentityHelper.AreSameTrackingTarget(templateBox, candidateBox);
            }

            if (templateBox.Label == "event")
            {
                return templateBox.EventId == candidateBox.EventId;
            }

            return false;
        }

        private bool IsWaypointTrackingMatch(BoundingBox box, WaypointMarker waypoint)
        {
            return TrackingIdentityHelper.MatchesWaypoint(box, waypoint);
        }

        private string GetTrackingIdentityKey(WaypointMarker waypoint, BoundingBox templateBox = null)
        {
            if (waypoint == null) return "unknown_unknown";
            if (string.Equals(waypoint.Label, "event", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(waypoint.EventInstanceId))
                return string.Format("event_instance_{0}", waypoint.EventInstanceId);
            return string.Format("{0}_{1}", waypoint.Label, waypoint.ObjectId);
        }

        private bool IsTrackingBoxMatch(BoundingBox box, WaypointMarker waypoint, BoundingBox templateBox = null)
        {
            if (box == null || waypoint == null || !string.Equals(box.Label, waypoint.Label, StringComparison.OrdinalIgnoreCase))
                return false;
            if (templateBox != null)
                return IsSameTrackedBox(templateBox, box);
            return TrackingIdentityHelper.MatchesWaypoint(box, waypoint);
        }

        private void RunAutoInterpolationForFace(BoundingBox selectedBox)
        {
            if (!IsFaceBox(selectedBox))
                return;

            if (!selectedBox.BoxEntryFrame.HasValue || !selectedBox.BoxExitFrame.HasValue)
            {
                MessageBox.Show(
                    "Face 박스의 입장/이탈 프레임이 설정되지 않았습니다.",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            int entryFrame = selectedBox.BoxEntryFrame.Value;
            int exitFrame = selectedBox.BoxExitFrame.Value;

            if (exitFrame <= entryFrame)
            {
                MessageBox.Show(
                    "Exit 프레임은 Entry 프레임보다 커야 합니다.",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var startBox = boundingBoxes.FirstOrDefault(b =>
                b.FrameIndex == entryFrame &&
                !b.IsDeleted &&
                IsSameTrackedBox(selectedBox, b));

            if (startBox == null)
            {
                MessageBox.Show(
                    "Face 박스의 시작 프레임에 대응하는 박스를 찾을 수 없습니다.",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            PerformForcedInertiaTracking(startBox, entryFrame, exitFrame);
        }

        #region Forced Inertia Tracking (Shift+T)

        /// <summary>
        /// 강제 관성 추적: a 프레임부터 b 프레임까지 수동 수정 프레임을 기준으로 보간
        /// </summary>
        private void PerformForcedInertiaTracking(
            BoundingBox selectedBox,
            int aFrame,
            int bFrame)
        {
            if (selectedBox == null || aFrame >= bFrame)
            {
                MessageBox.Show("잘못된 프레임 범위입니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

selectedBox = FindVehicleBodyForTracking(selectedBox);
            string key = GetTrackedBoxKey(selectedBox);

            // a 프레임의 박스 찾기
            var boxA = boundingBoxes.FirstOrDefault(b =>
                b.FrameIndex == aFrame &&
                !b.IsDeleted &&
                IsSameTrackedBox(selectedBox, b));

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
                            !b.IsDeleted &&
                            IsSameTrackedBox(selectedBox, b));

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
                !b.IsDeleted &&
                IsSameTrackedBox(selectedBox, b));

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
        private void PerformForcedPlateTracking(BoundingBox selectedPlate, int aFrame, int bFrame)
        {
            if (selectedPlate == null || aFrame >= bFrame)
            {
                MessageBox.Show("잘못된 번호판 프레임 범위입니다.", "번호판 추적", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var plateA = FindPlateAtFrame(aFrame, selectedPlate);
            if (plateA == null)
            {
                MessageBox.Show("a 프레임에서 선택한 번호판 박스를 찾을 수 없습니다.", "번호판 추적", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string key = GetTrackedBoxKey(selectedPlate);
            var successFrames = new Dictionary<int, Rectangle>
            {
                [aFrame] = plateA.Rectangle
            };

            if (manuallyAdjustedFrames.ContainsKey(key))
            {
                foreach (int adjustedFrame in manuallyAdjustedFrames[key])
                {
                    if (adjustedFrame > aFrame && adjustedFrame <= bFrame)
                    {
                        var adjustedPlate = FindPlateAtFrame(adjustedFrame, selectedPlate);
                        if (adjustedPlate != null)
                        {
                            successFrames[adjustedFrame] = adjustedPlate.Rectangle;
                        }
                    }
                }
            }

            var plateB = FindPlateAtFrame(bFrame, selectedPlate);
            if (plateB != null)
            {
                successFrames[bFrame] = plateB.Rectangle;
            }
            else
            {
                plateB = CloneBoundingBox(plateA);
                plateB.FrameIndex = bFrame;
                plateB.Action = "waypoint";
                boundingBoxes.Add(plateB);
                successFrames[bFrame] = plateB.Rectangle;
            }

            var sortedSuccessFrames = successFrames.Keys.OrderBy(frame => frame).ToList();
            var boxesToModify = new List<BoundingBox>();
            for (int frame = aFrame + 1; frame < bFrame; frame++)
            {
                if (!successFrames.ContainsKey(frame))
                {
                    boxesToModify.Add(FindOrCreatePlateAtFrame(frame, selectedPlate));
                }
            }

            if (boxesToModify.Count > 0)
            {
                AddUndoAction(new UndoAction
                {
                    Type = UndoActionType.ModifyBox,
                    Box = CloneBoundingBox(boxesToModify[0]),
                    TrackedBoxes = boxesToModify.Select(CloneBoundingBox).ToList()
                });
            }

            int interpolatedCount = 0;
            for (int frame = aFrame + 1; frame < bFrame; frame++)
            {
                if (successFrames.ContainsKey(frame))
                {
                    continue;
                }

                var plate = FindOrCreatePlateAtFrame(frame, selectedPlate);
                int? previous = FindPreviousSuccessFrame(frame, sortedSuccessFrames);
                int? next = FindNextSuccessFrame(frame, sortedSuccessFrames);
                if (previous.HasValue && next.HasValue)
                {
                    plate.Rectangle = InterpolateRect(
                        successFrames[previous.Value],
                        successFrames[next.Value],
                        previous.Value,
                        next.Value,
                        frame);
                    interpolatedCount++;
                }
                else if (previous.HasValue)
                {
                    plate.Rectangle = successFrames[previous.Value];
                    interpolatedCount++;
                }
                else if (next.HasValue)
                {
                    plate.Rectangle = successFrames[next.Value];
                    interpolatedCount++;
                }
            }

            InvalidateBoxCache();
            UpdateBoxCount();
            UpdateBboxListDisplay();
            pictureBoxVideo.Invalidate();
            MessageBox.Show(
                $"번호판 독립 관성 추적이 완료되었습니다.\n\n범위: {aFrame} ~ {bFrame}\n보간된 번호판 박스: {interpolatedCount}",
                "번호판 추적",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private BoundingBox FindOrCreatePlateAtFrame(int frameIndex, BoundingBox templatePlate)
        {
            var existing = FindPlateAtFrame(frameIndex, templatePlate);
            if (existing != null)
            {
                return existing;
            }

            var created = CloneBoundingBox(templatePlate);
            created.FrameIndex = frameIndex;
            created.Action = "waypoint";
            boundingBoxes.Add(created);
            return created;
        }
        private BoundingBox FindPlateAtFrame(int frameIndex, BoundingBox referencePlate)
        {
            return TrackingIdentityHelper.FindPlateAtFrame(boundingBoxes, frameIndex, referencePlate);
        }

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
            // 먼저 기존 박스 찾기
            var existing = boundingBoxes.FirstOrDefault(b =>
                b.FrameIndex == frameIndex &&
                !b.IsDeleted &&
                IsSameTrackedBox(templateBox, b));

            if (existing != null)
                return existing;

            // 없으면 생성
            var newBox = new BoundingBox
            {
                FrameIndex = frameIndex,
                Label = templateBox.Label,
                PersonId = templateBox.PersonId,
                VehicleId = templateBox.VehicleId,
                VehicleInstanceId = templateBox.VehicleInstanceId,
                LinkedVehicleInstanceId = templateBox.LinkedVehicleInstanceId,
                VehiclePartType = templateBox.VehiclePartType,
                VehicleName = templateBox.VehicleName,
                EventId = templateBox.EventId,
                EventInstanceId = templateBox.EventInstanceId,
                PersonPartType = templateBox.PersonPartType,
                LinkedPersonId = templateBox.LinkedPersonId,
                BoxEntryFrame = templateBox.BoxEntryFrame,
                BoxExitFrame = templateBox.BoxExitFrame,
                Rectangle = templateBox.Rectangle, // 임시값, 보간으로 업데이트됨
                Action = "waypoint"
            };

            boundingBoxes.Add(newBox);
            return newBox;
        }



        #region Disappearance Handling

        /// <summary>
        /// 특정 프레임에서 사라짐 구간 처리 (재추적 시작 시점에서 호출)
        /// </summary>
        private void ProcessDisappearedRangesAtFrame(WaypointMarker waypoint, int returnFrame)
        {
            int boxId = waypoint.ObjectId;
            string key = GetTrackingIdentityKey(waypoint);

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
            string key = GetTrackingIdentityKey(waypoint);

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
            string key = GetTrackingIdentityKey(waypoint);

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

            if (string.Equals(waypoint.Label, "event", StringComparison.OrdinalIgnoreCase))
            {
                var eventBoxesToDelete = EventFinalizationHelper.GetEventBoxesForWaypoint(
                    boundingBoxes,
                    waypoint,
                    startFrame: startFrame)
                    .Where(box => box.FrameIndex <= endFrame)
                    .ToList();

                foreach (var box in eventBoxesToDelete)
                {
                    boundingBoxes.Remove(box);
                    deletedCount++;
                }

                if (deletedCount > 0)
                {
                    InvalidateBoxCache();
                    UpdateBoxCount();
                    UpdateBboxListDisplay();
                    System.Diagnostics.Debug.WriteLine($"[Event 박스 정리] {key}: 프레임 {startFrame}~{endFrame}에서 {deletedCount}개 박스 제거");
                }

                return;
            }

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

        // ? 여러 Waypoint를 순차적으로 추적 (동시 실행 방지)
        private async void PerformSequentialTracking(List<WaypointMarker> waypoints)
        {
            // ? waypoint가 비어있으면 추적하지 않음
            if (waypoints == null || waypoints.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[순차 추적] 추적할 waypoint가 없습니다.");
                return;
            }

            // ? 순차 추적 시작 시 플래그 설정
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
                        // ? 각 Waypoint를 순차적으로 추적 (await으로 대기)
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

                // ? 모든 추적 완료 후 JSON 저장 및 재로드 (한 번만)
                if (!string.IsNullOrEmpty(currentVideoFile))
                {
                    string videoDir = Path.GetDirectoryName(currentVideoFile);
                    string saveDir = Path.Combine(videoDir, "labels");

                    if (!Directory.Exists(saveDir))
                    {
                        Directory.CreateDirectory(saveDir);
                    }

                    // ? 기존에 로드된 파일에 저장
                    string jsonFilePath;
                    if (!string.IsNullOrEmpty(currentJsonFile) && File.Exists(currentJsonFile))
                    {
                        jsonFilePath = currentJsonFile;
                    }
                    else
                    {
                        string fileName = Path.GetFileNameWithoutExtension(currentVideoFile) + "_labels.json";
                        jsonFilePath = Path.Combine(saveDir, fileName);
                        currentJsonFile = jsonFilePath;
                    }

                    // JSON 저장
                    await Task.Run(() => ExportToJsonExtended(jsonFilePath));

                    // JSON 재로드하여 추적 데이터 기반으로 표시
                    if (File.Exists(jsonFilePath))
                    {
                        await LoadLabelingData(currentVideoFile);
                    }

                    MessageBox.Show(
                        $"? {totalCount}개 Waypoint 추적 완료!\n\n" +
                        $"총 {totalBoxesAdded}개 BBox 추가됨\n" +
                        $"?? JSON 저장 및 재로드 완료\n\n" +
                        $"저장 위치: {jsonFilePath}",
                        "추적 완료",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"? {totalCount}개 Waypoint 추적 완료!\n\n" +
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
                // ? 순차 추적 종료 시 플래그 해제 (예외 발생 시에도 반드시 해제)
                isTrackingInProgress = false;
                System.Diagnostics.Debug.WriteLine("[순차 추적 종료] isTrackingInProgress = false");
            }
        }

        private async Task PerformTrackingForWaypointAsync(WaypointMarker waypoint, bool useYolo = false, BoundingBox templateBox = null)
        {
            try
            {
                // ? 추적 중에는 다른 추적 작업 차단 (단순 탐지는 차단하지 않음)
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
                    // ?? early return 시에도 호출자에게 알려야 하지만,
                    // PerformTrackingForWaypointAsync는 void Task이므로 예외를 throw해야 함
                    throw new InvalidOperationException("추적이 진행 중이어서 새 추적을 시작할 수 없습니다.");
                }

                // ? Entry 프레임에서 waypoint의 ObjectId와 Label에 해당하는 박스만 찾기
                List<BoundingBox> startBoxes = new List<BoundingBox>();

                if (waypoint.Label == "person" || waypoint.Label == "vehicle" || waypoint.Label == "event")
                {
                    startBoxes = boundingBoxes
                        .Where(b => b.FrameIndex == waypoint.EntryFrame &&
                                   !b.IsDeleted &&
                                   !(waypoint.Label == "vehicle" && TrackingIdentityHelper.IsPlate(b)) &&
                                   TrackingIdentityHelper.MatchesWaypoint(b, waypoint))
                        .ToList();
                }

                System.Diagnostics.Debug.WriteLine($"[추적 시작] {waypoint.Label} ID={waypoint.ObjectId}, startBoxes={startBoxes.Count}");

                if (startBoxes.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[추적 스킵] {waypoint.Label} ID={waypoint.ObjectId} - Entry 프레임에 박스 없음");
                    return;
                }

                // ? Event는 자동 추적 안함 (전파 기능만 사용)
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
                        // ? 실패 구간을 받아오는 오버로드 메서드 호출
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

                            // ? 실패 구간 저장
                            string key = $"{waypoint.Label}_{waypoint.ObjectId}";
                            if (result.Failures != null && result.Failures.Count > 0)
                            {
                                waypointFailureRanges[key] = result.Failures;
                                System.Diagnostics.Debug.WriteLine($"[실패 구간 저장] {key}: {result.Failures.Count}개 구간");
                            }

                            // ? YOLO 추적 완료 시 waypoint 구간 동안 탐지한 객체 종류별 로그 출력
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

                // ? 추적 완료 후 사라짐 구간 처리
                ProcessDisappearedRanges(waypoint);

                // ? 추적 완료 후 연속 박스 부재 구간 자동 감지
                DetectContinuousAbsence(waypoint);

                UpdateBoxCount();
                UpdateBboxListDisplay();

                // ? 개별 waypoint 추적 완료 로그
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

        // ? 부분 재추적 함수 (특정 프레임부터 Exit까지)
        private async Task PerformPartialRetrackingAsync(WaypointMarker waypoint, int startFrame, BoundingBox templateBox = null)
        {
            // ? 추적 시작 시 플래그 설정
            isTrackingInProgress = true;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[부분 재추적 시작] {waypoint.Label} ID={waypoint.ObjectId}, Frame {startFrame}~{waypoint.ExitFrame}");

                // ? 1. 먼저 startBox 찾기 (삭제 전에 찾아야 함)
                string key = $"{waypoint.Label}_{waypoint.ObjectId}";

                // ? 재추적 시작 시 해당 객체의 실패 구간 정보 초기화
                if (waypointFailureRanges.ContainsKey(key))
                {
                    waypointFailureRanges.Remove(key);
                    System.Diagnostics.Debug.WriteLine($"[재추적] {key}의 실패 구간 정보 초기화됨");
                }

                // ? 재추적 시작 시점에서 사라짐 구간 처리 (복귀 시점으로 간주)
                // 현재 프레임(startFrame)이 복귀 시점(b)일 수 있으므로, 이전 사라짐 구간 확정
                ProcessDisappearedRangesAtFrame(waypoint, startFrame);

                // ? 2. 현재 프레임의 박스를 startBox로 사용 (삭제 전에 찾아야 함)
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
                        IsTrackingBoxMatch(b, waypoint, templateBox));
                }

                if (startBox == null)
                {
                    MessageBox.Show($"Frame {startFrame}에 해당하는 박스를 찾을 수 없습니다.\n\n박스를 선택한 후 재추적을 실행해주세요.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // ? 3. 기존 데이터 삭제 (재추적 범위) - startBox는 제외
                int removedCount = 0;

                // startFrame부터 waypoint.ExitFrame까지의 기존 박스 삭제 (startBox 제외)
                var boxesToRemove = boundingBoxes.Where(b =>
                    IsTrackingBoxMatch(b, waypoint, templateBox) &&
                    b.FrameIndex >= startFrame &&
                    b.FrameIndex <= waypoint.ExitFrame &&
                    b != startBox).ToList(); // startBox는 삭제하지 않음

                foreach (var box in boxesToRemove)
                {
                    boundingBoxes.Remove(box);
                    removedCount++;
                }

                System.Diagnostics.Debug.WriteLine($"[부분 재추적] {removedCount}개 기존 박스 삭제됨");

                // ? 4. 재추적 수행
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
                int inertiaAppliedFrames = 0; // ? 관성 추적 적용된 프레임 수

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

                    // ? 실패 구간 업데이트
                    if (result.Failures != null && result.Failures.Count > 0)
                    {
                        waypointFailureRanges[key] = result.Failures;
                        System.Diagnostics.Debug.WriteLine($"[재추적 실패 구간] {key}: {result.Failures.Count}개 구간");
                    }

                    // ? 재추적 완료 시 waypoint 구간 동안 탐지한 객체 종류별 로그 출력
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

                // ? 5. 새 데이터 추가 전 중복 제거 확인
                int duplicateCount = 0;
                var framesToCheck = newTrackedBoxes.Select(b => b.FrameIndex).Distinct().ToList();

                // 새로 추가할 박스의 프레임에 이미 존재하는 동일한 객체 박스 제거
                foreach (var frame in framesToCheck)
                {
                    var existingBoxes = boundingBoxes.Where(b =>
                        b.FrameIndex == frame &&
                        IsTrackingBoxMatch(b, waypoint, templateBox)).ToList();

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

                // ? 6. 새 데이터 추가
                foreach (var box in newTrackedBoxes)
                {
                    boundingBoxes.Add(box);
                }

                // ? 7. 정렬
                boundingBoxes.Sort((a, b) => a.FrameIndex.CompareTo(b.FrameIndex));

                InvalidateBoxCache();
                UpdateBoxCount();
                UpdateBboxListDisplay();

                // ? 8. 재추적 완료 후 전체 범위(EntryFrame~ExitFrame)에서 재보간 수행
                // 재추적 이전 마지막 성공 프레임과 재추적 후 첫 성공 프레임 사이도 보간하기 위함
                int additionalInterpolatedFrames = await Task.Run(() =>
                {
                    return ApplyInertialInterpolationForWaypoint(waypoint, startFrame, templateBox);
                });

                System.Diagnostics.Debug.WriteLine($"[부분 재추적 완료] {waypoint.Label} ID={waypoint.ObjectId}, {newTrackedBoxes.Count}개 박스 추가됨, 재추적 범위 보간: {inertiaAppliedFrames}개, 전체 범위 재보간: {additionalInterpolatedFrames}개");

                // ? 재보간 후 UI 업데이트
                if (additionalInterpolatedFrames > 0)
                {
                    InvalidateBoxCache();
                    UpdateBoxCount();
                    UpdateBboxListDisplay();
                }

                // ? 9. 현재 프레임 새로고침
                pictureBoxVideo.Invalidate();

                // ? 10. JSON 자동 저장
                if (!string.IsNullOrEmpty(currentVideoFile))
                {
                    string videoDir = Path.GetDirectoryName(currentVideoFile);
                    string saveDir = Path.Combine(videoDir, "labels");

                    if (!Directory.Exists(saveDir))
                    {
                        Directory.CreateDirectory(saveDir);
                    }

                    // ? 기존에 로드된 파일에 저장
                    string jsonFilePath;
                    if (!string.IsNullOrEmpty(currentJsonFile) && File.Exists(currentJsonFile))
                    {
                        jsonFilePath = currentJsonFile;
                    }
                    else
                    {
                        string fileName = Path.GetFileNameWithoutExtension(currentVideoFile) + "_labels.json";
                        jsonFilePath = Path.Combine(saveDir, fileName);
                        currentJsonFile = jsonFilePath;
                    }

                    // JSON 저장 (재로드하지 않음 - 메모리 상태가 이미 최신)
                    await Task.Run(() => ExportToJsonExtended(jsonFilePath));
                }

                int totalInterpolated = inertiaAppliedFrames + additionalInterpolatedFrames;

                // ? 재추적 완료 후 사라짐 구간 처리
                ProcessDisappearedRanges(waypoint);

                // ? 재추적 완료 후 연속 박스 부재 구간 자동 감지
                DetectContinuousAbsence(waypoint);

                MessageBox.Show($"재추적이 완료되었습니다.\n추가된 박스: {newTrackedBoxes.Count}개\n\n관성 보간:\n- 재추적 범위: {inertiaAppliedFrames}개 프레임\n- 전체 범위 재보간: {additionalInterpolatedFrames}개 프레임\n- 총 보간: {totalInterpolated}개 프레임\n\nJSON 저장이 완료되었습니다.", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[부분 재추적 오류] {waypoint.Label} ID={waypoint.ObjectId}: {ex.Message}");
                MessageBox.Show($"재추적 중 오류가 발생했습니다.\n\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // ? 추적 종료 시 플래그 해제
                isTrackingInProgress = false;
            }
        }

        // ? 재추적 완료 후 전체 범위에서 재보간 수행
        private int ApplyInertialInterpolationForWaypoint(WaypointMarker waypoint, int retrackingStartFrame, BoundingBox templateBox = null)
        {
            int interpolatedCount = 0;

            try
            {
                // waypoint의 전체 범위에서 해당 객체의 모든 박스 찾기
                List<BoundingBox> allBoxes = boundingBoxes
                    .Where(b => b.FrameIndex >= waypoint.EntryFrame &&
                               b.FrameIndex <= waypoint.ExitFrame &&
                               IsTrackingBoxMatch(b, waypoint, templateBox))
                    .OrderBy(b => b.FrameIndex)
                    .ToList();

                if (allBoxes.Count == 0)
                    return 0;

                // ? 성공 프레임 찾기: 연속된 프레임에서 위치가 변경된 프레임 = Detection 성공 프레임
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

                // ? 성공 프레임 목록 정렬
                var sortedSuccessFrames = successfulFrames.Keys.OrderBy(f => f).ToList();

                System.Diagnostics.Debug.WriteLine($"[전체 범위 재보간] {waypoint.Label} ID={waypoint.ObjectId}: EntryFrame={waypoint.EntryFrame}~ExitFrame={waypoint.ExitFrame}, 성공 프레임 {successfulFrames.Count}개");

                // ? 각 실패 프레임에 대해 보간 적용
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

        #endregion
    }
}





