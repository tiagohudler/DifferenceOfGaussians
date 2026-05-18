using System.Drawing;
using System.Drawing.Imaging;

namespace DifferenceOfGaussians.Lib
{
    public class Threshold
    {
        private int thresholdValue;

        /// <summary>
        /// Creates a threshold filter.
        /// </summary>
        /// <param name="thresholdValue">Pixel values above this threshold become white (255), below become black (0). Range: 0-255</param>
        public Threshold(int thresholdValue)
        {
            if (thresholdValue < 0 || thresholdValue > 255)
                throw new ArgumentOutOfRangeException(nameof(thresholdValue), "Threshold value must be between 0 and 255");

            this.thresholdValue = thresholdValue;
        }

        public Stream Apply(FileInfo image)
        {
            Bitmap bitmap = new Bitmap(image.FullName);

            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, bitmap.PixelFormat);

            IntPtr pointer = data.Scan0;
            int bytesQtt = Math.Abs(data.Stride) * bitmap.Height;
            byte[] pixelData = new byte[bytesQtt];

            System.Runtime.InteropServices.Marshal.Copy(pointer, pixelData, 0, bytesQtt);

            bitmap.UnlockBits(data);

            int bytesPerPixel = bitmap.PixelFormat == PixelFormat.Format32bppArgb ? 4 : 3;

            ApplyThreshold(pixelData, bytesQtt, bytesPerPixel);

            data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, bitmap.PixelFormat);
            pointer = data.Scan0;
            System.Runtime.InteropServices.Marshal.Copy(pixelData, 0, pointer, bytesQtt);
            bitmap.UnlockBits(data);

            MemoryStream output = new MemoryStream();
            bitmap.Save(output, ImageFormat.Png);
            output.Position = 0;
            bitmap.Dispose();

            return output;
        }

        private void ApplyThreshold(byte[] pixelData, int length, int bytesPerPixel)
        {
            for (int i = 0; i < length; i += bytesPerPixel)
            {
                byte grayscaleValue;

                if (bytesPerPixel == 4)
                {
                    // ARGB format: skip alpha, process RGB
                    // Use weighted grayscale conversion: 0.299*R + 0.587*G + 0.114*B
                    grayscaleValue = (byte)((pixelData[i] * 0.299 + pixelData[i + 1] * 0.587 + pixelData[i + 2] * 0.114) / 255 * 255);
                    byte thresholded = grayscaleValue > thresholdValue ? (byte)255 : (byte)0;

                    pixelData[i] = thresholded;         // B
                    pixelData[i + 1] = thresholded;     // G
                    pixelData[i + 2] = thresholded;     // R
                    // pixelData[i + 3] stays as alpha
                }
                else
                {
                    // RGB format (3 bytes per pixel)
                    // Use weighted grayscale conversion: 0.299*R + 0.587*G + 0.114*B
                    grayscaleValue = (byte)((pixelData[i] * 0.299 + pixelData[i + 1] * 0.587 + pixelData[i + 2] * 0.114) / 255 * 255);
                    byte thresholded = grayscaleValue > thresholdValue ? (byte)255 : (byte)0;

                    pixelData[i] = thresholded;         // B
                    pixelData[i + 1] = thresholded;     // G
                    pixelData[i + 2] = thresholded;     // R
                }
            }
        }
    }
}
