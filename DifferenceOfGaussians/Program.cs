using DifferenceOfGaussians.Lib;
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

                    var crosshatchSettings = settings.CrossHatch;

                    Console.WriteLine(
                        $"Running CrossHatch  assets='{crosshatchSettings.AssetsFolder}'  layers={crosshatchSettings.Layers.Count}");

                    for (int i = 0; i < crosshatchSettings.Layers.Count; i++)
                    {
                        var l = crosshatchSettings.Layers[i];
                        Console.WriteLine(
                            $"  hatch {i} ε={l.Threshold}");
                    }

                    // Resolve assets folder relative to the working directory
                    string assetsPath = Path.IsPathRooted(crosshatchSettings.AssetsFolder)
                        ? crosshatchSettings.AssetsFolder
                        : Path.Combine(Directory.GetCurrentDirectory(), crosshatchSettings.AssetsFolder);

                    var crossHatch = new CrossHatch(crosshatchSettings, assetsPath);
                    using Stream crossHatchStream = crossHatch.Apply(file);

                    string outputPath = Path.Combine(
                        Path.GetDirectoryName(file.FullName)!,
                        Path.GetFileNameWithoutExtension(file.Name) + "_crosshatch.png");

                    using var output = new FileStream(outputPath, FileMode.Create);
                    crossHatchStream.Position = 0;
                    crossHatchStream.CopyTo(output);

                    Console.WriteLine($"Done! → {outputPath}");
                    break;
                }
                catch (FileNotFoundException ex)
                {
                    Console.WriteLine($"File not found: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An unexpected error occurred: {ex}");
                }
            }
        }
    }
}