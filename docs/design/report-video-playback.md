# Report Video Playback

## Problem

The final report contains three MP4 demonstrations. PDF RichMedia annotations
work in Adobe Acrobat, but browser PDF viewers do not consistently activate
them. A reader who opens the report in a browser still needs an ordinary,
portable way to reach each video.

## Options

| Option | Browser PDF viewers | Offline use | Trade-off |
| --- | --- | --- | --- |
| Embedded RichMedia only | Inconsistent | Acrobat only | Keeps one file but depends on Acrobat features. |
| HTTPS links only | Reliable while online | No | Small PDF, but no offline fallback. |
| HTTPS links plus embedded media and an offline HTML package | Reliable while online | Yes | Publishes the demo MP4s and provides a larger Acrobat PDF. |

The third option is used. Each poster keeps a standard HTTPS link to an
immutable jsDelivr URL backed by the GitHub commit that contains the matching
MP4. The CDN serves the file as `video/mp4`, so browsers open their native video
player. The generated PDF also places an Acrobat RichMedia annotation over the
same rectangle. The offline ZIP contains the PDF, a native HTML5 video gallery,
the three MP4s, and their poster images.

## State Machine

| State | Event | Guard | Next state | Side effect or failure path |
| --- | --- | --- | --- | --- |
| Report open in a browser PDF viewer | Reader selects a poster | Internet is available | Hosted video open | Browser opens the selected H.264/AAC MP4 in its native video player. |
| Report open in a browser PDF viewer | Reader selects a poster | Internet is unavailable | Link unavailable | Reader can use the extracted offline package instead. |
| Report open in Acrobat | Reader selects a poster | RichMedia is supported and trusted | Embedded playback | Acrobat reads the MP4 stored in the PDF. |
| Offline gallery open | Reader selects Play | Relative MP4 exists beside the gallery | Local playback | The browser uses its native HTML5 video controls. |
| Offline gallery open | Reader selects Play | Relative MP4 is missing | Playback error | Re-extract the complete package; no remote fallback is implied. |

## Impact Surface

- `output/final-report/` report source, generated PDF, poster images, scripts,
  public MP4 assets, and offline gallery.
- No frontend, backend, API, authentication, database, sandbox, or AI-provider
  behavior is changed.
- The videos are public demonstration assets and contain no credentials or
  configuration secrets.

## Rollback

Remove the hosted media, offline gallery, and browser URL annotations, then
regenerate the report with static posters or Acrobat-only RichMedia. No product
data or deployment state needs migration.

## Primitive Acceptance Criteria

- Selecting any of the three posters in a browser PDF viewer opens a distinct
  public HTTPS player URL for the matching MP4.
- The three browser Link annotations and three Acrobat RichMedia annotations
  occupy the same poster rectangles in the generated PDF.
- The public files are MP4 videos encoded as H.264 video with AAC audio.
- After extraction, the offline HTML gallery references only packaged relative
  MP4 and poster paths and exposes native video controls.
- Removing internet access does not affect playback from the extracted offline
  gallery.
