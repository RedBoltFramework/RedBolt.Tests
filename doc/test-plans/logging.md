# Logging subsystem test plan

Source: `../RedBolt/src/RedBolt/RedBolt/Logging/Log.cs`. Existing coverage is in
`IOTests/LogTests.cs`.

- `LogLevel`, `StoredLogMessage`, and `LogFileParams`: assert enum ordering,
  default payloads, raw/normal level values, value-copy independence, and all
  file rotation defaults/boundaries.
- `Logger` and nested `LoggerHandle`: unbound logger no-op, named/default
  logger cloning, level filtering, Microsoft `ILogger` level conversion,
  formatted messages with escaped braces/missing or extra arguments/nulls,
  exception formatting, unsupported levels, scope behavior, and callback/
  formatter exceptions. Verify clones share the intended log sink without
  double ownership.
- `LogImpl` and `MessageInfo`: open/close/reopen, directory creation, append,
  rotate-on-open, rotate-by-size, zero/max file limits, exact format tokens,
  quiet console mode, duplicate error suppression and five-second expiry,
  skipped-message summary, concurrent writes, flush on every message, file
  permission/IO failure, and disposal after partial open.
- `Log`: construction/subscription, level and format/quiet setters, enable
  boundaries, main-thread immediate message event, worker-thread queue and
  end-frame pump, message sender/level/timestamp/logger payload, nested log
  recursion guard, invalid level normalization, file-logging unsupported
  platform, null/empty/null-device filename, fallback basic open, close,
  repeated dispose, and context teardown.

Use temporary files and a fake `RuntimeServices.Platform` for all filesystem
branches. Add a race test that logs from several workers while pumping end
frames, then assert no lost/corrupt messages and no event after disposal.

## Verified checks

- [x] Log value defaults/copy semantics, logger cloning, main-thread dispatch, and worker-thread end-frame dispatch (`IOTests/LogTests`).
- [ ] File rotation, duplicate suppression, concurrent sink writes, and disposal-failure matrix still need isolated temporary-sink coverage.
