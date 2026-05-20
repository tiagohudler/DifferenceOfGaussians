namespace DifferenceOfGaussians.Lib
{
    public class FilterSettings
    {
        public DifferenceOfGaussiansSettings DifferenceOfGaussians { get; set; } = new();
        public ThresholdSettings Threshold { get; set; } = new();
    }

    public class DifferenceOfGaussiansSettings
    {
        public double StandardDeviation1 { get; set; } = 20;
        public double StandardDeviation2 { get; set; } = 4;
        public int KernelRadius { get; set; } = 7;
        public double ExtendedDoGParameter { get; set; } = 0.5;
    }

    public class ThresholdSettings
    {
        public int ThresholdValue { get; set; } = 128;
        public double Phi { get; set; } = 1.0;
    }
}
