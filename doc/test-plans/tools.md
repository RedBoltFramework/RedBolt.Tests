# Package tool test plan

Source: `../../RedBolt/src/RedBolt/Tools/PackageTool/Program.cs`.

- `PackageTool` and nested `FileEntry`: package an empty directory, one file,
  nested files, duplicate names, zero-byte files, large files, binary data,
  path separators, timestamps/metadata, and deterministic ordering. Verify
  extraction/listing if exposed, round-trip bytes, archive header, offsets,
  compression flags, and file-entry bounds.
- Test missing input, output already exists, output parent missing, unreadable
  file, symlink/path traversal, invalid archive, truncated header/table/data,
  duplicate/colliding entries, integer overflow, and cancellation/IO failure.
  Assert partial output cleanup or an explicitly documented recoverable state.
- `Program`: verify argument parsing, help/usage, invalid command/options,
  non-zero exit codes, logging, and errors from `PackageTool` are reported
without swallowing the root cause. Use temporary directories and clean them
in `finally`.

## Verified checks

- [x] Empty/hidden-file filtering, nested files, binary round trips, compressed/uncompressed archives, listing, and package inspection (`src/RedBolt/RedBolt/IO/PackageToolTests`).
- [x] Invalid archive rejection through `PackageFile.Open`, missing-input/output-parent failures, invalid-option diagnostics, and deterministic output-conflict/up-to-date behavior (`src/RedBolt/RedBolt/IO/PackageToolTests`).
- [x] PackageTool has no extraction API or entry-name policy hook, so
  path-traversal rejection is an explicit production-API gate rather than a
  falsely claimed end-to-end test.
