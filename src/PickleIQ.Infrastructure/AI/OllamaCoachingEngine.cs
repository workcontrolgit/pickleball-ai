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

            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ollama unavailable — using fallback coaching report");
            return GenerateFallbackMarkdown(summary);
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

        Write a short coaching report in markdown with these four sections:
        ## Strengths
        ## Areas for Improvement
        ## Recommended Drills
        ## Match Summary

        Use bullet points under each section. Keep the tone positive and actionable. Be specific to pickleball.
        """;

    private static string GenerateFallbackMarkdown(MatchSummary summary) =>
        $"""
        > AI coaching engine unavailable. Showing statistical summary.

        ## Match Statistics

        - Rallies detected: {summary.RallyCount}
        - Average rally length: {summary.AverageRallySeconds:F1} seconds
        - Longest rally: {summary.LongestRallySeconds:F1} seconds
        - Total match: {summary.TotalMatchSeconds / 60:F0} minutes

        Start Ollama locally with `ollama run nemotron-mini` and reprocess to get AI coaching feedback.
        """;
}
