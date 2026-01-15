namespace Cuisinier.Shared.DTOs;

public class AcceptFamilyInvitationRequest
{
    /// <summary>
    /// Token from email link (for accepting via email)
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Invitation ID (for accepting via UI when user is logged in)
    /// </summary>
    public int? InvitationId { get; set; }
}
