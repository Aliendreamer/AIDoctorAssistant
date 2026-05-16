FROM ghcr.io/ds4sd/docling-serve:latest

# Upgrade PyTorch to 2.7+ with CUDA 12.8 for Blackwell (sm_120) support
RUN pip install --upgrade --no-cache-dir \
    torch \
    torchvision \
    --index-url https://download.pytorch.org/whl/cu128
