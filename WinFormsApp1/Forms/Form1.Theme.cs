using System;
using System.Drawing;

namespace WinFormsApp1
{
    public partial class Form1
    {
        #region Theme
        private void btnTheme_Click(object sender, EventArgs e)
        {
            isDarkMode = !isDarkMode;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            Color bgColor = isDarkMode ? Color.FromArgb(17, 24, 39) : Color.FromArgb(243, 244, 246);
            Color surfaceColor = isDarkMode ? Color.FromArgb(31, 41, 55) : Color.White;
            Color textColor = isDarkMode ? Color.FromArgb(249, 250, 251) : Color.FromArgb(31, 41, 55);

            panelMainContainer.BackColor = bgColor;
            panelHeader.BackColor = surfaceColor;
            panelLeftSidebar.BackColor = surfaceColor;
            panelCenter.BackColor = bgColor;
            panelVideoControls.BackColor = surfaceColor;
            panelRightSidebar.BackColor = surfaceColor;

            labelTitle.ForeColor = textColor;
            labelBoxCount.ForeColor = textColor;
            labelTimeInfo.ForeColor = textColor;
        }
        #endregion

    }
}
