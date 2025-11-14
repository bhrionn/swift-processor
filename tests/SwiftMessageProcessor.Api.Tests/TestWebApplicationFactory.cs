using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Infrastructure.Data;
using NSubstitute;

namespace SwiftMessageProcessor.Api.Tests;

/// <summary>
/// Custom WebApplicationFactory for integration testing
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public IProcessCommunicationService? MockCommunicationService { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add test configuration from test project directory
            var testConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Testing.json");
            config.AddJsonFile(testConfigPath, optional: false);
        });

        builder.ConfigureServices(services =>
        {
            // Replace IProcessCommunicationService with a mock
            var commDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IProcessCommunicationService));
            if (commDescriptor != null)
            {
                services.Remove(commDescriptor);
            }
            
            MockCommunicationService = Substitute.For<IProcessCommunicationService>();
            services.AddSingleton(MockCommunicationService);

            // Build the service provider
            var sp = services.BuildServiceProvider();

            // Create a scope to obtain a reference to the database context
            using var scope = sp.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<SwiftMessageContext>();

            // Ensure the database is created
            db.Database.EnsureCreated();
        });
    }

    public void ResetMockCommunicationService()
    {
        MockCommunicationService?.ClearReceivedCalls();
    }
}
