# Tune local AI profile

`LocalAiProfileBuilder` derives `TuneSummary.LocalAiProfile` from collected signals:
GPU, CUDA tooling, llama.cpp, Ollama, PyTorch CUDA, and cached models.

The contract contains `Readiness`, `WorkloadClass`, `RecommendedRuntime`,
`TrainingReady`, `TrainingGate`, and `Signals`. The builder classifies observations;
it does not launch a model or a training job. Tune's diagnostic scripts can run
runtime probes. This profile is not collected by the core desktop inventory scan.
