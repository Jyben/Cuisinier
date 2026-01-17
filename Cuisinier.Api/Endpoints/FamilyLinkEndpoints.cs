using System.Security.Claims;
using Cuisinier.Api.Helpers;
using Cuisinier.Api.Services;
using Cuisinier.Shared.DTOs;

namespace Cuisinier.Api.Endpoints;

public static class FamilyLinkEndpoints
{
    public static void MapFamilyLinkEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/family");

        group.MapGet("/status", GetFamilyStatus)
            .WithName("GetFamilyStatus")
            .WithSummary("Get family link status")
            .WithDescription("Returns the current family link status including active link and pending invitations")
            .Produces<FamilyStatusResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

        group.MapPost("/invite", SendInvitation)
            .WithName("SendFamilyInvitation")
            .WithSummary("Send a family invitation")
            .WithDescription("Sends a family link invitation to another user by email")
            .Produces<FamilyLinkInvitationResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

        group.MapPost("/accept", AcceptInvitation)
            .WithName("AcceptFamilyInvitation")
            .WithSummary("Accept a family invitation")
            .WithDescription("Accepts a family link invitation using the token from the email")
            .Produces<FamilyLinkResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

        group.MapPost("/reject/{invitationId:int}", RejectInvitation)
            .WithName("RejectFamilyInvitation")
            .WithSummary("Reject a family invitation")
            .WithDescription("Rejects a received family link invitation")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

        group.MapDelete("/invitation/{invitationId:int}", CancelInvitation)
            .WithName("CancelFamilyInvitation")
            .WithSummary("Cancel a sent invitation")
            .WithDescription("Cancels a family link invitation that you sent")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

        group.MapDelete("/link", DeleteFamilyLink)
            .WithName("DeleteFamilyLink")
            .WithSummary("Delete family link")
            .WithDescription("Deletes the active family link (can be done by either user)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }

    private static async Task<IResult> GetFamilyStatus(
        ClaimsPrincipal user,
        IFamilyLinkService familyLinkService)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var status = await familyLinkService.GetFamilyStatusAsync(userId);
        return Results.Ok(status);
    }

    private static async Task<IResult> SendInvitation(
        SendFamilyInvitationRequest request,
        ClaimsPrincipal user,
        IFamilyLinkService familyLinkService)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var invitation = await familyLinkService.SendInvitationAsync(userId, request.Email);
        return Results.Ok(invitation);
    }

    private static async Task<IResult> AcceptInvitation(
        AcceptFamilyInvitationRequest request,
        ClaimsPrincipal user,
        IFamilyLinkService familyLinkService)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        FamilyLinkResponse link;
        if (!string.IsNullOrEmpty(request.Token))
        {
            link = await familyLinkService.AcceptInvitationByTokenAsync(userId, request.Token);
        }
        else if (request.InvitationId.HasValue)
        {
            link = await familyLinkService.AcceptInvitationByIdAsync(userId, request.InvitationId.Value);
        }
        else
        {
            throw new ArgumentException("Token ou InvitationId requis.");
        }
        return Results.Ok(link);
    }

    private static async Task<IResult> RejectInvitation(
        int invitationId,
        ClaimsPrincipal user,
        IFamilyLinkService familyLinkService)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        await familyLinkService.RejectInvitationAsync(userId, invitationId);
        return Results.NoContent();
    }

    private static async Task<IResult> CancelInvitation(
        int invitationId,
        ClaimsPrincipal user,
        IFamilyLinkService familyLinkService)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        await familyLinkService.CancelSentInvitationAsync(userId, invitationId);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteFamilyLink(
        ClaimsPrincipal user,
        IFamilyLinkService familyLinkService)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        await familyLinkService.DeleteFamilyLinkAsync(userId);
        return Results.NoContent();
    }
}
