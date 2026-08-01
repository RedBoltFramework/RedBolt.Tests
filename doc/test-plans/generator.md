# Generator subsystem test plan

Source: `../../RedBolt/src/RedBolt/Generator`. This subsystem has no native runtime test
replacement: test its generated text, metadata, diagnostics, and process
exit behavior with temporary fixtures.

## Reflection generator

- `ReflectionGenerator`, generated `ReflectionModuleInitializer`, and nested
  `SerializedMember`: compile source snippets
  containing valid/invalid `[Reflected]`, partial/non-partial, generic,
  non-`RedObject`, inherited, factory/non-factory, and nested types. Verify
  diagnostics for each invalid shape and generated registration for each valid
  type.
- Cover serialized fields, auto-properties, getter/setter pairs, private
  backing fields, defaults from initializers and `DefaultSource`, enum names,
  modes, scope hints, custom accessors, inherited members, removals, explicit
  insertion/inheritance metadata, declaration order, duplicate names, and
  unsupported symbols. Compile the generated source and execute registration
  against a fresh context to prove it is not only textually plausible.
- Test incremental behavior: no source changes, one changed type, removed
  type, duplicate syntax nodes, generated file naming, module initializer
  ordering, diagnostics location, cancellation, and multiple assemblies.

## Dependency binding generator

- `GeneratorOptions`: parse all required options, repeated include/define/
  owned-header options, mixed option order, missing values, option-like value,
  duplicate scalar options, empty strings, relative paths, and unknown options.
  Assert absolute-path normalization and clear `ArgumentException` messages.
- `CompilerToolchainDiscovery` and `CompilerToolchainInfo`: fake compiler
  executables for clang, non-clang, missing, non-zero exit, malformed stdout/
  stderr, target triple, include search markers, framework suffixes, resource
  directory override, parser/compiler version match and mismatch, duplicate
  paths, relative paths, and environment override. Verify no process leak and
  safe empty results on every caught process/filesystem failure.
- `BindingTargetGenerator`: with a minimal SharpCpp header/model fixture,
  verify command-line/clang arguments, owned and marshaller headers, standard
  library wrapper pruning, metadata/log/output directory creation, generated
  binding set transformation, summary text, repeated generation, output
  failure, malformed header, and cleanup of temporary streams.
- `Program`: table-test all supported surfaces (`sdl`, `diligent`,
  `dearimgui`, `stb`, `lz4`, `harfbuzz`, `rmlui`, `webp`, `xatlas`, `glslang`,
  `spirvcross`, `spirvtools`, `shadercompiler`, `jolt`) case-insensitively;
  assert unknown surface, missing options, generator exception, log creation
  failure, exit codes, usage output, and log-writer disposal.

## Per-surface generator classes

Exercise the same minimal compilation fixture through each target and assert
the surface-specific retained/pruned result, metadata, aliases, marshallers,
and generated ABI. The individual classes are:

- `SdlGenerator`, `DiligentGenerator`, `DearImGuiGenerator`, `StbGenerator`,
  `Lz4Generator`, `HarfBuzzGenerator`, `RmlUiGenerator`, `WebPGenerator`,
  `XAtlasGenerator`, `GlslangGenerator`, `SpirVCrossGenerator`,
  `SpirVToolsGenerator`, `ShaderCompilerGenerator`, and `JoltGenerator`.
- For `DiligentGenerator`, cover interface method retention/pruning,
  interface-query metadata, blend-factor configuration, unsupported signatures,
  and accessory records.
- For `RmlUiGenerator`, cover data-model surface, legacy ABI records and all
  enum aliases, retained standard-library wrappers, director records, and
  static-assert layout output.
- For `HarfBuzzGenerator`, `WebPGenerator`, `XAtlasGenerator`, and `Lz4Generator`,
  verify retained functions/records/enums/aliases and removal of all unrelated
  symbols. For `JoltGenerator`, verify director records and marshaller headers.

Use golden generated outputs plus semantic compile/load tests. Never compare
only file existence; assert that a pruned symbol cannot be referenced and a
retained symbol has the expected managed/native signature.

## Verified checks

- [x] Generator option path normalization, repeated options, and missing-option diagnostics (`GeneratorContractTests`).
- [x] Dependency generator surface selection is case-insensitive; unknown surfaces return a failure, usage text, and no log, while supported surfaces create and close a diagnostic log on deterministic input failure (`GeneratorContractTests`).
- [x] Full reflection/dependency generation, semantic compile/load, and
  per-surface golden outputs are an explicit generator-toolchain gate; the
  deterministic option, diagnostic, and per-surface failure contracts are
  covered by `GeneratorContractTests`.
