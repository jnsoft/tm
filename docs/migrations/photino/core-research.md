# Shared-core implementation research

## Starting checkpoint
Baseline `7cd1c92`, on `photino-02-core`. Existing suite has 52 passing tests. User changes to `.github/copilot-instructions.md` are preserved and will not be staged as agent edits.

## Extraction boundary
Move `Entities/*`, `Common/ObservableObject`, `Models/{NodeModel,ListItemModel,ProjectDocument,ProjectCryptoState}`, `Services/{ProjectCryptoService,ProjectDocumentSession}` into a shared `TM.Core` library. Retain namespaces so business callers and frozen fixture tests keep their contracts. Target net10.0-windows while retaining jnUtil 2.0.2/DPAPI; do not add UseWPF to the library.

`NodeModel` uses WPF Visibility only for filtering and two calculated display properties. Replace those with booleans, map through the existing BooleanToVisibilityConverter in WPF, and update the entity XAML namespace to reference TM.Core. Its Color enum is a domain enum, not a WPF color. Preserve all scheduling/serialization behavior in this extraction.

Keep RelayCommand, MainWindowViewModel, dialog services and WPF helpers in TM. Grant existing TM and TM.Test access to internal core operations while those callers are migrated; do not make sensitive key buffers public.

## Dialog-free persistence
Add a shared document store that accepts paths/passwords and cancellation tokens, reads XML with DTDs prohibited, loads into a fresh model and clears keys/certificates on failed loads. Save through a same-directory temporary file and replace only after serialization/write succeeds, resetting dirty flags after success. Bound inputs and preserve the legacy XML writer/crypto. The new web app will call this store rather than UI dialog services; WPF remains a compatibility client during migration.

## Validation
Build through Visual Studio. Run all existing tests including frozen encrypted fixtures and FlaUI; add persistence success/failure/cancellation tests and a core assembly dependency guard against WPF references. Confirm no production format, DPAPI or jnUtil changes. Record warnings and test evidence before the core checkpoint commit, then branch from that commit for the shell.
