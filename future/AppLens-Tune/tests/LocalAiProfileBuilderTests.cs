namespace AppLens.Backend.Tests;

public sealed class LocalAiProfileBuilderTests
{
    [Fact]
    public void Cuda_llama_cpp_profile_is_inference_ready_but_training_gated()
    {
        var tune = new TuneSummary
        {
            ToolProbes =
            [
                new ToolProbe { Name = "NVIDIA GPU", Status = "Succeeded", Output = "NVIDIA GeForce GTX 1660 SUPER, 6144 MiB" },
                new ToolProbe { Name = "CUDA Compiler", Status = "Succeeded", Output = "nvcc: NVIDIA (R) Cuda compiler driver" },
                new ToolProbe { Name = "llama.cpp CUDA-MMQ", Status = "Succeeded", Output = "llama-cli llama-server llama-bench" },
                new ToolProbe { Name = "PyTorch CUDA", Status = "Skipped", Output = "ModuleNotFoundError: No module named 'torch'" }
            ],
            StorageHotspots =
            [
                new StorageHotspot { Location = ".ollama", Bytes = 4L * 1024 * 1024 * 1024 }
            ]
        };

        var profile = new LocalAiProfileBuilder().Build(tune);

        Assert.Equal(LocalAiReadiness.InferenceReady, profile.Readiness);
        Assert.False(profile.TrainingReady);
        Assert.Contains("small-model", profile.WorkloadClass, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(profile.Signals, signal =>
            signal.Name == "llama.cpp" &&
            signal.Status == LocalAiSignalStatus.Present);
    }

    [Fact]
    public void Missing_gpu_keeps_autoresearch_readiness_limited()
    {
        var tune = new TuneSummary
        {
            ToolProbes =
            [
                new ToolProbe { Name = "NVIDIA GPU", Status = "Skipped", Output = "nvidia-smi not found" },
                new ToolProbe { Name = "Ollama Summary", Status = "Succeeded", Output = "NAME ID SIZE MODIFIED" }
            ]
        };

        var profile = new LocalAiProfileBuilder().Build(tune);

        Assert.Equal(LocalAiReadiness.Limited, profile.Readiness);
        Assert.False(profile.TrainingReady);
        Assert.Contains("CPU", profile.RecommendedRuntime, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Probe_error_output_does_not_count_as_present_runtime()
    {
        var tune = new TuneSummary
        {
            ToolProbes =
            [
                new ToolProbe { Name = "PyTorch CUDA", Status = "Succeeded", Output = "ModuleNotFoundError: No module named 'torch'" }
            ]
        };

        var profile = new LocalAiProfileBuilder().Build(tune);

        Assert.Contains(profile.Signals, signal =>
            signal.Name == "PyTorch CUDA" &&
            signal.Status == LocalAiSignalStatus.Missing);
        Assert.False(profile.TrainingReady);
    }

    [Fact]
    public void PyTorch_without_cuda_does_not_open_training_gate()
    {
        var tune = new TuneSummary
        {
            ToolProbes =
            [
                new ToolProbe { Name = "NVIDIA GPU", Status = "Succeeded", Output = "NVIDIA GeForce RTX, 12288 MiB" },
                new ToolProbe { Name = "PyTorch CUDA", Status = "Succeeded", Output = "2.9.0 False no cuda" }
            ]
        };

        var profile = new LocalAiProfileBuilder().Build(tune);

        Assert.Contains(profile.Signals, signal =>
            signal.Name == "PyTorch CUDA" &&
            signal.Status == LocalAiSignalStatus.Missing);
        Assert.False(profile.TrainingReady);
        Assert.NotEqual(LocalAiReadiness.TrainingReady, profile.Readiness);
    }
}
