# SmrtPad — Master Development Plan

> **Audience:** GitHub Copilot (executor) + Lead Architect (reviewer)
> **Runtime target:** .NET 10 · WinUI 3 · Windows App SDK 1.8 · MSIX
> **IDE:** Visual Studio 2026 (18.4 Insiders) · PowerShell terminal
> **Workspace root:** `C:\Users\John_\source\repos\SmrtPad\`
> **Status:** Active — Free-tier largely complete; Licensing, AI, and Polish phases not yet started


---

## How to Use This Document

Each phase contains **numbered, atomic tasks**. Every task ends with one or more **commit checkpoints** (`📌 COMMIT`). Tasks marked `🧪 TEST` must have passing unit tests before the commit. Tasks marked `📝 DOCS` require `README.md` or `CHANGELOG.md` updates before the commit.

### Execution Rules

1. **Never skip a commit checkpoint.** One logical change per commit.
2. **Never commit red tests.** Run `dotnet test` after every `🧪 TEST` step; confirm green before `📌 COMMIT`.
3. **Never skip a build verification.** Zero errors and zero warnings before every `📌 COMMIT`.
4. **CHANGELOG entries appended under `[Unreleased]`** throughout development; moved to versioned headings at Store milestones.
5. **README updated at the end of every phase** that ships user-visible capability.
6. Plan revisions committed with `docs: update plan.md — <reason>`.

---

## Current Workspace State (Post-Assessment)

The workspace is a **single-project WinUI 3 monolith** with supplementary test projects. This plan works **with** the existing structure; no disruptive refactoring.

### Solution Members

| Project | TFM | Role |
|---|---|---|
| `SmrtPad/SmrtPad.csproj` | `net10.0-windows10.0.19041.0` | WinUI 3 host — entire app |
| `SmrtPad (Package)` | WAP | MSIX packaging |
| `SmrtPad.Tests/SmrtPad.Tests.csproj` | `net10.0-windows10.0.19041.0` | xUnit unit tests (16 files) |
| `SmrtPad.UITests/SmrtPad.UITests.csproj` | `net10.0-windows10.0.19041.0` | Appium UI automation (17 files) |

### Already Implemented ✅

- WinUI 3, Mica backdrop, `TabView` multi-tab; `RichEditBox` RTF editing with full formatting toolbar
- Find & Replace; Zoom 10–500%; status bar (word/char/line/col); OS spell check; print
- Open/Save RTF/TXT; multi-window (`App.NewWindow()`)
- Export: PDF (`PdfHelper`), DOCX (`DocxExportHelper`), ODT (`OdtExportHelper`), HTML (`HtmlConverterHelper`)
- Import: DOCX (`DocxImportHelper`, `DocxAltChunkExporter`), ODT/DOCX text (`DocumentImportHelper`)
- 5 built-in document templates; macro recording and playback (`MacroHelper`)
- `SettingsService` — font, theme, autosave, paper size, margins, recent files (10 max)
- Localisation — 9 locales incl. RTL (`ar-SA`, `ur-PK`); `IDialogService`, `IFileService`, `ISettingsService` with DI
- `EditorViewModel` (CommunityToolkit.Mvvm); `FileBackstageView`; `RulerHelper`, `ParagraphStyleHelper`, `RtfHelper`
- 16 unit-test files in `SmrtPad.Tests`; 17 Appium UI-automation files in `SmrtPad.UITests`

### Not Yet Implemented ❌

| Area | Missing |
|---|---|
| **Licensing** | `FeatureFlags`, `LicensePayload`, `LocalKeyValidator`, `LicenseOrchestrator`, IsPro gating in UI |
| **AI Assembly** | `SmrtPad.AI` project; `HardwareProbeService`, `AIDispatcher`, `PromptTemplates` |
| **Smart Sidebar** | Sidebar control, `AssemblyLoadContext` guard, Pro upsell dialog |
| **AI Skills** | `SummarizerSkill`, `ToneShifterSkill`, `AIRewriteSkill`, `ImageOcrSkill` |
| **Semantic Search** | `TextChunker`, `SemanticSearchService` (embedding index) |
| **Editor Completeness** | Session restore/crash recovery, Markdown→RTF bridge, `InkService`, document outline, drag tear-out |
| **Infrastructure** | No `Directory.Build.props`/`Directory.Packages.props`; TFM at `19041` not `26100` |
| **Accessibility** | No audit; missing `AutomationProperties`; no `LiveSetting` on status bar |
| **Performance** | Cold-start < 800 ms unverified |
| **Store / MSIX** | `Package.appxmanifest` not configured for Store (Publisher, associations, assets) |
| **Crash Telemetry** | No opt-in WER integration |
| **Pro Strings** | No `resw` entries for Pro UI strings |


---

## Target Solution Layout

```
SmrtPad/
├── SmrtPad.slnx / plan.md / README.md / CHANGELOG.md
├── Directory.Build.props / Directory.Packages.props     ← NEW (Phase 1)
│
├── SmrtPad/                              ← TFM upgraded to 26100 (Phase 1)
│   ├── Controls/SmartSidebar.xaml/.cs    ← NEW (Phase 4)
│   ├── Helpers/ + MarkdownToRtfConverter.cs  ← NEW (Phase 7)
│   ├── Services/
│   │   ├── IAIDispatcher.cs              ← NEW (Phase 4)
│   │   ├── SessionRestoreService.cs      ← NEW (Phase 7)
│   │   └── Licensing/                    ← NEW (Phase 2)
│   │       ├── FeatureFlags.cs / LicensePayload.cs
│   │       └── LocalKeyValidator.cs / LicenseOrchestrator.cs
│   └── Strings/                          ← + Pro strings (Phases 4+)
│
├── SmrtPad.AI/                           ← NEW class library (Phase 3)
│   ├── SmrtPad.AI.csproj  (net10.0-windows10.0.26100.0)
│   ├── AIDispatcher.cs / HardwareProbeService.cs / PromptTemplates.cs / TextChunker.cs
│   └── Skills/ SummarizerSkill.cs / ToneShifterSkill.cs / AIRewriteSkill.cs
│                SemanticSearchService.cs / ImageOcrSkill.cs
│
├── SmrtPad (Package)/                    ← Updated in Phase 9
│
├── SmrtPad.Tests/                        ← + new test files (Phases 2, 7, 8)
│   ├── Licensing/ FeatureFlagsTests.cs / LicensePayloadTests.cs
│   │             LocalKeyValidatorTests.cs / LicenseOrchestratorTests.cs
│   ├── Helpers/MarkdownToRtfConverterTests.cs
│   └── Services/ SessionRestoreServiceTests.cs / SettingsServiceCrashTelemetryTests.cs
│
├── SmrtPad.AI.Tests/                     ← NEW test project (Phase 3, net10.0)
│   ├── HardwareProbeServiceTests.cs / PromptTemplatesTests.cs
│   ├── AIDispatcherTests.cs / TextChunkerTests.cs
│   └── Skills/ SummarizerSkillTests.cs / ToneShifterSkillTests.cs / AIRewriteSkillTests.cs
│               ImageOcrSkillTests.cs / SemanticSearchServiceTests.cs
│
└── SmrtPad.UITests/                      ← + SmartSidebarUITests.cs / SemanticSearchUITests.cs
```

---

## NuGet Package Reference Strategy

Central Package Management (`Directory.Packages.props`) introduced Phase 1. All `<PackageReference>` elements omit `Version`.

### Current Packages

| Package | Version |
|---|---|
| `CommunityToolkit.Mvvm` | 8.4.0 |
| `DocumentFormat.OpenXml` | 3.4.1 |
| `Microsoft.Extensions.DependencyInjection` | 10.0.3 |
| `Microsoft.Graphics.Win2D` | 1.3.2 |
| `Microsoft.Windows.Compatibility` | 10.0.3 |
| `Microsoft.Windows.SDK.BuildTools` | 10.0.26100.7705 |
| `Microsoft.WindowsAppSDK` | 1.8.260209005 |

### Packages Added by Phase

| Package | Phase | Project(s) |
|---|---|---|
| `System.Security.Cryptography.ProtectedData` | 2 | SmrtPad |
| `Moq` | 1 | SmrtPad.Tests, SmrtPad.AI.Tests |
| `Microsoft.AI.Foundry.Local` | 3 | SmrtPad.AI |
| `Microsoft.Windows.AI.MachineLearning` | 3 | SmrtPad.AI |

---

## Test Strategy

- **xUnit** — consistent with existing projects; **Moq** — added Phase 1 for interface mocking
- **coverlet** — ≥ 80% line coverage per project; **xunit.skippablefact** — Appium tests use `[SkippableFact]`

### Naming Convention: `MethodName_StateUnderTest_ExpectedBehaviour`

### xUnit Assertion Rules (project-wide)

- Use `Assert.Contains` — not `Assert.True(collection.Contains(…))`
- Use `Assert.Empty` for count == 0; `Assert.Single` for count == 1; `Assert.Equal(n, collection.Count)` for n > 1

### Test Checkpoint Cadence

`dotnet test` full-suite run required at end of **every task** that introduces or modifies tested code. Each `📌 COMMIT` carrying `🧪 TEST` is preceded by a confirmed green run.

### Coverage Gate (`Directory.Build.props`)

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <CollectCoverage>true</CollectCoverage>
  <CoverletOutputFormat>cobertura</CoverletOutputFormat>
  <ThresholdType>line</ThresholdType>
  <Threshold>80</Threshold>
</PropertyGroup>
```

---

## Documentation Maintenance Rules

**README.md** — updated at end of every phase shipping user-visible capability.

**CHANGELOG.md** — follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/). Version milestones:

| Version | Milestone |
|---|---|
| `0.9.0` | End of Phase 7 — all features complete |
| `0.9.1` | End of Phase 8 — accessibility + performance |
| `1.0.0-rc.1` | Phase 9 — Free tier Store submission |
| `1.0.0` | Phase 10 — GA |

---

## Git Branching & Commit Convention

All development committed directly to **`master`**. Tag milestones (`v0.9.0`, `v1.0.0`).
Format: `<type>(<scope>): <subject>` — types: `feat` · `fix` · `refactor` · `test` · `docs` · `build` · `chore` · `perf` · `style`

---

---

# PHASE 1 — Infrastructure Hardening

**Goal:** Upgrade TFM, introduce Central Package Management, add Moq, create `SmrtPad.AI.Tests` shell. No feature code. All existing tests must remain green.

---

### Task 1.1 — `Directory.Build.props`

Create `Directory.Build.props` at the solution root:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
    <CollectCoverage>true</CollectCoverage>
    <CoverletOutputFormat>cobertura</CoverletOutputFormat>
    <ThresholdType>line</ThresholdType>
    <Threshold>80</Threshold>
  </PropertyGroup>
</Project>
```

> `Platforms` and `RuntimeIdentifiers` omitted — AI project (x64/ARM64) and main app (x86/x64/ARM64) differ.

📌 **COMMIT:** `build: add Directory.Build.props with coverage gate`

---

### Task 1.2 — `Directory.Packages.props`

Create `Directory.Packages.props` with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` and all current package versions. Remove all `Version` attributes from every `<PackageReference>` across all projects. Build — zero errors.

📌 **COMMIT:** `build: add Directory.Packages.props (Central Package Management)`

---

### Task 1.3 — Upgrade `SmrtPad.csproj` TFM

Change `<TargetFramework>` to `net10.0-windows10.0.26100.0` and `<TargetPlatformMinVersion>` to `10.0.22000.0`.

> **Why:** `LanguageModel`, `ExecutionProviderCatalog`, and `GenerateEmbeddingVectorsAsync` require `26100.0`.

📌 **COMMIT:** `build: upgrade SmrtPad TFM to net10.0-windows10.0.26100.0`

---

### Task 1.4 — Add Moq and Upgrade `SmrtPad.Tests` TFM

1. Add `<PackageReference Include="Moq" />` to `SmrtPad.Tests.csproj` (version in CPM).
2. Change `SmrtPad.Tests` TFM to `net10.0-windows10.0.26100.0`.
3. `dotnet test SmrtPad.Tests` — all 16 existing files must pass.

🧪 **TEST:** `dotnet test SmrtPad.Tests` — 0 failures.

📌 **COMMIT:** `build: add Moq and upgrade SmrtPad.Tests TFM to 26100`

---

### Task 1.5 — Create `SmrtPad.AI.Tests` Project Shell

Create `SmrtPad.AI.Tests/SmrtPad.AI.Tests.csproj` targeting `net10.0` (pure logic — all WinRT mocked via Moq). References: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Moq`, `coverlet.collector`. No project reference to `SmrtPad.AI` yet. Add `GlobalUsings.cs` placeholder. Add to `SmrtPad.slnx`. Run — 0 tests, 0 failures.

📌 **COMMIT:** `chore: add SmrtPad.AI.Tests project shell`

---

### Task 1.6 — Phase 1 Full Test Run

Run `dotnet test` across all projects. All must compile; `SmrtPad.Tests` fully green.

📌 **COMMIT:** `test: phase 1 full test run — all green`

---

### Phase 1 — Definition of Done

- [ ] TFM is `net10.0-windows10.0.26100.0` in `SmrtPad` and `SmrtPad.Tests`.
- [ ] `Directory.Build.props` and `Directory.Packages.props` at solution root. No `Version` attributes in any `<PackageReference>`.
- [ ] All 16 existing unit tests pass. `SmrtPad.AI.Tests` reports 0 tests, 0 failures.


---

---

# PHASE 2 — Licensing & Feature Flags

**Goal:** Implement the offline-first licence system and bitmask feature flags. Wire `IsPro` into `App.xaml.cs`. All licensing code lives under `SmrtPad/Services/Licensing/`.

---

### Task 2.1 — `FeatureFlags`

**File:** `SmrtPad/Services/Licensing/FeatureFlags.cs`

```csharp
[Flags]
public enum SmrtPadFeature : uint
{
    None           = 0,
    CoreEditor     = 1 << 0,   // Always granted
    SmartSidebar   = 1 << 1,   // Pro
    AISummarize    = 1 << 2,   // Pro
    AIToneShift    = 1 << 3,   // Pro
    SemanticSearch = 1 << 4,   // Pro
    ImageOCR       = 1 << 5,   // Pro
    AIRewrite      = 1 << 6,   // Pro
    InkAnalytics   = 1 << 7,   // Pro
    HWBadge        = 1 << 8,   // Pro
}

public static class FeatureFlags
{
    private static volatile uint _activeFlags = (uint)SmrtPadFeature.CoreEditor;

    public static bool IsEnabled(SmrtPadFeature feature) =>
        (_activeFlags & (uint)feature) == (uint)feature;

    internal static void SetProFlags()  => _activeFlags |= 0x1FE_u;  // bits 1–8
    internal static void ClearProFlags() => _activeFlags = (uint)SmrtPadFeature.CoreEditor;
    internal static void Reset()         => _activeFlags = (uint)SmrtPadFeature.CoreEditor;
}
```

🧪 **TEST** (`SmrtPad.Tests/Licensing/FeatureFlagsTests.cs` — min. 17 methods):

```
IsEnabled_CoreEditor_AlwaysTrue
IsEnabled_SmartSidebar_FalseByDefault
IsEnabled_AISummarize_FalseByDefault
IsEnabled_AllProFeatures_FalseByDefault
SetProFlags_ThenIsEnabled_SmartSidebar_True
SetProFlags_ThenIsEnabled_AISummarize_True
SetProFlags_ThenIsEnabled_AllProBits_True
SetProFlags_DoesNotClearCoreEditor
ClearProFlags_AfterSetPro_SmartSidebar_False
ClearProFlags_AfterSetPro_AISummarize_False
ClearProFlags_PreservesCoreEditor
Reset_AfterSetPro_AllProFlagsFalse
Reset_CoreEditor_StillTrue
IsEnabled_None_ReturnsFalse
IsEnabled_MultipleFlags_RequiresAllBitsSet
SetProFlags_CalledTwice_IsIdempotent
ClearProFlags_WithoutPriorSetPro_RemainsCore
```

📌 **COMMIT:** `feat(licensing): implement FeatureFlags bitmask`
📌 **COMMIT:** `test(licensing): add FeatureFlags unit tests`

---

### Task 2.2 — `LicensePayload`

**File:** `SmrtPad/Services/Licensing/LicensePayload.cs`

```csharp
public sealed class LicensePayload
{
    public required string Sku { get; init; }
    public required DateTimeOffset Expiry { get; init; }
    public required byte[] Signature { get; init; }   // Ed25519 sig over SignedBytes
    public required byte[] SignedBytes { get; init; } // UTF-8 JSON of Sku + Expiry

    public static LicensePayload Deserialize(byte[] rawBytes);
    public byte[] Serialize();
}
```

Use `System.Text.Json` for the inner `SignedBytes` JSON and the outer wrapper.

🧪 **TEST** (`SmrtPad.Tests/Licensing/LicensePayloadTests.cs` — min. 16 methods):

```
Serialize_ThenDeserialize_RoundTrips_Sku
Serialize_ThenDeserialize_RoundTrips_Expiry
Serialize_ThenDeserialize_RoundTrips_Signature
Serialize_ThenDeserialize_RoundTrips_SignedBytes
Serialize_ProducesNonEmptyByteArray
Deserialize_NullBytes_ThrowsArgumentNullException
Deserialize_EmptyBytes_ThrowsFormatException
Deserialize_MalformedJson_ThrowsFormatException
Deserialize_MissingSkuField_ThrowsFormatException
Deserialize_MissingExpiryField_ThrowsFormatException
Deserialize_MissingSignatureField_ThrowsFormatException
Deserialize_ZeroLengthSignatureBytes_Deserializes
Deserialize_MaxDateTimeOffset_RoundTrips
Deserialize_MinDateTimeOffset_RoundTrips
Serialize_EmptySignature_RoundTrips
Serialize_LargeSignatureBytes_RoundTrips
```

📌 **COMMIT:** `feat(licensing): implement LicensePayload serialisation`
📌 **COMMIT:** `test(licensing): add LicensePayload unit tests`

---

### Task 2.3 — `LocalKeyValidator`

**File:** `SmrtPad/Services/Licensing/LocalKeyValidator.cs`

Validation pipeline:
1. Read `%LOCALAPPDATA%\SmrtPad\.lic` via `ILicenseFileProvider` (abstraction for testability).
2. Decrypt with `ProtectedData.Unprotect(data, MachineEntropy(), DataProtectionScope.CurrentUser)`.
3. Deserialise to `LicensePayload`.
4. Verify Ed25519 signature via `ECDsa.VerifyData` with the embedded DER public key.
5. Check `payload.Expiry > DateTimeOffset.UtcNow`.
6. Return `true` only if all checks pass; `false` (never throw) on any failure.

```csharp
public interface ILicenseFileProvider { bool Exists { get; } byte[] ReadAllBytes(); }

public sealed class LocalKeyValidator
{
    // Base64-encoded DER Ed25519 public key — generated offline; private key never committed.
    private const string PublicKeyBase64 = "MCowBQYDK2VdAyEA…"; // replaced before first run

    public LocalKeyValidator(ILicenseFileProvider? fileProvider = null);
    public Task<bool> ValidateAsync(CancellationToken ct = default);
    public static byte[] MachineEntropy();
}
```

Generate key pair offline:

```powershell
openssl genpkey -algorithm ed25519 -out private.pem
openssl pkey -in private.pem -pubout -outform DER | certutil -encode - pub.b64
```

Store **only** the Base64 public key in source. Private key kept in a secure vault, never committed.

🧪 **TEST** (`SmrtPad.Tests/Licensing/LocalKeyValidatorTests.cs` — min. 16 methods, all using mock `ILicenseFileProvider`):

```
ValidateAsync_NoLicenseFile_ReturnsFalse
ValidateAsync_EmptyFile_ReturnsFalse
ValidateAsync_CorruptedBytes_ReturnsFalse
ValidateAsync_DecryptionFailure_ReturnsFalse
ValidateAsync_MalformedPayload_ReturnsFalse
ValidateAsync_TamperedSignature_ReturnsFalse
ValidateAsync_SignatureAllZeros_ReturnsFalse
ValidateAsync_ExpiredByOneSecond_ReturnsFalse
ValidateAsync_ExpiresAtExactNow_ReturnsFalse
ValidateAsync_ExpiresTomorrow_ReturnsTrue
ValidateAsync_ValidPayload_ReturnsTrue
ValidateAsync_CancellationRequested_ThrowsOperationCanceledException
ValidateAsync_WrongSku_ReturnsFalse
MachineEntropy_ReturnsSameValueOnSameEnvironment
MachineEntropy_ReturnsNonEmptyBytes
MachineEntropy_ReturnsAtLeast16Bytes
```

📌 **COMMIT:** `feat(licensing): implement LocalKeyValidator with DPAPI + Ed25519`
📌 **COMMIT:** `test(licensing): add LocalKeyValidator unit tests`

---

### Task 2.4 — `LicenseOrchestrator`

**File:** `SmrtPad/Services/Licensing/LicenseOrchestrator.cs`

```csharp
public interface IStoreContextAdapter
{
    Task<bool> HasProLicenseAsync(CancellationToken ct);
    event EventHandler OfflineLicensesChanged;
}

public sealed class LicenseOrchestrator
{
    public bool IsPro { get; private set; }
    public event EventHandler<bool>? ProLicenseChanged;

    public LicenseOrchestrator(IStoreContextAdapter storeAdapter, LocalKeyValidator keyValidator);
    public Task InitializeAsync(CancellationToken ct = default);  // idempotent
}
```

- **Probe A** — `IStoreContextAdapter.HasProLicenseAsync()`: queries Store add-on SKU `SmrtPadPro`.
- **Probe B** — `LocalKeyValidator.ValidateAsync()`: offline Ed25519 key.
- Either probe returning `true` → `FeatureFlags.SetProFlags()`; `IsPro = true`.
- Subscribe to `OfflineLicensesChanged` for live upgrade/downgrade mid-session.

🧪 **TEST** (`SmrtPad.Tests/Licensing/LicenseOrchestratorTests.cs` — min. 17 methods):

```
InitializeAsync_StoreProLicense_IsPro_True
InitializeAsync_StoreProLicense_SetProFlags_Called
InitializeAsync_StoreFreeLicense_LocalKeyValid_IsPro_True
InitializeAsync_StoreFreeLicense_LocalKeyInvalid_IsPro_False
InitializeAsync_BothProbesFail_IsPro_False
InitializeAsync_BothProbesFail_ProFlags_NotSet
InitializeAsync_BothProbesSucceed_IsPro_True
InitializeAsync_StoreThrows_FallsBackToLocalKey
InitializeAsync_StoreThrows_LocalKeyInvalid_IsPro_False
InitializeAsync_StoreThrows_LocalKeyValid_IsPro_True
InitializeAsync_CancellationRequested_ThrowsOperationCanceledException
InitializeAsync_CalledTwice_InitializesOnce
OfflineLicensesChanged_UpgradedToProByStore_IsPro_BecomesTrue
OfflineLicensesChanged_UpgradedToProByStore_ProLicenseChanged_Raised
OfflineLicensesChanged_DowngradedFromPro_IsPro_BecomesFalse
OfflineLicensesChanged_DowngradedFromPro_ClearProFlags_Called
ProLicenseChanged_NotRaisedWhenStateUnchanged
```

📌 **COMMIT:** `feat(licensing): implement LicenseOrchestrator (Store + DPAPI probes)`
📌 **COMMIT:** `test(licensing): add LicenseOrchestrator unit tests`

---

### Task 2.5 — Wire `LicenseOrchestrator` into DI and `App.xaml.cs`

1. Register `LicenseOrchestrator` as singleton in `App.ConfigureServices()`.
2. In `App.OnLaunched`, after constructing `MainWindow` and before activating it, run `await Task.Run(() => orchestrator.InitializeAsync())` — never block the UI thread.
3. Store orchestrator in DI so `MainWindow` can query `IsPro`.
4. Subscribe to `ProLicenseChanged` — on state change, dispatch to UI thread via `DispatcherQueue.TryEnqueue` and refresh Pro-gated UI.

> No visual gating yet — wired in Phase 4.

📌 **COMMIT:** `feat(app): wire LicenseOrchestrator into App bootstrap`

---

### Task 2.6 — Phase 2 Full Test Run

🧪 **TEST:** Run `dotnet test` across all projects.

| Project | Expected |
|---|---|
| `SmrtPad.Tests` | All existing + all 4 new licensing test files green |
| `SmrtPad.AI.Tests` | 0 tests, 0 failures |
| `SmrtPad.UITests` | Compiles; runtime-skipped if Appium absent |

📌 **COMMIT:** `test(licensing): phase 2 full test run — all green`

---

### Phase 2 — Definition of Done

- [ ] `FeatureFlags.IsEnabled(SmartSidebar)` returns `false` on a clean install.
- [ ] `LicenseOrchestrator.InitializeAsync()` runs on a background thread without blocking the UI.
- [ ] All 4 new licensing test files pass (min. 66 methods total). All 16 pre-existing unit tests still pass.
- [ ] `System.Security.Cryptography.ProtectedData` added to CPM.


---

---

# PHASE 3 — SmrtPad.AI Assembly

**Goal:** Create `SmrtPad.AI`, implement hardware detection, build `AIDispatcher` with streaming infrastructure, integrate Foundry Local (CPU/GPU) and Phi-Silica (NPU). All testable classes covered by Moq-based unit tests.

---

### Task 3.1 — Create `SmrtPad.AI.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.22000.0</TargetPlatformMinVersion>
    <OutputType>Library</OutputType>
    <RootNamespace>SmrtPad.AI</RootNamespace>
    <Platforms>x64;ARM64</Platforms>
    <RuntimeIdentifiers>win-x64;win-arm64</RuntimeIdentifiers>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AI.Foundry.Local" />
    <PackageReference Include="Microsoft.Windows.AI.MachineLearning" />
    <PackageReference Include="Microsoft.WindowsAppSDK" />
  </ItemGroup>
</Project>
```

Add `SmrtPad.AI` to solution. Add project reference from `SmrtPad.AI.Tests` to `SmrtPad.AI`. Build both.

> **Do NOT** add a project reference from `SmrtPad.csproj` to `SmrtPad.AI.csproj`. The main app loads it at runtime via `AssemblyLoadContext`.

📌 **COMMIT:** `chore: add SmrtPad.AI class library project`

---

### Task 3.2 — `AIExecutionTarget` and `HardwareProbeService`

**File:** `SmrtPad.AI/HardwareProbeService.cs`

```csharp
public enum AIExecutionTarget { PhiSilicaNpu, FoundryLocalGpu, FoundryLocalCpu }

public interface IExecutionProviderCatalogAdapter
{
    Task<bool> IsNpuAvailableAsync(CancellationToken ct);
    Task<bool> IsGpuAvailableAsync(CancellationToken ct);
}

public sealed class HardwareProbeService
{
    public HardwareProbeService(IExecutionProviderCatalogAdapter catalog);
    public async Task<AIExecutionTarget> DetectAsync(CancellationToken ct = default);
}
```

Detection logic: probe NPU (any exception → unavailable) → if available return `PhiSilicaNpu`; else probe GPU → if available return `FoundryLocalGpu`; else return `FoundryLocalCpu`. Honour `CancellationToken` before either probe.

Production `ConcreteExecutionProviderCatalogAdapter` wraps `ExecutionProviderCatalog.GetDefault()` and `LanguageModel.IsAvailableAsync()`.

🧪 **TEST** (`SmrtPad.AI.Tests/HardwareProbeServiceTests.cs` — min. 10 methods):

```
DetectAsync_NpuAvailable_ReturnsPhiSilicaNpu
DetectAsync_NpuUnavailable_GpuAvailable_ReturnsFoundryLocalGpu
DetectAsync_NpuUnavailable_GpuUnavailable_ReturnsFoundryLocalCpu
DetectAsync_NpuProbeThrows_FallsBackToGpuProbe
DetectAsync_NpuProbeThrows_GpuUnavailable_ReturnsFoundryLocalCpu
DetectAsync_NpuProbeThrows_GpuProbeThrows_ReturnsFoundryLocalCpu
DetectAsync_GpuProbeThrows_ReturnsFoundryLocalCpu
DetectAsync_CanceledBeforeNpuProbe_ThrowsOperationCanceledException
DetectAsync_CanceledAfterNpuProbe_ThrowsOperationCanceledException
DetectAsync_CalledTwice_ReturnsSameResult
```

📌 **COMMIT:** `feat(ai): implement HardwareProbeService with NPU/GPU/CPU detection`
📌 **COMMIT:** `test(ai): add HardwareProbeService unit tests`

---

### Task 3.3 — `PromptTemplates`

**File:** `SmrtPad.AI/PromptTemplates.cs`

```csharp
public static class PromptTemplates
{
    public static string Summarize(string text);       // throws ArgumentNullException for null
    public static string ToneProfessional(string text);
    public static string ToneCasual(string text);
    public static string Rewrite(string text);
    public static string SemanticQuery(string query);  // trims whitespace only
    public static string OcrFallback(string rawOcrText);
}
```

🧪 **TEST** (`SmrtPad.AI.Tests/PromptTemplatesTests.cs` — min. 25 methods):

```
Summarize_ContainsInputText
Summarize_EmptyText_ReturnsValidPrompt
Summarize_WhitespaceOnlyText_ReturnsValidPrompt
Summarize_VeryLongText_ContainsEntireText
Summarize_TextWithSpecialChars_ContainsRawText
Summarize_TextWithCurlyBraces_DoesNotThrow
Summarize_NullText_ThrowsArgumentNullException
ToneProfessional_ContainsInputText
ToneProfessional_EmptyText_ReturnsValidPrompt
ToneProfessional_NullText_ThrowsArgumentNullException
ToneCasual_ContainsInputText
ToneCasual_EmptyText_ReturnsValidPrompt
ToneCasual_NullText_ThrowsArgumentNullException
Rewrite_ContainsInputText
Rewrite_EmptyText_ReturnsValidPrompt
Rewrite_NullText_ThrowsArgumentNullException
SemanticQuery_TrimsLeadingWhitespace
SemanticQuery_TrimsTrailingWhitespace
SemanticQuery_AlreadyTrimmed_ReturnsSameValue
SemanticQuery_EmptyString_ReturnsEmptyString
SemanticQuery_WhitespaceOnly_ReturnsEmptyString
OcrFallback_ContainsRawText
OcrFallback_EmptyText_ReturnsValidPrompt
OcrFallback_NullText_ThrowsArgumentNullException
ToneProfessional_AndToneCasual_UseDistinctPrompts
```

📌 **COMMIT:** `feat(ai): implement PromptTemplates`
📌 **COMMIT:** `test(ai): add PromptTemplates unit tests`

---

### Task 3.4 — `ILanguageModelAdapter` and `AIDispatcher`

**File:** `SmrtPad.AI/AIDispatcher.cs`

```csharp
public interface ILanguageModelAdapter : IAsyncDisposable
{
    IAsyncEnumerable<string> StreamAsync(string prompt, CancellationToken ct);
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct);
}

public sealed class AIDispatcher : IAsyncDisposable
{
    public AIExecutionTarget ExecutionTarget { get; private set; }
    public bool IsInitialized { get; private set; }

    public AIDispatcher(HardwareProbeService hardwareProbe,
        Func<AIExecutionTarget, Task<ILanguageModelAdapter>> modelFactory);

    public Task InitializeAsync(CancellationToken ct = default);  // idempotent
    public Task StreamResponseAsync(string prompt,
        Action<string> onToken, Action onComplete,
        Action<Exception>? onError = null, CancellationToken ct = default);
    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
    public ValueTask DisposeAsync();
}
```

Key constraints: `InitializeAsync` idempotent; `StreamResponseAsync` never blocks the calling thread; if not yet initialized, `StreamResponseAsync` auto-calls `InitializeAsync` first.

🧪 **TEST** (`SmrtPad.AI.Tests/AIDispatcherTests.cs` — min. 20 methods):

```
InitializeAsync_FirstCall_SetsIsInitializedTrue
InitializeAsync_CalledTwice_InitializesOnce
InitializeAsync_NpuTarget_SetsExecutionTargetPhiSilicaNpu
InitializeAsync_CpuTarget_SetsExecutionTargetFoundryLocalCpu
InitializeAsync_GpuTarget_SetsExecutionTargetFoundryLocalGpu
InitializeAsync_CancellationRequested_ThrowsOperationCanceledException
StreamResponseAsync_CallsOnTokenForEachToken
StreamResponseAsync_CallsOnCompleteAfterAllTokens
StreamResponseAsync_EmptyStream_CallsOnCompleteWithNoTokens
StreamResponseAsync_CancellationDuringStream_StopsTokenDelivery
StreamResponseAsync_CancellationDuringStream_CallsOnComplete
StreamResponseAsync_ModelThrows_CallsOnError
StreamResponseAsync_ModelThrows_OnErrorNull_DoesNotThrow
StreamResponseAsync_BeforeInitialize_AutoInitializes
StreamResponseAsync_CalledConcurrently_BothComplete
GenerateEmbeddingAsync_ReturnsNonEmptyArray
GenerateEmbeddingAsync_CancellationRequested_ThrowsOperationCanceledException
DisposeAsync_DisposesLanguageModelAdapter
DisposeAsync_CalledTwice_IsIdempotent
DisposeAsync_StreamingInProgress_CancelsStream
```

📌 **COMMIT:** `feat(ai): implement AIDispatcher with ILanguageModelAdapter abstraction`
📌 **COMMIT:** `test(ai): add AIDispatcher unit tests`

---

### Task 3.5 — Foundry Local SDK Integration

Implement `ConcreteFoundryModelAdapter` (production GPU/CPU path) using `FoundryLocalClient.StartAsync` with `modelAlias` set to `"phi-3.5-mini-instruct"` (GPU) or `"phi-3.5-mini-instruct-generic-cpu"` (CPU). Model cache path: `Path.Combine(ApplicationData.Current.LocalFolder.Path, "models")`. Test on a non-Copilot+ PC. Document the cache path in `README.md`.

📌 **COMMIT:** `feat(ai): integrate Foundry Local SDK (CPU/GPU inference path)`

---

### Task 3.6 — Phi-Silica Integration

Implement `ConcretePhiSilicaModelAdapter` (production NPU path) via `LanguageModel.CreateAsync()`. Test on a Copilot+ PC (Snapdragon X or Intel Core Ultra 2 NPU) to confirm `LanguageModel.IsAvailableAsync()` returns `true`.

📌 **COMMIT:** `feat(ai): integrate Phi-Silica LanguageModel (NPU inference path)`

---

### Task 3.7 — Phase 3 Full Test Run

🧪 **TEST:** Run `dotnet test` across all projects.

| Project | Expected |
|---|---|
| `SmrtPad.Tests` | All green |
| `SmrtPad.AI.Tests` | All 55+ new AI tests green |
| `SmrtPad.UITests` | Compiles; runtime-skipped if Appium absent |

📌 **COMMIT:** `test(ai): phase 3 full test run — all green`

---

### Phase 3 — Definition of Done

- [ ] `SmrtPad.AI.csproj` builds for `x64` and `ARM64`, zero errors.
- [ ] `HardwareProbeService` returns the correct target on each hardware type.
- [ ] `AIDispatcher.InitializeAsync` completes on a background thread.
- [ ] All `SmrtPad.AI.Tests` pass. `SmrtPad.csproj` does **not** reference `SmrtPad.AI` directly.


---

---

# PHASE 4 — Smart Sidebar Shell & Pro Gating

**Goal:** Create the `SmartSidebar` UserControl, implement the `AssemblyLoadContext` guard (loads `SmrtPad.AI` only for Pro users), wire `FeatureFlags` checks, and add the Pro upsell `ContentDialog`.

---

### Task 4.1 — Post-Build Copy of `SmrtPad.AI` into Package Layout

In the `SmrtPad (Package)` `.wapproj`, add a post-build target that copies `SmrtPad.AI.dll` and its transitive dependencies into the app output directory so the `AssemblyLoadContext` can resolve them at runtime. Verify the layout after a Release build.

📌 **COMMIT:** `build: copy SmrtPad.AI output into package layout for ALC`

---

### Task 4.2 — `IAIDispatcher` Interface in Main Project

**File:** `SmrtPad/Services/IAIDispatcher.cs`

```csharp
public interface IAIDispatcher
{
    bool IsInitialized { get; }
    string ExecutionTargetDisplayName { get; }
    Task InitializeAsync(CancellationToken ct = default);
    Task StreamResponseAsync(string prompt, Action<string> onToken,
        Action onComplete, Action<Exception>? onError = null, CancellationToken ct = default);
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
}
```

`AIDispatcherFactory` in `SmrtPad.AI` implements `IAIDispatcher` and wraps `AIDispatcher`. The main app uses only this interface — never a hard reference to `SmrtPad.AI` types.

📌 **COMMIT:** `feat(app): add IAIDispatcher interface for ALC boundary`

---

### Task 4.3 — `AssemblyLoadContext` Guard in `App.xaml.cs`

After `LicenseOrchestrator.InitializeAsync()` completes on the background thread:

```csharp
if (orchestrator.IsPro)
{
    var aiDllPath = Path.Combine(AppContext.BaseDirectory, "SmrtPad.AI.dll");
    if (File.Exists(aiDllPath))
    {
        var alc = new AIAssemblyLoadContext(aiDllPath);
        var assembly = alc.LoadFromAssemblyPath(aiDllPath);
        var factoryType = assembly.GetType("SmrtPad.AI.AIDispatcherFactory")!;
        _aiDispatcher = (IAIDispatcher)Activator.CreateInstance(factoryType)!;
    }
}
```

Register `_aiDispatcher` (or `null`) in DI as `IAIDispatcher?`.

📌 **COMMIT:** `feat(app): implement AssemblyLoadContext guard (Pro-only AI load)`

---

### Task 4.4 — `SmartSidebar` XAML Shell

**File:** `SmrtPad/Controls/SmartSidebar.xaml` and `SmartSidebar.xaml.cs`

Implement as a `UserControl`. Sections:

1. **Header:** `"✨ Smart Sidebar"` + close `Button` (`AutomationProperties.Name="Close sidebar"`).
2. **Summarize** (`x:Name="SummarizeSection"`): heading, `Button` `"Summarize selection"` (`AutomationProperties.AutomationId="SummarizeSectionButton"`), `ProgressRing` (collapsed), streaming output `TextBlock`.
3. **Tone** (`x:Name="ToneSection"`): `ToggleSwitch` Professional↔Casual (`AutomationProperties.AutomationId="ToneToggleSwitch"`), `Button` `"Rewrite"`, streaming output `TextBlock`.
4. **Semantic Search** (`x:Name="SemanticSection"`): `AutoSuggestBox` (`AutomationProperties.AutomationId="SemanticSearchBox"`), `ListView` results.
5. **Footer:** `TextBlock` hardware badge (`x:Name="HardwareBadge"`).
6. **Entry animation:** `RepositionThemeAnimation` sliding from the right.
7. Constructor accepts `IAIDispatcher`.

Instantiated in `MainWindow` **only** when `FeatureFlags.IsEnabled(SmrtPadFeature.SmartSidebar)`:

```csharp
if (FeatureFlags.IsEnabled(SmrtPadFeature.SmartSidebar))
    SidebarHost.Content = new SmartSidebar(_aiDispatcher!);
```

📌 **COMMIT:** `feat(ui): implement Smart Sidebar XAML shell (Pro-only, Phase 4)`

---

### Task 4.5 — Pro Upsell `ContentDialog`

Show when any Pro-gated UI element is activated in Free tier:

```csharp
var dialog = new ContentDialog
{
    Title             = Res.GetString("ProUpsellTitle"),
    Content           = Res.GetString("ProUpsellContent"),
    PrimaryButtonText = Res.GetString("ProUpsellUpgrade"),
    CloseButtonText   = Res.GetString("ProUpsellDismiss"),
    XamlRoot          = Content.XamlRoot
};
if (await dialog.ShowAsync() == ContentDialogResult.Primary)
    await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?productid=<store-id>"));
```

Add `ProUpsellTitle`, `ProUpsellContent`, `ProUpsellUpgrade`, `ProUpsellDismiss` to all 9 locale `resw` files.

📌 **COMMIT:** `feat(app): add Pro upsell ContentDialog for gated features`

---

### Task 4.6 — `SmartSidebarUITests`

**File:** `SmrtPad.UITests/Tests/SmartSidebarUITests.cs` — all `[SkippableFact]`

```
SidebarToggle_FreeTier_ShowsUpsellDialog
SidebarToggle_FreeTier_UpsellDialog_HasUpgradeButton
SidebarToggle_FreeTier_UpsellDialog_Dismiss_ClosesDialog
SidebarToggle_FreeTier_SidebarNotVisible
```

📌 **COMMIT:** `test(ui): add SmartSidebar Pro-upsell UI tests`

---

### Phase 4 — Definition of Done

- [ ] `SmartSidebar` constructed only when `FeatureFlags.IsEnabled(SmartSidebar) == true`.
- [ ] Free-tier: `SmrtPad.AI.dll` confirmed not loaded (verify via Process Explorer → Modules).
- [ ] Pro-tier (valid `.lic`): sidebar slides in with hardware badge.
- [ ] Upsell dialog appears for any Pro feature in Free tier. All 9 locale files contain 4 new upsell strings.

---

---

# PHASE 5 — AI Skills

**Goal:** Implement all four AI skills, wire to the Smart Sidebar, connect streaming to the document, and add the hardware badge detail flyout.

---

### Task 5.1 — `SummarizerSkill`

**File:** `SmrtPad.AI/Skills/SummarizerSkill.cs`

```csharp
public sealed class SummarizerSkill
{
    public SummarizerSkill(AIDispatcher dispatcher);
    public void Summarize(string text, Action<string> onToken, Action onComplete,
        Action<Exception>? onError = null, CancellationToken ct = default);
}
```

Wire: `"Summarize selection"` click → selected text or full document → `Summarize`. Tokens stream into sidebar `TextBlock`; `ProgressRing` shown during streaming; `"Stop"` button cancels via `CancellationTokenSource`.

🧪 **TEST** (`SmrtPad.AI.Tests/Skills/SummarizerSkillTests.cs` — min. 10 methods):

```
Summarize_InvokesDispatcherStreamResponseAsync
Summarize_PassesCorrectSummarizePrompt
Summarize_EmptyText_PassesEmptyPrompt_NoException
Summarize_NullText_ThrowsArgumentNullException
Summarize_OnTokenCallback_CalledForEachToken
Summarize_OnCompleteCallback_CalledOnce
Summarize_Cancellation_PropagatedToDispatcher
Summarize_DispatcherThrows_CallsOnError
Summarize_DispatcherThrows_OnErrorNull_DoesNotThrow
Summarize_VeryLongText_DoesNotTruncatePrompt
```

📌 **COMMIT:** `feat(ai): implement SummarizerSkill and wire to Smart Sidebar`
📌 **COMMIT:** `test(ai): add SummarizerSkill unit tests`

---

### Task 5.2 — `ToneShifterSkill`

**File:** `SmrtPad.AI/Skills/ToneShifterSkill.cs`

```csharp
public enum ToneTarget { Professional, Casual }

public sealed class ToneShifterSkill
{
    public ToneShifterSkill(AIDispatcher dispatcher);
    public void ShiftTone(string text, ToneTarget target,
        Action<string> onToken, Action onComplete,
        Action<Exception>? onError = null, CancellationToken ct = default);
}
```

Wire: `ToggleSwitch` → `ToneTarget`; `"Rewrite"` click → `ShiftTone` on selected paragraph. After `onComplete`: highlight changed words yellow via `ITextRange.CharacterFormat.BackgroundColor`; clear after 5 s via `DispatcherTimer`.

🧪 **TEST** (`SmrtPad.AI.Tests/Skills/ToneShifterSkillTests.cs` — min. 12 methods):

```
ShiftTone_Professional_UsesCorrectPromptTemplate
ShiftTone_Casual_UsesCorrectPromptTemplate
ShiftTone_Professional_PromptContainsInputText
ShiftTone_Casual_PromptContainsInputText
ShiftTone_NullText_ThrowsArgumentNullException
ShiftTone_EmptyText_DoesNotThrow
ShiftTone_OnTokenCallback_CalledForEachToken
ShiftTone_OnCompleteCallback_CalledOnce
ShiftTone_Cancellation_PropagatedToDispatcher
ShiftTone_DispatcherThrows_CallsOnError
ShiftTone_DispatcherThrows_OnErrorNull_DoesNotThrow
ShiftTone_ProfessionalAndCasual_UseDistinctPrompts
```

📌 **COMMIT:** `feat(ai): implement ToneShifterSkill and wire to Smart Sidebar`
📌 **COMMIT:** `test(ai): add ToneShifterSkill unit tests`

---

### Task 5.3 — `AIRewriteSkill`

**File:** `SmrtPad.AI/Skills/AIRewriteSkill.cs`

```csharp
public sealed class AIRewriteSkill
{
    public AIRewriteSkill(AIDispatcher dispatcher);
    public void Rewrite(string text, Action<string> onToken, Action onComplete,
        Action<Exception>? onError = null, CancellationToken ct = default);
}
```

NPU path: uses `TextIntelligence.RewriteAsync` if available; falls back to `PromptTemplates.Rewrite` via dispatcher on CPU/GPU. Wire to `"✏️ Rewrite for clarity"` button in sidebar.

🧪 **TEST** (`SmrtPad.AI.Tests/Skills/AIRewriteSkillTests.cs` — min. 9 methods):

```
Rewrite_InvokesDispatcherWithRewritePrompt
Rewrite_PromptContainsInputText
Rewrite_NullText_ThrowsArgumentNullException
Rewrite_EmptyText_DoesNotThrow
Rewrite_OnTokenCallback_CalledForEachToken
Rewrite_OnCompleteCallback_CalledOnce
Rewrite_Cancellation_PropagatedToDispatcher
Rewrite_DispatcherThrows_CallsOnError
Rewrite_DispatcherThrows_OnErrorNull_DoesNotThrow
```

📌 **COMMIT:** `feat(ai): implement AIRewriteSkill`
📌 **COMMIT:** `test(ai): add AIRewriteSkill unit tests`

---

### Task 5.4 — `ImageOcrSkill`

**File:** `SmrtPad.AI/Skills/ImageOcrSkill.cs`

```csharp
public interface IOcrEngineAdapter
{
    bool IsAvailable { get; }
    Task<string> RecognizeAsync(SoftwareBitmap bitmap, CancellationToken ct);
}

public sealed class ImageOcrSkill
{
    public ImageOcrSkill(IOcrEngineAdapter? engine = null);
    public Task<string> RecognizeAsync(SoftwareBitmap? bitmap, CancellationToken ct = default);
}
```

Production `ConcreteOcrEngineAdapter` wraps `OcrEngine.TryCreateFromUserProfileLanguages()`; returns `string.Empty` when unavailable. Wire in sidebar as drag-drop zone; extracted text shown in `Flyout` with `"Insert into document"` button.

🧪 **TEST** (`SmrtPad.AI.Tests/Skills/ImageOcrSkillTests.cs` — min. 7 methods):

```
RecognizeAsync_NullBitmap_ThrowsArgumentNullException
RecognizeAsync_EngineUnavailable_ReturnsEmptyString
RecognizeAsync_EngineAvailable_ReturnsEngineResult
RecognizeAsync_EngineThrows_ReturnsEmptyString
RecognizeAsync_CancellationRequested_ThrowsOperationCanceledException
RecognizeAsync_EmptyOcrResult_ReturnsEmptyString
RecognizeAsync_OcrResultWithWhitespaceOnly_ReturnsWhitespace
```

📌 **COMMIT:** `feat(ai): implement ImageOcrSkill using Windows AI Text Recognition`
📌 **COMMIT:** `test(ai): add ImageOcrSkill unit tests`

---

### Task 5.5 — Hardware Badge and Execution Mode Detail Flyout

After `IAIDispatcher.InitializeAsync` completes, dispatch to UI thread via `DispatcherQueue.TryEnqueue`. Set badge text (`"⚡ NPU"` / `"🖥️ GPU"` / `"🐢 CPU"`). Add `ToolTip` and a detail `Flyout` (on badge tap) showing model name and tokens/sec after first inference. Set `AutomationProperties.Name = "AI execution: {tier}"` for Narrator.

📌 **COMMIT:** `feat(ui): implement hardware badge and AI execution mode detail flyout`

---

### Task 5.6 — Phase 5 Full Test Run

🧪 **TEST:** Run `dotnet test` across all projects — all green.

📌 **COMMIT:** `test(ai): phase 5 full test run — all green`

---

### Phase 5 — Definition of Done

- [ ] All four skills stream tokens into the sidebar on both NPU and Foundry paths.
- [ ] Stop button cancels an in-flight stream. Tone diff-highlight clears after 5 s.
- [ ] Image OCR extracts text from dragged images. Hardware badge displays correctly.
- [ ] All `SmrtPad.AI.Tests` skill tests pass.


---

---

# PHASE 6 — Semantic Search

**Goal:** Implement `TextChunker`, build the HNSW cosine-similarity embedding index, and wire the semantic search `AutoSuggestBox` in the Smart Sidebar.

---

### Task 6.1 — `TextChunker`

**File:** `SmrtPad.AI/TextChunker.cs`

```csharp
public static class TextChunker
{
    public static IReadOnlyList<string> ChunkByParagraph(string text, int maxTokens = 512);
}
```

- Split at `\r\n\r\n` or `\n\n` paragraph boundaries. Estimate tokens as `text.Length / 4`.
- If a paragraph exceeds `maxTokens`, split further at sentence boundaries (`.`, `!`, `?`).
- Discard empty/whitespace-only paragraphs.
- Throw `ArgumentNullException` for null; `ArgumentOutOfRangeException` for `maxTokens <= 0`.

🧪 **TEST** (`SmrtPad.AI.Tests/TextChunkerTests.cs` — min. 18 methods):

```
ChunkByParagraph_EmptyString_ReturnsEmptyList
ChunkByParagraph_WhitespaceOnly_ReturnsEmptyList
ChunkByParagraph_NullText_ThrowsArgumentNullException
ChunkByParagraph_MaxTokensZero_ThrowsArgumentOutOfRangeException
ChunkByParagraph_MaxTokensNegative_ThrowsArgumentOutOfRangeException
ChunkByParagraph_SingleShortParagraph_ReturnsSingleChunk
ChunkByParagraph_SingleShortParagraph_ChunkEqualsInput
ChunkByParagraph_TwoParagraphs_ReturnsTwoChunks
ChunkByParagraph_ThreeParagraphs_ReturnsThreeChunks
ChunkByParagraph_ParagraphExceedsMaxTokens_SplitsAtSentenceBoundary
ChunkByParagraph_VeryShortMaxTokens_SplitsAggressively
ChunkByParagraph_NoSentenceBoundary_ReturnsSingleOversizedChunk
ChunkByParagraph_ParagraphWithOnlyWhitespace_IsDiscarded
ChunkByParagraph_WindowsLineEndings_SplitsCorrectly
ChunkByParagraph_UnixLineEndings_SplitsCorrectly
ChunkByParagraph_AllChunks_NonEmpty
ChunkByParagraph_MaxTokens_NoChunkExceedsLimit
ChunkByParagraph_UnicodeText_SplitsCorrectly
```

📌 **COMMIT:** `feat(ai): implement TextChunker for paragraph/sentence splitting`
📌 **COMMIT:** `test(ai): add TextChunker unit tests`

---

### Task 6.2 — `SemanticSearchService`

**File:** `SmrtPad.AI/Skills/SemanticSearchService.cs`

```csharp
public sealed record SearchResult(int TabId, string ChunkText, float Score);

public sealed class SemanticSearchService : IAsyncDisposable
{
    public SemanticSearchService(AIDispatcher dispatcher);
    public Task IndexDocumentAsync(int tabId, string documentText, CancellationToken ct = default);
    public Task<IReadOnlyList<SearchResult>> QueryAsync(string queryText, int topK = 5, CancellationToken ct = default);
    public void RemoveTab(int tabId);
    public Task SaveIndexAsync(string filePath, CancellationToken ct = default);
    public Task LoadIndexAsync(string filePath, CancellationToken ct = default);
    public ValueTask DisposeAsync();
}
```

Key implementation details:
- Embeddings via `AIDispatcher.GenerateEmbeddingAsync`. Cosine similarity: `dot(a,b) / (|a| × |b|)`.
- Thread safety: `ReaderWriterLockSlim` — concurrent reads, exclusive writes.
- `IndexDocumentAsync` on same `tabId` replaces existing entries for that tab.
- Index persistence via `BinaryWriter` to `%LOCALAPPDATA%\SmrtPad\semantic_index.bin`.
- Results sorted descending by score; return at most `topK`.

🧪 **TEST** (`SmrtPad.AI.Tests/Skills/SemanticSearchServiceTests.cs` — min. 25 methods):

```
CosineSimilarity_IdenticalVectors_ReturnsOne
CosineSimilarity_OppositeVectors_ReturnsNegativeOne
CosineSimilarity_OrthogonalVectors_ReturnsZero
CosineSimilarity_ZeroVector_ReturnsZero
CosineSimilarity_SingleElementVectors_CorrectResult
IndexDocument_ThenQuery_ReturnsMatchingChunk
IndexDocument_ThenQuery_TopKLimitsResults
IndexDocument_ThenQuery_ScoresDescending
IndexDocument_SameTabId_ReplacesExistingEntries
IndexDocument_MultipleTabIds_BothReturned
IndexDocument_EmptyText_IndexesNoChunks
IndexDocument_NullText_ThrowsArgumentNullException
IndexDocument_CancellationRequested_ThrowsOperationCanceledException
Query_EmptyIndex_ReturnsEmptyList
Query_NullQuery_ThrowsArgumentNullException
Query_TopKGreaterThanResults_ReturnsAllResults
Query_TopKZero_ThrowsArgumentOutOfRangeException
Query_TopKNegative_ThrowsArgumentOutOfRangeException
RemoveTab_ExistingTab_RemovesChunks
RemoveTab_NonExistentTab_DoesNotThrow
SaveIndex_ThenLoadIndex_PreservesEntries
SaveIndex_EmptyIndex_WritesValidFile
LoadIndex_NonExistentFile_IndexRemainsEmpty
LoadIndex_CorruptedFile_ThrowsOrReturnsEmpty
ConcurrentIndexDocument_ThreadSafe
```

📌 **COMMIT:** `feat(ai): implement SemanticSearchService with HNSW cosine index`
📌 **COMMIT:** `test(ai): add SemanticSearchService unit tests`

---

### Task 6.3 — Semantic Search Sidebar UI

Wire the `"🔎 SEMANTIC"` section of `SmartSidebar.xaml`:
1. `AutoSuggestBox` `QuerySubmitted` → `SemanticSearchService.QueryAsync` on background thread.
2. `ProgressRing` shown during query.
3. Results in `ListView` — chunk text (80 chars max) + tab name badge.
4. Result click: navigate to tab, `ITextRange.FindText` to scroll chunk into view.
5. Gate on `FeatureFlags.IsEnabled(SmrtPadFeature.SemanticSearch)`.
6. Background re-indexing: `ContentChanged` debounced 2 s → `IndexDocumentAsync` on background thread.

📌 **COMMIT:** `feat(ui): wire Semantic Search AutoSuggestBox and results ListView`

---

### Task 6.4 — `SemanticSearchUITests`

**File:** `SmrtPad.UITests/Tests/SemanticSearchUITests.cs` — all `[SkippableFact]`

```
SemanticSearch_FreeTier_SectionNotVisible
SemanticSearch_FreeTier_TriggerShowsUpsellDialog
```

📌 **COMMIT:** `test(ui): add SemanticSearch Pro-gating UI tests`

---

### Task 6.5 — Phase 6 Full Test Run

🧪 **TEST:** Run `dotnet test` — all projects green.

📌 **COMMIT:** `test(ai): phase 6 full test run — all green`

---

### Phase 6 — Definition of Done

- [ ] Semantic Search returns relevant chunks on both NPU and Foundry paths. Index persists across sessions.
- [ ] Result click navigates to correct tab and scrolls to chunk. Feature gated in Free tier.
- [ ] All `SmrtPad.AI.Tests` pass.

---

---

# PHASE 7 — Editor Completeness

**Goal:** Session restore with crash recovery, Markdown→RTF bridge, `InkService` with `InkAnalyzer`, document outline panel, and drag-based tab tear-out.

---

### Task 7.1 — Session Restore & Crash Recovery

**File:** `SmrtPad/Services/SessionRestoreService.cs`

```csharp
public interface ISessionRestoreService
{
    Task SaveSessionAsync(IReadOnlyList<SessionTabState> tabs, CancellationToken ct = default);
    Task<IReadOnlyList<SessionTabState>> LoadSessionAsync(CancellationToken ct = default);
    Task ClearSessionAsync(CancellationToken ct = default);
}

public sealed record SessionTabState(string Title, string? FilePath, string? TempBackupPath, int CursorPosition);
```

Concrete implementation: session JSON → `%LOCALAPPDATA%\SmrtPad\session.json`; document content auto-saved to `%LOCALAPPDATA%\SmrtPad\backups\tab_{id}.rtf` every 30 s via `DispatcherTimer`. In `App.OnLaunched`: if `session.json` exists and `ClearSessionAsync` was not previously called (crash indicator), show restore `ContentDialog`. On clean exit: call `ClearSessionAsync`.

🧪 **TEST** (`SmrtPad.Tests/Services/SessionRestoreServiceTests.cs` — min. 14 methods):

```
SaveSession_ThenLoadSession_ReturnsEquivalentTabs
SaveSession_EmptyList_SavesValidJson
LoadSession_NoSavedFile_ReturnsEmptyList
LoadSession_CorruptedJson_ReturnsEmptyList
ClearSession_AfterSave_LoadReturnsEmptyList
SaveSession_NullList_ThrowsArgumentNullException
SaveSession_CancellationRequested_ThrowsOperationCanceledException
LoadSession_CancellationRequested_ThrowsOperationCanceledException
ClearSession_WhenNoFileExists_DoesNotThrow
SaveSession_MultipleTabs_AllTabsPreserved
SaveSession_TabWithNullFilePath_Preserved
SaveSession_TabWithNullTempBackupPath_Preserved
SaveSession_OverwritesPreviousSave
LoadSession_ValidFile_PreservesTabOrder
```

📌 **COMMIT:** `feat(core): implement SessionRestoreService with crash recovery`
📌 **COMMIT:** `test(core): add SessionRestoreService unit tests`

---

### Task 7.2 — Markdown-to-RTF Bridge

**File:** `SmrtPad/Helpers/MarkdownToRtfConverter.cs`

```csharp
public static class MarkdownToRtfConverter
{
    public static string Convert(string markdown);  // throws ArgumentNullException for null
}
```

| Markdown | RTF |
|---|---|
| `# H1` | `\pard\sb240\sa60\b\fs36` |
| `## H2` | `\pard\sb180\sa40\b\fs28` |
| `### H3` | `\pard\sb120\sa20\b\fs24` |
| `**bold**` | `{\b text}` |
| `*italic*` | `{\i text}` |
| `` `code` `` | `{\f1\highlight1 text}` |
| `- item` / `* item` | RTF bulleted list (`\pnlvlblt`) |
| `1. item` | RTF numbered list (`\pnlvlbody`) |
| `---` | `\brdrb\brdrs\brdrw10` horizontal rule |
| `> quote` | `\li720\ri720` indented paragraph |
| ` ```block``` ` | Monospace + light background |
| Plain paragraph | `\pard\sl276\slmult1` |

Special RTF chars (`\`, `{`, `}`) escaped as `\\`, `\{`, `\}`.

🧪 **TEST** (`SmrtPad.Tests/Helpers/MarkdownToRtfConverterTests.cs` — min. 23 methods):

```
Convert_NullInput_ThrowsArgumentNullException
Convert_EmptyString_ReturnsMinimalRtfHeader
Convert_PlainParagraph_WrapsInPard
Convert_H1_ProducesLargeBoldFragment
Convert_H2_ProducesMediumBoldFragment
Convert_H3_ProducesSmallBoldFragment
Convert_BoldText_WrapsBold
Convert_ItalicText_WrapsItalic
Convert_InlineCode_UsesMonospaceFont
Convert_UnorderedList_ProducesBulletedList
Convert_OrderedList_ProducesNumberedList
Convert_HorizontalRule_ProducesHrFragment
Convert_Blockquote_ProducesIndentedParagraph
Convert_FencedCodeBlock_UsesMonospaceFont
Convert_NestedBoldAndItalic_BothApplied
Convert_MultiParagraph_EachParagraphSeparated
Convert_HeadingFollowedByParagraph_BothPresent
Convert_EmptyListItem_HandledGracefully
Convert_OnlyWhitespaceParagraph_SkippedOrEmpty
Convert_UnicodeChars_PreservedInOutput
Convert_SpecialRtfChars_Escaped
Convert_LargeDocument_CompletesWithoutException
Convert_MixedContent_AllElementsPresent
```

📌 **COMMIT:** `feat(core): implement MarkdownToRtfConverter (MD→RTF bridge)`
📌 **COMMIT:** `test(core): add MarkdownToRtfConverter unit tests`

---

### Task 7.3 — `InkService` with `InkAnalyzer`

**File:** `SmrtPad/Services/InkService.cs`

```csharp
public interface IInkService
{
    Task<string> RecognizeAsync(IReadOnlyList<InkStroke> strokes, CancellationToken ct = default);
}
```

Concrete `InkService` uses `InkAnalyzer` (`Windows.UI.Input.Inking.Analysis`): detects line structure (`InkAnalysisNodeKind.Line`), excludes drawing regions, joins words in reading order. `InkCanvas` overlay in `MainWindow` toggled via View menu (`"✏️ Ink"`). On `"Recognise"` (`Ctrl+Shift+R`): insert result at cursor, clear canvas. Advanced `InkAnalytics` path gated on `FeatureFlags.IsEnabled(SmrtPadFeature.InkAnalytics)`; Free tier uses basic `InkRecognizerContainer`.

📌 **COMMIT:** `feat(core,ui): implement InkService with InkAnalyzer and InkCanvas overlay`

---

### Task 7.4 — Document Outline Panel

Collapsible `ListView` panel (240 px, collapses to 40 px) in `MainWindow`. Scan active `RichEditBox` for bold + large-font paragraphs on `ContentChanged` (debounced 500 ms). Populate `ListView` with heading text. Item click: `ITextRange.FindText` + `ScrollIntoView`. Toggle: `"📋 Document Outline"` in View menu (`Ctrl+Shift+O`).

📌 **COMMIT:** `feat(ui): implement document outline panel from heading styles`

---

### Task 7.5 — Drag-Based Tab Tear-Out

Handle `TabView.TabDroppedOutside`: capture `DocumentTab`, call `App.NewWindow()`, transfer `DocumentTab` (including `RichEditBox` content) to new window, remove from source window, activate.

📌 **COMMIT:** `feat(app): implement drag-based tab tear-out via TabDroppedOutside`

---

### Task 7.6 — Phase 7 Full Test Run

🧪 **TEST:** Run `dotnet test` — all projects green (incl. `SessionRestoreService` and `MarkdownToRtfConverter`).

📌 **COMMIT:** `test(core): phase 7 full test run — all green`

---

### Phase 7 — Definition of Done

- [ ] Session restore dialog appears after simulated crash. `.md` files render headings/bold/italic/code correctly.
- [ ] Ink mode activates; strokes recognised and inserted. Document outline lists headings; scroll on click.
- [ ] Tab tear-out creates independent `MainWindow`. All tests green; coverage ≥ 80%.


---

---

# PHASE 8 — Accessibility, Performance & Polish

**Goal:** WCAG AA compliance, < 800 ms cold-start, opt-in crash telemetry, and UX polish.

---

### Task 8.1 — Accessibility Audit

Run and fix all **Critical** and **Serious** issues from Accessibility Insights for Windows, Narrator (`Win+Ctrl+Enter`), and High Contrast mode.

Mandatory fixes:

| Control | Fix |
|---|---|
| All toolbar buttons | `AutomationProperties.Name = "{localised label}"` |
| Status bar `TextBlock`s | `AutomationProperties.LiveSetting = "Polite"` |
| Sidebar streaming `TextBlock` | `AutomationProperties.LiveSetting = "Assertive"` |
| All `ProgressRing`s | `AutomationProperties.Name = "Loading"` |
| `FindReplaceBar` inputs | `AutomationProperties.LabeledBy` pointing to heading |
| Hardware badge | `AutomationProperties.Name = "AI execution: {tier}"` |

📌 **COMMIT:** `feat(app): accessibility audit fixes (Narrator, WCAG AA, High Contrast)`

---

### Task 8.2 — Performance Profiling — Cold-Start < 800 ms

**Target:** App cold-start to first interactive frame ≤ 800 ms on Intel Core i5, 16 GB RAM, SSD.

1. Profile with VS 2026 CPU Usage + Timeline profiler on a Release build.
2. Measure: `App.OnLaunched` entry → `MainWindow.Activate()` → first tab frame rendered.
3. If > 800 ms, investigate hot paths: `LicenseOrchestrator.InitializeAsync` still on UI thread; `SettingsService.Load` synchronous file I/O in constructor; excessive XAML element count in initial template.
4. Iterate until target met. Document baseline and result in `README.md`.

📌 **COMMIT:** `perf: optimise cold-start path to meet <800ms target`

---

### Task 8.3 — Crash Telemetry (Opt-in)

First-launch `ContentDialog`: *"Help improve SmrtPad by sending anonymous crash reports."* Store consent in `SettingsService.CrashTelemetryEnabled` (new `bool` property). If consent given: attach `Application.UnhandledException`; write structured JSON to `%LOCALAPPDATA%\SmrtPad\crashes\crash_{timestamp}.json`; call WER `ReportFault` P/Invoke. No third-party analytics SDK.

🧪 **TEST** (`SmrtPad.Tests/Services/SettingsServiceCrashTelemetryTests.cs` — min. 3 methods):

```
CrashTelemetryEnabled_DefaultValue_IsFalse
CrashTelemetryEnabled_SetTrue_Persists
CrashTelemetryEnabled_SetFalse_AfterTrue_Persists
```

📌 **COMMIT:** `feat(app): add opt-in crash telemetry via WER`
📌 **COMMIT:** `test(core): add CrashTelemetryEnabled settings tests`

---

### Task 8.4 — UI Polish Pass

1. `EntranceThemeTransition` on tab content area when switching tabs.
2. Empty-state placeholder when no tabs open: `"Start a new document or open a file"` with quick-action buttons.
3. `CommandBar` collapses gracefully at narrow widths; overflow items in `SecondaryCommands`.
4. Drag-over visual feedback on `RichEditBox` for valid file types.
5. Font family `ComboBox` renders each item in its own face.

📌 **COMMIT:** `style(ui): Phase 8 polish pass (transitions, empty state, toolbar overflow, font preview)`

---

### Task 8.5 — Pro Strings in All Locales

Ensure all 9 locale `resw` files contain translations for every Pro-tier string added in Phases 4–7:
`ProUpsellTitle` · `ProUpsellContent` · `ProUpsellUpgrade` · `ProUpsellDismiss` · `SmartSidebarTitle` · `SummarizeSectionHeading` · `ToneSectionHeading` · `SemanticSectionHeading` · `HardwareBadgeNpu` · `HardwareBadgeGpu` · `HardwareBadgeCpu` · `InkModeMenuItem` · `DocumentOutlineMenuItem` · `SessionRestoreTitle` · `SessionRestoreContent` · `SessionRestoreYes` · `SessionRestoreNo`

📌 **COMMIT:** `feat(i18n): add Pro-tier and Phase 7 strings to all 9 locale resw files`

---

### Task 8.6 — Phase 8 Full Test Run

🧪 **TEST:** Run `dotnet test` across all projects. Also run Appium UI suite if available.

📌 **COMMIT:** `test: phase 8 full test run — all green`

---

### Phase 8 — Definition of Done

- [ ] Accessibility Insights: zero Critical/Serious issues. Narrator reads all controls. High Contrast renders correctly.
- [ ] Cold-start ≤ 800 ms; result documented in `README.md`.
- [ ] Crash telemetry consent dialog on first launch. All 9 locales contain all new strings.
- [ ] Zero build warnings.

---

---

# PHASE 9 — MSIX Store Prep & Free Tier Submission

**Goal:** Finalise `Package.appxmanifest`, generate Store assets, pass WACK, submit Free tier.
**Target version:** `1.0.0-rc.1`

---

### Task 9.1 — `Package.appxmanifest` Configuration

1. `<Identity Publisher="CN=…" Version="1.0.0.0" Name="…SmrtPad" />` — match Partner Center.
2. `<DisplayName>SmrtPad</DisplayName>` · `<Description>A Fluent AI-powered rich text editor for Windows 11/12</Description>`
3. File type associations: `.rtf`, `.md`, `.txt`.
4. `<ExecutionAlias Alias="smrtpad.exe" />` for command-line open.
5. `<TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.22000.0" MaxVersionTested="10.0.26100.0" />`

📌 **COMMIT:** `build: configure Package.appxmanifest for Store submission`

---

### Task 9.2 — Store Logo Assets

Replace all placeholder `Assets/` images with production assets at all required scales and target sizes per Microsoft Store guidelines:

| Asset | Size |
|---|---|
| `StoreLogo.png` | 50×50 |
| `Square44x44Logo.png` | 44×44 |
| `Square150x150Logo.png` | 150×150 |
| `Wide310x150Logo.png` | 310×150 |
| `SplashScreen.png` | 620×300 |

Include `scale-100`, `scale-200`, `targetsize-16/24/32/48` variants.

📌 **COMMIT:** `chore: add production Store logo and tile assets`

---

### Task 9.3 — Remove `x86` Platform

Confirm `<Platforms>` in `SmrtPad.csproj` excludes `x86` (AI NuGet packages do not ship `x86` binaries). Update all publish profiles accordingly.

📌 **COMMIT:** `build: remove x86 platform (AI NuGet compatibility)`

---

### Task 9.4 — WACK Certification

1. `dotnet publish -c Release -f net10.0-windows10.0.26100.0 -r win-x64` → signed `.msix`.
2. Run WACK. Fix **all** failures. Re-run; confirm zero failures; save report.

📌 **COMMIT:** `build: WACK certification pass — zero failures`

---

### Task 9.5 — Version Bump and Free Tier Submission

1. Increment `Package.appxmanifest` version to `1.0.0.0`.
2. Create Partner Center listing — description, screenshots (min. 3), feature highlights.
3. Upload signed `.msixupload`. Submit for Store certification.

📝 **DOCS:** Move `[Unreleased]` to `[1.0.0-rc.1]` in `CHANGELOG.md`. Update `README.md` with Store download badge.

📌 **COMMIT:** `build: bump version to 1.0.0-rc.1 for Free tier Store submission`
📌 **COMMIT:** `docs: update README and CHANGELOG for v1.0.0-rc.1`

---

### Phase 9 — Definition of Done

- [ ] `Package.appxmanifest` has correct Publisher matching Partner Center. File associations work post-install.
- [ ] WACK passes zero failures. `x86` removed. `.msixupload` uploaded and submitted.
- [ ] `master` tagged `v1.0.0-rc.1`.

---

---

# PHASE 10 — Pro Add-On, Beta Loop & v1.0 GA

**Goal:** Publish the Pro add-on, run the Windows Insider beta, triage feedback, ship v1.0 GA.
**Target version:** `1.0.0`

---

### Task 10.1 — Pro Add-On in Partner Center

1. Create `SmrtPadPro` add-on (one-time purchase). Update `LicenseOrchestrator` with final SKU ID `"SmrtPadPro"`.
2. Link add-on to Free tier listing. Test purchase flow end-to-end with a test Microsoft account.

📌 **COMMIT:** `feat(licensing): wire production SmrtPadPro SKU into LicenseOrchestrator`

---

### Task 10.2 — Windows Insider Beta

Submit as "Limited Audience" beta via Partner Center Flight Rings. Post to Windows Insider Program community. Set up GitHub Discussions on `github.com/John-Donnelly/SmrtPad` for beta feedback.

📌 **COMMIT:** `build: prepare beta build for Windows Insider submission`

---

### Task 10.3 — Beta Feedback Triage Loop

For each crash report or P0/P1 issue: reproduce locally → write regression test annotated `// Regression: #<issue>` → fix → confirm test passes → commit `fix(<scope>): <description> (fixes #<issue>)`. Allow 4–6 weeks of active triage.

---

### Task 10.4 — Final Pre-GA Checklist

1. WACK — zero failures on final Release build.
2. Accessibility Insights — zero Critical/Serious issues.
3. Cold-start ≤ 800 ms confirmed on reference hardware.
4. Spot-check all 9 locales in the running app.
5. Pro licence flow: purchase → unlock → downgrade re-gates features.
6. Increment `Package.appxmanifest` to `1.0.0.0`.

📌 **COMMIT:** `build: bump version to 1.0.0 for GA release`

---

### Task 10.5 — GA Launch Documentation

📝 **DOCS:** `README.md` — remove alpha/beta badges; add Store download badge; full feature showcase. `CHANGELOG.md` — move `[Unreleased]` to `[1.0.0] — YYYY-MM-DD`. Create `CONTRIBUTING.md` with fork/PR guidelines and test naming convention. Create `SECURITY.md` with responsible disclosure policy and Ed25519 key rotation procedure.

📌 **COMMIT:** `docs: finalise README, CHANGELOG, CONTRIBUTING, SECURITY for v1.0.0`

---

### Phase 10 — Definition of Done

- [ ] `SmrtPadPro` add-on live. Pro purchase flow works on a clean test account.
- [ ] All P0/P1 beta issues fixed and regression-tested.
- [ ] v1.0.0 MSIX certified by WACK; published on Store (Free + Pro). `master` tagged `v1.0.0`.

---

---

## Cross-Phase Quality Gates

| Gate | Tool | Pass Criterion |
|---|---|---|
| Build | `dotnet build -c Release` | Zero errors, zero warnings |
| Unit tests | `dotnet test` | All pass |
| Coverage | `coverlet` | ≥ 80% line coverage per project |
| WACK | Windows App Certification Kit | Zero failures (Phases 9, 10) |
| Accessibility | Accessibility Insights | Zero Critical/Serious (Phases 8, 10) |
| Performance | VS Profiler / ETW | Cold-start ≤ 800 ms (Phases 8, 10) |

### Self-Review Checklist (per commit batch)

- [ ] No `async void` except WinUI event handlers.
- [ ] All `Stream` / `IAsyncDisposable` objects disposed in `using` or `await using`.
- [ ] No `Task.Result` or `.GetAwaiter().GetResult()` on the UI thread.
- [ ] `SmrtPad.AI` types never referenced directly from `SmrtPad.csproj`.
- [ ] All WinRT objects accessed from UI thread or marshalled via `DispatcherQueue.TryEnqueue`.
- [ ] `FeatureFlags.IsEnabled(…)` applied to every Pro-gated UI element.
- [ ] New user-facing strings added to all 9 locale `resw` files.
- [ ] `CHANGELOG.md` updated. `README.md` feature table reflects current state.

---

---

## Appendix A — Target Framework Reference

| Project | TFM | Reason |
|---|---|---|
| `SmrtPad` | `net10.0-windows10.0.26100.0` | Windows 11 24H2+ AI APIs |
| `SmrtPad.AI` | `net10.0-windows10.0.26100.0` | `LanguageModel`, `ExecutionProviderCatalog`, `GenerateEmbeddingVectorsAsync` |
| `SmrtPad.Tests` | `net10.0-windows10.0.26100.0` | Matches main project |
| `SmrtPad.AI.Tests` | `net10.0` | Pure logic — all WinRT mocked via Moq |
| `SmrtPad.UITests` | `net10.0-windows10.0.19041.0` | Appium driver; basic WinRT surface only |

---

## Appendix B — Key API Reference

| API | SDK / Package | Phase |
|---|---|---|
| `MicaBackdrop` / `TabView` / `RichEditBox` | Windows App SDK 1.8 / WinUI 3 | ✅ Done |
| `Windows.Graphics.Printing` / `DocumentFormat.OpenXml` | WinRT / NuGet | ✅ Done |
| `ProtectedData.Unprotect` | System.Security.Cryptography | Phase 2 |
| `ECDsa.VerifyData` (Ed25519) | .NET 10 | Phase 2 |
| `StoreContext.GetAppLicenseAsync` | Windows.Services.Store | Phase 2 |
| `AssemblyLoadContext` | .NET 10 | Phase 4 |
| `ExecutionProviderCatalog` | Windows App SDK 1.8 | Phase 3 |
| `LanguageModel` (Phi-Silica) | Windows App SDK 1.8 | Phase 3 |
| `FoundryLocalClient` | Microsoft.AI.Foundry.Local | Phase 3 |
| `GenerateEmbeddingVectorsAsync` | Windows App SDK 1.8 | Phase 6 |
| `OcrEngine` | Windows.Media.Ocr | Phase 5 |
| `InkAnalyzer` | Windows.UI.Input.Inking.Analysis | Phase 7 |

---

## Appendix C — Test Count Summary (Minimum Targets)

| Project | Existing Files | Added Files | Total |
|---|---|---|---|
| `SmrtPad.Tests` | 16 | +7 (4 Licensing, SessionRestore, MarkdownConverter, CrashTelemetry) | 23 |
| `SmrtPad.AI.Tests` | 0 | +9 | 9 |
| `SmrtPad.UITests` | 17 | +2 (SmartSidebar, SemanticSearch) | 19 |

Minimum test method counts per new file:

| Test File | Min. Methods |
|---|---|
| `FeatureFlagsTests.cs` | 17 |
| `LicensePayloadTests.cs` | 16 |
| `LocalKeyValidatorTests.cs` | 16 |
| `LicenseOrchestratorTests.cs` | 17 |
| `SessionRestoreServiceTests.cs` | 14 |
| `MarkdownToRtfConverterTests.cs` | 23 |
| `SettingsServiceCrashTelemetryTests.cs` | 3 |
| `HardwareProbeServiceTests.cs` | 10 |
| `PromptTemplatesTests.cs` | 25 |
| `AIDispatcherTests.cs` | 20 |
| `TextChunkerTests.cs` | 18 |
| `SummarizerSkillTests.cs` | 10 |
| `ToneShifterSkillTests.cs` | 12 |
| `AIRewriteSkillTests.cs` | 9 |
| `ImageOcrSkillTests.cs` | 7 |
| `SemanticSearchServiceTests.cs` | 25 |

---

---

## PHASE 11 — v2 Roadmap (Placeholder)

Scoped for v2.0 after v1.0 GA ships:

- **LoRA custom fine-tune:** Phi-Silica LoRA via Windows App SDK when API stabilises. Enables per-user writing style adaptation.
- **Voice dictation:** `Windows.Media.SpeechRecognition.SpeechRecognizer` with streaming transcription into `RichEditBox`. Real-time dictation with automatic punctuation.
- **Extended locale coverage:** Add `pt-BR`, `ko-KR`, `it-IT`, `pl-PL`.
- **Cloud sync:** OneDrive integration via `Windows.Storage.Provider`. Automatic sync with conflict-resolution UI.
- **Automated UI regression:** Upgrade `SmrtPad.UITests` to Playwright or Windows Application Driver 2.x for full CI coverage.
- **Themes marketplace:** Custom accent themes beyond light/dark (neon, sepia, high-visibility).

---

*Last updated: Post-assessment revision — workspace is a functional monolith with free-tier largely complete. Phases 1–10 represent the path from current state to v1.0 GA, with the full AI feature set behind `IsPro` / `FeatureFlags` gating.*







