using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DifferenceOfGaussians.Lib;

namespace DifferenceOfGaussians.Lib
{
    public class GaussianBlur
    {
        private double standardDeviation;
        private int kernelRadius;

        public GaussianBlur(double standardDeviation, int kernelRadius)
        {
            this.standardDeviation = standardDeviation;
            this.kernelRadius = kernelRadius;
        }

        public Stream Blur(FileInfo image)
        {
            Bitmap bitmap = new Bitmap(image.FullName);

            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, bitmap.PixelFormat);

            IntPtr pointer = data.Scan0;

            int bytesQtt = Math.Abs(data.Stride) * bitmap.Height;
            byte[] rgbValues = new byte[bytesQtt];

            System.Runtime.InteropServices.Marshal.Copy(pointer, rgbValues, 0, bytesQtt);

            int bytesPerPixel = bitmap.PixelFormat == PixelFormat.Format32bppArgb ? 4 : 3;
            int stride = data.Stride;

            // First pass: horizontal blur (x-axis)
            byte[] horizontalPass = new byte[bytesQtt];
            Array.Copy(rgbValues, horizontalPass, bytesQtt);
            ApplyHorizontalGaussianPass(horizontalPass, bitmap.Width, bitmap.Height, stride, bytesPerPixel, kernelRadius);

            // Second pass: vertical blur (y-axis)
            byte[] verticalPass = new byte[bytesQtt];
            Array.Copy(horizontalPass, verticalPass, bytesQtt);
            ApplyVerticalGaussianPass(verticalPass, bitmap.Width, bitmap.Height, stride, bytesPerPixel, kernelRadius);

            System.Runtime.InteropServices.Marshal.Copy(verticalPass, 0, pointer, bytesQtt);
            bitmap.UnlockBits(data);

            MemoryStream output = new MemoryStream();
            bitmap.Save(output, ImageFormat.Png);
            output.Position = 0;

            return output;
        }

        private void ApplyHorizontalGaussianPass(byte[] pixelData, int width, int height, int stride, int bytesPerPixel, int kernelRadius)
        {
            byte[] temp = new byte[pixelData.Length];
            Array.Copy(pixelData, temp, pixelData.Length);

            double[] kernel = GenerateGaussianKernel(kernelRadius);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int c = 0; c < bytesPerPixel; c++)
                    {
                        double sum = 0.0;
                        double weightSum = 0.0;

                        for (int kx = -kernelRadius; kx <= kernelRadius; kx++)
                        {
                            // Edge clamping: clamp x coordinate to valid range
                            int sampleX = Math.Clamp(x + kx, 0, width - 1);
                            int sourceIndex = y * stride + sampleX * bytesPerPixel + c;
                            double weight = kernel[kx + kernelRadius];

                            sum += temp[sourceIndex] * weight;
                            weightSum += weight;
                        }

                        int destIndex = y * stride + x * bytesPerPixel + c;
                        pixelData[destIndex] = (byte)Math.Clamp(sum / weightSum, 0, 255);
                    }
                }
            }
        }

        private void ApplyVerticalGaussianPass(byte[] pixelData, int width, int height, int stride, int bytesPerPixel, int kernelRadius)
        {
            byte[] temp = new byte[pixelData.Length];
            Array.Copy(pixelData, temp, pixelData.Length);

            double[] kernel = GenerateGaussianKernel(kernelRadius);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int c = 0; c < bytesPerPixel; c++)
                    {
                        double sum = 0.0;
                        double weightSum = 0.0;

                        for (int ky = -kernelRadius; ky <= kernelRadius; ky++)
                        {
                            // Edge clamping: clamp y coordinate to valid range
                            int sampleY = Math.Clamp(y + ky, 0, height - 1);
                            int sourceIndex = sampleY * stride + x * bytesPerPixel + c;
                            double weight = kernel[ky + kernelRadius];

                            sum += temp[sourceIndex] * weight;
                            weightSum += weight;
                        }

                        int destIndex = y * stride + x * bytesPerPixel + c;
                        pixelData[destIndex] = (byte)Math.Clamp(sum / weightSum, 0, 255);
                    }
                }
            }
        }

        private double[] GenerateGaussianKernel(int radius)
        {
            int kernelSize = 2 * radius + 1;
            double[] kernel = new double[kernelSize];

            double sum = 0.0;
            for (int i = 0; i < kernelSize; i++)
            {
                int x = i - radius; // Center at zero, ranges from -radius to +radius
                kernel[i] = MathHelper.Gaussian(x, standardDeviation);
                sum += kernel[i];
            }

            // Normalize the kernel so weights sum to 1.0
            if (sum > 0)
            {
                for (int i = 0; i < kernelSize; i++)
                {
                    kernel[i] /= sum;
                }
            }

            return kernel;
        }
    }
}
