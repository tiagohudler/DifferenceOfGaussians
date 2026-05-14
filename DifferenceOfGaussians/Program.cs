using DifferenceOfGaussians.Lib;

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

                        var gaussianBlur = new GaussianBlur(8);

                        using var result = gaussianBlur.Blur(file);

                        FileStream output = new FileStream(file.FullName.Replace(".", "_blurred."), FileMode.OpenOrCreate);

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
