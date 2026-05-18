using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace DifferenceOfGaussians.Lib
{
    public class DifferenceOfGaussians
    {
        private GaussianBlur blur1;
        private GaussianBlur blur2;

        /// <summary>
        /// Creates a Difference of Gaussians filter.
        /// </summary>
        /// <param name="standardDeviation1">Standard deviation for the first (larger) Gaussian blur</param>
        /// <param name="standardDeviation2">Standard deviation for the second (smaller) Gaussian blur</param>
        /// <param name="kernelRadius">Radius of the Gaussian kernel</param>
        public DifferenceOfGaussians(double standardDeviation1, double standardDeviation2, int kernelRadius)
        {
            blur1 = new GaussianBlur(standardDeviation1, kernelRadius);
            blur2 = new GaussianBlur(standardDeviation2, kernelRadius);
        }

        public Stream Apply(FileInfo image)
        {
            Bitmap bitmap = new Bitmap(image.FullName);

            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, bitmap.PixelFormat);

            IntPtr pointer = data.Scan0;
            int bytesQtt = Math.Abs(data.Stride) * bitmap.Height;
            byte[] originalPixels = new byte[bytesQtt];

            System.Runtime.InteropServices.Marshal.Copy(pointer, originalPixels, 0, bytesQtt);

            bitmap.UnlockBits(data);

            int bytesPerPixel = bitmap.PixelFormat == PixelFormat.Format32bppArgb ? 4 : 3;
            int stride = data.Stride;

            byte[] blurred1 = blur1.BlurPixelData(originalPixels, bitmap.Width, bitmap.Height, stride, bytesPerPixel);

            byte[] blurred2 = blur2.BlurPixelData(originalPixels, bitmap.Width, bitmap.Height, stride, bytesPerPixel);

            byte[] dogResult = ComputeDifference(blurred1, blurred2, bytesQtt, bytesPerPixel);

            // Write back to bitmap
            data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, bitmap.PixelFormat);
            pointer = data.Scan0;
            System.Runtime.InteropServices.Marshal.Copy(dogResult, 0, pointer, bytesQtt);
            bitmap.UnlockBits(data);

            MemoryStream output = new MemoryStream();
            bitmap.Save(output, ImageFormat.Png);
            output.Position = 0;
            bitmap.Dispose();

            return output;
        }

        private byte[] ComputeDifference(byte[] blurred1, byte[] blurred2, int length, int bytesPerPixel)
        {
            byte[] result = new byte[length];

            for (int i = 0; i < length; i++)
            {
                // Skip alpha channel for 32-bit images
                if (bytesPerPixel == 4 && i % 4 == 3)
                {
                    result[i] = blurred1[i];
                    continue;
                }

                // Compute absolute difference and center around 128
                int diff = (int)blurred1[i] - (int)blurred2[i];
                int value = 128 + (diff / 2);
                result[i] = (byte)Math.Clamp(value, 0, 255);
            }

            return result;
        }
    }
}
