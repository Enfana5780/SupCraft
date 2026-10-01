"""Frozen macOS SUPer entry point. No GPU preference is exposed to the user."""
import multiprocessing
import os
from pathlib import Path
import runpy
import sys


def main():
    multiprocessing.freeze_support()
    # Numba and OpenCL caches must not be written into the signed app bundle.
    cache = Path.home() / "Library" / "Caches" / "SupCraft"
    cache.mkdir(parents=True, exist_ok=True)
    os.environ.setdefault("NUMBA_CACHE_DIR", str(cache / "numba"))
    os.environ.setdefault("XDG_CACHE_HOME", str(cache))
    os.environ.setdefault("PYOPENCL_COMPILER_OUTPUT", "0")
    from gpu_fallback import install
    install()
    root = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parent))
    runpy.run_path(str(root / "supercli.py"), run_name="__main__")


if __name__ == "__main__":
    main()
