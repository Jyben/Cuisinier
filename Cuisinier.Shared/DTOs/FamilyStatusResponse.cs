namespace Cuisinier.Shared.DTOs;

public class FamilyStatusResponse
{
    public FamilyLinkResponse? ActiveLink { get; set; }
    public FamilyLinkInvitationResponse? PendingSentInvitation { get; set; }
    public List<FamilyLinkInvitationResponse> PendingReceivedInvitations { get; set; } = new();
}
