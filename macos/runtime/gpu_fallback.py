# SPDX-License-Identifier: 0BSD
# See ../../legal/SUPCRAFT-0BSD.txt.
"""Request GPU by default, with a logged CPU fallback for OpenCL errors only."""
import importlib
import sys


_compare = None


def _create_wrapper():
    ssim = importlib.import_module("SSIM_PIL.__main__")
    original = ssim.compare_ssim
    usable = getattr(ssim, "_gpu_available", False)
    if not usable:
        print("[SupCraft] OpenCL unavailable; SSIM uses CPU: " + str(getattr(ssim, "_msg", "no device")), file=sys.stderr, flush=True)
    try:
        import pyopencl
        errors = (pyopencl.Error,)
    except ImportError:
        errors = ()

    def compare(image0, image1, tile_size=7, GPU=True):
        nonlocal usable
        try:
            return original(image0, image1, tile_size=tile_size, GPU=GPU and usable)
        except errors as error:
            if not GPU or not usable:
                raise
            usable = False
            print("[SupCraft] OpenCL image calculation failed; SSIM falls back to CPU: " + str(error), file=sys.stderr, flush=True)
            return original(image0, image1, tile_size=tile_size, GPU=False)

    return compare


def compare_ssim(image0, image1, tile_size=7, GPU=True):
    # Spawned workers import this function directly; they do not run the CLI setup.
    global _compare
    if _compare is None:
        _compare = _create_wrapper()
    return _compare(image0, image1, tile_size=tile_size, GPU=GPU)


def install():
    global _compare
    _compare = _create_wrapper()
    from SUPer import utils
    utils.compare_ssim = compare_ssim
