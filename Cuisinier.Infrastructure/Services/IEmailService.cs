namespace Cuisinier.Infrastructure.Services;

public interface IEmailService
{
    Task SendConfirmationEmailAsync(string email, string userId, string confirmationToken, CancellationToken cancellationToken = default);
    Task SendPasswordResetEmailAsync(string email, string userId, string resetToken, CancellationToken cancellationToken = default);
    Task SendFamilyInvitationEmailAsync(string recipientEmail, string inviterName, string inviterEmail, string token, CancellationToken cancellationToken = default);
}