namespace AppLens.Backend;

public sealed class LocalAiProfileBuilder
{
    public LocalAiProfile Build(TuneSummary tune)
    {
        var signals = new List<LocalAiSignal>
        {
            Signal("NVIDIA GPU", HasSucceededProbe(tune, "NVIDIA GPU"), Detail(tune, "NVIDIA GPU")),
            Signal("CUDA compiler", HasSucceededProbe(tune, "CUDA Compiler"), Detail(tune, "CUDA Compiler")),
            Signal("llama.cpp", HasSucceededProbe(tune, "llama.cpp"), Detail(tune, "llama.cpp")),
            Signal("Ollama", HasSucceededProbe(tune, "Ollama Summary"), Detail(tune, "Ollama Summary")),
            Signal("PyTorch CUDA", HasPyTorchCuda(tune), Detail(tune, "PyTorch CUDA")),
            Signal("Model cache", HasModelCache(tune), ModelCacheDetail(tune))
        };

        var hasGpu = signals.Any(signal => signal.Name == "NVIDIA GPU" && signal.Status == LocalAiSignalStatus.Present);
        var hasLlamaCpp = signals.Any(signal => signal.Name == "llama.cpp" && signal.Status == LocalAiSignalStatus.Present);
        var hasOllama = signals.Any(signal => signal.Name == "Ollama" && signal.Status == LocalAiSignalStatus.Present);
        var hasTorch = signals.Any(signal => signal.Name == "PyTorch CUDA" && signal.Status == LocalAiSignalStatus.Present);
        var hasCache = signals.Any(signal => signal.Name == "Model cache" && signal.Status == LocalAiSignalStatus.Present);

        var trainingReady = hasGpu && hasTorch;
        var readiness = trainingReady
            ? LocalAiReadiness.TrainingReady
            : hasGpu && (hasLlamaCpp || hasOllama || hasCache)
                ? LocalAiReadiness.InferenceReady
                : hasLlamaCpp || hasOllama || hasCache
                    ? LocalAiReadiness.Limited
                    : LocalAiReadiness.Unknown;

        return new LocalAiProfile
        {
            Readiness = readiness,
            WorkloadClass = WorkloadClass(hasGpu, signals),
            RecommendedRuntime = RecommendedRuntime(hasGpu, hasLlamaCpp, hasOllama),
            TrainingReady = trainingReady,
            TrainingGate = trainingReady
                ? "PyTorch CUDA appears available; still require explicit user approval before training."
                : "Training remains gated until PyTorch CUDA passes a smoke test and the user approves a run.",
            Signals = signals
        };
    }

    private static LocalAiSignal Signal(string name, bool present, string detail) =>
        new()
        {
            Name = name,
            Status = present ? LocalAiSignalStatus.Present : LocalAiSignalStatus.Missing,
            Detail = detail
        };

    private static bool HasSucceededProbe(TuneSummary tune, string probeName) =>
        tune.ToolProbes.Any(probe =>
            probe.Name.Contains(probeName, StringComparison.OrdinalIgnoreCase) &&
            probe.Status.Equals(ProbeState.Succeeded.ToString(), StringComparison.OrdinalIgnoreCase) &&
            !LooksLikeError(probe.Output));

    private static string Detail(TuneSummary tune, string probeName) =>
        tune.ToolProbes
            .FirstOrDefault(probe => probe.Name.Contains(probeName, StringComparison.OrdinalIgnoreCase))
            ?.Output ?? "";

    private static bool HasPyTorchCuda(TuneSummary tune)
    {
        var output = Detail(tune, "PyTorch CUDA");
        return HasSucceededProbe(tune, "PyTorch CUDA") &&
               output.Contains("True", StringComparison.OrdinalIgnoreCase) &&
               !output.Contains("no cuda", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeError(string output) =>
        output.Contains("No module named", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("not recognized", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("could not connect", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("error", StringComparison.OrdinalIgnoreCase);

    private static bool HasModelCache(TuneSummary tune) =>
        tune.StorageHotspots.Any(hotspot =>
            hotspot.Location.Contains(".ollama", StringComparison.OrdinalIgnoreCase) &&
            hotspot.Bytes > 0);

    private static string ModelCacheDetail(TuneSummary tune)
    {
        var cache = tune.StorageHotspots.FirstOrDefault(hotspot =>
            hotspot.Location.Contains(".ollama", StringComparison.OrdinalIgnoreCase));
        return cache is null ? "" : $"{cache.Location}: {Formatting.Size(cache.Bytes)}";
    }

    private static string WorkloadClass(bool hasGpu, List<LocalAiSignal> signals)
    {
        if (!hasGpu)
        {
            return "CPU/local-service only; use small models or remote endpoints for heavier work.";
        }

        var gpuDetail = signals.First(signal => signal.Name == "NVIDIA GPU").Detail;
        if (gpuDetail.Contains("6144", StringComparison.OrdinalIgnoreCase) ||
            gpuDetail.Contains("6 GB", StringComparison.OrdinalIgnoreCase) ||
            gpuDetail.Contains("1660", StringComparison.OrdinalIgnoreCase))
        {
            return "Small-model/autoresearch worker: 3B-8B quantized inference, eval sweeps, and dataset prep.";
        }

        return "GPU local-AI workstation; benchmark model size, context, and training jobs before unattended use.";
    }

    private static string RecommendedRuntime(bool hasGpu, bool hasLlamaCpp, bool hasOllama)
    {
        if (hasGpu && hasLlamaCpp)
        {
            return "llama.cpp CUDA/MMQ with full offload when VRAM allows.";
        }

        if (hasGpu && hasOllama)
        {
            return "Ollama or llama.cpp GPU inference after runtime benchmark.";
        }

        if (hasLlamaCpp || hasOllama)
        {
            return "CPU llama.cpp/Ollama for light local tasks; prefer remote or larger GPU hosts for heavy work.";
        }

        return "Install or connect a local model runtime before autoresearch.";
    }
}
