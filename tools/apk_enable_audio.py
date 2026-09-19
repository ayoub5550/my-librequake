#!/usr/bin/env python3
"""Re-enable Unity audio in an APK that was built with `LQ_KEEP_AUDIO_DISABLED=1`.

The sandbox build (AGENTS.md §9.3) has to keep `m_DisableAudio: 1` because the Editor cannot
open any FMOD device there. The player reads that flag from
`assets/bin/Data/globalgamemanagers` (AudioManager, class 11), so we flip it in the APK
instead, then re-zip, zipalign and sign with a debug key.

    pip install --target ./pyx UnityPy      # once (any recent UnityPy)
    PYTHONPATH=./pyx python3 tools/apk_enable_audio.py in.apk out.apk \
        [--sdk /work/unity/editor/Editor/Data/PlaybackEngines/AndroidPlayer]

Requires the Android build-tools (zipalign, apksigner) and a JDK (keytool) — both ship
inside the Unity AndroidPlayer module, which is the default `--sdk`.
"""
import argparse
import glob
import os
import shutil
import subprocess
import sys
import tempfile
import zipfile

import UnityPy

GGM = "assets/bin/Data/globalgamemanagers"
DEFAULT_SDK = "/work/unity/editor/Editor/Data/PlaybackEngines/AndroidPlayer"


def patch_ggm(data: bytes) -> bytes:
    env = UnityPy.load(data)
    hit = 0
    for obj in env.objects:
        if obj.type.name != "AudioManager":
            continue
        tree = obj.read_typetree()
        print(f"AudioManager m_DisableAudio was {tree['m_DisableAudio']}")
        tree["m_DisableAudio"] = False
        obj.save_typetree(tree)
        hit += 1
    if hit != 1:
        sys.exit(f"expected exactly one AudioManager, found {hit}")
    return env.file.save()


def rebuild_apk(src: str, dst: str, patched: bytes) -> None:
    with zipfile.ZipFile(src) as zin, zipfile.ZipFile(dst, "w") as zout:
        for info in zin.infolist():
            if info.filename.startswith("META-INF/") and info.filename.split(".")[-1] in ("SF", "DSA", "RSA", "MF", "EC"):
                continue  # old signature
            payload = patched if info.filename == GGM else zin.read(info)
            # Android needs resources.arsc and native libs stored uncompressed (zipalign -p).
            comp = zipfile.ZIP_STORED if info.compress_type == zipfile.ZIP_STORED else zipfile.ZIP_DEFLATED
            zout.writestr(info, payload, compress_type=comp)


def tool(sdk: str, pattern: str) -> str:
    hits = sorted(glob.glob(os.path.join(sdk, pattern)))
    if not hits:
        sys.exit(f"missing tool: {pattern} under {sdk}")
    return hits[-1]


def sign(sdk: str, unsigned: str, out: str) -> None:
    zipalign = tool(sdk, "SDK/build-tools/*/zipalign")
    apksigner = tool(sdk, "SDK/build-tools/*/apksigner")
    keytool = tool(sdk, "OpenJDK/bin/keytool")
    java_home = os.path.dirname(os.path.dirname(keytool))
    env = dict(os.environ, JAVA_HOME=java_home, PATH=f"{java_home}/bin:" + os.environ["PATH"])
    ks = os.path.join(os.path.dirname(os.path.abspath(out)), "debug.keystore")
    if not os.path.exists(ks):
        subprocess.run([keytool, "-genkeypair", "-keystore", ks, "-storepass", "android", "-keypass", "android",
                        "-alias", "androiddebugkey", "-keyalg", "RSA", "-keysize", "2048", "-validity", "10000",
                        "-dname", "CN=Android Debug,O=Android,C=US"], check=True, env=env)
    aligned = unsigned + ".aligned"
    subprocess.run([zipalign, "-p", "-f", "4", unsigned, aligned], check=True)
    subprocess.run([apksigner, "sign", "--ks", ks, "--ks-pass", "pass:android", "--key-pass", "pass:android",
                    "--ks-key-alias", "androiddebugkey", "--out", out, aligned], check=True, env=env)
    subprocess.run([apksigner, "verify", out], check=True, env=env)
    os.remove(aligned)


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("src")
    ap.add_argument("dst")
    ap.add_argument("--sdk", default=DEFAULT_SDK)
    a = ap.parse_args()
    with zipfile.ZipFile(a.src) as z:
        patched = patch_ggm(z.read(GGM))
    tmp = tempfile.mkdtemp()
    try:
        unsigned = os.path.join(tmp, "unsigned.apk")
        rebuild_apk(a.src, unsigned, patched)
        sign(a.sdk, unsigned, a.dst)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    with zipfile.ZipFile(a.dst) as z:
        check = UnityPy.load(z.read(GGM))
        flags = [o.read_typetree()["m_DisableAudio"] for o in check.objects if o.type.name == "AudioManager"]
    print(f"OK {a.dst} ({os.path.getsize(a.dst):,} bytes) m_DisableAudio={flags}")


if __name__ == "__main__":
    main()
