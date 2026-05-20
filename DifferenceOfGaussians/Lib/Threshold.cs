using System.Drawing;
using System.Drawing.Imaging;

namespace DifferenceOfGaussians.Lib
{
    public class Threshold
    {
        private int thresholdValue;
        private double phi;

        /// <summary>
        /// Creates a threshold filter with hyperbolic tangent function.
        /// </summary>
        /// <param name="thresholdValue">Threshold value (e). Range: 0-255</param>
        public Threshold(int thresholdValue)
        {
            if (thresholdValue < 0 || thresholdValue > 255)
                throw new ArgumentOutOfRangeException(nameof(thresholdValue), "Threshold value must be between 0 and 255");

            this.thresholdValue = thresholdValue;
            this.phi = 0;
        }

        /// <summary>
        /// Creates a threshold filter with hyperbolic tangent function.
        /// </summary>
        /// <param name="thresholdValue">Threshold value (e). Range: 0-255</param>
        /// <param name="phi">Parameter for the tanh function smoothness</param>
        public Threshold(int thresholdValue, double phi)
        {
            if (thresholdValue < 0 || thresholdValue > 255)
                throw new ArgumentOutOfRangeException(nameof(thresholdValue), "Threshold value must be between 0 and 255");

            this.thresholdValue = thresholdValue;
            this.phi = phi;
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
                    byte thresholded = ApplyTanhThreshold(grayscaleValue);

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
                    byte thresholded = ApplyTanhThreshold(grayscaleValue);

                    pixelData[i] = thresholded;         // B
                    pixelData[i + 1] = thresholded;     // G
                    pixelData[i + 2] = thresholded;     // R
                }
            }
        }

        private byte ApplyTanhThreshold(byte pixelValue)
        {
            if (pixelValue >= thresholdValue)
            {
                return 255;
            }

            double u = pixelValue;
            double e = thresholdValue;
            double result = 1 + Math.Tanh(phi + (u - e));

            // Scale result to 0-255 range
            // tanh output is in range [-1, 1], so 1 + tanh is in range [0, 2]
            // We need to scale this to [0, 255]
            byte output = (byte)Math.Clamp(result * 127.5, 0, 255);
            return output;
        }
    }
}
