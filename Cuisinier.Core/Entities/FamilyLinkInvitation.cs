namespace Cuisinier.Core.Entities;

public class FamilyLinkInvitation
{
    public int Id { get; set; }
    public string InviterUserId { get; set; } = string.Empty;
    public ApplicationUser? InviterUser { get; set; }
    public string InvitedEmail { get; set; } = string.Empty;
    public string? InvitedUserId { get; set; }
    public ApplicationUser? InvitedUser { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public bool IsActive => AcceptedAt == null && RejectedAt == null
        && CancelledAt == null && ExpiresAt > DateTime.UtcNow;
}
