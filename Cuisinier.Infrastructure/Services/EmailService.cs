using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Cuisinier.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly string _baseUrl;
    private readonly AsyncRetryPolicy _retryPolicy;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _smtpHost = _configuration["Smtp:Host"] ?? throw new InvalidOperationException("Smtp:Host not configured");
        _smtpPort = int.Parse(_configuration["Smtp:Port"] ?? "587");
        _smtpUsername = _configuration["Smtp:Username"] ?? throw new InvalidOperationException("Smtp:Username not configured");
        _smtpPassword = _configuration["Smtp:Password"] ?? throw new InvalidOperationException("Smtp:Password not configured");
        _fromEmail = _configuration["Smtp:FromEmail"] ?? throw new InvalidOperationException("Smtp:FromEmail not configured");
        _fromName = _configuration["Smtp:FromName"] ?? "Cuisinier";
        _baseUrl = _configuration["FrontendBaseUrl"] ?? _configuration["BaseUrl"] ?? "https://localhost:7092";

        // Configure retry policy with exponential backoff using Polly (recommended by Microsoft)
        var retryCount = int.Parse(_configuration["Smtp:RetryCount"] ?? "3");
        var baseDelaySeconds = double.Parse(_configuration["Smtp:RetryDelaySeconds"] ?? "2");
        
        _retryPolicy = Policy
            .Handle<SmtpException>()
            .Or<SmtpFailedRecipientException>()
            .Or<InvalidOperationException>()
            .Or<SocketException>()
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) * baseDelaySeconds),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Retry {RetryCount} for email send after {Delay}ms. Exception: {Exception}",
                        retryCount,
                        timespan.TotalMilliseconds,
                        exception.Message);
                });
    }

    public async Task SendConfirmationEmailAsync(string email, string userId, string confirmationToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var confirmationUrl = $"{_baseUrl}/confirm-email?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(confirmationToken)}";
            
            var subject = "Confirmez votre adresse email - Cuisinier";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .button:hover {{ background-color: #0056b3; }}
    </style>
</head>
<body>
    <div class='container'>
        <h1>Bienvenue sur Cuisinier !</h1>
        <p>Merci de vous être inscrit. Pour activer votre compte, veuillez confirmer votre adresse email en cliquant sur le bouton ci-dessous :</p>
        <a href='{confirmationUrl}' class='button'>Confirmer mon email</a>
        <p>Ou copiez-collez ce lien dans votre navigateur :</p>
        <p>{confirmationUrl}</p>
        <p>Ce lien expirera dans 24 heures.</p>
        <p>Si vous n'avez pas créé de compte, vous pouvez ignorer cet email.</p>
    </div>
</body>
</html>";

            await SendEmailAsync(email, subject, body, cancellationToken);
            _logger.LogInformation("Confirmation email sent to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending confirmation email to {Email}", email);
            throw;
        }
    }

    public async Task SendPasswordResetEmailAsync(string email, string userId, string resetToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var resetUrl = $"{_baseUrl}/reset-password?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(resetToken)}";
            
            var subject = "Réinitialisation de votre mot de passe - Cuisinier";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #dc3545; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .button:hover {{ background-color: #c82333; }}
    </style>
</head>
<body>
    <div class='container'>
        <h1>Réinitialisation de votre mot de passe</h1>
        <p>Vous avez demandé à réinitialiser votre mot de passe. Cliquez sur le bouton ci-dessous pour créer un nouveau mot de passe :</p>
        <a href='{resetUrl}' class='button'>Réinitialiser mon mot de passe</a>
        <p>Ou copiez-collez ce lien dans votre navigateur :</p>
        <p>{resetUrl}</p>
        <p>Ce lien expirera dans 1 heure.</p>
        <p>Si vous n'avez pas demandé cette réinitialisation, vous pouvez ignorer cet email. Votre mot de passe restera inchangé.</p>
    </div>
</body>
</html>";

            await SendEmailAsync(email, subject, body, cancellationToken);
            _logger.LogInformation("Password reset email sent to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending password reset email to {Email}", email);
            throw;
        }
    }

    public async Task SendFamilyInvitationEmailAsync(string recipientEmail, string inviterName, string inviterEmail, string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var acceptUrl = $"{_baseUrl}/accept-family-invitation?token={Uri.EscapeDataString(token)}";

            var subject = $"{inviterName} vous invite à partager son compte Cuisinier";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #E85A4F; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .button:hover {{ background-color: #D94A3F; }}
    </style>
</head>
<body>
    <div class='container'>
        <h1>Invitation Mode Famille</h1>
        <p><strong>{inviterName}</strong> ({inviterEmail}) vous invite à partager ses menus et listes de courses sur Cuisinier.</p>
        <p>En acceptant cette invitation :</p>
        <ul>
            <li>Vous pourrez voir et modifier ses menus</li>
            <li>Vous pourrez voir et modifier ses listes de courses</li>
            <li>Il pourra également voir et modifier les vôtres</li>
            <li>Vos paramètres resteront personnels</li>
        </ul>
        <a href='{acceptUrl}' class='button'>Accepter l'invitation</a>
        <p>Ou copiez-collez ce lien dans votre navigateur :</p>
        <p>{acceptUrl}</p>
        <p>Ce lien expirera dans 7 jours.</p>
        <p>Si vous ne connaissez pas cette personne, ignorez cet email.</p>
    </div>
</body>
</html>";

            await SendEmailAsync(recipientEmail, subject, body, cancellationToken);
            _logger.LogInformation("Family invitation email sent to {Email} from {Inviter}", recipientEmail, inviterEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending family invitation email to {Email}", recipientEmail);
            throw;
        }
    }

    private async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        // Execute with retry policy (exponential backoff using Polly - recommended by Microsoft)
        await _retryPolicy.ExecuteAsync(async (ct) =>
        {
            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = _smtpPort == 587 || _smtpPort == 465,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 30000
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            message.To.Add(to);

            await client.SendMailAsync(message, ct);
        }, cancellationToken);
    }
}