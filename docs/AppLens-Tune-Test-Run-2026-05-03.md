# AppLens-Tune Local AI Test Run - 2026-05-03

## Scope

Controlled local-AI readiness test for the gaming PC. This run tested inference, local API access, PyTorch CUDA availability, and a tiny AppLens-Tune eval. It did not start training.

## Host

- SSH target: `cody@192.168.68.57`
- GPU: NVIDIA GeForce GTX 1660 SUPER, 6 GB VRAM
- llama.cpp source: `/home/cody/local-llm/src/llama.cpp`
- Runtime build: `/home/cody/local-llm/src/llama.cpp/build-cuda-mmq`
- Model: `/home/cody/local-llm/models/qwen2.5-7b-ollama.gguf`

## llama.cpp Server Smoke

Server command family:

```bash
~/local-llm/src/llama.cpp/build-cuda-mmq/bin/llama-server \
  -m ~/local-llm/models/qwen2.5-7b-ollama.gguf \
  -ngl 99 \
  -t 8 \
  -c 4096 \
  --parallel 1 \
  --host 127.0.0.1 \
  --port 8080
```

Results:

- Remote health check: `{"status":"ok"}`
- Remote OpenAI-compatible chat response: `AppLens local llama is ready.`
- Local SSH tunnel health check at `http://127.0.0.1:18080/health`: `{"status":"ok"}`
- Local tunneled chat response: `AppLens tunnel ready.`
- Local tunneled generation speed: about `51 tok/s`

## PyTorch CUDA Smoke

Isolated environment:

```bash
~/local-llm/envs/torch-cuda
```

Installed only into the venv:

- `torch 2.11.0+cu128`
- `numpy 2.4.4`

Smoke result:

```json
{
  "cuda_available": true,
  "cuda_version": "12.8",
  "device": "NVIDIA GeForce GTX 1660 SUPER",
  "torch": "2.11.0+cu128"
}
```

## AppLens-Tune Report

When run from the isolated torch env, AppLens-Tune reports:

- PyTorch CUDA probe: `CUDA ready`
- Runtime: `llama.cpp CUDA-MMQ`
- Seed model: cached `qwen2.5:7b`
- Training: `manual approval required`

Report path:

```bash
/home/cody/applens-cli-20260502/reports/latest-tune-torch-output.txt
```

## Tiny Eval

Eval record path:

```bash
/home/cody/local-llm/evals/applens-tune-summary-eval.jsonl
```

The eval asked the local model to summarize the AppLens-Tune report into readiness, safe next test, and remaining gate.

Measured result:

- Prompt tokens: `1206`
- Completion tokens: `83`
- Prompt eval: about `588 tok/s`
- Generation: about `47 tok/s`
- Latency: about `3.9s`

## Current Boundary

The machine is ready for local inference tests, AppLens eval sweeps, and small dataset-prep jobs. Training should start only after selecting a tiny controlled target, run manifest, stop conditions, output folder, and expected duration.
