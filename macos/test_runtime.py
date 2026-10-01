"""Check GPU preference and narrow fallback without requiring OpenCL hardware."""
import contextlib
import importlib.util
import io
from pathlib import Path
import sys
import types


spec = importlib.util.spec_from_file_location("gpu_fallback", Path(__file__).parent / "runtime/gpu_fallback.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class OpenCLError(Exception):
    pass


calls = []
ssim = types.ModuleType("SSIM_PIL.__main__")
ssim._gpu_available = True
ssim._msg = "no devices"
def compare(a, b, tile_size=7, GPU=True):
    calls.append(GPU)
    if GPU:
        raise OpenCLError("device reset")
    return 0.99
ssim.compare_ssim = compare
utils = types.ModuleType("SUPer.utils")
package = types.ModuleType("SUPer"); package.utils = utils
pyopencl = types.ModuleType("pyopencl"); pyopencl.Error = OpenCLError
sys.modules.update({"SSIM_PIL.__main__": ssim, "SUPer": package, "pyopencl": pyopencl})
with contextlib.redirect_stderr(io.StringIO()) as log:
    module.install()
    assert utils.compare_ssim(None, None) == 0.99
    assert utils.compare_ssim(None, None) == 0.99
assert calls == [True, False, False], calls
assert "falls back to CPU" in log.getvalue()
# Simulate a fresh spawn worker: imports the wrapper without calling install().
worker = importlib.util.module_from_spec(spec); spec.loader.exec_module(worker); calls.clear()
with contextlib.redirect_stderr(io.StringIO()) as log:
    assert worker.compare_ssim(None, None) == 0.99
    assert worker.compare_ssim(None, None) == 0.99
assert calls == [True, False, False], calls
assert log.getvalue().count("falls back to CPU") == 1
ssim._gpu_available = False; calls.clear()
with contextlib.redirect_stderr(io.StringIO()) as log:
    module.install(); utils.compare_ssim(None, None)
assert calls == [False] and "OpenCL unavailable" in log.getvalue()
def invalid(*args, **kwargs):
    raise ValueError("bad images")
ssim.compare_ssim = invalid
module.install()
try:
    utils.compare_ssim(None, None)
    raise AssertionError("Unexpected exception was swallowed")
except ValueError:
    pass
print("PASS: GPU requested by default, CLI and spawn workers fall back once, unrelated errors preserved.")
