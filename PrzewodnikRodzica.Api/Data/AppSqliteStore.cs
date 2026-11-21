using System.Text.Json;
using Microsoft.Data.Sqlite;
using PrzewodnikRodzica.Core.Models;
using System.Text.Json.Serialization;

namespace PrzewodnikRodzica.Api.Data;

public record QuotaStatus(int PagesUsedAzure, int HardPageBudget, int RemainingPages, decimal EstimatedCostPln, bool AzureEnabledByQuota);

public record CachedDocumentExtraction(string Sha256, int PageCount, string EngineUsed, string ExtractedText, DateTime CreatedUtc);

public record SubmittedApplicationRecord(
    string Id,
    string Category,      // "URLOP_PRACODAWCA" | "ZUS_ZAM" | "ZUS_ZAS53" | "USC_AKT" | "PUE_PYTANIE"
    string Title,
    string Recipient,
    string PayloadSummary,
    string Status,
    DateTime SubmittedUtc);

public record SavedCalculationRecord(
    string Id,
    string PersonaId,
    string Name,
    CalculationRequest Request,
    int TotalLeaveDays,
    decimal TotalEstimatedBenefitsGross,
    decimal PrimaryBenefitBase,
    string EligibilityHeadline,
    DateTime SavedUtc);

public class AppSqliteStore
{
    private readonly string _connectionString;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public AppSqliteStore(IWebHostEnvironment env)
    {
        // Na Azure App Service (Linux) /home/data jest trwałe między restartami
        string baseDir = Directory.Exists("/home")
            ? Path.Combine("/home", "data")
            : Path.Combine(env.ContentRootPath, "App_Data");

        Directory.CreateDirectory(baseDir);
        string dbPath = Path.Combine(baseDir, "przewodnik_rodzica.db");
        _connectionString = $"Data Source={dbPath};Cache=Shared";

        InitializeSchema();
    }

    private void InitializeSchema()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=DELETE;

            CREATE TABLE IF NOT EXISTS AdminConfig (
                Key TEXT PRIMARY KEY,
                JsonValue TEXT NOT NULL,
                UpdatedUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS DocumentCache (
                Sha256 TEXT PRIMARY KEY,
                PageCount INTEGER NOT NULL,
                EngineUsed TEXT NOT NULL,
                ExtractedText TEXT NOT NULL,
                CreatedUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS AzureQuotaLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PagesConsumed INTEGER NOT NULL,
                FileName TEXT NOT NULL,
                CreatedUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS SubmittedApplications (
                Id TEXT PRIMARY KEY,
                Category TEXT NOT NULL,
                Title TEXT NOT NULL,
                Recipient TEXT NOT NULL,
                PayloadSummary TEXT NOT NULL,
                Status TEXT NOT NULL,
                SubmittedUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS SavedCalculations (
                Id TEXT PRIMARY KEY,
                PersonaId TEXT NOT NULL,
                Name TEXT NOT NULL,
                RequestJson TEXT NOT NULL,
                TotalLeaveDays INTEGER NOT NULL,
                TotalEstimatedBenefitsGross REAL NOT NULL,
                PrimaryBenefitBase REAL NOT NULL,
                EligibilityHeadline TEXT NOT NULL,
                SavedUtc TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    // --- 1. Parametry Panelu Administracyjnego ---

    public BenefitParameters GetParameters()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT JsonValue FROM AdminConfig WHERE Key = 'BenefitParameters'";
        var raw = cmd.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(raw))
            return new BenefitParameters();

        return JsonSerializer.Deserialize<BenefitParameters>(raw, JsonOpts) ?? new BenefitParameters();
    }

    public void SaveParameters(BenefitParameters parameters)
    {
        string json = JsonSerializer.Serialize(parameters, JsonOpts);
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO AdminConfig (Key, JsonValue, UpdatedUtc)
            VALUES ('BenefitParameters', $json, $utc)
            ON CONFLICT(Key) DO UPDATE SET JsonValue = $json, UpdatedUtc = $utc;
            """;
        cmd.Parameters.AddWithValue("$json", json);
        cmd.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public BenefitParameters ResetParameters()
    {
        var defaults = new BenefitParameters();
        SaveParameters(defaults);
        return defaults;
    }

    // --- 2. Cache ekstrakcji PDF (SHA-256) ---

    public CachedDocumentExtraction? GetCachedDocument(string sha256)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Sha256, PageCount, EngineUsed, ExtractedText, CreatedUtc FROM DocumentCache WHERE Sha256 = $hash";
        cmd.Parameters.AddWithValue("$hash", sha256);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new CachedDocumentExtraction(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            DateTime.Parse(reader.GetString(4)));
    }

    public void SaveCachedDocument(CachedDocumentExtraction entry)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO DocumentCache (Sha256, PageCount, EngineUsed, ExtractedText, CreatedUtc)
            VALUES ($hash, $pages, $engine, $text, $utc);
            """;
        cmd.Parameters.AddWithValue("$hash", entry.Sha256);
        cmd.Parameters.AddWithValue("$pages", entry.PageCount);
        cmd.Parameters.AddWithValue("$engine", entry.EngineUsed);
        cmd.Parameters.AddWithValue("$text", entry.ExtractedText);
        cmd.Parameters.AddWithValue("$utc", entry.CreatedUtc.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    // --- 3. Licznik stron Azure Document Intelligence (dla wersji Pay-as-you-go dla DEMO ---

    public QuotaStatus GetQuotaStatus(int hardBudgetPages)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(PagesConsumed), 0) FROM AzureQuotaLog";
        int used = Convert.ToInt32(cmd.ExecuteScalar());
        int remaining = Math.Max(0, hardBudgetPages - used);
        decimal estPln = Math.Round(used * 0.04m, 2); // ~10 USD / 1000 str. ≈ 0,04 zł/str.
        return new QuotaStatus(used, hardBudgetPages, remaining, estPln, remaining > 0);
    }

    public void RecordAzurePagesUsed(int pages, string fileName)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO AzureQuotaLog (PagesConsumed, FileName, CreatedUtc) VALUES ($p, $f, $utc)";
        cmd.Parameters.AddWithValue("$p", pages);
        cmd.Parameters.AddWithValue("$f", fileName);
        cmd.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    // --- 4. Historia zasymulowanych wniosków i zapytań ---

    public void SaveApplication(SubmittedApplicationRecord record)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO SubmittedApplications (Id, Category, Title, Recipient, PayloadSummary, Status, SubmittedUtc)
            VALUES ($id, $cat, $title, $rec, $sum, $status, $utc);
            """;
        cmd.Parameters.AddWithValue("$id", record.Id);
        cmd.Parameters.AddWithValue("$cat", record.Category);
        cmd.Parameters.AddWithValue("$title", record.Title);
        cmd.Parameters.AddWithValue("$rec", record.Recipient);
        cmd.Parameters.AddWithValue("$sum", record.PayloadSummary);
        cmd.Parameters.AddWithValue("$status", record.Status);
        cmd.Parameters.AddWithValue("$utc", record.SubmittedUtc.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public List<SubmittedApplicationRecord> GetSubmittedApplications(int limit = 25)
    {
        var list = new List<SubmittedApplicationRecord>();
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Category, Title, Recipient, PayloadSummary, Status, SubmittedUtc FROM SubmittedApplications ORDER BY SubmittedUtc DESC LIMIT $lim";
        cmd.Parameters.AddWithValue("$lim", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new SubmittedApplicationRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                DateTime.Parse(reader.GetString(6))));
        }
        return list;
    }

    // --- 5. Zapisane symulacje kalkulatora per użytkownik (mObywatel) ---

    public void SaveCalculation(SavedCalculationRecord record)
    {
        string requestJson = JsonSerializer.Serialize(record.Request, JsonOpts);
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO SavedCalculations
                (Id, PersonaId, Name, RequestJson, TotalLeaveDays, TotalEstimatedBenefitsGross, PrimaryBenefitBase, EligibilityHeadline, SavedUtc)
            VALUES
                ($id, $pid, $name, $req, $days, $gross, $base, $head, $utc);
            """;
        cmd.Parameters.AddWithValue("$id", record.Id);
        cmd.Parameters.AddWithValue("$pid", record.PersonaId);
        cmd.Parameters.AddWithValue("$name", record.Name);
        cmd.Parameters.AddWithValue("$req", requestJson);
        cmd.Parameters.AddWithValue("$days", record.TotalLeaveDays);
        cmd.Parameters.AddWithValue("$gross", (double)record.TotalEstimatedBenefitsGross);
        cmd.Parameters.AddWithValue("$base", (double)record.PrimaryBenefitBase);
        cmd.Parameters.AddWithValue("$head", record.EligibilityHeadline);
        cmd.Parameters.AddWithValue("$utc", record.SavedUtc.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public List<SavedCalculationRecord> GetSavedCalculations(string personaId)
    {
        var list = new List<SavedCalculationRecord>();
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, PersonaId, Name, RequestJson, TotalLeaveDays, TotalEstimatedBenefitsGross, PrimaryBenefitBase, EligibilityHeadline, SavedUtc
            FROM SavedCalculations
            WHERE PersonaId = $pid
            ORDER BY SavedUtc DESC;
            """;
        cmd.Parameters.AddWithValue("$pid", personaId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var req = JsonSerializer.Deserialize<CalculationRequest>(reader.GetString(3), JsonOpts) ?? new CalculationRequest();
            list.Add(new SavedCalculationRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                req,
                reader.GetInt32(4),
                Convert.ToDecimal(reader.GetDouble(5)),
                Convert.ToDecimal(reader.GetDouble(6)),
                reader.GetString(7),
                DateTime.Parse(reader.GetString(8))));
        }
        return list;
    }

    public bool DeleteSavedCalculation(string personaId, string id)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM SavedCalculations WHERE PersonaId = $pid AND Id = $id";
        cmd.Parameters.AddWithValue("$pid", personaId);
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteNonQuery() > 0;
    }
}