using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wimeabay;

var builder = Host.CreateApplicationBuilder(args);

// Add configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Change to Singleton to maintain connections across calls
builder.Services.AddSingleton<IWimeabayService, WimeabayService>();

var host = builder.Build();

// Get the service and run the two clients example
var wimeabayService = host.Services.GetRequiredService<IWimeabayService>();

try
{
    Console.WriteLine("Running Two Clients - Same Session Example...");
    await TwoClientsOneSessionExample.RunExample();
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

Console.WriteLine("Application completed.");
