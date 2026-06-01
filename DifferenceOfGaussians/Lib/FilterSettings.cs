namespace DifferenceOfGaussians.Lib
{
    public class FilterSettings
    {
        public FlowDifferenceOfGaussiansSettings FlowDifferenceOfGaussians { get; set; } = new();
        public CrossHatchSettings CrossHatch { get; set; } = new();
    }

    /// <summary>
    /// Settings for the Flow-based Difference of Gaussians (FDoG).
    ///
    /// Parameter guidance from Winnemoller et al., Appendix A:
    ///   SigmaC — structure tensor blur width. Typical range 0.1 – 6.
    ///   SigmaE — edge-aligned DoG width. Controls edge line width.
    ///   SigmaM — tangent-aligned LIC width. Controls stroke coherence.
    ///   P      — edge emphasis strength.
    /// </summary>
    public class FlowDifferenceOfGaussiansSettings
    {
        public double SigmaC { get; set; } = 0.2;
        public double SigmaE { get; set; } = 1.4;
        public double SigmaM { get; set; } = 4.4;
        public double P { get; set; } = 20.0;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Cross-hatch settings
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One layer = one Gaussian blur + one hatch texture.
    ///
    /// Sigma     — blur radius. Larger sigma → broader tonal range captured
    ///             (coarse shading). Smaller sigma → fine detail only.
    /// Threshold — epsilon in [0,1]. Controls where the mask turns on.
    ///             Higher value → only the darkest regions get hatching.
    ///             Lower value  → more of the midtones get hatching.
    ///
    /// Typical four-layer setup (light → dark):
    ///   Layer 0 (0°)   σ=2  ε=0.9  — catches only deepest shadows
    ///   Layer 1 (45°)  σ=4  ε=0.7  — darker midtones
    ///   Layer 2 (90°)  σ=6  ε=0.5  — midtones
    ///   Layer 3 (135°) σ=8  ε=0.3  — light midtones and shadows
    /// </summary>
    public class CrossHatchLayerSettings
    {
        public double SigmaC { get; set; }
        public double SigmaE { get; set; }
        public double SigmaM { get; set; }
        public double P { get; set; }

        public double Threshold { get; set; }
    }

    public class CrossHatchSettings
    {
        /// <summary>
        /// Path to the folder containing hatch_0.png … hatch_3.png.
        /// Relative paths are resolved from the working directory.
        /// </summary>
        public string AssetsFolder { get; set; } = "Assets";

        /// <summary>
        /// Exactly four layers, one per hatch texture.
        /// Order must match the texture filenames (hatch_0 … hatch_3).
        /// </summary>
        public List<CrossHatchLayerSettings> Layers { get; set; } = [];
    }
}