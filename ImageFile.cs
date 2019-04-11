using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;

namespace FmPostToBlogger
{
    static class ImageFile
    {
        public static void ResizeImage(Stream src, Stream dst, int width, int height)
        {
            // Load the image to change
            using (var image = Image.FromStream(src))
            {
                using (var resizedImage = new Bitmap(width, height))
                {
                    resizedImage.SetResolution(72, 72);

                    foreach(var prop in image.PropertyItems)
                    {
                        resizedImage.SetPropertyItem(prop);
                    }

                    using (var graphic = Graphics.FromImage(resizedImage))
                    {
                        graphic.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphic.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        graphic.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        graphic.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        graphic.DrawImage(image, 0, 0, width, height);
                    }

                    resizedImage.Save(dst, image.RawFormat);
                }
            }
        }
    }
}
