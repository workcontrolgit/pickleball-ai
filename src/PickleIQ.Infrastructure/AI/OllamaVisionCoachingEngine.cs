using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using PickleIQ.Core.Interfaces;

namespace PickleIQ.Infrastructure.AI;

public class OllamaVisionCoachingEngine(
    IConfiguration configuration,
    ILogger<OllamaVisionCoachingEngine> logger) : ICoachingEngine
{
    public async Task<string> GenerateReportHtmlAsync(
        MatchSummary summary,
        IReadOnlyList<byte[]>? coachingFrames = null,
        Action<string>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = configuration["Coaching:Endpoint"] ?? "http://localhost:11434";
        var model = configuration["Coaching:Model"] ?? "qwen2-vl:7b";
        var contextWindow = int.TryParse(configuration["Coaching:ContextWindow"], out var cw) ? cw : 4096;

        var frameCount = coachingFrames?.Count ?? 0;
        logger.LogInformation(
            "Generating vision coaching report via {Model} with {FrameCount} frames",
            model, frameCount);

        try
        {
            var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10), BaseAddress = new Uri(endpoint) };
            var client = new OllamaApiClient(httpClient, model, null);

            var message = new Message
            {
                Role = ChatRole.User,
                Content = BuildPrompt(summary, frameCount),
                Images = frameCount > 0
                    ? coachingFrames!.Select(f => Convert.ToBase64String(f)).ToArray()
                    : null
            };

            var request = new ChatRequest
            {
                Model = model,
                Messages = [message],
                Options = new RequestOptions { NumCtx = contextWindow },
                Stream = true
            };

            var sb = new System.Text.StringBuilder();
            await foreach (var chunk in client.ChatAsync(request, cancellationToken))
            {
                var content = chunk?.Message?.Content;
                if (!string.IsNullOrEmpty(content))
                {
                    var cleaned = StripSpecialTokens(content);
                    if (!string.IsNullOrEmpty(cleaned))
                    {
                        sb.Append(cleaned);
                        onChunk?.Invoke(cleaned);
                    }
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ollama unavailable — using fallback coaching report");
            return GenerateFallbackMarkdown(summary);
        }
    }

    private static string BuildPrompt(MatchSummary summary, int frameCount)
    {
        var frameSection = frameCount > 0
            ? $"""
               You are given {frameCount} frames sampled from the rallies. Analyse what you can see:
               - Court positioning — are players at the kitchen line, baseline, or transition zone?
               - Ready position — paddle up, athletic stance, weight forward between shots?
               - Footwork — split-step, shuffle steps, crossover footwork visible?
               - Paddle and grip — continental vs eastern, wrist position, paddle height?
               - Partner coordination — side-by-side, stacking, covering the middle?
               """
            : "No video frames were available. Base your coaching on the match statistics only.";

        return $"""
                You are a certified pickleball coach reviewing a recreational doubles match.

                Match data:
                - Rallies detected: {summary.RallyCount}
                - Average rally length: {summary.AverageRallySeconds:F1} seconds
                - Longest rally: {summary.LongestRallySeconds:F1} seconds
                - Total match duration: {summary.TotalMatchSeconds / 60:F0} minutes

                {frameSection}

                Write a coaching report in markdown with exactly these four sections:
                ## Strengths
                ## Areas for Improvement
                ## Recommended Drills
                ## Match Summary

                Use bullet points under each section. Keep tone encouraging and actionable. Be specific to pickleball.
                """;
    }

    private static readonly string[] _qwenSpecialTokens =
        ["<|im_start|>", "<|im_end|>", "<|endoftext|>", "<|object_ref_start|>", "<|object_ref_end|>"];

    private static string StripSpecialTokens(string text)
    {
        foreach (var token in _qwenSpecialTokens)
            text = text.Replace(token, string.Empty);
        return text;
    }

    private static string GenerateFallbackMarkdown(MatchSummary summary) =>
        $"""
         > AI coaching engine unavailable. Showing statistical summary.

         ## Match Statistics

         - Rallies detected: {summary.RallyCount}
         - Average rally length: {summary.AverageRallySeconds:F1} seconds
         - Longest rally: {summary.LongestRallySeconds:F1} seconds
         - Total match: {summary.TotalMatchSeconds / 60:F0} minutes

         Start Ollama locally and run your configured model, then reprocess to get AI coaching feedback.
         """;
}
