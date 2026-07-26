using System.Text.Json;
using CarAutoParts.Application.DTOs.Fbr;
using CarAutoParts.Application.DTOs.Pos;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Infrastructure.Data;
using CarAutoParts.Infrastructure.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;



namespace CarAutoParts.Infrastructure.Services;



public sealed class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly OutboxHeartbeat _heartbeat;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor> logger,
        OutboxHeartbeat heartbeat)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _heartbeat = heartbeat;
    }



    protected override async Task ExecuteAsync(CancellationToken stoppingToken)

    {

        while (!stoppingToken.IsCancellationRequested)

        {

            try

            {

                await ProcessBatchAsync(stoppingToken);

            }

            catch (Exception ex) when (ex is not OperationCanceledException)

            {

                _logger.LogError(ex, "Outbox processor failure");

            }



            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        }

    }



    private async Task ProcessBatchAsync(CancellationToken ct)

    {

        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var fbrService = scope.ServiceProvider.GetService<IFbrService>();



        var messages = await db.OutboxMessages

            .Where(m => m.ProcessedAtUtc == null && !m.IsDeleted)

            .OrderBy(m => m.OccurredAtUtc)

            .Take(50)

            .ToListAsync(ct);



        foreach (var message in messages)

        {

            try

            {

                if (IsFbrMessage(message.Type) && fbrService is not null)

                    await ProcessFbrMessageAsync(db, fbrService, message, ct);

                else

                    _logger.LogInformation("Outbox dispatched {Type} #{Id}", message.Type, message.Id);



                message.ProcessedAtUtc = DateTime.UtcNow;

                message.AttemptCount++;

            }

            catch (Exception ex)

            {

                message.AttemptCount++;

                message.Error = ex.Message;

                _logger.LogWarning(ex, "Outbox message {Id} failed", message.Id);

            }

        }



        if (messages.Count > 0)
            await db.SaveChangesAsync(ct);

        var pending = await db.OutboxMessages.CountAsync(m => m.ProcessedAtUtc == null && !m.IsDeleted, ct);
        _heartbeat.RecordSuccess(pending);
    }



    private static bool IsFbrMessage(string type) =>

        type.Contains("Fbr", StringComparison.OrdinalIgnoreCase) ||

        type.Equals("FbrSubmissionRequested", StringComparison.OrdinalIgnoreCase);



    private async Task ProcessFbrMessageAsync(

        ApplicationDbContext db,

        IFbrService fbrService,

        OutboxMessage message,

        CancellationToken ct)

    {

        var submission = JsonSerializer.Deserialize<FbrSubmissionRequested>(message.Payload, JsonOptions)

            ?? throw new InvalidOperationException("Invalid FBR outbox payload.");



        var invoice = await db.SalesInvoices

            .Include(i => i.Lines)

            .FirstOrDefaultAsync(i => i.Id == submission.SalesInvoiceId && !i.IsDeleted, ct)

            ?? throw new InvalidOperationException($"Sales invoice {submission.SalesInvoiceId} not found.");



        var settings = await db.CompanySettings.FirstOrDefaultAsync(s => !s.IsDeleted, ct)

            ?? new CompanySettings();



        FbrInvoiceRequestDto request;

        if (!string.IsNullOrWhiteSpace(submission.RequestJson))

            request = JsonSerializer.Deserialize<FbrInvoiceRequestDto>(submission.RequestJson, JsonOptions)

                ?? throw new InvalidOperationException("Invalid FBR request JSON in outbox payload.");

        else

        {

            var buyer = new PosBuyerDto(

                invoice.BuyerName ?? "Walk-in Customer",

                invoice.BuyerNtnCnic,

                invoice.BuyerRegistrationType ?? "Unregistered",

                invoice.BuyerProvince ?? string.Empty,

                invoice.BuyerAddress ?? string.Empty,

                null,

                null);

            request = FbrInvoiceBuilder.Build(invoice, invoice.Lines.ToList(), settings, buyer, null, null);

        }



        var result = await fbrService.PostInvoiceAsync(request, ct);



        var existingSubmission = await db.FbrSubmissions

            .FirstOrDefaultAsync(f => f.SalesInvoiceId == invoice.Id, ct);



        if (existingSubmission is null)

        {

            db.FbrSubmissions.Add(new FbrSubmission

            {

                SalesInvoiceId = invoice.Id,

                FbrInvoiceNumber = result.InvoiceNumber,

                Status = result.Success

                    ? (result.WasStubbed ? FbrSubmissionStatus.Stub : FbrSubmissionStatus.Success)

                    : FbrSubmissionStatus.Failed,

                RequestJson = result.RequestJson ?? submission.RequestJson,

                ResponseJson = result.ResponseJson,

                ErrorMessage = result.Success ? null : result.Message,

                SubmittedAt = DateTime.UtcNow

            });

        }

        else

        {

            existingSubmission.FbrInvoiceNumber = result.InvoiceNumber;

            existingSubmission.Status = result.Success

                ? (result.WasStubbed ? FbrSubmissionStatus.Stub : FbrSubmissionStatus.Success)

                : FbrSubmissionStatus.Failed;

            existingSubmission.RequestJson = result.RequestJson ?? submission.RequestJson;

            existingSubmission.ResponseJson = result.ResponseJson;

            existingSubmission.ErrorMessage = result.Success ? null : result.Message;

            existingSubmission.SubmittedAt = DateTime.UtcNow;

            existingSubmission.UpdatedAt = DateTime.UtcNow;

        }



        _logger.LogInformation(

            "FBR outbox processed for invoice {InvoiceId}: success={Success}",

            invoice.Id,

            result.Success);

    }

}


