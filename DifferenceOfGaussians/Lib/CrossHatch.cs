using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection.Emit;

namespace DifferenceOfGaussians.Lib
{
    /// <summary>
    /// Cross-hatching filter.
    ///
    /// Pipeline
    /// ────────
    /// 1. Convert input to grayscale float [0,1].
    /// 2. Apply four Gaussian blurs (σ₁ … σ₄) and threshold each with four
    ///    matching epsilon values → four binary-ish masks M₁ … M₄.
    ///    A mask pixel is WHITE (1.0) where the blurred image is light (no stroke),
    ///    and DARK (0.0) where it is dark (stroke should appear).
    /// 3. Load four hatch textures from the Assets folder (0°, 45°, 90°, 135°).
    ///    Textures are expected to be white lines on a white background; dark pixels
    ///    are the hatch strokes.
    /// 4. Composite each hatch texture using its mask:
    ///      composited = lerp(texture, white, mask)
    ///    → Where the mask is WHITE the layer is forced to white (no stroke).
    ///    → Where the mask is DARK  the original texture pixel shows through.
    /// 5. Multiply all four composited layers together (values in [0,1]) to produce
    ///    the final cross-hatch image.
    /// 6. Return the result as a PNG stream.
    ///
    /// Naming convention expected in Assets/:
    ///   hatch_0.png, hatch_1.png, hatch_2.png, hatch_3.png
    /// (0° / 45° / 90° / 135° or whatever order you prefer)
    /// </summary>
    public class CrossHatch
    {
        private readonly FilterSettings settings;
        private readonly string assetsFolder;
        private readonly string outputFolder;

        public CrossHatch(FilterSettings settings, string assetsFolder, string outputFolder = @"F:\Pictures\Results")
        {
            this.settings      = settings;
            this.assetsFolder  = assetsFolder;
            this.outputFolder  = outputFolder;
        }

        // ───────────────────────────────────────────────────────────────────
        // Public entry point
        // ───────────────────────────────────────────────────────────────────

        public Stream Apply(FileInfo imageFile)
        {
            // Create output folder if it doesn't exist
            Directory.CreateDirectory(outputFolder);

            // ── Load source image ──────────────────────────────────────────
            using Bitmap src = new Bitmap(imageFile.FullName);

            int width   = src.Width;
            int height  = src.Height;
            int n       = width * height;

            Rectangle  rect       = new Rectangle(0, 0, width, height);
            BitmapData srcData    = src.LockBits(rect, ImageLockMode.ReadOnly, src.PixelFormat);
            int        bpp        = src.PixelFormat == PixelFormat.Format32bppArgb ? 4 : 3;
            int        stride     = srcData.Stride;
            int        bytesTotal = Math.Abs(stride) * height;
            byte[]     pixels     = new byte[bytesTotal];
            System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, pixels, 0, bytesTotal);
            src.UnlockBits(srcData);

            // ── Step 1: grayscale [0,1] ────────────────────────────────────
            float[] gray = ToGrayscale(pixels, width, height, stride, bpp);
            SaveLayerToDisk(gray, width, height, "grayscale.png");

            // ── Steps 2: four blur → threshold masks ───────────────────────
            var layers = settings.CrossHatch.Layers;
            if (layers == null || layers.Count == 0)
                throw new InvalidOperationException("CrossHatch: no layers configured.");

            int layerCount = layers.Count;

            // Each mask[i][px] ∈ [0,1]: 1 = white (no stroke), 0 = dark (stroke)
            float[][] masks = new float[layerCount][];

            var fdogger =
                new FlowDifferenceOfGaussians(
                    sigmaC: settings.FlowDifferenceOfGaussians.SigmaC,
                    sigmaE: settings.FlowDifferenceOfGaussians.SigmaE,
                    sigmaM: settings.FlowDifferenceOfGaussians.SigmaM,
                    p: settings.FlowDifferenceOfGaussians.P);

            var fdog = fdogger.Apply(gray, width, height);

            SaveLayerToDisk(fdog, width, height, $"smoothed_fdog.png");

            for (int li = 0; li < layerCount; li++)
            {
                var layer = layers[li];

                masks[li] =
                    Threshold(
                        gray,
                        width,
                        height,
                        layer.Threshold);

                // Save final mask
                SaveLayerToDisk(masks[li], width, height, $"mask_{li:D2}_mask.png");
            }

            // ── Step 3: load hatch textures ────────────────────────────────
            float[][] textures = new float[layerCount][];

            var textureFiles = Directory.GetFiles(assetsFolder);

            int i = 0;
            foreach (string textureFilePath in textureFiles)
            {
                textures[i] = LoadTextureGray(textureFilePath, width, height);
                i++;
            }

            // ── Steps 4–5: composite and multiply ─────────────────────────
            // result[px] starts at 1.0 (white); each layer multiplies in.
            float[] result = new float[n];
            Array.Fill(result, 1.0f);

            for (int li = 0; li < layerCount; li++)
            {
                float[] mask    = masks[li];
                float[] texture = textures[li];

                for (int px = 0; px < n; px++)
                {
                    // Where mask ≈ 1 (light region) → force white, no stroke.
                    // Where mask ≈ 0 (dark region)  → let the texture through.
                    float composited = texture[px] + mask[px] * (1.0f - texture[px]);
                    // equivalent to: lerp(texture, 1, mask)

                    result[px] *= composited;
                }

                // Save composited layer result to disk
                SaveLayerToDisk(result, width, height, $"layer_{li:D2}_composited.png");
            }

            // ── Step 6: write output PNG ───────────────────────────────────
            Bitmap    dst     = new Bitmap(width, height, src.PixelFormat);
            BitmapData dstData = dst.LockBits(rect, ImageLockMode.WriteOnly, src.PixelFormat);
            int       dstStride = dstData.Stride;
            int       dstBpp    = src.PixelFormat == PixelFormat.Format32bppArgb ? 4 : 3;
            byte[]    dstPixels = new byte[Math.Abs(dstStride) * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte v   = (byte)Math.Clamp(result[y * width + x] * 255.0f, 0f, 255f);
                    int  idx = y * dstStride + x * dstBpp;
                    dstPixels[idx]     = v;
                    dstPixels[idx + 1] = v;
                    dstPixels[idx + 2] = v;
                    if (dstBpp == 4) dstPixels[idx + 3] = pixels[y * stride + x * bpp + 3];
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(dstPixels, 0, dstData.Scan0, dstPixels.Length);
            dst.UnlockBits(dstData);

            MemoryStream output = new MemoryStream();
            dst.Save(output, System.Drawing.Imaging.ImageFormat.Png);

            // Save final result
            dst.Save(Path.Combine(outputFolder, "final_result.png"), System.Drawing.Imaging.ImageFormat.Png);

            dst.Dispose();
            output.Position = 0;
            return output;
        }

        // ───────────────────────────────────────────────────────────────────
        // Save a float array layer as a PNG image
        // ───────────────────────────────────────────────────────────────────
        private void SaveLayerToDisk(float[] layer, int width, int height, string filename)
        {
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, bitmap.PixelFormat);

            int stride = data.Stride;
            byte[] pixels = new byte[Math.Abs(stride) * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte v = (byte)Math.Clamp(layer[y * width + x] * 255.0f, 0f, 255f);
                    int idx = y * stride + x * 3;
                    pixels[idx]     = v;
                    pixels[idx + 1] = v;
                    pixels[idx + 2] = v;
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            bitmap.UnlockBits(data);

            string filepath = Path.Combine(outputFolder, filename);
            bitmap.Save(filepath, System.Drawing.Imaging.ImageFormat.Png);
            bitmap.Dispose();
        }

        // ───────────────────────────────────────────────────────────────────
        // Load a hatch texture, resize to match the source, return gray [0,1]
        // ───────────────────────────────────────────────────────────────────
        private static float[] LoadTextureGray(string path, int width, int height)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"CrossHatch: hatch texture not found at '{path}'. " +
                    "Ensure the Assets folder contains hatch_0.png … hatch_3.png.");

            using Bitmap tex = new Bitmap(path);

            // Resize to match the source image if necessary
            Bitmap resized;
            if (tex.Width == width && tex.Height == height)
            {
                resized = tex;
            }
            else
            {
                resized = new Bitmap(width, height);
                using Graphics g = Graphics.FromImage(resized);
                g.InterpolationMode =
                    System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(tex, 0, 0, width, height);
            }

            Rectangle  rect  = new Rectangle(0, 0, width, height);
            BitmapData data  = resized.LockBits(rect, ImageLockMode.ReadOnly, resized.PixelFormat);
            int        bpp   = resized.PixelFormat == PixelFormat.Format32bppArgb ? 4 : 3;
            int        stride = data.Stride;
            byte[]     buf   = new byte[Math.Abs(stride) * height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, buf.Length);
            resized.UnlockBits(data);

            if (!object.ReferenceEquals(resized, tex)) resized.Dispose();

            float[] gray = new float[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int   i = y * stride + x * bpp;
                    float b = buf[i]     / 255.0f;
                    float gr= buf[i + 1] / 255.0f;
                    float r = buf[i + 2] / 255.0f;
                    gray[y * width + x] = 0.114f * b + 0.587f * gr + 0.299f * r;
                }

            return gray;
        }

        // ───────────────────────────────────────────────────────────────────
        // Grayscale conversion (same formula used throughout the project)
        // ───────────────────────────────────────────────────────────────────
        private static float[] ToGrayscale(
            byte[] pixels, int width, int height, int stride, int bpp)
        {
            float[] gray = new float[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int   idx = y * stride + x * bpp;
                    float b   = pixels[idx]     / 255.0f;
                    float g   = pixels[idx + 1] / 255.0f;
                    float r   = pixels[idx + 2] / 255.0f;
                    gray[y * width + x] = 0.114f * b + 0.587f * g + 0.299f * r;
                }
            return gray;
        }

        private static float[] Threshold(float[] input, int width, int height, double epsilon, double phi = 10.0)
        {
            int n = width * height;

            float[] mask =
                new float[n];

            for (int i = 0; i < n; i++)
            {
                double u = input[i];

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
    }
}
