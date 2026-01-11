namespace Cuisinier.Shared.DTOs;

public class CheckDuplicatesBatchResponse
{
    public List<RecipeDuplicateMatch> Matches { get; set; } = new();
}

public class RecipeDuplicateMatch
{
    public int RecipeId { get; set; }
    public int FavoriteId { get; set; }
}
