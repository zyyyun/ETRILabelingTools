using System.Linq;
using System.Windows.Forms;

namespace WinFormsApp1
{
    public partial class Form1
    {
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

                case UndoActionType.EventIdChange:
                    foreach (var change in action.EventIdChanges)
                    {
                        change.Box.EventId = change.OriginalEventId;
                    }
                    InvalidateBoxCache();
                    UpdateWaypointListView();
                    if (selectedBox != null)
                        UpdateObjectInfo(selectedBox);
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

                case UndoActionType.EventIdChange:
                    foreach (var change in action.EventIdChanges)
                    {
                        change.Box.EventId = change.NewEventId;
                    }
                    InvalidateBoxCache();
                    UpdateWaypointListView();
                    if (selectedBox != null)
                        UpdateObjectInfo(selectedBox);
                    break;
            }

            undoStack.Push(action);
            UpdateBoxCount();
            UpdateBboxListDisplay();
            pictureBoxVideo.Invalidate();
        }
        #endregion

    }
}
