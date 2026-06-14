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

        /// <summary>
        /// Applies the FDoG to an array of grayscale values
        /// </summary>
        /// <param name="gray">The grayscale values</param>
        /// <param name="width">Width of each row</param>
        /// <param name="height">Number of rows</param>
        /// <returns>The final processed values</returns>
        public float[] Apply(float[] gray, int width, int height)
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

            return
                TangentAlignedSmooth(
                    dogRaw,
                    tx,
                    ty,
                    width,
                    height);
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

                        double w1 = MathHelper.Gaussian(s, sigmaE);
                        double w2 = MathHelper.Gaussian(s, sigmaE2);

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
                            double w     = MathHelper.Gaussian(s, sigmaM);

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
    }
}
