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
            this.trackBarVolume = new System.Windows.Forms.TrackBar();
            this.panelTimeline = new System.Windows.Forms.Panel();

            // Right Sidebar (Info Panel)
            this.panelRightSidebar = new System.Windows.Forms.Panel();
            this.groupBoxObjectInfo = new System.Windows.Forms.GroupBox();
            this.labelObjectLabel = new System.Windows.Forms.Label();
            this.labelPrevWaypoint = new System.Windows.Forms.Label();
            this.labelNextWaypoint = new System.Windows.Forms.Label();
            this.groupBoxWaypoint = new System.Windows.Forms.GroupBox();
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
            ((System.ComponentModel.ISupportInitialize)(this.trackBarVolume)).BeginInit();
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
            this.panelHeader.Controls.Add(this.labelBoxCount);
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

            // Box Count Label
            this.labelBoxCount.Text = "박스 개수:";
            this.labelBoxCount.Location = new System.Drawing.Point(470, 15);
            this.labelBoxCount.Size = new System.Drawing.Size(80, 25);

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
            this.panelVideoControls.Controls.Add(this.btnPlay);
            this.panelVideoControls.Controls.Add(this.btnRewind);
            this.panelVideoControls.Controls.Add(this.btnForward);
            this.panelVideoControls.Controls.Add(this.labelTimeInfo);
            this.panelVideoControls.Controls.Add(this.btnEntry);
            this.panelVideoControls.Controls.Add(this.btnExit);
            this.panelVideoControls.Controls.Add(this.btnToggleSubtitle);
            this.panelVideoControls.Controls.Add(this.trackBarVolume);
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
            this.labelTimeInfo.Text = "00:00:00 / 00:00:00 x264";
            this.labelTimeInfo.Location = new System.Drawing.Point(160, 24);
            this.labelTimeInfo.Size = new System.Drawing.Size(200, 25);
            this.labelTimeInfo.ForeColor = System.Drawing.Color.Gray;
            this.labelTimeInfo.Font = new System.Drawing.Font("Consolas", 10F);

            // Real-time (subtitle replacement) label

            // Entry/Exit buttons
            this.btnEntry.Text = "Entry";
            this.btnEntry.Location = new System.Drawing.Point(600, 16);
            this.btnEntry.Size = new System.Drawing.Size(130, 40);
            this.btnEntry.BackColor = System.Drawing.Color.FromArgb(250, 204, 21);
            this.btnEntry.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnEntry.FlatAppearance.BorderSize = 0;
            this.btnEntry.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnEntry.Click += new System.EventHandler(this.btnEntry_Click);

            this.btnExit.Text = "Exit";
            this.btnExit.Location = new System.Drawing.Point(740, 16);
            this.btnExit.Size = new System.Drawing.Size(130, 40);
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

            // Volume control
            this.trackBarVolume.Location = new System.Drawing.Point(920, 20);
            this.trackBarVolume.Size = new System.Drawing.Size(100, 35);
            this.trackBarVolume.Maximum = 100;
            this.trackBarVolume.Value = 50;
            this.trackBarVolume.TickStyle = System.Windows.Forms.TickStyle.None;

            // 
            // panelTimeline (Progress bar with markers)
            // 
            this.panelTimeline.Location = new System.Drawing.Point(16, 70);
            this.panelTimeline.Size = new System.Drawing.Size(1000, 30);
            this.panelTimeline.Anchor = System.Windows.Forms.AnchorStyles.Top;
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

            int rightY = 16;

            // 
            // groupBoxObjectInfo
            // 
            this.groupBoxObjectInfo.Text = "Object Info";
            this.groupBoxObjectInfo.Location = new System.Drawing.Point(16, rightY);
            this.groupBoxObjectInfo.Size = new System.Drawing.Size(280, 120);
            this.groupBoxObjectInfo.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.groupBoxObjectInfo.Controls.Add(this.labelObjectLabel);
            this.groupBoxObjectInfo.Controls.Add(this.labelPrevWaypoint);
            this.groupBoxObjectInfo.Controls.Add(this.labelNextWaypoint);

            this.labelObjectLabel.Text = "Label: person_01";
            this.labelObjectLabel.Location = new System.Drawing.Point(12, 25);
            this.labelObjectLabel.Size = new System.Drawing.Size(250, 20);
            this.labelObjectLabel.Font = new System.Drawing.Font("Segoe UI", 9F);

            this.labelPrevWaypoint.Text = "Previous Waypoint: C0001.mp4, 00:10:32 - 00:11:05";
            this.labelPrevWaypoint.Location = new System.Drawing.Point(12, 50);
            this.labelPrevWaypoint.Size = new System.Drawing.Size(260, 30);
            this.labelPrevWaypoint.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.labelPrevWaypoint.ForeColor = System.Drawing.Color.Gray;

            this.labelNextWaypoint.Text = "Next Waypoint: C0003.mp4, 00:15:21 - 00:16:01";
            this.labelNextWaypoint.Location = new System.Drawing.Point(12, 85);
            this.labelNextWaypoint.Size = new System.Drawing.Size(260, 30);
            this.labelNextWaypoint.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.labelNextWaypoint.ForeColor = System.Drawing.Color.Gray;

            rightY += 140;

            // 
            // groupBoxWaypoint
            // 
            this.groupBoxWaypoint.Text = "Waypoint";
            this.groupBoxWaypoint.Location = new System.Drawing.Point(16, rightY);
            this.groupBoxWaypoint.Size = new System.Drawing.Size(280, 150);
            this.groupBoxWaypoint.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);

            // ListView for waypoints
            this.listViewWaypoints = new System.Windows.Forms.ListView();
            this.listViewWaypoints.Location = new System.Drawing.Point(12, 25);
            this.listViewWaypoints.Size = new System.Drawing.Size(260, 80);
            this.listViewWaypoints.View = System.Windows.Forms.View.Details;
            this.listViewWaypoints.FullRowSelect = true;
            this.listViewWaypoints.Columns.Add("Entry", 80);
            this.listViewWaypoints.Columns.Add("Exit", 80);
            this.listViewWaypoints.Columns.Add("객체", 60);

            // Delete waypoint button
            this.btnDeleteWaypoint = new System.Windows.Forms.Button();
            this.btnDeleteWaypoint.Text = "선택 삭제";
            this.btnDeleteWaypoint.Location = new System.Drawing.Point(12, 110);
            this.btnDeleteWaypoint.Size = new System.Drawing.Size(100, 30);
            this.btnDeleteWaypoint.Click += new System.EventHandler(this.btnDeleteWaypoint_Click);

            this.groupBoxWaypoint.Controls.Add(this.listViewWaypoints);
            this.groupBoxWaypoint.Controls.Add(this.btnDeleteWaypoint);

            rightY += 170;

            // 
            // groupBoxLabels
            // 
            this.groupBoxLabels.Text = "Labels";
            this.groupBoxLabels.Location = new System.Drawing.Point(16, rightY);
            this.groupBoxLabels.Size = new System.Drawing.Size(280, 425);
            this.groupBoxLabels.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);

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

            // Bbox 목록 표시 영역 (스크롤 가능한 패널)
            this.panelBboxList.Location = new System.Drawing.Point(12, 70);
            this.panelBboxList.Size = new System.Drawing.Size(256, 200);
            this.panelBboxList.BackColor = System.Drawing.Color.White;
            this.panelBboxList.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelBboxList.AutoScroll = true;

            // 삭제 버튼
            this.btnDeleteLabel.Text = "선택한 Bbox 삭제";
            this.btnDeleteLabel.Location = new System.Drawing.Point(12, 278);
            this.btnDeleteLabel.Size = new System.Drawing.Size(256, 35);
            this.btnDeleteLabel.BackColor = System.Drawing.Color.FromArgb(220, 38, 38);
            this.btnDeleteLabel.ForeColor = System.Drawing.Color.White;
            this.btnDeleteLabel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDeleteLabel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnDeleteLabel.TabStop = false;
            this.btnDeleteLabel.Click += new System.EventHandler(this.btnDeleteLabel_Click);

            // JSON 저장 버튼 (하단)
            this.btnExportJsonInLabels.Text = "JSON 저장";
            this.btnExportJsonInLabels.Location = new System.Drawing.Point(12, 320);
            this.btnExportJsonInLabels.Size = new System.Drawing.Size(256, 35);
            this.btnExportJsonInLabels.BackColor = System.Drawing.Color.FromArgb(34, 197, 94);
            this.btnExportJsonInLabels.ForeColor = System.Drawing.Color.White;
            this.btnExportJsonInLabels.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExportJsonInLabels.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnExportJsonInLabels.TabStop = false;
            this.btnExportJsonInLabels.Click += new System.EventHandler(this.btnExportJson_Click);

            this.groupBoxLabels.Controls.Add(this.btnLabelPerson);
            this.groupBoxLabels.Controls.Add(this.btnLabelVehicle);
            this.groupBoxLabels.Controls.Add(this.btnLabelEvent);
            this.groupBoxLabels.Controls.Add(this.panelBboxList);
            this.groupBoxLabels.Controls.Add(this.btnDeleteLabel);
            this.groupBoxLabels.Controls.Add(this.btnExportJsonInLabels);

            rightY += 445;

            this.panelRightSidebar.Controls.Add(this.groupBoxObjectInfo);
            this.panelRightSidebar.Controls.Add(this.groupBoxWaypoint);
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
            ((System.ComponentModel.ISupportInitialize)(this.trackBarVolume)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        // Header
        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.Label labelTitle;
        private System.Windows.Forms.Button btnSelectFolder;
        private System.Windows.Forms.Button btnSelectFolderPath;
        private System.Windows.Forms.Button btnExportJson;
        private System.Windows.Forms.Label labelBoxCount;
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
        private System.Windows.Forms.TrackBar trackBarVolume;
        private System.Windows.Forms.Panel panelTimeline;

        // Right Sidebar
        private System.Windows.Forms.Panel panelRightSidebar;
        private System.Windows.Forms.GroupBox groupBoxObjectInfo;
        private System.Windows.Forms.Label labelObjectLabel;
        private System.Windows.Forms.Label labelPrevWaypoint;
        private System.Windows.Forms.Label labelNextWaypoint;
        private System.Windows.Forms.GroupBox groupBoxWaypoint;
        private System.Windows.Forms.Label labelWaypointTime;
        private System.Windows.Forms.ListView listViewWaypoints;
        private System.Windows.Forms.Button btnDeleteWaypoint;
        private System.Windows.Forms.GroupBox groupBoxLabels;
        private System.Windows.Forms.Button btnLabelPerson;
        private System.Windows.Forms.Button btnLabelVehicle;
        private System.Windows.Forms.Button btnLabelEvent;
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