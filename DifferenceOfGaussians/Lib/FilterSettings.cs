namespace DifferenceOfGaussians.Lib
{
    public class FilterSettings
    {
        public DifferenceOfGaussiansSettings DifferenceOfGaussians { get; set; } = new();
        public ThresholdSettings Threshold { get; set; } = new();
    }

    public class DifferenceOfGaussiansSettings
    {
        public double BaseStandardDeviation { get; set; } = 15;
        public double ExtendedDoGParameter { get; set; } = 15;
    }

    public class ThresholdSettings
    {
        public double ThresholdValue { get; set; } = 128;
        public double Phi { get; set; } = 1.0;
    }
}
