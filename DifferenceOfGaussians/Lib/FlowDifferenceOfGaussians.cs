using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace DifferenceOfGaussians.Lib
{
    /// <summary>
    /// Flow-based Difference of Gaussians (FDoG) — Winnemoller et al., XDoG, Sec. 2.6.
    ///
    /// Pipeline:
    ///   1. Convert to grayscale float [0,1].
    ///   2. Build Edge Tangent Flow (ETF) via Smoothed Structure Tensor with sigma_c.
    ///   3. For each pixel: 1-D DoG ACROSS edges (gradient direction) with sigma_e.
    ///      Uses XDoG reparameterisation Eq.7:  S = (1+p)*G_se - p*G_{k*se},  k=1.6.
    ///   4. Smooth DoG response ALONG edges (tangent direction) with sigma_m via LIC.
    ///   5. Write result as a float-range grayscale PNG for the Threshold pass.
    /// </summary>
    public class FlowDifferenceOfGaussians
    {
        private readonly double sigmaC;
        private readonly double sigmaE;
        private readonly double sigmaM;
        private readonly double p;

        // k = 1.6 per Marr & Hildreth recommendation used throughout the paper.
        private const double K = 1.6;

        public FlowDifferenceOfGaussians(
            double sigmaC, double sigmaE, double sigmaM, double p)
        {
            this.sigmaC = sigmaC;
            this.sigmaE = sigmaE;
            this.sigmaM = sigmaM;
            this.p      = p;
        }

        public Stream Apply(FileInfo imageFile)
        {
            using Bitmap src = new Bitmap(imageFile.FullName);

            int width  = src.Width;
            int height = src.Height;

            Rectangle  rect       = new Rectangle(0, 0, width, height);
            BitmapData data       = src.LockBits(rect, ImageLockMode.ReadOnly, src.PixelFormat);
            int        bpp        = src.PixelFormat == PixelFormat.Format32bppArgb ? 4 : 3;
            int        stride     = data.Stride;
            int        bytesTotal = Math.Abs(stride) * height;
            byte[]     pixels     = new byte[bytesTotal];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, bytesTotal);
            src.UnlockBits(data);

            // Step 1 — grayscale [0,1]
            float[] gray = ToGrayscale(pixels, width, height, stride, bpp);

            // Step 2 — Edge Tangent Flow
            var (tx, ty) = new StructureTensor(sigmaC).Compute(gray, width, height);

            // Step 3 — gradient-aligned 1-D DoG (raw, NOT clamped)
            float[] dogRaw = GradientAlignedDoG(gray, tx, ty, width, height);

            // Step 4 — tangent-aligned LIC smoothing
            float[] smoothed = TangentAlignedSmooth(dogRaw, tx, ty, width, height);

            // Step 5 — write back; map raw float to [0,255] byte via midpoint shift
            // The DoG/XDoG response is centred around the blurred image value (~0.5 for
            // a mid-grey image). We re-map so that 0.0 → 0, 1.0 → 255 directly; the
            // Threshold class then applies its tanh gate in [0,1] space.
            Bitmap    result    = new Bitmap(width, height, src.PixelFormat);
            BitmapData dstData  = result.LockBits(rect, ImageLockMode.WriteOnly, src.PixelFormat);
            int        dstStride = dstData.Stride;
            byte[]     dstPixels = new byte[Math.Abs(dstStride) * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte v   = (byte)Math.Clamp(smoothed[y * width + x] * 255.0f, 0f, 255f);
                    int  idx = y * dstStride + x * bpp;
                    dstPixels[idx]     = v;
                    dstPixels[idx + 1] = v;
                    dstPixels[idx + 2] = v;
                    if (bpp == 4) dstPixels[idx + 3] = pixels[y * stride + x * bpp + 3];
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(dstPixels, 0, dstData.Scan0, dstPixels.Length);
            result.UnlockBits(dstData);

            MemoryStream output = new MemoryStream();
            result.Save(output, ImageFormat.Png);
            result.Dispose();
            output.Position = 0;
            return output;
        }

        // ---------------------------------------------------------------
        // Step 3 — Gradient-aligned 1-D DoG
        // ---------------------------------------------------------------
        // Walk along the gradient direction (perpendicular to the tangent) and
        // compute two Gaussian-weighted sums at sigma_e and k*sigma_e, then combine
        // them using the XDoG reparameterisation (Eq. 7):
        //   S = (1+p)*G_sigmaE(x) - p*G_{k*sigmaE}(x)
        //
        // IMPORTANT: do NOT clamp here. The raw S value can exceed [0,1] and the
        // negative region is meaningful signal for the Threshold pass.
        private float[] GradientAlignedDoG(
            float[] gray, float[] tx, float[] ty, int width, int height)
        {
            float[] response = new float[width * height];

            double sigmaE2 = K * sigmaE;
            // Use 3x the LARGER sigma so the wider kernel is fully sampled.
            int radius = (int)Math.Ceiling(3.0 * sigmaE2);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;

                    // Gradient direction = rotate tangent 90°
                    float gx = -ty[i];
                    float gy =  tx[i];

                    double sum1 = 0, w1sum = 0;
                    double sum2 = 0, w2sum = 0;

                    for (int s = -radius; s <= radius; s++)
                    {
                        float sx = x + s * gx;
                        float sy = y + s * gy;

                        float sample = SampleBilinear(gray, sx, sy, width, height);

                        double w1 = Gauss(s, sigmaE);
                        double w2 = Gauss(s, sigmaE2);

                        sum1  += sample * w1;  w1sum += w1;
                        sum2  += sample * w2;  w2sum += w2;
                    }

                    double g1 = sum1 / w1sum;
                    double g2 = sum2 / w2sum;

                    // Eq. 7 — raw, unclamped
                    response[i] = (float)((1.0 + p) * g1 - p * g2);
                }
            }

            return response;
        }

        // ---------------------------------------------------------------
        // Step 4 — Tangent-aligned LIC smoothing
        // ---------------------------------------------------------------
        // Walk ALONG the tangent direction (both forward and backward) accumulating
        // Gaussian-weighted DoG response values.  This fuses short disconnected edge
        // fragments into long coherent strokes.
        //
        // We follow the tangent field continuously rather than stepping by integers,
        // so the path curves naturally with the ETF.
        private float[] TangentAlignedSmooth(
            float[] dog, float[] tx, float[] ty, int width, int height)
        {
            float[] result  = new float[width * height];
            int     radius  = (int)Math.Ceiling(3.0 * sigmaM);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;

                    double acc = 0, wsum = 0;

                    // Walk forward and backward along the tangent field.
                    // At each step we re-read the local tangent so the path curves
                    // with the ETF (proper LIC, not a straight-line scan).
                    for (int dir = -1; dir <= 1; dir += 2)
                    {
                        float cx = x, cy = y;

                        for (int s = 0; s <= radius; s++)
                        {
                            // Skip centre on the backward pass to avoid double-counting.
                            if (dir == -1 && s == 0) continue;

                            float sample = SampleBilinear(dog, cx, cy, width, height);
                            double w     = Gauss(s, sigmaM);

                            acc  += sample * w;
                            wsum += w;

                            // Advance one pixel-step along the tangent at the current position.
                            int lx = Math.Clamp((int)Math.Round(cx), 0, width  - 1);
                            int ly = Math.Clamp((int)Math.Round(cy), 0, height - 1);
                            float ttx = tx[ly * width + lx];
                            float tty = ty[ly * width + lx];

                            cx += dir * ttx;
                            cy += dir * tty;

                            // Stop if we walk off the image.
                            if (cx < 0 || cx >= width || cy < 0 || cy >= height) break;
                        }
                    }

                    result[i] = (float)(acc / wsum);
                }
            }

            return result;
        }

        // ---------------------------------------------------------------
        // Utilities
        // ---------------------------------------------------------------

        private static float[] ToGrayscale(
            byte[] pixels, int width, int height, int stride, int bpp)
        {
            float[] gray = new float[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int idx = y * stride + x * bpp;
                    float b = pixels[idx]     / 255.0f;
                    float g = pixels[idx + 1] / 255.0f;
                    float r = pixels[idx + 2] / 255.0f;
                    gray[y * width + x] = 0.114f * b + 0.587f * g + 0.299f * r;
                }
            return gray;
        }

        private static float SampleBilinear(float[] img, float x, float y, int width, int height)
        {
            x = Math.Clamp(x, 0, width  - 1.001f);
            y = Math.Clamp(y, 0, height - 1.001f);

            int x0 = (int)x, y0 = (int)y;
            int x1 = Math.Min(x0 + 1, width  - 1);
            int y1 = Math.Min(y0 + 1, height - 1);

            float fx = x - x0, fy = y - y0;

            return (img[y0 * width + x0] * (1 - fx) + img[y0 * width + x1] * fx) * (1 - fy)
                 + (img[y1 * width + x0] * (1 - fx) + img[y1 * width + x1] * fx) *      fy;
        }

        public float[] ComputeMask(
            float[] gray,
            int width,
            int height,
            double epsilon,
            double phi = 10.0)
        {
            var (tx, ty) =
                new StructureTensor(sigmaC)
                .Compute(gray, width, height);

            float[] dogRaw =
                GradientAlignedDoG(
                    gray,
                    tx,
                    ty,
                    width,
                    height);

            float[] smoothed =
                TangentAlignedSmooth(
                    dogRaw,
                    tx,
                    ty,
                    width,
                    height);

            int n = width * height;

            float[] mask =
                new float[n];

            for (int i = 0; i < n; i++)
            {
                double u = smoothed[i];

                double t;

                if (u >= epsilon)
                {
                    t = 1.0;
                }
                else
                {
                    t =
                        1.0
                        + Math.Tanh(
                            phi *
                            (u - epsilon));
                }

                mask[i] =
                    (float)Math.Clamp(
                        t,
                        0.0,
                        1.0);
            }

            return mask;
        }

        private static double Gauss(double x, double sigma)
            => Math.Exp(-(x * x) / (2.0 * sigma * sigma));
    }
}
