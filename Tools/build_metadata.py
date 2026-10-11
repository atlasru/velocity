"""Emit non-secret metadata; require stable Android signing for tagged releases."""
import os
import re

version = "0.1.0"
if os.environ.get("VELOCITY_REF_TYPE") == "tag":
    tag = os.environ.get("VELOCITY_REF_NAME", "")
    if not re.fullmatch(r"v\d+\.\d+\.\d+", tag):
        raise SystemExit("Release tags must be vMAJOR.MINOR.PATCH")
    version = tag[1:]
    if os.environ.get("VELOCITY_TARGET") == "Android":
        required = ("ANDROID_KEYSTORE_BASE64", "ANDROID_KEYSTORE_PASSWORD", "ANDROID_KEY_ALIAS", "ANDROID_KEY_PASSWORD")
        missing = [key for key in required if not os.environ.get(key)]
        if missing:
            raise SystemExit("Tagged Android export requires signing secrets: " + ", ".join(missing))
code = 100 + int(os.environ.get("GITHUB_RUN_NUMBER", "1"))
output = f"version={version}\ncode={code}\n"
if os.environ.get("GITHUB_OUTPUT"):
    with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as file:
        file.write(output)
else:
    print(output, end="")
