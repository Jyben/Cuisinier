namespace Cuisinier.Shared.DTOs;

public class FamilyLinkInvitationResponse
{
    public int Id { get; set; }
    public string InviterUserId { get; set; } = string.Empty;
    public string InviterEmail { get; set; } = string.Empty;
    public string InviterUserName { get; set; } = string.Empty;
    public string InvitedEmail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Status { get; set; } = string.Empty;
}
