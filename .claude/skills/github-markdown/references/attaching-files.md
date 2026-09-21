# Attaching files

Attach files to issues, PRs, discussions, and commits by dragging/dropping into
the comment box, pasting (images), or using the paperclip button. Uploads happen
immediately and the editor inserts an **anonymized URL** for the file.

## Size limits

| File type                              | Max size              |
| -------------------------------------- | --------------------- |
| Images and GIFs                        | 10 MB                 |
| Videos (free-plan repos)               | 10 MB                 |
| Videos (paid-plan repos)               | 100 MB                |
| All other files                        | 25 MB                 |

For paid-plan repos, uploading videos >10 MB requires organization membership,
outside-collaborator status, or a paid plan.

## Accessibility

- **Public repos:** uploaded files are readable without authentication.
- **Private/internal repos:** only people with repo access can view the files.

## Supported types at a glance

- **Image & media (all surfaces):** PNG, GIF, JPEG, SVG, video (MP4, MOV, WEBM).
  Use H.264 video for widest browser compatibility.
- **Documents (comment fields):** PDF, Office (DOCX/PPTX/XLSX/XLS/XLSM),
  OpenDocument (ODT/ODS/ODP/…), RTF/DOC.
- **Text & data:** TXT, MD, CSV, TSV, LOG, JSON/JSONC.
- **Code:** a broad set (C/C++/C#, JS/TS, Python, HTML, XML, YAML, SQL, …),
  plus notebooks (IPYNB), patch files, diagrams (DRAWIO), and dump/profile files.
- **Archives:** ZIP, GZ, TGZ.
- **Audio:** MP3, WAV.

## Referencing the uploaded file

The inserted URL can be used three ways in Markdown:

- **Embed as image** (media files): `![alt text](https://…/image.png)`
- **Link:** `[download](https://…/file.pdf)`
- **Leave as bare URL:** auto-links to the file.

For images inside repository files, prefer **relative links** to committed
assets (`/assets/img.png`) over upload URLs so clones work offline.

## Practical notes

- Copy-and-paste images directly into the box in most browsers.
- Uploaded URLs are anonymized (do not expose auth or account info in the path).
- An SVG attachment renders inline as an image.
- A `.patch` upload fails with an error on Linux — a known issue.
- Videos are codec-dependent on the viewer's browser; test important uploads.

## Pragmatic rules

1. Drag/drop or paste for speed; paperclip for browsing.
2. Keep images under 10 MB; link larger binaries rather than embedding.
3. Embed media with `![alt](url)`; add descriptive alt text.
4. Use relative repo paths for assets that belong in the repository itself.