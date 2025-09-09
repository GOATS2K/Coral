# coral-ml-api
An API server to get embeddings for tracks, used for track recommendations.

## Usage
```bash
$ uv venv
$ source .venv/bin/activate
$ uv sync
$ fastapi dev main.py
```

## Setting up WSL2 with Cuda

This will only work on Ubuntu.

```bash
$ wget https://developer.download.nvidia.com/compute/cuda/repos/wsl-ubuntu/x86_64/cuda-keyring_1.1-1_all.deb
$ sudo dpkg -i cuda-keyring_1.1-1_all.deb
$ sudo apt-get update
$ sudo apt-get -y install cuda-toolkit-11-1
$ sudo apt install nvidia-cudnn
$ export LD_LIBRARY_PATH=/usr/local/cuda-11.1/targets/x86_64-linux/lib:$LD_LIBRARY_PATH
```

If CUDA was installed correctly, you should see `fastapi dev main.py` print the following logs:


```
2025-09-09 02:53:35.095904: I tensorflow/core/common_runtime/gpu/gpu_device.cc:1418] Created TensorFlow device (/job:localhost/replica:0/task:0/device:GPU:0 with 7417 MB memory) -> physical GPU (device: 0, name: NVIDIA GeForce RTX 3080, pci bus id: 0000:17:00.0, compute capability: 8.6)
```
