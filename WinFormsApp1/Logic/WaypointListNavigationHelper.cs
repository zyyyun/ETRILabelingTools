namespace WinFormsApp1
{
    public static class WaypointListNavigationHelper
    {
        public static int ResolveTargetFrame(int subItemIndex, int entryFrame, int exitFrame, out bool selectObject)
        {
            selectObject = subItemIndex == 2;
            return subItemIndex == 1 ? exitFrame : entryFrame;
        }
    }
}
