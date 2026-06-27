# NexusCentral BitNet Runtime

This runtime folder now contains a thin NexusCentral adapter around Microsoft `bitnet.cpp`.

## Contract

Desktop still launches one executable:

```bash
runtime/NexusCentral-BitNet-Runtime --model <model.gguf> --n-predict 1200 --mode thinking run-once --prompt "..." --json
```

The adapter returns the same JSON shape the desktop app already expects.

## External BitNet Source

Set `NEXUSCENTRAL_BITNET_ROOT` to a checked-out Microsoft BitNet repository, or place the repo at `Runtime/BitNet`:

```bash
git clone --recursive https://github.com/microsoft/BitNet.git Runtime/BitNet
cd Runtime/BitNet
python setup_env.py -md models/BitNet-b1.58-2B-4T -q i2_s
```

The adapter invokes `run_inference.py` from that BitNet root.
