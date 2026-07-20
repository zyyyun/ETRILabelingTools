using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace WinFormsApp1
{
    public partial class Form1
    {
        #region Keyboard Shortcuts
        
        /// <summary>
        /// 방향키 등 특수 키를 Form 레벨에서 먼저 처리하여 패널 포커스 문제 해결
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            try
            {
                // ? YOLO 추적/탐지 중에는 방향키(5초 이동) 차단 (중요!)
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
                    // Shift + 방향키: 2초씩 이동 (우선 처리)
                    if (keyData == (Keys.Shift | Keys.Left))
                    {
                        SeekBySeconds(-2);
                        return true; // ??? ?? ??
                    }
                    else if (keyData == (Keys.Shift | Keys.Right))
                    {
                        SeekBySeconds(2);
                        return true; // ??? ?? ??
                    }
                    else if (keyData == Keys.Left)
                    {
                        SeekBySeconds(-5);
                        return true; // ??? ?? ??
                    }
                    else if (keyData == Keys.Right)
                    {
                        SeekBySeconds(5);
                        return true; // ??? ?? ??
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
        
        // ? YOLO 추적 중인지 확인하는 메서드
        // 단순 탐지(DetectCurrentFrameOnly)는 추적과 별개이므로 추적 중일 때만 true 반환
        private bool IsYoloOperationInProgress()
        {
            try
            {
                // YOLO 추적 중인지 확인 (단순 탐지는 추적과 별개이므로 제외)
                if (isTrackingInProgress)
                    return true;
                
                // ? 단순 탐지 작업은 추적과 별개이므로 차단하지 않음
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
                // ? 입력 컨트롤(TextBox, ComboBox 등)에 포커스가 있으면 단축키 무시
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
                
                // ? YOLO 추적/탐지 중에는 모든 키 입력 무시 (작업 보호)
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[키 입력 차단] YOLO 추적/탐지 중이므로 키 입력 무시");
                    e.Handled = true;
                    return;
                }
                
                // ? 추적 중에는 모든 키 입력 무시 (추적 작업 보호)
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
            
            // ? 영상 로드 여부와 무관하게 동작하는 ID 설정 단축키들
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
                    // ? 선택된 person 박스가 있으면 현재 박스의 ID를 변경
                    if (selectedBox != null && selectedBox.Label == "person")
                    {
                        int oldId = selectedBox.PersonId;
                        int newId = assignedId.Value;
                        
                        // ? 해당 박스가 속한 waypoint 찾기
                        var waypoint = FindWaypointForBox(selectedBox);
                        
                        if (waypoint != null && waypoint.Label == "person")
                        {
                            // ? waypoint 범위 내의 모든 person 박스의 PersonId 변경
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
                            
                            // ? waypoint의 ObjectId도 변경
                            waypoint.ObjectId = newId;
                            
                            // ? waypoint 리스트 업데이트
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
                    // ? 선택된 person 박스가 있으면 현재 박스의 ID를 변경
                    if (selectedBox != null && selectedBox.Label == "person")
                    {
                        int oldId = selectedBox.PersonId;
                        int newId = assignedId.Value;
                        
                        // ? 해당 박스가 속한 waypoint 찾기
                        var waypoint = FindWaypointForBox(selectedBox);
                        
                        if (waypoint != null && waypoint.Label == "person")
                        {
                            // ? waypoint 범위 내의 모든 person 박스의 PersonId 변경
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
                            
                            // ? waypoint의 ObjectId도 변경
                            waypoint.ObjectId = newId;
                            
                            // ? waypoint 리스트 업데이트
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
                    // ? 선택된 person 박스가 있으면 현재 박스의 ID를 변경
                    if (selectedBox != null && selectedBox.Label == "person")
                    {
                        int oldId = selectedBox.PersonId;
                        int newId = assignedId.Value;
                        
                        // ? 해당 박스가 속한 waypoint 찾기
                        var waypoint = FindWaypointForBox(selectedBox);
                        
                        if (waypoint != null && waypoint.Label == "person")
                        {
                            // ? waypoint 범위 내의 모든 person 박스의 PersonId 변경
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
                            
                            // ? waypoint의 ObjectId도 변경
                            waypoint.ObjectId = newId;
                            
                            // ? waypoint 리스트 업데이트
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
            else if (e.Shift && e.KeyCode == Keys.OemPeriod) // Shift + > (> ?)
            {
                IncreasePlaybackSpeed();
                e.Handled = true;
            }
            else if (e.Shift && e.KeyCode == Keys.Oemcomma) // Shift + < (< ?)
            {
                DecreasePlaybackSpeed();
                e.Handled = true;
            }
            else if (e.Shift && e.KeyCode == Keys.OemQuestion) // Shift + / ?
            {
                ResetPlaybackSpeed();
                e.Handled = true;
            }
            else if (e.Shift && e.KeyCode == Keys.N && !e.Control && !e.Alt) // Shift + N: 속성값 조회 토글
            {
                // ? Shift + N: 속성창 토글 (켜져 있으면 끄고, 꺼져 있으면 켜기)
                // 재생 중이어도 토글 가능 (단, 재생 중에는 창이 업데이트되지 않음)
                ToggleAttributeView();
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
                
                // ? 삭제 플래그 설정 (실제 제거 안 함, 흔적 유지)
                selectedBox.IsDeleted = true;
                
                // ? 사라짐 의도 기록
                RecordDisappearanceIntent(selectedBox);
                
                selectedBox = null;
                UpdateBoxCount();
                UpdateBboxListDisplay();
                pictureBoxVideo.Invalidate();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                // ? 박스가 선택되어 있으면 박스 삭제 우선
                if (selectedBox != null)
                {
                    AddUndoAction(new UndoAction { Type = UndoActionType.RemoveBox, Box = CloneBoundingBox(selectedBox) });
                    
                    // ? 삭제 플래그 설정 (실제 제거 안 함, 흔적 유지)
                    selectedBox.IsDeleted = true;
                
                    // ? 사라짐 의도 기록
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
                // ? R: 강제 관성 추적을 위한 a프레임 설정
                if (selectedBox != null)
                {
                    if (IsFaceBox(selectedBox))
                    {
                        var parentWaypoint = FindParentPersonWaypointForFace(selectedBox);
                        if (parentWaypoint == null)
                        {
                            MessageBox.Show(
                                "Face 박스의 부모 person 웨이포인트를 찾을 수 없습니다.",
                                "알림",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                            e.Handled = true;
                            return;
                        }

                        selectedBox.BoxEntryFrame = currentFrameIndex;
                        selectedBox.BoxExitFrame = null;

                        string key = GetTrackedBoxKey(selectedBox);
                        forcedInertiaTrackingStartFrames[key] = currentFrameIndex;

                        MessageBox.Show(
                            "Face a프레임이 설정되었습니다.\n\n" +
                            $"객체: {GetCategoryName(selectedBox.Label, GetTrackedPersonIdentity(selectedBox))}\n" +
                            $"a프레임: {currentFrameIndex}\n\n" +
                            "이제 b프레임으로 이동한 뒤\n" +
                            "Shift+T를 눌러 강제 관성 추적(보간)을 실행하세요.\n" +
                            "(Shift+T 누른 시점이 b프레임입니다)",
                            "a프레임 설정 완료",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        e.Handled = true;
                    }
                    else
                    {
                        var waypoint = FindWaypointForBox(selectedBox);
                        if (waypoint != null)
                        {
                                                        int boxId = GetBoxId(selectedBox);
                            string key = GetTrackedBoxKey(selectedBox);
                            int displayVehicleId = selectedBox.Label == "vehicle"
                                ? FindVehicleBodyForTracking(selectedBox)?.VehicleId ?? selectedBox.VehicleId
                                : boxId;
                            string displayVehicleName = selectedBox.Label == "vehicle"
                                ? GetCategoryName("vehicle", displayVehicleId)
                                : GetCategoryName(selectedBox.Label, boxId);

                            // a프레임 저장 (현재 프레임)
                            forcedInertiaTrackingStartFrames[key] = currentFrameIndex;

                            MessageBox.Show(
                                $"a프레임이 설정되었습니다.\n\n" +
                                $"객체: {displayVehicleName}\n" +
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
                }
                else
                {
                    MessageBox.Show("a프레임을 설정할 박스를 먼저 선택해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    e.Handled = true;
                }
            }
            else if (e.Shift && e.KeyCode == Keys.T && !e.Control && !e.Alt)
            {
                // ? Shift+T: 강제 관성 추적 (현재 프레임을 b프레임으로 사용)
                if (selectedBox != null)
                {
                    if (IsFaceBox(selectedBox))
                    {
                        var parentWaypoint = FindParentPersonWaypointForFace(selectedBox);
                        if (parentWaypoint == null)
                        {
                            MessageBox.Show(
                                "Face 박스의 부모 person 웨이포인트를 찾을 수 없습니다.",
                                "알림",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                            e.Handled = true;
                            return;
                        }

                        int aFrame;
                        string key = GetTrackedBoxKey(selectedBox);
                        if (forcedInertiaTrackingStartFrames.ContainsKey(key))
                        {
                            aFrame = forcedInertiaTrackingStartFrames[key];
                        }
                        else
                        {
                            MessageBox.Show(
                                "Face 보간은 먼저 R로 a프레임을 설정해야 합니다.",
                                "알림",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                            e.Handled = true;
                            return;
                        }

                        int bFrame = currentFrameIndex;

                        if (bFrame < parentWaypoint.EntryFrame || bFrame > parentWaypoint.ExitFrame)
                        {
                            MessageBox.Show(
                                $"b프레임({bFrame})이 부모 person 웨이포인트 범위({parentWaypoint.EntryFrame}~{parentWaypoint.ExitFrame})를 벗어났습니다.",
                                "오류",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                            e.Handled = true;
                            return;
                        }

                        if (aFrame < parentWaypoint.EntryFrame || aFrame > parentWaypoint.ExitFrame)
                        {
                            MessageBox.Show(
                                $"a프레임({aFrame})이 부모 person 웨이포인트 범위({parentWaypoint.EntryFrame}~{parentWaypoint.ExitFrame})를 벗어났습니다.\n\n" +
                                "이전 Face 추적 구간에서 R를 다시 설정해 주세요.",
                                "오류",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                            e.Handled = true;
                            return;
                        }

                        if (aFrame >= bFrame)
                        {
                            MessageBox.Show(
                                "a프레임은 b프레임보다 작아야 합니다.\n\n" +
                                "R로 설정한 a프레임을 더 작은 값으로 설정하세요.",
                                "오류",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                            e.Handled = true;
                            return;
                        }

                        BoundingBox boxForTracking = FindTrackedFaceBoxAtFrame(aFrame, selectedBox);
                        if (boxForTracking == null)
                        {
                            MessageBox.Show(
                                $"a프레임({aFrame})에서 동일한 Face 박스를 찾을 수 없습니다.",
                                "오류",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                            e.Handled = true;
                            return;
                        }

                        selectedBox.BoxEntryFrame = aFrame;
                        selectedBox.BoxExitFrame = bFrame;
                        PerformForcedInertiaTracking(boxForTracking, aFrame, bFrame);
                        e.Handled = true;
                    }
                    else
                    {
                        var waypoint = FindWaypointForBox(selectedBox);
                        if (waypoint != null)
                        {
                            bool selectedPlate = IsPlateBox(selectedBox);
                            int boxId = GetBoxId(selectedBox);
                            string key = GetTrackedBoxKey(selectedBox);

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

                            // ? b 프레임: Shift+T를 누른 시점(현재 프레임)
                            int bFrame = currentFrameIndex;

                            // b 프레임이 waypoint 범위를 넘지 않도록 제한
                            if (bFrame > waypoint.ExitFrame)
                            {
                                bFrame = waypoint.ExitFrame;
                            }

                            // ? 프레임 범위 유효성 검증
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

                            // a프레임 >= b프레임인 경우 (보간 불가)
                            if (aFrame >= bFrame)
                            {
                                MessageBox.Show(
                                    $"프레임 범위가 유효하지 않습니다.\n\n" +
                                    $"a프레임: {aFrame}\n" +
                                    $"b프레임: {bFrame}\n\n" +
                                    "a프레임은 b프레임보다 작아야 합니다.\n" +
                                    "현재 b프레임이 a프레임과 같거나 작습니다.\n\n" +
                                    "해결 방법:\n" +
                                    "R키로 a프레임을 더 작은 값으로 설정하세요.",
                                    "오류",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                                e.Handled = true;
                                return;
                            }

                            // a프레임이 현재 선택된 박스의 프레임과 다르면 해당 프레임의 박스로 전환
                            if (selectedPlate)
                            {
                                BoundingBox plateForTracking = FindPlateAtFrame(aFrame, selectedBox);
                                if (plateForTracking == null)
                                {
                                    MessageBox.Show(
                                        "a 프레임에서 선택한 번호판 박스를 찾을 수 없습니다.",
                                        "번호판 추적",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Warning);
                                    e.Handled = true;
                                    return;
                                }

                                PerformForcedPlateTracking(plateForTracking, aFrame, bFrame);
                                e.Handled = true;
                                return;
                            }
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
                }
                else
                {
                    MessageBox.Show("추적할 박스를 먼저 선택해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    e.Handled = true;
                }
            }
            else if (e.Control && e.KeyCode == Keys.T)
            {
                // ? Ctrl+T: 부분 재추적 (기존 로직)
                if (selectedBox != null)
                {
                    if (IsFaceBox(selectedBox))
                    {
                        MessageBox.Show(
                            "Face 박스는 Ctrl+T 강제/부분 재추적 경로를 사용할 수 없습니다.\nR 후 Shift+T를 사용해 주세요.",
                            "알림",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        e.Handled = true;
                        return;
                    }

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
                // ? Entry 설정 해제
                if (entryFrameIndex.HasValue)
                {
                    entryFrameIndex = null;
                    btnEntry.Text = "Entry";
                    panelTimeline.Invalidate();
                    System.Diagnostics.Debug.WriteLine("[Entry 해제] ESC 키로 Entry 설정이 해제되었습니다.");
                }
                
                selectedBox = null;
                ClearSidebarHighlights(); // ? 하이라이트 초기화
                pictureBoxVideo.Invalidate();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Y && !e.Control && !e.Shift && !e.Alt)
            {
                // ? Y 키: YOLO 탐지 토글
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
                    // ? YOLO 추적/탐지 중에는 한 프레임 이동 차단 (중요!)
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
                    
                    // ? 이전 프레임으로 이동
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
                    // ? YOLO 추적/탐지 중에는 한 프레임 이동 차단 (중요!)
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
                    
                    // ? 다음 프레임으로 이동
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

            // ? UI 업데이트: 선택된 박스의 정보도 갱신
            UpdateObjectInfo(selectedBox);
            UpdateBboxListDisplay();
            labelObjectLabel.Text = $"Label: {selectedBox.Label}_{id:D2}";
            pictureBoxVideo.Invalidate();

            currentMode = DrawMode.Draw;
            btnEdit.BackColor = Color.FromArgb(59, 130, 246);
            btnSelectAll.BackColor = SystemColors.Control;
            pictureBoxVideo.Cursor = Cursors.Cross;

            // ? selectedBox를 null로 초기화하지 않음 (선택 상태 유지)
        }
        #endregion

        private WaypointMarker FindParentPersonWaypointForFace(BoundingBox faceBox)
        {
            if (faceBox == null || !IsFaceBox(faceBox))
                return null;

            int personId = GetTrackedPersonIdentity(faceBox);

            return waypointMarkers.FirstOrDefault(w =>
                w.Label == "person" &&
                w.ObjectId == personId &&
                currentFrameIndex >= w.EntryFrame &&
                currentFrameIndex <= w.ExitFrame);
        }

        private BoundingBox FindTrackedFaceBoxAtFrame(int frameIndex, BoundingBox templateBox)
        {
            if (templateBox == null)
                return null;

            return boundingBoxes.FirstOrDefault(b =>
                b.FrameIndex == frameIndex &&
                !b.IsDeleted &&
                IsSameTrackedBox(templateBox, b));
        }
    }
}
