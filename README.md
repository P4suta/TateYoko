# TateYoko

Create PDF spreads for vertical text on Windows

Place two portrait pages side by side in right-to-left reading order.
Choose whether the first page starts on the right, stays alone as a cover, or follows a blank right half.

```text
Input:  1, 2, 3
Output: [ 2 | 1 ] [ blank | 3 ]
```

Conversion runs offline and preserves the static appearance of scanned pages.
Output omits OCR, bookmarks, links, and metadata.
The app rejects forms, annotations, attachments, scripts, and other active content.
It verifies a temporary output before replacing the selected destination.

## Build and run

Use Windows 11 on x64 or ARM64 with [mise](https://mise.jdx.dev/).
The packaged development app requires Windows Developer Mode.

```powershell
mise install
mise exec -- just setup
mise exec -- just ci
mise exec -- just run
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for rendered and UI checks.
Distribution and signing procedures live in [RELEASING.md](docs/RELEASING.md) and [SIGNING.md](docs/SIGNING.md).

## License

[Apache-2.0](LICENSE).
File-level copyright and license information lives in [REUSE.toml](REUSE.toml) and [LICENSES](LICENSES/).
