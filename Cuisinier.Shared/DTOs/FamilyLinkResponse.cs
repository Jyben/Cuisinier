namespace Cuisinier.Shared.DTOs;

public class FamilyLinkResponse
{
    public int Id { get; set; }
    public string LinkedUserId { get; set; } = string.Empty;
    public string LinkedUserEmail { get; set; } = string.Empty;
    public string LinkedUserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
