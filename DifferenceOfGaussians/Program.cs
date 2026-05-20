using DifferenceOfGaussians.Lib;
using DoG = DifferenceOfGaussians.Lib.DifferenceOfGaussians;

namespace DifferenceOfGaussians
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("What do you want to do?\nq: quit\ns: select image to apply fiter");

            string? input = Console.ReadLine();
            while (input != "s" && input != "q")
            {
                input = Console.ReadLine();
            }

            if (input == "s")
            {
                Console.WriteLine("Please enter the absolute path to your image");
                while (true)
                {
                    var targetPath = Console.ReadLine();

                    if (targetPath == null)
                    {
                        continue;
                    }

                    var file = new FileInfo(targetPath);

                    try
                    {
                        var fs = file.Open(FileMode.Open);
                        fs.Close();

                        var dog = new DoG(20, 4, 7, t: 0.5);
                        using var dogResult = dog.Apply(file);

                        // Save DoG result to temporary file
                        string tempFile = file.FullName.Replace(".", "_temp.");
                        using (FileStream tempOutput = new FileStream(tempFile, FileMode.OpenOrCreate))
                        {
                            dogResult.Position = 0;
                            dogResult.CopyTo(tempOutput);
                        }

                        // Apply thresholding to create binary image (black background, white edges)
                        var threshold = new Threshold(128);
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
