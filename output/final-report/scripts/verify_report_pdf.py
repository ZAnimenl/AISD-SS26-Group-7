from __future__ import annotations

import re
import sys
import hashlib
from pathlib import Path

from pypdf import PdfReader


EXPECTED_VIDEOS = {
    "Admin_AssessmentCreation.mp4",
    "Admin_AssessmentReport.mp4",
    "User_Assessment.mp4",
}
ONLINE_VIDEO_BASE = (
    "https://cdn.jsdelivr.net/gh/ZAnimenl/AISD-SS26-Group-7@"
    "519ee8f2b33b415d9c20c77f35316cac6ff1a863/"
    "output/final-report/media/"
)
EXPECTED_VIDEO_URLS = {
    f"{ONLINE_VIDEO_BASE}{filename}" for filename in EXPECTED_VIDEOS
}
ROOT = Path(__file__).resolve().parents[1]


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit("Usage: verify_report_pdf.py REPORT.pdf")

    pdf_path = Path(sys.argv[1]).resolve()
    reader = PdfReader(pdf_path)
    all_text = "\n".join(page.extract_text() or "" for page in reader.pages)

    richmedia = []
    browser_links = []
    richmedia_rects = []
    for page_number, page in enumerate(reader.pages, start=1):
        for annotation_ref in page.get("/Annots") or []:
            annotation = annotation_ref.get_object()
            if annotation.get("/Subtype") == "/RichMedia":
                richmedia.append((page_number, str(annotation.get("/NM"))))
                richmedia_rects.append(
                    (page_number, tuple(float(value) for value in annotation["/Rect"]))
                )
            action = annotation.get("/A")
            if action:
                uri = str(action.get_object().get("/URI", ""))
                if uri in EXPECTED_VIDEO_URLS:
                    browser_links.append(
                        (
                            page_number,
                            uri,
                            tuple(float(value) for value in annotation["/Rect"]),
                        )
                    )

    root = reader.trailer["/Root"]
    embedded_names = root["/Names"]["/EmbeddedFiles"]["/Names"]
    video_names = {str(name) for name in embedded_names[::2]}
    embedded_hashes = {}
    for index in range(0, len(embedded_names), 2):
        filename = str(embedded_names[index])
        file_spec = embedded_names[index + 1].get_object()
        embedded_data = file_spec["/EF"]["/F"].get_object().get_data()
        embedded_hashes[filename] = hashlib.sha256(embedded_data).hexdigest()
        local_data = (ROOT / "media" / filename).read_bytes()
        assert embedded_data == local_data, filename

    assert reader.pdf_header == "%PDF-1.7"
    assert len(richmedia) == 3, richmedia
    assert len(browser_links) == 3, browser_links
    assert {uri for _, uri, _ in browser_links} == EXPECTED_VIDEO_URLS
    assert sorted((page, rect) for page, _, rect in browser_links) == sorted(
        richmedia_rects
    )
    assert video_names == EXPECTED_VIDEOS, video_names
    assert len(root["/AF"]) == 3
    assert root["/Extensions"]["/ADBE"]["/ExtensionLevel"] == 3

    required_text = (
        "Deepseek__ApiKey=<your-deepseek-api-key>",
        "Weekly commit activity from the first repository record",
        "GitHub commit activity",
        "The next planned step is not immediate deployment",
        "This participant group was selected deliberately",
        "Core administrator and student processes covered by the User Guide",
        "Video 1:Administrator assessment creation",
        "Video 2:Administrator report review",
        "Video 3:Student assessment workflow",
    )
    for expected in required_text:
        assert expected in all_text, expected

    removed_captions = (
        "Assessment creation flow: define basics",
        "Administrator reports page with final",
        "Student start page before opening",
        "Student workspace with task panel",
    )
    for removed in removed_captions:
        assert removed not in all_text, removed

    assert "late July" not in all_text
    assert "Repository Contributions" not in all_text
    assert "Commit distribution on the" not in all_text

    figure_numbers = sorted(
        {int(value) for value in re.findall(r"Figure\s+(\d+):", all_text)}
    )
    assert figure_numbers == list(range(1, len(figure_numbers) + 1)), figure_numbers

    print(f"pages={len(reader.pages)}")
    print(f"richmedia={richmedia}")
    print(f"browser_links={[(page, uri) for page, uri, _ in browser_links]}")
    print(f"embedded={sorted(video_names)}")
    print(f"embedded_sha256={embedded_hashes}")
    print(f"figure_numbers={figure_numbers}")
    print(f"size_bytes={pdf_path.stat().st_size}")


if __name__ == "__main__":
    main()
