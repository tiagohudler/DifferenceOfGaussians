using DifferenceOfGaussians.Lib;
using DoG  = DifferenceOfGaussians.Lib.DifferenceOfGaussians;
using FDoG = DifferenceOfGaussians.Lib.FlowDifferenceOfGaussians;
using Microsoft.Extensions.Configuration;

namespace DifferenceOfGaussians
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var settings = new FilterSettings();
            configuration.Bind(settings);

            Console.WriteLine($"Mode: {settings.Mode}");

            while (true)
            {
                Console.WriteLine("Please enter the absolute path to your image or 'q' to exit");

                var targetPath = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(targetPath)) continue;
                if (targetPath == "q") break;

                var file = new FileInfo(targetPath);

                try
                {
                    using var check = file.Open(FileMode.Open);
                    check.Close();

                    Stream dogStream;

                    if (settings.Mode.Equals("flow", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine(
                            $"Running FDoG  σc={settings.FlowDifferenceOfGaussians.SigmaC}  " +
                            $"σe={settings.FlowDifferenceOfGaussians.SigmaE}  " +
                            $"σm={settings.FlowDifferenceOfGaussians.SigmaM}  " +
                            $"p={settings.FlowDifferenceOfGaussians.P}");

                        var fdog = new FDoG(
                            settings.FlowDifferenceOfGaussians.SigmaC,
                            settings.FlowDifferenceOfGaussians.SigmaE,
                            settings.FlowDifferenceOfGaussians.SigmaM,
                            settings.FlowDifferenceOfGaussians.P);

                        dogStream = fdog.Apply(file);
                    }
                    else
                    {
                        Console.WriteLine(
                            $"Running XDoG  σ={settings.DifferenceOfGaussians.BaseStandardDeviation}  " +
                            $"t={settings.DifferenceOfGaussians.ExtendedDoGParameter}");

                        var dog = new DoG(
                            settings.DifferenceOfGaussians.BaseStandardDeviation,
                            settings.DifferenceOfGaussians.BaseStandardDeviation * 1.6,
                            t: settings.DifferenceOfGaussians.ExtendedDoGParameter);

                        dogStream = dog.Apply(file);
                    }

                    // Write to temp file so Threshold (which takes a FileInfo) can read it.
                    // PNG is lossless so the byte-level round-trip is exact.
                    string tempFile = Path.Combine(
                        Path.GetTempPath(),
                        Path.GetFileNameWithoutExtension(file.Name) + "_dog_tmp.png");

                    using (var tempOut = new FileStream(tempFile, FileMode.Create))
                    {
                        dogStream.Position = 0;
                        dogStream.CopyTo(tempOut);
                    }
                    dogStream.Dispose();

                    // Threshold pass
                    var threshold = new Threshold(
                        settings.Threshold.ThresholdValue,
                        settings.Threshold.Phi);

                    using var thresholdResult = threshold.Apply(new FileInfo(tempFile));

                    string suffix     = settings.Mode.Equals("flow", StringComparison.OrdinalIgnoreCase) ? "_fdog" : "_dog";
                    string outputPath = Path.Combine(
                        Path.GetDirectoryName(file.FullName)!,
                        Path.GetFileNameWithoutExtension(file.Name) + suffix + ".png");

                    using var output = new FileStream(outputPath, FileMode.Create);
                    thresholdResult.Position = 0;
                    thresholdResult.CopyTo(output);

                    File.Delete(tempFile);

                    Console.WriteLine($"Done! → {outputPath}");
                    break;
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine($"File '{targetPath}' not found.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An unexpected error occurred: {ex}");
                }
            }
        }
    }
}
