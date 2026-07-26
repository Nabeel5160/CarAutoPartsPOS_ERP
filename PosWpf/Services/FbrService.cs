using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PosWpf.Models;
using PosWpf.Models.Fbr;

namespace PosWpf.Services;

/// <summary>
/// Posts invoices to FBR's Digital Invoicing (DI) REST API.
/// If no Bearer token is configured it falls back to a local stub so the
/// POS keeps working offline / during development.
/// </summary>
public class FbrService : IFbrService
{
    private readonly FbrSettings _settings;
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public FbrService(FbrSettings settings, HttpClient? http = null)
    {
        _settings = settings;
        _http = http ?? new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(5, settings.TimeoutSeconds));
    }

    public async Task<FbrPostResult> PostInvoiceAsync(FbrInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var requestJson = JsonSerializer.Serialize(request, JsonOptions);

        // No token => run in stub mode so the UI flow is fully testable.
        if (!_settings.HasToken)
        {
            var fakeNumber = GenerateStubInvoiceNumber();
            var stubResponse = new FbrInvoiceResponse
            {
                InvoiceNumber = fakeNumber,
                Dated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ValidationResponse = new FbrValidationResponse { StatusCode = "00", Status = "Valid" }
            };
            var stubJson = JsonSerializer.Serialize(stubResponse, JsonOptions);
            return FbrPostResult.Ok(fakeNumber, stubbed: true, stubResponse, requestJson, stubJson);
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _settings.PostInvoiceUrl)
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.BearerToken);

            using var httpResponse = await _http.SendAsync(httpRequest, cancellationToken);
            var responseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                return FbrPostResult.Fail(
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
                return FbrPostResult.Fail("Could not parse FBR response.", requestJson, responseJson);
            }

            if (parsed is null)
                return FbrPostResult.Fail("Empty response from FBR.", requestJson, responseJson);

            if (!parsed.IsValid)
            {
                var err = parsed.ValidationResponse?.Error
                          ?? parsed.ValidationResponse?.Status
                          ?? "Invoice rejected by FBR.";
                return FbrPostResult.Fail($"FBR validation failed: {err}", requestJson, responseJson);
            }

            return FbrPostResult.Ok(
                parsed.InvoiceNumber ?? "(no number)",
                stubbed: false, parsed, requestJson, responseJson);
        }
        catch (TaskCanceledException)
        {
            return FbrPostResult.Fail("Request to FBR timed out.", requestJson);
        }
        catch (HttpRequestException ex)
        {
            return FbrPostResult.Fail($"Network error contacting FBR: {ex.Message}", requestJson);
        }
    }

    private static string GenerateStubInvoiceNumber()
    {
        // Mimic FBR's 7N + datetime + sequence shape, clearly marked as a test value.
        return $"TEST-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
    }
}
