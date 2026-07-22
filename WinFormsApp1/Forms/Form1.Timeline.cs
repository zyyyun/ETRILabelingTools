using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace WinFormsApp1
{
    public partial class Form1
    {
        #region Timeline
        private static readonly Font timelineFont = new Font("Segoe UI", 7F, FontStyle.Bold);
        private static readonly Font timelineLabelFont = new Font("Segoe UI", 7F, FontStyle.Bold);
        private string activeWaypointListOwner;

        private void ActivateWaypointListOwner(string owner)
        {
            activeWaypointListOwner = owner;

            if (!string.Equals(owner, "person", StringComparison.OrdinalIgnoreCase))
            {
                listViewPersonWaypoints.SelectedItems.Clear();
            }

            if (!string.Equals(owner, "vehicle", StringComparison.OrdinalIgnoreCase))
            {
                listViewVehicleWaypoints.SelectedItems.Clear();
            }

            if (!string.Equals(owner, "event", StringComparison.OrdinalIgnoreCase))
            {
                listViewEventWaypoints.SelectedItems.Clear();
            }
        }

        private void ClearWaypointListOwner(string owner)
        {
            if (string.Equals(activeWaypointListOwner, owner, StringComparison.OrdinalIgnoreCase))
            {
                activeWaypointListOwner = WaypointSelectionHelper.ResolveActiveListOwner(
                    activeWaypointListOwner,
                    listViewPersonWaypoints.SelectedItems.Count > 0,
                    listViewVehicleWaypoints.SelectedItems.Count > 0,
                    listViewEventWaypoints.SelectedItems.Count > 0);
            }
        }

        private void panelTimeline_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int width = panelTimeline.Width;
            int height = panelTimeline.Height;

            using (var bgBrush = new SolidBrush(TimelineLayoutHelper.BackgroundColor))
            {
                g.FillRectangle(bgBrush, 0, 0, width, height);
            }

            using (var linePen = new Pen(TimelineLayoutHelper.DividerColor, 1))
            {
                int y1 = TimelineLayoutHelper.HeaderHeight;
                g.DrawLine(linePen, 0, y1, width, y1);
                int y2 = y1 + TimelineLayoutHelper.RowHeight;
                g.DrawLine(linePen, 0, y2, width, y2);
                int y3 = y2 + TimelineLayoutHelper.RowHeight;
                g.DrawLine(linePen, 0, y3, width, y3);
                int y4 = y3 + TimelineLayoutHelper.RowHeight;
                g.DrawLine(linePen, 0, y4, width, y4);
            }

            using (var personBrush = new SolidBrush(Color.FromArgb(120, 255, 107, 107)))
            using (var vehicleBrush = new SolidBrush(Color.FromArgb(120, 107, 158, 255)))
            using (var eventBrush = new SolidBrush(Color.FromArgb(120, 107, 255, 107)))
            {
                g.DrawString("P", timelineLabelFont, personBrush, 2, TimelineLayoutHelper.HeaderHeight + 1);
                g.DrawString("V", timelineLabelFont, vehicleBrush, 2, TimelineLayoutHelper.HeaderHeight + TimelineLayoutHelper.RowHeight + 1);
                g.DrawString("E", timelineLabelFont, eventBrush, 2, TimelineLayoutHelper.HeaderHeight + TimelineLayoutHelper.RowHeight * 2 + 1);
            }

            if (totalFrames > 0)
            {
                foreach (var waypoint in waypointMarkers)
                {
                    var segment = TimelineLayoutHelper.CreateSegmentLayout(waypoint, width, totalFrames);
                    Color segmentColor = TimelineLayoutHelper.GetTimelineSegmentColor(waypoint.Label);

                    using (var path = CreateRoundedRect(segment.Bounds, TimelineLayoutHelper.SegmentRadius))
                    using (var brush = new SolidBrush(segmentColor))
                    {
                        g.FillPath(brush, path);
                    }

                    bool isSelected = selectedWaypoint != null &&
                        waypoint.Label == selectedWaypoint.Label &&
                        waypoint.ObjectId == selectedWaypoint.ObjectId &&
                        waypoint.EntryFrame == selectedWaypoint.EntryFrame &&
                        waypoint.ExitFrame == selectedWaypoint.ExitFrame;

                    if (isSelected)
                    {
                        using (var path = CreateRoundedRect(segment.Bounds, TimelineLayoutHelper.SegmentRadius))
                        using (var pen = new Pen(TimelineLayoutHelper.SelectionColor, 2))
                        {
                            g.DrawPath(pen, path);
                        }
                    }

                    if (segment.Width > 25)
                    {
                        string idText = TimelineLayoutHelper.GetTimelineDisplayText(waypoint);
                        using (var textBrush = new SolidBrush(TimelineLayoutHelper.SegmentTextColor))
                        {
                            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.DrawString(idText, timelineFont, textBrush, segment.Bounds, sf);
                        }
                    }
                }

                int currentX = TimelineLayoutHelper.GetPlayheadX(width, timelineProgress);
                using (var pen = new Pen(TimelineLayoutHelper.PlayheadColor, 1.5f))
                {
                    g.DrawLine(pen, currentX, TimelineLayoutHelper.HeaderHeight, currentX, height);
                }

                var triangle = new Point[]
                {
                    new(currentX, 0),
                    new(currentX - 4, TimelineLayoutHelper.HeaderHeight - 1),
                    new(currentX + 4, TimelineLayoutHelper.HeaderHeight - 1)
                };
                using (var brush = new SolidBrush(TimelineLayoutHelper.PlayheadHeadColor))
                {
                    g.FillPolygon(brush, triangle);
                }
            }

            if (entryFrameIndex.HasValue && !exitFrameIndex.HasValue && totalFrames > 0)
            {
                int entryX = TimelineLayoutHelper.GetFrameX(entryFrameIndex.Value, width, totalFrames);
                using (var pen = new Pen(Color.Red, 2))
                {
                    g.DrawLine(pen, entryX, TimelineLayoutHelper.HeaderHeight, entryX, height);
                }

                var trianglePoints = new Point[]
                {
                    new(entryX, 0),
                    new(entryX - 4, TimelineLayoutHelper.HeaderHeight - 1),
                    new(entryX + 4, TimelineLayoutHelper.HeaderHeight - 1)
                };
                using (var brush = new SolidBrush(Color.Red))
                {
                    g.FillPolygon(brush, trianglePoints);
                }
            }
        }

        private System.Drawing.Drawing2D.GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            if (rect.Width < radius * 2) radius = rect.Width / 2;
            if (rect.Height < radius * 2) radius = rect.Height / 2;
            float diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void panelTimeline_MouseDown(object sender, MouseEventArgs e)
        {
            try
            {
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[Timeline] Click ignored because a YOLO operation is in progress.");
                    return;
                }

                if (totalFrames == 0) return;

                if (TrySelectWaypointFromTimeline(e.Location))
                {
                    return;
                }

                isTimelineDragging = true;
                UpdateFrameFromMousePosition(e.X);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Timeline scrub error] {ex.Message}\\n{ex.StackTrace}");
            }
        }

        private bool TrySelectWaypointFromTimeline(Point location)
        {
            var hitWaypoint = TimelineLayoutHelper.TryHitWaypointSegment(waypointMarkers, location.X, location.Y, panelTimeline.Width, totalFrames);
            if (hitWaypoint == null)
            {
                return false;
            }

            selectedWaypoint = hitWaypoint;
            UpdateWaypointInfo(hitWaypoint);
            SelectWaypointInListView(hitWaypoint);
            panelTimeline.Invalidate();
            LoadFrame(hitWaypoint.EntryFrame);
            suppressWaypointClickOnce = true;
            return true;
        }

        private void SelectWaypointInListView(WaypointMarker waypoint)
        {
            ListView listView = GetWaypointListView(waypoint.Label);
            if (listView == null)
            {
                return;
            }

            foreach (ListViewItem item in listView.Items)
            {
                bool matches = item.Tag is WaypointMarker candidate &&
                    candidate.Label == waypoint.Label &&
                    candidate.ObjectId == waypoint.ObjectId &&
                    candidate.EntryFrame == waypoint.EntryFrame &&
                    candidate.ExitFrame == waypoint.ExitFrame;

                item.Selected = matches;
                if (matches)
                {
                    item.Focused = true;
                    item.EnsureVisible();
                }
            }
        }

        private ListView GetWaypointListView(string label)
        {
            return label switch
            {
                "person" => listViewPersonWaypoints,
                "vehicle" => listViewVehicleWaypoints,
                "event" => listViewEventWaypoints,
                _ => null
            };
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
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[Timeline] Scrub ignored because a YOLO operation is in progress.");
                    return;
                }

                if (totalFrames == 0) return;

                int targetFrame = TimelineLayoutHelper.GetFrameFromMouseX(mouseX, panelTimeline.Width, totalFrames);
                LoadFrame(targetFrame);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Timeline scrub error] {ex.Message}\\n{ex.StackTrace}");
            }
        }

        private void listViewPersonWaypoints_Click(object sender, EventArgs e)
        {
            if (suppressWaypointClickOnce)
            {
                suppressWaypointClickOnce = false;
                return;
            }
            ActivateWaypointListOwner("person");
            // Person Waypoint ????????????????????Entry ?????諛몃마??????諛몃마?????筌?痢??????
            if (listViewPersonWaypoints.SelectedItems.Count > 0)
            {
                var selectedItem = listViewPersonWaypoints.SelectedItems[0];
                var waypoint = selectedItem.Tag as WaypointMarker;

                if (waypoint != null)
                {
                    selectedWaypoint = waypoint; // ??????影?력???waypoint ????
                    UpdateWaypointInfo(waypoint); // ??Waypoint ??轅붽틓???????????癲?
                    panelTimeline.Invalidate(); // ??Timeline ??????살퓢???????좊틣??????蹂졻뀳?
                    LoadFrame(waypoint.EntryFrame);
                }
            }
            else
            {
                selectedWaypoint = null; // ??????影?력????????⑤뜪??
                UpdateWaypointInfo(null); // ??Waypoint ??轅붽틓?????????????멸괜???
                panelTimeline.Invalidate();
                ClearWaypointListOwner("person");
            }
        }

        private void listViewVehicleWaypoints_Click(object sender, EventArgs e)
        {
            if (suppressWaypointClickOnce)
            {
                suppressWaypointClickOnce = false;
                return;
            }
            ActivateWaypointListOwner("vehicle");
            // Vehicle Waypoint ?????????????????????????諛몃마??????諛몃마?????筌?痢??????(????獒뺣폍???????諛몃마???
            if (listViewVehicleWaypoints.SelectedItems.Count > 0)
            {
                var selectedItem = listViewVehicleWaypoints.SelectedItems[0];
                var waypoint = selectedItem.Tag as WaypointMarker;

                if (waypoint != null)
                {
                    selectedWaypoint = waypoint; // ??????影?력???waypoint ????
                    panelTimeline.Invalidate(); // ??Timeline ??????살퓢???????좊틣??????蹂졻뀳?
                    LoadFrame(waypoint.EntryFrame); // Entry = Exit (????獒뺣폍???????諛몃마???
                }
            }
            else
            {
                selectedWaypoint = null; // ??????影?력????????⑤뜪??
                UpdateWaypointInfo(null); // ??Waypoint ??轅붽틓?????????????멸괜???
                panelTimeline.Invalidate();
                ClearWaypointListOwner("vehicle");
            }
        }

        private void listViewEventWaypoints_Click(object sender, EventArgs e)
        {
            // Event Waypoint ????????????????????Entry ?????諛몃마??????諛몃마?????筌?痢??????
            if (suppressWaypointClickOnce)
            {
                suppressWaypointClickOnce = false;
                return;
            }
            ActivateWaypointListOwner("event");
            if (listViewEventWaypoints.SelectedItems.Count > 0)
            {
                var selectedItem = listViewEventWaypoints.SelectedItems[0];
                var waypoint = selectedItem.Tag as WaypointMarker;

                if (waypoint != null)
                {
                    selectedWaypoint = waypoint; // ??????影?력???waypoint ????
                    UpdateWaypointInfo(waypoint); // ??Waypoint ??轅붽틓???????????癲?
                    panelTimeline.Invalidate(); // ??Timeline ??????살퓢???????좊틣??????蹂졻뀳?
                    LoadFrame(waypoint.EntryFrame);
                }
            }
            else
            {
                selectedWaypoint = null; // ??????影?력????????⑤뜪??
                UpdateWaypointInfo(null); // ??Waypoint ??轅붽틓?????????????멸괜???
                panelTimeline.Invalidate();
                ClearWaypointListOwner("event");
            }
        }

        // ????녾컯????욱렱嶺??轅붽틓????????????棺堉?댆??????????力?肉?????????????怨뺤떪??Entry/Exit????????????????ル늉??????????썹땟戮녹??諛명꺐??????影?력??
        private void listViewWaypoints_MouseDown(object sender, MouseEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null) return;

            var hit = listView.HitTest(e.Location);
            if (listView == listViewPersonWaypoints)
                ActivateWaypointListOwner("person");
            else if (listView == listViewVehicleWaypoints)
                ActivateWaypointListOwner("vehicle");
            else if (listView == listViewEventWaypoints)
                ActivateWaypointListOwner("event");

            if (hit.Item == null)
            {
                listView.SelectedItems.Clear();
                selectedWaypoint = null;
                UpdateWaypointInfo(null);
                panelTimeline.Invalidate();

                if (listView == listViewPersonWaypoints)
                    ClearWaypointListOwner("person");
                else if (listView == listViewVehicleWaypoints)
                    ClearWaypointListOwner("vehicle");
                else if (listView == listViewEventWaypoints)
                    ClearWaypointListOwner("event");

                return;
            }

            if (hit.Item.Tag is WaypointMarker waypoint)
            {
                selectedWaypoint = waypoint;
                UpdateWaypointInfo(waypoint); // ??Waypoint ??轅붽틓???????????癲?
                panelTimeline.Invalidate();

                // ???棺堉?댆??????????力?肉??????? SubItem ??轅붽틓??????????Entry/Exit ???????
                int targetFrame = waypoint.EntryFrame;
                bool shouldSelectBox = false;

                if (hit.SubItem != null)
                {
                    int subIndex = hit.Item.SubItems.IndexOf(hit.SubItem);

                    // Person/Vehicle: ???棺堉?댆???(Entry)=Entry, ???棺堉?댆???(Exit)=Exit, ???棺堉?댆???(????ル늉??????=????썹땟戮녹??諛명꺐??????影?력??
                    if (listView == listViewPersonWaypoints || listView == listViewVehicleWaypoints)
                    {
                        if (subIndex == 0) // Entry ???棺堉?댆???
                            targetFrame = waypoint.EntryFrame;
                        else if (subIndex == 1) // Exit ???棺堉?댆???
                            targetFrame = waypoint.ExitFrame;
                        else if (subIndex == 2) // ????ル늉?????????棺堉?댆??? ?????諛몃마???????諛몃마??????諛몃마???????썹땟戮녹??諛명꺐??????影?력????몃쭫??(EntryFrame ?????????
                        {
                            shouldSelectBox = true;
                        }
                    }
                    // Event: ???棺堉?댆???(Event)=Entry, ???棺堉?댆???(??????=Entry, ???棺堉?댆???(????ル늉??????=????썹땟戮녹??諛명꺐??????影?력??
                    else if (listView == listViewEventWaypoints)
                    {
                        if (subIndex == 2) // ????ル늉?????????棺堉?댆??? ?????諛몃마???????諛몃마??????諛몃마???????썹땟戮녹??諛명꺐??????影?력????몃쭫??(EntryFrame ?????????
                        {
                            shouldSelectBox = true;
                        }
                        else // ??????딅텑?????棺堉?댆???? Entry???????
                            targetFrame = waypoint.EntryFrame;
                    }
                }

                // ??????ル늉?????????棺堉?댆?????????諛몃마?????????????諛몃마????????
                if (!shouldSelectBox)
                {
                    LoadFrame(targetFrame);
                }

                // ????ル늉?????????棺堉?댆???????????????????ル늉???????????썹땟戮녹??諛명꺐??????影?력??(?????諛몃마???????諛몃마??????諛몃마???
                if (shouldSelectBox)
                {
                    SelectBoxForWaypoint(waypoint);
                }

                // ???????????1?????嶺뚮Ĳ????
                suppressWaypointClickOnce = true;
            }
        }

        // Waypoint?????????汝뷴젆??녷뉩??읂?????ル늉??????????썹땟戮녹??諛명꺐??????影?력??
        private void SelectBoxForWaypoint(WaypointMarker waypoint)
        {
            // ?????諛몃마???????諛몃마??????諛몃마???waypoint??ObjectId?? Label?????????汝뷴젆??녷뉩??읂?????썹땟戮녹??諛명꺐???饔낅떽??????釉먮꼥???レ젛?
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
                                       TrackingIdentityHelper.MatchesWaypoint(b, waypoint) &&
                                       !TrackingIdentityHelper.IsPlate(b) &&
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

        // Event waypoint??????ル늉??????P/V) ???棺堉?댆???3?癲????? ????嫄???????????轅붽틓????釉먮?????轅붽틓????獄쏅챸??
        private TextBox eventListEditBox;

        private void listViewEventWaypoints_DoubleClick(object sender, EventArgs e)
        {
            var mouse = listViewEventWaypoints.PointToClient(Control.MousePosition);
            var hit = listViewEventWaypoints.HitTest(mouse);
            if (hit.Item == null || hit.SubItem == null) return;

            int subIndex = hit.Item.SubItems.IndexOf(hit.SubItem);
            if (subIndex != 2) return; // ????ル늉??????P/V) ???棺堉?댆???삳ħ?????轅붽틓????獄쏅챸?????嚥싲갭큔?댁빢??

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

            eventListEditBox.Tag = hit.Item; // ListViewItem ????쇰뮛??? (Waypoint??Item.Tag?????嚥싲갭큔???
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

            // ????癰궽블뀯???饔낅떽??影?곗몡嶺뚮??껆빊??????
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
            // ????獒뺣폍???????????????3?癲????????棺堉?댆??????????덇텣??????轅붽틓????獄쏅챸?????꿔꺂???影?우Ŀ?
            var hit = listViewEventWaypoints.HitTest(e.Location);
            if (hit.Item == null || hit.SubItem == null) return;
            int subIndex = hit.Item.SubItems.IndexOf(hit.SubItem);
            if (subIndex != 2) return;

            // ???? ??轅붽틓????獄쏅챸???關???꾨き??熬곥룊??????爰?????嶺뚮Ĳ????
            if (eventListEditBox != null && eventListEditBox.Tag != null) return;

            // ??轅붽틓????獄쏅챸?????꿔꺂???影?우Ŀ?
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

        // ?????? waypoint ?????????(?饔낅떽????ш낄?뉔뇡????????饔낅떽??????
        private void btnDeleteSelectedWaypoint_Click(object sender, EventArgs e)
        {
            try
            {
                WaypointMarker waypoint = null;
                string waypointType = string.Empty;

                string owner = WaypointSelectionHelper.ResolveActiveListOwner(
                    activeWaypointListOwner,
                    listViewPersonWaypoints.SelectedItems.Count > 0,
                    listViewVehicleWaypoints.SelectedItems.Count > 0,
                    listViewEventWaypoints.SelectedItems.Count > 0);

                if (string.Equals(owner, "person", StringComparison.OrdinalIgnoreCase) &&
                    listViewPersonWaypoints.SelectedItems.Count > 0)
                {
                    waypoint = listViewPersonWaypoints.SelectedItems[0].Tag as WaypointMarker;
                    waypointType = "Person";
                }
                else if (string.Equals(owner, "vehicle", StringComparison.OrdinalIgnoreCase) &&
                    listViewVehicleWaypoints.SelectedItems.Count > 0)
                {
                    waypoint = listViewVehicleWaypoints.SelectedItems[0].Tag as WaypointMarker;
                    waypointType = "Vehicle";
                }
                else if (string.Equals(owner, "event", StringComparison.OrdinalIgnoreCase) &&
                    listViewEventWaypoints.SelectedItems.Count > 0)
                {
                    waypoint = listViewEventWaypoints.SelectedItems[0].Tag as WaypointMarker;
                    waypointType = "Event";
                }
                else
                {
                    MessageBox.Show("삭제할 Waypoint를 선택해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                    $"해당 구간의 {waypointType} 박스도 함께 삭제됩니다.",
                    $"{waypointType} Waypoint 삭제",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                {
                    return;
                }

                List<BoundingBox> boxesToDelete = new List<BoundingBox>();

                if (waypoint.Label == "person")
                {
                    boxesToDelete = boundingBoxes
                        .Where(b => b.Label == "person" && b.PersonId == waypoint.ObjectId && b.FrameIndex >= waypoint.EntryFrame && b.FrameIndex <= waypoint.ExitFrame)
                        .ToList();
                }
                else if (waypoint.Label == "vehicle")
                {
                    boxesToDelete = boundingBoxes
                        .Where(b => TrackingIdentityHelper.MatchesWaypoint(b, waypoint) &&
                                    b.FrameIndex >= waypoint.EntryFrame &&
                                    b.FrameIndex <= waypoint.ExitFrame)
                        .ToList();
                }
                else if (waypoint.Label == "event")
                {
                    boxesToDelete = EventFinalizationHelper.GetEventBoxesForWaypoint(
                        boundingBoxes,
                        waypoint)
                        .ToList();
                }

                foreach (var box in boxesToDelete)
                {
                    AddUndoAction(new UndoAction { Type = UndoActionType.RemoveBox, Box = CloneBoundingBox(box) });
                    boundingBoxes.Remove(box);
                }

                if (selectedBox != null && boxesToDelete.Contains(selectedBox))
                {
                    selectedBox = null;
                }

                if (selectedWaypoint == waypoint)
                {
                    selectedWaypoint = null;
                    UpdateWaypointInfo(null);
                }

                activeWaypointListOwner = null;

                waypointMarkers.Remove(waypoint);
                UpdateWaypointListView();
                InvalidateBoxCache();
                UpdateBoxCount();
                UpdateBboxListDisplay();
                panelTimeline.Invalidate();
                pictureBoxVideo.Invalidate();

                bool saved = SaveCurrentLabelingData();

                MessageBox.Show(
                    saved
                        ? $"삭제된 박스 수: {boxesToDelete.Count}\n\n삭제가 완료되었습니다."
                        : $"삭제된 박스 수: {boxesToDelete.Count}\n\n메모리에서는 삭제되었지만 JSON 저장에 실패했습니다. 다시 저장해 주세요.",
                    saved ? "알림" : "저장 경고",
                    MessageBoxButtons.OK,
                    saved ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Waypoint delete error] {ex.Message}\\n{ex.StackTrace}");
                MessageBox.Show(
                    $"Waypoint 삭제 중 오류가 발생했습니다:\n{ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

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
    }
}




