from __future__ import annotations

import hashlib
import json
import shutil
import subprocess
import zipfile
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw


PROJECT = Path(__file__).resolve().parents[1]
REVIEW = PROJECT / "ReviewBundles"
VALIDATION_INPUT = REVIEW / "validation"


def read_capture_state() -> dict[str, str]:
    state_file = REVIEW / ".capture-state.txt"
    if not state_file.exists():
        raise SystemExit("Review capture state is missing. Run Tools > Shader Capture > Generate Review Captures first.")
    values: dict[str, str] = {}
    for line in state_file.read_text(encoding="utf-8").splitlines():
        if "=" in line:
            key, value = line.split("=", 1)
            values[key] = value
    return values


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def create_video(frame_root: Path, output: Path, start_number: int = 0, frame_count: int | None = None) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    command = [
        "ffmpeg", "-loglevel", "error", "-y", "-framerate", "30", "-start_number", str(start_number),
        "-i", str(frame_root / "frame_%04d.png")
    ]
    if frame_count is not None:
        command.extend(["-frames:v", str(frame_count)])
    command.extend([
        "-c:v", "libx264", "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p",
        "-movflags", "+faststart", str(output),
    ])
    subprocess.run(command, check=True)


def create_contact_sheet(screenshot_root: Path, output: Path) -> None:
    sources = sorted(screenshot_root.glob("*_comparison.png"))
    if len(sources) != 12:
        raise SystemExit(f"Expected 12 comparison screenshots, found {len(sources)}")
    canvas = Image.new("RGB", (1280, 960), (7, 12, 28))
    draw = ImageDraw.Draw(canvas)
    cell_w, cell_h = 426, 240
    for index, source in enumerate(sources):
        with Image.open(source) as image:
            preview = image.convert("RGB").resize((400, 225), Image.Resampling.LANCZOS)
        x = (index % 3) * cell_w + 13
        y = (index // 3) * cell_h + 5
        canvas.paste(preview, (x, y))
        label = source.name.removesuffix("_comparison.png")
        draw.rectangle((x, y + 201, x + 400, y + 225), fill=(4, 8, 18))
        draw.text((x + 8, y + 206), label, fill=(220, 244, 255))
    canvas.save(output, "PNG", optimize=True)


def create_graph_closeups(graph_root: Path, output_root: Path) -> None:
    output_root.mkdir(parents=True, exist_ok=True)
    selected = ("Effect04_", "Effect06_", "Effect08_", "Effect09_", "Effect10_")
    for source in sorted(graph_root.glob("*.png")):
        if not source.name.startswith(selected):
            continue
        with Image.open(source) as image:
            rgb = image.convert("RGB")
            left = int(rgb.width * .14)
            right = int(rgb.width * .79)
            top = int(rgb.height * .08)
            bottom = int(rgb.height * .92)
            crop = rgb.crop((left, top, right, bottom))
            crop = crop.resize((crop.width * 2, crop.height * 2), Image.Resampling.LANCZOS)
            crop.save(output_root / source.name.replace(".png", "_closeup.png"), "PNG", optimize=True)


def create_scene_closeups(screenshot_root: Path, output_root: Path) -> None:
    output_root.mkdir(parents=True, exist_ok=True)
    source = screenshot_root / "09_NormalMap2DLight_comparison.png"
    if not source.exists():
        raise SystemExit(f"Missing Scene 09 comparison screenshot: {source}")
    with Image.open(source) as image:
        rgb = image.convert("RGB")
        crop = rgb.crop((int(rgb.width * .16), int(rgb.height * .12), int(rgb.width * .84), int(rgb.height * .91)))
        crop = crop.resize((crop.width * 2, crop.height * 2), Image.Resampling.LANCZOS)
        crop.save(output_root / "09_NormalMap2DLight_visual_crop.png", "PNG", optimize=True)


def add_path_to_zip(archive: zipfile.ZipFile, source: Path, arc_prefix: Path) -> None:
    if source.is_file():
        archive.write(source, (arc_prefix / source.name).as_posix())
        return
    for file in sorted(path for path in source.rglob("*") if path.is_file() and "__pycache__" not in path.parts and path.suffix != ".pyc"):
        archive.write(file, (arc_prefix / file.relative_to(source)).as_posix())


def read_test_result(path: Path, expected_platform: str) -> dict[str, object]:
    if not path.exists():
        raise SystemExit(f"Missing {expected_platform} test result: {path}")
    run = ET.parse(path).getroot()
    result = {
        "platform": expected_platform,
        "total": int(run.attrib.get("total", "0")),
        "passed": int(run.attrib.get("passed", "0")),
        "failed": int(run.attrib.get("failed", "0")),
        "skipped": int(run.attrib.get("skipped", "0")),
        "result": run.attrib.get("result", "Unknown"),
        "modifiedUtc": datetime.fromtimestamp(path.stat().st_mtime, timezone.utc).isoformat(),
        "sha256": sha256(path),
    }
    if result["failed"] != 0 or result["result"] != "Passed" or result["passed"] != result["total"]:
        raise SystemExit(f"{expected_platform} tests are not passing: {result}")
    return result


def read_console_receipt(path: Path) -> dict[str, object]:
    if not path.exists():
        raise SystemExit(f"Missing Unity Console receipt: {path}")
    receipt = json.loads(path.read_text(encoding="utf-8"))
    if receipt.get("consoleErrors") != 0 or receipt.get("consoleWarnings") != 0:
        raise SystemExit(f"Unity Console is not clean: {receipt}")
    receipt["modifiedUtc"] = datetime.fromtimestamp(path.stat().st_mtime, timezone.utc).isoformat()
    receipt["sha256"] = sha256(path)
    return receipt


def create_source_zip(output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        add_path_to_zip(archive, PROJECT / "Assets" / "ShaderCapture", Path("Assets/ShaderCapture"))
        add_path_to_zip(archive, PROJECT / "Assets" / "Settings", Path("Assets/Settings"))
        add_path_to_zip(archive, PROJECT / "Documentation", Path("Documentation"))
        add_path_to_zip(archive, PROJECT / "Tools", Path("Tools"))
        add_path_to_zip(archive, PROJECT / "Packages", Path("Packages"))
        add_path_to_zip(archive, PROJECT / "ProjectSettings", Path("ProjectSettings"))
        for relative in [Path("README.md"), Path("IMPLEMENTATION_PLAN.md")]:
            archive.write(PROJECT / relative, relative.as_posix())


def create_evidence_zip(round_root: Path, output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        add_path_to_zip(archive, round_root / "screenshots", Path("screenshots"))
        add_path_to_zip(archive, round_root / "videos", Path("videos"))
        add_path_to_zip(archive, round_root / "shader-graphs", Path("shader-graphs"))
        add_path_to_zip(archive, round_root / "shader-graph-closeups", Path("shader-graph-closeups"))
        add_path_to_zip(archive, round_root / "scene-closeups", Path("scene-closeups"))
        add_path_to_zip(archive, round_root / "validation", Path("validation"))
        for name in ["README_REVIEW.md", "VALIDATION_REPORT.md", "review-request.md"]:
            path = round_root / name
            if path.exists():
                archive.write(path, name)


def package_versions() -> dict[str, str]:
    manifest = json.loads((PROJECT / "Packages" / "manifest.json").read_text(encoding="utf-8"))
    deps = manifest["dependencies"]
    lock = json.loads((PROJECT / "Packages" / "packages-lock.json").read_text(encoding="utf-8"))["dependencies"]
    return {
        "unity": (PROJECT / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8").splitlines()[0].split(":", 1)[1].strip(),
        "urp": deps["com.unity.render-pipelines.universal"],
        "shaderGraph": deps.get("com.unity.shadergraph", lock["com.unity.shadergraph"]["version"]),
        "recorder": deps["com.unity.recorder"],
    }


def write_readme(round_root: Path, round_number: str, versions: dict[str, str], tests: dict[str, dict[str, object]], console: dict[str, object]) -> None:
    text = f"""# Unity Shader Capture review bundle - round {round_number}

This bundle was regenerated from the current Unity project immediately before review.

- Unity: {versions['unity']}
- URP: {versions['urp']}
- Shader Graph: {versions['shaderGraph']}
- Recorder: {versions['recorder']}
- Scenes: Capture Hub plus 12 independent effect scenes
- Automated tests: EditMode {tests['editMode']['passed']}/{tests['editMode']['total']} passed; PlayMode {tests['playMode']['passed']}/{tests['playMode']['total']} passed
- Unity Console at capture: {console['consoleErrors']} errors, {console['consoleWarnings']} warnings

## Review gate

Score the implementation using the 100-point rubric in `IMPLEMENTATION_PLAN.md`. Acceptance requires **98/100 or higher and zero unresolved Critical/Major findings**. Review both appearance and functionality. Return a complete Markdown review with score breakdown, severity, affected scene/file, evidence, exact correction, and acceptance condition. Do not treat self-reported completion as proof.

## Contents

- `screenshots/00_contact_sheet.png`: all 12 effects
- `screenshots/*_comparison.png`: before/after comparison per scene
- `screenshots/*_before.png`, `*_after.png`: isolated review views
- `shader-graphs/*.png`: all 12 native-node graph overviews
- `shader-graph-closeups/*.png`: enlarged Alpha / Custom Function / Shine / Normal / Mask paths
- `scene-closeups/09_NormalMap2DLight_visual_crop.png`: enlarged flat-vs-normal visual comparison
- `videos/all-scenes-review.mp4`: 48-second, 30 fps deterministic overview
- `videos/01_*.mp4`, `03_*.mp4`, `06_*.mp4`, `08_*.mp4`-`12_*.mp4`: dedicated 4-second effect clips
- `source/UnityShaderCapture-source-round-{round_number}.zip`: reviewable source and settings
- `VALIDATION_REPORT.md`: verification evidence
- `MANIFEST.json`, `SHA256SUMS.txt`: provenance and integrity
"""
    (round_root / "README_REVIEW.md").write_text(text, encoding="utf-8")


def write_manifest(round_root: Path, round_number: str, versions: dict[str, str], tests: dict[str, dict[str, object]], console: dict[str, object]) -> None:
    files = []
    for path in sorted(file for file in round_root.rglob("*") if file.is_file() and file.name not in {"MANIFEST.json", "SHA256SUMS.txt"}):
        files.append({
            "path": path.relative_to(round_root).as_posix(),
            "bytes": path.stat().st_size,
            "modifiedUtc": datetime.fromtimestamp(path.stat().st_mtime, timezone.utc).isoformat(),
            "sha256": sha256(path),
        })
    manifest = {
        "round": int(round_number),
        "createdUtc": datetime.now(timezone.utc).isoformat(),
        "versions": versions,
        "sceneCount": 12,
        "tests": tests,
        "console": console,
        "files": files,
    }
    (round_root / "MANIFEST.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    all_files = sorted(file for file in round_root.rglob("*") if file.is_file() and file.name != "SHA256SUMS.txt")
    lines = [f"{sha256(path)}  {path.relative_to(round_root).as_posix()}" for path in all_files]
    (round_root / "SHA256SUMS.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")


def append_review_log(round_number: str, round_root: Path) -> None:
    log = REVIEW / "review-log.md"
    if not log.exists():
        log.write_text("# GPT Pro review log\n\n", encoding="utf-8")
    if f"## Round {int(round_number)}\n" in log.read_text(encoding="utf-8"):
        return
    entry = (
        f"## Round {int(round_number)}\n\n"
        f"- Generated: {datetime.now(timezone.utc).isoformat()}\n"
        f"- Bundle: `{round_root}`\n"
        "- State: pending GPT Pro review\n"
        "- Score: pending\n\n"
    )
    with log.open("a", encoding="utf-8") as stream:
        stream.write(entry)


def main() -> None:
    state = read_capture_state()
    round_number = state["round"]
    round_root = Path(state["roundRoot"])
    frame_root = Path(state["frameRoot"])
    screenshot_root = round_root / "screenshots"
    versions = package_versions()
    test_results = {
        "editMode": read_test_result(VALIDATION_INPUT / "EditMode.xml", "EditMode"),
        "playMode": read_test_result(VALIDATION_INPUT / "PlayMode.xml", "PlayMode"),
    }
    console_receipt = read_console_receipt(VALIDATION_INPUT / "console-receipt.json")
    source_roots = [PROJECT / "Assets" / "ShaderCapture", PROJECT / "Assets" / "Settings", PROJECT / "ProjectSettings", PROJECT / "Packages"]
    latest_source_mtime = max(path.stat().st_mtime for root in source_roots for path in root.rglob("*") if path.is_file())
    for name in ("EditMode.xml", "PlayMode.xml", "console-receipt.json"):
        if (VALIDATION_INPUT / name).stat().st_mtime < latest_source_mtime:
            raise SystemExit(f"Validation input is older than project source: {name}")
    validation_output = round_root / "validation"
    validation_output.mkdir(parents=True, exist_ok=True)
    for validation_file in ("EditMode.xml", "PlayMode.xml", "console-receipt.json"):
        shutil.copy2(VALIDATION_INPUT / validation_file, validation_output / validation_file)

    (round_root / "videos").mkdir(parents=True, exist_ok=True)
    (round_root / "source").mkdir(parents=True, exist_ok=True)
    (round_root / "feedback").mkdir(parents=True, exist_ok=True)
    graph_source = PROJECT / "Captures" / "ShaderGraphEvidence"
    graph_files = sorted(graph_source.glob("*.png"))
    if len(graph_files) != 12:
        raise SystemExit(f"Expected 12 Shader Graph evidence screenshots, found {len(graph_files)}")
    graph_output = round_root / "shader-graphs"
    graph_output.mkdir(parents=True, exist_ok=True)
    for graph_file in graph_files:
        shutil.copy2(graph_file, graph_output / graph_file.name)
    create_graph_closeups(graph_output, round_root / "shader-graph-closeups")
    create_scene_closeups(screenshot_root, round_root / "scene-closeups")
    create_video(frame_root, round_root / "videos" / "all-scenes-review.mp4")
    frames_per_scene = int(state.get("framesPerScene", "120"))
    dedicated = {
        1: "01_HitFlashInvincible.mp4",
        3: "03_Dissolve.mp4",
        6: "06_GlitchRgbSplit.mp4",
        8: "08_HologramShine.mp4",
        9: "09_NormalMap2DLight.mp4",
        10: "10_MaskMapLighting.mp4",
        11: "11_WaterReflection.mp4",
        12: "12_WorldScanReveal.mp4",
    }
    for scene_index, file_name in dedicated.items():
        create_video(frame_root, round_root / "videos" / file_name,
                     start_number=(scene_index - 1) * frames_per_scene, frame_count=frames_per_scene)
    create_contact_sheet(screenshot_root, screenshot_root / "00_contact_sheet.png")
    create_source_zip(round_root / "source" / f"UnityShaderCapture-source-round-{round_number}.zip")
    validation = PROJECT / "Documentation" / "VALIDATION_REPORT.md"
    if validation.exists():
        shutil.copy2(validation, round_root / "VALIDATION_REPORT.md")
    previous_scores = {2: "64/100", 3: "83/100", 4: "90/100", 5: "95/100"}
    previous_score = previous_scores.get(int(round_number), "see review-log.md")
    request = f"""# GPT Pro implementation review request - round {round_number}

Previous score: **{previous_score}**

Review the complete source ZIP and evidence ZIP against `IMPLEMENTATION_PLAN.md` and the fixed 100-point rubric.
Acceptance requires **98/100 or higher, Critical 0, and Major 0**. This is revision round {round_number} of at most 10.

For this round, independently verify every prior finding. In particular, prove from source plus PlayMode XML that Scene 10 has no `_SC_MetalLightPosition` or manual G BaseColor highlight, that Blend Style 3 Light2D targets only the `MaskMetal` Sorting Layer, and that toggling the real Light2D changes G pixels while R/B stay fixed. Also verify Scene 05 UV-only/Squash-only paths, Solo panel/heading isolation, Scene 08/09/11/12 pixel regressions, the Scene 09 visual crop, and Manifest timestamps.

Return a complete Markdown review with score breakdown, Critical/Major/Minor findings, exact evidence, corrections, and acceptance conditions. Do not accept this request's self-reported status as proof.
"""
    (round_root / "review-request.md").write_text(request, encoding="utf-8")
    write_readme(round_root, round_number, versions, test_results, console_receipt)
    create_evidence_zip(round_root, round_root / "evidence" / f"UnityShaderCapture-evidence-round-{round_number}.zip")
    write_manifest(round_root, round_number, versions, test_results, console_receipt)
    append_review_log(round_number, round_root)
    print(round_root)


if __name__ == "__main__":
    main()
