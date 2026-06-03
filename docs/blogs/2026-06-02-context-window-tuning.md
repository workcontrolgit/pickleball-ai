# Why Your AI Coaching Report Was Blank: A .NET Developer's Guide to Context Windows, Frames, and VRAM

*Published: 2026-06-02 | Author: PickleIQ Team*

---

When we first wired up the AI coaching engine in PickleIQ, we ran into a baffling bug: the coaching report was completely blank. No error. No exception. The job completed successfully, the database row was written, and the `HtmlContent` column contained exactly two characters — `<|` — and nothing else.

This post explains what happened, why it happened, and how we fixed it. If you are a .NET developer who has never had to think about GPUs, video memory, or "context windows" before, this is for you.

---

## What Is a Context Window?

Think of a context window as the working memory of a language model — similar to `StringBuilder` capacity, but for AI inference.

When you call a language model (whether via OpenAI, Anthropic, or a local Ollama model), every token you send in the request and every token the model generates in response consumes space in this window. The window has a fixed maximum size, measured in **tokens**.

A token is roughly 3–4 characters of text, but for images it works very differently — more on that in a moment.

If the combined size of your input (prompt + images) plus the output (generated text) exceeds the context window, the model simply stops generating. It does not throw an exception. It does not return an error. It just... stops. Whatever partial output it managed to produce is returned as the response.

In C# terms, imagine this contract:

```csharp
// This is conceptually what happens inside the model
if (inputTokens + generatedTokens >= contextWindow)
{
    // Stop generating. Return whatever we have so far.
    break;
}
```

---

## Why Images Are Expensive

When you send a text prompt to a language model, each word costs roughly 1–2 tokens. A 500-word prompt might use 600–700 tokens.

Images are entirely different. A vision model does not see a JPEG — it sees a grid of **visual patches**, and each patch costs tokens. The number of patches is determined by the image resolution:

| Image width | Approximate visual tokens |
|-------------|--------------------------|
| 1280 px | ~1,200 tokens |
| 640 px | ~300 tokens |
| 480 px | ~170 tokens |

Those numbers are not arbitrary — they come from how the model slices the image into fixed-size tiles (typically 14×14 or 16×16 pixel patches) before processing them.

---

## What Happened in PickleIQ

Our `CoachingFrameSampler` was extracting 6 frames per job — 3 frames from each of the top 2 rallies, at 1280px wide:

```csharp
// Original code — 1280px per frame
.WithVideoFilters(f => f.Scale(1280, -2))
```

Each frame was roughly **1,200 visual tokens**. Six frames: **7,200 tokens**.

Our context window was set to 4,096 tokens.

The math:

```
6 frames × 1,200 tokens/frame = 7,200 visual tokens
+ coaching prompt             =   450 tokens
─────────────────────────────────────────────
Total input                   = 7,650 tokens
Context window                = 4,096 tokens
Overflow                      = 3,554 tokens 🔴
```

The model received 7,650 tokens of input but only had a 4,096-token window. It processed as much as it could, had zero tokens left to generate a response, and returned the first two characters of its start-of-response marker (`<|im_start|>`) before hitting the limit.

There was no crash. No log warning. No exception in the Hangfire dashboard. The job completed in one second instead of the expected 30–60 seconds — which, in hindsight, was the first clue that something was wrong.

---

## How We Debugged It

We tested directly against the Ollama HTTP API using PowerShell (avoiding shell argument length limits that make `curl` impractical for large payloads):

```powershell
$imgBytes = [System.IO.File]::ReadAllBytes("C:/temp/pickleiq/frame.jpg")
$imgB64   = [System.Convert]::ToBase64String($imgBytes)

$body = @{
    model    = "qwen2.5vl:7b"
    messages = @(@{ role = "user"; content = "Describe."; images = @($imgB64) })
    stream   = $false
    options  = @{ num_ctx = 4096 }
} | ConvertTo-Json -Depth 5 -Compress

$resp = Invoke-RestMethod -Uri "http://localhost:11434/api/chat" `
        -Method Post -ContentType "application/json" -Body $body -TimeoutSec 300

Write-Host "Tokens generated: $($resp.eval_count)"   # <- the smoking gun
Write-Host "Content: $($resp.message.content)"
```

With 1 frame at 4,096 context: `eval_count = 78`, full description returned.  
With 6 frames at 4,096 context: `eval_count = 2`, content = `"woman"`.

The `eval_count` field (number of tokens generated) told us everything.

---

## The Fix: Two Levers

There are two ways to fix context exhaustion: use fewer input tokens, or increase the context window. We needed both.

### Lever 1 — Reduce frame resolution

```csharp
// Before: 1,200 tokens/frame × 6 = 7,200 tokens
.WithVideoFilters(f => f.Scale(1280, -2))

// After: ~300 tokens/frame × 6 = 1,800 tokens
.WithVideoFilters(f => f.Scale(640, -2))
```

640px is still perfectly readable for a vision model analysing court positioning and player stance. The coaching quality did not noticeably degrade.

### Lever 2 — Increase the context window

```json
// appsettings.json
"Coaching": {
  "ContextWindow": 12288
}
```

We tested several values before settling on 12,288. Here is the full benchmark:

| Context window | VRAM used | Tokens generated | Notes |
|---------------|-----------|-----------------|-------|
| 4,096 | 14.5 GB | 2 | Blank report |
| 8,192 | 12.5 GB | 415 | Works |
| **12,288** | **13.17 GB** | **544** | **Recommended** |
| 16,384 | — | Error | GGML assertion failure |

At 12,288 context the model had ~10,400 tokens available for its response after processing the frames and prompt — enough for a thorough four-section coaching report.

---

## What Is VRAM and Why Does It Matter?

This is where the hardware side comes in. If you have never touched GPU programming, here is the mental model.

**RAM** is your application's memory. When you allocate a `List<byte[]>`, that memory lives in RAM.

**VRAM** (Video RAM) is memory that lives on the GPU chip itself. When a language model runs on a GPU, the entire model — billions of floating-point weights — must be loaded into VRAM before inference can begin.

`qwen2.5vl:7b` at Q4_K_M quantization (a compressed format that reduces precision to save space) weighs in at **~14.5 GB** of VRAM just for the model weights.

The context window adds more VRAM on top of the weights. This is the **KV cache** (Key-Value cache) — the model's internal scratchpad for attending over the input tokens. A larger context window = a larger KV cache = more VRAM consumed.

```
VRAM used = model weights + KV cache
          = ~14.5 GB    + f(context_window)
```

On a 16 GB GPU:

- Context 4,096 → ~14.5 GB VRAM (tight, but fits)
- Context 8,192 → ~12.5 GB VRAM (paradoxically lower — Ollama optimises allocation at reload)
- Context 12,288 → 13.17 GB VRAM (safe 2.8 GB margin)
- Context 16,384 → hits an internal model architecture limit before VRAM becomes the constraint

When Ollama says `size_vram: 0` in `/api/ps`, the model is running on CPU. CPU inference for a 7B vision model takes 5–10 minutes per request. GPU inference takes 30–60 seconds. That is why GPU matters for this use case.

---

## If You Do Not Have a GPU

If no GPU is available (or Ollama cannot access it), the engine falls back to a statistical summary:

```csharp
catch (Exception ex)
{
    logger.LogWarning(ex, "Ollama unavailable — using fallback coaching report");
    return GenerateFallbackMarkdown(summary);
}
```

The fallback returns a plain markdown summary of the rally statistics without any AI analysis. To confirm your GPU is being used:

```bash
curl http://localhost:11434/api/ps
```

Look for `"size_vram": 14527397248` (non-zero). If you see `"size_vram": 0`, the model is on CPU. The most common cause on Windows is that the Docker container was started without the `--gpus all` flag.

---

## Summary for .NET Developers

| Concept | .NET analogy |
|---------|-------------|
| Context window | `StringBuilder` with a fixed `MaxCapacity` — overflow silently truncates |
| Visual tokens | Each image pixel grid patch costs capacity, like a large object in a bounded collection |
| VRAM | GPU-side RAM; the entire model must fit here before inference starts |
| KV cache | The model's internal working buffer; grows with context window size |
| `eval_count = 2` | Your first diagnostic — if this is near zero, context is exhausted |

The key diagnostic when a report is blank or truncated is **always check `eval_count`**. A healthy coaching response should produce 400–600 tokens. Two tokens means the model ran out of room before it could write a single sentence.

---

## Configuration Reference

Tune these values in `appsettings.json` based on your hardware:

```json
"Coaching": {
  "Endpoint": "http://localhost:11434",
  "Model": "qwen2.5vl:7b",
  "ContextWindow": 12288
}
```

| VRAM available | Recommended ContextWindow |
|---------------|--------------------------|
| 8 GB | Not supported (model weights alone need ~14.5 GB) |
| 16 GB | 12,288 (tested, recommended) |
| 24 GB | 24,576 (untested — may hit model architecture limits) |

Frame resolution is controlled in `CoachingFrameSampler.cs`:

```csharp
.WithVideoFilters(f => f.Scale(640, -2))  // 640px width, height auto
```

If you reduce to 3 frames instead of 6, you could afford 1280px again and still fit in 12,288 context — trading temporal coverage for spatial detail.
