using DifferenceOfGaussians.Lib;
using DoG = DifferenceOfGaussians.Lib.DifferenceOfGaussians;
using Microsoft.Extensions.Configuration;

namespace DifferenceOfGaussians
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Load configuration from appsettings.json
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var settings = new FilterSettings();
            configuration.Bind(settings);

            Console.WriteLine("What do you want to do?\nq: quit\ns: select image to apply fiter");

            string? input = Console.ReadLine();
            while (input != "s" && input != "q")
            {
                input = Console.ReadLine();
            }

            if (input == "s")
            {
                while (true)
                {
                    Console.WriteLine("Please enter the absolute path to your image");

                    var targetPath = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(targetPath))
                    {
                        continue;
                    }
                    else if (targetPath == "q")
                    {
                        break;
                    }

                    var file = new FileInfo(targetPath);

                    try
                    {
                        var fs = file.Open(FileMode.Open);
                        fs.Close();

                        // Apply Extended Difference of Gaussians with settings from appsettings.json
                        var dog = new DoG(
                            settings.DifferenceOfGaussians.StandardDeviation1,
                            settings.DifferenceOfGaussians.StandardDeviation2,
                            settings.DifferenceOfGaussians.KernelRadius,
                            t: settings.DifferenceOfGaussians.ExtendedDoGParameter
                        );
                        using var dogResult = dog.Apply(file);

                        // Save DoG result to temporary file
                        string tempFile = file.FullName.Replace(".", "_temp.");
                        using (FileStream tempOutput = new FileStream(tempFile, FileMode.OpenOrCreate))
                        {
                            dogResult.Position = 0;
                            dogResult.CopyTo(tempOutput);
                        }

                        // Apply thresholding with settings from appsettings.json
                        var threshold = new Threshold(settings.Threshold.ThresholdValue, settings.Threshold.Phi);
                        using var thresholdResult = threshold.Apply(new FileInfo(tempFile));

                        // Save final thresholded result
                        FileStream output = new FileStream(file.FullName.Replace(".", "_dog."), FileMode.OpenOrCreate);
                        thresholdResult.Position = 0;
                        thresholdResult.CopyTo(output);
                        output.Close();

                        // Clean up temporary file
                        File.Delete(tempFile);

                        Console.WriteLine("Done!");

                        break;
                    }
                    catch (FileNotFoundException)
                    {
                        Console.WriteLine($"File {targetPath} not found");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"An unexpected error occurred: {ex}");
                        continue;
                    }
                }
            }
        }
    }
}
