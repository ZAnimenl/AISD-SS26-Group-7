from __future__ import annotations

import hashlib
import sys
from datetime import datetime
from pathlib import Path

from PIL import Image
from pypdf import PdfReader, PdfWriter
from pypdf.generic import (
    ArrayObject,
    BooleanObject,
    ByteStringObject,
    DecodedStreamObject,
    DictionaryObject,
    FloatObject,
    NameObject,
    NumberObject,
    TextStringObject,
)


ROOT = Path(__file__).resolve().parents[1]
ONLINE_VIDEO_BASE = (
    "https://raw.githubusercontent.com/ZAnimenl/AISD-SS26-Group-7/"
    "main/output/final-report/media"
)
VIDEO_ASSETS = {
    "Admin_AssessmentCreation.mp4": (
        ROOT / "media/Admin_AssessmentCreation.mp4",
        ROOT / "figures/video-admin-assessment-creation.jpg",
    ),
    "Admin_AssessmentReport.mp4": (
        ROOT / "media/Admin_AssessmentReport.mp4",
        ROOT / "figures/video-admin-assessment-report.jpg",
    ),
    "User_Assessment.mp4": (
        ROOT / "media/User_Assessment.mp4",
        ROOT / "figures/video-user-assessment.jpg",
    ),
}
VIDEO_URLS = {
    filename: f"{ONLINE_VIDEO_BASE}/{filename}" for filename in VIDEO_ASSETS
}


def pdf_date(path: Path) -> TextStringObject:
    modified = datetime.fromtimestamp(path.stat().st_mtime).astimezone()
    offset = modified.strftime("%z")
    if offset:
        offset = f"{offset[:3]}'{offset[3:]}'"
    return TextStringObject(modified.strftime("D:%Y%m%d%H%M%S") + offset)


def make_poster_appearance(
    writer: PdfWriter, poster_path: Path, width: float, height: float
):
    with Image.open(poster_path) as image:
        image_width, image_height = image.size

    image_stream = DecodedStreamObject()
    image_stream.set_data(poster_path.read_bytes())
    image_stream.update(
        {
            NameObject("/Type"): NameObject("/XObject"),
            NameObject("/Subtype"): NameObject("/Image"),
            NameObject("/Width"): NumberObject(image_width),
            NameObject("/Height"): NumberObject(image_height),
            NameObject("/ColorSpace"): NameObject("/DeviceRGB"),
            NameObject("/BitsPerComponent"): NumberObject(8),
            NameObject("/Filter"): NameObject("/DCTDecode"),
        }
    )
    image_ref = writer._add_object(image_stream)

    form_stream = DecodedStreamObject()
    form_stream.set_data(
        f"q\n{width:.4f} 0 0 {height:.4f} 0 0 cm\n/PosterImage Do\nQ\n".encode(
            "ascii"
        )
    )
    form_stream.update(
        {
            NameObject("/Type"): NameObject("/XObject"),
            NameObject("/Subtype"): NameObject("/Form"),
            NameObject("/FormType"): NumberObject(1),
            NameObject("/BBox"): ArrayObject(
                [
                    FloatObject(0),
                    FloatObject(0),
                    FloatObject(width),
                    FloatObject(height),
                ]
            ),
            NameObject("/Resources"): DictionaryObject(
                {
                    NameObject("/XObject"): DictionaryObject(
                        {NameObject("/PosterImage"): image_ref}
                    )
                }
            ),
        }
    )
    return writer._add_object(form_stream)


def make_richmedia_annotation(
    writer: PdfWriter,
    rect: ArrayObject,
    filename: str,
    video_path: Path,
    poster_path: Path,
):
    video_data = video_path.read_bytes()
    params = DictionaryObject(
        {
            NameObject("/Size"): NumberObject(len(video_data)),
            NameObject("/CheckSum"): ByteStringObject(
                hashlib.md5(video_data).digest()
            ),
            NameObject("/ModDate"): pdf_date(video_path),
        }
    )

    embedded_file = DecodedStreamObject()
    embedded_file.set_data(video_data)
    embedded_file.update(
        {
            NameObject("/Type"): NameObject("/EmbeddedFile"),
            NameObject("/Subtype"): NameObject("/video/mp4"),
            NameObject("/Params"): params,
        }
    )
    embedded_file_ref = writer._add_object(embedded_file)

    file_spec = DictionaryObject(
        {
            NameObject("/Type"): NameObject("/Filespec"),
            NameObject("/F"): TextStringObject(filename),
            NameObject("/UF"): TextStringObject(filename),
            NameObject("/Desc"): TextStringObject(f"Embedded video demo: {filename}"),
            NameObject("/EF"): DictionaryObject(
                {
                    NameObject("/F"): embedded_file_ref,
                    NameObject("/UF"): embedded_file_ref,
                }
            ),
            NameObject("/AFRelationship"): NameObject("/Data"),
        }
    )
    file_spec_ref = writer._add_object(file_spec)

    instance = DictionaryObject(
        {
            NameObject("/Type"): NameObject("/RichMediaInstance"),
            NameObject("/Subtype"): NameObject("/Video"),
            NameObject("/Asset"): file_spec_ref,
        }
    )
    instance_ref = writer._add_object(instance)

    configuration = DictionaryObject(
        {
            NameObject("/Type"): NameObject("/RichMediaConfiguration"),
            NameObject("/Subtype"): NameObject("/Video"),
            NameObject("/Name"): TextStringObject(filename),
            NameObject("/Instances"): ArrayObject([instance_ref]),
        }
    )
    configuration_ref = writer._add_object(configuration)

    assets = DictionaryObject(
        {
            NameObject("/Names"): ArrayObject(
                [TextStringObject(filename), file_spec_ref]
            )
        }
    )
    richmedia_content = DictionaryObject(
        {
            NameObject("/Assets"): assets,
            NameObject("/Configurations"): ArrayObject([configuration_ref]),
        }
    )
    richmedia_content_ref = writer._add_object(richmedia_content)

    activation = DictionaryObject(
        {
            NameObject("/Type"): NameObject("/RichMediaActivation"),
            NameObject("/Condition"): NameObject("/XA"),
            NameObject("/Configuration"): configuration_ref,
            NameObject("/Presentation"): DictionaryObject(
                {
                    NameObject("/Style"): NameObject("/Embedded"),
                    NameObject("/Toolbar"): BooleanObject(True),
                    NameObject("/NavigationPane"): BooleanObject(False),
                    NameObject("/PassContextClick"): BooleanObject(False),
                    NameObject("/Transparent"): BooleanObject(False),
                }
            ),
        }
    )
    deactivation = DictionaryObject(
        {
            NameObject("/Type"): NameObject("/RichMediaDeactivation"),
            NameObject("/Condition"): NameObject("/XD"),
        }
    )

    width = float(rect[2]) - float(rect[0])
    height = float(rect[3]) - float(rect[1])
    poster_appearance_ref = make_poster_appearance(
        writer, poster_path, width, height
    )

    annotation = DictionaryObject(
        {
            NameObject("/Type"): NameObject("/Annot"),
            NameObject("/Subtype"): NameObject("/RichMedia"),
            NameObject("/Rect"): rect,
            NameObject("/Border"): ArrayObject(
                [NumberObject(0), NumberObject(0), NumberObject(0)]
            ),
            NameObject("/Contents"): TextStringObject(
                f"Embedded video demo: {filename}. Acrobat can play the embedded "
                "copy; browser PDF viewers use the overlapping HTTPS link."
            ),
            NameObject("/NM"): TextStringObject(f"video-{filename}"),
            NameObject("/RichMediaContent"): richmedia_content_ref,
            NameObject("/RichMediaSettings"): DictionaryObject(
                {
                    NameObject("/Activation"): activation,
                    NameObject("/Deactivation"): deactivation,
                }
            ),
            NameObject("/AP"): DictionaryObject(
                {NameObject("/N"): poster_appearance_ref}
            ),
        }
    )
    return writer._add_object(annotation), file_spec_ref


def ensure_catalog_extensions(writer: PdfWriter, file_specs: list) -> None:
    root = writer._root_object
    writer._header = b"%PDF-1.7"
    root[NameObject("/Version")] = NameObject("/1.7")

    extensions = root.get("/Extensions")
    if extensions is None:
        extensions = DictionaryObject()
        root[NameObject("/Extensions")] = extensions
    else:
        extensions = extensions.get_object()
    extensions[NameObject("/ADBE")] = DictionaryObject(
        {
            NameObject("/BaseVersion"): NameObject("/1.7"),
            NameObject("/ExtensionLevel"): NumberObject(3),
        }
    )

    associated_files = root.get("/AF")
    if associated_files is None:
        associated_files = ArrayObject()
        root[NameObject("/AF")] = associated_files
    else:
        associated_files = associated_files.get_object()
    associated_files.extend(file_specs)

    names = root.get("/Names")
    if names is None:
        names = DictionaryObject()
        root[NameObject("/Names")] = names
    else:
        names = names.get_object()
    embedded_names = names.get("/EmbeddedFiles")
    if embedded_names is None:
        embedded_names = DictionaryObject(
            {NameObject("/Names"): ArrayObject()}
        )
        names[NameObject("/EmbeddedFiles")] = embedded_names
    else:
        embedded_names = embedded_names.get_object()
    name_array = embedded_names["/Names"].get_object()
    for filename, file_spec in zip(VIDEO_ASSETS, file_specs, strict=True):
        name_array.extend([TextStringObject(filename), file_spec])


def embed_videos(input_pdf: Path, output_pdf: Path) -> int:
    reader = PdfReader(input_pdf)
    writer = PdfWriter()
    writer.clone_document_from_reader(reader)

    embedded_count = 0
    file_specs = []
    for page in writer.pages:
        annotations = page.get("/Annots")
        if annotations is None:
            continue
        annotation_array = annotations.get_object()
        for annotation_ref in list(annotation_array):
            annotation = annotation_ref.get_object()
            if annotation.get("/Subtype") != "/Link":
                continue
            action = annotation.get("/A")
            if action is None:
                continue
            action = action.get_object()
            if action.get("/S") != "/URI":
                continue
            uri = str(action.get("/URI", ""))
            filename = next(
                (name for name, url in VIDEO_URLS.items() if uri == url), None
            )
            if filename is None:
                continue

            video_path, poster_path = VIDEO_ASSETS[filename]
            if not video_path.exists() or not poster_path.exists():
                raise FileNotFoundError(f"Missing asset for {filename}")

            richmedia_ref, file_spec_ref = make_richmedia_annotation(
                writer,
                annotation["/Rect"],
                filename,
                video_path,
                poster_path,
            )
            # Keep the standard HTTPS Link annotation for browser PDF viewers.
            # Acrobat sees the RichMedia annotation appended above the same area.
            annotation_array.append(richmedia_ref)
            file_specs.append(file_spec_ref)
            embedded_count += 1

    if embedded_count != len(VIDEO_ASSETS):
        raise RuntimeError(
            f"Expected {len(VIDEO_ASSETS)} video markers, found {embedded_count}"
        )

    ensure_catalog_extensions(writer, file_specs)
    output_pdf.parent.mkdir(parents=True, exist_ok=True)
    with output_pdf.open("wb") as output_stream:
        writer.write(output_stream)
    return embedded_count


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("Usage: embed_richmedia.py INPUT.pdf OUTPUT.pdf")
    input_pdf = Path(sys.argv[1]).resolve()
    output_pdf = Path(sys.argv[2]).resolve()
    count = embed_videos(input_pdf, output_pdf)
    print(f"Embedded {count} videos in {output_pdf}")


if __name__ == "__main__":
    main()
