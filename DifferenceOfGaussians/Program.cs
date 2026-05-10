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
                var targetPath = Console.ReadLine();

                while (true)
                {
                    targetPath = Console.ReadLine();

                    if (targetPath == null)
                    {
                        continue;
                    }

                    var file = new FileInfo(targetPath);

                    try
                    {
                        file.Open(FileMode.Open);
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
