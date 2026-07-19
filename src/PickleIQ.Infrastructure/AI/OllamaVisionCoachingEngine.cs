using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using PickleIQ.Core.Entities;
using PickleIQ.Core.Interfaces;

namespace PickleIQ.Infrastructure.AI;

public class OllamaVisionCoachingEngine(
    IConfiguration configuration,
    ILogger<OllamaVisionCoachingEngine> logger) : ICoachingEngine
{
    public async Task<string> GenerateReportHtmlAsync(
        MatchSummary summary,
        VideoMode mode = VideoMode.Match,
        IReadOnlyList<byte[]>? coachingFrames = null,
        Action<string>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = configuration["Coaching:Endpoint"] ?? "http://localhost:11434";
        var model = configuration["Coaching:Model"] ?? "qwen3-vl:8b";
        var contextWindow = int.TryParse(configuration["Coaching:ContextWindow"], out var cw) ? cw : 4096;
        var logInteraction = bool.TryParse(configuration["Coaching:LogInteraction"], out var li) && li;

        var frameCount = coachingFrames?.Count ?? 0;
        logger.LogInformation(
            "Generating vision coaching report via {Model} with {FrameCount} frames",
            model, frameCount);

        try
        {
            var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10), BaseAddress = new Uri(endpoint) };
            var client = new OllamaApiClient(httpClient, model, null);

            var prompt = BuildPrompt(summary, mode, frameCount);

            var message = new Message
            {
                Role = ChatRole.User,
                Content = prompt,
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

            if (logInteraction)
            {
                var promptPreview = prompt.Length > 200 ? prompt[..200] : prompt;
                logger.LogInformation(
                    "Ollama request — model={Model} endpoint={Endpoint} ctx={ContextWindow} frames={FrameCount} prompt={PromptLength} chars | \"{PromptPreview}\"",
                    model, endpoint, contextWindow, frameCount, prompt.Length, promptPreview);
            }

            var sw = Stopwatch.StartNew();
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
            sw.Stop();

            if (logInteraction)
            {
                var response = sb.ToString();
                var responsePreview = response.Length > 200 ? response[..200] : response;
                logger.LogInformation(
                    "Ollama response — {ResponseLength} chars in {ElapsedSeconds:F1}s | \"{ResponsePreview}\"",
                    response.Length, sw.Elapsed.TotalSeconds, responsePreview);
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ollama unavailable — using fallback coaching report");
            return GenerateFallbackMarkdown(summary, mode);
        }
    }

    private static string BuildPrompt(MatchSummary summary, VideoMode mode, int frameCount)
    {
        string role, statsHeader, segmentLabel, summarySection, frameSection;

        switch (mode)
        {
            case VideoMode.Training:
                role = "You are a certified pickleball coach reviewing a practice session. The session may involve one player drilling alone, two players in singles practice, or a full doubles team drilling together. Adapt your coaching to what you observe.";
                statsHeader = "Training session data:";
                segmentLabel = "Active segments detected";
                summarySection = "## Session Summary";
                frameSection = frameCount > 0
                    ? $"""
                       You are given {frameCount} frames sampled from the practice session. Analyse what you can see:
                       - Number of players visible and the likely drill format (solo, singles, doubles)
                       - Court positioning — relative to the kitchen line and baseline?
                       - Ready position — paddle up, athletic stance, weight forward between shots?
                       - Footwork — split-step, shuffle steps, crossover footwork, recovery steps?
                       - Paddle and grip — continental vs eastern, wrist position, paddle height?
                       - Drill execution — repetition quality, consistency, cooperative feeding if a partner is present?
                       """
                    : "No video frames were available. Base your coaching on the training session statistics only.";
                break;

            case VideoMode.FollowCam:
                role = "You are a certified pickleball coach reviewing a competitive match filmed with a follow-cam tracking one specific player throughout. Player count per frame will vary — the tracked player may be isolated or surrounded by all four players depending on court position.";
                statsHeader = "Match data (follow-cam):";
                segmentLabel = "Rallies detected";
                summarySection = "## Match Summary";
                frameSection = frameCount > 0
                    ? $"""
                       You are given {frameCount} frames tracking one player through the match. Focus your analysis on that player:
                       - Court positioning — kitchen line, transition zone, or baseline during each phase?
                       - Decision-making under pressure — when to dink, drive, or lob?
                       - Ready position and reset — recovering between shots, paddle height, weight transfer?
                       - Footwork — split-step timing, lateral movement, closing to the kitchen?
                       - Competitive awareness — positioning relative to opponents when visible, shot selection given court situation?
                       """
                    : "No video frames were available. Base your coaching on the match statistics only.";
                break;

            default: // Match
                role = "You are a certified pickleball coach reviewing a recreational doubles match.";
                statsHeader = "Match data:";
                segmentLabel = "Rallies detected";
                summarySection = "## Match Summary";
                frameSection = frameCount > 0
                    ? $"""
                       You are given {frameCount} frames sampled from the rallies. Analyse what you can see:
                       - Court positioning — are players at the kitchen line, baseline, or transition zone?
                       - Ready position — paddle up, athletic stance, weight forward between shots?
                       - Footwork — split-step, shuffle steps, crossover footwork visible?
                       - Paddle and grip — continental vs eastern, wrist position, paddle height?
                       - Partner coordination — side-by-side, stacking, covering the middle?
                       """
                    : "No video frames were available. Base your coaching on the match statistics only.";
                break;
        }

        return $"""
                {role}

                {statsHeader}
                - {segmentLabel}: {summary.RallyCount}
                - Average segment length: {summary.AverageRallySeconds:F1} seconds
                - Longest segment: {summary.LongestRallySeconds:F1} seconds
                - Total duration: {summary.TotalMatchSeconds / 60:F0} minutes

                {frameSection}

                Write a coaching report in markdown with exactly these four sections:
                ## Strengths
                ## Areas for Improvement
                ## Recommended Drills
                {summarySection}

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

    private static string GenerateFallbackMarkdown(MatchSummary summary, VideoMode mode)
    {
        var header = mode == VideoMode.Training ? "## Training Statistics" : "## Match Statistics";
        var segmentLabel = mode == VideoMode.Training ? "Active segments detected" : "Rallies detected";

        return $"""
                > AI coaching engine unavailable. Showing statistical summary.

                {header}

                - {segmentLabel}: {summary.RallyCount}
                - Average segment length: {summary.AverageRallySeconds:F1} seconds
                - Longest segment: {summary.LongestRallySeconds:F1} seconds
                - Total duration: {summary.TotalMatchSeconds / 60:F0} minutes

                Start Ollama locally and run your configured model, then reprocess to get AI coaching feedback.
                """;
    }
}
