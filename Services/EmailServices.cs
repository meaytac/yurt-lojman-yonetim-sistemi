using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.Services;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken);
}

public interface IEmailOutboxService
{
    Task EnqueueAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken);
}

public class LoggingEmailSender(ILogger<LoggingEmailSender> logger, IOptions<EmailOptions> options) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        logger.LogInformation("Email outbox sent from {From} to {To}: {Subject}", options.Value.FromAddress, toEmail, subject);
        return Task.CompletedTask;
    }
}

public class EmailOutboxService(AppDbContext db) : IEmailOutboxService
{
    public async Task EnqueueAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        db.EmailOutboxMessages.Add(new EmailOutboxMessage
        {
            ToEmail = toEmail.Trim(),
            Subject = subject.Trim(),
            HtmlBody = htmlBody
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}

public class EmailOutboxWorker(IServiceScopeFactory scopeFactory, ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Email outbox batch failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var messages = await db.EmailOutboxMessages
            .Where(x => x.SentAt == null && x.AttemptCount < 5)
            .OrderBy(x => x.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.AttemptCount++;
            message.LastAttemptAt = DateTime.UtcNow;
            try
            {
                await sender.SendAsync(message.ToEmail, message.Subject, message.HtmlBody, cancellationToken);
                message.SentAt = DateTime.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            }
        }

        if (messages.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
