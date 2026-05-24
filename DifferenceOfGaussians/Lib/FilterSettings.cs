namespace DifferenceOfGaussians.Lib
{
    public class FilterSettings
    {
        public DifferenceOfGaussiansSettings DifferenceOfGaussians { get; set; } = new();
        public FlowDifferenceOfGaussiansSettings FlowDifferenceOfGaussians { get; set; } = new();
        public ThresholdSettings Threshold { get; set; } = new();

        /// <summary>
        /// Selects which DoG variant to run.
        /// "standard" — original isotropic XDoG (default).
        /// "flow"     — flow-based FDoG (Sec. 2.6 of the paper).
        /// </summary>
        public string Mode { get; set; } = "flow";
    }

    public class DifferenceOfGaussiansSettings
    {
        public double BaseStandardDeviation { get; set; } = 15;
        public double ExtendedDoGParameter  { get; set; } = 15;
    }

    /// <summary>
    /// Settings for the Flow-based Difference of Gaussians (FDoG).
    ///
    /// Parameter guidance from Winnemoller et al., Appendix A:
    ///   SigmaC — structure tensor blur width. Typical range 0.1 – 6.
    ///            Small values preserve fine detail; large values smooth the ETF.
    ///   SigmaE — edge-aligned DoG width. Controls edge line width.
    ///            Typical range 0.4 – 7. Start around 1.0–2.0.
    ///   SigmaM — tangent-aligned LIC width. Controls stroke coherence.
    ///            Typical range 4 – 20. Should not greatly exceed SigmaC.
    ///   P      — edge emphasis strength (XDoG reparameterisation).
    ///            Typical range 15 – 100. ~20 gives strong sketch-like edges.
    /// </summary>
    public class FlowDifferenceOfGaussiansSettings
    {
        public double SigmaC { get; set; } = 0.2;
        public double SigmaE { get; set; } = 1.4;
        public double SigmaM { get; set; } = 4.4;
        public double P      { get; set; } = 20.0;
    }

    public class ThresholdSettings
    {
        public double ThresholdValue { get; set; } = 0.5;
        public double Phi            { get; set; } = 1.0;
    }
}
