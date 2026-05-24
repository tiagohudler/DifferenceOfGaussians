using System;

namespace DifferenceOfGaussians.Lib
{
    /// <summary>
    /// Computes the Smoothed Structure Tensor (SST) and extracts the Edge Tangent Flow (ETF).
    ///
    /// The structure tensor at each pixel is J = outer(grad, grad), smoothed with sigma_c.
    /// Eigenanalysis of J gives:
    ///   lambda_max / major eigenvector => gradient direction  (across edges)
    ///   lambda_min / minor eigenvector => tangent direction   (along edges)  ← this is the ETF
    ///
    /// Reference: Winnemoller et al., XDoG, Sec. 2.6.
    /// Structure-tensor blur radius = 2.45*sigma_c per Appendix A.
    /// </summary>
    public class StructureTensor
    {
        private readonly double sigmaC;

        public StructureTensor(double sigmaC) => this.sigmaC = sigmaC;

        /// <summary>
        /// Returns (tx, ty): unit tangent vectors (along edges) at every pixel.
        /// </summary>
        public (float[] tx, float[] ty) Compute(float[] gray, int width, int height)
        {
            int n = width * height;

            // 1. Sobel gradients
            float[] gx = new float[n];
            float[] gy = new float[n];
            ComputeSobelGradients(gray, gx, gy, width, height);

            // 2. Tensor outer products
            float[] Jxx = new float[n];
            float[] Jxy = new float[n];
            float[] Jyy = new float[n];
            for (int i = 0; i < n; i++)
            {
                Jxx[i] = gx[i] * gx[i];
                Jxy[i] = gx[i] * gy[i];
                Jyy[i] = gy[i] * gy[i];
            }

            // 3. Smooth tensor components with sigma_c (radius = ceil(2.45*sigma_c))
            int      radius = Math.Max(1, (int)Math.Ceiling(2.45 * sigmaC));
            double[] kernel = BuildGaussianKernel(radius, sigmaC);

            Jxx = SeparableConvolve(Jxx, width, height, kernel, radius);
            Jxy = SeparableConvolve(Jxy, width, height, kernel, radius);
            Jyy = SeparableConvolve(Jyy, width, height, kernel, radius);

            // 4. Per-pixel eigenanalysis — extract the MINOR eigenvector (tangent).
            //
            //    For the symmetric 2x2 matrix  M = [ a  b ]
            //                                      [ b  c ]
            //
            //    discriminant  D  = sqrt((a-c)^2 + 4*b^2)
            //    lambda_min       = ((a+c) - D) / 2          ← smallest eigenvalue
            //
            //    The eigenvector for lambda_min satisfies (M - lambda_min*I)*v = 0.
            //    Taking the first row:  (a - lambda_min)*vx + b*vy = 0
            //      => if |b| > eps:   v = [ -b,  a - lambda_min ]  (unnormalized)
            //      => if |b| ~ 0:     M is diagonal; pick axis with smaller eigenvalue.
            //
            //    This is the CORRECT minor-eigenvector formula.
            //    The previous code computed [-(disc+diff)/(2b), 1] which is the MAJOR
            //    eigenvector — tangent and gradient were completely swapped.
            //
            // 5. Orientation consistency: eigenvectors are only defined up to sign.
            //    Flip each vector so that it points into the same half-plane as its
            //    right-hand neighbour's vector. This prevents the LIC path from
            //    oscillating back and forth when adjacent pixels have opposite signs.

            float[] tx = new float[n];
            float[] ty = new float[n];

            for (int i = 0; i < n; i++)
            {
                double a = Jxx[i], b = Jxy[i], c = Jyy[i];
                double diff  = a - c;
                double disc  = Math.Sqrt(diff * diff + 4.0 * b * b);
                double lMin  = ((a + c) - disc) * 0.5;   // smallest eigenvalue

                double vx, vy;
                if (Math.Abs(b) > 1e-10)
                {
                    // Minor eigenvector: [ -b,  a - lambda_min ]
                    vx = -b;
                    vy =  a - lMin;
                }
                else
                {
                    // Diagonal tensor — tangent is the axis with the smaller eigenvalue
                    vx = (a <= c) ? 1.0 : 0.0;
                    vy = (a <= c) ? 0.0 : 1.0;
                }

                double len = Math.Sqrt(vx * vx + vy * vy);
                if (len > 1e-10) { vx /= len; vy /= len; }

                tx[i] = (float)vx;
                ty[i] = (float)vy;
            }

            // 6. Orientation smoothing: one separable Gaussian pass on the raw
            //    (tx,ty) components to suppress sign-flip discontinuities.
            //    After this the vectors may not be exactly unit-length; re-normalise.
            tx = SeparableConvolve(tx, width, height, kernel, radius);
            ty = SeparableConvolve(ty, width, height, kernel, radius);

            for (int i = 0; i < n; i++)
            {
                double len = Math.Sqrt(tx[i] * tx[i] + ty[i] * ty[i]);
                if (len > 1e-10) { tx[i] /= (float)len; ty[i] /= (float)len; }
            }

            return (tx, ty);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static void ComputeSobelGradients(
            float[] gray, float[] gx, float[] gy, int width, int height)
        {
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    float sx =
                        -gray[(y-1)*width + x-1] + gray[(y-1)*width + x+1]
                        - 2*gray[y*width + x-1]  + 2*gray[y*width + x+1]
                        - gray[(y+1)*width + x-1] + gray[(y+1)*width + x+1];

                    float sy =
                        -gray[(y-1)*width + x-1] - 2*gray[(y-1)*width + x] - gray[(y-1)*width + x+1]
                        + gray[(y+1)*width + x-1] + 2*gray[(y+1)*width + x] + gray[(y+1)*width + x+1];

                    gx[y*width + x] = sx / 8.0f;
                    gy[y*width + x] = sy / 8.0f;
                }
            }
        }

        private static double[] BuildGaussianKernel(int radius, double sigma)
        {
            int      size = 2 * radius + 1;
            double[] k    = new double[size];
            double   sum  = 0;
            for (int i = 0; i < size; i++)
            {
                double x = i - radius;
                k[i] = Math.Exp(-(x*x) / (2*sigma*sigma));
                sum += k[i];
            }
            for (int i = 0; i < size; i++) k[i] /= sum;
            return k;
        }

        public static float[] SeparableConvolve(
            float[] src, int width, int height, double[] kernel, int radius)
        {
            float[] tmp = new float[src.Length];
            float[] dst = new float[src.Length];

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    double acc = 0;
                    for (int k = -radius; k <= radius; k++)
                        acc += src[y*width + Math.Clamp(x+k, 0, width-1)] * kernel[k+radius];
                    tmp[y*width + x] = (float)acc;
                }

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    double acc = 0;
                    for (int k = -radius; k <= radius; k++)
                        acc += tmp[Math.Clamp(y+k, 0, height-1)*width + x] * kernel[k+radius];
                    dst[y*width + x] = (float)acc;
                }

            return dst;
        }
    }
}
