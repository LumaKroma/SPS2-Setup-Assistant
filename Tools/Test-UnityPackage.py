"""Audit a native Unity export against Prepare-UnityPackage's staged manifest."""
import argparse
import hashlib
import json
import re
import tarfile
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("archive", type=Path)
parser.add_argument("manifest", type=Path)
args = parser.parse_args()
manifest = json.loads(args.manifest.read_text(encoding="utf-8-sig"))
expected = {item["path"]: item["sha256"] for item in manifest["files"]}
assert len(expected) == len(manifest["files"]), "Duplicate manifest path"
seen = set()
paths = set()
with tarfile.open(args.archive, "r:gz") as archive:
    members = archive.getmembers()
    names = [member.name.rstrip("/") for member in members]
    assert len(names) == len(set(names)), "Duplicate archive member"
    for member in members:
        assert member.isdir() or member.isfile(), "Links are not allowed"
        assert re.fullmatch(r"[a-f0-9]{32}(?:/(?:asset|asset.meta|pathname|preview.png))?/?", member.name), member.name
    for member in members:
        if not member.name.endswith("/pathname"):
            continue
        guid = member.name.split("/")[0]
        path = archive.extractfile(member).read().decode("utf-8").rstrip("\0\r\n")
        assert path not in paths, f"Duplicate asset path: {path}"
        paths.add(path)
        assert path == manifest["assetRoot"] or path.startswith(manifest["assetRoot"] + "/"), path
        meta = archive.extractfile(guid + "/asset.meta").read()
        assert re.search(rb"(?m)^guid: " + guid.encode() + rb"\s*$", meta), f"GUID mismatch: {path}"
        for name, content in ((path + ".meta", meta),):
            assert name in expected, f"Unexpected metadata: {name}"
            assert hashlib.sha256(content).hexdigest() == expected[name], f"Changed bytes: {name}"
            seen.add(name)
        if path in expected:
            content = archive.extractfile(guid + "/asset").read()
            assert hashlib.sha256(content).hexdigest() == expected[path], f"Changed bytes: {path}"
            seen.add(path)
        else:
            assert b"folderAsset: yes" in meta, f"Unlisted asset: {path}"
    assert seen == set(expected), f"Missing entries: {set(expected) - seen}"
print(json.dumps({"result": "PASS", "assets": len(paths), "files": len(seen),
                  "sha256": hashlib.sha256(args.archive.read_bytes()).hexdigest()}))
