namespace DifferenceOfGaussians.Lib
{
    public static class MathHelper
    {
        public static double Gaussian(double x, double standardDeviation)
            {
                double a = 1.0 / (standardDeviation * Math.Sqrt(2 * Math.PI));

                // b is always 0 since curve is normalized
                return a * Math.Exp(-(x * x) / (2 * standardDeviation * standardDeviation));
            }
    }
}
