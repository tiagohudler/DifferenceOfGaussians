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

                        // kernel radius 7 is sufficient, pixels outside this radius can safely be ignored
                        // sigma1 (larger blur) and sigma2 (smaller blur) for Difference of Gaussians
                        var dog = new DoG(20, 4, 7);

                        using var result = dog.Apply(file);

                        FileStream output = new FileStream(file.FullName.Replace(".", "_dog."), FileMode.OpenOrCreate);

                        result.Position = 0;
                        result.CopyTo(output);

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
