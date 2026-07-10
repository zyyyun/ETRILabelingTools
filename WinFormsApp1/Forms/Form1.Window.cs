using System;
using System.Windows.Forms;

namespace WinFormsApp1
{
    public partial class Form1
    {
        private void btnClose_Click(object sender, EventArgs e)
        {
            try
            {
                // ✅ 종료 시 YOLO 탐지 작업 중지
                System.Diagnostics.Debug.WriteLine("[애플리케이션 종료] YOLO 탐지 중지");
                StopYoloDetection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[애플리케이션 종료] YOLO 탐지 중지 오류: {ex.Message}");
                // 종료는 계속 진행
            }
            finally
            {
                this.Close();
            }
        }
        private void btnMaximize_Click(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            else
            {
                this.WindowState = FormWindowState.Maximized;
            }
        }

        private void btnMinimize_Click(object sender, EventArgs e) => this.WindowState = FormWindowState.Minimized;

        /// <summary>
        /// 최대화/복원 버튼 아이콘 업데이트
        /// </summary>
        private void UpdateMaximizeButtonIcon()
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                btnMaximize.Text = "❐"; // 복원 아이콘
            }
            else
            {
                btnMaximize.Text = "□"; // 최대화 아이콘
            }
        }

        #region 창 이동 및 크기 조절

        /// <summary>
        /// 헤더 드래그로 창 이동 기능 설정
        /// </summary>
        private void SetupWindowDragHandlers()
        {
            // panelHeader를 드래그하면 창이 이동
            panelHeader.MouseDown += PanelHeader_MouseDown;
            panelHeader.MouseMove += PanelHeader_MouseMove;
            panelHeader.MouseUp += PanelHeader_MouseUp;
            panelHeader.DoubleClick += PanelHeader_DoubleClick;

            // labelTitle도 드래그 가능하게
            labelTitle.MouseDown += PanelHeader_MouseDown;
            labelTitle.MouseMove += PanelHeader_MouseMove;
            labelTitle.MouseUp += PanelHeader_MouseUp;
            labelTitle.DoubleClick += PanelHeader_DoubleClick;
        }

        private void PanelHeader_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 최대화 상태가 아닐 때만 이동 가능
                if (this.WindowState != FormWindowState.Maximized)
                {
                    isMovingWindow = true;
                    windowMoveStartPoint = e.Location;
                }
            }
        }

        private void PanelHeader_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMovingWindow)
            {
                // 마우스 이동량 계산
                System.Drawing.Point currentScreenPoint = Control.MousePosition;
                System.Drawing.Point offset = new System.Drawing.Point(
                    currentScreenPoint.X - windowMoveStartPoint.X,
                    currentScreenPoint.Y - windowMoveStartPoint.Y
                );

                this.Location = offset;
            }
        }

        private void PanelHeader_MouseUp(object sender, MouseEventArgs e)
        {
            isMovingWindow = false;
        }

        /// <summary>
        /// 헤더 더블클릭으로 최대화/복원 토글
        /// </summary>
        private void PanelHeader_DoubleClick(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            else
            {
                this.WindowState = FormWindowState.Maximized;
            }
            // Resize 이벤트가 자동으로 UpdateMaximizeButtonIcon을 호출함
        }

        /// <summary>
        /// Windows 메시지를 가로채서 창 테두리 크기 조절 기능 추가
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTCLIENT = 1;
            const int HTLEFT = 10;
            const int HTRIGHT = 11;
            const int HTTOP = 12;
            const int HTTOPLEFT = 13;
            const int HTTOPRIGHT = 14;
            const int HTBOTTOM = 15;
            const int HTBOTTOMLEFT = 16;
            const int HTBOTTOMRIGHT = 17;

            if (m.Msg == WM_NCHITTEST && this.WindowState != FormWindowState.Maximized)
            {
                base.WndProc(ref m);

                // 마우스 위치 가져오기
                System.Drawing.Point pos = this.PointToClient(new System.Drawing.Point(m.LParam.ToInt32()));

                int width = this.ClientSize.Width;
                int height = this.ClientSize.Height;

                // 모서리 및 테두리 영역 감지
                if (pos.X <= RESIZE_BORDER_WIDTH && pos.Y <= RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTTOPLEFT;
                }
                else if (pos.X >= width - RESIZE_BORDER_WIDTH && pos.Y <= RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTTOPRIGHT;
                }
                else if (pos.X <= RESIZE_BORDER_WIDTH && pos.Y >= height - RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTBOTTOMLEFT;
                }
                else if (pos.X >= width - RESIZE_BORDER_WIDTH && pos.Y >= height - RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTBOTTOMRIGHT;
                }
                else if (pos.X <= RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTLEFT;
                }
                else if (pos.X >= width - RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTRIGHT;
                }
                else if (pos.Y <= RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTTOP;
                }
                else if (pos.Y >= height - RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTBOTTOM;
                }
            }
            else
            {
                base.WndProc(ref m);
            }
        }

        #endregion
    }
}
