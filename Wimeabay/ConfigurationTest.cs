using Microsoft.Extensions.Configuration;

namespace Wimeabay
{
    // Example configuration test class to demonstrate URI loading
    public class ConfigurationTest
    {
        public static void TestConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var mediaClientUri = configuration["AzureCommunicationServices:MediaClientUri"];
            Console.WriteLine($"Loaded MediaClientUri from config: {mediaClientUri}");
        }
    }
}