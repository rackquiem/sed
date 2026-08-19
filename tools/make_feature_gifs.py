from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageOps


def make_gif(
    paths: list[Path],
    output: Path,
    size: tuple[int, int],
    durations: list[int],
    crops: dict[int, tuple[int, int, int, int]] | None = None,
) -> None:
    frames = [Image.open(path).convert("RGB") for path in paths]
    for index, crop in (crops or {}).items():
        frames[index] = frames[index].crop(crop)
    resized = [ImageOps.fit(image, size, Image.Resampling.LANCZOS) for image in frames]
    output.parent.mkdir(parents=True, exist_ok=True)
    resized[0].save(
        output,
        save_all=True,
        append_images=resized[1:],
        duration=durations,
        loop=0,
        optimize=True,
        disposal=2,
    )


def main() -> int:
    if len(sys.argv) != 3:
        raise SystemExit("usage: make_feature_gifs.py FRAME_DIRECTORY OUTPUT_DIRECTORY")

    source = Path(sys.argv[1])
    output = Path(sys.argv[2])
    make_gif(
        [source / "safari" / name for name in (
            "01-safari-configured.png",
            "02-safari-results.png",
            "03-safari-predictor-button.png",
            "04-safari-prediction.png",
        )],
        output / "safari-capture-flee.gif",
        (960, 570),
        [1600, 1900, 1100, 3000],
        {3: (0, 0, 980, 582)},
    )
    make_gif(
        [source / "hidden-power" / name for name in (
            "01-hidden-power-filter.png",
            "02-method4-configured.png",
            "03-method4-searching.png",
            "04-method4-results.png",
        )],
        output / "method4-hidden-power.gif",
        (960, 570),
        [2200, 1600, 900, 3000],
        {0: (0, 400, 648, 785)},
    )
    print(f"Created feature GIF previews in {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
