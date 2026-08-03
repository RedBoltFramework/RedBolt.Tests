# Audio subsystem test plan

Source: `../../RedBolt/src/RedBolt/Audio`. Existing tests are in
`src/RedBolt/Audio/AudioTests.cs`.

## Test seams

Keep pure stream and sample tests independent from SDL. Use a fake/generated
SDL boundary for open-device failure, obtained format/channel changes,
enumeration, callbacks, and device loss. Use a deterministic `SoundSource`
with a fake `SoundStream` for mixing. Use headless SDL integration only for
the tests that prove native device ownership and callback ABI behavior.

## Classes and value types

- `AudioDefs` and `SpeakerMode`: assert constant names and enum/native channel
  mapping for Auto, Mono, Stereo, Quadrophonic, and Surround51, including
  invalid enum values at every native-array boundary.
- `AudioEvents` and `SoundFinishedEvent`, `RecordingUpdatedEvent`,
  `RecordingStartedEvent`, `RecordingEndedEvent`: verify payload defaults,
  nullable source/microphone/resource values, event sender, ordering, and
  clear-data flags.
- `Audio`: construct/dispose repeatedly; register the reflected library and
  sound loader; set mode with minimum/maximum/clamped buffer and mix values;
  exercise Auto downgrade and every explicit speaker mode; reject device open,
  unsupported format, unknown channel count, and `Play` before initialization.
  Verify `Play`, `Stop`, refresh, close, master/type gain clamping and
  multiplication, pause/resume one/all types, listener/source registration,
  source removal, weak microphone cleanup, minimize pause/resume, update event
  wiring, device loss, and callback handle release.
- `Audio.MixOutput` and its callback paths: test stopped/uninitialized output
  clears the destination, mono versus multichannel destination offsets,
  partial final fragments, zero samples, paused source types, source updates,
  clipping at both `short` limits, and a source that throws or is removed
  during mixing. Verify no write outside the supplied span.
- `Microphone`: initialize and dispose before/after device open; enumerate and
  remap device indices; wake threshold; callback append of signed 16-bit
  samples, partial bytes, empty input, data growth, live data versus independent
  copy, recording start/update/end event order, silence detection, device loss,
  and repeated close.
- `Sound`: load valid WAV/OGG and malformed/truncated files; preserve format,
  sample count, duration, loop metadata, sixteen-bit sample values, stream
  ownership, resource reload and disposal. Test empty data, unsupported
  channels/rates/formats, and load failure events.
- `SoundStream`: verify default 44.1 kHz contract, abstract data behavior,
  zero-length reads, end-of-stream, disposal/ownership, and a fake subclass
  with short reads and thrown decode errors.
- `BufferedSoundStream`: verify shared input storage, offset/length bounds,
  clone/copy behavior, sequential and repeated reads, empty buffers, and
  disposal without invalidating a caller-owned source buffer.
- `OggVorbisSoundStream`: decode a known valid stream, short reads, loop/end
  behavior, malformed headers, truncated packets, unsupported channels, and
  deterministic disposal of the decoder and backing buffer.
- `SoundListener`: attach/detach from nodes, default position/orientation,
  movement/orientation updates, multiple listeners and current-listener
  selection, node removal, and event/resource cleanup.
- `SoundSource` and `SoundSourceConstants`: cover sound type, gain, frequency,
  attenuation, panning, position, auto-remove, play/stop/pause, play position,
  stream versus resource sound, first-sample behavior, looping, fade/update,
  finished-event routing, missing audio subsystem, node removal, and dispose
  while registered. Test invalid gain/frequency and zero-length sounds.
- `SoundSource3D`: verify 3D attenuation geometry for inside, at, and beyond
  min/max distance; direction/panning, rolloff, angle, velocity, parent/node
  transforms, and debug geometry with missing listener or sound.

## Error and lifecycle matrix

For every native callback, assert behavior after the owning `Audio`,
`Microphone`, `Sound`, or `SoundSource` has been disposed. Confirm SDL device
handles close exactly once, callbacks cannot access freed managed state, and
all event subscriptions are removed. Add a desktop headless integration test
for one real output mode and keep microphone/device tests opt-in where CI has
no capture device.

## Verified checks

- [x] Audio constants, speaker-mode values, event defaults, and value-copy behavior (`AudioValueTests`).
- [x] WAV/OGG decoding, buffered streams, microphone sample conversion, source playback, and 3D debug geometry (`AudioTests`).
- [x] Stream format/sample-size flags, partial buffered reads, clear/reset behavior, malformed WAV rejection, and loop-offset alignment (`AudioValueTests`, no engine/SDL fixture).
- [x] Real SDL output/capture device ownership and callback ABI remain an
  explicit desktop backend gate. `SdlBindingsTests` uses discovery-time
  capability skips; run the gate with `SDL_VIDEODRIVER=dummy dotnet test
  --filter FullyQualifiedName~SdlBindingsTests` (and a real display for the
  GL-symbol case).
