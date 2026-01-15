using Cuisinier.Shared.DTOs;

namespace Cuisinier.Api.Services;

public interface IFamilyLinkService
{
    Task<FamilyStatusResponse> GetFamilyStatusAsync(string userId);
    Task<FamilyLinkInvitationResponse> SendInvitationAsync(string userId, string targetEmail);
    Task<FamilyLinkResponse> AcceptInvitationByTokenAsync(string userId, string token);
    Task<FamilyLinkResponse> AcceptInvitationByIdAsync(string userId, int invitationId);
    Task RejectInvitationAsync(string userId, int invitationId);
    Task CancelSentInvitationAsync(string userId, int invitationId);
    Task DeleteFamilyLinkAsync(string userId);
    Task<string?> GetLinkedUserIdAsync(string userId);
}
