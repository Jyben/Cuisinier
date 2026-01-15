using System.Security.Cryptography;
using Cuisinier.Core.Entities;
using Cuisinier.Infrastructure.Data;
using Cuisinier.Infrastructure.Services;
using Cuisinier.Shared.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cuisinier.Api.Services;

public class FamilyLinkService : IFamilyLinkService
{
    private readonly CuisinierDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IUserAccessService _userAccessService;
    private readonly ILogger<FamilyLinkService> _logger;
    private static readonly TimeSpan InvitationExpiration = TimeSpan.FromDays(7);

    public FamilyLinkService(
        CuisinierDbContext context,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IUserAccessService userAccessService,
        ILogger<FamilyLinkService> logger)
    {
        _context = context;
        _userManager = userManager;
        _emailService = emailService;
        _userAccessService = userAccessService;
        _logger = logger;
    }

    public async Task<FamilyStatusResponse> GetFamilyStatusAsync(string userId)
    {
        var response = new FamilyStatusResponse();

        // Get active link
        var link = await _context.FamilyLinks
            .Include(fl => fl.User1)
            .Include(fl => fl.User2)
            .FirstOrDefaultAsync(fl => fl.User1Id == userId || fl.User2Id == userId);

        if (link != null)
        {
            var linkedUser = link.User1Id == userId ? link.User2 : link.User1;
            response.ActiveLink = new FamilyLinkResponse
            {
                Id = link.Id,
                LinkedUserId = linkedUser?.Id ?? string.Empty,
                LinkedUserEmail = linkedUser?.Email ?? string.Empty,
                LinkedUserName = linkedUser?.UserName ?? string.Empty,
                CreatedAt = link.CreatedAt
            };
        }

        // Get pending sent invitation (only one at a time)
        var sentInvitation = await _context.FamilyLinkInvitations
            .Include(i => i.InviterUser)
            .Where(i => i.InviterUserId == userId && i.AcceptedAt == null && i.RejectedAt == null && i.CancelledAt == null && i.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync();

        if (sentInvitation != null)
        {
            response.PendingSentInvitation = MapToInvitationResponse(sentInvitation, isSent: true);
        }

        // Get pending received invitations
        var user = await _userManager.FindByIdAsync(userId);
        if (user?.Email != null)
        {
            var receivedInvitations = await _context.FamilyLinkInvitations
                .Include(i => i.InviterUser)
                .Where(i => i.InvitedEmail.ToLower() == user.Email.ToLower() &&
                           i.AcceptedAt == null && i.RejectedAt == null && i.CancelledAt == null &&
                           i.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            response.PendingReceivedInvitations = receivedInvitations
                .Select(i => MapToInvitationResponse(i, isSent: false))
                .ToList();
        }

        return response;
    }

    public async Task<FamilyLinkInvitationResponse> SendInvitationAsync(string userId, string targetEmail)
    {
        _logger.LogInformation("User {UserId} sending family invitation to {Email}", userId, targetEmail);

        var inviter = await _userManager.FindByIdAsync(userId);
        if (inviter == null)
        {
            throw new InvalidOperationException("Utilisateur non trouvé");
        }

        // Validate inviter doesn't already have a link
        var existingLink = await _context.FamilyLinks
            .AnyAsync(fl => fl.User1Id == userId || fl.User2Id == userId);
        if (existingLink)
        {
            throw new InvalidOperationException("Vous avez déjà un lien famille actif. Supprimez-le d'abord pour en créer un nouveau.");
        }

        // Validate not inviting self
        if (inviter.Email?.Equals(targetEmail, StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new InvalidOperationException("Vous ne pouvez pas vous inviter vous-même.");
        }

        // Find target user
        var targetUser = await _userManager.FindByEmailAsync(targetEmail);
        if (targetUser == null)
        {
            throw new InvalidOperationException("Aucun compte Cuisinier n'existe avec cette adresse email.");
        }

        // Validate target doesn't already have a link
        var targetHasLink = await _context.FamilyLinks
            .AnyAsync(fl => fl.User1Id == targetUser.Id || fl.User2Id == targetUser.Id);
        if (targetHasLink)
        {
            throw new InvalidOperationException("Cet utilisateur a déjà un lien famille actif.");
        }

        // Cancel any existing pending invitation from this user
        var existingInvitations = await _context.FamilyLinkInvitations
            .Where(i => i.InviterUserId == userId && i.AcceptedAt == null && i.RejectedAt == null && i.CancelledAt == null)
            .ToListAsync();
        foreach (var existing in existingInvitations)
        {
            existing.CancelledAt = DateTime.UtcNow;
        }

        // Generate secure token
        var token = GenerateSecureToken();

        // Create invitation
        var invitation = new FamilyLinkInvitation
        {
            InviterUserId = userId,
            InvitedEmail = targetEmail,
            InvitedUserId = targetUser.Id,
            Token = HashToken(token),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(InvitationExpiration)
        };

        _context.FamilyLinkInvitations.Add(invitation);
        await _context.SaveChangesAsync();

        // Send email
        await _emailService.SendFamilyInvitationEmailAsync(
            targetEmail,
            inviter.UserName ?? inviter.Email ?? "Un utilisateur",
            inviter.Email ?? string.Empty,
            token);

        _logger.LogInformation("Family invitation sent from {InviterId} to {TargetEmail}", userId, targetEmail);

        return new FamilyLinkInvitationResponse
        {
            Id = invitation.Id,
            InviterUserId = userId,
            InviterEmail = inviter.Email ?? string.Empty,
            InviterUserName = inviter.UserName ?? string.Empty,
            InvitedEmail = targetEmail,
            CreatedAt = invitation.CreatedAt,
            ExpiresAt = invitation.ExpiresAt,
            Status = "pending"
        };
    }

    public async Task<FamilyLinkResponse> AcceptInvitationByTokenAsync(string userId, string token)
    {
        _logger.LogInformation("User {UserId} accepting family invitation by token", userId);

        var hashedToken = HashToken(token);
        var invitation = await _context.FamilyLinkInvitations
            .Include(i => i.InviterUser)
            .FirstOrDefaultAsync(i => i.Token == hashedToken);

        if (invitation == null)
        {
            throw new InvalidOperationException("Invitation non trouvée ou invalide.");
        }

        return await AcceptInvitationInternal(userId, invitation);
    }

    public async Task<FamilyLinkResponse> AcceptInvitationByIdAsync(string userId, int invitationId)
    {
        _logger.LogInformation("User {UserId} accepting family invitation {InvitationId}", userId, invitationId);

        var user = await _userManager.FindByIdAsync(userId);
        if (user?.Email == null)
        {
            throw new InvalidOperationException("Utilisateur non trouvé.");
        }

        var invitation = await _context.FamilyLinkInvitations
            .Include(i => i.InviterUser)
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.InvitedEmail.ToLower() == user.Email.ToLower());

        if (invitation == null)
        {
            throw new InvalidOperationException("Invitation non trouvée ou vous n'êtes pas le destinataire.");
        }

        return await AcceptInvitationInternal(userId, invitation);
    }

    private async Task<FamilyLinkResponse> AcceptInvitationInternal(string userId, FamilyLinkInvitation invitation)
    {
        if (!invitation.IsActive)
        {
            throw new InvalidOperationException("Cette invitation a expiré ou n'est plus valide.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user?.Email?.Equals(invitation.InvitedEmail, StringComparison.OrdinalIgnoreCase) != true)
        {
            throw new InvalidOperationException("Cette invitation n'est pas destinée à votre compte.");
        }

        // Verify neither user has an existing link
        var inviterHasLink = await _context.FamilyLinks
            .AnyAsync(fl => fl.User1Id == invitation.InviterUserId || fl.User2Id == invitation.InviterUserId);
        var accepterHasLink = await _context.FamilyLinks
            .AnyAsync(fl => fl.User1Id == userId || fl.User2Id == userId);

        if (inviterHasLink || accepterHasLink)
        {
            throw new InvalidOperationException("L'un des utilisateurs a déjà un lien famille actif.");
        }

        // Create the family link
        var link = new FamilyLink
        {
            User1Id = invitation.InviterUserId,
            User2Id = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.FamilyLinks.Add(link);

        // Mark invitation as accepted
        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.InvitedUserId = userId;

        await _context.SaveChangesAsync();

        // Invalidate caches for both users
        _userAccessService.InvalidateCacheForBothUsers(invitation.InviterUserId, userId);

        _logger.LogInformation("Family link created between {User1Id} and {User2Id}", invitation.InviterUserId, userId);

        return new FamilyLinkResponse
        {
            Id = link.Id,
            LinkedUserId = invitation.InviterUserId,
            LinkedUserEmail = invitation.InviterUser?.Email ?? string.Empty,
            LinkedUserName = invitation.InviterUser?.UserName ?? string.Empty,
            CreatedAt = link.CreatedAt
        };
    }

    public async Task RejectInvitationAsync(string userId, int invitationId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user?.Email == null)
        {
            throw new InvalidOperationException("Utilisateur non trouvé.");
        }

        var invitation = await _context.FamilyLinkInvitations
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.InvitedEmail.ToLower() == user.Email.ToLower());

        if (invitation == null)
        {
            throw new KeyNotFoundException("Invitation non trouvée.");
        }

        if (!invitation.IsActive)
        {
            throw new InvalidOperationException("Cette invitation n'est plus valide.");
        }

        invitation.RejectedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} rejected invitation {InvitationId}", userId, invitationId);
    }

    public async Task CancelSentInvitationAsync(string userId, int invitationId)
    {
        var invitation = await _context.FamilyLinkInvitations
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.InviterUserId == userId);

        if (invitation == null)
        {
            throw new KeyNotFoundException("Invitation non trouvée.");
        }

        if (!invitation.IsActive)
        {
            throw new InvalidOperationException("Cette invitation n'est plus valide.");
        }

        invitation.CancelledAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} cancelled invitation {InvitationId}", userId, invitationId);
    }

    public async Task DeleteFamilyLinkAsync(string userId)
    {
        var link = await _context.FamilyLinks
            .FirstOrDefaultAsync(fl => fl.User1Id == userId || fl.User2Id == userId);

        if (link == null)
        {
            throw new KeyNotFoundException("Aucun lien famille actif trouvé.");
        }

        var otherUserId = link.User1Id == userId ? link.User2Id : link.User1Id;

        _context.FamilyLinks.Remove(link);
        await _context.SaveChangesAsync();

        // Invalidate caches for both users
        _userAccessService.InvalidateCacheForBothUsers(userId, otherUserId);

        _logger.LogInformation("Family link deleted between {User1Id} and {User2Id}", userId, otherUserId);
    }

    public async Task<string?> GetLinkedUserIdAsync(string userId)
    {
        var link = await _context.FamilyLinks
            .FirstOrDefaultAsync(fl => fl.User1Id == userId || fl.User2Id == userId);

        return link == null ? null : (link.User1Id == userId ? link.User2Id : link.User1Id);
    }

    private static FamilyLinkInvitationResponse MapToInvitationResponse(FamilyLinkInvitation invitation, bool isSent)
    {
        string status;
        if (invitation.AcceptedAt != null) status = "accepted";
        else if (invitation.RejectedAt != null) status = "rejected";
        else if (invitation.CancelledAt != null) status = "cancelled";
        else if (invitation.ExpiresAt <= DateTime.UtcNow) status = "expired";
        else status = "pending";

        return new FamilyLinkInvitationResponse
        {
            Id = invitation.Id,
            InviterUserId = invitation.InviterUserId,
            InviterEmail = invitation.InviterUser?.Email ?? string.Empty,
            InviterUserName = invitation.InviterUser?.UserName ?? string.Empty,
            InvitedEmail = invitation.InvitedEmail,
            CreatedAt = invitation.CreatedAt,
            ExpiresAt = invitation.ExpiresAt,
            Status = status
        };
    }

    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private static string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
