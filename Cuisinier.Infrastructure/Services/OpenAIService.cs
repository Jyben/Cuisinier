using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Cuisinier.Core.DTOs;
using OpenAI.Chat;
using OpenAI.Images;

namespace Cuisinier.Infrastructure.Services;

public class OpenAIService : IOpenAIService
{
    private readonly ChatClient _chatClient;
    private readonly ImageClient _imageClient;
    private readonly ILogger<OpenAIService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public OpenAIService(string apiKey, ILogger<OpenAIService> logger)
    {
        _chatClient = new ChatClient(model: "gpt-4o-mini", apiKey: apiKey);
        _imageClient = new ImageClient(model: "dall-e-3", apiKey: apiKey);
        _logger = logger;
        
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
            var prompt = BuildMenuPrompt(parameters);
            
            var systemMessage = "Tu es un expert en cuisine française qui génère des menus de la semaine. Tu réponds toujours en JSON valide, sans texte avant ou après le JSON.";
            
            _logger.LogInformation("Menu generation - System prompt: {SystemMessage}", systemMessage);
            _logger.LogInformation("Menu generation - User prompt: {Prompt}", prompt);
            
            var chatMessages = new ChatMessage[]
            {
                new SystemChatMessage(systemMessage),
                new UserChatMessage(prompt)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.7f
            };

            var completion = await _chatClient.CompleteChatAsync(chatMessages, options);
            var completionValue = completion.Value;
            
            // Access response content
            var jsonResponse = completionValue.Content[0].Text ?? throw new InvalidOperationException("Empty OpenAI response");
            
            // Clean JSON response (remove markdown code blocks if present)
            jsonResponse = CleanJsonResponse(jsonResponse);
            
            _logger.LogInformation("OpenAI response received: {Length} characters", jsonResponse.Length);
            
            var menuData = JsonSerializer.Deserialize<MenuResponseDto>(jsonResponse, _jsonOptions)
                ?? throw new InvalidOperationException("Unable to deserialize OpenAI response");

            var menuResponse = new MenuResponse
            {
                WeekStartDate = parameters.WeekStartDate,
                CreationDate = DateTime.UtcNow,
                Recipes = menuData.Recettes.Select(r => new RecipeResponse
                {
                    Title = r.Titre,
                    Description = r.Description,
                    PreparationTime = ParseTimeSpan(r.TempsPreparation),
                    CookingTime = ParseTimeSpan(r.TempsCuisson),
                    Kcal = r.Kcal,
                    Servings = r.Personnes,
                    Ingredients = r.Ingredients.Select(i => new IngredientResponse
                    {
                        Name = i.Nom,
                        Quantity = i.Quantite ?? "",
                        Category = i.Categorie ?? ""
                    }).ToList()
                }).ToList()
            };

            return menuResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during menu generation");
            throw;
        }
    }

    public async Task<string> GenerateDetailedRecipeAsync(string recipeTitle, List<IngredientResponse> ingredients, string shortDescription)
    {
        try
        {
            var ingredientsList = string.Join("\n", ingredients.Select(i => $"- {i.Name}: {i.Quantity}"));
            
            var prompt = $@"Génère une recette complète et détaillée pour le plat suivant :

Titre : {recipeTitle}
Description : {shortDescription}

Ingrédients disponibles (liste COMPLÈTE et OBLIGATOIRE à utiliser) :
{ingredientsList}

Génère une recette détaillée avec :
1. Une introduction (2-3 phrases)
2. Les étapes de préparation numérotées et détaillées
3. Des conseils de cuisson si nécessaire
4. Des suggestions de présentation

IMPORTANT - CONTRAINTES OBLIGATOIRES : 
- N'inclus PAS le titre du plat car il est déjà affiché ailleurs.
- N'inclus PAS la liste des ingrédients car elle est déjà affichée ailleurs.
- Commence directement par l'introduction sans répéter le titre.
- Tu DOIS utiliser UNIQUEMENT les ingrédients listés ci-dessus. N'ajoute AUCUN ingrédient qui ne figure pas dans cette liste.
- La recette doit être cohérente avec les ingrédients fournis. Si un ingrédient est mentionné dans la liste, il DOIT être utilisé dans les étapes de préparation.

Rédige la recette de manière claire, pédagogique et appétissante. Utilise un ton chaleureux et convivial. 
Formate la réponse en Markdown avec des titres (##, ###), des listes à puces (-) et des listes numérotées (1., 2., etc.).";

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
                Temperature = 0.8f
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

    public async Task<string> GenerateDishImageAsync(string dishTitle, string description)
    {
        try
        {
            var imagePrompt = $"Plat culinaire français: {dishTitle}. {description}. Photo de qualité professionnelle, éclairage naturel, style moderne et appétissant, fond neutre.";

            _logger.LogInformation("Image generation for {Title} - Prompt: {Prompt}", dishTitle, imagePrompt);

            // Use default options for now
            // Note: The exact structure of OpenAI.NET 2.8.0 API may require adjustments
            var imageResult = await _imageClient.GenerateImageAsync(imagePrompt);
            var image = imageResult.Value;
            
            // Try different ways to access URL according to API structure
            // This part may require adjustments according to exact OpenAI.NET 2.8.0 documentation
            string? imageUrl = null;
            
            // Try to access via different possible properties
            var imageType = image.GetType();
            var urlProperty = imageType.GetProperty("Url") ?? imageType.GetProperty("UrlString") ?? imageType.GetProperty("B64Json");
            if (urlProperty != null && urlProperty.GetValue(image) is string url)
            {
                imageUrl = url;
            }
            else if (imageType.GetProperty("Items")?.GetValue(image) is System.Collections.IEnumerable items)
            {
                var firstItem = items.Cast<object>().FirstOrDefault();
                if (firstItem != null)
                {
                    var itemType = firstItem.GetType();
                    var itemUrlProperty = itemType.GetProperty("Url") ?? itemType.GetProperty("UrlString");
                    imageUrl = itemUrlProperty?.GetValue(firstItem)?.ToString();
                }
            }
            
            if (string.IsNullOrEmpty(imageUrl))
            {
                throw new InvalidOperationException("Unable to extract URL from generated image. Check OpenAI.NET 2.8.0 API structure.");
            }

            return imageUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during image generation for {Title}", dishTitle);
            throw;
        }
    }

    public async Task<RecipeResponse> ReplaceRecipeAsync(MenuParameters parameters, RecipeResponse recipeToReplace)
    {
        try
        {
            var prompt = BuildReplacementPrompt(parameters, recipeToReplace);

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
                Temperature = 0.8f
            };

            var completion = await _chatClient.CompleteChatAsync(chatMessages, options);
            var completionValue = completion.Value;
            var jsonResponse = completionValue.Content[0].Text ?? throw new InvalidOperationException("Empty OpenAI response");
            
            jsonResponse = CleanJsonResponse(jsonResponse);
            
            var recipeData = JsonSerializer.Deserialize<RecipeResponseDto>(jsonResponse, _jsonOptions)
                ?? throw new InvalidOperationException("Unable to deserialize OpenAI response");

            var newRecipe = new RecipeResponse
            {
                Title = recipeData.Titre,
                Description = recipeData.Description,
                PreparationTime = ParseTimeSpan(recipeData.TempsPreparation),
                CookingTime = ParseTimeSpan(recipeData.TempsCuisson),
                Kcal = recipeData.Kcal,
                Servings = recipeData.Personnes,
                Ingredients = recipeData.Ingredients.Select(i => new IngredientResponse
                {
                    Name = i.Nom,
                    Quantity = i.Quantite ?? "",
                    Category = i.Categorie ?? ""
                }).ToList()
            };

            return newRecipe;
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
            var sb = new StringBuilder();
            sb.AppendLine("Génère une liste de courses organisée par catégories à partir des recettes suivantes :\n");

            foreach (var recipe in recipes)
            {
                sb.AppendLine($"\n{recipe.Title} ({recipe.Servings} personnes):");
                foreach (var ingredient in recipe.Ingredients)
                {
                    sb.AppendLine($"- {ingredient.Name}: {ingredient.Quantity}");
                }
            }

            sb.AppendLine("\n\nGénère une liste de courses organisée par catégories (Légumes, Fruits, Viandes, Poissons, Produits laitiers, Épicerie, etc.) en regroupant les ingrédients similaires et en additionnant les quantités quand c'est pertinent.");
            sb.AppendLine("\nRéponds en JSON avec cette structure :");
            sb.AppendLine(@"{
  ""items"": [
    {
      ""nom"": ""Nom de l'ingrédient"",
      ""quantite"": ""Quantité totale"",
      ""categorie"": ""Catégorie""
    }
  ]
}");

            var prompt = sb.ToString();
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
                Temperature = 0.3f
            };

            var completion = await _chatClient.CompleteChatAsync(chatMessages, options);
            var completionValue = completion.Value;
            var jsonResponse = completionValue.Content[0].Text ?? throw new InvalidOperationException("Empty OpenAI response");
            
            jsonResponse = CleanJsonResponse(jsonResponse);
            
            var listData = JsonSerializer.Deserialize<ShoppingListResponseDto>(jsonResponse, _jsonOptions)
                ?? throw new InvalidOperationException("Unable to deserialize OpenAI response");

            var shoppingList = new ShoppingListResponse
            {
                CreationDate = DateTime.UtcNow,
                Items = listData.Items.Select(i => new ShoppingListItemResponse
                {
                    Name = i.Nom,
                    Quantity = i.Quantite ?? "",
                    Category = i.Categorie ?? "",
                    IsManuallyAdded = false
                }).ToList()
            };

            return shoppingList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during shopping list generation");
            throw;
        }
    }

    private string BuildMenuPrompt(MenuParameters parameters)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Génère un menu de la semaine avec les paramètres suivants:");
        sb.AppendLine($"Date de début de semaine: {parameters.WeekStartDate:yyyy-MM-dd}");
        
        var totalDishes = parameters.NumberOfDishes.Any() ? parameters.NumberOfDishes.Sum(item => item.NumberOfDishes) : 0;
        
        if (parameters.NumberOfDishes.Any())
        {
            sb.AppendLine("\nNombre de plats:");
            foreach (var item in parameters.NumberOfDishes)
            {
                sb.AppendLine($"- {item.NumberOfDishes} plats pour {item.Servings} personne(s)");
            }
            sb.AppendLine($"\nIMPORTANT: Tu DOIS générer EXACTEMENT {totalDishes} plat(s) au total dans le tableau 'recettes'. C'est une contrainte stricte et obligatoire.");
        }
        
        if (parameters.DishTypes.Any())
        {
            sb.AppendLine("\nTypes de plats souhaités:");
            foreach (var (type, number) in parameters.DishTypes)
            {
                if (number.HasValue)
                {
                    sb.AppendLine($"- {type}: {number.Value} fois");
                }
                else
                {
                    sb.AppendLine($"- {type} (optionnel, peut être inclus dans le menu)");
                }
            }
        }
        
        if (parameters.BannedFoods.Any())
        {
            sb.AppendLine($"\nAliments à bannir: {string.Join(", ", parameters.BannedFoods)}");
        }
        
        if (parameters.DesiredFoods.Any())
        {
            sb.AppendLine("\nAliments souhaités:");
            foreach (var item in parameters.DesiredFoods)
            {
                sb.AppendLine($"- {item.Food}");
            }
            sb.AppendLine("Ces aliments peuvent apparaître dans une ou plusieurs recettes du menu.");
        }
        
        sb.AppendLine($"\nAliments de saison: {(parameters.SeasonalFoods ? "Oui" : "Non")}");
        
        if (parameters.MinKcalPerDish.HasValue || parameters.MaxKcalPerDish.HasValue)
        {
            if (parameters.MinKcalPerDish.HasValue && parameters.MaxKcalPerDish.HasValue)
            {
                sb.AppendLine($"\nCalories par plat: entre {parameters.MinKcalPerDish.Value} et {parameters.MaxKcalPerDish.Value} kcal");
            }
            else if (parameters.MinKcalPerDish.HasValue)
            {
                sb.AppendLine($"\nCalories minimales par plat: {parameters.MinKcalPerDish.Value} kcal");
            }
            else if (parameters.MaxKcalPerDish.HasValue)
            {
                sb.AppendLine($"\nCalories maximales par plat: {parameters.MaxKcalPerDish.Value} kcal");
            }
        }
        
        if (parameters.WeightedOptions.Any(kvp => kvp.Value.HasValue))
        {
            // Separate options with intensity and boolean options
            var optionsWithIntensity = new[] { "Équilibré", "Gourmand" };
            var booleanOptions = new[] { "Végan", "Végétarien", "Sans gluten", "Sans lactose" };
            
            var intensityOptions = parameters.WeightedOptions
                .Where(kvp => kvp.Value.HasValue && optionsWithIntensity.Contains(kvp.Key))
                .ToList();
            var boolOptions = parameters.WeightedOptions
                .Where(kvp => kvp.Value.HasValue && kvp.Value.Value > 0 && booleanOptions.Contains(kvp.Key))
                .ToList();
            
            if (intensityOptions.Any())
            {
                sb.AppendLine("\nOptions avec intensité:");
                foreach (var (option, value) in intensityOptions)
                {
                    if (value == 0)
                    {
                        sb.AppendLine($"- {option}: aucun (0%)");
                    }
                    else
                    {
                        sb.AppendLine($"- {option}: {value}%");
                    }
                }
            }
            
            if (boolOptions.Any())
            {
                sb.AppendLine("\nContraintes alimentaires (obligatoires pour tous les plats):");
                foreach (var (option, _) in boolOptions)
                {
                    sb.AppendLine($"- {option}");
                }
            }
        }
        
        if (parameters.MaxPreparationTime.HasValue)
        {
            sb.AppendLine($"\nTemps de préparation maximum: {parameters.MaxPreparationTime.Value.TotalMinutes} minutes");
        }
        
        if (parameters.MaxCookingTime.HasValue)
        {
            sb.AppendLine($"Temps de cuisson maximum: {parameters.MaxCookingTime.Value.TotalMinutes} minutes");
        }
        
        sb.AppendLine("\nGénère une réponse JSON avec cette structure:");
        sb.AppendLine(@"{
  ""recettes"": [
    {
      ""titre"": ""Titre du plat"",
      ""description"": ""Description courte"",
      ""tempsPreparation"": ""00:30:00"",
      ""tempsCuisson"": ""01:00:00"",
      ""kcal"": 450,
      ""personnes"": 4,
      ""ingredients"": [
        {
          ""nom"": ""Nom de l'ingrédient"",
          ""quantite"": ""200g"",
          ""categorie"": ""Légumes""
        }
      ]
    }
  ]
}");
        sb.AppendLine($"\nIMPORTANT - CONTRAINTES OBLIGATOIRES:");
        if (totalDishes > 0)
        {
            sb.AppendLine($"- Le tableau 'recettes' DOIT contenir EXACTEMENT {totalDishes} recette(s). C'est une contrainte stricte et non négociable.");
        }
        sb.AppendLine("- Tu NE DOIS PAS générer de desserts (tartes, gâteaux, crèmes, glaces, fruits au sirop, etc.). Uniquement des plats principaux et entrées.");
        sb.AppendLine("- Pour chaque recette, tu DOIS fournir une liste COMPLÈTE et DÉTAILLÉE de TOUS les ingrédients nécessaires pour réaliser le plat. N'omets aucun ingrédient important (viande, poisson, légumes, épices, condiments, produits laitiers, etc.). La liste doit être exhaustive et réaliste.");
        sb.AppendLine("- Pour chaque recette, tu DOIS fournir le nombre total de calories (kcal) du plat. Calcule les calories en fonction des ingrédients et de leurs quantités.");
        if (parameters.MinKcalPerDish.HasValue || parameters.MaxKcalPerDish.HasValue)
        {
            if (parameters.MinKcalPerDish.HasValue && parameters.MaxKcalPerDish.HasValue)
            {
                sb.AppendLine($"- Chaque recette DOIT avoir entre {parameters.MinKcalPerDish.Value} et {parameters.MaxKcalPerDish.Value} kcal. C'est une contrainte stricte.");
            }
            else if (parameters.MinKcalPerDish.HasValue)
            {
                sb.AppendLine($"- Chaque recette DOIT avoir au minimum {parameters.MinKcalPerDish.Value} kcal. C'est une contrainte stricte.");
            }
            else if (parameters.MaxKcalPerDish.HasValue)
            {
                sb.AppendLine($"- Chaque recette DOIT avoir au maximum {parameters.MaxKcalPerDish.Value} kcal. C'est une contrainte stricte.");
            }
        }
        
        return sb.ToString();
    }

    private string BuildReplacementPrompt(MenuParameters parameters, RecipeResponse recipeToReplace)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Génère une nouvelle recette pour remplacer: {recipeToReplace.Title}");
        sb.AppendLine($"Description actuelle: {recipeToReplace.Description}");
        
        // Reuse constraints from original menu
        if (parameters.MaxPreparationTime.HasValue)
        {
            sb.AppendLine($"Temps de préparation maximum: {parameters.MaxPreparationTime.Value.TotalMinutes} minutes");
        }
        
        if (parameters.MaxCookingTime.HasValue)
        {
            sb.AppendLine($"Temps de cuisson maximum: {parameters.MaxCookingTime.Value.TotalMinutes} minutes");
        }
        
        if (parameters.MinKcalPerDish.HasValue || parameters.MaxKcalPerDish.HasValue)
        {
            if (parameters.MinKcalPerDish.HasValue && parameters.MaxKcalPerDish.HasValue)
            {
                sb.AppendLine($"Calories par plat: entre {parameters.MinKcalPerDish.Value} et {parameters.MaxKcalPerDish.Value} kcal");
            }
            else if (parameters.MinKcalPerDish.HasValue)
            {
                sb.AppendLine($"Calories minimales par plat: {parameters.MinKcalPerDish.Value} kcal");
            }
            else if (parameters.MaxKcalPerDish.HasValue)
            {
                sb.AppendLine($"Calories maximales par plat: {parameters.MaxKcalPerDish.Value} kcal");
            }
        }
        
        if (parameters.BannedFoods.Any())
        {
            sb.AppendLine($"Aliments à bannir: {string.Join(", ", parameters.BannedFoods)}");
        }
        
        sb.AppendLine("\nIMPORTANT: Tu DOIS générer UNIQUEMENT UN SEUL plat (pas un tableau, pas plusieurs plats).");
        sb.AppendLine("\nGénère une nouvelle recette similaire mais différente, en JSON avec la structure suivante (un objet unique, pas un tableau) :");
        sb.AppendLine(@"{
  ""titre"": ""Titre du plat"",
  ""description"": ""Description courte"",
  ""tempsPreparation"": ""00:30:00"",
  ""tempsCuisson"": ""01:00:00"",
  ""kcal"": 450,
  ""personnes"": 4,
  ""ingredients"": [
    {
      ""nom"": ""Nom de l'ingrédient"",
      ""quantite"": ""200g"",
      ""categorie"": ""Légumes""
    }
  ]
}");
        sb.AppendLine("\nIMPORTANT - CONTRAINTES OBLIGATOIRES:");
        sb.AppendLine("- Tu DOIS générer UNIQUEMENT UN SEUL plat (un objet JSON unique, pas un tableau).");
        sb.AppendLine("- Tu DOIS fournir le nombre total de calories (kcal) du plat. Calcule les calories en fonction des ingrédients et de leurs quantités.");
        sb.AppendLine("- Tu NE DOIS PAS générer de desserts (tartes, gâteaux, crèmes, glaces, fruits au sirop, etc.). Uniquement des plats principaux et entrées.");
        sb.AppendLine("- Tu DOIS fournir une liste COMPLÈTE et DÉTAILLÉE de TOUS les ingrédients nécessaires pour réaliser le plat. N'omets aucun ingrédient important.");
        
        return sb.ToString();
    }

    private string CleanJsonResponse(string jsonResponse)
    {
        // Remove markdown code blocks if present
        jsonResponse = Regex.Replace(jsonResponse, @"^```json\s*", "", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        jsonResponse = Regex.Replace(jsonResponse, @"^```\s*", "", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        jsonResponse = Regex.Replace(jsonResponse, @"```\s*$", "", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        
        // Remove spaces at start and end
        jsonResponse = jsonResponse.Trim();
        
        return jsonResponse;
    }

    private TimeSpan? ParseTimeSpan(string? timeSpanString)
    {
        if (string.IsNullOrWhiteSpace(timeSpanString))
            return null;

        // Expected format: "HH:mm:ss" or "HH:mm"
        if (TimeSpan.TryParse(timeSpanString, out var result))
            return result;

        // Try to manually parse "HH:mm:ss" format
        var parts = timeSpanString.Split(':');
        if (parts.Length >= 2 && int.TryParse(parts[0], out var hours) && int.TryParse(parts[1], out var minutes))
        {
            var seconds = parts.Length > 2 && int.TryParse(parts[2], out var s) ? s : 0;
            return new TimeSpan(hours, minutes, seconds);
        }

        return null;
    }

    // DTOs for deserialization
    // Note: Property names must remain in French to match JSON keys from OpenAI responses
    private class MenuResponseDto
    {
        public List<RecipeResponseDto> Recettes { get; set; } = new();
    }

    private class RecipeResponseDto
    {
        public string Titre { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? TempsPreparation { get; set; }
        public string? TempsCuisson { get; set; }
        public int? Kcal { get; set; }
        public int Personnes { get; set; }
        public List<IngredientResponseDto> Ingredients { get; set; } = new();
    }

    private class IngredientResponseDto
    {
        public string Nom { get; set; } = string.Empty;
        public string? Quantite { get; set; }
        public string? Categorie { get; set; }
    }

    private class ShoppingListResponseDto
    {
        public List<ShoppingListItemResponseDto> Items { get; set; } = new();
    }

    private class ShoppingListItemResponseDto
    {
        public string Nom { get; set; } = string.Empty;
        public string? Quantite { get; set; }
        public string? Categorie { get; set; }
    }
}
