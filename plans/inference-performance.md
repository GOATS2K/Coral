# Inference Performance Optimization

## Executive Summary

Investigation into audio embedding inference performance revealed that **model loading is not the bottleneck** - the TensorFlow inference computation itself dominates execution time. Cross-platform optimization via ONNX Runtime is the recommended path forward.

**Current Performance:** ~2s effective per track (10 workers, Intel CPU)
**Target:** Sub-second effective per track
**Strategy:** ONNX Runtime with platform-specific execution providers

---

## Benchmark Results (2026-01-14)

### Test Environment
- **CPU:** Intel (10 cores)
- **Model:** `discogs_track_embeddings-effnet-bs64-1.pb` (EfficientNet-based, 50MB)
- **Test Set:** 1000 audio tracks from SSD

### Findings

| Mode | Individual Time | Workers | CPU Usage | Memory/Instance | Effective Throughput |
|------|-----------------|---------|-----------|-----------------|---------------------|
| Single-shot | ~14s | 10 | 60% | 500MB | **~2s/track** |
| Streaming | ~16s | 10 | 80% | 600MB | **~2s/track** |

### Key Insights

1. **Model loading overhead is negligible** (~2-3s) compared to inference time (~12s)
2. **Streaming mode provides no speed advantage** - only memory stability
3. **CPU saturation occurs around 10 workers** - more workers causes contention
4. **Memory is stable** at 500-700MB per process with patched Essentia

### What We Tried

- **Streaming inference service:** Keeps TensorFlow model hot in memory, avoids process spawn overhead. Result: No meaningful speedup.
- **Worker scaling:** 10 workers optimal for 10-core CPU. More causes contention.

---

## Optimization Strategy

### Recommended: ONNX Runtime

ONNX Runtime provides cross-platform CPU optimization with platform-specific execution providers.

**Why ONNX Runtime:**
1. Single codebase for all target platforms
2. 1.5-3x typical CPU speedup (enough for sub-second target)
3. No accuracy loss (FP32 precision preserved)
4. Platform-specific optimizations via execution providers

### Platform Support Matrix

| Platform | Execution Provider | Expected Speedup |
|----------|-------------------|------------------|
| Intel x86_64 | DNNL (oneDNN) | 2-3x |
| AMD x86_64 | Default CPU | 1.5-2x |
| Apple Silicon | CoreML | 2-4x |
| ARM64 (Linux) | ACL | 1.5-2x |
| ARM64 (Android) | NNAPI | 2-3x |

### Alternative: Platform-Specific Backends

If ONNX Runtime doesn't meet targets, consider:

| Platform | Backend | Complexity |
|----------|---------|------------|
| Intel | OpenVINO | Medium |
| Apple | Core ML (native) | Medium |
| Qualcomm | QNN | High |

---

## Implementation Plan

### Phase 1: Model Conversion
```bash
# Convert TensorFlow frozen graph to ONNX
python -m tf2onnx.convert \
  --input discogs_track_embeddings-effnet-bs64-1.pb \
  --inputs signal:0 \
  --outputs PartitionedCall:1 \
  --output model.onnx
```

### Phase 2: CLI Integration

Replace TensorFlow inference in `Coral.Essentia.Cli` with ONNX Runtime:

```cpp
#include <onnxruntime_cxx_api.h>

class OnnxInference {
    Ort::Env env;
    Ort::Session session;

public:
    OnnxInference(const std::string& modelPath)
        : env(ORT_LOGGING_LEVEL_WARNING, "coral")
        , session(env, modelPath.c_str(), Ort::SessionOptions{})
    {
        // Auto-selects best EP for platform
    }

    std::vector<float> run(const std::vector<float>& melspec);
};
```

### Phase 3: Benchmark Suite

Extend `BulkInferenceBenchmarkCommand` to:
- Support `--backend` flag (tensorflow, onnx)
- Report platform/EP information
- Export results to CSV for cross-platform comparison

---

## Verification Checklist

- [ ] ONNX model produces identical embeddings to TensorFlow model
- [ ] Sub-second effective throughput on Intel
- [ ] Memory stable over 1000+ tracks
- [ ] Builds and runs on: Intel, AMD, Apple Silicon, ARM64

---

## Files Modified (Streaming Mode - Completed)

These changes provide memory stability but not speed improvement:

| File | Changes |
|------|---------|
| `essentia/.../tensorflowpredicteffnetdiscogs.cpp` | Fixed memory leak in `clearAlgos()` |
| `essentia/.../audioloader.cpp` | Fixed `av_packet_free()` leaks |
| `src/Coral.Essentia.Cli/Coral.Essentia.Cli.cpp` | Added `--stream` mode |
| `src/Coral.Services/StreamingInferenceService.cs` | Process pool with CliWrap |
| `src/Coral.Cli/Commands/BulkInferenceBenchmarkCommand.cs` | Benchmark command |

---

## Next Steps

1. Convert model to ONNX format
2. Prototype ONNX Runtime inference in CLI
3. Benchmark on Intel (primary dev machine)
4. Cross-compile and test on other platforms
