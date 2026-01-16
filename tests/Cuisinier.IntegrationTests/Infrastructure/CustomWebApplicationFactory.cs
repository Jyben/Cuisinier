using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Cuisinier.Infrastructure.Data;
using Cuisinier.Infrastructure.Services;
using Cuisinier.Shared.DTOs;

namespace Cuisinier.IntegrationTests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<CuisinierDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Remove existing DbContext
            var dbContextServiceDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(CuisinierDbContext));
            if (dbContextServiceDescriptor != null)
            {
                services.Remove(dbContextServiceDescriptor);
            }

            // Add in-memory database for testing
            services.AddDbContext<CuisinierDbContext>(options =>
            {
                options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}");
            });

            // Mock OpenAI Service
            var openAIDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IOpenAIService));
            if (openAIDescriptor != null)
            {
                services.Remove(openAIDescriptor);
            }

            var mockOpenAI = Substitute.For<IOpenAIService>();
            mockOpenAI.GenerateMenuAsync(Arg.Any<MenuParameters>())
                .Returns(Task.FromResult(new MenuResponse
                {
                    Recipes = new List<RecipeResponse>
                    {
                        new()
                        {
                            Title = "Test Recipe",
                            Servings = 4,
                            Ingredients = new List<IngredientResponse>
                            {
                                new() { Name = "Test Ingredient", Quantity = "100g", Category = "Légumes" }
                            }
                        }
                    }
                }));
            services.AddSingleton(mockOpenAI);

            // Mock Email Service
            var emailDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IEmailService));
            if (emailDescriptor != null)
            {
                services.Remove(emailDescriptor);
            }

            var mockEmail = Substitute.For<IEmailService>();
            mockEmail.SendConfirmationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(Task.CompletedTask);
            mockEmail.SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(Task.CompletedTask);
            services.AddScoped<IEmailService>(_ => mockEmail);

            // Build service provider and ensure database is created
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CuisinierDbContext>();
            db.Database.EnsureCreated();
        });

        builder.UseEnvironment("Testing");
    }
}
