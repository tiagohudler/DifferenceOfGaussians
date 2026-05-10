using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DifferenceOfGaussians.Lib
{
    public static class MathHelper
    {
        public static double Gaussian(double x, double standardDeviation)
        {
            double a = 1 / standardDeviation * Math.Sqrt(Math.PI * 2);

            // b is awlays 0 since curve is normalized
            return a * Math.Exp(-(x * x) / (2 * standardDeviation * standardDeviation));
        }
    }
}
