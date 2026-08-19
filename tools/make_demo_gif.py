from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageOps


def main() -> int:
    if len(sys.argv) != 3:
        raise SystemExit("usage: make_demo_gif.py FRAME_DIRECTORY OUTPUT_GIF")

    source = Path(sys.argv[1])
    output = Path(sys.argv[2])
    paths = [source / name for name in (
        "01-configured.png",
        "02-searching.png",
        "03-results.png",
        "04-view-in-editor.png",
        "05-pkhex-editor.png",
    )]
    frames = [Image.open(path).convert("RGB") for path in paths]
    resized = [ImageOps.fit(image, (960, 570), Image.Resampling.LANCZOS) for image in frames]
    output.parent.mkdir(parents=True, exist_ok=True)
    resized[0].save(
        output,
        save_all=True,
        append_images=resized[1:],
        duration=[1300, 850, 1800, 1000, 2400],
        loop=0,
        optimize=True,
        disposal=2,
    )
    print(f"Created {output} from {len(resized)} headlessly rendered plugin frames")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
