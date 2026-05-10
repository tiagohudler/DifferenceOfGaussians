using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DifferenceOfGaussians.Lib
{
    public class GaussianBlur
    {
        private int standardDeviation;

        public GaussianBlur(int standardDeviation)
        {
            this.standardDeviation = standardDeviation;
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

            int kernelRadius = 7;

            // make pass

            return new MemoryStream();
        }
    }
}
