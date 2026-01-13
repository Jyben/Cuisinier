using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Cuisinier.Shared.DTOs;
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
            var totalDishes = parameters.NumberOfDishes.Any() 
                ? parameters.NumberOfDishes.Sum(item => item.NumberOfDishes) 
                : 0;

            if (totalDishes == 0)
            {
                throw new InvalidOperationException("No dishes requested");
            }

            var maxDishesPerBatch = _options.MaxDishesPerBatch;
            var allRecipes = new List<RecipeResponse>();

            // Si le nombre total est <= maxDishesPerBatch, on fait un seul appel
            if (totalDishes <= maxDishesPerBatch)
            {
                var menuResponse = await GenerateMenuBatchAsync(parameters, parameters.NumberOfDishes, new List<string>());
                return menuResponse;
            }

            // Sinon, on découpe en plusieurs appels en préservant la distribution des portions
            var batches = DistributeDishesIntoBatches(parameters.NumberOfDishes, maxDishesPerBatch);
            var generatedTitles = new List<string>();

            foreach (var batch in batches)
            {
                var dishesInThisBatch = batch.Sum(d => d.NumberOfDishes);
                
                _logger.LogInformation(
                    "Generating batch: {DishesInBatch} dishes (already generated: {AlreadyGenerated})",
                    dishesInThisBatch,
                    generatedTitles.Count);

                var batchResponse = await GenerateMenuBatchAsync(parameters, batch, generatedTitles);
                
                allRecipes.AddRange(batchResponse.Recipes);
                
                // Ajouter les titres générés pour éviter les doublons
                generatedTitles.AddRange(batchResponse.Recipes.Select(r => r.Title));
                
                _logger.LogInformation(
                    "Batch completed: {Generated} dishes generated",
                    batchResponse.Recipes.Count);
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
        MenuParameters parameters, 
        List<NumberOfDishesDto> dishesToGenerate, 
        List<string> generatedTitles)
    {
        var promptBuilder = new MenuPromptBuilder()
            .WithWeekStartDate(parameters.WeekStartDate)
            .WithDishCounts(dishesToGenerate)
            .WithAlreadyGeneratedTitles(generatedTitles)
            .WithDishTypes(parameters.DishTypes)
            .WithBannedFoods(parameters.BannedFoods)
            .WithDesiredFoods(parameters.DesiredFoods)
            .WithSeasonalFoods(parameters.SeasonalFoods, parameters.WeekStartDate)
            .WithCalorieConstraints(parameters.MinKcalPerDish, parameters.MaxKcalPerDish)
            .WithDietaryConstraints(parameters.WeightedOptions)
            .WithMaxPreparationTime(parameters.MaxPreparationTime)
            .WithMaxCookingTime(parameters.MaxCookingTime);
        
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

        return _mapper.MapToMenuResponse(menuData, parameters.WeekStartDate);
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
            var promptBuilder = new RecipeReplacementPromptBuilder()
                .WithRecipeToReplace(recipeToReplace.Title, recipeToReplace.Description)
                .WithWeekStartDate(parameters.WeekStartDate)
                .WithSeasonalFoods(parameters.SeasonalFoods, parameters.WeekStartDate)
                .WithCalorieConstraints(parameters.MinKcalPerDish, parameters.MaxKcalPerDish)
                .WithBannedFoods(parameters.BannedFoods)
                .WithMaxPreparationTime(parameters.MaxPreparationTime)
                .WithMaxCookingTime(parameters.MaxCookingTime);
            
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
