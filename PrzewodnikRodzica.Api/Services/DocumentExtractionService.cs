using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.AI.DocumentIntelligence;
using PrzewodnikRodzica.Api.Data;
using PrzewodnikRodzica.Core.Models;
using PrzewodnikRodzica.Core.Services;
using UglyToad.PdfPig;

namespace PrzewodnikRodzica.Api.Services;

public class DocumentAnalysisResponse
{
    public string FileName { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public string ExtractionEngine { get; set; } = string.Empty;
    public bool UsedAiEngine { get; set; }
    public bool ServedFromCache { get; set; }
    public string? Notice { get; set; }
    public ParsedDocumentResult ParsedDocument { get; set; } = new();
    public CalculationResult Calculation { get; set; } = new();
    public QuotaStatus Quota { get; set; } = default!;
}

public class DocumentExtractionService
{
    private readonly AppSqliteStore _store;
    private readonly IConfiguration _config;
    private readonly BenefitEngine _engine;
    private readonly ZusDocumentParser _parser;
    private readonly ILogger<DocumentExtractionService> _logger;

    public DocumentExtractionService(
        AppSqliteStore store,
        IConfiguration config,
        BenefitEngine engine,
        ZusDocumentParser parser,
        ILogger<DocumentExtractionService> logger)
    {
        _store = store;
        _config = config;
        _engine = engine;
        _parser = parser;
        _logger = logger;
    }

    public int HardMonthlyPageBudget => _config.GetValue("AzureDocumentIntelligence:HardMonthlyPageBudget", 1500);
    public int MaxPagesPerFile => _config.GetValue("AzureDocumentIntelligence:MaxPagesPerFile", 100);

    public async Task<DocumentAnalysisResponse> AnalyzePdfAsync(byte[] pdfBytes, string fileName, CancellationToken ct)
    {
        string sha256 = Convert.ToHexString(SHA256.HashData(pdfBytes));
        var parameters = _store.GetParameters();

        // 1. Szybki odczyt z cache po SHA-256 (0 zł)
        var cached = _store.GetCachedDocument(sha256);
        if (cached != null)
        {
            var cachedParsed = _parser.ParseExtractedText(cached.ExtractedText);
            var cachedCalc = _engine.Calculate(cachedParsed.MappedRequest, parameters);
            return new DocumentAnalysisResponse
            {
                FileName = fileName,
                PageCount = cached.PageCount,
                ExtractionEngine = cached.EngineUsed,
                UsedAiEngine = cached.EngineUsed.Contains("Azure"),
                ServedFromCache = true,
                Notice = "Wynik analizy pliku PDF pobrany z pamięci podręcznej.",
                ParsedDocument = cachedParsed,
                Calculation = cachedCalc,
                Quota = _store.GetQuotaStatus(HardMonthlyPageBudget)
            };
        }

        // 2. Zliczenie stron i wstępna ekstrakcja lokalna przez PdfPig (0 zł)
        int pageCount;
        string pdfPigText;
        try
        {
            using var pdf = PdfDocument.Open(pdfBytes);
            pageCount = pdf.NumberOfPages;
            var sb = new StringBuilder();
            foreach (var page in pdf.GetPages())
            {
                sb.AppendLine(page.Text);
            }
            pdfPigText = sb.ToString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Nie udało się odczytać pliku PDF ({ex.Message}). Upewnij się, że plik nie jest zaszyfrowany hasłem.");
        }

        if (pageCount > MaxPagesPerFile)
        {
            throw new InvalidOperationException($"Plik ma {pageCount} stron (maksymalny dozwolony rozmiar demo to {MaxPagesPerFile} stron).");
        }

        // 3. Decyzja: Azure AI Document Intelligence (prebuilt-layout) vs lokalny fallback PdfPig
        string? endpoint = _config["AzureDocumentIntelligence:Endpoint"];
        string? apiKey = _config["AzureDocumentIntelligence:ApiKey"];
        var quotaBefore = _store.GetQuotaStatus(HardMonthlyPageBudget);

        string extractedText = pdfPigText;
        string engineUsed = "PdfPig (lokalny parser warstwy tekstowej — bez AI)";
        bool usedAi = false;
        string? notice = null;

        bool hasAzureConfig = !string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey);

        if (hasAzureConfig && quotaBefore.RemainingPages >= pageCount)
        {
            try
            {
                var client = new DocumentIntelligenceClient(new Uri(endpoint!), new AzureKeyCredential(apiKey!));
                var options = new AnalyzeDocumentOptions("prebuilt-layout", BinaryData.FromBytes(pdfBytes))
                {
                    OutputContentFormat = DocumentContentFormat.Markdown
                };

                Operation<AnalyzeResult> operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, options, ct);
                extractedText = operation.Value.Content ?? pdfPigText;
                engineUsed = "Azure AI Document Intelligence";
                usedAi = true;

                _store.RecordAzurePagesUsed(pageCount, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Błąd Azure Document Intelligence — przełączono na lokalny fallback PdfPig.");
                notice = "Usługa OCR Azure chwilowo niedostępna — zastosowano lokalny parser warstwy tekstowej PdfPig.";
            }
        }
        else if (hasAzureConfig && quotaBefore.RemainingPages < pageCount)
        {
            notice = $"Osiągnięto limit budżetowy stron OCR ({quotaBefore.PagesUsedAzure}/{quotaBefore.HardPageBudget}) — użyto darmowego parsera lokalnego PdfPig.";
        }
        else
        {
            notice = "Tryb bez klucza Azure OCR: użyto darmowego parsera warstwy tekstowej PdfPig. Dla skanów obrazkowych podepnij klucz Azure Document Intelligence S0.";
        }

        // 4. Zapis do cache i uruchomienie deterministycznego silnika C#
        _store.SaveCachedDocument(new CachedDocumentExtraction(sha256, pageCount, engineUsed, extractedText, DateTime.UtcNow));

        var parsed = _parser.ParseExtractedText(extractedText);
        var calc = _engine.Calculate(parsed.MappedRequest, parameters);

        return new DocumentAnalysisResponse
        {
            FileName = fileName,
            PageCount = pageCount,
            ExtractionEngine = engineUsed,
            UsedAiEngine = usedAi,
            ServedFromCache = false,
            Notice = notice,
            ParsedDocument = parsed,
            Calculation = calc,
            Quota = _store.GetQuotaStatus(HardMonthlyPageBudget)
        };
    }
}