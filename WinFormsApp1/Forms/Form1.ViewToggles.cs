using OpenCvSharp;
using Compunet.YoloSharp;
using Compunet.YoloSharp.Data;
using System.Drawing;

namespace WinFormsApp1
{
    public partial class Form1
    {
        #region View Toggles
        private void btnToggleAttributeView_Click(object sender, EventArgs e)
        {
            // ???곸긽 ?ъ깮 以묒뿉???좉? 遺덇? (硫붿떆吏諛뺤뒪 ?놁씠 踰꾪듉留?鍮꾪솢?깊솕)
            if (isPlaying)
            {
                MessageBox.Show("속성 보기 창은 동영상이 일시 정지된 상태에서만 사용할 수 있습니다.\n먼저 재생을 일시 정지하세요.", "안내", MessageBoxButtons.OK, MessageBoxIcon.Information);

                return;
            }
            
            // ???좉?: 耳쒖졇 ?덉쑝硫??꾧퀬, 爰쇱졇 ?덉쑝硫?耳쒓린
            isAttributeViewEnabled = !isAttributeViewEnabled;
            btnToggleAttributeView.Text = isAttributeViewEnabled ? "속성 보기 닫기" : "속성 보기";
            btnToggleAttributeView.BackColor = isAttributeViewEnabled 
                ? System.Drawing.Color.FromArgb(239, 68, 68) // 鍮④컯 (?꾧린)
                : System.Drawing.Color.FromArgb(100, 116, 139); // ?뚯깋 (議고쉶)
            
            UpdateAttributeWindows();
        }
        
        // ??Shift + N ?⑥텞???꾩슜 ?좉? 硫붿꽌??(?ъ깮 以묒뿉???좉? 媛??
        private void ToggleAttributeView()
        {
            // ???좉?: 耳쒖졇 ?덉쑝硫??꾧퀬, 爰쇱졇 ?덉쑝硫?耳쒓린
            isAttributeViewEnabled = !isAttributeViewEnabled;
            
            // 踰꾪듉 UI ?낅뜲?댄듃
            if (btnToggleAttributeView != null)
            {
                btnToggleAttributeView.Text = isAttributeViewEnabled ? "속성 보기 닫기" : "속성 보기";
                btnToggleAttributeView.BackColor = isAttributeViewEnabled 
                    ? System.Drawing.Color.FromArgb(239, 68, 68) // 鍮④컯 (?꾧린)
                    : System.Drawing.Color.FromArgb(100, 116, 139); // ?뚯깋 (議고쉶)
            }
            
            UpdateAttributeWindows();
        }

        // ???띿꽦 李??낅뜲?댄듃 (?좉? ?곹깭???곕씪 ?앹꽦/?쒓굅)
        private void UpdateAttributeWindows()
        {
            // ???ъ깮 以묒뿉???띿꽦 李??낅뜲?댄듃 李⑤떒 (?깅뒫 諛?UI 釉붾줈??諛⑹?)
            if (isPlaying)
            {
                return;
            }
            
            if (isAttributeViewEnabled)
            {
                // ?좉???耳쒖졇 ?덉쑝硫? ?꾩옱 ?꾨젅?꾩쓽 紐⑤뱺 person 諛뺤뒪??????띿꽦 李??앹꽦/?낅뜲?댄듃
                var currentPersonBoxes = boundingBoxes
                    .Where(b => b.FrameIndex == currentFrameIndex && b.Label == "person" && !b.IsDeleted)
                    .ToList();

                // 湲곗〈 李쎈뱾 以??꾩옱 ?꾨젅?꾩뿉 ?녿뒗 寃껊뱾? ?쒓굅
                var windowsToRemove = attributeWindows.Keys
                    .Where(key => !currentPersonBoxes.Any(b => b.PersonId == key.personId && b.FrameIndex == key.frameIndex))
                    .ToList();

                foreach (var key in windowsToRemove)
                {
                    if (attributeWindows.ContainsKey(key))
                    {
                        attributeWindows[key].Close();
                        attributeWindows.Remove(key);
                    }
                }

                // ?꾩옱 ?꾨젅?꾩쓽 person 諛뺤뒪?ㅼ뿉 ???李??앹꽦/?낅뜲?댄듃
                int windowIndex = 0;

                foreach (var box in currentPersonBoxes)
                {
                    var key = (box.PersonId, box.FrameIndex);
                    
                    // ?띿꽦 媛?몄삤湲?
                    var attributes = personAttributeStore.GetAllAttributes(box.PersonId, box.FrameIndex, waypointMarkers, currentVideoFile);

                    if (attributeWindows.ContainsKey(key))
                    {
                        // 湲곗〈 李??낅뜲?댄듃
                        attributeWindows[key].UpdateAttributes(attributes);
                    }
                    else
                    {
                        // ??李??앹꽦
                        // 諛뺤뒪 ?꾩튂 湲곗??쇰줈 珥덇린 ?꾩튂 ?ㅼ젙
                        var viewRect = ImageToView(new RectangleF(box.Rectangle.X, box.Rectangle.Y, 
                            box.Rectangle.Width, box.Rectangle.Height));
                        
                        System.Drawing.Point initialLocation = new System.Drawing.Point(
                            this.Location.X + (int)viewRect.Right + 10 + (windowIndex * 30),
                            this.Location.Y + (int)viewRect.Top + (windowIndex * 30));

                        var window = new PersonAttributeWindow(box.PersonId, box.FrameIndex, attributes, initialLocation);
                        window.FormClosed += (s, e) =>
                        {
                            // 李쎌씠 ?ロ옄 ??Dictionary?먯꽌 ?쒓굅
                            if (attributeWindows.ContainsKey(key))
                            {
                                attributeWindows.Remove(key);
                            }
                        };
                        window.Show();
                        attributeWindows[key] = window;

                        windowIndex++;
                    }
                }
            }
            else
            {
                // ?좉???爰쇱졇 ?덉쑝硫? 紐⑤뱺 ?띿꽦 李??リ린
                foreach (var window in attributeWindows.Values.ToList())
                {
                    window.Close();
                }
                attributeWindows.Clear();
            }
        }

        // ??YOLO ?먯? 諛뺤뒪 ?좉? 踰꾪듉 ?대┃ ?몃뱾??
        private void btnToggleYoloDetections_Click(object sender, EventArgs e)
        {
            try
            {
                if (btnToggleYoloDetections == null)
                {
                    System.Diagnostics.Debug.WriteLine("[YOLO ?먯? ?좉? ?ㅻ쪟] btnToggleYoloDetections媛 null?낅땲??");
                    return;
                }
                
                showYoloDetections = !showYoloDetections;
                System.Diagnostics.Debug.WriteLine($"[YOLO ?먯? ?좉?] showYoloDetections = {showYoloDetections}");
                
                btnToggleYoloDetections.Text = showYoloDetections ? "YOLO ON" : "YOLO";
                btnToggleYoloDetections.BackColor = showYoloDetections
                    ? System.Drawing.Color.FromArgb(34, 197, 94) // ?뱀깋 (?쒖떆 以?
                    : System.Drawing.Color.FromArgb(100, 116, 139); // ?뚯깋 (?④?)
                
                if (showYoloDetections)
                {
                    // ???ъ깮 以묒씠硫??쇱떆?뺤?
                    if (isPlaying)
                    {
                        System.Diagnostics.Debug.WriteLine("[YOLO ?먯? ?좉?] ?ъ깮 以묒씠誘濡??쇱떆?뺤?");
                        isPlaying = false;
                        btnPlay.Text = "Pause";
                        timerPlayback.Stop();
                    }
                    
                    // ???꾩옱 ?꾨젅?꾨쭔 ?먯?
                    if (isYoloAvailable && videoCapture != null && videoCapture.IsOpened())
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO ?먯? ?좉?] ?꾩옱 ?꾨젅??{currentFrameIndex})留??먯? ?쒖옉");
                        DetectCurrentFrameOnly();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO ?먯? ?좉?] YOLO ?ъ슜 遺덇? - isYoloAvailable: {isYoloAvailable}, videoCapture: {(videoCapture != null ? "not null" : "null")}, IsOpened: {(videoCapture != null && videoCapture.IsOpened() ? "true" : "false")}");
                        MessageBox.Show(
                            "YOLO 모델을 사용할 수 없습니다.\n" +
                            "비디오가 로드되어 있는지 확인하세요.",
                            "YOLO 감지 비활성화",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        showYoloDetections = false;
                        if (btnToggleYoloDetections != null)
                        {
                            btnToggleYoloDetections.Text = "YOLO";
                            btnToggleYoloDetections.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
                        }
                    }
                }
                else
                {
                    // ??YOLO ?먯? 以묒?
                    System.Diagnostics.Debug.WriteLine("[YOLO ?먯? ?좉?] YOLO ?먯? 以묒?");
                    StopYoloDetection();
                }
                
                if (pictureBoxVideo != null)
                {
                    pictureBoxVideo.Invalidate();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YOLO ?먯? ?좉? ?ㅻ쪟] {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"YOLO 감지 중 오류가 발생했습니다.\n{ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                // ?곹깭 蹂듭썝
                showYoloDetections = false;
                if (btnToggleYoloDetections != null)
                {
                    try
                    {
                        btnToggleYoloDetections.Text = "YOLO ?먯?";
                        btnToggleYoloDetections.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
                    }
                    catch (Exception restoreEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO ?먯? ?좉?] ?곹깭 蹂듭썝 ?ㅻ쪟: {restoreEx.Message}");
                    }
                }
            }
        }

        private void btnToggleSkeleton_Click(object sender, EventArgs e)
        {
            try
            {
                if (btnToggleSkeleton == null)
                {
                    System.Diagnostics.Debug.WriteLine("[Skeleton ?좉? ?ㅻ쪟] btnToggleSkeleton??null?낅땲??");
                    return;
                }

                showSkeleton = !showSkeleton;
                System.Diagnostics.Debug.WriteLine($"[Skeleton ?좉?] showSkeleton = {showSkeleton}");

                btnToggleSkeleton.Text = showSkeleton ? "Skeleton ON" : "Skeleton OFF";
                btnToggleSkeleton.BackColor = showSkeleton
                    ? System.Drawing.Color.FromArgb(34, 197, 94) // ?뱀깋 (?쒖떆 以?
                    : System.Drawing.Color.FromArgb(100, 116, 139); // ?뚯깋 (?④?)

                if (pictureBoxVideo != null)
                {
                    pictureBoxVideo.Invalidate();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Skeleton ?좉? ?ㅻ쪟] {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"Skeleton 표시 중 오류가 발생했습니다.\n{ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ???꾩옱 ?꾨젅?꾨쭔 YOLO ?먯? ?섑뻾 (?숆린 ?섑띁)
        private void DetectCurrentFrameOnly()
        {
            _ = DetectCurrentFrameOnlyAsync(); // 鍮꾨룞湲곕줈 ?ㅽ뻾 (fire-and-forget)
        }
        
        // ??鍮꾨룞湲?踰꾩쟾 (?ㅼ젣 ?묒뾽 ?섑뻾)
        private async Task DetectCurrentFrameOnlyAsync()
        {
            // ??Semaphore濡??숈떆 ?ㅽ뻾 ?쒗븳 (理쒕? 1媛? 釉붾줈???놁씠)
            if (!await detectionSemaphore.WaitAsync(0))
            {
                System.Diagnostics.Debug.WriteLine("[YOLO ?먯?] ?대? ?먯? ?묒뾽??吏꾪뻾 以묒씠誘濡??ㅽ궢");
                return;
            }
            
            try
            {
                int targetFrame;
                bool shouldReturn = false;
                
                // ??鍮좊Ⅸ 泥댄겕 (理쒖냼?쒖쓽 ??
                lock (yoloDetectionTaskLock)
                {
                    targetFrame = currentFrameIndex;
                    
                    if (string.IsNullOrEmpty(currentVideoFile) || !File.Exists(currentVideoFile))
                    {
                        System.Diagnostics.Debug.WriteLine("[YOLO ?먯?] 鍮꾨뵒???뚯씪???녾굅???좏슚?섏? ?딆쓬");
                        shouldReturn = true;
                    }
                    
                    if (!shouldReturn && (videoCapture == null || !videoCapture.IsOpened()))
                    {
                        System.Diagnostics.Debug.WriteLine("[YOLO ?먯?] 鍮꾨뵒??罹≪쿂媛 ?대젮?덉? ?딆쓬");
                        shouldReturn = true;
                    }
                }
                
                if (shouldReturn)
                    return;
                
                // ??罹먯떆 ?뺤씤
                lock (yoloDetectionCacheLock)
                {
                    if (yoloDetectionCache.ContainsKey(targetFrame))
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] ?꾨젅??{targetFrame}? ?대? 罹먯떆???덉쓬");
                        SafeInvoke(() => pictureBoxVideo?.Invalidate());
                        return;
                    }
                }
                
                // ??湲곗〈 ?묒뾽 鍮꾨룞湲?痍⑥냼 (釉붾줈???놁씠)
                if (yoloDetectionTask != null && !yoloDetectionTask.IsCompleted)
                {
                                System.Diagnostics.Debug.WriteLine($"[YOLO Detection] Cancelled detection for frame {targetFrame}");
                    StopYoloDetection();
                    
                    // ??鍮꾨룞湲곕줈 ?湲?(釉붾줈???놁쓬)
                    try
                    {
                        await yoloDetectionTask.ContinueWith(t => { }, TaskContinuationOptions.OnlyOnRanToCompletion);
                    }
                    catch (Exception waitEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] 湲곗〈 ?묒뾽 ?湲??ㅻ쪟: {waitEx.Message}");
                    }
                }
                
                // ???ㅼ떆 ?쒕쾲 ?뺤씤 (痍⑥냼 ?湲?以묒뿉 ?꾨젅?꾩씠 蹂寃쎈릺?덉쓣 ???덉쓬)
                if (targetFrame != currentFrameIndex)
                {
                    System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] ?꾨젅?꾩씠 蹂寃쎈맖 ({targetFrame} -> {currentFrameIndex}), ?먯? 痍⑥냼");
                    return;
                }
                
                // ???ㅼ떆 罹먯떆 ?뺤씤
                lock (yoloDetectionCacheLock)
                {
                    if (yoloDetectionCache.ContainsKey(targetFrame))
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] ?꾨젅??{targetFrame}? ?대? 罹먯떆???덉쓬 (?湲?以?異붽???");
                        SafeInvoke(() => pictureBoxVideo?.Invalidate());
                        return;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] ?꾩옱 ?꾨젅??{targetFrame})留??먯? ?쒖옉");
                
                yoloDetectionCancellationToken = new CancellationTokenSource();
                var token = yoloDetectionCancellationToken.Token;
                
                // ??Task.Run?쇰줈 ?ㅽ뻾 (鍮꾨룞湲??묒뾽)
                yoloDetectionTask = Task.Run(async () =>
                    {
                        YoloPredictor predictor = null;
                        Mat frame = null;
                        string tempImagePath = null;
                        
                        try
                        {
                            // ??痍⑥냼 ?좏겙 ?뺤씤
                            token.ThrowIfCancellationRequested();
                            
                            // ???꾨젅?꾩씠 蹂寃쎈릺?덈뒗吏 ?뺤씤
                            if (targetFrame != currentFrameIndex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] ?묒뾽 ?쒖옉 ???꾨젅?꾩씠 蹂寃쎈맖 ({targetFrame} -> {currentFrameIndex})");
                                return;
                            }
                            
                            // YOLO Predictor ?앹꽦
                            try
                            {
                                System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] 紐⑤뜽 濡쒕뱶 ?쒖옉: {yoloModelPath}");
                                predictor = new YoloPredictor(yoloModelPath);
                                System.Diagnostics.Debug.WriteLine("[YOLO ?먯?] 紐⑤뜽 濡쒕뱶 ?꾨즺");
                            }
                            catch (Exception ex)
                            {
                                string errorDetails = $"[YOLO ?먯? ?ㅻ쪟] 紐⑤뜽 濡쒕뱶 ?ㅽ뙣: {ex.Message}\n{ex.StackTrace}";
                                if (ex.InnerException != null)
                                {
                                    errorDetails += $"\n\n?대? ?덉쇅: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
                                }
                                System.Diagnostics.Debug.WriteLine(errorDetails);
                                
                                // CUDA 愿???먮윭?몄? ?뺤씤
                                string errorMessage = ex.Message;
                                if (ex.Message.Contains("CUDA") || ex.Message.Contains("cuda") || 
                                    ex.Message.Contains("GPU") || ex.Message.Contains("gpu") ||
                                    ex.InnerException != null && (ex.InnerException.Message.Contains("CUDA") || 
                                                                   ex.InnerException.Message.Contains("cuda")))
                                {
                                    errorMessage += "\n\n[CUDA 愿???먮윭]\n" +
                                                  "NVIDIA ?쒕씪?대쾭, CUDA Toolkit, cuDNN 踰꾩쟾???뺤씤?섏꽭??\n" +
                                                  "?먯꽭???닿껐 諛⑸쾿? ?꾨줈洹몃옩 ?쒖옉 ???쒖떆???먮윭 硫붿떆吏瑜?李몄“?섏꽭??";
                                }
                                
                                SafeInvoke(() =>
                                {
                                    MessageBox.Show(
                                        $"YOLO 모델 로드에 실패했습니다:\n\n{errorMessage}",
                                        "YOLO 오류",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                                });
                                return;
                            }
                            
                            // ??痍⑥냼 ?좏겙 ?ы솗??
                            token.ThrowIfCancellationRequested();
                            
                            YoloTempFileHelper.CleanupStaleFiles();
                            tempImagePath = YoloTempFileHelper.CreateFramePath("yolo_detection_frame", targetFrame);
                            frame = new Mat();
                            
                            try
                            {
                                // ???꾨젅???쎄린 ??痍⑥냼 ?뺤씤
                                token.ThrowIfCancellationRequested();
                                
                                // ?꾩옱 ?꾨젅???쎄린
                                lock (yoloDetectionTaskLock)
                                {
                                    if (videoCapture == null || !videoCapture.IsOpened())
                                    {
                                        System.Diagnostics.Debug.WriteLine("[YOLO ?먯?] 鍮꾨뵒??罹≪쿂媛 ?ロ옒");
                                        return;
                                    }
                                    videoCapture.Set(VideoCaptureProperties.PosFrames, targetFrame);
                                    if (!videoCapture.Read(frame) || frame.Empty())
                                    {
                                System.Diagnostics.Debug.WriteLine($"[YOLO Detection] Cancelled detection for frame {targetFrame}");
                                        return;
                                    }
                                }
                                
                                // ???꾨젅?꾩씠 ?ъ쟾???좏슚?쒖? ?뺤씤
                                if (targetFrame != currentFrameIndex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] ?꾨젅???쎄린 ??蹂寃??뺤씤 ({targetFrame} -> {currentFrameIndex})");
                                    return;
                                }
                                
                                token.ThrowIfCancellationRequested();
                                
                                // Mat???꾩떆 ?뚯씪濡????
                                if (!Cv2.ImWrite(tempImagePath, frame))
                                {
                                System.Diagnostics.Debug.WriteLine($"[YOLO Detection] Cancelled detection for frame {targetFrame}");
                                    return;
                                }
                                
                                token.ThrowIfCancellationRequested();
                                
                                // ???꾨젅?꾩씠 ?ъ쟾???좏슚?쒖? ?ы솗??
                                if (targetFrame != currentFrameIndex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] ?먯? ???꾨젅??蹂寃??뺤씤 ({targetFrame} -> {currentFrameIndex})");
                                    return;
                                }
                                
                                // YOLO ?먯? ?섑뻾
                                var detections = predictor.Detect(tempImagePath);
                                
                                token.ThrowIfCancellationRequested();
                                
                                // ??理쒖쥌 ?꾨젅???뺤씤
                                if (targetFrame != currentFrameIndex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] ?먯? ???꾨젅??蹂寃??뺤씤 ({targetFrame} -> {currentFrameIndex})");
                                    return;
                                }
                                
                                // ?먯? 寃곌낵瑜?YoloDetectionBox 由ъ뒪?몃줈 蹂??
                                var detectionBoxes = new List<YoloDetectionBox>();
                                foreach (var d in detections)
                                {
                                    try
                                    {
                                        // YOLO ?쇱씠釉뚮윭由ш? "0: 'person'" ?뺤떇???대쫫??諛섑솚?섎?濡??뚯떛
                                        string rawDetectionName = d?.Name?.ToString() ?? "";
                                        string detectionName = rawDetectionName;
                                        
                                        if (!string.IsNullOrEmpty(rawDetectionName))
                                        {
                                            int firstQuote = rawDetectionName.IndexOf('\'');
                                            int lastQuote = rawDetectionName.LastIndexOf('\'');
                                            if (firstQuote != -1 && lastQuote > firstQuote)
                                            {
                                                detectionName = rawDetectionName.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                                            }
                                        }
                                        
                                        if (!string.IsNullOrEmpty(detectionName) && d != null)
                                        {
                                            detectionBoxes.Add(new YoloDetectionBox
                                            {
                                                Rectangle = new Rectangle(
                                                    (int)d.Bounds.X,
                                                    (int)d.Bounds.Y,
                                                    Math.Max(1, (int)d.Bounds.Width),
                                                    Math.Max(1, (int)d.Bounds.Height)
                                                ),
                                                Label = detectionName,
                                                Confidence = d.Confidence
                                            });
                                        }
                                    }
                                    catch (Exception detEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] ?먯? 媛앹껜 蹂???ㅻ쪟: {detEx.Message}");
                                    }
                                }
                                
                                System.Diagnostics.Debug.WriteLine($"[YOLO ?먯? ?꾨즺] ?꾨젅??{targetFrame}: {detectionBoxes.Count}媛?媛앹껜 ?먯?");
                                
                                // ??理쒖쥌 ?꾨젅???뺤씤 ??罹먯떆 ???
                                if (targetFrame == currentFrameIndex)
                                {
                                    lock (yoloDetectionCacheLock)
                                    {
                                        // ??踰????뺤씤 (?ㅻⅨ ?ㅻ젅?쒖뿉???대? 罹먯떆??異붽??덉쓣 ???덉쓬)
                                        if (!yoloDetectionCache.ContainsKey(targetFrame))
                                        {
                                            yoloDetectionCache[targetFrame] = detectionBoxes;
                                        }
                                    }
                                    
                                    // ??UI ?낅뜲?댄듃 (?꾨젅?꾩씠 ?ъ쟾???좏슚???뚮쭔, SafeInvoke ?ъ슜)
                                    SafeInvoke(() => pictureBoxVideo?.Invalidate());
                                }
                            }
                            finally
                                {
                                    // 由ъ냼???뺣━
                                    try
                                    {
                                        predictor?.Dispose();
                                        predictor = null;
                                        frame?.Dispose();
                                        
                                        if (!string.IsNullOrEmpty(tempImagePath) && File.Exists(tempImagePath))
                                        {
                                            try
                                            {
                                                YoloTempFileHelper.TryDelete(tempImagePath);
                                            }
                                            catch (Exception delEx)
                                            {
                                                System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] ?꾩떆 ?뚯씪 ??젣 ?ㅻ쪟: {delEx.Message}");
                                            }
                                        }
                                    }
                                    catch (Exception cleanupEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] 由ъ냼???뺣━ ?ㅻ쪟: {cleanupEx.Message}");
                                    }
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                // ??痍⑥냼???뺤긽?곸씤 ?곹솴?대?濡?濡쒓렇留??④?
                                System.Diagnostics.Debug.WriteLine($"[YOLO Detection] Cancelled detection for frame {targetFrame}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[YOLO ?먯? ?ㅻ쪟] 諛깃렇?쇱슫???묒뾽 ?꾩껜 ?ㅻ쪟: {ex.Message}\n{ex.StackTrace}");
                                // ??UI ?ㅻ젅?쒖뿉?쒕쭔 硫붿떆吏 諛뺤뒪 ?쒖떆 (痍⑥냼 ?ㅻ쪟 ?쒖쇅, SafeInvoke ?ъ슜)
                                if (!(ex is OperationCanceledException))
                                {
                                    SafeInvoke(() =>
                                    {
                                        MessageBox.Show(
                                            $"YOLO 감지 중 오류가 발생했습니다.\n{ex.Message}",
                                            "YOLO 오류",
                                            MessageBoxButtons.OK,
                                            MessageBoxIcon.Error);
                                    });
                                }
                            }
                            finally
                            {
                                // Predictor ?뺣━
                                try
                                {
                                    predictor?.Dispose();
                                    predictor = null;
                                }
                                catch (Exception predEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] Predictor ?뺣━ ?ㅻ쪟: {predEx.Message}");
                                }
                            }
                        }, token);
            }
            finally
            {
                // ??Semaphore ?댁젣 (??긽 ?댁젣)
                detectionSemaphore.Release();
            }
        }
        
        // ???덉쟾??Invoke ?ы띁 (Form??Dispose?섏? ?딆븯?붿? ?뺤씤)
        private void SafeInvoke(Action action)
        {
            try
            {
                if (isFormDisposed || this.IsDisposed || this.Disposing)
                    return;
                    
                if (this.InvokeRequired)
                {
                    this.Invoke((MethodInvoker)(() =>
                    {
                        try
                        {
                            if (!isFormDisposed && !this.IsDisposed && !this.Disposing)
                                action?.Invoke();
                        }
                        catch (ObjectDisposedException)
                        {
                            // Form??Dispose??寃쎌슦 臾댁떆
                        }
                        catch (InvalidOperationException)
                        {
                            // ?몃뱾???녿뒗 寃쎌슦 臾댁떆
                        }
                    }));
                }
                else
                {
                    if (!isFormDisposed && !this.IsDisposed && !this.Disposing)
                        action?.Invoke();
                }
            }
            catch (ObjectDisposedException)
            {
                // Form??Dispose??寃쎌슦 臾댁떆
            }
            catch (InvalidOperationException)
            {
                // ?몃뱾???녿뒗 寃쎌슦 臾댁떆
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SafeInvoke ?ㅻ쪟] {ex.Message}");
            }
        }

        // ??諛깃렇?쇱슫?쒖뿉??YOLO ?먯? ?섑뻾 (踰붿쐞 ?먯?) - ?꾩옱 ?ъ슜 ????
        private void StartYoloDetectionInBackground()
        {
            try
            {
                // 湲곗〈 ?먯? ?묒뾽???덉쑝硫?以묒?
                StopYoloDetection();
                
                if (string.IsNullOrEmpty(currentVideoFile) || !File.Exists(currentVideoFile))
                {
                    System.Diagnostics.Debug.WriteLine("[YOLO ?먯? ?쒖옉] 鍮꾨뵒???뚯씪???녾굅???좏슚?섏? ?딆쓬");
                    return;
                }
                
                yoloDetectionCancellationToken = new CancellationTokenSource();
                var token = yoloDetectionCancellationToken.Token;
                
                // ?꾩옱 ?꾨젅??湲곗? ?욌뮘 踰붿쐞 怨꾩궛
                int startFrame = Math.Max(0, currentFrameIndex - YOLO_DETECTION_RANGE);
                int endFrame = Math.Min(totalFrames - 1, currentFrameIndex + YOLO_DETECTION_RANGE);
                
                System.Diagnostics.Debug.WriteLine($"[YOLO ?먯? ?쒖옉] ?꾨젅??踰붿쐞: {startFrame} ~ {endFrame} (?꾩옱: {currentFrameIndex}, 珥??꾨젅?? {totalFrames})");
                
                yoloDetectionTask = Task.Run(async () =>
                {
                    YoloPredictor predictor = null;
                    VideoCapture videoCaptureCopy = null;
                    Mat frame = null;
                    string tempImagePath = null;
                    
                    try
                    {
                        // YOLO Predictor ?앹꽦 (異붿쟻 ?붿쭊怨?蹂꾨룄)
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] 紐⑤뜽 濡쒕뱶 ?쒖옉: {yoloModelPath}");
                            predictor = new YoloPredictor(yoloModelPath);
                            System.Diagnostics.Debug.WriteLine("[YOLO ?먯?] 紐⑤뜽 濡쒕뱶 ?꾨즺");
                        }
                        catch (Exception ex)
                        {
                            string errorDetails = $"[YOLO ?먯? ?ㅻ쪟] 紐⑤뜽 濡쒕뱶 ?ㅽ뙣: {ex.Message}\n{ex.StackTrace}";
                            if (ex.InnerException != null)
                            {
                                errorDetails += $"\n\n?대? ?덉쇅: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
                            }
                            System.Diagnostics.Debug.WriteLine(errorDetails);
                            
                            // CUDA 愿???먮윭?몄? ?뺤씤
                            string errorMessage = ex.Message;
                            if (ex.Message.Contains("CUDA") || ex.Message.Contains("cuda") || 
                                ex.Message.Contains("GPU") || ex.Message.Contains("gpu") ||
                                ex.InnerException != null && (ex.InnerException.Message.Contains("CUDA") || 
                                                               ex.InnerException.Message.Contains("cuda")))
                            {
                                errorMessage += "\n\n[CUDA 愿???먮윭]\n" +
                                              "NVIDIA ?쒕씪?대쾭, CUDA Toolkit, cuDNN 踰꾩쟾???뺤씤?섏꽭??\n" +
                                              "?먯꽭???닿껐 諛⑸쾿? ?꾨줈洹몃옩 ?쒖옉 ???쒖떆???먮윭 硫붿떆吏瑜?李몄“?섏꽭??";
                            }
                            
                            this.Invoke((MethodInvoker)(() =>
                            {
                                MessageBox.Show(
                                    $"YOLO 모델 로드에 실패했습니다:\n\n{errorMessage}",
                                    "YOLO 오류",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                            }));
                            return;
                        }
                        
                        YoloTempFileHelper.CleanupStaleFiles();
                        tempImagePath = YoloTempFileHelper.CreateFramePath("yolo_detection_frame");
                        frame = new Mat();
                        
                        try
                        {
                            videoCaptureCopy = new VideoCapture(currentVideoFile);
                            
                            if (!videoCaptureCopy.IsOpened())
                            {
                                System.Diagnostics.Debug.WriteLine("[YOLO ?먯? ?ㅻ쪟] 鍮꾨뵒???뚯씪 ?닿린 ?ㅽ뙣");
                                predictor?.Dispose();
                                return;
                            }
                            
                            System.Diagnostics.Debug.WriteLine("[YOLO ?먯?] 鍮꾨뵒???뚯씪 ?닿린 ?꾨즺");
                            
                            int processedFrames = 0;
                            int detectedObjectsTotal = 0;
                            
                            // 踰붿쐞 ???꾨젅?꾨뱾???쒖감?곸쑝濡??먯?
                            for (int frameIdx = startFrame; frameIdx <= endFrame; frameIdx++)
                            {
                                if (token.IsCancellationRequested)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] 痍⑥냼??- ?꾨젅??{frameIdx}?먯꽌 以묐떒");
                                    break;
                                }
                                
                                try
                                {
                                    videoCaptureCopy.Set(VideoCaptureProperties.PosFrames, frameIdx);
                                    if (!videoCaptureCopy.Read(frame) || frame.Empty())
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] ?꾨젅??{frameIdx} ?쎄린 ?ㅽ뙣");
                                        continue;
                                    }
                                    
                                    try
                                    {
                                        // Mat???꾩떆 ?뚯씪濡????
                                        if (!Cv2.ImWrite(tempImagePath, frame))
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] ?꾨젅??{frameIdx} ?꾩떆 ?뚯씪 ????ㅽ뙣");
                                            continue;
                                        }
                                        
                                        // YOLO ?먯? ?섑뻾
                                        var detections = predictor.Detect(tempImagePath);
                                        
                                        // ?먯? 寃곌낵瑜?YoloDetectionBox 由ъ뒪?몃줈 蹂??
                                        var detectionBoxes = new List<YoloDetectionBox>();
                                        foreach (var d in detections)
                                        {
                                            try
                                            {
                                                // YOLO ?쇱씠釉뚮윭由ш? "0: 'person'" ?뺤떇???대쫫??諛섑솚?섎?濡??뚯떛
                                                string rawDetectionName = d?.Name?.ToString() ?? "";
                                                string detectionName = rawDetectionName;
                                                
                                                if (!string.IsNullOrEmpty(rawDetectionName))
                                                {
                                                    int firstQuote = rawDetectionName.IndexOf('\'');
                                                    int lastQuote = rawDetectionName.LastIndexOf('\'');
                                                    if (firstQuote != -1 && lastQuote > firstQuote)
                                                    {
                                                        detectionName = rawDetectionName.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                                                    }
                                                }
                                                
                                                if (!string.IsNullOrEmpty(detectionName) && d != null)
                                                {
                                                    detectionBoxes.Add(new YoloDetectionBox
                                                    {
                                                        Rectangle = new Rectangle(
                                                            (int)d.Bounds.X,
                                                            (int)d.Bounds.Y,
                                                            Math.Max(1, (int)d.Bounds.Width),
                                                            Math.Max(1, (int)d.Bounds.Height)
                                                        ),
                                                        Label = detectionName,
                                                        Confidence = d.Confidence
                                                    });
                                                }
                                            }
                                            catch (Exception detEx)
                                            {
                                                System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] ?먯? 媛앹껜 蹂???ㅻ쪟 (?꾨젅??{frameIdx}): {detEx.Message}");
                                            }
                                        }
                                        
                                        detectedObjectsTotal += detectionBoxes.Count;
                                        
                                        // 罹먯떆?????
                                        lock (yoloDetectionCacheLock)
                                        {
                                            yoloDetectionCache[frameIdx] = detectionBoxes;
                                        }
                                        
                                        processedFrames++;
                                        
                                        // 10?꾨젅?꾨쭏??吏꾪뻾 ?곹솴 濡쒓렇
                                        if (processedFrames % 10 == 0)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[YOLO ?먯? 吏꾪뻾] {processedFrames}/{endFrame - startFrame + 1} ?꾨젅??泥섎━??(?먯? 媛앹껜: {detectedObjectsTotal}媛?");
                                        }
                                        
                                        // ?꾩옱 ?꾨젅?꾩씠硫?UI ?낅뜲?댄듃
                                        if (frameIdx == currentFrameIndex)
                                        {
                                            this.Invoke((MethodInvoker)(() =>
                                            {
                                                try
                                                {
                                                    pictureBoxVideo?.Invalidate();
                                                }
                                                catch (Exception invEx)
                                                {
                                                    System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] UI ?낅뜲?댄듃 ?ㅻ쪟: {invEx.Message}");
                                                }
                                            }));
                                        }
                                    }
                                    catch (Exception frameEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[YOLO ?먯? ?ㅻ쪟] ?꾨젅??{frameIdx} 泥섎━ 以??ㅻ쪟: {frameEx.Message}\n{frameEx.StackTrace}");
                                        // 媛쒕퀎 ?꾨젅???ㅻ쪟??怨꾩냽 吏꾪뻾
                                    }
                                    
                                    // 諛깃렇?쇱슫???묒뾽?대?濡?CPU 遺?섎? 以꾩씠湲??꾪빐 ?쎄컙??吏??
                                    await Task.Delay(10, token);
                                }
                                catch (Exception readEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YOLO ?먯? ?ㅻ쪟] ?꾨젅??{frameIdx} ?쎄린 ?ㅻ쪟: {readEx.Message}");
                                    continue;
                                }
                            }
                            
                            System.Diagnostics.Debug.WriteLine($"[YOLO ?먯? ?꾨즺] 珥?{processedFrames} ?꾨젅??泥섎━, {detectedObjectsTotal}媛?媛앹껜 ?먯?");
                        }
                        finally
                        {
                            // 由ъ냼???뺣━
                            try
                            {
                                videoCaptureCopy?.Release();
                                videoCaptureCopy?.Dispose();
                                predictor?.Dispose();
                                predictor = null;
                                frame?.Dispose();
                                
                                if (!string.IsNullOrEmpty(tempImagePath) && File.Exists(tempImagePath))
                                {
                                    try
                                    {
                                        YoloTempFileHelper.TryDelete(tempImagePath);
                                    }
                                    catch (Exception delEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] ?꾩떆 ?뚯씪 ??젣 ?ㅻ쪟: {delEx.Message}");
                                    }
                                }
                            }
                            catch (Exception cleanupEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] 由ъ냼???뺣━ ?ㅻ쪟: {cleanupEx.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO ?먯? ?ㅻ쪟] 諛깃렇?쇱슫???묒뾽 ?꾩껜 ?ㅻ쪟: {ex.Message}\n{ex.StackTrace}");
                        this.Invoke((MethodInvoker)(() =>
                        {
                            try
                            {
                                MessageBox.Show(
                                    $"YOLO 감지 중 오류가 발생했습니다.\n{ex.Message}",
                                    "YOLO 오류",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                            }
                        catch (Exception msgEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] 硫붿떆吏 諛뺤뒪 ?쒖떆 ?ㅻ쪟: {msgEx.Message}");
                        }
                        }));
                    }
                    finally
                    {
                        // Predictor ?뺣━
                        try
                        {
                            predictor?.Dispose();
                        }
                        catch (Exception predEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[YOLO ?먯?] Predictor ?뺣━ ?ㅻ쪟: {predEx.Message}");
                        }
                    }
                }, token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YOLO ?먯? ?쒖옉 ?ㅻ쪟] {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"YOLO 감지 시작 중 오류가 발생했습니다.\n{ex.Message}",
                    "YOLO 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ??YOLO ?먯? 以묒?
        private void StopYoloDetection()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[YOLO ?먯? 以묒?] ?먯? ?묒뾽 以묒? ?쒖옉");
                
                // ?붾컮?댁뒪 ??대㉧ 痍⑥냼
                detectionDebounceTimer?.Dispose();
                detectionDebounceTimer = null;
                
                if (yoloDetectionCancellationToken != null)
                {
                    try
                    {
                        yoloDetectionCancellationToken.Cancel();
                        System.Diagnostics.Debug.WriteLine("[YOLO ?먯? 以묒?] 痍⑥냼 ?좏겙 ?좏샇 ?꾩넚");
                    }
                    catch (Exception cancelEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO ?먯? 以묒?] 痍⑥냼 ?좏겙 ?ㅻ쪟: {cancelEx.Message}");
                    }
                    finally
                    {
                        try
                        {
                            yoloDetectionCancellationToken.Dispose();
                        }
                        catch (Exception dispEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[YOLO ?먯? 以묒?] 痍⑥냼 ?좏겙 ?뺣━ ?ㅻ쪟: {dispEx.Message}");
                        }
                        yoloDetectionCancellationToken = null;
                    }
                }
                
                if (yoloDetectionTask != null)
                {
                    try
                    {
                        if (!yoloDetectionTask.IsCompleted)
                        {
                            System.Diagnostics.Debug.WriteLine("[YOLO ?먯? 以묒?] ?묒뾽 ?꾨즺 ?湲?以?(理쒕? 1珥?");
                            bool completed = yoloDetectionTask.Wait(1000); // 1珥??湲?
                            if (!completed)
                            {
                                System.Diagnostics.Debug.WriteLine("[YOLO ?먯? 以묒?] ?묒뾽??1珥??댁뿉 ?꾨즺?섏? ?딆쓬 - 媛뺤젣 醫낅즺");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[YOLO Detection] Detection task completed successfully.");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[YOLO Detection] Detection task was already completed before wait.");
                        }
                    }
                    catch (Exception waitEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO ?먯? 以묒?] ?묒뾽 ?湲??ㅻ쪟: {waitEx.Message}");
                    }
                    finally
                    {
                        yoloDetectionTask = null;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("[YOLO ?먯? 以묒?] ?꾨즺");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YOLO ?먯? 以묒? ?ㅻ쪟] {ex.Message}\n{ex.StackTrace}");
            }
        }

        #endregion
    }
}

