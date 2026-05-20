using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace DifferenceOfGaussians.Lib
{
    public class DifferenceOfGaussians
    {
        private GaussianBlur blur1;
        private GaussianBlur blur2;
        private double t;

        /// <summary>
        /// Creates a Difference of Gaussians filter.
        /// </summary>
        /// <param name="standardDeviation1">Standard deviation for the first (larger) Gaussian blur</param>
        /// <param name="standardDeviation2">Standard deviation for the second (smaller) Gaussian blur</param>
        /// <param name="t">Weight parameter for extended DoG. First blur is multiplied by (1+t), second by t</param>
        public DifferenceOfGaussians(double standardDeviation1, double standardDeviation2, double t)
        {
            int radius1 = Math.Max(1, (int)Math.Ceiling(3 * standardDeviation1));
            int radius2 = Math.Max(1, (int)Math.Ceiling(3 * standardDeviation2));

            blur1 = new GaussianBlur(standardDeviation1, radius1);
            blur2 = new GaussianBlur(standardDeviation2, radius2);
            this.t = t;
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
            double weight1 = 1 + t;
            double weight2 = t;

            for (int i = 0; i < length; i++)
            {
                // Skip alpha channel for 32-bit images
                if (bytesPerPixel == 4 && i % 4 == 3)
                {
                    result[i] = blurred1[i];
                    continue;
                }

                // Extended DoG: (1+t)*blur1 - t*blur2
                double weightedBlur1 = blurred1[i] * weight1;
                double weightedBlur2 = blurred2[i] * weight2;
                double diff = weightedBlur1 - weightedBlur2;
                int value = (int)Math.Clamp(diff, 0, 255);
                result[i] = (byte)value;
            }

            return result;
        }
    }
}
