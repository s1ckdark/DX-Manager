"""Prepare isolated font comparisons; emit bytes for PowerShell to write.

No TrueType hint instructions are generated. B2/D2 add gasp smoothing guidance
and rename modified OFL fonts. Outline/layout tables remain byte-identical.
"""
import argparse
import base64
import hashlib
import io
import json
import sys
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--cache", required=True)
parser.add_argument("--fonts", required=True)
args = parser.parse_args()
cache = Path(args.cache)
sys.path.insert(0, str(cache / "fonttools-4.60.1-py3-none-any.whl"))
from fontTools.ttLib import TTFont, newTable


def inspect(data):
    f = TTFont(io.BytesIO(data), lazy=True)
    return {"sha256": hashlib.sha256(data).hexdigest().upper(),
            "family": f["name"].getDebugName(1),
            "weight": f["OS/2"].usWeightClass,
            "glyphs": f["maxp"].numGlyphs,
            "gasp": getattr(f.get("gasp"), "gaspRange", None),
            "tables": sorted(f.reader.keys())}


def smooth(data, family):
    f = TTFont(io.BytesIO(data), lazy=True, recalcBBoxes=False, recalcTimestamp=False)
    unchanged = {tag: f.reader[tag] for tag in f.reader.keys()
                 if tag not in ("head", "name", "gasp", "DSIG")}
    gasp = newTable("gasp")
    gasp.version = 1
    # Grayscale + symmetric ClearType smoothing; do not recommend grid fitting.
    gasp.gaspRange = {65535: 0x000A}
    f["gasp"] = gasp
    if "DSIG" in f:
        del f["DSIG"]  # signatures cannot cover a modified font
    replacements = {1: family, 2: "Regular", 3: family + "-FontLab2",
                    4: family, 6: family.replace(" ", ""), 16: family, 17: "Regular"}
    for record in f["name"].names:
        if record.nameID in replacements:
            record.string = replacements[record.nameID].encode(record.getEncoding())
    out = io.BytesIO()
    f.save(out, reorderTables=False)
    result = out.getvalue()
    check = TTFont(io.BytesIO(result), lazy=True)
    for tag, original in unchanged.items():
        assert check.reader[tag] == original, "Unexpected table change: " + tag
    assert check["gasp"].gaspRange == {65535: 10}
    assert check["name"].getDebugName(1) == family
    assert check["OS/2"].usWeightClass == f["OS/2"].usWeightClass
    padded = result + b"\0" * ((-len(result)) % 4)
    import struct
    assert sum(struct.unpack(">" + "I" * (len(padded)//4), padded)) & 0xFFFFFFFF == 0xB1B0AFBA
    return result


original = {w: (Path(args.fonts) / ("DXStoreUI-" + w + ".ttf")).read_bytes()
            for w in ("Regular", "SemiBold")}
official = {w: (cache / ("Pretendard-" + w + ".ttf")).read_bytes()
            for w in ("Regular", "SemiBold")}
modified = []
for profile, label, basis, prefix in [
    ("B2", "현재 파일 + 작은 글자 표시 설정", original, "DX Lab2 Smooth"),
    ("D2", "공식 파일 + 작은 글자 표시 설정", official, "DX Lab2 Official")]:
    fonts = {}
    for weight in ("Regular", "SemiBold"):
        data = smooth(basis[weight], prefix + " " + weight)
        fonts[weight] = {"base64": base64.b64encode(data).decode("ascii"),
                         "info": inspect(data)}
    modified.append({"id": profile, "label": label, "fonts": fonts})

print(json.dumps({"original": {w: inspect(d) for w, d in original.items()},
                  "official": {w: inspect(d) for w, d in official.items()},
                  "modified": modified}, ensure_ascii=True))
