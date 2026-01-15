using Cuisinier.Shared.DTOs;
using Refit;

namespace Cuisinier.App.Services;

public interface IFamilyApi
{
    [Get("/api/family/status")]
    Task<FamilyStatusResponse> GetFamilyStatusAsync();

    [Post("/api/family/invite")]
    Task<FamilyLinkInvitationResponse> SendInvitationAsync([Body] SendFamilyInvitationRequest request);

    [Post("/api/family/accept")]
    Task<FamilyLinkResponse> AcceptInvitationAsync([Body] AcceptFamilyInvitationRequest request);

    [Post("/api/family/reject/{invitationId}")]
    Task RejectInvitationAsync(int invitationId);

    [Delete("/api/family/invitation/{invitationId}")]
    Task CancelInvitationAsync(int invitationId);

    [Delete("/api/family/link")]
    Task DeleteFamilyLinkAsync();
}
