#!/usr/bin/env python3
import tempfile
import unittest
from pathlib import Path
from build_native import prepare_sdlgpu

class GpuDriverAuditTests(unittest.TestCase):
    def fixture(self, root):
        source = Path(root) / "lib" / "FNA3D" / "src" / "FNA3D_Driver_SDL.c"
        source.parent.mkdir(parents=True)
        source.write_text(
            "\tSDL_stack_free(resourceSetLayoutInfos);\n"
            'SDL_SetBooleanProperty(props, "SDL.gpu.device.create.vulkan.requirehardwareacceleration", true);\n'
            "\tresult = SDL_malloc(sizeof(FNA3D_Device));\n"
            "\tASSIGN_DRIVER(SDLGPU)\n"
        )
        return source

    def test_gpu_device_driver_is_logged_once(self):
        with tempfile.TemporaryDirectory() as root:
            source = self.fixture(root)
            prepare_sdlgpu(Path(root))
            patched = source.read_text()
            self.assertIn("TML_SDL_GPU_ACTIVE_DRIVER=%s", patched)
            self.assertIn("SDL_GetGPUDeviceDriver(device)", patched)
            self.assertIn("requirehardwareacceleration\", !SDL_GetHintBoolean", patched)
            self.assertEqual(patched.count("TML_SDL_GPU_ACTIVE_DRIVER="), 1)
            prepare_sdlgpu(Path(root))
            self.assertEqual(source.read_text(), patched)

    def test_unknown_upstream_context_fails_closed(self):
        with tempfile.TemporaryDirectory() as root:
            source = self.fixture(root)
            source.write_text(source.read_text().replace("\tASSIGN_DRIVER(SDLGPU)", ""))
            with self.assertRaises(SystemExit):
                prepare_sdlgpu(Path(root))

if __name__ == "__main__":
    unittest.main()
