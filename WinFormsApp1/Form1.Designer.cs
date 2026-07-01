namespace WinFormsApp1
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            // Top Header Panel
            this.panelHeader = new System.Windows.Forms.Panel();
            this.labelTitle = new System.Windows.Forms.Label();
            this.btnSelectFolder = new System.Windows.Forms.Button();
            this.btnDeleteJson = new System.Windows.Forms.Button();
            this.labelBoxCount = new System.Windows.Forms.Label();
            this.btnMinimize = new System.Windows.Forms.Button();
            this.btnMaximize = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();

            // Main Container
            this.panelMainContainer = new System.Windows.Forms.Panel();

            // Left Sidebar (Vertical Icon Toolbar)
            this.panelLeftSidebar = new System.Windows.Forms.Panel();
            this.btnVideoList = new System.Windows.Forms.Button();
            this.btnSelectAll = new System.Windows.Forms.Button();
            this.btnEdit = new System.Windows.Forms.Button();

            // Center Video Area
            this.panelCenter = new System.Windows.Forms.Panel();
            this.pictureBoxVideo = new System.Windows.Forms.PictureBox();

            // Video Controls (Bottom)
            this.panelVideoControls = new System.Windows.Forms.Panel();
            this.btnPlay = new System.Windows.Forms.Button();
            this.btnRewind = new System.Windows.Forms.Button();
            this.btnForward = new System.Windows.Forms.Button();
            this.labelTimeInfo = new System.Windows.Forms.Label();
            this.btnEntry = new System.Windows.Forms.Button();
            this.btnExit = new System.Windows.Forms.Button();
            this.btnToggleSubtitle = new System.Windows.Forms.Button();
            this.btnToggleYoloDetections = new System.Windows.Forms.Button();
            this.btnToggleSkeleton = new System.Windows.Forms.Button();
            this.btnToggleAttributeView = new System.Windows.Forms.Button();
            this.panelTimeline = new System.Windows.Forms.Panel();

            // Right Sidebar (Info Panel)
            this.panelRightSidebar = new System.Windows.Forms.Panel();
            this.groupBoxObjectInfo = new System.Windows.Forms.GroupBox();
            this.labelObjectLabel = new System.Windows.Forms.Label();
            this.labelPrevWaypoint = new System.Windows.Forms.Label();
            this.labelNextWaypoint = new System.Windows.Forms.Label();
            this.groupBoxPersonWaypoint = new System.Windows.Forms.GroupBox();
            this.groupBoxVehicleWaypoint = new System.Windows.Forms.GroupBox();
            this.groupBoxEventWaypoint = new System.Windows.Forms.GroupBox();
            this.labelWaypointTime = new System.Windows.Forms.Label();
            this.groupBoxLabels = new System.Windows.Forms.GroupBox();
            this.btnLabelPerson = new System.Windows.Forms.Button();
            this.btnLabelVehicle = new System.Windows.Forms.Button();
            this.btnLabelEvent = new System.Windows.Forms.Button();
            this.panelBboxList = new System.Windows.Forms.Panel();
            this.btnDeleteLabel = new System.Windows.Forms.Button();
            this.btnExportJsonInLabels = new System.Windows.Forms.Button();
            this.labelModifyBox = new System.Windows.Forms.Label();
            this.comboBoxPerson = new System.Windows.Forms.ComboBox();
            this.comboBoxVehicle = new System.Windows.Forms.ComboBox();
            this.comboBoxEvent = new System.Windows.Forms.ComboBox();
            this.btnExportJson = new System.Windows.Forms.Button();

            // Timer
            this.timerPlayback = new System.Windows.Forms.Timer(this.components);

            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxVideo)).BeginInit();
            this.SuspendLayout();

            // 
            // panelHeader
            // 
            this.panelHeader.BackColor = System.Drawing.Color.White;
            this.panelHeader.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelHeader.Height = 50;
            this.panelHeader.Controls.Add(this.labelTitle);
            this.panelHeader.Controls.Add(this.btnSelectFolder);
            this.panelHeader.Controls.Add(this.btnExportJson);
            this.panelHeader.Controls.Add(this.btnDeleteJson);
            this.panelHeader.Controls.Add(this.labelBoxCount);
            this.panelHeader.Controls.Add(this.labelCurrentJsonFile);
            // Track Selected Only Toggle Button
            // 선택만 추적 버튼 제거 (롤백)
            this.panelHeader.Controls.Add(this.btnMinimize);
            this.panelHeader.Controls.Add(this.btnMaximize);
            this.panelHeader.Controls.Add(this.btnClose);

            // Title
            this.labelTitle.Text = "Form_AllDay";
            this.labelTitle.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.labelTitle.Location = new System.Drawing.Point(15, 12);
            this.labelTitle.Size = new System.Drawing.Size(150, 25);

            // File Select Button
            this.btnSelectFolder.Text = "파일 선택";
            this.btnSelectFolder.Location = new System.Drawing.Point(170, 10);
            this.btnSelectFolder.Size = new System.Drawing.Size(90, 30);
            this.btnSelectFolder.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSelectFolder.BackColor = System.Drawing.Color.FromArgb(226, 232, 240);
            this.btnSelectFolder.Click += new System.EventHandler(this.btnSelectFolder_Click);

            // Folder Select Button
            this.btnSelectFolderPath = new System.Windows.Forms.Button();
            this.btnSelectFolderPath.Text = "폴더 선택";
            this.btnSelectFolderPath.Location = new System.Drawing.Point(270, 10);
            this.btnSelectFolderPath.Size = new System.Drawing.Size(90, 30);
            this.btnSelectFolderPath.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSelectFolderPath.BackColor = System.Drawing.Color.FromArgb(226, 232, 240);
            this.btnSelectFolderPath.Click += new System.EventHandler(this.btnSelectFolderPath_Click);
            this.panelHeader.Controls.Add(this.btnSelectFolderPath);

            // Export JSON Button
            this.btnExportJson.Text = "JSON 저장";
            this.btnExportJson.Location = new System.Drawing.Point(370, 10);
            this.btnExportJson.Size = new System.Drawing.Size(90, 30);
            this.btnExportJson.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExportJson.BackColor = System.Drawing.Color.FromArgb(34, 197, 94);
            this.btnExportJson.ForeColor = System.Drawing.Color.White;
            this.btnExportJson.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnExportJson.Click += new System.EventHandler(this.btnExportJson_Click);

            // Delete JSON Button
            this.btnDeleteJson.Text = "JSON 삭제";
            this.btnDeleteJson.Location = new System.Drawing.Point(465, 10);
            this.btnDeleteJson.Size = new System.Drawing.Size(90, 30);
            this.btnDeleteJson.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDeleteJson.BackColor = System.Drawing.Color.FromArgb(239, 68, 68);
            this.btnDeleteJson.ForeColor = System.Drawing.Color.White;
            this.btnDeleteJson.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnDeleteJson.Click += new System.EventHandler(this.btnDeleteJson_Click);

            // Box Count Label
            this.labelBoxCount.Text = "박스 개수:";
            this.labelBoxCount.Location = new System.Drawing.Point(560, 15);
            this.labelBoxCount.Size = new System.Drawing.Size(80, 25);
            this.labelBoxCount.Visible = false; // 표시 숨김

            // Current JSON File Label
            this.labelCurrentJsonFile = new System.Windows.Forms.Label();
            this.labelCurrentJsonFile.Text = "";
            this.labelCurrentJsonFile.Location = new System.Drawing.Point(565, 15);
            this.labelCurrentJsonFile.Size = new System.Drawing.Size(800, 20);
            this.labelCurrentJsonFile.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.labelCurrentJsonFile.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.labelCurrentJsonFile.AutoEllipsis = true;

            // Window Control Buttons (Right side)
            this.btnClose.Text = "✕";
            this.btnClose.Location = new System.Drawing.Point(1520, 8);
            this.btnClose.Size = new System.Drawing.Size(35, 35);
            this.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClose.FlatAppearance.BorderSize = 0;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);

            this.btnMaximize.Text = "□";
            this.btnMaximize.Location = new System.Drawing.Point(1480, 8);
            this.btnMaximize.Size = new System.Drawing.Size(35, 35);
            this.btnMaximize.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnMaximize.FlatAppearance.BorderSize = 0;
            this.btnMaximize.Click += new System.EventHandler(this.btnMaximize_Click);

            this.btnMinimize.Text = "−";
            this.btnMinimize.Location = new System.Drawing.Point(1440, 8);
            this.btnMinimize.Size = new System.Drawing.Size(35, 35);
            this.btnMinimize.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnMinimize.FlatAppearance.BorderSize = 0;
            this.btnMinimize.Click += new System.EventHandler(this.btnMinimize_Click);

            // 
            // panelMainContainer
            // 
            this.panelMainContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMainContainer.BackColor = System.Drawing.Color.FromArgb(243, 244, 246);
            this.panelMainContainer.Controls.Add(this.panelLeftSidebar);
            this.panelMainContainer.Controls.Add(this.panelCenter);
            this.panelMainContainer.Controls.Add(this.panelRightSidebar);

            // 
            // panelLeftSidebar (Vertical Icon Toolbar)
            // 
            this.panelLeftSidebar.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelLeftSidebar.Width = 60;
            this.panelLeftSidebar.BackColor = System.Drawing.Color.White;
            this.panelLeftSidebar.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelLeftSidebar.Padding = new System.Windows.Forms.Padding(8);

            int iconBtnY = 10;
            int iconBtnSize = 44;
            int iconBtnSpacing = 10;

            // Video List Button (Yellow)
            this.btnVideoList.Location = new System.Drawing.Point(8, iconBtnY);
            this.btnVideoList.Size = new System.Drawing.Size(iconBtnSize, iconBtnSize);
            this.btnVideoList.Text = "📽️";
            this.btnVideoList.Font = new System.Drawing.Font("Segoe UI", 16F);
            this.btnVideoList.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnVideoList.BackColor = System.Drawing.Color.FromArgb(250, 204, 21);
            this.btnVideoList.FlatAppearance.BorderSize = 0;
            this.btnVideoList.Click += new System.EventHandler(this.btnVideoList_Click);
            iconBtnY += iconBtnSize + iconBtnSpacing;

            // Select All Icon (Active)
            this.btnSelectAll.Location = new System.Drawing.Point(8, iconBtnY);
            this.btnSelectAll.Size = new System.Drawing.Size(iconBtnSize, iconBtnSize);
            this.btnSelectAll.Text = "☐";
            this.btnSelectAll.Font = new System.Drawing.Font("Segoe UI", 20F);
            this.btnSelectAll.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSelectAll.BackColor = System.Drawing.Color.FromArgb(59, 130, 246);
            this.btnSelectAll.ForeColor = System.Drawing.Color.White;
            this.btnSelectAll.FlatAppearance.BorderSize = 0;
            this.btnSelectAll.Click += new System.EventHandler(this.btnSelectAll_Click);
            iconBtnY += iconBtnSize + iconBtnSpacing;

            // Edit
            this.btnEdit.Location = new System.Drawing.Point(8, iconBtnY);
            this.btnEdit.Size = new System.Drawing.Size(iconBtnSize, iconBtnSize);
            this.btnEdit.Text = "✏️";
            this.btnEdit.Font = new System.Drawing.Font("Segoe UI", 16F);
            this.btnEdit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnEdit.FlatAppearance.BorderSize = 0;
            this.btnEdit.Click += new System.EventHandler(this.btnEdit_Click);
            iconBtnY += iconBtnSize + iconBtnSpacing;

            // Theme Toggle
            this.panelLeftSidebar.Controls.Add(this.btnVideoList);
            this.panelLeftSidebar.Controls.Add(this.btnSelectAll);
            this.panelLeftSidebar.Controls.Add(this.btnEdit);

            // 
            // panelCenter (Video Area + Controls)
            // 
            this.panelCenter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelCenter.Padding = new System.Windows.Forms.Padding(16);
            this.panelCenter.BackColor = System.Drawing.Color.FromArgb(243, 244, 246);
            this.panelCenter.Controls.Add(this.pictureBoxVideo);
            this.panelCenter.Controls.Add(this.panelVideoControls);

            // 
            // pictureBoxVideo
            // 
            this.pictureBoxVideo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBoxVideo.BackColor = System.Drawing.Color.Black;
            this.pictureBoxVideo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxVideo.TabStop = true;
            this.pictureBoxVideo.Paint += new System.Windows.Forms.PaintEventHandler(this.pictureBoxVideo_Paint);
            this.pictureBoxVideo.MouseDown += new System.Windows.Forms.MouseEventHandler(this.pictureBoxVideo_MouseDown);
            this.pictureBoxVideo.MouseMove += new System.Windows.Forms.MouseEventHandler(this.pictureBoxVideo_MouseMove);
            this.pictureBoxVideo.MouseUp += new System.Windows.Forms.MouseEventHandler(this.pictureBoxVideo_MouseUp);
            this.pictureBoxVideo.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.pictureBoxVideo_MouseDoubleClick);

            // 
            // labelSubtitleTimestamp (자막 타임스탬프 표시 - 좌측 하단)
            // 
            this.labelSubtitleTimestamp = new System.Windows.Forms.Label();
            this.labelSubtitleTimestamp.AutoSize = false;
            this.labelSubtitleTimestamp.Size = new System.Drawing.Size(200, 30);
            this.labelSubtitleTimestamp.BackColor = System.Drawing.Color.FromArgb(180, 0, 0, 0);
            this.labelSubtitleTimestamp.ForeColor = System.Drawing.Color.White;
            this.labelSubtitleTimestamp.Font = new System.Drawing.Font("Consolas", 11F, System.Drawing.FontStyle.Bold);
            this.labelSubtitleTimestamp.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.labelSubtitleTimestamp.Text = "";
            this.labelSubtitleTimestamp.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            this.pictureBoxVideo.Controls.Add(this.labelSubtitleTimestamp);
            this.pictureBoxVideo.Resize += new System.EventHandler(this.pictureBoxVideo_Resize);

            // 
            // panelVideoControls (Bottom control bar)
            // 
            this.panelVideoControls.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelVideoControls.Height = 120;
            this.panelVideoControls.BackColor = System.Drawing.Color.White;
            this.panelVideoControls.Padding = new System.Windows.Forms.Padding(12);
            this.panelVideoControls.Controls.Add(this.groupBoxObjectInfo);
            this.panelVideoControls.Controls.Add(this.btnPlay);
            this.panelVideoControls.Controls.Add(this.btnRewind);
            this.panelVideoControls.Controls.Add(this.btnForward);
            this.panelVideoControls.Controls.Add(this.labelTimeInfo);
            this.panelVideoControls.Controls.Add(this.btnEntry);
            this.panelVideoControls.Controls.Add(this.btnExit);
            this.panelVideoControls.Controls.Add(this.btnToggleSubtitle);
            this.panelVideoControls.Controls.Add(this.btnToggleYoloDetections);
            this.panelVideoControls.Controls.Add(this.btnToggleSkeleton);
            this.panelVideoControls.Controls.Add(this.btnToggleAttributeView);
            this.panelVideoControls.Controls.Add(this.panelTimeline);
            this.panelVideoControls.Resize += new System.EventHandler(this.panelVideoControls_Resize);

            // Playback buttons
            this.btnPlay.Text = "▶";
            this.btnPlay.Location = new System.Drawing.Point(82, 16);
            this.btnPlay.Size = new System.Drawing.Size(40, 40);
            this.btnPlay.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnPlay.FlatAppearance.BorderSize = 0;
            this.btnPlay.Font = new System.Drawing.Font("Segoe UI", 14F);
            this.btnPlay.Click += new System.EventHandler(this.btnPlay_Click);

            this.btnRewind.Text = "⏪";
            this.btnRewind.Location = new System.Drawing.Point(42, 16);
            this.btnRewind.Size = new System.Drawing.Size(40, 40);
            this.btnRewind.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRewind.FlatAppearance.BorderSize = 0;
            this.btnRewind.Font = new System.Drawing.Font("Segoe UI", 14F);
            this.btnRewind.Click += new System.EventHandler(this.btnRewind_Click);

            this.btnForward.Text = "⏩";
            this.btnForward.Location = new System.Drawing.Point(118, 16);
            this.btnForward.Size = new System.Drawing.Size(40, 40);
            this.btnForward.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnForward.FlatAppearance.BorderSize = 0;
            this.btnForward.Font = new System.Drawing.Font("Segoe UI", 14F);
            this.btnForward.Click += new System.EventHandler(this.btnForward_Click);

            // Time Info
            this.labelTimeInfo.Text = "00:00:00 / 00:00:00 1.0x";
            this.labelTimeInfo.Location = new System.Drawing.Point(160, 24);
            this.labelTimeInfo.Size = new System.Drawing.Size(200, 25);
            this.labelTimeInfo.ForeColor = System.Drawing.Color.Gray;
            this.labelTimeInfo.Font = new System.Drawing.Font("Consolas", 10F);

            // Real-time (subtitle replacement) label

            // Entry/Exit buttons
            this.btnEntry.Text = "Entry";
            this.btnEntry.Location = new System.Drawing.Point(860, 16);
            this.btnEntry.Size = new System.Drawing.Size(110, 40);
            this.btnEntry.BackColor = System.Drawing.Color.FromArgb(250, 204, 21);
            this.btnEntry.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnEntry.FlatAppearance.BorderSize = 0;
            this.btnEntry.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnEntry.Click += new System.EventHandler(this.btnEntry_Click);

            this.btnExit.Text = "Exit";
            this.btnExit.Location = new System.Drawing.Point(980, 16);
            this.btnExit.Size = new System.Drawing.Size(110, 40);
            this.btnExit.BackColor = System.Drawing.Color.FromArgb(250, 204, 21);
            this.btnExit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExit.FlatAppearance.BorderSize = 0;
            this.btnExit.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnExit.Click += new System.EventHandler(this.btnExit_Click);

            // btnToggleSubtitle (자막 열기/닫기)
            this.btnToggleSubtitle.Text = "자막 열기";
            this.btnToggleSubtitle.Location = new System.Drawing.Point(480, 16);
            this.btnToggleSubtitle.Size = new System.Drawing.Size(100, 40);
            this.btnToggleSubtitle.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.btnToggleSubtitle.ForeColor = System.Drawing.Color.White;
            this.btnToggleSubtitle.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnToggleSubtitle.FlatAppearance.BorderSize = 0;
            this.btnToggleSubtitle.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnToggleSubtitle.TabStop = false;
            this.btnToggleSubtitle.Click += new System.EventHandler(this.btnToggleSubtitle_Click);

            // btnToggleYoloDetections (YOLO 탐지 박스 표시/숨기기)
            this.btnToggleYoloDetections.Text = "YOLO 탐지";
            this.btnToggleYoloDetections.Location = new System.Drawing.Point(370, 16);
            this.btnToggleYoloDetections.Size = new System.Drawing.Size(100, 40);
            this.btnToggleYoloDetections.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.btnToggleYoloDetections.ForeColor = System.Drawing.Color.White;
            this.btnToggleYoloDetections.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnToggleYoloDetections.FlatAppearance.BorderSize = 0;
            this.btnToggleYoloDetections.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnToggleYoloDetections.TabStop = false;
            this.btnToggleYoloDetections.Click += new System.EventHandler(this.btnToggleYoloDetections_Click);

            // btnToggleSkeleton (Skeleton 표시/숨기기)
            this.btnToggleSkeleton.Text = "Skeleton 표시";
            this.btnToggleSkeleton.Location = new System.Drawing.Point(750, 16);
            this.btnToggleSkeleton.Size = new System.Drawing.Size(100, 40);
            this.btnToggleSkeleton.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.btnToggleSkeleton.ForeColor = System.Drawing.Color.White;
            this.btnToggleSkeleton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnToggleSkeleton.FlatAppearance.BorderSize = 0;
            this.btnToggleSkeleton.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnToggleSkeleton.TabStop = false;
            this.btnToggleSkeleton.Click += new System.EventHandler(this.btnToggleSkeleton_Click);

            // btnToggleAttributeView (속성값 조회)
            this.btnToggleAttributeView.Text = "속성값 조회";
            this.btnToggleAttributeView.Location = new System.Drawing.Point(590, 16);
            this.btnToggleAttributeView.Size = new System.Drawing.Size(150, 40);
            this.btnToggleAttributeView.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.btnToggleAttributeView.ForeColor = System.Drawing.Color.White;
            this.btnToggleAttributeView.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnToggleAttributeView.FlatAppearance.BorderSize = 0;
            this.btnToggleAttributeView.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnToggleAttributeView.TabStop = false;
            this.btnToggleAttributeView.Click += new System.EventHandler(this.btnToggleAttributeView_Click);

            // 
            // panelTimeline (Progress bar with markers - 좌측에 고정)
            // 
            this.panelTimeline.Location = new System.Drawing.Point(70, 70);
            this.panelTimeline.Size = new System.Drawing.Size(1000, 30);
            this.panelTimeline.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.panelTimeline.BackColor = System.Drawing.Color.FromArgb(229, 231, 235);
            this.panelTimeline.Paint += new System.Windows.Forms.PaintEventHandler(this.panelTimeline_Paint);
            this.panelTimeline.MouseDown += new System.Windows.Forms.MouseEventHandler(this.panelTimeline_MouseDown);
            this.panelTimeline.MouseMove += new System.Windows.Forms.MouseEventHandler(this.panelTimeline_MouseMove);
            this.panelTimeline.MouseUp += new System.Windows.Forms.MouseEventHandler(this.panelTimeline_MouseUp);

            // 
            // panelRightSidebar (Info Panel)
            // 
            this.panelRightSidebar.Dock = System.Windows.Forms.DockStyle.Right;
            this.panelRightSidebar.Width = 320;
            this.panelRightSidebar.BackColor = System.Drawing.Color.White;
            this.panelRightSidebar.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelRightSidebar.Padding = new System.Windows.Forms.Padding(16);
            this.panelRightSidebar.AutoScroll = true;
            this.panelRightSidebar.TabStop = false;

            int rightY = 0; // ✅ 상단 정렬 (Padding이 있으므로 0으로 시작)

            // 
            // groupBoxObjectInfo (하단 플레이바 - 우측에 고정 배치)
            // 
            this.groupBoxObjectInfo.Text = "Object Info";
            this.groupBoxObjectInfo.Size = new System.Drawing.Size(280, 100);
            this.groupBoxObjectInfo.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.groupBoxObjectInfo.BackColor = System.Drawing.Color.White;
            this.groupBoxObjectInfo.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.groupBoxObjectInfo.TabStop = false;
            this.groupBoxObjectInfo.Controls.Add(this.labelObjectLabel);

            this.labelObjectLabel.Text = "Label: -";
            this.labelObjectLabel.Location = new System.Drawing.Point(8, 18);
            this.labelObjectLabel.Size = new System.Drawing.Size(264, 78);
            this.labelObjectLabel.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold);
            this.labelObjectLabel.ForeColor = System.Drawing.Color.FromArgb(55, 65, 81);
            this.labelObjectLabel.AutoSize = false;

            this.labelPrevWaypoint.Text = "Previous Waypoint: -";
            this.labelPrevWaypoint.Location = new System.Drawing.Point(8, 38);
            this.labelPrevWaypoint.Size = new System.Drawing.Size(264, 20);
            this.labelPrevWaypoint.Font = new System.Drawing.Font("Segoe UI", 7F);
            this.labelPrevWaypoint.ForeColor = System.Drawing.Color.Gray;
            this.labelPrevWaypoint.AutoSize = false;
            this.labelPrevWaypoint.Visible = false;

            this.labelNextWaypoint.Text = "Next Waypoint: -";
            this.labelNextWaypoint.Location = new System.Drawing.Point(8, 58);
            this.labelNextWaypoint.Size = new System.Drawing.Size(264, 20);
            this.labelNextWaypoint.Font = new System.Drawing.Font("Segoe UI", 7F);
            this.labelNextWaypoint.ForeColor = System.Drawing.Color.Gray;
            this.labelNextWaypoint.AutoSize = false;
            this.labelNextWaypoint.Visible = false;

            // 
            // groupBoxPersonWaypoint (빨강 테마)
            // 
            this.groupBoxPersonWaypoint.Text = "■ Person Waypoint";
            this.groupBoxPersonWaypoint.Location = new System.Drawing.Point(12, 0); // ✅ 상단 정렬 (Padding이 있으므로 0으로 설정)
            this.groupBoxPersonWaypoint.Size = new System.Drawing.Size(280, 250);
            this.groupBoxPersonWaypoint.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.groupBoxPersonWaypoint.ForeColor = System.Drawing.Color.FromArgb(255, 107, 107); // 빨강
            this.groupBoxPersonWaypoint.TabStop = false;

            this.listViewPersonWaypoints = new System.Windows.Forms.ListView();
            this.listViewPersonWaypoints.Location = new System.Drawing.Point(12, 25);
            this.listViewPersonWaypoints.Size = new System.Drawing.Size(260, 220);
            this.listViewPersonWaypoints.View = System.Windows.Forms.View.Details;
            this.listViewPersonWaypoints.FullRowSelect = true;
            this.listViewPersonWaypoints.TabStop = false;
            this.listViewPersonWaypoints.BackColor = System.Drawing.Color.FromArgb(255, 224, 224); // 연한 빨강
            this.listViewPersonWaypoints.Columns.Add("Entry", 80);
            this.listViewPersonWaypoints.Columns.Add("Exit", 80);
            this.listViewPersonWaypoints.Columns.Add("객체", 95);
            this.listViewPersonWaypoints.Click += new System.EventHandler(this.listViewPersonWaypoints_Click);
            this.listViewPersonWaypoints.MouseDown += new System.Windows.Forms.MouseEventHandler(this.listViewWaypoints_MouseDown);

            this.groupBoxPersonWaypoint.Controls.Add(this.listViewPersonWaypoints);

            rightY += 270;

            // 
            // groupBoxVehicleWaypoint (파랑 테마)
            // 
            this.groupBoxVehicleWaypoint.Text = "■ Vehicle Waypoint";
            this.groupBoxVehicleWaypoint.Location = new System.Drawing.Point(12, rightY);
            this.groupBoxVehicleWaypoint.Size = new System.Drawing.Size(280, 250);
            this.groupBoxVehicleWaypoint.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.groupBoxVehicleWaypoint.ForeColor = System.Drawing.Color.FromArgb(107, 158, 255); // 파랑
            this.groupBoxVehicleWaypoint.TabStop = false;

            this.listViewVehicleWaypoints = new System.Windows.Forms.ListView();
            this.listViewVehicleWaypoints.Location = new System.Drawing.Point(12, 25);
            this.listViewVehicleWaypoints.Size = new System.Drawing.Size(260, 220);
            this.listViewVehicleWaypoints.View = System.Windows.Forms.View.Details;
            this.listViewVehicleWaypoints.FullRowSelect = true;
            this.listViewVehicleWaypoints.TabStop = false;
            this.listViewVehicleWaypoints.BackColor = System.Drawing.Color.FromArgb(224, 232, 255); // 연한 파랑
            this.listViewVehicleWaypoints.Columns.Add("Entry", 80);
            this.listViewVehicleWaypoints.Columns.Add("Exit", 80);
            this.listViewVehicleWaypoints.Columns.Add("객체", 95);
            this.listViewVehicleWaypoints.Click += new System.EventHandler(this.listViewVehicleWaypoints_Click);
            this.listViewVehicleWaypoints.MouseDown += new System.Windows.Forms.MouseEventHandler(this.listViewWaypoints_MouseDown);

            this.groupBoxVehicleWaypoint.Controls.Add(this.listViewVehicleWaypoints);

            rightY += 270;

            // 
            // groupBoxEventWaypoint (초록 테마)
            // 
            this.groupBoxEventWaypoint.Text = "■ Event Waypoint";
            this.groupBoxEventWaypoint.Location = new System.Drawing.Point(12, rightY);
            this.groupBoxEventWaypoint.Size = new System.Drawing.Size(280, 250);
            this.groupBoxEventWaypoint.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.groupBoxEventWaypoint.ForeColor = System.Drawing.Color.FromArgb(107, 255, 107); // 초록
            this.groupBoxEventWaypoint.TabStop = false;

            this.listViewEventWaypoints = new System.Windows.Forms.ListView();
            this.listViewEventWaypoints.Location = new System.Drawing.Point(12, 25);
            this.listViewEventWaypoints.Size = new System.Drawing.Size(260, 220);
            this.listViewEventWaypoints.View = System.Windows.Forms.View.Details;
            this.listViewEventWaypoints.FullRowSelect = true;
            this.listViewEventWaypoints.TabStop = false;
            this.listViewEventWaypoints.BackColor = System.Drawing.Color.FromArgb(224, 255, 224); // 연한 초록
            this.listViewEventWaypoints.Columns.Add("Event", 70);
            this.listViewEventWaypoints.Columns.Add("Entry", 80);
            this.listViewEventWaypoints.Columns.Add("Exit", 80);
            this.listViewEventWaypoints.Columns.Add("객체", 70);
            this.listViewEventWaypoints.Click += new System.EventHandler(this.listViewEventWaypoints_Click);
            this.listViewEventWaypoints.MouseDown += new System.Windows.Forms.MouseEventHandler(this.listViewWaypoints_MouseDown);
            this.listViewEventWaypoints.MouseUp += new System.Windows.Forms.MouseEventHandler(this.listViewEventWaypoints_MouseUp);

            this.groupBoxEventWaypoint.Controls.Add(this.listViewEventWaypoints);

            rightY += 270;

            // 
            // btnDeleteSelectedWaypoint (통합 Waypoint 삭제 버튼 - 모든 패널 아래에 배치)
            // 
            this.btnDeleteEventWaypoint = new System.Windows.Forms.Button();
            this.btnDeleteEventWaypoint.Text = "선택한 Waypoint 삭제";
            this.btnDeleteEventWaypoint.Location = new System.Drawing.Point(12, rightY);
            this.btnDeleteEventWaypoint.Size = new System.Drawing.Size(280, 35);
            this.btnDeleteEventWaypoint.TabStop = false;
            this.btnDeleteEventWaypoint.BackColor = System.Drawing.Color.FromArgb(239, 68, 68); // 빨간색
            this.btnDeleteEventWaypoint.ForeColor = System.Drawing.Color.White;
            this.btnDeleteEventWaypoint.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnDeleteEventWaypoint.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDeleteEventWaypoint.FlatAppearance.BorderSize = 0;
            this.btnDeleteEventWaypoint.Click += new System.EventHandler(this.btnDeleteSelectedWaypoint_Click);

            rightY += 50;

            // 
            // groupBoxLabels
            // 
            this.groupBoxLabels.Text = "Labels";
            this.groupBoxLabels.Location = new System.Drawing.Point(12, rightY);
            this.groupBoxLabels.Size = new System.Drawing.Size(288, 260);
            this.groupBoxLabels.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.groupBoxLabels.TabStop = false;

            // Label type selection buttons (상단 가로 배열)
            this.btnLabelPerson.Text = "person";
            this.btnLabelPerson.Location = new System.Drawing.Point(12, 25);
            this.btnLabelPerson.Size = new System.Drawing.Size(78, 35);
            this.btnLabelPerson.BackColor = System.Drawing.Color.FromArgb(252, 231, 243);
            this.btnLabelPerson.ForeColor = System.Drawing.Color.FromArgb(157, 23, 77);
            this.btnLabelPerson.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLabelPerson.FlatAppearance.BorderSize = 2;
            this.btnLabelPerson.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(157, 23, 77);
            this.btnLabelPerson.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnLabelPerson.TabStop = false;
            this.btnLabelPerson.Click += new System.EventHandler(this.btnLabelPerson_Click);

            this.btnLabelVehicle.Text = "vehicle";
            this.btnLabelVehicle.Location = new System.Drawing.Point(95, 25);
            this.btnLabelVehicle.Size = new System.Drawing.Size(78, 35);
            this.btnLabelVehicle.BackColor = System.Drawing.Color.FromArgb(219, 234, 254);
            this.btnLabelVehicle.ForeColor = System.Drawing.Color.FromArgb(30, 64, 175);
            this.btnLabelVehicle.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLabelVehicle.FlatAppearance.BorderSize = 2;
            this.btnLabelVehicle.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(30, 64, 175);
            this.btnLabelVehicle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnLabelVehicle.TabStop = false;
            this.btnLabelVehicle.Click += new System.EventHandler(this.btnLabelVehicle_Click);

            this.btnLabelEvent.Text = "event";
            this.btnLabelEvent.Location = new System.Drawing.Point(178, 25);
            this.btnLabelEvent.Size = new System.Drawing.Size(78, 35);
            this.btnLabelEvent.BackColor = System.Drawing.Color.FromArgb(220, 252, 231);
            this.btnLabelEvent.ForeColor = System.Drawing.Color.FromArgb(20, 83, 45);
            this.btnLabelEvent.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLabelEvent.FlatAppearance.BorderSize = 2;
            this.btnLabelEvent.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(20, 83, 45);
            this.btnLabelEvent.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnLabelEvent.TabStop = false;
            this.btnLabelEvent.Click += new System.EventHandler(this.btnLabelEvent_Click);

            // Person 리스트 토글 버튼 (레이블 대체)
            this.labelPersonList = new System.Windows.Forms.Label();
            this.labelPersonList.Text = "> person";
            this.labelPersonList.Location = new System.Drawing.Point(8, 70);
            this.labelPersonList.Size = new System.Drawing.Size(270, 30);
            this.labelPersonList.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.labelPersonList.BackColor = System.Drawing.Color.FromArgb(252, 231, 243);
            this.labelPersonList.ForeColor = System.Drawing.Color.FromArgb(157, 23, 77);
            this.labelPersonList.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.labelPersonList.Padding = new System.Windows.Forms.Padding(10, 0, 0, 0);
            this.labelPersonList.Cursor = System.Windows.Forms.Cursors.Hand;
            this.labelPersonList.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelPersonList.Click += new System.EventHandler(this.TogglePersonPanel);

            // Person 리스트 패널
            this.panelPersonList = new System.Windows.Forms.Panel();
            this.panelPersonList.Location = new System.Drawing.Point(8, 100);
            this.panelPersonList.Size = new System.Drawing.Size(270, 100);
            this.panelPersonList.BackColor = System.Drawing.Color.White;
            this.panelPersonList.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelPersonList.AutoScroll = true;
            this.panelPersonList.MaximumSize = new System.Drawing.Size(270, 100);
            this.panelPersonList.TabStop = false;
            this.panelPersonList.Visible = false; // 초기: 접힌 상태

            // Vehicle 리스트 토글 버튼 (레이블 대체)
            this.labelVehicleList = new System.Windows.Forms.Label();
            this.labelVehicleList.Text = "> vehicle";
            this.labelVehicleList.Location = new System.Drawing.Point(8, 100);
            this.labelVehicleList.Size = new System.Drawing.Size(270, 30);
            this.labelVehicleList.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.labelVehicleList.BackColor = System.Drawing.Color.FromArgb(219, 234, 254);
            this.labelVehicleList.ForeColor = System.Drawing.Color.FromArgb(30, 64, 175);
            this.labelVehicleList.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.labelVehicleList.Padding = new System.Windows.Forms.Padding(10, 0, 0, 0);
            this.labelVehicleList.Cursor = System.Windows.Forms.Cursors.Hand;
            this.labelVehicleList.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelVehicleList.Click += new System.EventHandler(this.ToggleVehiclePanel);

            // Vehicle 리스트 패널
            this.panelVehicleList = new System.Windows.Forms.Panel();
            this.panelVehicleList.Location = new System.Drawing.Point(8, 130);
            this.panelVehicleList.Size = new System.Drawing.Size(270, 100);
            this.panelVehicleList.BackColor = System.Drawing.Color.White;
            this.panelVehicleList.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelVehicleList.AutoScroll = true;
            this.panelVehicleList.MaximumSize = new System.Drawing.Size(270, 100);
            this.panelVehicleList.TabStop = false;
            this.panelVehicleList.Visible = false; // 초기: 접힌 상태

            // Event 리스트 토글 버튼 (레이블 대체)
            this.labelEventList = new System.Windows.Forms.Label();
            this.labelEventList.Text = "> event";
            this.labelEventList.Location = new System.Drawing.Point(8, 130);
            this.labelEventList.Size = new System.Drawing.Size(270, 30);
            this.labelEventList.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.labelEventList.BackColor = System.Drawing.Color.FromArgb(220, 252, 231);
            this.labelEventList.ForeColor = System.Drawing.Color.FromArgb(20, 83, 45);
            this.labelEventList.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.labelEventList.Padding = new System.Windows.Forms.Padding(10, 0, 0, 0);
            this.labelEventList.Cursor = System.Windows.Forms.Cursors.Hand;
            this.labelEventList.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelEventList.Click += new System.EventHandler(this.ToggleEventPanel);

            // Event 리스트 패널
            this.panelEventList = new System.Windows.Forms.Panel();
            this.panelEventList.Location = new System.Drawing.Point(8, 160);
            this.panelEventList.Size = new System.Drawing.Size(270, 100);
            this.panelEventList.BackColor = System.Drawing.Color.White;
            this.panelEventList.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelEventList.AutoScroll = true;
            this.panelEventList.MaximumSize = new System.Drawing.Size(270, 100);
            this.panelEventList.TabStop = false;
            this.panelEventList.Visible = false; // 초기: 접힌 상태

            // 기존 panelBboxList는 호환성을 위해 panelPersonList를 참조
            this.panelBboxList = this.panelPersonList;

            // 삭제 버튼
            this.btnDeleteLabel.Text = "선택한 Bbox 삭제";
            this.btnDeleteLabel.Location = new System.Drawing.Point(8, 165);
            this.btnDeleteLabel.Size = new System.Drawing.Size(270, 35);
            this.btnDeleteLabel.BackColor = System.Drawing.Color.FromArgb(220, 38, 38);
            this.btnDeleteLabel.ForeColor = System.Drawing.Color.White;
            this.btnDeleteLabel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDeleteLabel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnDeleteLabel.TabStop = false;
            this.btnDeleteLabel.Click += new System.EventHandler(this.btnDeleteLabel_Click);

            // JSON 저장 버튼 (하단)
            this.btnExportJsonInLabels.Text = "JSON 저장";
            this.btnExportJsonInLabels.Location = new System.Drawing.Point(8, 207);
            this.btnExportJsonInLabels.Size = new System.Drawing.Size(270, 35);
            this.btnExportJsonInLabels.BackColor = System.Drawing.Color.FromArgb(34, 197, 94);
            this.btnExportJsonInLabels.ForeColor = System.Drawing.Color.White;
            this.btnExportJsonInLabels.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExportJsonInLabels.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnExportJsonInLabels.TabStop = false;
            this.btnExportJsonInLabels.Click += new System.EventHandler(this.btnExportJson_Click);

            this.groupBoxLabels.Controls.Add(this.btnLabelPerson);
            this.groupBoxLabels.Controls.Add(this.btnLabelVehicle);
            this.groupBoxLabels.Controls.Add(this.btnLabelEvent);
            this.groupBoxLabels.Controls.Add(this.labelPersonList);
            this.groupBoxLabels.Controls.Add(this.panelPersonList);
            this.groupBoxLabels.Controls.Add(this.labelVehicleList);
            this.groupBoxLabels.Controls.Add(this.panelVehicleList);
            this.groupBoxLabels.Controls.Add(this.labelEventList);
            this.groupBoxLabels.Controls.Add(this.panelEventList);
            this.groupBoxLabels.Controls.Add(this.btnDeleteLabel);
            this.groupBoxLabels.Controls.Add(this.btnExportJsonInLabels);

            rightY += 275;

            this.panelRightSidebar.Controls.Add(this.groupBoxPersonWaypoint);
            this.panelRightSidebar.Controls.Add(this.groupBoxVehicleWaypoint);
            this.panelRightSidebar.Controls.Add(this.groupBoxEventWaypoint);
            this.panelRightSidebar.Controls.Add(this.btnDeleteEventWaypoint); // ✅ 통합 삭제 버튼을 패널 외부에 배치
            this.panelRightSidebar.Controls.Add(this.groupBoxLabels);

            // 
            // timerPlayback
            // 
            this.timerPlayback.Interval = 33; // ~30 FPS
            this.timerPlayback.Tick += new System.EventHandler(this.timerPlayback_Tick);

            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1600, 900);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Controls.Add(this.panelMainContainer);
            this.Controls.Add(this.panelHeader);
            this.KeyPreview = true;
            this.Name = "Form1";
            this.Text = "CCTV Video Labeling Tool";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Form1_KeyDown);
            this.Load += new System.EventHandler(this.Form1_Load);

            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxVideo)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        // Header
        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.Label labelTitle;
        private System.Windows.Forms.Button btnSelectFolder;
        private System.Windows.Forms.Button btnSelectFolderPath;
        private System.Windows.Forms.Button btnExportJson;
        private System.Windows.Forms.Button btnDeleteJson;
        private System.Windows.Forms.Label labelBoxCount;
        private System.Windows.Forms.Label labelCurrentJsonFile;
        private System.Windows.Forms.Button btnMinimize;
        private System.Windows.Forms.Button btnMaximize;
        private System.Windows.Forms.Button btnClose;
        

        // Main Container
        private System.Windows.Forms.Panel panelMainContainer;

        // Left Sidebar
        private System.Windows.Forms.Panel panelLeftSidebar;
        private System.Windows.Forms.Button btnVideoList;
        private System.Windows.Forms.Button btnSelectAll;
        private System.Windows.Forms.Button btnEdit;

        // Center Video
        private System.Windows.Forms.Panel panelCenter;
        private System.Windows.Forms.PictureBox pictureBoxVideo;

        // Video Controls
        private System.Windows.Forms.Panel panelVideoControls;
        private System.Windows.Forms.Button btnPlay;
        private System.Windows.Forms.Button btnRewind;
        private System.Windows.Forms.Button btnForward;
        private System.Windows.Forms.Label labelTimeInfo;
        private System.Windows.Forms.Label labelSubtitleTimestamp;
        private System.Windows.Forms.Button btnEntry;
        private System.Windows.Forms.Button btnExit;
        private System.Windows.Forms.Button btnToggleSubtitle;
        private System.Windows.Forms.Button btnToggleYoloDetections;
        private System.Windows.Forms.Button btnToggleSkeleton;
        private System.Windows.Forms.Button btnToggleAttributeView;
        private System.Windows.Forms.Panel panelTimeline;

        // Right Sidebar
        private System.Windows.Forms.Panel panelRightSidebar;
        private System.Windows.Forms.GroupBox groupBoxObjectInfo;
        private System.Windows.Forms.Label labelObjectLabel;
        private System.Windows.Forms.Label labelPrevWaypoint;
        private System.Windows.Forms.Label labelNextWaypoint;
        private System.Windows.Forms.GroupBox groupBoxPersonWaypoint;
        private System.Windows.Forms.GroupBox groupBoxVehicleWaypoint;
        private System.Windows.Forms.GroupBox groupBoxEventWaypoint;
        private System.Windows.Forms.Label labelWaypointTime;
        private System.Windows.Forms.ListView listViewPersonWaypoints;
        private System.Windows.Forms.ListView listViewVehicleWaypoints;
        private System.Windows.Forms.ListView listViewEventWaypoints;
        private System.Windows.Forms.Button btnDeletePersonWaypoint;
        private System.Windows.Forms.Button btnDeleteVehicleWaypoint;
        private System.Windows.Forms.Button btnDeleteEventWaypoint;
        private System.Windows.Forms.GroupBox groupBoxLabels;
        private System.Windows.Forms.Button btnLabelPerson;
        private System.Windows.Forms.Button btnLabelVehicle;
        private System.Windows.Forms.Button btnLabelEvent;
        private System.Windows.Forms.Label labelPersonList;
        private System.Windows.Forms.Panel panelPersonList;
        private System.Windows.Forms.Label labelVehicleList;
        private System.Windows.Forms.Panel panelVehicleList;
        private System.Windows.Forms.Label labelEventList;
        private System.Windows.Forms.Panel panelEventList;
        private System.Windows.Forms.Panel panelBboxList;
        private System.Windows.Forms.Button btnDeleteLabel;
        private System.Windows.Forms.Button btnExportJsonInLabels;
        private System.Windows.Forms.Label labelModifyBox;
        private System.Windows.Forms.ComboBox comboBoxPerson;
        private System.Windows.Forms.ComboBox comboBoxVehicle;
        private System.Windows.Forms.ComboBox comboBoxEvent;

        // Timer
        private System.Windows.Forms.Timer timerPlayback;
    }
}