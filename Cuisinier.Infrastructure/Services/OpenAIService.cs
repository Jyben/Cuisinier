using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Cuisinier.Shared.DTOs;
using Cuisinier.Shared.Helpers;
using Cuisinier.Infrastructure.Services.Options;
using Cuisinier.Infrastructure.Services.Prompts;
using Cuisinier.Infrastructure.Services.Mappers;
using Cuisinier.Infrastructure.Services.Helpers;
using Cuisinier.Infrastructure.Services.DTOs;
using OpenAI.Chat;

namespace Cuisinier.Infrastructure.Services;

public class OpenAIService : IOpenAIService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<OpenAIService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly OpenAIServiceOptions _options;
    private readonly OpenAIResponseMapper _mapper;

    public OpenAIService(
        string apiKey,
        ILogger<OpenAIService> logger,
        IOptions<OpenAIServiceOptions> options,
        OpenAIResponseMapper mapper)
    {
        _options = options.Value;
        _chatClient = new ChatClient(model: _options.Model, apiKey: apiKey);
        _logger = logger;
        _mapper = mapper;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };
    }

    public async Task<MenuResponse> GenerateMenuAsync(MenuParameters parameters)
    {
        try
        {
            // Migrate legacy parameters if needed
            parameters = MenuParametersMigrationHelper.MigrateIfNeeded(parameters);

            if (!parameters.Configurations.Any())
            {
                throw new InvalidOperationException("No configurations defined");
            }

            var totalDishes = parameters.Configurations.Sum(c => c.NumberOfDishes);

            if (totalDishes == 0)
            {
                throw new InvalidOperationException("No dishes requested");
            }

            var maxDishesPerBatch = _options.MaxDishesPerBatch;
            var allRecipes = new List<RecipeResponse>();
            var generatedTitles = new List<string>();

            // Generate dishes for each configuration separately (each has its own parameters)
            foreach (var config in parameters.Configurations)
            {
                _logger.LogInformation(
                    "Generating {NumberOfDishes} dishes for {Servings} servings (already generated: {AlreadyGenerated})",
                    config.NumberOfDishes,
                    config.Servings,
                    generatedTitles.Count);

                // Build NumberOfDishesDto list for this configuration
                var dishCountsForConfig = new List<NumberOfDishesDto>
                {
                    new NumberOfDishesDto
                    {
                        NumberOfDishes = config.NumberOfDishes,
                        Servings = config.Servings
                    }
                };

                // If this config exceeds max batch size, split into batches
                if (config.NumberOfDishes <= maxDishesPerBatch)
                {
                    var configResponse = await GenerateMenuBatchAsync(
                        parameters,
                        config.Parameters,
                        dishCountsForConfig,
                        generatedTitles);

                    allRecipes.AddRange(configResponse.Recipes);
                    generatedTitles.AddRange(configResponse.Recipes.Select(r => r.Title));
                }
                else
                {
                    // Split into batches
                    var batches = DistributeDishesIntoBatches(dishCountsForConfig, maxDishesPerBatch);

                    foreach (var batch in batches)
                    {
                        var batchResponse = await GenerateMenuBatchAsync(
                            parameters,
                            config.Parameters,
                            batch,
                            generatedTitles);

                        allRecipes.AddRange(batchResponse.Recipes);
                        generatedTitles.AddRange(batchResponse.Recipes.Select(r => r.Title));
                    }
                }

                _logger.LogInformation(
                    "Configuration completed: {NumberOfDishes} dishes for {Servings} servings",
                    config.NumberOfDishes,
                    config.Servings);
            }

            // Vérifier qu'on a bien le nombre attendu
            if (allRecipes.Count != totalDishes)
            {
                _logger.LogError(
                    "Mismatch between requested and generated dishes. Expected {Expected} dishes but generated {Actual}. This likely indicates an AI generation issue; consider retrying menu generation or notifying the user.",
                    totalDishes,
                    allRecipes.Count);
            }

            return new MenuResponse
            {
                WeekStartDate = parameters.WeekStartDate,
                CreationDate = DateTime.UtcNow,
                Recipes = allRecipes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during menu generation");
            throw;
        }
    }

    private List<List<NumberOfDishesDto>> DistributeDishesIntoBatches(
        List<NumberOfDishesDto> numberOfDishes, 
        int maxDishesPerBatch)
    {
        var batches = new List<List<NumberOfDishesDto>>();
        var currentBatch = new List<NumberOfDishesDto>();
        var currentBatchTotal = 0;

        foreach (var dishGroup in numberOfDishes)
        {
            var remainingInGroup = dishGroup.NumberOfDishes;

            while (remainingInGroup > 0)
            {
                var availableInBatch = maxDishesPerBatch - currentBatchTotal;
                
                if (availableInBatch == 0)
                {
                    // Current batch is full, start a new one
                    batches.Add(currentBatch);
                    currentBatch = new List<NumberOfDishesDto>();
                    currentBatchTotal = 0;
                    availableInBatch = maxDishesPerBatch;
                }

                // Add as many dishes from this group as possible to the current batch
                var dishesToAdd = Math.Min(remainingInGroup, availableInBatch);
                currentBatch.Add(new NumberOfDishesDto
                {
                    NumberOfDishes = dishesToAdd,
                    Servings = dishGroup.Servings
                });
                currentBatchTotal += dishesToAdd;
                remainingInGroup -= dishesToAdd;
            }
        }

        // Add the last batch if it has any dishes
        if (currentBatch.Any())
        {
            batches.Add(currentBatch);
        }

        return batches;
    }

    private async Task<MenuResponse> GenerateMenuBatchAsync(
        MenuParameters globalParameters,
        DishConfigurationParametersDto configParams,
        List<NumberOfDishesDto> dishesToGenerate,
        List<string> generatedTitles)
    {
        var promptBuilder = new MenuPromptBuilder()
            .WithWeekStartDate(globalParameters.WeekStartDate)
            .WithDishCounts(dishesToGenerate)
            .WithAlreadyGeneratedTitles(generatedTitles)
            .WithDishTypes(configParams.DishTypes)
            .WithBannedFoods(configParams.BannedFoods)
            .WithDesiredFoods(configParams.DesiredFoods)
            .WithSeasonalFoods(globalParameters.SeasonalFoods, globalParameters.WeekStartDate)
            .WithCalorieConstraints(configParams.MinKcalPerDish, configParams.MaxKcalPerDish)
            .WithDietaryConstraints(configParams.WeightedOptions)
            .WithMaxPreparationTime(configParams.MaxPreparationTime)
            .WithMaxCookingTime(configParams.MaxCookingTime);

        var prompt = promptBuilder.Build();

        var systemMessage = "Tu es un expert en cuisine française qui génère des menus de la semaine. Tu réponds toujours en JSON valide, sans texte avant ou après le JSON.";

        _logger.LogInformation("Menu batch generation - System prompt: {SystemMessage}", systemMessage);
        _logger.LogInformation("Menu batch generation - User prompt: {Prompt}", prompt);

        var chatMessages = new ChatMessage[]
        {
            new SystemChatMessage(systemMessage),
            new UserChatMessage(prompt)
        };

        var options = new ChatCompletionOptions
        {
            Temperature = _options.Temperatures.Menu
        };

        var completion = await _chatClient.CompleteChatAsync(chatMessages, options);
        var completionValue = completion.Value;

        var jsonResponse = completionValue.Content[0].Text
            ?? throw new InvalidOperationException("Empty OpenAI response");

        jsonResponse = CleanJsonResponse(jsonResponse);

        _logger.LogInformation("OpenAI batch response received: {Length} characters", jsonResponse.Length);

        var menuData = JsonSerializer.Deserialize<MenuResponseDto>(jsonResponse, _jsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize OpenAI response");

        return _mapper.MapToMenuResponse(menuData, globalParameters.WeekStartDate);
    }

    public async Task<string> GenerateDetailedRecipeAsync(string recipeTitle, List<IngredientResponse> ingredients, string shortDescription)
    {
        try
        {
            var promptBuilder = new RecipePromptBuilder(recipeTitle, ingredients, shortDescription);
            var prompt = promptBuilder.Build();

            var systemMessage = "Tu es un chef cuisinier expert qui rédige des recettes détaillées et appétissantes en français.";
            
            _logger.LogInformation("Detailed recipe generation for {Title} - System prompt: {SystemMessage}", recipeTitle, systemMessage);
            _logger.LogInformation("Detailed recipe generation for {Title} - User prompt: {Prompt}", recipeTitle, prompt);
            
            var chatMessages = new ChatMessage[]
            {
                new SystemChatMessage(systemMessage),
                new UserChatMessage(prompt)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = _options.Temperatures.DetailedRecipe
            };

            var completion = await _chatClient.CompleteChatAsync(chatMessages, options);
            var completionValue = completion.Value;
            var completeRecipe = completionValue.Content[0].Text ?? throw new InvalidOperationException("Empty OpenAI response");

            return completeRecipe;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during detailed recipe generation for {Title}", recipeTitle);
            throw;
        }
    }

    public async Task<RecipeResponse> ReplaceRecipeAsync(MenuParameters parameters, RecipeResponse recipeToReplace)
    {
        try
        {
            // Migrate legacy parameters if needed
            parameters = MenuParametersMigrationHelper.MigrateIfNeeded(parameters);

            // Use the first configuration's parameters for recipe replacement
            // (In a future version, we could match the recipe to its original configuration)
            var configParams = parameters.Configurations.FirstOrDefault()?.Parameters
                ?? new DishConfigurationParametersDto();

            var promptBuilder = new RecipeReplacementPromptBuilder()
                .WithRecipeToReplace(recipeToReplace.Title, recipeToReplace.Description)
                .WithWeekStartDate(parameters.WeekStartDate)
                .WithSeasonalFoods(parameters.SeasonalFoods, parameters.WeekStartDate)
                .WithCalorieConstraints(configParams.MinKcalPerDish, configParams.MaxKcalPerDish)
                .WithBannedFoods(configParams.BannedFoods)
                .WithMaxPreparationTime(configParams.MaxPreparationTime)
                .WithMaxCookingTime(configParams.MaxCookingTime);

            var prompt = promptBuilder.Build();

            var systemMessage = "Tu es un expert en cuisine française qui génère des recettes. Tu réponds toujours en JSON valide, sans texte avant ou après le JSON.";
            
            _logger.LogInformation("Recipe replacement {Title} - System prompt: {SystemMessage}", recipeToReplace.Title, systemMessage);
            _logger.LogInformation("Recipe replacement {Title} - User prompt: {Prompt}", recipeToReplace.Title, prompt);
            
            var chatMessages = new ChatMessage[]
            {
                new SystemChatMessage(systemMessage),
                new UserChatMessage(prompt)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = _options.Temperatures.RecipeReplacement
            };

            var completion = await _chatClient.CompleteChatAsync(chatMessages, options);
            var completionValue = completion.Value;
            var jsonResponse = completionValue.Content[0].Text ?? throw new InvalidOperationException("Empty OpenAI response");
            
            jsonResponse = CleanJsonResponse(jsonResponse);
            
            var recipeData = JsonSerializer.Deserialize<RecipeResponseDto>(jsonResponse, _jsonOptions)
                ?? throw new InvalidOperationException("Unable to deserialize OpenAI response");

            return _mapper.MapToRecipeResponse(recipeData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during recipe replacement {Title}", recipeToReplace.Title);
            throw;
        }
    }

    public async Task<ShoppingListResponse> GenerateShoppingListAsync(List<RecipeResponse> recipes)
    {
        try
        {
            var promptBuilder = new ShoppingListPromptBuilder(recipes);
            var prompt = promptBuilder.Build();
            
            var systemMessage = "Tu es un assistant qui organise des listes de courses. Tu réponds toujours en JSON valide, sans texte avant ou après le JSON.";
            
            _logger.LogInformation("Shopping list generation - System prompt: {SystemMessage}", systemMessage);
            _logger.LogInformation("Shopping list generation - User prompt: {Prompt}", prompt);
            
            var chatMessages = new ChatMessage[]
            {
                new SystemChatMessage(systemMessage),
                new UserChatMessage(prompt)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = _options.Temperatures.ShoppingList
            };

            var completion = await _chatClient.CompleteChatAsync(chatMessages, options);
            var completionValue = completion.Value;
            var jsonResponse = completionValue.Content[0].Text ?? throw new InvalidOperationException("Empty OpenAI response");
            
            jsonResponse = CleanJsonResponse(jsonResponse);
            
            var listData = JsonSerializer.Deserialize<ShoppingListResponseDto>(jsonResponse, _jsonOptions)
                ?? throw new InvalidOperationException("Unable to deserialize OpenAI response");

            return _mapper.MapToShoppingListResponse(listData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during shopping list generation");
            throw;
        }
    }

    private static string CleanJsonResponse(string jsonResponse)
    {
        // Remove markdown code blocks if present
        jsonResponse = Regex.Replace(jsonResponse, @"^```json\s*", "", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        jsonResponse = Regex.Replace(jsonResponse, @"^```\s*", "", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        jsonResponse = Regex.Replace(jsonResponse, @"```\s*$", "", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        
        // Remove spaces at start and end
        jsonResponse = jsonResponse.Trim();
        
        return jsonResponse;
    }
}
