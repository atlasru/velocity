"""Publish only after both exports and all required CI checks passed."""
from pathlib import Path
import hashlib
import json
import os
import subprocess

folder = Path("release")
required = [folder / name for name in ("Velocity-Windows-x64.zip", "Velocity-Android-arm64.apk", "Velocity-source.zip")]
for path in required:
    if not path.is_file() or path.stat().st_size == 0:
        raise SystemExit("Missing verified release asset: " + str(path))
checksums = folder / "SHA256SUMS.txt"
checksums.write_text("".join(hashlib.sha256(path.read_bytes()).hexdigest() + "  " + path.name + "\n" for path in required), encoding="utf-8")
notes = folder / "release-notes.md"
notes.write_text("Velocity — offline surf, bunnyhop and LAN combat.\n\nExtract the complete Windows ZIP and launch Velocity.exe, or install the ARM64 APK on Android 10+. See README and KNOWN_LIMITATIONS in the source archive for controls, LAN setup and device verification status.\n\nUnity EditMode/PlayMode tests and both GameCI exports passed for this tag. Automated export checks do not verify physical Windows/Android devices.\n", encoding="utf-8")
tag = os.environ["VELOCITY_TAG"]
repository = os.environ["GITHUB_REPOSITORY"]
view = subprocess.run(["gh", "release", "view", tag, "--repo", repository, "--json", "isDraft"], capture_output=True, text=True)
if view.returncode == 0:
    if not json.loads(view.stdout)["isDraft"]:
        raise SystemExit("Use a new tag instead of overwriting an already published release")
else:
    subprocess.run(["gh", "release", "create", tag, "--repo", repository, "--verify-tag", "--draft", "--title", "Velocity " + tag, "--notes-file", str(notes)], check=True)
subprocess.run(["gh", "release", "upload", tag, "--repo", repository, *map(str, required), str(checksums), *map(str, sorted(folder.glob("build-info-*.json"))), "--clobber"], check=True)
subprocess.run(["gh", "release", "edit", tag, "--repo", repository, "--draft=false"], check=True)
