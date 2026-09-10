from __future__ import annotations

import argparse
import json
import shutil
import subprocess
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont


PROJECT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE = PROJECT / "ReviewBundles" / "round-05" / "videos" / "all-scenes-review.mp4"
DEFAULT_OUTPUT = PROJECT / "Deliverables" / "YouTubeCompilation"
WIDTH = 1920
HEIGHT = 1080
FPS = 30
TITLE_SECONDS = 3
EFFECT_SECONDS = 4


@dataclass(frozen=True)
class Effect:
    number: int
    english: str
    japanese: str


EFFECTS = (
    Effect(1, "Hit Flash + Invincible Blink", "ヒットフラッシュ ＋ 無敵点滅"),
    Effect(2, "Palette Swap", "パレットスワップ"),
    Effect(3, "Dissolve", "ディゾルブ"),
    Effect(4, "Outline + Inner Rim", "アウトライン ＋ インナーリム"),
    Effect(5, "Wind Vertex + Squash", "風による頂点変形 ＋ スクワッシュ"),
    Effect(6, "Glitch + RGB Split", "グリッチ ＋ RGB分離"),
    Effect(7, "Pixelate + Posterize + Dither", "ピクセル化 ＋ ポスタライズ ＋ ディザ"),
    Effect(8, "Hologram + Shine", "ホログラム ＋ シャイン"),
    Effect(9, "2D Normal Map Lighting", "2Dノーマルマップライティング"),
    Effect(10, "Mask Map Lighting", "マスクマップライティング"),
    Effect(11, "Water Reflection", "水面反射"),
    Effect(12, "World Scan + Reveal", "ワールドスキャン ＋ リビール"),
)


def run(command: list[str]) -> None:
    print(" ".join(command))
    subprocess.run(command, check=True)


def find_font(bold: bool) -> Path:
    candidates = (
        Path("C:/Windows/Fonts/meiryob.ttc") if bold else Path("C:/Windows/Fonts/meiryo.ttc"),
        Path("C:/Windows/Fonts/YuGothB.ttc") if bold else Path("C:/Windows/Fonts/YuGothR.ttc"),
        Path("C:/Windows/Fonts/arialbd.ttf") if bold else Path("C:/Windows/Fonts/arial.ttf"),
    )
    for candidate in candidates:
        if candidate.exists():
            return candidate
    raise FileNotFoundError("No suitable Japanese font was found.")


FONT_BOLD = find_font(True)
FONT_REGULAR = find_font(False)


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(FONT_BOLD if bold else FONT_REGULAR), size)


def lerp(left: int, right: int, amount: float) -> int:
    return round(left + (right - left) * amount)


def base_background() -> Image.Image:
    image = Image.new("RGB", (WIDTH, HEIGHT))
    pixels = image.load()
    top = (5, 12, 29)
    bottom = (1, 4, 13)
    for y in range(HEIGHT):
        t = y / (HEIGHT - 1)
        color = tuple(lerp(top[channel], bottom[channel], t) for channel in range(3))
        for x in range(WIDTH):
            pixels[x, y] = color

    glow = Image.new("RGBA", image.size, (0, 0, 0, 0))
    glow_draw = ImageDraw.Draw(glow)
    glow_draw.ellipse((1060, -260, 2100, 780), fill=(0, 229, 255, 85))
    glow_draw.ellipse((-420, 540, 620, 1500), fill=(255, 54, 117, 48))
    glow = glow.filter(ImageFilter.GaussianBlur(150))
    return Image.alpha_composite(image.convert("RGBA"), glow)


def text_size(draw: ImageDraw.ImageDraw, value: str, selected_font: ImageFont.FreeTypeFont) -> tuple[int, int]:
    box = draw.textbbox((0, 0), value, font=selected_font)
    return box[2] - box[0], box[3] - box[1]


def fit_font(draw: ImageDraw.ImageDraw, value: str, max_width: int, start: int, minimum: int, bold: bool) -> ImageFont.FreeTypeFont:
    size = start
    while size > minimum:
        selected = font(size, bold)
        if text_size(draw, value, selected)[0] <= max_width:
            return selected
        size -= 2
    return font(minimum, bold)


def centered_text(
    draw: ImageDraw.ImageDraw,
    y: int,
    value: str,
    selected_font: ImageFont.FreeTypeFont,
    fill: tuple[int, int, int, int],
) -> None:
    width, _ = text_size(draw, value, selected_font)
    draw.text(((WIDTH - width) / 2, y), value, font=selected_font, fill=fill)


def decorate(draw: ImageDraw.ImageDraw, active: int | None) -> None:
    draw.text((92, 66), "UNITY SHADER GRAPH", font=font(28, True), fill=(215, 240, 255, 220))
    draw.text((92, 108), "12 EFFECTS SHOWCASE", font=font(22), fill=(95, 201, 229, 210))
    draw.rounded_rectangle((92, 168, WIDTH - 92, 172), radius=2, fill=(41, 126, 160, 150))
    start_x = (WIDTH - (12 * 26 + 11 * 18)) / 2
    y = HEIGHT - 104
    for index in range(12):
        x = start_x + index * 44
        color = (255, 65, 116, 255) if active == index + 1 else (64, 119, 143, 180)
        draw.rounded_rectangle((x, y, x + 26, y + 8), radius=4, fill=color)


def create_intro_card(output: Path) -> None:
    image = base_background()
    draw = ImageDraw.Draw(image)
    decorate(draw, None)
    centered_text(draw, 315, "Unity Shader Graph", font(114, True), (235, 250, 255, 255))
    centered_text(draw, 475, "12の2D演出", font(92, True), (42, 229, 255, 255))
    centered_text(draw, 620, "12 EFFECTS SHOWCASE", font(42), (166, 213, 229, 235))
    environment = "Unity 6.5  •  URP 2D  •  Shader Graph 17.5"
    environment_font = font(28)
    environment_width, _ = text_size(draw, environment, environment_font)
    environment_box = (
        (WIDTH - environment_width) / 2 - 42,
        735,
        (WIDTH + environment_width) / 2 + 42,
        807,
    )
    draw.rounded_rectangle(environment_box, radius=36, fill=(12, 49, 70, 210), outline=(48, 198, 225, 180), width=2)
    centered_text(draw, 752, environment, environment_font, (222, 244, 251, 255))
    image.convert("RGB").save(output, "PNG", optimize=True)


def create_effect_card(effect: Effect, output: Path) -> None:
    image = base_background()
    draw = ImageDraw.Draw(image)
    decorate(draw, effect.number)

    badge = f"EFFECT  {effect.number:02d} / 12"
    badge_font = font(34, True)
    badge_width, _ = text_size(draw, badge, badge_font)
    badge_box = ((WIDTH - badge_width) / 2 - 36, 276, (WIDTH + badge_width) / 2 + 36, 342)
    draw.rounded_rectangle(badge_box, radius=33, fill=(10, 74, 98, 220), outline=(42, 229, 255, 210), width=2)
    centered_text(draw, 288, badge, badge_font, (218, 249, 255, 255))

    japanese_font = fit_font(draw, effect.japanese, 1580, 84, 56, True)
    english_font = fit_font(draw, effect.english.upper(), 1500, 54, 38, False)
    centered_text(draw, 430, effect.japanese, japanese_font, (240, 250, 255, 255))
    centered_text(draw, 585, effect.english.upper(), english_font, (44, 227, 255, 255))
    centered_text(draw, 745, "NEXT  •  4 SECOND EFFECT DEMO", font(28), (150, 196, 211, 230))
    image.convert("RGB").save(output, "PNG", optimize=True)


def encode_title_card(ffmpeg: str, image: Path, output: Path) -> None:
    run(
        [
            ffmpeg,
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-loop",
            "1",
            "-framerate",
            str(FPS),
            "-i",
            str(image),
            "-frames:v",
            str(TITLE_SECONDS * FPS),
            "-vf",
            "fade=t=in:st=0:d=0.25,fade=t=out:st=2.75:d=0.25,format=yuv420p",
            "-an",
            "-c:v",
            "libx264",
            "-preset",
            "slow",
            "-crf",
            "18",
            "-profile:v",
            "high",
            "-level",
            "4.1",
            "-r",
            str(FPS),
            "-g",
            str(FPS * 2),
            "-movflags",
            "+faststart",
            str(output),
        ]
    )


def encode_effect(ffmpeg: str, source: Path, index: int, output: Path) -> None:
    start = index * EFFECT_SECONDS
    run(
        [
            ffmpeg,
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-ss",
            f"{start:.3f}",
            "-i",
            str(source),
            "-frames:v",
            str(EFFECT_SECONDS * FPS),
            "-vf",
            "fps=30,format=yuv420p",
            "-an",
            "-c:v",
            "libx264",
            "-preset",
            "slow",
            "-crf",
            "18",
            "-profile:v",
            "high",
            "-level",
            "4.1",
            "-r",
            str(FPS),
            "-g",
            str(FPS * 2),
            "-movflags",
            "+faststart",
            str(output),
        ]
    )


def timestamp(seconds: int) -> str:
    minutes, remainder = divmod(seconds, 60)
    return f"{minutes}:{remainder:02d}"


def write_timecodes(output_root: Path) -> None:
    lines = [
        "# YouTube / Zenn 再生開始時刻",
        "",
        "- Video: `UnityShaderGraph_12Effects_1080p30.mp4`",
        f"- Total duration: `{timestamp(TITLE_SECONDS + len(EFFECTS) * (TITLE_SECONDS + EFFECT_SECONDS))}`",
        f"- Structure: 冒頭タイトル{TITLE_SECONDS}秒 → 各演出タイトル{TITLE_SECONDS}秒 → 演出{EFFECT_SECONDS}秒",
        "",
        "Zennでは各演出の「タイトル開始」を指定するのがおすすめです。",
        "",
        "| # | 演出 | タイトル開始 | 演出開始 | watch URL用 | youtu.be用 |",
        "|---:|---|---:|---:|---|---|",
    ]
    entries = []
    for index, effect in enumerate(EFFECTS):
        title_start = TITLE_SECONDS + index * (TITLE_SECONDS + EFFECT_SECONDS)
        effect_start = title_start + TITLE_SECONDS
        lines.append(
            f"| {effect.number:02d} | {effect.japanese} / {effect.english} | "
            f"`{timestamp(title_start)}` | `{timestamp(effect_start)}` | "
            f"`https://www.youtube.com/watch?v=VIDEO_ID&t={title_start}s` | "
            f"`https://youtu.be/VIDEO_ID?t={title_start}` |"
        )
        entries.append(
            {
                "number": effect.number,
                "japanese": effect.japanese,
                "english": effect.english,
                "titleStartSeconds": title_start,
                "titleStart": timestamp(title_start),
                "effectStartSeconds": effect_start,
                "effectStart": timestamp(effect_start),
            }
        )
    lines.extend(
        [
            "",
            "## 時刻一覧",
            "",
            "```text",
            "0:00  Unity Shader Graph 12の2D演出",
            *[
                f"{timestamp(TITLE_SECONDS + index * (TITLE_SECONDS + EFFECT_SECONDS))}  "
                f"{effect.number:02d}. {effect.japanese}"
                for index, effect in enumerate(EFFECTS)
            ],
            "```",
            "",
            "URL中の`VIDEO_ID`をYouTube公開後のIDへ置換してください。",
        ]
    )
    (output_root / "YOUTUBE_TIMECODES.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    (output_root / "timecodes.json").write_text(
        json.dumps(
            {
                "introSeconds": TITLE_SECONDS,
                "titleSeconds": TITLE_SECONDS,
                "effectSeconds": EFFECT_SECONDS,
                "totalSeconds": TITLE_SECONDS + len(EFFECTS) * (TITLE_SECONDS + EFFECT_SECONDS),
                "effects": entries,
            },
            ensure_ascii=False,
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )


def make_thumbnail(intro_card: Path, output: Path) -> None:
    with Image.open(intro_card) as image:
        image.resize((1280, 720), Image.Resampling.LANCZOS).convert("RGB").save(output, "PNG", optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser(description="Build a title-card YouTube compilation from the final 12-scene review video.")
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--ffmpeg", default=shutil.which("ffmpeg") or "ffmpeg")
    args = parser.parse_args()

    source = args.source.resolve()
    output_root = args.output.resolve()
    if not source.exists():
        raise SystemExit(f"Missing source video: {source}")

    cards = output_root / "title-cards"
    segments = output_root / "segments"
    cards.mkdir(parents=True, exist_ok=True)
    segments.mkdir(parents=True, exist_ok=True)

    intro_card = cards / "00_intro.png"
    create_intro_card(intro_card)
    encode_title_card(args.ffmpeg, intro_card, segments / "00_intro.mp4")

    concat_entries = [segments / "00_intro.mp4"]
    for index, effect in enumerate(EFFECTS):
        card = cards / f"{effect.number:02d}_{effect.english.lower().replace(' ', '_').replace('+', 'and')}.png"
        title_segment = segments / f"{effect.number:02d}_a_title.mp4"
        effect_segment = segments / f"{effect.number:02d}_b_effect.mp4"
        create_effect_card(effect, card)
        encode_title_card(args.ffmpeg, card, title_segment)
        encode_effect(args.ffmpeg, source, index, effect_segment)
        concat_entries.extend((title_segment, effect_segment))

    concat_file = segments / "concat.txt"
    concat_file.write_text(
        "\n".join(f"file '{path.as_posix()}'" for path in concat_entries) + "\n",
        encoding="utf-8",
    )
    final_video = output_root / "UnityShaderGraph_12Effects_1080p30.mp4"
    run(
        [
            args.ffmpeg,
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-f",
            "concat",
            "-safe",
            "0",
            "-i",
            str(concat_file),
            "-c",
            "copy",
            "-movflags",
            "+faststart",
            str(final_video),
        ]
    )
    write_timecodes(output_root)
    make_thumbnail(intro_card, output_root / "youtube-thumbnail.png")
    print(final_video)


if __name__ == "__main__":
    main()
