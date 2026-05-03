# AppLens-Tune Local LLM Profile

## Purpose

AppLens-Tune should turn machine evidence into a local-LLM runtime profile. It should not choose a model by hype or parameter count. It should measure the host, identify the safe workload class, and recommend runtime settings that can be verified with benchmarks.

## Current Prototype

`AppLens-Tune.py` now emits read-only sections for:

- NVIDIA GPU profile: driver, VRAM, current VRAM use, compute capability, power limit.
- PyTorch CUDA probe: installed state, CUDA availability, version, device, and VRAM.
- Local LLM toolchain: Git, Python, pip, uv, cmake, make, compilers, Docker, Ollama, `nvidia-smi`, and `nvcc`.
- llama.cpp builds: source checkout plus CPU, CUDA, CUDA-MMQ, and Vulkan build folders.
- Ollama cached models: offline manifest detection even when the Ollama daemon is stopped.
- Local LLM profile: backend, model target, context target, training target, safe overnight jobs, and manual-gated jobs.
- Auto-research queue: runtime, seed model, unattended-safe jobs, training gates, and stop-condition guidance.

The .NET backend now mirrors this direction with a `LocalAiProfile` contract, a read-only profile builder, exported Markdown/HTML sections, readiness highlights, and a tune-plan item that keeps training manual-gated.

## Dogfood Finding

The gaming PC is a small-GPU node:

- Ryzen 5 5600X, 32 GB RAM.
- GTX 1660 SUPER, 6 GB VRAM, compute capability 7.5.
- NVIDIA driver is present.
- Ollama is installed but not running.
- PyTorch is not installed.
- `uv` is missing; `cmake`, `ninja`, `nvcc`, Vulkan tools, and Docker are present.
- Local llama.cpp CPU, CUDA, and CUDA-MMQ builds exist under `/home/cody/local-llm/src/llama.cpp`.
- `qwen2.5:7b` is cached in Ollama as a GGUF blob and can be used directly by llama.cpp.
- CPU-only llama.cpp is too slow for quick interactive use on `qwen2.5:7b`.
- CUDA full offload works. The best measured build for this GTX 1660 SUPER is the CUDA-MMQ build.

This should be treated as a small-model/autoresearch worker, not a large fine-tune host.

## llama.cpp Benchmark

Model: `qwen2.5:7b`, Q4_K_M GGUF, 7.6B params, 4.36 GB model blob.

| Build | GPU layers | Prompt eval | Generation |
| --- | ---: | ---: | ---: |
| CUDA | 0 | 104.50 tok/s | 6.64 tok/s |
| CUDA | 20 | 137.64 tok/s | 17.39 tok/s |
| CUDA | 99 | 161.19 tok/s | 50.02 tok/s |
| CUDA-MMQ | 0 | 193.28 tok/s | 6.63 tok/s |
| CUDA-MMQ | 20 | 333.91 tok/s | 16.53 tok/s |
| CUDA-MMQ | 99 | 558.29 tok/s | 49.96 tok/s |

Recommended llama.cpp runtime for this host:

```bash
~/local-llm/src/llama.cpp/build-cuda-mmq/bin/llama-cli \
  -m ~/local-llm/models/qwen2.5-7b-ollama.gguf \
  -ngl 99 \
  -t 8
```

For service experiments, start from the same binary family:

```bash
~/local-llm/src/llama.cpp/build-cuda-mmq/bin/llama-server \
  -m ~/local-llm/models/qwen2.5-7b-ollama.gguf \
  -ngl 99 \
  -t 8 \
  --host 127.0.0.1 \
  --port 8080
```

## Recommended Profile

- Backend: GGUF inference through Ollama, Jan, or llama.cpp first.
- Model target: 3B-8B Q4/IQ4-class models.
- Inference context: start around 4k-16k and benchmark.
- Training/autoresearch context: start around 256-512 tokens.
- llama.cpp acceleration: prefer the CUDA-MMQ build with full offload (`-ngl 99`) on this GTX 1660 SUPER.
- Good jobs: read-only scans, llama.cpp/Ollama benchmarks, eval sweeps, dataset prep, tiny classifier training.
- Gated jobs: driver/CUDA changes, service changes, firmware/RF/Wi-Fi actions, and large model downloads.

## Product Boundary

Keep the separation explicit:

- AppLens measures installed apps, tools, hardware, services, storage, and runtime state.
- AppLens-Tune recommends and later applies user-approved configuration.
- LLM Tune learns from benchmark results and proposes runtime profiles.

The first metric set should be tokens/sec, time to first token, prompt eval speed, VRAM/RAM headroom, load time, crash rate, and quality tradeoff.

## Backend Contract

`TuneSummary.LocalAiProfile` captures the local AI posture without starting a model or changing the machine:

- `Readiness`: unknown, limited, inference-ready, or training-ready.
- `WorkloadClass`: plain-language machine role.
- `RecommendedRuntime`: current best runtime family.
- `TrainingReady` and `TrainingGate`: explicit training boundary.
- `Signals`: GPU, CUDA compiler, llama.cpp, Ollama, PyTorch CUDA, and model-cache evidence.

This gives AppLens-Tune and future AppLens-Tune extensions a stable place to hang benchmark results, run manifests, and user-approved training state later.
