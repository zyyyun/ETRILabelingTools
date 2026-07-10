using System;
using System.Collections.Generic;
using System.Drawing;

namespace WinFormsApp1
{
    public partial class Form1
    {
        #region Coordinate Transformation
        private enum SkeletonCoordSpace
        {
            Pixel,
            NormalizedZeroToOne,
            NormalizedMinusOneToOne
        }

        private SkeletonCoordSpace DetectSkeletonCoordSpace(List<List<double>> joints)
        {
            if (joints == null || joints.Count == 0)
                return SkeletonCoordSpace.Pixel;

            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            double maxAbs = 0;
            bool hasValid = false;

            foreach (var joint in joints)
            {
                if (joint == null || joint.Count < 2)
                    continue;

                double x = joint[0];
                double y = joint[1];

                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
                maxAbs = Math.Max(maxAbs, Math.Max(Math.Abs(x), Math.Abs(y)));
                hasValid = true;
            }

            if (!hasValid)
                return SkeletonCoordSpace.Pixel;

            const double normalizedMargin = 1.2; // 약간의 오차 허용
            if (minX >= 0 && maxX <= normalizedMargin && minY >= 0 && maxY <= normalizedMargin)
                return SkeletonCoordSpace.NormalizedZeroToOne;

            if (minX >= -normalizedMargin && maxX <= normalizedMargin && minY >= -normalizedMargin && maxY <= normalizedMargin)
                return SkeletonCoordSpace.NormalizedMinusOneToOne;

            // -1~1을 넘더라도 작은 범위(예: -2~2, -3~3 등)는 정규화/로컬 좌표로 간주
            if (maxAbs <= 5.0)
                return SkeletonCoordSpace.NormalizedMinusOneToOne;

            return SkeletonCoordSpace.Pixel;
        }

        private double InvertSkeletonYValue(double y, float imageHeight, Rectangle? bbox)
        {
            if (bbox.HasValue && bbox.Value.Width > 0 && bbox.Value.Height > 0)
            {
                return bbox.Value.Y + bbox.Value.Height - (y - bbox.Value.Y);
            }

            return imageHeight - y;
        }

        private bool TryConvertSkeletonJointToImagePoint(
            List<double> joint,
            SkeletonCoordSpace coordSpace,
            float imageWidth,
            float imageHeight,
            Rectangle? bbox,
            out PointF imagePoint)
        {
            imagePoint = default;
            if (joint == null || joint.Count < 2)
                return false;

            double x = joint[0];
            double y = joint[1];

            switch (coordSpace)
            {
                case SkeletonCoordSpace.NormalizedMinusOneToOne:
                    x = (x + 1.0) * 0.5 * imageWidth;
                    y = (y + 1.0) * 0.5 * imageHeight;
                    break;
                case SkeletonCoordSpace.NormalizedZeroToOne:
                    x *= imageWidth;
                    y *= imageHeight;
                    break;
                default:
                    break;
            }

            if (invertSkeletonY)
            {
                y = InvertSkeletonYValue(y, imageHeight, bbox);
            }

            const float margin = 5f;
            if (x < -margin || y < -margin || x > imageWidth + margin || y > imageHeight + margin)
                return false;

            imagePoint = new PointF((float)x, (float)y);
            return true;
        }

        private List<PointF?> ConvertSkeletonJointsToImagePoints(
            List<List<double>> joints,
            Rectangle bbox,
            float imageWidth,
            float imageHeight)
        {
            var points = new List<PointF?>(joints.Count);
            var coordSpace = DetectSkeletonCoordSpace(joints);

            if (coordSpace == SkeletonCoordSpace.Pixel)
            {
                foreach (var joint in joints)
                {
                    if (TryConvertSkeletonJointToImagePoint(joint, coordSpace, imageWidth, imageHeight, bbox, out var imagePoint))
                    {
                        points.Add(imagePoint);
                    }
                    else
                    {
                        points.Add(null);
                    }
                }

                return points;
            }

            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            bool hasValid = false;

            foreach (var joint in joints)
            {
                if (joint == null || joint.Count < 2)
                    continue;

                double x = joint[0];
                double y = joint[1];
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
                hasValid = true;
            }

            if (!hasValid)
            {
                for (int i = 0; i < joints.Count; i++)
                    points.Add(null);
                return points;
            }

            double rangeX = Math.Max(maxX - minX, 1e-6);
            double rangeY = Math.Max(maxY - minY, 1e-6);

            float centerX;
            float centerY;
            double scale;

            if (bbox.Width > 0 && bbox.Height > 0)
            {
                centerX = bbox.X + bbox.Width / 2f;
                centerY = bbox.Y + bbox.Height / 2f;
                double scaleX = bbox.Width / rangeX;
                double scaleY = bbox.Height / rangeY;
                scale = Math.Min(scaleX, scaleY) * 0.8; // 약간 여유 공간
            }
            else
            {
                centerX = imageWidth / 2f;
                centerY = imageHeight / 2f;
                double scaleX = imageWidth / rangeX;
                double scaleY = imageHeight / rangeY;
                scale = Math.Min(scaleX, scaleY) * 0.9;
            }

            double centerJointX = (minX + maxX) / 2.0;
            double centerJointY = (minY + maxY) / 2.0;

            const float margin = 5f;
            foreach (var joint in joints)
            {
                if (joint == null || joint.Count < 2)
                {
                    points.Add(null);
                    continue;
                }

                double x = (joint[0] - centerJointX) * scale + centerX;
                double y = (joint[1] - centerJointY) * scale + centerY;

                if (invertSkeletonY)
                {
                y = InvertSkeletonYValue(y, imageHeight, bbox);
                }

                if (x < -margin || y < -margin || x > imageWidth + margin || y > imageHeight + margin)
                {
                    points.Add(null);
                    continue;
                }

                points.Add(new PointF((float)x, (float)y));
            }

            return points;
        }

        // PictureBox의 Zoom 모드에서 실제 이미지가 표시되는 영역 계산
        private RectangleF GetImageDisplayRectangle()
        {
            if (pictureBoxVideo.Image == null)
                return RectangleF.Empty;

            float imageAspect = (float)pictureBoxVideo.Image.Width / pictureBoxVideo.Image.Height;
            float controlAspect = (float)pictureBoxVideo.Width / pictureBoxVideo.Height;

            float renderWidth, renderHeight;
            float renderX = 0, renderY = 0;

            if (imageAspect > controlAspect)
            {
                // 이미지가 더 넓음 - 좌우에 맞춤
                renderWidth = pictureBoxVideo.Width;
                renderHeight = pictureBoxVideo.Width / imageAspect;
                renderY = (pictureBoxVideo.Height - renderHeight) / 2f;
            }
            else
            {
                // 이미지가 더 높음 - 상하에 맞춤
                renderHeight = pictureBoxVideo.Height;
                renderWidth = pictureBoxVideo.Height * imageAspect;
                renderX = (pictureBoxVideo.Width - renderWidth) / 2f;
            }

            return new RectangleF(renderX, renderY, renderWidth, renderHeight);
        }

        // 이미지 좌표 → 뷰(PictureBox) 좌표 변환
        private PointF ImageToView(PointF imagePoint)
        {
            if (pictureBoxVideo.Image == null)
                return imagePoint;

            var displayRect = GetImageDisplayRectangle();
            
            float scaleX = displayRect.Width / pictureBoxVideo.Image.Width;
            float scaleY = displayRect.Height / pictureBoxVideo.Image.Height;

            return new PointF(
                displayRect.X + imagePoint.X * scaleX,
                displayRect.Y + imagePoint.Y * scaleY
            );
        }

        private RectangleF ImageToView(RectangleF imageRect)
        {
            var topLeft = ImageToView(new PointF(imageRect.X, imageRect.Y));
            var bottomRight = ImageToView(new PointF(imageRect.Right, imageRect.Bottom));
            
            return new RectangleF(
                topLeft.X,
                topLeft.Y,
                bottomRight.X - topLeft.X,
                bottomRight.Y - topLeft.Y
            );
        }

        // 뷰(PictureBox) 좌표 → 이미지 좌표 변환
        private PointF ViewToImage(PointF viewPoint)
        {
            if (pictureBoxVideo.Image == null)
                return viewPoint;

            var displayRect = GetImageDisplayRectangle();
            
            float scaleX = pictureBoxVideo.Image.Width / displayRect.Width;
            float scaleY = pictureBoxVideo.Image.Height / displayRect.Height;

            return new PointF(
                (viewPoint.X - displayRect.X) * scaleX,
                (viewPoint.Y - displayRect.Y) * scaleY
            );
        }

        private RectangleF ViewToImage(RectangleF viewRect)
        {
            var topLeft = ViewToImage(new PointF(viewRect.X, viewRect.Y));
            var bottomRight = ViewToImage(new PointF(viewRect.Right, viewRect.Bottom));
            
            return new RectangleF(
                topLeft.X,
                topLeft.Y,
                bottomRight.X - topLeft.X,
                bottomRight.Y - topLeft.Y
            );
        }
        #endregion

    }
}
