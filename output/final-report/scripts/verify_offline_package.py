from __future__ import annotations

import sys
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import urlparse


EXPECTED_VIDEOS = {
    "Admin_AssessmentCreation.mp4",
    "Admin_AssessmentReport.mp4",
    "User_Assessment.mp4",
}


class GalleryParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.video_controls = 0
        self.posters: list[str] = []
        self.sources: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        attributes = dict(attrs)
        if tag == "video":
            assert "controls" in attributes
            self.video_controls += 1
            poster = attributes.get("poster")
            assert poster
            self.posters.append(poster)
        elif tag == "source" and attributes.get("type") == "video/mp4":
            source = attributes.get("src")
            assert source
            self.sources.append(source)


def checked_relative_path(root: Path, value: str) -> Path:
    parsed = urlparse(value)
    assert not parsed.scheme and not parsed.netloc, value
    relative = Path(parsed.path)
    assert not relative.is_absolute() and ".." not in relative.parts, value
    resolved = (root / relative).resolve()
    assert resolved.is_relative_to(root.resolve()), value
    assert resolved.is_file(), resolved
    return resolved


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit("Usage: verify_offline_package.py PACKAGE_DIRECTORY")

    root = Path(sys.argv[1]).resolve()
    html_path = root / "offline-video-demos.html"
    report_path = root / "AISD-SS26-Group-7-final-report.pdf"
    assert html_path.is_file(), html_path
    assert report_path.is_file(), report_path

    parser = GalleryParser()
    parser.feed(html_path.read_text(encoding="utf-8"))

    assert parser.video_controls == 3, parser.video_controls
    assert len(parser.sources) == 3, parser.sources
    assert len(parser.posters) == 3, parser.posters
    source_paths = [checked_relative_path(root, value) for value in parser.sources]
    poster_paths = [checked_relative_path(root, value) for value in parser.posters]
    assert {path.name for path in source_paths} == EXPECTED_VIDEOS

    print(f"report={report_path.name}")
    print(f"videos={[path.relative_to(root).as_posix() for path in source_paths]}")
    print(f"posters={[path.relative_to(root).as_posix() for path in poster_paths]}")
    print("offline_gallery=ok")


if __name__ == "__main__":
    main()
