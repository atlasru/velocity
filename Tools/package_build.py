"""Validate actual Unity export architecture and package its complete output."""
from pathlib import Path
import hashlib
import json
import os
import shutil
import struct
import subprocess
import sys
import zipfile

root = Path(__file__).resolve().parents[1]
target = sys.argv[1]
output = root / "build" / target
dist = root / "dist"
dist.mkdir(exist_ok=True)
if target == "StandaloneWindows64":
    content = (output / "Velocity.exe").read_bytes()
    assert content[:2] == b"MZ", "Windows export is not a PE executable"
    header = struct.unpack_from("<I", content, 0x3C)[0]
    assert content[header:header+4] == b"PE\0\0" and struct.unpack_from("<H", content, header+4)[0] == 0x8664, "Executable must be x64"
    for path in ("UnityPlayer.dll", "Velocity_Data/globalgamemanagers", "Velocity_Data/Managed/Velocity.Core.dll", "Velocity_Data/Managed/Velocity.Runtime.dll"):
        assert (output / path).is_file(), "Incomplete Windows output: " + path
    archive_name = dist / "Velocity-Windows-x64.zip"
    with zipfile.ZipFile(archive_name, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path in sorted(output.rglob("*")):
            if path.is_file() and path.suffix != ".log":
                archive.write(path, "Velocity/" + path.relative_to(output).as_posix())
        for name in ("README.md", "KNOWN_LIMITATIONS.md", "LICENSE"):
            archive.write(root / name, "Velocity/" + name)
elif target == "Android":
    apk = output / "Velocity.apk"
    with zipfile.ZipFile(apk) as archive:
        assert archive.testzip() is None, "Corrupt APK"
        assert "AndroidManifest.xml" in archive.namelist(), "Missing Android manifest"
        native = [name for name in archive.namelist() if name.startswith("lib/") and name.endswith(".so")]
        assert native and all(name.startswith("lib/arm64-v8a/") for name in native), "APK must be ARM64 only"
        for name in ("lib/arm64-v8a/libunity.so", "lib/arm64-v8a/libil2cpp.so"):
            with archive.open(name) as library:
                header = library.read(64)
            assert header[:4] == b"\x7fELF" and header[4:6] == b"\x02\x01" and struct.unpack_from("<H", header, 18)[0] == 183, "Invalid ARM64 ELF: " + name
    sdk = Path(os.environ.get("ANDROID_HOME") or os.environ.get("ANDROID_SDK_ROOT") or "/usr/local/lib/android/sdk")
    verifiers = sorted((sdk / "build-tools").glob("*/apksigner"))
    assert verifiers, "SDK apksigner is required to verify APK signing"
    subprocess.run([str(verifiers[-1]), "verify", "--verbose", str(apk)], check=True)
    archive_name = dist / "Velocity-Android-arm64.apk"
    shutil.copyfile(apk, archive_name)
else:
    raise SystemExit("Unsupported target: " + target)
info = {"commit": os.environ.get("GITHUB_SHA", "local"), "version": os.environ.get("VELOCITY_VERSION", "0.1.0"), "editor": "6000.3.26f1", "target": target, "sha256": hashlib.sha256(archive_name.read_bytes()).hexdigest()}
(dist / ("build-info-" + target + ".json")).write_text(json.dumps(info, indent=2) + "\n", encoding="utf-8")
print("Verified architecture and package contents: " + archive_name.name)
print("Launching/installing this export still requires platform smoke testing.")
