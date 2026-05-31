using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using PickleIQ.Core.Interfaces;

namespace PickleIQ.Infrastructure.AI;

public class OllamaCoachingEngine(
    IConfiguration configuration,
    ILogger<OllamaCoachingEngine> logger) : ICoachingEngine
{
    public async Task<string> GenerateReportHtmlAsync(MatchSummary summary, CancellationToken cancellationToken = default)
    {
        var endpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
        var model = configuration["Ollama:Model"] ?? "nemotron-mini";

        logger.LogInformation("Generating coaching report via Ollama at {Endpoint} using model {Model}", endpoint, model);

        var prompt = BuildPrompt(summary);

        try
        {
            var client = new OllamaApiClient(new Uri(endpoint));
            client.SelectedModel = model;

            var chat = new Chat(client);
            var sb = new System.Text.StringBuilder();
            await foreach (var chunk in chat.SendAsync(prompt, cancellationToken))
                sb.Append(chunk);

            return WrapInHtml(sb.ToString(), summary);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ollama unavailable — using fallback coaching report");
            return GenerateFallbackReport(summary);
        }
    }

    private static string BuildPrompt(MatchSummary summary) =>
        $"""
        You are an encouraging pickleball coach reviewing a recreational player's match.

        Match data:
        - Rallies detected: {summary.RallyCount}
        - Average rally length: {summary.AverageRallySeconds:F1} seconds
        - Longest rally: {summary.LongestRallySeconds:F1} seconds
        - Total match duration: {summary.TotalMatchSeconds / 60:F0} minutes

        Write a short coaching report with exactly these four sections:
        STRENGTHS: (2-3 bullet points starting with -)
        AREAS FOR IMPROVEMENT: (2-3 bullet points starting with -)
        RECOMMENDED DRILLS: (2-3 drills, each on its own line starting with -)
        MATCH SUMMARY: (1 short paragraph)

        Keep the tone positive and actionable. Be specific to pickleball.
        """;

    private static string WrapInHtml(string aiResponse, MatchSummary summary)
    {
        var sections = aiResponse
            .Replace("STRENGTHS:", "<h3>Strengths</h3><ul>")
            .Replace("AREAS FOR IMPROVEMENT:", "</ul><h3>Areas for Improvement</h3><ul>")
            .Replace("RECOMMENDED DRILLS:", "</ul><h3>Recommended Drills</h3><ul>")
            .Replace("MATCH SUMMARY:", "</ul><h3>Match Summary</h3><p>")
            .Replace("\n- ", "\n<li>")
            + "</p>";

        return $"""
            <div class="coaching-report">
                <div class="match-stats mb-3">
                    <strong>Rallies:</strong> {summary.RallyCount} &nbsp;|&nbsp;
                    <strong>Avg rally:</strong> {summary.AverageRallySeconds:F1}s &nbsp;|&nbsp;
                    <strong>Longest:</strong> {summary.LongestRallySeconds:F1}s
                </div>
                {sections}
            </div>
            """;
    }

    private static string GenerateFallbackReport(MatchSummary summary) =>
        $"""
        <div class="coaching-report">
            <div class="alert alert-warning">AI coaching engine unavailable. Showing statistical summary.</div>
            <h3>Match Statistics</h3>
            <ul>
                <li>Rallies detected: {summary.RallyCount}</li>
                <li>Average rally length: {summary.AverageRallySeconds:F1} seconds</li>
                <li>Longest rally: {summary.LongestRallySeconds:F1} seconds</li>
                <li>Total match: {summary.TotalMatchSeconds / 60:F0} minutes</li>
            </ul>
            <p>Start Ollama locally with <code>ollama run nemotron-mini</code> and reprocess to get AI coaching feedback.</p>
        </div>
        """;
}
