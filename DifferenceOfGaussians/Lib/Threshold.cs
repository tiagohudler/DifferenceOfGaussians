using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace DifferenceOfGaussians.Lib
{
    public class Threshold
    {
        private readonly double thresholdValue;
        private readonly double phi;

        /// <summary>
        /// Creates an XDoG threshold filter.
        /// thresholdValue (epsilon) expected in range [0,1].
        /// phi controls edge sharpness.
        /// </summary>
        public Threshold(double thresholdValue, double phi = 10.0)
        {
            if (thresholdValue < 0.0 || thresholdValue > 1.0)
                throw new ArgumentOutOfRangeException(
                    nameof(thresholdValue),
                    "Threshold value must be between 0 and 1.");

            if (phi <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(phi),
                    "Phi must be greater than zero.");

            this.thresholdValue = thresholdValue;
            this.phi = phi;
        }

        public Stream Apply(FileInfo image)
        {
            using Bitmap bitmap = new Bitmap(image.FullName);

            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);

            BitmapData data = bitmap.LockBits(
                rect,
                ImageLockMode.ReadWrite,
                bitmap.PixelFormat);

            int stride = data.Stride;
            int bytesPerPixel =
                bitmap.PixelFormat == PixelFormat.Format32bppArgb ? 4 : 3;

            int bytesQtt = Math.Abs(stride) * bitmap.Height;

            byte[] pixelData = new byte[bytesQtt];

            System.Runtime.InteropServices.Marshal.Copy(
                data.Scan0,
                pixelData,
                0,
                bytesQtt);

            ApplyThreshold(
                pixelData,
                bitmap.Width,
                bitmap.Height,
                stride,
                bytesPerPixel);

            System.Runtime.InteropServices.Marshal.Copy(
                pixelData,
                0,
                data.Scan0,
                bytesQtt);

            bitmap.UnlockBits(data);

            MemoryStream output = new MemoryStream();

            bitmap.Save(output, ImageFormat.Png);

            output.Position = 0;

            return output;
        }

        private void ApplyThreshold(
            byte[] pixelData,
            int width,
            int height,
            int stride,
            int bytesPerPixel)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * stride + x * bytesPerPixel;

                    byte blue = pixelData[index];
                    byte green = pixelData[index + 1];
                    byte red = pixelData[index + 2];

                    // Correct System.Drawing order: BGR
                    double grayscale =
                          0.114 * blue
                        + 0.587 * green
                        + 0.299 * red;

                    // Normalize to [0,1]
                    double u = grayscale / 255.0;

                    byte thresholded =
                        ApplyXDoGThreshold(u);

                    pixelData[index] = thresholded;
                    pixelData[index + 1] = thresholded;
                    pixelData[index + 2] = thresholded;

                    // Preserve alpha channel automatically
                }
            }
        }

        private byte ApplyXDoGThreshold(double u)
        {
            double result;

            // Standard XDoG thresholding
            if (u >= thresholdValue)
            {
                result = 1.0;
            }
            else
            {
                result =
                    1.0 +
                    Math.Tanh(
                        phi * (u - thresholdValue));
            }

            result = Math.Clamp(result, 0.0, 1.0);

            return (byte)(result * 255.0);
        }
    }
}