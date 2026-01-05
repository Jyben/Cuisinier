namespace Cuisinier.Core.DTOs;

public class MenuGenerationResponse
{
    public int MenuId { get; set; }
    public string Status { get; set; } = "generating"; // "generating", "completed", "error"
}
