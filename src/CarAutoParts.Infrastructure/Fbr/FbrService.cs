using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CarAutoParts.Application.DTOs.Fbr;
using CarAutoParts.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CarAutoParts.Infrastructure.Fbr;

/// <summary>
/// Posts invoices to FBR's Digital Invoicing (DI) REST API.
/// Falls back to stub mode when no Bearer token is configured.
/// </summary>
public class FbrService : IFbrService
{
    private readonly FbrOptions _options;
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public FbrService(IOptions<FbrOptions> options, HttpClient http)
    {
        _options = options.Value;
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds));
    }

    public async Task<FbrPostResultDto> PostInvoiceAsync(FbrInvoiceRequestDto request, CancellationToken ct = default)
    {
        var requestJson = JsonSerializer.Serialize(request, JsonOptions);

        if (!_options.HasToken)
        {
            var fakeNumber = GenerateStubInvoiceNumber();
            var stubResponse = new FbrInvoiceResponse
            {
                InvoiceNumber = fakeNumber,
                Dated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ValidationResponse = new FbrValidationResponse { StatusCode = "00", Status = "Valid" }
            };
            var stubJson = JsonSerializer.Serialize(stubResponse, JsonOptions);
            return FbrPostResultDto.Ok(fakeNumber, stubbed: true, requestJson, stubJson);
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.PostInvoiceUrl)
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BearerToken);

            using var httpResponse = await _http.SendAsync(httpRequest, ct);
            var responseJson = await httpResponse.Content.ReadAsStringAsync(ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                return FbrPostResultDto.Fail(
                    $"FBR returned HTTP {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}.",
                    requestJson, responseJson);
            }

            FbrInvoiceResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<FbrInvoiceResponse>(responseJson, JsonOptions);
            }
            catch (JsonException)
            {
                return FbrPostResultDto.Fail("Could not parse FBR response.", requestJson, responseJson);
            }

            if (parsed is null)
                return FbrPostResultDto.Fail("Empty response from FBR.", requestJson, responseJson);

            if (!parsed.IsValid)
            {
                var err = parsed.ValidationResponse?.Error
                          ?? parsed.ValidationResponse?.Status
                          ?? "Invoice rejected by FBR.";
                return FbrPostResultDto.Fail($"FBR validation failed: {err}", requestJson, responseJson);
            }

            return FbrPostResultDto.Ok(
                parsed.InvoiceNumber ?? "(no number)",
                stubbed: false, requestJson, responseJson);
        }
        catch (TaskCanceledException)
        {
            return FbrPostResultDto.Fail("Request to FBR timed out.", requestJson);
        }
        catch (HttpRequestException ex)
        {
            return FbrPostResultDto.Fail($"Network error contacting FBR: {ex.Message}", requestJson);
        }
    }

    private static string GenerateStubInvoiceNumber()
        => $"TEST-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
}
