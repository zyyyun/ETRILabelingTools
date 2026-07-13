using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Compunet.YoloSharp.Data;

namespace WinFormsApp1
{
    public partial class Form1
    {
        #region Drawing Mode
        private void btnVideoList_Click(object sender, EventArgs e) => ShowVideoListForm();

        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            currentMode = DrawMode.Select;
            btnSelectAll.BackColor = Color.FromArgb(59, 130, 246);
            btnEdit.BackColor = SystemColors.Control;
            pictureBoxVideo.Cursor = Cursors.Hand;
            
            // ????좎?源??獄쏅벡?ゅ첎? ??좎럩?앭뜝???좎럩???좎럩?????좎룞??
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
            
            // ??Edit 筌뤴뫀諭뜹뜝???좎???????좎럩???좎럩?????좎룞??
            if (selectedBox != null)
            {
                HighlightSelectedBoxInSidebar();
            }
        }

        

        private void pictureBoxVideo_MouseDown(object sender, MouseEventArgs e)
        {
            // ????좎?寃℡뜝? Person 獄쏅벡????좎럩苑???좎럩彛?
            if (e.Button == MouseButtons.Right && currentMode == DrawMode.Select)
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
                            // ??좎럩苑???????좎럥利?(form??좎럩苑???좎룞?? ????좎럥留?
                            UpdateBboxListDisplay();
                            pictureBoxVideo.Invalidate();
                            
                            // ??좎럩苑?鈺곌퀬??????좎럥???좎???
                            UpdateAttributeWindows();
                        }
                    }
                    return;
                }
            }
            
            if (currentMode == DrawMode.Draw)
            {
                bool isFaceCreation = (e.Button == MouseButtons.Right && currentSelectedLabel == "person");
                var activeBodyForFace = isFaceCreation ? FindActivePersonBodyForCurrentFrame(currentFrameIndex) : null;
                if (isFaceCreation && activeBodyForFace == null)
                {
                    MessageBox.Show("No matching active body in current person waypoint. Face box creation is disabled.", "Face box", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Entry ???(???) - YOLO ?좎룞?쇿뜝?숈삕 ?좎룞?쇿뜝?
                isDrawing = true;
                drawStartPoint = e.Location; // ?좎룞?쇿뜝?숈삕 ?좎룞?숉몴 ?좎룞?쇿뜝?숈삕

                // ?붷뜝?숈삕 ?좎룞?숉몴 -> ?좎떛諭꾩삕?좎룞???좎룞?숉몴 ?좎룞?숉솚
                var imagePoint = ViewToImage(new PointF(e.X, e.Y));

                drawingBox = new BoundingBox
                {
                    FrameIndex = currentFrameIndex,
                    Rectangle = new Rectangle((int)imagePoint.X, (int)imagePoint.Y, 0, 0),
                    Label = currentSelectedLabel,
                    PersonId = isFaceCreation ? (activeBodyForFace != null ? activeBodyForFace.PersonId : currentAssignedId) : (currentSelectedLabel == "person" ? currentAssignedId : 0),
                    VehicleId = currentSelectedLabel == "vehicle" ? currentAssignedId : 0,
                    EventId = currentSelectedLabel == "event" ? currentAssignedId : 0,
                    EventInstanceId = currentSelectedLabel == "event" ? CreateEventInstanceId() : null,
                    Action = "waypoint",
                    PersonPartType = isFaceCreation
                        ? "face"
                        : (currentSelectedLabel == "person" ? "body" : null),
                    LinkedPersonId = null,
                    BoxEntryFrame = null
                };
            }
            else if (currentMode == DrawMode.Select)
            {
                // ???믪눦?? ??좎?源??獄쏅벡?????좎럡由?鈺곌퀣????좎럥諭??筌ｋ똾寃?
                if (selectedBox != null)
                {
                    var viewRect = ImageToView(new RectangleF(selectedBox.Rectangle.X, selectedBox.Rectangle.Y, 
                        selectedBox.Rectangle.Width, selectedBox.Rectangle.Height));
                    
                    ResizeHandle handle = GetResizeHandleAtPoint(e.Location, viewRect);
                    
                    if (handle != ResizeHandle.None)
                    {
                        // ??좎럡由?鈺곌퀣????좎럩??
                        isResizing = true;
                        currentResizeHandle = handle;
                        resizeStartPoint = e.Location;
                        originalResizeRect = selectedBox.Rectangle;
                        return;
                    }

                    // ????좎?源??獄쏅벡????좎룞?? ??좎럥??????좎럥????좎럥????좎룞??????좎럥?믣뜝???좎럩??
                    if (viewRect.Contains(e.Location))
                    {
                        // 野껊????獄쏅벡?ゅ첎? ??좎럩?앭뜝???좎럥????좎럥????좎룞??
                        if (HasAnotherHitCandidateAt(e.Location, selectedBox))
                        {
                            isWaitingForDoubleClick = true;
                            lastClickPoint = e.Location;
                            dragOffset = new System.Drawing.Point(e.X - (int)viewRect.X, e.Y - (int)viewRect.Y);
                            
                            // ??좎럥????좎럥??????좎럥????좎럩??(500ms ????좎럥?믣뜝???좎럩??
                            if (doubleClickTimer != null)
                            {
                                doubleClickTimer.Dispose();
                            }
                            doubleClickTimer = new System.Threading.Timer((state) =>
                            {
                                if (isWaitingForDoubleClick && !isDragging)
                                {
                                    this.Invoke((Action)(() =>
                                    {
                                        if (isWaitingForDoubleClick && selectedBox != null)
                                        {
                                            isDragging = true;
                                            isWaitingForDoubleClick = false;
                                            pictureBoxVideo.Invalidate();
                                        }
                                    }));
                                }
                            }, null, 500, Timeout.Infinite);
                            
                            return;
                        }
                        else
                        {
                            // 野껊????獄쏅벡?ゅ첎? ??좎럩?앭뜝?筌앸맩????좎럥?믣뜝???좎럩??
                            isDragging = true;
                            dragOffset = new System.Drawing.Point(e.X - (int)viewRect.X, e.Y - (int)viewRect.Y);
                            UpdateObjectInfo(selectedBox);
                            UpdateBboxListDisplay();
                            HighlightSelectedBoxInSidebar();
                            pictureBoxVideo.Invalidate();
                            return;
                        }
                    }
                }
                
                // ??좎럥諭????좎럥?꿨뜝?獄쏅벡????좎?源???좎럥????좎럥?믣뜝?
                selectedBox = GetBoundingBoxAt(e.Location);

                if (selectedBox != null)
                {
                    isDragging = true;
                    // ???ル슦紐닷뜝?癰궰??좎?釉?獄쏅벡????좎럩??疫꿸낀????좎럥以???좎럥?믣뜝???좎?遊???④쑴沅?
                    var viewRect = ImageToView(new RectangleF(selectedBox.Rectangle.X, selectedBox.Rectangle.Y, 
                        selectedBox.Rectangle.Width, selectedBox.Rectangle.Height));
                    dragOffset = new System.Drawing.Point(e.X - (int)viewRect.X, e.Y - (int)viewRect.Y);
                    UpdateObjectInfo(selectedBox);
                    UpdateBboxListDisplay(); // ??좎?源?????좎럩瑜???좎럥瑗???좎럡由??
                    HighlightSelectedBoxInSidebar(); // ????좎럩瑜???좎럩???좎럥而??좎럩苑???좎?源??獄쏅벡????좎럩???좎럩???
                    pictureBoxVideo.Invalidate();
                }
                else
                {
                    // ?????⑤벀而???좎럥??????좎?源???좎럩??
                    selectedBox = null;
                    ClearSidebarHighlights(); // ??좎럩???좎럩????λ뜃由??
                    UpdateObjectInfo(null); // 揶쏆빘猿???좎럥???λ뜃由??
                    UpdateBboxListDisplay(); // ??좎럩瑜???좎럥瑗???좎럡由??
                    pictureBoxVideo.Invalidate();
                }
            }
        }

        private void pictureBoxVideo_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            // ????좎럥????좎럥?? 野껊????獄쏅벡??????좎럩????좎럥?ュ뜝???좎?源???좎???
            if (currentMode == DrawMode.Select && e.Button == MouseButtons.Left)
            {
                // ??좎럥????좎럥????좎룞???띯뫁????????좎럥????좎럥??
                isWaitingForDoubleClick = false;
                if (doubleClickTimer != null)
                {
                    doubleClickTimer.Dispose();
                    doubleClickTimer = null;
                }
                
                // ??좎럥?믣뜝??띯뫁??
                if (isDragging)
                {
                    isDragging = false;
                }
                
                var clickedBox = GetBoundingBoxAt(e.Location);
                if (clickedBox != null)
                {
                    // 野껊????獄쏅벡?ゅ첎? ??좎럥?쀯쭪? ??좎럩??
                    if (HasAnotherHitCandidateAt(e.Location, clickedBox))
                    {
                        var ordered = GetOrderedCandidatesAt(e.Location);
                        if (ordered.Count > 1)
                        {
                            int idx = ordered.IndexOf(clickedBox);
                            // ??좎럩????좎럥?ュ뜝???좎???
                            BoundingBox next = ordered[(idx + 1) % ordered.Count];

                            if (next != clickedBox)
                            {
                                selectedBox = next;
                                UpdateObjectInfo(selectedBox);
                                UpdateBboxListDisplay();
                                HighlightSelectedBoxInSidebar();
                                pictureBoxVideo.Invalidate();
                            }
                        }
                    }
                }
            }
        }

        private void pictureBoxVideo_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDrawing && drawingBox != null)
            {
                // ?좎떎?쒓낀???좎룞?숉몴?좎룞???좎룞?쇿뜝?숈삕 ?좎뙓?숈삕 泥섇뜝?숈삕
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
                PerformResize(e.Location);
                pictureBoxVideo.Invalidate();
            }
            else if (isWaitingForDoubleClick && selectedBox != null)
            {
                int moveDistance = (int)Math.Sqrt(Math.Pow(e.X - lastClickPoint.X, 2) + Math.Pow(e.Y - lastClickPoint.Y, 2));
                if (moveDistance > 5)
                {
                    isDragging = true;
                    isWaitingForDoubleClick = false;
                    if (doubleClickTimer != null)
                    {
                        doubleClickTimer.Dispose();
                        doubleClickTimer = null;
                    }
                }
            }
            else if (isDragging && selectedBox != null)
            {
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
                var viewRect = ImageToView(new RectangleF(selectedBox.Rectangle.X, selectedBox.Rectangle.Y,
                    selectedBox.Rectangle.Width, selectedBox.Rectangle.Height));
                ResizeHandle handle = GetResizeHandleAtPoint(e.Location, viewRect);
                UpdateCursorForHandle(handle);
            }
            else
            {
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
                bool shouldAddDrawingBox = false;
                if (drawingBox.Rectangle.Width > 10 && drawingBox.Rectangle.Height > 10)
                {
                    bool isFaceBox = string.Equals(drawingBox.Label, "person", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(drawingBox.PersonPartType, "face", StringComparison.OrdinalIgnoreCase);

                    if (isFaceBox)
                    {
                        var matchingBody = FindBestMatchingBodyForFace(drawingBox);
                        if (matchingBody == null)
                        {
                            MessageBox.Show("No valid parent body was found for this face box. Face creation was canceled.", "Face box", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else
                        {
                            drawingBox.LinkedPersonId = matchingBody.PersonId;
                            drawingBox.PersonId = matchingBody.PersonId;
                            drawingBox.BoxEntryFrame = currentFrameIndex;
                            boundingBoxes.Add(drawingBox);
                            shouldAddDrawingBox = true;
                        }
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(drawingBox.PersonPartType))
                        {
                            drawingBox.PersonPartType = drawingBox.Label == "person" ? "body" : null;
                        }

                        boundingBoxes.Add(drawingBox);
                        shouldAddDrawingBox = true;
                    }

                    if (shouldAddDrawingBox)
                    {
                        InvalidateBoxCache();
                        AddUndoAction(new UndoAction { Type = UndoActionType.AddBox, Box = drawingBox });

                        selectedBox = drawingBox;
                        UpdateObjectInfo(selectedBox);
                        UpdateBoxCount();
                        UpdateBboxListDisplay();

                        if (drawingBox.Label == "event")
                        {
                            // no-op
                        }
                    }
                }

                drawingBox = null;
                isDrawing = false;
                pictureBoxVideo.Invalidate();
            }
            else if (isResizing)
            {
                isResizing = false;
                currentResizeHandle = ResizeHandle.None;

                var undoBox = CloneBoundingBox(selectedBox);
                undoBox.Rectangle = originalResizeRect;
                AddUndoAction(new UndoAction { Type = UndoActionType.ModifyBox, Box = undoBox });

                RecordManuallyAdjustedFrame(selectedBox);

                InvalidateBoxCache();
                UpdateObjectInfo(selectedBox);
                UpdateBboxListDisplay();

                if (selectedBox != null && selectedBox.Label == "event")
                {
                    PropagateEventBoxFromCurrentFrame(selectedBox);
                }

                pictureBoxVideo.Cursor = Cursors.Default;
                pictureBoxVideo.Invalidate();
            }
            else if (isDragging || isWaitingForDoubleClick)
            {
                if (isWaitingForDoubleClick && !isDragging)
                {
                    isDragging = true;
                    isWaitingForDoubleClick = false;
                }

                if (isDragging)
                {
                    isDragging = false;

                    if (selectedBox != null)
                    {
                        RecordManuallyAdjustedFrame(selectedBox);
                    }

                    if (selectedBox != null && selectedBox.Label == "event")
                    {
                        PropagateEventBoxFromCurrentFrame(selectedBox);
                    }
                }

                if (doubleClickTimer != null)
                {
                    doubleClickTimer.Dispose();
                    doubleClickTimer = null;
                }
                isWaitingForDoubleClick = false;
            }
        }

        private void pictureBoxVideo_Resize(object sender, EventArgs e)
        {
            // PictureBox ??좎럡由겼첎? 癰궰野껋럥留???????좎럩???좎?遊?Label ??좎럩??鈺곌퀣??(?ル슣瑜???좎럥??
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
            if (panelVideoControls != null && panelVideoControls.Height < TimelineLayoutHelper.GetMinimumVideoControlsHeight())
            {
                panelVideoControls.Height = TimelineLayoutHelper.GetMinimumVideoControlsHeight();
            }

            if (panelVideoControls == null)
            {
                return;
            }

            if (groupBoxObjectInfo != null)
            {
                groupBoxObjectInfo.Location = new System.Drawing.Point(
                    panelVideoControls.Width - groupBoxObjectInfo.Width - 16,
                    16);
            }

            if (panelTimeline != null && groupBoxObjectInfo != null)
            {
                const int reservedGap = 8;
                const int minimumWidth = 100;
                panelTimeline.Width = TimelineLayoutHelper.CalculateTimelineWidth(
                    panelTimeline.Left,
                    groupBoxObjectInfo.Left,
                    reservedGap,
                    minimumWidth);
            }
        }

        private void pictureBoxVideo_Paint(object sender, PaintEventArgs e)
        {
            if (pictureBoxVideo.Image == null)
                return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // ??좎럥??筌ㅼ뮇??? ??좎럩????좎럥???좎럩??獄쏅벡?ゅ뜝?筌?Ŋ???좎럩苑?揶쎛??좎럩?ㅵ뜝?
            if (lastCachedFrameForPaint != currentFrameIndex)
            {
                cachedCurrentFrameBoxes = boundingBoxes.Where(b => b.FrameIndex == currentFrameIndex).ToList();
                lastCachedFrameForPaint = currentFrameIndex;
            }

            // 1??좎럡?? ??좎?源??獄쏅벡?ゅ뜝???좎럩???筌뤴뫀諭?獄쏅벡??域밸챶?곩뜝?
            foreach (var box in cachedCurrentFrameBoxes)
            {
                // ??좎?源??獄쏅벡?????좎럩夷??域밸챶?곮첋???椰꾨?瑗??좎럡由?
                if (box == selectedBox ||
                    (selectedBox != null && box.FrameIndex == currentFrameIndex &&
                     AreDrawingIdentitiesEqual(box, selectedBox)))
                    continue;

                // ??좎럩????좎럥???좎럩??獄쏅벡?ゅ뜝???좎럩??(box.FrameIndex == currentFrameIndex)
                // currentFrameBoxes??좎럩苑???좎룞?? ??좎?苑ｏ쭕怨룸┷??좎럩?앲첋????곕톪?? 筌ｋ똾寃??븍뜇釉??

                // ??좎룞??筌왖 ?ル슦紐닷뜝????ル슦紐닷뜝?癰궰??
                var viewRect = ImageToView(new RectangleF(box.Rectangle.X, box.Rectangle.Y, 
                    box.Rectangle.Width, box.Rectangle.Height));

                Color boxColor = GetColorForLabel(box.Label);
                
                // ?????좎룞???獄쏅벡?????좎럡????좎럡苡???좎럩??
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
                    // ??좎럩湲?獄쏅벡???疫꿸퀣??嚥≪뮇彛??좎룞??
                    using (Pen pen = new Pen(boxColor, 3))
                    {
                        g.DrawRectangle(pen, viewRect.X, viewRect.Y, viewRect.Width, viewRect.Height);
                    }

                    // ??좎럥爰???좎럩?????좎럩苑?(??좎럥??筌ㅼ뮇??? 筌?Ŋ???獄쏄퀣肉???좎럩??
                    string labelText = GetBoxLabelText(box);
                    
                    // ??좎럥??筌ㅼ뮇??? ??좎럩沅??揶쎛??좎?釉?Font ??좎럩??
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
                }
            }

            // 2??좎럡?? ??좎?源??獄쏅벡?ゅ뜝?筌띾뜉??筌띾맩肉?域밸챶?곩뜝?(??좎럩????좎럩肉???좎럩??
            if (selectedBox != null && selectedBox.FrameIndex == currentFrameIndex)
            {
                // ??좎룞??筌왖 ?ル슦紐닷뜝????ル슦紐닷뜝?癰궰??
                var viewRect = ImageToView(new RectangleF(selectedBox.Rectangle.X, selectedBox.Rectangle.Y, 
                    selectedBox.Rectangle.Width, selectedBox.Rectangle.Height));

                Color boxColor = GetColorForLabel(selectedBox.Label);
                
                // ?????좎룞???獄쏅벡?????좎럡????좎럡苡???좎럩??
                if (selectedBox.IsDeleted)
                {
                    Color fadedColor = Color.FromArgb(150, boxColor.R, boxColor.G, boxColor.B);
                    using (Pen pen = new Pen(fadedColor, 2))
                    {
                        g.DrawRectangle(pen, viewRect.X, viewRect.Y, viewRect.Width, viewRect.Height);
                    }
                }
                else
                {
                    // ??좎럩湲?獄쏅벡???疫꿸퀣??嚥≪뮇彛??좎룞??(??좎?源??獄쏅벡???????좎럡蹂????
                    using (Pen pen = new Pen(boxColor, 5))
                    {
                        g.DrawRectangle(pen, viewRect.X, viewRect.Y, viewRect.Width, viewRect.Height);
                    }

                    // ??좎럥爰???좎럩?????좎럩苑?(??좎럥??筌ㅼ뮇??? 筌?Ŋ???獄쏄퀣肉???좎럩??
                    string labelText = GetBoxLabelText(selectedBox);
                    
                    // ??좎럥??筌ㅼ뮇??? ??좎럩沅??揶쎛??좎?釉?Font ??좎럩??
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
                    
                    // ????좎?源??獄쏅벡?????좎럡由?鈺곌퀣????좎럥諭???좎럩??(4????좎룞????
                    DrawResizeHandles(g, viewRect);
                }
            }

            if (isDrawing && drawingBox != null)
            {
                // ??좎룞??筌왖 ?ル슦紐닷뜝????ル슦紐닷뜝?癰궰??
                var viewRect = ImageToView(new RectangleF(drawingBox.Rectangle.X, drawingBox.Rectangle.Y, 
                    drawingBox.Rectangle.Width, drawingBox.Rectangle.Height));

                Color boxColor = GetColorForLabel(drawingBox.Label);
                using (Pen pen = new Pen(boxColor, 3) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(pen, viewRect.X, viewRect.Y, viewRect.Width, viewRect.Height);
            }

            // ??YOLO ??좎룞?? 獄쏅벡????좎럩??(??좎룞?????녹뮇議???좎럩????좎럥彛?
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
                                
                                // ??좎룞??筌왖 ?ル슦紐닷뜝????ル슦紐닷뜝?癰궰??
                                var viewRect = ImageToView(new RectangleF(detection.Rectangle.X, detection.Rectangle.Y,
                                    detection.Rectangle.Width, detection.Rectangle.Height));

                                // ??좎?????ル슦紐??좎룞?? ??좎럩??
                                if (viewRect.Width <= 0 || viewRect.Height <= 0 || 
                                    viewRect.X < -1000 || viewRect.Y < -1000 || 
                                    viewRect.X > 10000 || viewRect.Y > 10000)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YOLO ??좎룞?? ??좎럥?묈뜝? ??좎????좎룞?? ??좎룞?? ?ル슦紐? {viewRect}");
                                    continue;
                                }

                                // ??YOLO ??좎룞?? 獄쏅벡?????좎럥??獄쏅뗄???獄쏅벡??????좎럩?????좎럡???좎럥由???좎룞?? ??좎럡?▼뜝???좎럩??
                                // ??좎럥爰??COCO ??좎럩???좎럩苑???좎?逾녺뵳?????좎럩????좎럩???좎럥以?癰궰??
                                string appLabel = ConvertCocoLabelToAppLabel(detection.Label);
                                Color boxColor = GetColorForLabel(appLabel);
                                
                                // ??좎룞?? ??좎럡??1px)????좎럩???좎럩肉??닌됲뀋 揶쎛??좎?釉?뜝?
                                using (Pen pen = new Pen(boxColor, 1))
                                {
                                    g.DrawRectangle(pen, viewRect.X, viewRect.Y, viewRect.Width, viewRect.Height);
                                }

                                // ??좎럥爰???좎럩?????좎럩??(??좎럩沅??揶쎛??좎?釉?Font ??좎럩??
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

                                            // ??좎럥????좎럩湲??獄쏆꼹?얍뜝?獄쏄퀗瑗???좎럩??
                                            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(200, boxColor)))
                                                g.FillRectangle(bgBrush, labelBg);

                                            using (SolidBrush textBrush = new SolidBrush(Color.White))
                                                g.DrawString(labelText, yoloDetectionFont, textBrush, viewRect.X + 2, viewRect.Y - textSize.Height);
                                        }
                                    }
                                    catch (Exception textEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[YOLO ??좎룞?? ??좎럥?묈뜝? ??좎럩?????좎럩????좎럥履? {textEx.Message}");
                                    }
                                }
                            }
                            catch (Exception detectionEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"[YOLO ??좎룞?? ??좎럥?묈뜝? 揶쏆뮆??獄쏅벡????좎럥?묈뜝???좎럥履? {detectionEx.Message}");
                                // 揶쏆뮆??獄쏅벡????좎럥履???④쑴??筌욊쑵六?
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[YOLO ??좎룞?? ??좎럥?묈뜝???좎럥履? {ex.Message}\n{ex.StackTrace}");
                    // ??좎럥?묈뜝???좎럥履????좎럩猿?UI????좎?堉?雅뚯눦?? ??좎럥猷꾢뜝??얜똻??
                }
            }

            // ??Skeleton ??좎럥?묈뜝?
            if (showSkeleton)
            {
                try
                {
                    var skeletonConnections = GetSkeletonConnections();
                    Color skeletonColor = Color.Yellow;

                    foreach (var box in cachedCurrentFrameBoxes)
                    {
                        if (box.Skeleton3D == null || box.Skeleton3D.Count == 0)
                            continue;

                        try
                        {
                            float imageWidth = pictureBoxVideo.Image.Width;
                            float imageHeight = pictureBoxVideo.Image.Height;
                            var imagePoints = ConvertSkeletonJointsToImagePoints(
                                box.Skeleton3D,
                                box.Rectangle,
                                imageWidth,
                                imageHeight);

                            // ?온????域밸챶?곩뜝?
                            for (int i = 0; i < box.Skeleton3D.Count; i++)
                            {
                                var imagePoint = imagePoints[i];
                                if (!imagePoint.HasValue)
                                    continue;

                                // ??좎룞??筌왖 ?ル슦紐닷뜝????ル슦紐닷뜝?癰궰??
                                var viewPoint = ImageToView(imagePoint.Value);

                                // ?온????域밸챶?곩뜝?(??좎???
                                using (SolidBrush brush = new SolidBrush(skeletonColor))
                                {
                                    g.FillEllipse(brush, viewPoint.X - 3, viewPoint.Y - 3, 6, 6);
                                }
                            }

                            // ?온????좎럡猿??域밸챶?곩뜝?
                            using (Pen pen = new Pen(skeletonColor, 2))
                            {
                                foreach (var (start, end) in skeletonConnections)
                                {
                                    if (start >= box.Skeleton3D.Count || end >= box.Skeleton3D.Count)
                                        continue;

                                    var startImagePoint = imagePoints[start];
                                    var endImagePoint = imagePoints[end];

                                    if (!startImagePoint.HasValue || !endImagePoint.HasValue)
                                        continue;

                                    // ??좎룞??筌왖 ?ル슦紐닷뜝????ル슦紐닷뜝?癰궰??
                                    var startPoint = ImageToView(startImagePoint.Value);
                                    var endPoint = ImageToView(endImagePoint.Value);

                                    // ??좎럡猿??域밸챶?곩뜝?
                                    g.DrawLine(pen, startPoint, endPoint);
                                }
                            }
                        }
                        catch (Exception skeletonBoxEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Skeleton ??좎럥?묈뜝? 揶쏆뮆??獄쏅벡????좎럥?묈뜝???좎럥履? {skeletonBoxEx.Message}");
                        }
                    }
                }
                catch (Exception skeletonEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[Skeleton ??좎럥?묈뜝???좎럥履? {skeletonEx.Message}\n{skeletonEx.StackTrace}");
                }
            }
        }

        // ????좎???獄쏅벡????좎럥????좎럩??
        private bool IsTrackingFailed(BoundingBox box)
        {
            string key = GetDrawingIdentityKey(box);
            if (!waypointFailureRanges.ContainsKey(key))
                return false;
            
            return waypointFailureRanges[key].Any(range => 
                box.FrameIndex >= range.start && box.FrameIndex <= range.end);
        }
        
        // ????좎럩瑜???좎럩???좎럥而??좎럩苑???좎?源??獄쏅벡????좎럩???좎럩???
        private void HighlightSelectedBoxInSidebar()
        {
            if (selectedBox == null) return;
            
            // 筌뤴뫀諭???좎럥瑗????좎럩???좎럩????λ뜃由??
            ClearSidebarHighlights();
            
            // ??좎?源??獄쏅벡?????좎럥爰????좎럥????좎럥????좎럥瑗??좎럩苑???좎럩???좎럩???
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
        
        // ????좎럥瑗??좎럩苑???좎럩??獄쏅벡????좎럩???좎럩???
        private void HighlightBoxInPanel(Panel panel, BoundingBox targetBox)
        {
            // 燁삳똾?믤⑥쥓?곩뜝???좎럩???좎럩?????좎럩湲?野껉퀣??
            Color highlightColor;
            if (panel == panelPersonList)
                highlightColor = Color.FromArgb(252, 231, 243); // ??좎?釉??브쑵???
            else if (panel == panelVehicleList)
                highlightColor = Color.FromArgb(219, 234, 254); // ??좎?釉???좎룞????
            else if (panel == panelEventList)
                highlightColor = Color.FromArgb(220, 252, 231); // ??좎?釉???좎럩源?
            else
                highlightColor = Color.FromArgb(200, 255, 200); // 疫꿸퀡????좎?釉???좎럩源?
            
            foreach (Control ctrl in panel.Controls)
            {
                if (ctrl is Panel itemPanel)
                {
                    // ??좎럥瑗??Tag??좎럩苑?獄쏅벡????좎럥??揶쎛??좎럩?ㅵ뜝?
                    if (itemPanel.Tag is BoundingBox box && box == targetBox)
                    {
                        itemPanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
                        itemPanel.BackColor = highlightColor; // 燁삳똾?믤⑥쥓?곩뜝???좎럩湲??좎럥以???좎럩???좎럩???
                    }
                    else
                    {
                        itemPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
                        itemPanel.BackColor = SystemColors.Control;
                    }
                }
            }
        }
        
        // ????좎럩???좎럥而???좎럩???좎럩????λ뜃由??
        private void ClearSidebarHighlights()
        {
            ClearPanelHighlights(panelPersonList);
            ClearPanelHighlights(panelVehicleList);
            ClearPanelHighlights(panelEventList);
        }
        
        // ????좎럥瑗???좎럩???좎럩????λ뜃由??
        private void ClearPanelHighlights(Panel panel)
        {
            // 燁삳똾?믤⑥쥓?곩뜝?疫꿸퀡????좎럩湲?野껉퀣??
            Color defaultColor;
            if (panel == panelPersonList)
                defaultColor = Color.FromArgb(252, 231, 243); // ??좎?釉??브쑵???
            else if (panel == panelVehicleList)
                defaultColor = Color.FromArgb(219, 234, 254); // ??좎?釉???좎룞????
            else if (panel == panelEventList)
                defaultColor = Color.FromArgb(220, 252, 231); // ??좎?釉???좎럩源?
            else
                defaultColor = SystemColors.Control; // 疫꿸퀡????좎럩湲?
            
            foreach (Control ctrl in panel.Controls)
            {
                if (ctrl is Panel itemPanel)
                {
                    itemPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
                    itemPanel.BackColor = defaultColor; // 燁삳똾?믤⑥쥓?곩뜝?疫꿸퀡????좎럩湲??좎럥以?癰귣벊??
                }
            }
        }
        
        private System.Drawing.Point lastClickViewPoint;
        private List<BoundingBox> lastHitCandidates = new List<BoundingBox>();
        private int lastHitIndex = -1;

        private BoundingBox GetBoundingBoxAt(System.Drawing.Point location)
        {
            // ???ル슦紐닷뜝???좎룞??筌왖 ?ル슦紐닷뜝?癰궰??
            var imageLocation = ViewToImage(new PointF(location.X, location.Y));
            
            // ????좎럩????좎럥???좎럩肉???좎럥???좎럥??獄쏅벡???좎럩????좎?苑ｅ뜝?(???좎룞???좎룞?? ??좎룞?? 獄쏅벡?ゅ뜝?
            var currentFrameBoxes = boundingBoxes.Where(b => b.FrameIndex == currentFrameIndex && !b.IsDeleted).ToList();

            // ????좎?????좎럩???筌띾뜆彛???좎룞??筌왖 ?ル슦紐?疫꿸낀??)??좎럥以?野껋럡????좎럥????좎럩??
            const int hitMargin = 4;

            // ??좎럥????좎럩彛?
            var candidates = new List<(BoundingBox box, bool inActiveWaypoint, int labelPri, double dist, int area, int zIndex)>();

            foreach (var box in currentFrameBoxes)
            {
                // ??좎?源???좎럡???좎럩苑??Waypoint 甕곕뗄????좎?釉????좎럩???좎룞?? ??좎럩??(??좎럩彛???좎럩肉됧뜝???좎?釉?

                // ??좎??껓쭕?됱춭 ??좎럩?????좎룞??筌왖 ?ル슦紐?????좎?釉???좎룞??
                var r = box.Rectangle;
                r.Inflate(hitMargin, hitMargin);
                if (!r.Contains((int)imageLocation.X, (int)imageLocation.Y))
                    continue;

                bool inActiveWaypoint = false;
                if (selectedWaypoint != null)
                {
                    inActiveWaypoint = (IsBoxInWaypoint(box, selectedWaypoint) &&
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
                int zIndex = boundingBoxes.IndexOf(box); // ????좎럩??????좎럥???????좎럥?????좎럩肉???좎럥?롥뜝?揶쎛??

                candidates.Add((box, inActiveWaypoint, labelPri, dist, area, zIndex));
            }

            if (candidates.Count == 0)
                return null;

            // ??揶쎛餓λ쵐??疫꿸퀡而???좎럥?? ??좎럩苑???좎럩???좎럩???> ??좎럥爰???좎럩苑??좎럩??> 餓λ쵐?뽩쳞怨뺚봺??> 筌롫똻?????좎룞?? ????좎럩苑? > Z-Order
            var ordered = candidates
                .OrderByDescending(c => c.inActiveWaypoint)
                .ThenByDescending(c => c.labelPri)
                .ThenBy(c => c.dist)
                .ThenBy(c => c.area)
                .ThenByDescending(c => c.zIndex)
                .ToList();

            // ??좎?源???좎럩???좎럥彛????좎?鍮???좎럥??????좎럥????좎럩??????
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

        // ??좎럩????좎럥??筌왖??좎럩肉???좎?源??獄쏅벡????좎럩????좎럥????좎럥?ュ첎? 鈺곕똻???좎럥?쀯쭪? 野꺜??
        private bool HasAnotherHitCandidateAt(System.Drawing.Point viewLocation, BoundingBox exclude)
        {
            var imageLocation = ViewToImage(new PointF(viewLocation.X, viewLocation.Y));
            var currentFrameBoxes = boundingBoxes.Where(b => b.FrameIndex == currentFrameIndex && !b.IsDeleted);
            const int hitMargin = 4;

            foreach (var box in currentFrameBoxes)
            {
                if (box == exclude || AreDrawingIdentitiesEqual(box, exclude)) continue;
                // ??좎?源???좎럡???좎럩苑??Waypoint 甕곕뗄????좎?釉????좎럩???좎룞?? ??좎럩??

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
                // ??좎?源???좎럡???좎럩苑??Waypoint 甕곕뗄????좎?釉????좎럩???좎룞?? ??좎럩??

                var r = box.Rectangle;
                r.Inflate(hitMargin, hitMargin);
                if (!r.Contains((int)imageLocation.X, (int)imageLocation.Y))
                    continue;

                bool inActiveWaypoint = false;
                if (selectedWaypoint != null)
                {
                    inActiveWaypoint = (IsBoxInWaypoint(box, selectedWaypoint) &&
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

        // ?占쎈좂 ?占쎈깮(exclude)???占쎌쇅?占쎄퀬 ?占쎌씪 吏?占쎌쓽 理쒖쟻 ?占쎈낫 諛섑솚
        private BoundingBox GetBestCandidateAtExcluding(System.Drawing.Point viewLocation, BoundingBox exclude)
        {
            var imageLocation = ViewToImage(new PointF(viewLocation.X, viewLocation.Y));
            var currentFrameBoxes = boundingBoxes.Where(b => b.FrameIndex == currentFrameIndex && !b.IsDeleted).ToList();
            const int hitMargin = 2;

            var candidates = new List<(BoundingBox box, bool inActiveWaypoint, int labelPri, double dist, int area, int zIndex)>();

            foreach (var box in currentFrameBoxes)
            {
                if (exclude != null && AreDrawingIdentitiesEqual(box, exclude))
                    continue;

                var waypoint = waypointMarkers.FirstOrDefault(w => IsBoxInWaypoint(box, w));

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
                    inActiveWaypoint = (IsBoxInWaypoint(box, selectedWaypoint) &&
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

        // Tab/Shift+Tab ??좎?源???좎럩???좎럥彛?
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

        // ????좎럡由?鈺곌퀣????좎럥諭?域밸챶?곩뜝?(4??筌뤴뫁苑뚨뵳?彛?
        private void DrawResizeHandles(Graphics g, RectangleF rect)
        {
            Color handleColor = Color.White;
            Color borderColor = Color.Black;
            
            // 4??筌뤴뫁苑뚦뜝??ル슦紐??④쑴沅?
            PointF topLeft = new PointF(rect.X, rect.Y);
            PointF topRight = new PointF(rect.X + rect.Width, rect.Y);
            PointF bottomLeft = new PointF(rect.X, rect.Y + rect.Height);
            PointF bottomRight = new PointF(rect.X + rect.Width, rect.Y + rect.Height);
            
            // ??좎럥諭?域밸챶?곩뜝?
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

        // ??Person 獄쏅벡????좎럩苑???좎럥????좎럩??
        private string FormatAttributeValueForDisplay(object value)
        {
            if (value == null)
                return "";
            
            // 獄쏄퀣肉??귐딅뮞??좎럩??野껋럩??筌ｌ꼶??
            if (value is List<string> listValue)
            {
                if (listValue.Count == 0)
                    return "";
                // ??좎럡???좎럥以?癰궰??좎?釉????좎럩??
                var koreanValues = listValue.Select(v => PersonAttributesForm.GetAttributeValueKorean(v)).ToList();
                return string.Join(", ", koreanValues);
            }
            else if (value is string[] arrayValue)
            {
                if (arrayValue.Length == 0)
                    return "";
                var koreanValues = arrayValue.Select(v => PersonAttributesForm.GetAttributeValueKorean(v)).ToList();
                return string.Join(", ", koreanValues);
            }
            else if (value is string stringValue)
            {
                // ??좎럩??揶쏅???野껋럩????좎럡???좎럥以?癰궰??
                return PersonAttributesForm.GetAttributeValueKorean(stringValue);
            }
            else
            {
                // 疫꿸낀?? ????좎룞?? ?얜챷???좎럥以?癰궰??????좎럡???癰궰????좎럥猷?
                string strValue = value.ToString();
                return PersonAttributesForm.GetAttributeValueKorean(strValue);
            }
        }

        private void DrawPersonAttributes(Graphics g, BoundingBox box, RectangleF viewRect)
        {
            try
            {
                // ??좎럩苑?揶쎛??좎럩?ㅵ뜝?
                var attributes = personAttributeStore.GetAllAttributes(box.PersonId, box.FrameIndex, waypointMarkers);
                if (attributes == null || attributes.Count == 0)
                    return;

                // 揶쏅?????좎럥????좎럩苑?뜝???좎?苑ｅ뜝?
                var nonNullAttributes = attributes.Where(kvp => kvp.Value != null).ToList();
                if (nonNullAttributes.Count == 0)
                    return;

                // ??좎럩苑???좎럩?????좎럩苑?(??좎럡???좎럥以???좎럩??
                var attributeTexts = nonNullAttributes.Select(kvp => 
                {
                    string attrName = kvp.Key;
                    string formattedValue = FormatAttributeValueForDisplay(kvp.Value);
                    
                    return $"{attrName}:{formattedValue}";
                }).ToList();

                if (attributeTexts.Count == 0)
                    return;

                // ??좎럩?????좎럡由?筌β돦??
                string sampleText = attributeTexts[0];
                SizeF textSize = g.MeasureString(sampleText, attributeFont);
                float lineHeight = textSize.Height + 2; // ??揶쏄쑨爰?
                float totalHeight = lineHeight * attributeTexts.Count;
                float maxWidth = attributeTexts.Max(t => g.MeasureString(t, attributeFont).Width);

                // ??좎럥?⑨쭫?뚮퓠 ??좎럩????⑤벀而???좎럩??
                float rightSpace = pictureBoxVideo.Width - (viewRect.X + viewRect.Width);
                float leftSpace = viewRect.X;
                bool showOnRight = rightSpace >= maxWidth + 10; // ??좎럩? ?⑤벀而???좎?釉?

                float startX = showOnRight ? viewRect.X + viewRect.Width + 5 : viewRect.X - maxWidth - 5;
                float startY = viewRect.Y;

                // 獄쏄퀗瑗?域밸챶?곩뜝?
                RectangleF bgRect = new RectangleF(
                    startX - 2,
                    startY - 2,
                    maxWidth + 4,
                    totalHeight + 4
                );

                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(220, Color.Black)))
                    g.FillRectangle(bgBrush, bgRect);

                // ??좎럩苑???좎럩???域밸챶?곩뜝?
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
                // ??좎럩苑???좎럩??????좎럥履?獄쏆뮇源???鈺곌퀣???筌ｌ꼶??
                System.Diagnostics.Debug.WriteLine($"[DrawPersonAttributes ??좎럥履? {ex.Message}");
            }
        }
        
        // ??筌띾뜆?????좎럩???좎럩苑???좎럡由?鈺곌퀣????좎럥諭?揶쏅㉡?? (4??筌뤴뫁苑뚨뵳?彛?
        private ResizeHandle GetResizeHandleAtPoint(System.Drawing.Point viewPoint, RectangleF viewRect)
        {
            float tolerance = HANDLE_SIZE / 2f + 2; // ??좎럥????좎럩??甕곕뗄??
            
            // ?ル슣湲??筌뤴뫁苑뚦뜝?
            PointF topLeft = new PointF(viewRect.X, viewRect.Y);
            if (Distance(viewPoint, topLeft) <= tolerance)
                return ResizeHandle.TopLeft;
            
            // ??좎럩湲??筌뤴뫁苑뚦뜝?
            PointF topRight = new PointF(viewRect.X + viewRect.Width, viewRect.Y);
            if (Distance(viewPoint, topRight) <= tolerance)
                return ResizeHandle.TopRight;
            
            // ?ル슦釉??筌뤴뫁苑뚦뜝?
            PointF bottomLeft = new PointF(viewRect.X, viewRect.Y + viewRect.Height);
            if (Distance(viewPoint, bottomLeft) <= tolerance)
                return ResizeHandle.BottomLeft;
            
            // ??좎?釉??筌뤴뫁苑뚦뜝?
            PointF bottomRight = new PointF(viewRect.X + viewRect.Width, viewRect.Y + viewRect.Height);
            if (Distance(viewPoint, bottomRight) <= tolerance)
                return ResizeHandle.BottomRight;
            
            return ResizeHandle.None;
        }
        
        // ??????좎럩???椰꾧퀡???④쑴沅?
        private float Distance(PointF p1, PointF p2)
        {
            float dx = p1.X - p2.X;
            float dy = p1.Y - p2.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
        
        // ????좎럡由?鈺곌퀣????좎?六?(10x10 筌ㅼ뮇????좎럡由???좎럩?? 筌뤴뫁苑뚨뵳?以???좎럥????좎럩????좎럩??鈺곌퀣??
        private void PerformResize(System.Drawing.Point currentViewPoint)
        {
            if (selectedBox == null || currentResizeHandle == ResizeHandle.None)
                return;
            
            // ???ル슦紐?筌△뫁???④쑴沅?
            int deltaX = currentViewPoint.X - resizeStartPoint.X;
            int deltaY = currentViewPoint.Y - resizeStartPoint.Y;
            
            // ???ル슦紐?筌△뫁?졾뜝???좎룞??筌왖 ?ル슦紐?筌△뫁?졾뜝?癰궰??
            var viewDelta = new PointF(deltaX, deltaY);
            var imageDelta = ViewToImageDistance(viewDelta);
            
            // ??좎럥??Rectangle????좎럩??
            Rectangle newRect = originalResizeRect;
            
            switch (currentResizeHandle)
            {
                case ResizeHandle.TopLeft:
                    // ?ル슣湲??筌뤴뫁苑뚦뜝? X, Y, Width, Height 筌뤴뫀紐?癰궰??
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
                        // 筌ㅼ뮇????좎럡由???좎룞??
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
                    // ??좎럩湲??筌뤴뫁苑뚦뜝? Y, Width, Height 癰궰??
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
                    // ?ル슦釉??筌뤴뫁苑뚦뜝? X, Width, Height 癰궰??
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
                    // ??좎?釉??筌뤴뫁苑뚦뜝? Width, Height??癰궰??
                    newWidth = originalResizeRect.Width + (int)imageDelta.X;
                    newHeight = originalResizeRect.Height + (int)imageDelta.Y;
                    
                    newRect.Width = Math.Max(newWidth, MIN_BBOX_SIZE);
                    newRect.Height = Math.Max(newHeight, MIN_BBOX_SIZE);
                    break;
            }
            
            selectedBox.Rectangle = newRect;
        }
        
        // ?????ル슦紐?椰꾧퀡?곩뜝???좎룞??筌왖 ?ル슦紐?椰꾧퀡?곩뜝?癰궰??
        private PointF ViewToImageDistance(PointF viewDistance)
        {
            if (pictureBoxVideo.Image == null)
                return viewDistance;
            
            float scaleX = (float)pictureBoxVideo.Image.Width / pictureBoxVideo.ClientSize.Width;
            float scaleY = (float)pictureBoxVideo.Image.Height / pictureBoxVideo.ClientSize.Height;
            
            return new PointF(viewDistance.X * scaleX, viewDistance.Y * scaleY);
        }
        
        // ????좎럥諭????좎럥???뚣끉苑?癰궰??(筌뤴뫁苑뚨뵳?????좎럡而???뚣끉苑?
        private void UpdateCursorForHandle(ResizeHandle handle)
        {
            pictureBoxVideo.Cursor = handle switch
            {
                ResizeHandle.TopLeft => Cursors.SizeNWSE,      // ??좎뜦???ル슣湲???좎?釉???좎럡而??
                ResizeHandle.BottomRight => Cursors.SizeNWSE,  // ??좎뜦???ル슣湲???좎?釉???좎럡而??
                ResizeHandle.TopRight => Cursors.SizeNESW,     // ??좎뜦????좎럩湲??ル슦釉???좎럡而??
                ResizeHandle.BottomLeft => Cursors.SizeNESW,   // ??좎뜦????좎럩湲??ル슦釉???좎럡而??
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

        // ??COCO ??좎럥爰????좎?逾녺뵳?????좎럩????좎럥爰쇔뜝?癰궰??(car, motorcycle, bus ????vehicle)
        private string ConvertCocoLabelToAppLabel(string cocoLabel)
        {
            if (string.IsNullOrEmpty(cocoLabel))
                return "person"; // 疫꿸퀡??뜝?
            
            string lowerLabel = cocoLabel.ToLower();
            
            // Person ?온??
            if (lowerLabel == "person")
                return "person";
            
            // Vehicle ?온??(car, motorcycle, bus, truck ??
            if (lowerLabel == "car" || lowerLabel == "motorcycle" || lowerLabel == "bus" || 
                lowerLabel == "truck" || lowerLabel == "bicycle" || lowerLabel == "train" ||
                lowerLabel == "boat" || lowerLabel == "airplane")
                return "vehicle";
            
            // Event ?온??좎룞?? 疫꿸퀡???좎럩?앭뜝?event????좎룞??
            if (lowerLabel == "event")
                return "event";
            
            // 疫꿸퀡??첎誘?삕? person
            return "person";
        }

        private void UpdateBoxCount()
        {
            // ?????좎룞???좎룞?? ??좎룞?? 獄쏅벡?ゅ뜝?燁삳똻???
            int activeCount = boundingBoxes.Count(b => !b.IsDeleted);
            labelBoxCount.Text = $"獄쏅벡??揶쏆뮇?? {activeCount}";
        }

        // ????좎럥????좎럥?????좎럩??JSON ??좎럩?ゅ뜝???좎럩??
        private void UpdateCurrentJsonFileLabel()
        {
            if (labelCurrentJsonFile == null) return;

            if (!string.IsNullOrEmpty(currentJsonFile))
            {
                string fileName = Path.GetFileName(currentJsonFile);
                labelCurrentJsonFile.Text = $"??좎룞??{fileName}";
            }
            else
            {
                labelCurrentJsonFile.Text = "";
            }
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
                
                // Label癰귢쑬以?category name ??좎럩??
                if (waypoint.Label == "person")
                {
                    // ??waypoint??ObjectId??筌욊낯????좎럩??(PersonId揶쎛 ??좎룞?? ????좎럥由????좎럩??
                    string categoryName = GetCategoryName("person", waypoint.ObjectId);
                    item.SubItems.Add(categoryName);
                
                    item.ForeColor = waypoint.MarkerColor;
                    item.Tag = waypoint;
                    listViewPersonWaypoints.Items.Add(item);
                }
                else if (waypoint.Label == "vehicle")
                {
                    // Vehicle: ??좎럩????좎럥???좎럩??Vehicle category name ??좎럩??
                    var vehicleBox = boundingBoxes
                        .FirstOrDefault(b => b.Label == "vehicle" && b.FrameIndex == waypoint.EntryFrame);
                    
                    if (vehicleBox != null)
                    {
                        // ???⑥쥙? 甕곕뜇????좎럩???좎럥以???좎럩??(car, motorcycle, e_scooter, bicycle)
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
                    // Event: [Event, Frame Time, 揶쏆빘猿?P/V)] ??좎럩???좎럥以???좎럩??
                    var eventBox = boundingBoxes
                        .FirstOrDefault(b => b.Label == "event" && 
                                           b.FrameIndex >= waypoint.EntryFrame && 
                                           b.FrameIndex <= waypoint.ExitFrame);

                    string eventName = "contact";
                    if (eventBox != null)
                    {
                        eventName = GetCategoryName("event", eventBox.EventId);
                    }

                    // ???뚎됱쓥: Event ??좎럥已?
                    item = new ListViewItem(eventName);
                    // ??甕곕뜆?? timestamp (JSON images[].timestamp??좎럩苑?癰귣벊?? ??좎럩?앭뜝???좎럥彛?EntryTime)
                    string ts = null;
                    if (frameTimestampMap.TryGetValue(waypoint.EntryFrame, out var jsonTs))
                        ts = jsonTs;
                    if (string.IsNullOrEmpty(ts))
                        ts = GetSubtitleTimestampForFrame(waypoint.EntryFrame);
                    item.SubItems.Add(!string.IsNullOrEmpty(ts) ? ts : waypoint.EntryTime);
                    // ??甕곕뜆?? 揶쏆빘猿?P/V) ??좎럩???
                    item.SubItems.Add(waypoint.InteractingObject ?? "");

                    item.ForeColor = waypoint.MarkerColor;
                    item.Tag = waypoint;
                    listViewEventWaypoints.Items.Add(item);
                }
            }
            
            // ??Waypoint ??좎럩肉???좎럥????좎럥瑗???좎럩????좎럩??鈺곌퀣??
            UpdateWaypointPanelHeights();
        }
        
        // ??Waypoint ??좎럥瑗???좎럩?졾뜝???좎럩???좎럥以?鈺곌퀣??
        private void UpdateWaypointPanelHeights()
        {
            const int MAX_LISTVIEW_HEIGHT = 220; // 筌ㅼ뮋?? ListView ??좎럩??
            const int MAX_GROUPBOX_HEIGHT = 250; // 筌ㅼ뮋?? GroupBox ??좎럩??
            const int ITEM_HEIGHT = 23; // ListView ???좎룞????좎럩??(????
            const int HEADER_HEIGHT = 23; // ListView ??좎럥????좎럩??(???좎룞????좎럩??????좎럩???좎럡苡?
            const int PADDING = 30; // GroupBox ??좎룞?? ??좎럥媛?(??좎럥??25 + ??좎럥??5)
            const int MIN_VISIBLE_ITEMS = 3; // 筌ㅼ뮇??癰귣똻肉у뜝????좎룞????
            
            // Person Waypoint ??좎럥瑗???좎럩??鈺곌퀣??
            int personItemCount = listViewPersonWaypoints.Items.Count;
            int personListViewHeight;
            if (personItemCount == 0)
            {
                // ??waypoint揶쎛 ??좎럩堉??3?????좎룞???癰귣똻????⑤벀而???좎럩??
                personListViewHeight = HEADER_HEIGHT + (MIN_VISIBLE_ITEMS * ITEM_HEIGHT);
            }
            else if (personItemCount < MIN_VISIBLE_ITEMS)
            {
                // 3??沃섎챶彛????좎럥??筌ㅼ뮇??3揶쏆뮋?? 癰귣똻?????좎럡由겼뜝???좎럩??(??좎럥??+ 3?????좎룞??
                personListViewHeight = HEADER_HEIGHT + (MIN_VISIBLE_ITEMS * ITEM_HEIGHT);
            }
            else
            {
                // 3????좎럩湲????좎럥????좎럩?????좎룞????좎럩肉?筌띿쉳苡???좎럩????좎럩?? 筌ㅼ뮋??????좎?釉?
                personListViewHeight = Math.Min(HEADER_HEIGHT + (personItemCount * ITEM_HEIGHT), MAX_LISTVIEW_HEIGHT);
            }
            
            int personGroupBoxHeight = personListViewHeight + PADDING;
            personGroupBoxHeight = Math.Min(personGroupBoxHeight, MAX_GROUPBOX_HEIGHT);
            
            listViewPersonWaypoints.Height = personListViewHeight;
            groupBoxPersonWaypoint.Height = personGroupBoxHeight;
            
            // Vehicle Waypoint ??좎럥瑗???좎럩??鈺곌퀣??
            int vehicleItemCount = listViewVehicleWaypoints.Items.Count;
            int vehicleListViewHeight;
            if (vehicleItemCount == 0)
            {
                // ??waypoint揶쎛 ??좎럩堉??3?????좎룞???癰귣똻????⑤벀而???좎럩??
                vehicleListViewHeight = HEADER_HEIGHT + (MIN_VISIBLE_ITEMS * ITEM_HEIGHT);
            }
            else if (vehicleItemCount < MIN_VISIBLE_ITEMS)
            {
                // 3??沃섎챶彛????좎럥??筌ㅼ뮇??3揶쏆뮋?? 癰귣똻?????좎럡由겼뜝???좎럩??(??좎럥??+ 3?????좎룞??
                vehicleListViewHeight = HEADER_HEIGHT + (MIN_VISIBLE_ITEMS * ITEM_HEIGHT);
            }
            else
            {
                // 3????좎럩湲????좎럥????좎럩?????좎룞????좎럩肉?筌띿쉳苡???좎럩????좎럩?? 筌ㅼ뮋??????좎?釉?
                vehicleListViewHeight = Math.Min(HEADER_HEIGHT + (vehicleItemCount * ITEM_HEIGHT), MAX_LISTVIEW_HEIGHT);
            }
            
            int vehicleGroupBoxHeight = vehicleListViewHeight + PADDING;
            vehicleGroupBoxHeight = Math.Min(vehicleGroupBoxHeight, MAX_GROUPBOX_HEIGHT);
            
            listViewVehicleWaypoints.Height = vehicleListViewHeight;
            groupBoxVehicleWaypoint.Height = vehicleGroupBoxHeight;
            
            // Event Waypoint ??좎럥瑗???좎럩??鈺곌퀣??
            int eventItemCount = listViewEventWaypoints.Items.Count;
            int eventListViewHeight;
            if (eventItemCount == 0)
            {
                // ??waypoint揶쎛 ??좎럩堉??3?????좎룞???癰귣똻????⑤벀而???좎럩??
                eventListViewHeight = HEADER_HEIGHT + (MIN_VISIBLE_ITEMS * ITEM_HEIGHT);
            }
            else if (eventItemCount < MIN_VISIBLE_ITEMS)
            {
                // 3??沃섎챶彛????좎럥??筌ㅼ뮇??3揶쏆뮋?? 癰귣똻?????좎럡由겼뜝???좎럩??(??좎럥??+ 3?????좎룞??
                eventListViewHeight = HEADER_HEIGHT + (MIN_VISIBLE_ITEMS * ITEM_HEIGHT);
            }
            else
            {
                // 3????좎럩湲????좎럥????좎럩?????좎룞????좎럩肉?筌띿쉳苡???좎럩????좎럩?? 筌ㅼ뮋??????좎?釉?
                eventListViewHeight = Math.Min(HEADER_HEIGHT + (eventItemCount * ITEM_HEIGHT), MAX_LISTVIEW_HEIGHT);
            }
            
            int eventGroupBoxHeight = eventListViewHeight + PADDING;
            eventGroupBoxHeight = Math.Min(eventGroupBoxHeight, MAX_GROUPBOX_HEIGHT);
            
            listViewEventWaypoints.Height = eventListViewHeight;
            groupBoxEventWaypoint.Height = eventGroupBoxHeight;
            
            // ????좎럩????좎럥瑗??좎럩????좎럩????좎럥???좎???(??좎럥??疫꿸낀?? ??좎럥??
            int currentY = 0; // ??좎럩??Y ??좎럩??(??좎럥瑗?Padding????좎럩?앲첋???0??좎럥以???좎럩??
            
            // Person Waypoint ??좎럩??
            groupBoxPersonWaypoint.Location = new System.Drawing.Point(12, currentY);
            currentY += groupBoxPersonWaypoint.Height + 20; // ??좎럥瑗???좎럩??+ ??좎럥媛?
            
            // Vehicle Waypoint ??좎럩??
            groupBoxVehicleWaypoint.Location = new System.Drawing.Point(12, currentY);
            currentY += groupBoxVehicleWaypoint.Height + 20; // ??좎럥瑗???좎럩??+ ??좎럥媛?
            
            // Event Waypoint ??좎럩??
            groupBoxEventWaypoint.Location = new System.Drawing.Point(12, currentY);
            currentY += groupBoxEventWaypoint.Height + 20; // ??좎럥瑗???좎럩??+ ??좎럥媛?
            
            // ???좎룞??甕곌쑵????좎럩??
            btnDeleteEventWaypoint.Location = new System.Drawing.Point(12, currentY);
            currentY += btnDeleteEventWaypoint.Height + 20; // 甕곌쑵????좎럩??+ ??좎럥媛?
            
            // Labels ??좎럥瑗???좎럩??
            groupBoxLabels.Location = new System.Drawing.Point(12, currentY);
        }

        private void UpdateObjectInfo(BoundingBox box)
        {
            if (box == null)
            {
                // ??獄쏅벡?ゅ첎? null????waypoint ??좎럥????좎럩??
                if (selectedWaypoint != null)
                {
                    UpdateWaypointInfo(selectedWaypoint);
                }
                else
                {
                    labelObjectLabel.Text = "Label: -";
                }
                return;
            }
            
            string labelText = "";
            if (box.Label == "person")
            {
                labelText = $"Label: {GetPersonDisplayLabel(box)}";
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
            
            // ??Person??野껋럩??waypoint-scoped ??좎럩苑?뜝???좎럩??(筌ㅼ뮋?? 3??
            if (box.Label == "person")
            {
                var attributes = personAttributeStore.GetAllAttributes(box.PersonId, box.FrameIndex, waypointMarkers);
                if (attributes != null && attributes.Count > 0)
                {
                    // waypoint-scoped ??좎럩苑?뜝???좎?苑ｅ뜝?(Occlusion, BodyView, ActionType)
                    var waypointScopedAttrs = attributes
                        .Where(kvp => kvp.Value != null && PersonAttributeStore.IsWaypointScoped(kvp.Key))
                        .Take(3)  // 筌ㅼ뮋?? 3揶쏆뮆彛???좎럩??
                        .ToList();
                    
                    if (waypointScopedAttrs.Count > 0)
                    {
                        // ??좎럩苑????癰귣떯由??ル뿪苡???좎럩??(????좎럩苑??餓κ쑬而?퐛?됱몵???닌됲뀋)
                        var attrLines = waypointScopedAttrs.Select(kvp => $"  ??{kvp.Key}: {kvp.Value}");
                        string attrText = string.Join("\n", attrLines);
                        labelText += $"\n??좎럩苑?\n{attrText}";
                    }
                }
            }
            
            labelObjectLabel.Text = labelText;
            
            // ????좎?源??獄쏅벡?ゅ첎? ??좎?釉?waypoint ??좎럥????좎럩??
            if (selectedWaypoint != null)
            {
                UpdateWaypointInfo(selectedWaypoint);
            }
        }
        
        // ??Waypoint ??좎럥????좎럩????좎럩??
        private void UpdateWaypointInfo(WaypointMarker waypoint)
        {
            if (waypoint == null)
            {
                return;
            }
            
            // ????좎럩??waypoint????좎럥爰???좎럩??(??좎룞?? labelObjectLabel????좎럩???좎럩堉???좎럩?앭뜝?域밸챿??????좎룞??)
            string categoryName = GetCategoryName(waypoint.Label, waypoint.ObjectId);
            if (selectedBox == null)
            {
                // 獄쏅벡?ゅ첎? ??좎?源??좎룞?? ??좎룞?? 野껋럩??waypoint ??좎럥爰???좎럩??
                labelObjectLabel.Text = $"Label: {categoryName}";
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
                MessageBox.Show($"Person ??좎럥爰???좎?源?? ??좎럩??ID: {currentAssignedId}", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show($"Vehicle ??좎럥爰???좎?源?? ??좎럩??ID: {currentAssignedId}", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show($"Event ??좎럥爰???좎?源?? ??좎럩??ID: {currentAssignedId}", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ??좎럥爰???????좎?源?甕곌쑵????좎럥諭??
        private void btnLabelPerson_Click(object sender, EventArgs e)
        {
            try
            {
                // ??YOLO ?곕뗄????좎룞?? 餓λ쵐肉????좎럥爰???좎?源?筌△뫀??
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[??좎럥爰?甕곌쑵??筌△뫀?? YOLO ?곕뗄????좎룞?? 餓λ쵐?좄첋?????좎럥爰???좎?源??븍뜉??");
                    MessageBox.Show(
                        "YOLO ?곕뗄????좎럥????좎룞??揶쎛 筌욊쑵六?餓λ쵐???좎럥??\n??좎럩毓????좎럥利????좎럡?댐쭪? 疫꿸퀡???좎럩竊??좎럩??",
                        "Info",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                
                // ??좎룞??: 揶쏆닂?? 甕곌쑵?????좎룞?? ??좎?源??좎럩堉???좎럩?앭뜝???좎?源???좎럩??
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
                System.Diagnostics.Debug.WriteLine($"[Person ??좎럥爰?甕곌쑵????좎럥履? {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"Person label selection error:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            
            currentSelectedLabel = "person";
            currentAssignedId = 1; // 疫꿸퀡??뜝?person_01
            
            // 甕곌쑵????좎럡而????좎?源???좎럥???좎??? person ??좎?源? ??좎럥?㏆쭪? ??좎럩??
            btnLabelPerson.BackColor = System.Drawing.Color.FromArgb(236, 72, 153);
            btnLabelPerson.FlatAppearance.BorderSize = 3;
            btnLabelVehicle.BackColor = System.Drawing.Color.FromArgb(219, 234, 254);
            btnLabelVehicle.FlatAppearance.BorderSize = 2;
            btnLabelEvent.BackColor = System.Drawing.Color.FromArgb(220, 252, 231);
            btnLabelEvent.FlatAppearance.BorderSize = 2;
            
            // ??좎?源??bbox揶쎛 ??좎럩?앭뜝???좎럥??獄쏅벡?ゅ뜝?person??좎럥以?癰궰??
            if (selectedBox != null)
            {
                string oldLabel = selectedBox.Label;
                
                // ??Event ??Person 癰궰??????좎????Event 獄쏅벡?????좎룞??
                if (oldLabel == "event")
                {
                    RemovePropagatedEventBoxes(selectedBox);
                }
                
                selectedBox.Label = "person";
                SetBoxId(selectedBox, "person", 1); // 疫꿸퀡??뜝?person_01
                
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
                // ??YOLO ?곕뗄????좎룞?? 餓λ쵐肉????좎럥爰???좎?源?筌△뫀??
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[??좎럥爰?甕곌쑵??筌△뫀?? YOLO ?곕뗄????좎룞?? 餓λ쵐?좄첋?????좎럥爰???좎?源??븍뜉??");
                    MessageBox.Show(
                        "YOLO ?곕뗄????좎럥????좎룞??揶쎛 筌욊쑵六?餓λ쵐???좎럥??\n??좎럩毓????좎럥利????좎럡?댐쭪? 疫꿸퀡???좎럩竊??좎럩??",
                        "Info",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                
                // ??좎룞??: 揶쏆닂?? 甕곌쑵?????좎룞?? ??좎?源??좎럩堉???좎럩?앭뜝???좎?源???좎럩??
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
                System.Diagnostics.Debug.WriteLine($"[Vehicle label selection error] {ex.Message}");
                MessageBox.Show(
                    $"Vehicle label selection error:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            
            currentSelectedLabel = "vehicle";
            currentAssignedId = 1; // 疫꿸퀡??뜝?vehicle_car
            
            // 甕곌쑵????좎럡而????좎?源???좎럥???좎??? vehicle ??좎?源? ??좎럥?㏆쭪? ??좎럩??
            btnLabelPerson.BackColor = System.Drawing.Color.FromArgb(252, 231, 243);
            btnLabelPerson.FlatAppearance.BorderSize = 2;
            btnLabelVehicle.BackColor = System.Drawing.Color.FromArgb(59, 130, 246);
            btnLabelVehicle.FlatAppearance.BorderSize = 3;
            btnLabelEvent.BackColor = System.Drawing.Color.FromArgb(220, 252, 231);
            btnLabelEvent.FlatAppearance.BorderSize = 2;
            
            // ??좎?源??bbox揶쎛 ??좎럩?앭뜝???좎럥??獄쏅벡?ゅ뜝?vehicle??癰궰??
            if (selectedBox != null)
            {
                string oldLabel = selectedBox.Label;
                
                // ??Event ??Vehicle 癰궰??????좎????Event 獄쏅벡?????좎룞??
                if (oldLabel == "event")
                {
                    RemovePropagatedEventBoxes(selectedBox);
                }
                
                selectedBox.Label = "vehicle";
                SetBoxId(selectedBox, "vehicle", 1); // 疫꿸퀡??뜝?vehicle_car
                
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
                // ??YOLO ?곕뗄????좎룞?? 餓λ쵐肉????좎럥爰???좎?源?筌△뫀??
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[??좎럥爰?甕곌쑵??筌△뫀?? YOLO ?곕뗄????좎룞?? 餓λ쵐?좄첋?????좎럥爰???좎?源??븍뜉??");
                    MessageBox.Show(
                        "YOLO ?곕뗄????좎럥????좎룞??揶쎛 筌욊쑵六?餓λ쵐???좎럥??\n??좎럩毓????좎럥利????좎럡?댐쭪? 疫꿸퀡???좎럩竊??좎럩??",
                        "Info",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                
                // ??좎룞??: 揶쏆닂?? 甕곌쑵?????좎룞?? ??좎?源??좎럩堉???좎럩?앭뜝???좎?源???좎럩??
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
                System.Diagnostics.Debug.WriteLine($"[Event label selection error] {ex.Message}");
                MessageBox.Show(
                    $"Event label selection error:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            
            currentSelectedLabel = "event";
            currentAssignedId = 1; // 疫꿸퀡??뜝?event_contact
            
            // 甕곌쑵????좎럡而????좎?源???좎럥???좎??? event ??좎?源? ??좎럥?㏆쭪? ??좎럩??
            btnLabelPerson.BackColor = System.Drawing.Color.FromArgb(252, 231, 243);
            btnLabelPerson.FlatAppearance.BorderSize = 2;
            btnLabelVehicle.BackColor = System.Drawing.Color.FromArgb(219, 234, 254);
            btnLabelVehicle.FlatAppearance.BorderSize = 2;
            btnLabelEvent.BackColor = System.Drawing.Color.FromArgb(34, 197, 94);
            btnLabelEvent.FlatAppearance.BorderSize = 3;
            
            // ??좎?源??bbox揶쎛 ??좎럩?앭뜝???좎럥??獄쏅벡?ゅ뜝?event??癰궰??
            if (selectedBox != null)
            {
                string oldLabel = selectedBox.Label;
                
                selectedBox.Label = "event";
                SetBoxId(selectedBox, "event", 1); // 疫꿸퀡??뜝?event_contact
                
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
                
                // Event: 筌앸맩????좎?????좎럩???좎럩?????좎럩苑??좎룞?? ??좎럩?? Exit ??좎럩????筌ｌ꼶??
            }
        }

        // Labels ??좎럥瑗???좎럡由???좎럩?귛뜝???좎룞?? ??좎럩???
        private void TogglePersonPanel(object sender, EventArgs e)
        {
            isPersonExpanded = !isPersonExpanded;
            panelPersonList.Visible = isPersonExpanded;
            labelPersonList.Text = isPersonExpanded ? "??person" : "> person";
            
            // ??좎럥瑗????좎럩?쒎뜝???좎럥彛???좎럩????좎럥???좎???
            if (isPersonExpanded)
            {
                UpdatePersonListDisplay();
            }
            
            // ??좎럩???좎럩????좎럡???(??좎럥????좎럩???좎럩????좎럩??鈺곌퀣??
            UpdateLabelsLayoutAfterToggle();
        }

        private void ToggleVehiclePanel(object sender, EventArgs e)
        {
            isVehicleExpanded = !isVehicleExpanded;
            panelVehicleList.Visible = isVehicleExpanded;
            labelVehicleList.Text = isVehicleExpanded ? "??vehicle" : "> vehicle";
            
            // ??좎럥瑗????좎럩?쒎뜝???좎럥彛???좎럩????좎럥???좎???
            if (isVehicleExpanded)
            {
                UpdateVehicleListDisplay();
            }
            
            // ??좎럩???좎럩????좎럡???(??좎럥????좎럩???좎럩????좎럩??鈺곌퀣??
            UpdateLabelsLayoutAfterToggle();
        }

        private void ToggleEventPanel(object sender, EventArgs e)
        {
            isEventExpanded = !isEventExpanded;
            panelEventList.Visible = isEventExpanded;
            labelEventList.Text = isEventExpanded ? "??event" : "> event";
            
            // ??좎럥瑗????좎럩?쒎뜝???좎럥彛???좎럩????좎럥???좎???
            if (isEventExpanded)
            {
                UpdateEventListDisplay();
            }
            
            // ??좎럩???좎럩????좎럡???(??좎럥????좎럩???좎럩????좎럩??鈺곌퀣??
            UpdateLabelsLayoutAfterToggle();
        }
        
        // Labels ??좎럥瑗???좎럩???좎럩????좎럥???좎???(??좎룞?? ????좎럩???좎럩????좎럩????좎럩??鈺곌퀣??
        private void UpdateLabelsLayoutAfterToggle()
        {
            const int startY = 70;
            const int toggleHeight = 30;
            const int panelHeight = 100;
            int currentY = startY;
            
            // Person ??좎럩??
            currentY += toggleHeight; // Person ??좎룞?? 甕곌쑵????좎럩??
            if (isPersonExpanded)
                currentY += panelHeight; // Person ??좎럥瑗????좎럩?????좎럩?앭뜝?????좎럩?좑쭕?곌껍 ?곕톪??
            
            // Vehicle ??좎럩??
            labelVehicleList.Location = new System.Drawing.Point(8, currentY);
            currentY += toggleHeight;
            panelVehicleList.Location = new System.Drawing.Point(8, currentY);
            if (isVehicleExpanded)
                currentY += panelHeight;
            
            // Event ??좎럩??
            labelEventList.Location = new System.Drawing.Point(8, currentY);
            currentY += toggleHeight;
            panelEventList.Location = new System.Drawing.Point(8, currentY);
            if (isEventExpanded)
                currentY += panelHeight;
            
            // ??좎럥??甕곌쑵???
            currentY += 5; // ??좎럡而????좎럥媛?
            btnDeleteLabel.Location = new System.Drawing.Point(8, currentY);
            currentY += 42; // ???좎룞??甕곌쑵????좎럩??+ ??좎럥媛?
            btnExportJsonInLabels.Location = new System.Drawing.Point(8, currentY);
            
            // Labels ??좎럥瑗???좎럩猿???좎럩??鈺곌퀣??
            currentY += 50; // JSON ????甕곌쑵????좎럩??+ ??좎럥媛?
            groupBoxLabels.Height = Math.Max(260, currentY);
        }

        // ??좎럥??筌ㅼ뮇??? 獄쏅벡????좎럥爰???좎럩?????좎럩苑?(??좎럩沅??揶쎛??좎?釉?獄쏄퀣肉???좎럩??
        private static readonly string[] VehicleTypes = { "car", "motorcycle", "e_scooter", "bicycle" };
        private static readonly string[] EventTypes = { "contact", "exchange", "board", "final_exchange" };
        
        private string GetPersonDisplayLabel(BoundingBox box)
        {
            if (box == null || box.Label != "person")
            {
                return string.Empty;
            }

            if (string.Equals(box.PersonPartType, "face", StringComparison.OrdinalIgnoreCase))
            {
                int linkedId = box.LinkedPersonId.GetValueOrDefault(box.PersonId);
                if (linkedId > 0)
                {
                    return $"person_face->body_{linkedId:D2}";
                }

                return "person_face";
            }

            return $"person_{box.PersonId:D2}";
        }

        private string GetBoxLabelText(BoundingBox box)
        {
            if (box.Label == "person")
            {
                return GetPersonDisplayLabel(box);
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
        
        // ??좎럥??筌ㅼ뮇??? 獄쏅벡????좎럩???좎룞?? 癰궰野껋럥由뷴뜝?筌?Ŋ???얜똾???
        private void InvalidateBoxCache()
        {
            lastCachedFrameForPaint = -1;
            cachedCurrentFrameBoxes.Clear();
        }
        
        // bbox ?귐딅뮞????좎럥???좎??껃첎? ??좎럩???좎룞?? ??좎럩??(?귐딅꺖??筌ㅼ뮇???
        private bool ShouldUpdateBboxList(int frameIndex)
        {
            // ??좎럩????좎럥???좎럩????좎?釉?waypoint 筌≪뼐由?
            var currentWaypoint = waypointMarkers.FirstOrDefault(w =>
                frameIndex >= w.EntryFrame &&
                frameIndex <= w.ExitFrame);
            
            // Waypoint揶쎛 癰궰野껋럥由??좎럥?쀯쭪? ??좎럩??
            bool waypointChanged = false;
            
            if (currentWaypoint == null && lastRenderedWaypoint == null)
            {
                // ????null??좎럥??癰궰????좎럩??
                waypointChanged = false;
            }
            else if (currentWaypoint == null || lastRenderedWaypoint == null)
            {
                // ??좎럥援밧뜝?null??좎럥??癰궰野껋럥留?
                waypointChanged = true;
            }
            else
            {
                // ????null????좎럥?꿨뜝?EntryFrame??ExitFrame??좎럥以???쑨??
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
        
        // 3??bbox 筌뤴뫖以????좎럩???좎럥以???좎럩苑??좎럩肉???좎럩??(??좎럩????좎럥???疫꿸낀??)
        private void UpdateBboxListDisplay()
        {
            // ??좎럩?쒎뜝???좎럥瑗멨뜝???좎럥???좎???(??좎럥??筌ㅼ뮇???
            if (isPersonExpanded)
                UpdatePersonListDisplay();
            if (isVehicleExpanded)
                UpdateVehicleListDisplay();
            if (isEventExpanded)
                UpdateEventListDisplay();
        }
        
        // Person ?귐딅뮞????좎럩??(??좎럩????좎럥???좎럩??Person bbox)
        private void UpdatePersonListDisplay()
        {
            panelPersonList.Controls.Clear();
            
            // ????좎럥瑗???좎럩猿???좎럥??????좎?源???좎럩??
            panelPersonList.Click += (s, e) =>
            {
                selectedBox = null;
                ClearSidebarHighlights();
                UpdateObjectInfo(null);
                pictureBoxVideo.Invalidate();
            };
            
            var currentBoxes = boundingBoxes
                .Where(b => b.FrameIndex == currentFrameIndex && b.Label == "person" && !b.IsDeleted)
                .OrderBy(b => IsBodySubTypeCandidate(b) ? 1 : 0)
                .ThenBy(b => GetDrawingIdentityKey(b))
                .ToList();
            
            if (currentBoxes.Count == 0)
            {
                Label emptyLabel = new Label
                {
                    Text = "No person boxes in current frame",
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
                    Tag = currentBox // ??獄쏅벡????좎럥?ュ뜝?Tag??????
                };
                
                Label itemLabel = new Label
                {
                    Text = GetPersonDisplayLabel(currentBox),
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
                
                // ComboBox ??좎럥苡?????좎?寃뺝뜝?獄쎻뫜??
                comboBox.MouseWheel += (s, e) => ((HandledMouseEventArgs)e).Handled = true;
                
                if (string.Equals(currentBox.PersonPartType, "face", StringComparison.OrdinalIgnoreCase))
                {
                    AppendFaceDebugLog("PersonList", $"RenderFaceRow: frame={currentFrameIndex}, personId={currentBox.PersonId}, linkedPersonId={currentBox.LinkedPersonId}, entryFrame={currentBox.BoxEntryFrame}, exitFrame={currentBox.BoxExitFrame}, displayLabel={GetPersonDisplayLabel(currentBox)}");
                    comboBox.Items.Add(GetPersonDisplayLabel(currentBox));
                    comboBox.SelectedIndex = 0;
                    comboBox.Enabled = false;
                }
                else
                {
                    for (int i = 1; i <= 30; i++)
                    {
                        comboBox.Items.Add($"person_{i:D2}");
                    }
                    comboBox.SelectedItem = $"person_{currentBox.PersonId:D2}";
                    
                    comboBox.SelectedIndexChanged += (s, e) =>
                    {
                        if (comboBox.SelectedItem == null)
                            return;

                        string selected = comboBox.SelectedItem.ToString();
                        if (!selected.StartsWith("person_", StringComparison.OrdinalIgnoreCase))
                            return;

                        string idText = selected.Substring(7);
                        if (!int.TryParse(idText, out int newId))
                        {
                            AppendFaceDebugLog("PersonList", $"ParseFailed: selected={selected}, idText={idText}, frame={currentFrameIndex}, currentPersonId={currentBox.PersonId}, currentPartType={currentBox.PersonPartType}, linkedPersonId={currentBox.LinkedPersonId}");
                            return;
                        }

                        int oldId = currentBox.PersonId;
                        
                        // ????좎럥??獄쏅벡?ゅ첎? ??좎?釉?waypoint 筌≪뼐由?
                        var waypoint = FindWaypointForBox(currentBox);
                        
                        if (waypoint != null && waypoint.Label == "person")
                        {
                            // ??waypoint 甕곕뗄????좎럩??筌뤴뫀諭?person 獄쏅벡???PersonId 癰궰??
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
                            
                            // ??waypoint??ObjectId??癰궰??
                            waypoint.ObjectId = newId;
                            
                            // ??waypoint ?귐딅뮞????좎럥???좎???
                            UpdateWaypointListView();
                        }
                        else
                        {
                            // waypoint????좎?釉?쭪? ??좎룞?? 野껋럩????좎럩??獄쏅벡?ゅ뜝?癰궰??
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
                    };
                }
                
                itemPanel.Controls.Add(itemLabel);
                itemPanel.Controls.Add(comboBox);
                
                EventHandler clickHandler = (s, e) =>
                {
                    selectedBox = currentBox;
                    UpdateObjectInfo(selectedBox);
                    HighlightSelectedBoxInSidebar(); // ????좎럩???좎럩?????좎럥???좎???
                    pictureBoxVideo.Invalidate();
                };
                
                itemPanel.Click += clickHandler;
                itemLabel.Click += clickHandler;
                
                panelPersonList.Controls.Add(itemPanel);
                yPos += 70;
            }
        }
        
        // Vehicle ?귐딅뮞????좎럩??(??좎럩????좎럥???좎럩??Vehicle bbox)
        private void UpdateVehicleListDisplay()
        {
            panelVehicleList.Controls.Clear();
            
            // ????좎럥瑗???좎럩猿???좎럥??????좎?源???좎럩??
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
                    Text = "No vehicle boxes in current frame",
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
                    Tag = currentBox // ??獄쏅벡????좎럥?ュ뜝?Tag??????
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
                
                // ComboBox ??좎럥苡?????좎?寃뺝뜝?獄쎻뫜??
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
                                
                                // ????좎럥??獄쏅벡?ゅ첎? ??좎?釉?waypoint 筌≪뼐由?
                                var waypoint = FindWaypointForBox(currentBox);
                                
                                if (waypoint != null && waypoint.Label == "vehicle")
                                {
                                    // ??waypoint 甕곕뗄????좎럩??筌뤴뫀諭?vehicle 獄쏅벡???VehicleId 癰궰??
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
                                    
                                    // ??waypoint??ObjectId??癰궰??
                                    waypoint.ObjectId = newVehicleId;
                                    
                                    // ??waypoint ?귐딅뮞????좎럥???좎???
                                    UpdateWaypointListView();
                                }
                                else
                                {
                                    // waypoint????좎?釉?쭪? ??좎룞?? 野껋럩????좎럩??獄쏅벡?ゅ뜝?癰궰??
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
                    HighlightSelectedBoxInSidebar(); // ????좎럩???좎럩?????좎럥???좎???
                    pictureBoxVideo.Invalidate();
                };
                
                itemPanel.Click += clickHandler;
                itemLabel.Click += clickHandler;
                
                panelVehicleList.Controls.Add(itemPanel);
                yPos += 70;
            }
        }
        
        // Event ?귐딅뮞????좎럩??(??좎럩????좎럥???좎럩??Event bbox)
        private void UpdateEventListDisplay()
        {
            panelEventList.Controls.Clear();
            
            // ????좎럥瑗???좎럩猿???좎럥??????좎?源???좎럩??
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
                    Text = "No event boxes in current frame",
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
                    Tag = currentBox // ??獄쏅벡????좎럥?ュ뜝?Tag??????
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
                
                // ComboBox ??좎럥苡?????좎?寃뺝뜝?獄쎻뫜??
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
                            
                                // Event ????癰궰??????좎럩???EventId???Rectangle??揶쎛??獄쏅벡?ゅ뜝???좎럥???좎???
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
                    HighlightSelectedBoxInSidebar(); // ????좎럩???좎럩?????좎럥???좎???
                    pictureBoxVideo.Invalidate();
                };
                
                itemPanel.Click += clickHandler;
                itemLabel.Click += clickHandler;
                
                panelEventList.Controls.Add(itemPanel);
                yPos += 70;
            }
        }
        
        // ??좎?源??bbox ???좎룞??甕곌쑵????좎럥諭??
        private void btnDeleteLabel_Click(object sender, EventArgs e)
        {
            if (selectedBox == null)
            {
                MessageBox.Show("Please select a bbox to delete first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            AddUndoAction(new UndoAction { Type = UndoActionType.RemoveBox, Box = CloneBoundingBox(selectedBox) });
            
            // ?????좎룞????좎럥?믣뜝???좎럩??(??좎럩????좎럡援????? ??좎럩????좎룞??)
            selectedBox.IsDeleted = true;
            
            selectedBox = null;
            ClearSidebarHighlights(); // ????좎럩???좎럩????λ뜃由??
            UpdateBoxCount();
            UpdateBboxListDisplay();
            pictureBoxVideo.Invalidate();
        }

        private string CreateEventInstanceId()
        {
            return $"event-{Guid.NewGuid().ToString("N")}";
        }

        private bool IsSameEventInstance(BoundingBox box, WaypointMarker waypoint)
        {
            if (box == null || waypoint == null)
                return false;

            if (box.Label != "event" || waypoint.Label != "event")
                return false;

            bool boxHasInstanceId = !string.IsNullOrWhiteSpace(box.EventInstanceId);
            bool waypointHasInstanceId = !string.IsNullOrWhiteSpace(waypoint.EventInstanceId);

            if (boxHasInstanceId && waypointHasInstanceId)
            {
                return string.Equals(box.EventInstanceId, waypoint.EventInstanceId, StringComparison.Ordinal);
            }

            if (!boxHasInstanceId && !waypointHasInstanceId)
            {
                return box.EventId == waypoint.ObjectId;
            }

            return false;
        }

        // 獄쏅벡?????좎럩????좎럥爰????좎럥???좎럥??ID 揶쎛??좎럩?ㅵ뜝?
        private int GetBoxId(BoundingBox box)
        {
            if (box.Label == "person") return box.PersonId;
            if (box.Label == "vehicle") return box.VehicleId;
            if (box.Label == "event") return box.EventId;
            return 0;
        }
        
        // ??좎?源??獄쏅벡?ゅ첎? ??좎?釉?疫꿸퀣??Waypoint 筌≪뼐由?
        private WaypointMarker FindWaypointForBox(BoundingBox box)
        {
            if (box == null) return null;

            int boxId = GetDrawingIdentityId(box);

            // Event: ??좎럩??EventInstanceId揶쎛 ??좎럩?앭뜝???좎럩苑???좎???筌띲끉臾?
            if (box.Label == "event")
            {
                if (!string.IsNullOrWhiteSpace(box.EventInstanceId))
                {
                    var sameInstanceWaypoint = waypointMarkers.FirstOrDefault(w =>
                        w.Label == box.Label &&
                        w.EntryFrame <= box.FrameIndex &&
                        w.ExitFrame >= box.FrameIndex &&
                        string.Equals(w.EventInstanceId, box.EventInstanceId, StringComparison.Ordinal));

                    if (sameInstanceWaypoint != null)
                        return sameInstanceWaypoint;
                }

                var sameTypeWaypoint = waypointMarkers.FirstOrDefault(w =>
                    w.Label == box.Label &&
                    w.EntryFrame <= box.FrameIndex &&
                    w.ExitFrame >= box.FrameIndex &&
                    IsSameEventInstance(box, w));

                if (sameTypeWaypoint != null)
                    return sameTypeWaypoint;
            }

            // 疫꿸퀣????좎럩??fallback
            var waypoint = waypointMarkers.FirstOrDefault(w =>
                w.Label == box.Label &&
                w.ObjectId == boxId &&
                box.FrameIndex >= w.EntryFrame &&
                box.FrameIndex <= w.ExitFrame);

            return waypoint;
        }
        private object GetPersonAttribute(int personId, int frameIndex, string attributeName)
        {
            return personAttributeStore.GetAttribute(personId, frameIndex, attributeName, waypointMarkers, currentVideoFile);
        }

        // Person ??좎럩苑?????筌롫뗄苑??
        private void SetPersonAttribute(int personId, int waypointEntryFrame, string attributeName, object value)
        {
            int applyFromFrame;
            
            // EntryFrame??좎럩苑???좎럩???좎럥??waypoint ??좎럩猿????좎럩??
            if (currentFrameIndex == waypointEntryFrame)
            {
                applyFromFrame = waypointEntryFrame;
            }
            else
            {
                // 餓λ쵌而???좎럥???좎럩肉????좎럩???좎럥????좎럩????좎럥???좎룞??????좎럩??
                applyFromFrame = currentFrameIndex;
            }
            
            personAttributeStore.SetAttribute(personId, waypointEntryFrame, applyFromFrame, attributeName, value, waypointMarkers, currentVideoFile);
        }
        
        // 獄쏅벡????좎럩????좎럥利?????좎럩?????좎럥???疫꿸퀡以?
        private void RecordManuallyAdjustedFrame(BoundingBox box)
        {
            if (box == null) return;
            
            // waypoint ??좎룞????좎럩苑뚦뜝?疫꿸퀡以?
            var waypoint = FindWaypointForBox(box);
            if (waypoint == null) return;
            
            string key = GetDrawingIdentityKey(box);
            
            if (!manuallyAdjustedFrames.ContainsKey(key))
            {
                manuallyAdjustedFrames[key] = new List<int>();
            }
            
            // 餓λ쵎????좎럡援?????좎럥????좎룞??
            if (!manuallyAdjustedFrames[key].Contains(box.FrameIndex))
            {
                manuallyAdjustedFrames[key].Add(box.FrameIndex);
                manuallyAdjustedFrames[key].Sort();
            }
        }
        
        // ??獄쏅벡?????좎룞??????좎럥?ゅ뜝???좎럥猷?疫꿸퀡以?
        private void RecordDisappearanceIntent(BoundingBox box)
        {
            if (box == null) return;
            
            // waypoint ??좎룞????좎럩苑뚦뜝?疫꿸퀡以?
            var waypoint = FindWaypointForBox(box);
            if (waypoint == null) return;
            
            string key = GetDrawingIdentityKey(box);
            
            if (!disappearedRanges.ContainsKey(key))
            {
                disappearedRanges[key] = new List<(int, int?)>();
            }
            
            // ??좎룞?? ??좎럥????좎럥???좎럩肉????좎럩???좎럥????좎럥?ゅ뜝?疫꿸퀡以????좎럥?쀯쭪? ??좎럩??
            bool exists = disappearedRanges[key].Any(r => r.startFrame == box.FrameIndex && !r.endFrame.HasValue);
            
            if (!exists)
            {
                // ??좎럥?ゅ뜝???좎럩????좎럥???疫꿸퀡以?(?ル굝利???좎럥???좎룞?? ??좎럩彛?沃섎챸???
                disappearedRanges[key].Add((box.FrameIndex, null));
                System.Diagnostics.Debug.WriteLine($"[Disappearance intent recorded] {key}: start frame {box.FrameIndex}");
            }
        }
        
        // 獄쏅벡?????좎럩????좎럥爰?????좎럩肉?ID ??좎럩??
        private void SetBoxId(BoundingBox box, string label, int id)
        {
            if (label == "person") box.PersonId = id;
            else if (label == "vehicle") box.VehicleId = id;
            else if (label == "event") box.EventId = id;
        }

        // ??좎????筌띿쉶??Category ID??獄쏆꼹??(JSON ??좎럥???좎럡由??
        private int GetCategoryId(string label, int boxId)
        {
            string categoryName = GetCategoryName(label, boxId);
            
            if (CategoryIdMap.ContainsKey(categoryName))
                return CategoryIdMap[categoryName];
            
            // 疫꿸퀡??뜝?筌ｌ꼶??(筌띲끋釉??좎룞?? ??좎룞?? 野껋럩??
            if (label == "person") return Math.Min(boxId, 20); // 1~20
            if (label == "vehicle") return Math.Min(21 + (boxId - 1), 24); // 21~24
            if (label == "event") return Math.Min(25 + (boxId - 1), 28); // 25~28 (4??
            
            return boxId;
        }

        // ??좎????筌띿쉶??Category Name??獄쏆꼹??(JSON ??좎럥???좎럡由??
        private string GetCategoryName(string label, int boxId)
        {
            if (label == "person")
            {
                // person???person_01 ~ person_20 ??좎럩??
                return $"person_{boxId:D2}";
            }
            else if (label == "vehicle")
            {
                // vehicle????⑥쥙? ??좎럥已?筌띲끋釉?(ID 21~24)
                switch (boxId)
                {
                    case 1: return "car";           // ID: 21
                    case 2: return "motorcycle";    // ID: 22
                    case 3: return "e_scooter";     // ID: 23
                    case 4: return "bicycle";       // ID: 24
                    default: return "car"; // 疫꿸퀡??뜝?
                }
            }
            else if (label == "event")
            {
                // event???⑥쥙? ??좎럥已?筌띲끋釉?(ID 25~28)
                switch (boxId)
                {
                    case 1: return "contact";         // ID: 25
                    case 2: return "exchange";        // ID: 26
                    case 3: return "board";           // ID: 27
                    case 4: return "final_exchange";  // ID: 28
                    default: return "contact"; // 疫꿸퀡??뜝?
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
            string labelName = ShowInputDialog("????좎럥爰??곕톪??", "??좎럥爰???좎럥已????좎럥???좎럩苑??(?? person_02):");

            if (string.IsNullOrWhiteSpace(labelName))
                return;

            if (customLabels.Any(l => l.Name == labelName))
            {
                MessageBox.Show("??좎룞?? 鈺곕똻???좎럥????좎럥爰??좎럥???", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show("?믪눦?? BBox????좎?源??좎럩竊??좎럩??", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            Button confirmation = new Button() { Text = "OK", Left = 200, Width = 80, Top = 80, DialogResult = DialogResult.OK };
            Button cancel = new Button() { Text = "Cancel", Left = 290, Width = 80, Top = 80, DialogResult = DialogResult.Cancel };

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

    }
}










