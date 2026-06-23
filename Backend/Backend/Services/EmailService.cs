using System.Net;
using System.Net.Mail;
using Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public sealed class EmailService(
    IOptions<SmtpOptions> smtpOptions,
    ILogger<EmailService> logger)
{
    private readonly SmtpOptions options = smtpOptions.Value;

    public async Task<bool> SendVerificationEmailAsync(
        string toAddress,
        string toName,
        string verificationUrl,
        CancellationToken cancellationToken)
    {
        var subject = "Verify your email — AI-Coding Assessment Platform";
        var html = BuildVerificationHtml(toName, verificationUrl);
        var plain = BuildVerificationPlain(toName, verificationUrl);

        return await SendAsync(toAddress, toName, subject, html, plain, cancellationToken);
    }

    public async Task<bool> SendVerificationCodeAsync(
        string toAddress,
        string toName,
        string code,
        CancellationToken cancellationToken)
    {
        var subject = $"Your verification code: {code}";
        var html = BuildVerificationCodeHtml(toName, code);
        var plain = BuildVerificationCodePlain(toName, code);

        return await SendAsync(toAddress, toName, subject, html, plain, cancellationToken);
    }

    public async Task<bool> SendTemporaryPasswordAsync(
        string toAddress,
        string toName,
        string temporaryPassword,
        CancellationToken cancellationToken)
    {
        var subject = "Your temporary password — AI-Coding Assessment Platform";
        var html = BuildTemporaryPasswordHtml(toName, temporaryPassword);
        var plain = BuildTemporaryPasswordPlain(toName, temporaryPassword);

        return await SendAsync(toAddress, toName, subject, html, plain, cancellationToken);
    }

    private async Task<bool> SendAsync(
        string toAddress,
        string toName,
        string subject,
        string htmlBody,
        string plainBody,
        CancellationToken cancellationToken)
    {
        if (!options.IsConfigured)
        {
            logger.LogWarning(
                "SMTP not configured. Verification email to {Email} would say: {Body}",
                toAddress, plainBody);
            return false;
        }

        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(options.FromAddress, options.FromName);
            message.To.Add(new MailAddress(toAddress, toName));
            message.Subject = subject;
            message.Body = htmlBody;
            message.IsBodyHtml = true;

            // Plain-text fallback alternative view
            var plainView = AlternateView.CreateAlternateViewFromString(
                plainBody, null, "text/plain");
            var htmlView = AlternateView.CreateAlternateViewFromString(
                htmlBody, null, "text/html");
            message.AlternateViews.Add(plainView);
            message.AlternateViews.Add(htmlView);

            using var client = new SmtpClient(options.Host, options.Port)
            {
                EnableSsl = options.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(options.Username, options.Password),
                Timeout = 15000
            };

            await client.SendMailAsync(message, cancellationToken);
            logger.LogInformation("Verification email sent to {Email}", toAddress);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to send verification email to {Email}", toAddress);
            return false;
        }
    }

    private static string BuildVerificationHtml(string name, string verificationUrl)
    {
        var safeName = WebUtility.HtmlEncode(name);
        var safeUrl = WebUtility.HtmlEncode(verificationUrl);
        return $$"""
        <!DOCTYPE html>
        <html>
        <body style="font-family: -apple-system, system-ui, sans-serif; background: #0a0f1a; color: #e5e7eb; padding: 24px;">
          <div style="max-width: 520px; margin: 0 auto; background: #111827; border: 1px solid #1f2937; border-radius: 12px; padding: 32px;">
            <h1 style="color: #06b6d4; margin: 0 0 16px;">Verify your email</h1>
            <p>Hi {{safeName}},</p>
            <p>Thanks for signing up to the <strong>AI-Coding Assessment Platform</strong>. Please confirm your email address by clicking the button below:</p>
            <p style="text-align: center; margin: 32px 0;">
              <a href="{{safeUrl}}" style="display: inline-block; background: #06b6d4; color: #0a0f1a; padding: 12px 24px; border-radius: 8px; text-decoration: none; font-weight: 600;">Verify email</a>
            </p>
            <p style="font-size: 13px; color: #9ca3af;">Or copy this link into your browser:<br/><a href="{{safeUrl}}" style="color: #06b6d4; word-break: break-all;">{{safeUrl}}</a></p>
            <p style="font-size: 13px; color: #6b7280; margin-top: 32px;">This link expires in 24 hours. If you did not create an account, ignore this email.</p>
          </div>
        </body>
        </html>
        """;
    }

    private static string BuildVerificationPlain(string name, string verificationUrl)
    {
        return $"""
        Hi {name},

        Thanks for signing up to the AI-Coding Assessment Platform.
        Please confirm your email address by opening this link:

        {verificationUrl}

        This link expires in 24 hours. If you did not create an account, ignore this email.
        """;
    }

    private static string BuildVerificationCodeHtml(string name, string code)
    {
        var safeName = WebUtility.HtmlEncode(name);
        var safeCode = WebUtility.HtmlEncode(code);
        return $$"""
        <!DOCTYPE html>
        <html>
        <body style="font-family: -apple-system, system-ui, sans-serif; background: #0a0f1a; color: #e5e7eb; padding: 24px;">
          <div style="max-width: 520px; margin: 0 auto; background: #111827; border: 1px solid #1f2937; border-radius: 12px; padding: 32px;">
            <h1 style="color: #06b6d4; margin: 0 0 16px;">Your verification code</h1>
            <p>Hi {{safeName}},</p>
            <p>Use this code on the registration page to verify your email address:</p>
            <p style="text-align: center; margin: 32px 0;">
              <span style="display: inline-block; background: #06b6d4; color: #0a0f1a; padding: 16px 32px; border-radius: 12px; font-size: 28px; letter-spacing: 8px; font-weight: 700; font-family: ui-monospace, SFMono-Regular, monospace;">{{safeCode}}</span>
            </p>
            <p style="font-size: 13px; color: #6b7280;">This code expires in 15 minutes. If you did not request it, ignore this email.</p>
          </div>
        </body>
        </html>
        """;
    }

    private static string BuildVerificationCodePlain(string name, string code)
    {
        return $"""
        Hi {name},

        Use this code on the registration page to verify your email address:

            {code}

        This code expires in 15 minutes. If you did not request it, ignore this email.
        """;
    }

    private static string BuildTemporaryPasswordHtml(string name, string tempPassword)
    {
        var safeName = WebUtility.HtmlEncode(name);
        var safeTemp = WebUtility.HtmlEncode(tempPassword);
        return $$"""
        <!DOCTYPE html>
        <html>
        <body style="font-family: -apple-system, system-ui, sans-serif; background: #0a0f1a; color: #e5e7eb; padding: 24px;">
          <div style="max-width: 520px; margin: 0 auto; background: #111827; border: 1px solid #1f2937; border-radius: 12px; padding: 32px;">
            <h1 style="color: #06b6d4; margin: 0 0 16px;">Temporary password</h1>
            <p>Hi {{safeName}},</p>
            <p>You requested a password reset. Sign in with the temporary password below — you will be asked to choose a new password right after.</p>
            <p style="text-align: center; margin: 32px 0;">
              <span style="display: inline-block; background: #06b6d4; color: #0a0f1a; padding: 14px 28px; border-radius: 12px; font-size: 18px; letter-spacing: 2px; font-weight: 700; font-family: ui-monospace, SFMono-Regular, monospace;">{{safeTemp}}</span>
            </p>
            <p style="font-size: 13px; color: #6b7280;">This password expires in 30 minutes. If you did not request a reset, sign in with your old password and the temporary one will be discarded — ignore this email.</p>
          </div>
        </body>
        </html>
        """;
    }

    private static string BuildTemporaryPasswordPlain(string name, string tempPassword)
    {
        return $"""
        Hi {name},

        You requested a password reset. Sign in with this temporary password:

            {tempPassword}

        You will be asked to choose a new password right after signing in.
        This password expires in 30 minutes.

        If you did not request a reset, ignore this email — your old password still works.
        """;
    }
}
