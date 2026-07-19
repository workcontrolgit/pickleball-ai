# Rally Detection Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the serial extract-then-detect flow in `RallyDetectionService` with an in-memory producer-consumer pipeline where FFmpeg streams raw frames directly to YOLO workers, eliminating temp disk usage and maximizing CPU/GPU utilization.

**Architecture:** FFmpeg pipes `bgra` rawvideo frames to stdout; a producer task reads frames in fixed-size chunks and enqueues `SKBitmap` objects into a bounded `Channel<(int Index, SKBitmap Frame)>`; N consumer tasks (each with its own `Yolo` instance) dequeue frames and run inference concurrently. Results flow into a `ConcurrentBag<double>`, sorted and passed to the unchanged `GroupIntoSegments`.

**Tech Stack:** .NET 10, SkiaSharp 3.119.4, YoloDotNet 4.2.0, System.Threading.Channels, xUnit 2.9

---

### Task 1: Create xUnit test project

**Files:**
- Create: `src/PickleIQ.Tests/PickleIQ.Tests.csproj`
- Modify: `src/PickleIQ.Infrastructure/PickleIQ.Infrastructure.csproj` — add `InternalsVisibleTo`

- [ ] **Step 1: Create test project file**

```xml
<!-- src/PickleIQ.Tests/PickleIQ.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\PickleIQ.Infrastructure\PickleIQ.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Add InternalsVisibleTo to Infrastructure project**

In `src/PickleIQ.Infrastructure/PickleIQ.Infrastructure.csproj`, add inside the first `<ItemGroup>`:

```xml
  <ItemGroup>
    <ProjectReference Include="..\PickleIQ.Core\PickleIQ.Core.csproj" />
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>PickleIQ.Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
```

- [ ] **Step 3: Add project to solution**

```bash
cd c:/apps/pickleball/PickleIQ
dotnet sln add src/PickleIQ.Tests/PickleIQ.Tests.csproj
```

Expected output: `Project 'src/PickleIQ.Tests/PickleIQ.Tests.csproj' added to the solution.`

- [ ] **Step 4: Verify it builds**

```bash
cd c:/apps/pickleball/PickleIQ
dotnet build src/PickleIQ.Tests/PickleIQ.Tests.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
cd c:/apps/pickleball/PickleIQ
git add src/PickleIQ.Tests/PickleIQ.Tests.csproj src/PickleIQ.Infrastructure/PickleIQ.Infrastructure.csproj
git commit -m "test: add PickleIQ.Tests xUnit project"
```

---

### Task 2: Add and test ComputeScaledHeight

This pure function computes the FFmpeg output height from native video dimensions. It must be `internal static` so the test project can call it directly.

**Files:**
- Modify: `src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs` — add `internal static int ComputeScaledHeight`
- Create: `src/PickleIQ.Tests/Services/RallyDetectionServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `src/PickleIQ.Tests/Services/RallyDetectionServiceTests.cs`:

```csharp
using PickleIQ.Infrastructure.Services;

namespace PickleIQ.Tests.Services;

public class RallyDetectionServiceTests
{
    [Theory]
    [InlineData(1920, 1080, 360)]  // 16:9 → 360 (even)
    [InlineData(1280, 720,  360)]  // 16:9 smaller
    [InlineData(3840, 2160, 360)]  // 4K 16:9
    [InlineData(1920, 1440, 480)]  // 4:3
    [InlineData(1920, 1280, 428)]  // 3:2 → 426.67 rounds to 427 → +1 = 428
    public void ComputeScaledHeight_ReturnsNearestEvenHeight(int w, int h, int expected)
    {
        var result = RallyDetectionService.ComputeScaledHeight(w, h, 640);
        Assert.Equal(expected, result);
        Assert.Equal(0, result % 2);
    }
}
```

- [ ] **Step 2: Run to confirm it fails**

```bash
cd c:/apps/pickleball/PickleIQ
dotnet test src/PickleIQ.Tests/PickleIQ.Tests.csproj --no-build 2>&1 | tail -5
```

Expected: build error — `ComputeScaledHeight` does not exist yet.

- [ ] **Step 3: Add ComputeScaledHeight to RallyDetectionService**

In `src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs`, add this method anywhere in the class (e.g., before `GroupIntoSegments`):

```csharp
internal static int ComputeScaledHeight(int nativeW, int nativeH, int targetW)
{
    var h = (int)Math.Round((double)targetW * nativeH / nativeW);
    return h % 2 == 0 ? h : h + 1;
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
cd c:/apps/pickleball/PickleIQ
dotnet test src/PickleIQ.Tests/PickleIQ.Tests.csproj -v minimal
```

Expected:
```
Passed! - Failed: 0, Passed: 5, Skipped: 0
```

- [ ] **Step 5: Commit**

```bash
git add src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs \
        src/PickleIQ.Tests/Services/RallyDetectionServiceTests.cs
git commit -m "feat: add RallyDetectionService.ComputeScaledHeight with tests"
```

---

### Task 3: Implement RunProducerAsync

Starts FFmpeg with `bgra` rawvideo output piped to stdout, reads frames in exact `frameSize`-byte chunks, creates `SKBitmap` from each chunk, and writes to the channel.

**Files:**
- Modify: `src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs`

- [ ] **Step 1: Add required usings at top of RallyDetectionService.cs**

Ensure these usings are present (add any missing ones):

```csharp
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using FFMpegCore;
using FFMpegCore.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PickleIQ.Core.Entities;
using PickleIQ.Core.Interfaces;
using SkiaSharp;
using YoloDotNet;
using YoloDotNet.Models;
using YoloDotNet.ExecutionProvider.Cpu;
using YoloDotNet.ExecutionProvider.DirectML;
```

- [ ] **Step 2: Add ReadExactAsync helper**

Add this private static method to `RallyDetectionService`:

```csharp
private static async ValueTask<int> ReadExactAsync(
    Stream stream, byte[] buffer, int count, CancellationToken ct)
{
    int totalRead = 0;
    while (totalRead < count)
    {
        var read = await stream.ReadAsync(buffer.AsMemory(totalRead, count - totalRead), ct);
        if (read == 0) break;
        totalRead += read;
    }
    return totalRead;
}
```

- [ ] **Step 3: Add RunProducerAsync**

Add this private method to `RallyDetectionService`:

```csharp
private async Task RunProducerAsync(
    string videoPath,
    int scaledH,
    int frameSize,
    ChannelWriter<(int Index, SKBitmap Frame)> writer,
    FFOptions ffOptions,
    CancellationTokenSource cts)
{
    var ffmpegExe = Path.Combine(ffOptions.BinaryFolder ?? "", "ffmpeg.exe");
    if (!File.Exists(ffmpegExe)) ffmpegExe = "ffmpeg";

    var args = $"-y -i \"{videoPath.Replace("\\", "/")}\" " +
               $"-vf scale=640:{scaledH},fps={FrameRateFps} " +
               $"-f rawvideo -pix_fmt bgra pipe:1";

    var psi = new ProcessStartInfo(ffmpegExe, args)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    try
    {
        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.BaseStream;
        var buffer = new byte[frameSize];
        var frameIndex = 0;

        while (true)
        {
            var bytesRead = await ReadExactAsync(stdout, buffer, frameSize, cts.Token);
            if (bytesRead < frameSize) break;

            var bmp = new SKBitmap(new SKImageInfo(640, scaledH, SKColorType.Bgra8888, SKAlphaType.Opaque));
            Marshal.Copy(buffer, 0, bmp.GetPixels(), frameSize);

            await writer.WriteAsync((frameIndex++, bmp), cts.Token);
        }

        await proc.WaitForExitAsync(cts.Token);

        if (proc.ExitCode != 0)
        {
            var stderr = await proc.StandardError.ReadToEndAsync(cts.Token);
            throw new InvalidOperationException(
                $"FFmpeg exited {proc.ExitCode}: {stderr[..Math.Min(500, stderr.Length)]}");
        }

        logger.LogInformation("Producer: streamed {Count} frames", frameIndex);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        cts.Cancel();
        throw;
    }
    finally
    {
        writer.Complete();
    }
}
```

- [ ] **Step 4: Build to check for compile errors**

```bash
cd c:/apps/pickleball/PickleIQ
dotnet build src/PickleIQ.Infrastructure/PickleIQ.Infrastructure.csproj 2>&1 | grep -E "error CS|Build succeeded|Build FAILED"
```

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs
git commit -m "feat: add RunProducerAsync — FFmpeg stdout pipe to channel"
```

---

### Task 4: Implement RunConsumerAsync

Each consumer owns its own `Yolo` instance. Worker 0 tries DirectML (GPU); all others use CPU. Consumers drain the channel until it is complete or the token is cancelled.

**Files:**
- Modify: `src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs`

- [ ] **Step 1: Add RunConsumerAsync**

Add this private method to `RallyDetectionService`:

```csharp
private async Task RunConsumerAsync(
    int workerId,
    ChannelReader<(int Index, SKBitmap Frame)> reader,
    int minPlayers,
    ConcurrentBag<double> activeTimestamps,
    CancellationTokenSource cts)
{
    var modelPath = configuration["YoloModel:Path"]
        ?? Path.Combine(AppContext.BaseDirectory, "Models", "yolo26n.onnx");

    var useGpu = workerId == 0
        && bool.TryParse(configuration["Processing:UseGpuYolo"], out var gy) && gy;

    Yolo? yolo = null;

    if (useGpu)
    {
        try
        {
            yolo = new Yolo(new YoloOptions
            {
                ExecutionProvider = new DirectMLExecutionProvider(modelPath)
            });
            logger.LogInformation("Consumer {WorkerId}: YOLO on GPU (DirectML)", workerId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Consumer {WorkerId}: DirectML failed, falling back to CPU", workerId);
        }
    }

    if (yolo is null)
    {
        yolo = new Yolo(new YoloOptions
        {
            ExecutionProvider = new CpuExecutionProvider(modelPath)
        });
        logger.LogInformation("Consumer {WorkerId}: YOLO on CPU", workerId);
    }

    using (yolo)
    {
        try
        {
            await foreach (var (index, frame) in reader.ReadAllAsync(cts.Token))
            {
                using (frame)
                {
                    try
                    {
                        var detections = yolo.RunObjectDetection(
                            frame, confidence: PersonConfidenceThreshold, iou: 0.5f);
                        var personCount = detections.Count(d => d.Label.Name == "person");
                        if (personCount >= minPlayers)
                            activeTimestamps.Add(index * (1.0 / FrameRateFps));
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex,
                            "Consumer {WorkerId}: frame {Index} detection failed, skipping",
                            workerId, index);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            cts.Cancel();
            throw;
        }
    }
}
```

- [ ] **Step 2: Build to check for compile errors**

```bash
cd c:/apps/pickleball/PickleIQ
dotnet build src/PickleIQ.Infrastructure/PickleIQ.Infrastructure.csproj 2>&1 | grep -E "error CS|Build succeeded|Build FAILED"
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs
git commit -m "feat: add RunConsumerAsync — parallel YOLO inference workers"
```

---

### Task 5: Wire pipeline + update DetectRalliesAsync

Add `RunDetectionPipelineAsync` which probes video dimensions, creates the channel, and launches producer + consumers. Update `DetectRalliesAsync` to call it. Remove `ExtractFramesAsync` and `DetectActiveFrames`.

**Files:**
- Modify: `src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs`

- [ ] **Step 1: Add RunDetectionPipelineAsync**

Add this private method to `RallyDetectionService`:

```csharp
private async Task<List<double>> RunDetectionPipelineAsync(
    string videoPath, VideoMode mode, CancellationToken cancellationToken)
{
    var ffOptions = FFmpegLocator.GetOptions(configuration);
    var workerCount = int.TryParse(
        configuration["Processing:PipelineWorkers"], out var w) ? w : 2;
    var minPlayers = mode is VideoMode.Training or VideoMode.FollowCam ? 1 : 2;

    var probe = await FFProbe.AnalyseAsync(videoPath, cancellationToken: cancellationToken);
    var videoStream = probe.VideoStreams.FirstOrDefault()
        ?? throw new InvalidOperationException($"No video stream found in {videoPath}");

    var scaledH = ComputeScaledHeight(videoStream.Width, videoStream.Height, 640);
    var frameSize = 640 * scaledH * 4; // bgra = 4 bytes per pixel

    logger.LogInformation(
        "Pipeline: {W}x{H} native → 640x{ScaledH}, workers={Workers}",
        videoStream.Width, videoStream.Height, scaledH, workerCount);

    var channel = Channel.CreateBounded<(int Index, SKBitmap Frame)>(
        new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.Wait });

    var activeTimestamps = new ConcurrentBag<double>();
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

    var producerTask = RunProducerAsync(
        videoPath, scaledH, frameSize, channel.Writer, ffOptions, cts);

    var consumerTasks = Enumerable.Range(0, workerCount)
        .Select(id => RunConsumerAsync(id, channel.Reader, minPlayers, activeTimestamps, cts))
        .ToArray();

    await Task.WhenAll(new[] { producerTask }.Concat(consumerTasks));

    return activeTimestamps.OrderBy(t => t).ToList();
}
```

- [ ] **Step 2: Replace DetectRalliesAsync body**

Replace the entire `DetectRalliesAsync` method (currently ~25 lines with temp dir + frame extraction) with:

```csharp
public async Task<IList<(double StartSeconds, double EndSeconds)>> DetectRalliesAsync(
    string videoPath, VideoMode mode = VideoMode.Match, CancellationToken cancellationToken = default)
{
    logger.LogInformation("Starting rally detection for {VideoPath}", videoPath);

    var activeTimestamps = await RunDetectionPipelineAsync(videoPath, mode, cancellationToken);

    logger.LogInformation("Active in {Count} frames", activeTimestamps.Count);

    var segments = GroupIntoSegments(activeTimestamps);

    logger.LogInformation("Detected {Count} rally segments", segments.Count);
    return segments;
}
```

- [ ] **Step 3: Delete ExtractFramesAsync and DetectActiveFrames**

Remove both methods entirely from `RallyDetectionService.cs`:
- `private static async Task ExtractFramesAsync(...)` (lines ~62-74 in current file)
- `private List<double> DetectActiveFrames(...)` (lines ~76-148 in current file)

These are fully replaced by `RunDetectionPipelineAsync`, `RunProducerAsync`, and `RunConsumerAsync`.

- [ ] **Step 4: Remove unused `using FFMpegCore.Enums;` if now unused**

Check that `VideoMode` still requires it — `VideoMode` is in `PickleIQ.Core.Entities`, not FFMpegCore.Enums. Remove the `using FFMpegCore.Enums;` line if the build flags it.

- [ ] **Step 5: Build**

```bash
cd c:/apps/pickleball/PickleIQ
dotnet build src/PickleIQ.Infrastructure/PickleIQ.Infrastructure.csproj 2>&1 | grep -E "error CS|Build succeeded|Build FAILED"
```

Expected: `Build succeeded.`

- [ ] **Step 6: Run tests**

```bash
cd c:/apps/pickleball/PickleIQ
dotnet test src/PickleIQ.Tests/PickleIQ.Tests.csproj -v minimal
```

Expected: `Passed! - Failed: 0, Passed: 5`

- [ ] **Step 7: Commit**

```bash
git add src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs
git commit -m "feat: replace serial rally detection with producer-consumer pipeline"
```

---

### Task 6: Add PipelineWorkers config

**Files:**
- Modify: `src/PickleIQ.Web/appsettings.json`

- [ ] **Step 1: Add PipelineWorkers to Processing section**

In `src/PickleIQ.Web/appsettings.json`, update the `Processing` block:

```json
"Processing": {
  "TargetHighlightDurationSeconds": 300,
  "UseGpuEncoding": true,
  "VideoCodec": "h264_nvenc",
  "FallbackVideoCodec": "libx264",
  "Preset": "p4",
  "FallbackPreset": "fast",
  "Crf": 23,
  "UseGpuYolo": true,
  "PipelineWorkers": 2
}
```

- [ ] **Step 2: Full solution build**

```bash
cd c:/apps/pickleball/PickleIQ
dotnet build 2>&1 | grep -E "error CS|Build succeeded|Build FAILED"
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/PickleIQ.Web/appsettings.json
git commit -m "config: add Processing:PipelineWorkers default 2"
```

---

### Task 7: Update OpenWolf anatomy and memory

**Files:**
- Modify: `.wolf/anatomy.md`
- Append: `.wolf/memory.md`

- [ ] **Step 1: Update anatomy.md**

Update the entry for `RallyDetectionService.cs` in `.wolf/anatomy.md`:

```
- `RallyDetectionService.cs` — In-memory producer-consumer pipeline: FFmpeg bgra pipe → Channel<(int,SKBitmap)> → N parallel Yolo workers; ComputeScaledHeight, RunProducerAsync, RunConsumerAsync, RunDetectionPipelineAsync (~1800 tok)
```

Add new entry under `src/PickleIQ.Tests/Services/`:
```
- `RallyDetectionServiceTests.cs` — xUnit tests for ComputeScaledHeight (5 theory cases) (~80 tok)
```

- [ ] **Step 2: Append to memory.md**

```
| HH:MM | Replaced serial YOLO loop with in-memory producer-consumer pipeline | RallyDetectionService.cs | eliminates temp disk, GPU now continuous | ~200 tok |
```

- [ ] **Step 3: Commit**

```bash
git add .wolf/anatomy.md .wolf/memory.md
git commit -m "docs: update wolf anatomy and memory for pipeline refactor"
```
