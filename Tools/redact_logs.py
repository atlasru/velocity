"""Remove activation/signing secrets from file artifacts as well as console masking."""
from pathlib import Path
import base64
import os
import re

names = ("UNITY_LICENSE", "UNITY_EMAIL", "UNITY_PASSWORD", "UNITY_SERIAL",
         "ANDROID_KEYSTORE_BASE64", "ANDROID_KEYSTORE_PASSWORD", "ANDROID_KEY_ALIAS", "ANDROID_KEY_PASSWORD")
values = {os.environ[name] for name in names if os.environ.get(name)}
match = re.search(r'<DeveloperData Value="([^"]+)"', os.environ.get("UNITY_LICENSE", ""))
if match:
    try:
        serial = base64.b64decode(match.group(1)).decode("utf-8", errors="ignore")[4:]
        if serial:
            values.add(serial)
    except ValueError:
        pass
values.update(value.replace("\r\n", "\n") for value in list(values))
count = 0
for folder in ("build", "Logs", "artifacts/unity-tests"):
    for path in Path(folder).rglob("*"):
        if not path.is_file() or path.suffix not in (".log", ".xml"):
            continue
        original = path.read_text(encoding="utf-8", errors="replace")
        content = original
        for value in sorted(values, key=len, reverse=True):
            content = content.replace(value, "[REDACTED]")
        if content != original:
            path.write_text(content, encoding="utf-8")
            count += 1
print(f"Sanitized {count} log/result files; secret values are never printed.")
