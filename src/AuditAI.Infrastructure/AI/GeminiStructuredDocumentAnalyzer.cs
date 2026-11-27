using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuditAI.Application.Common.Interfaces;
using AuditAI.Application.DTOs;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditAI.Infrastructure.AI;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public string PrimaryModel { get; set; } = "gemini-3.5-flash-lite";
    public string FallbackModel { get; set; } = "gemini-3.8-flash";
    public bool PreferIpv4 { get; set; } = true;
}

public static class Ipv4SocketsHttpHandlerFactory
{
    public static SocketsHttpHandler Create(bool preferIpv4)
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            ConnectTimeout = TimeSpan.FromSeconds(15)
        };

        if (!preferIpv4)
        {
            return handler;
        }

        handler.ConnectCallback = async (context, cancellationToken) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
            var ordered = addresses
                .OrderBy(ip => ip.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
                .ToArray();

            Exception? lastError = null;
            foreach (var address in ordered)
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true
                };

                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    socket.Dispose();
                }
            }

            throw lastError ?? new SocketException((int)SocketError.HostNotFound);
        };

        return handler;
    }
}

/// <summary>
/// Adaptador oficial de Microsoft.Extensions.AI (IChatClient) hacia Google Gemini API
/// con soporte de Structured Outputs JSON y fallback automático entre modelos.
/// </summary>
public sealed class GeminiStructuredDocumentAnalyzer(
    HttpClient httpClient,
    IOptions<GeminiOptions> options,
    ILogger<GeminiStructuredDocumentAnalyzer> logger) : IAiDocumentAnalyzer, IChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly GeminiOptions _options = options.Value;

    public ChatClientMetadata Metadata => new("GoogleGemini", new Uri("https://generativelanguage.googleapis.com"), _options.PrimaryModel);

    public async Task<DocumentAiAnalysisResult> AnalyzeDocumentAsync(
        string fileName,
        string rawText,
        CancellationToken ct = default)
    {
        var apiKey = ResolveApiKey();
        var modelsToTry = new[]
        {
            _options.PrimaryModel,
            _options.FallbackModel,
            "gemini-2.5-flash"
        }
        .Where(m => !string.IsNullOrWhiteSpace(m))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        var systemPrompt = """
            Eres un auditor financiero y legal corporativo senior especializado en normativa B2B de Ecuador (SRI, RUC de 13 dígitos e IVA del 15%).
            Analiza el texto extraído de un documento PDF empresarial y devuelve ÚNICAMENTE un objeto JSON estricto conforme al esquema solicitado:
            1. Determina `documentKind`: usa exactamente "INVOICE" si es una factura/comprobante comercial, o "CONTRACT" si es un contrato/convenio legal.
            2. Si es "INVOICE":
               - Extrae `invoiceNumber`, `purchaseOrderNumber` (ej. OC-2026-001; null si no existe en el texto), `supplierName`, `supplierRuc` (tal como aparezca), `issueDate`, `subtotal`, `taxRatePercent`, `taxAmount`, `totalAmount` y todas las líneas de detalle en `lineItems`.
               - Deja `riskClauses` como arreglo vacío `[]` y `overallContractRisk` en "LOW".
            3. Si es "CONTRACT":
               - Extrae la razón social del proveedor en `supplierName`, su RUC en `supplierRuc` (si consta) y el valor total contractual en `totalAmount` (si consta).
               - Identifica en `riskClauses` toda cláusula riesgosa o abusiva (penalidades desproporcionadas, renovación automática forzosa sin preaviso razonable, terminación unilateral asimétrica, limitación nula de responsabilidad, cesión no autorizada o jurisdicción desfavorable), asignando `severity` ("LOW", "MEDIUM", "HIGH", "CRITICAL") y `estimatedFinancialExposureUsd` numérico cuando pueda estimarse.
               - Define `overallContractRisk` en "LOW", "MEDIUM", "HIGH" o "CRITICAL".
            4. Redacta `executiveSummary` en español claro, ejecutivo y conciso (2 a 3 oraciones) resumiendo el documento y cualquier hallazgo relevante.
            """;

        var userContent = $"Archivo: {fileName}\n\n=== TEXTO EXTRAÍDO DEL PDF ===\n{rawText}";

        var requestBody = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userContent } }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                responseMimeType = "application/json",
                responseSchema = BuildStructuredOutputSchema()
            }
        };

        var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);
        Exception? lastException = null;

        foreach (var model in modelsToTry)
        {
            try
            {
                var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
                };

                using var response = await httpClient.SendAsync(httpRequest, ct);
                var responseBody = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning(
                        "Gemini modelo {Model} devolvió HTTP {StatusCode}: {Body}. Intentando modelo de respaldo...",
                        model, (int)response.StatusCode, responseBody);
                    lastException = new HttpRequestException($"Gemini HTTP {(int)response.StatusCode}: {responseBody}");
                    continue;
                }

                var rawModelJson = ExtractCandidateText(responseBody);
                var payload = JsonSerializer.Deserialize<ExtractedDocumentPayload>(rawModelJson, JsonOptions)
                    ?? throw new InvalidOperationException("Gemini devolvió un payload JSON nulo.");

                var safePayload = payload with
                {
                    LineItems = payload.LineItems ?? [],
                    RiskClauses = payload.RiskClauses ?? [],
                    ExecutiveSummary = string.IsNullOrWhiteSpace(payload.ExecutiveSummary)
                        ? "Análisis estructurado completado."
                        : payload.ExecutiveSummary
                };

                return new DocumentAiAnalysisResult(safePayload, rawModelJson, model);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Fallo al invocar modelo Gemini {Model}. Probando siguiente fallback...", model);
                lastException = ex;
            }
        }

        throw new InvalidOperationException(
            $"No fue posible completar el análisis con Gemini tras intentar [{string.Join(", ", modelsToTry)}].",
            lastException);
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = ResolveApiKey();
        var model = options?.ModelId ?? _options.PrimaryModel;
        var combinedPrompt = string.Join("\n", messages.Select(m => $"{m.Role}: {m.Text}"));

        var body = JsonSerializer.Serialize(new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = combinedPrompt } } }
            },
            generationConfig = new { temperature = options?.Temperature ?? 0.2f }
        }, JsonOptions);

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
        using var response = await httpClient.PostAsync(endpoint, new StringContent(body, Encoding.UTF8, "application/json"), cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var text = ExtractCandidateText(json);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text)) { ModelId = model };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var res = await GetResponseAsync(messages, options, cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, res.Text);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }

    private string ResolveApiKey()
    {
        var key = _options.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(key) || key.Contains("PEGA_AQUI", StringComparison.OrdinalIgnoreCase))
        {
            key = Environment.GetEnvironmentVariable("GEMINI_API_KEY")?.Trim();
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("GEMINI_API_KEY no configurada en appsettings.Development.json ni variables de entorno.");
        }

        return key;
    }

    private static string ExtractCandidateText(string geminiResponseJson)
    {
        using var doc = JsonDocument.Parse(geminiResponseJson);
        var root = doc.RootElement;
        if (root.TryGetProperty("candidates", out var candidates) &&
            candidates.GetArrayLength() > 0 &&
            candidates[0].TryGetProperty("content", out var content) &&
            content.TryGetProperty("parts", out var parts) &&
            parts.GetArrayLength() > 0 &&
            parts[0].TryGetProperty("text", out var textProp))
        {
            return textProp.GetString() ?? "{}";
        }

        throw new InvalidOperationException("Respuesta de Gemini sin bloque candidates[0].content.parts[0].text.");
    }

    private static object BuildStructuredOutputSchema() => new
    {
        type = "OBJECT",
        required = new[] { "documentKind", "lineItems", "riskClauses", "executiveSummary" },
        properties = new Dictionary<string, object>
        {
            ["documentKind"] = new { type = "STRING", @enum = new[] { "INVOICE", "CONTRACT" } },
            ["invoiceNumber"] = new { type = "STRING", nullable = true },
            ["purchaseOrderNumber"] = new { type = "STRING", nullable = true },
            ["supplierName"] = new { type = "STRING", nullable = true },
            ["supplierRuc"] = new { type = "STRING", nullable = true },
            ["issueDate"] = new { type = "STRING", nullable = true },
            ["subtotal"] = new { type = "NUMBER", nullable = true },
            ["taxRatePercent"] = new { type = "NUMBER", nullable = true },
            ["taxAmount"] = new { type = "NUMBER", nullable = true },
            ["totalAmount"] = new { type = "NUMBER", nullable = true },
            ["overallContractRisk"] = new { type = "STRING", nullable = true, @enum = new[] { "LOW", "MEDIUM", "HIGH", "CRITICAL" } },
            ["executiveSummary"] = new { type = "STRING" },
            ["lineItems"] = new
            {
                type = "ARRAY",
                items = new
                {
                    type = "OBJECT",
                    required = new[] { "description", "quantity", "unitPrice", "lineTotal" },
                    properties = new Dictionary<string, object>
                    {
                        ["description"] = new { type = "STRING" },
                        ["quantity"] = new { type = "NUMBER" },
                        ["unitPrice"] = new { type = "NUMBER" },
                        ["lineTotal"] = new { type = "NUMBER" }
                    }
                }
            },
            ["riskClauses"] = new
            {
                type = "ARRAY",
                items = new
                {
                    type = "OBJECT",
                    required = new[] { "clauseReference", "riskCategory", "severity", "explanation" },
                    properties = new Dictionary<string, object>
                    {
                        ["clauseReference"] = new { type = "STRING" },
                        ["riskCategory"] = new { type = "STRING" },
                        ["severity"] = new { type = "STRING", @enum = new[] { "LOW", "MEDIUM", "HIGH", "CRITICAL" } },
                        ["explanation"] = new { type = "STRING" },
                        ["estimatedFinancialExposureUsd"] = new { type = "NUMBER", nullable = true }
                    }
                }
            }
        }
    };
}
