# Agent notes

LogViewer is a local WPF log viewer targeting **.NET Framework 4.8** (GPL-3.0).

* Keep the existing **WPF + MVVM** architecture.
* Put new domain logic in `src/LogViewer.Core/`.
* Keep the WPF project focused on UI, settings, and commands.
* Follow existing project conventions unless this file or task documentation says otherwise.

## Tasks

For numbered product tasks, read `docs/tasks/00-overview.md` first, then the matching `docs/tasks/NN-*.md`.

Do not implement anything explicitly marked as out of scope.

## Comments

Use **English only** for XML documentation (`///`) and code comments (`//`) in Core and UI.

Comments should explain **why** something exists: invariants, protocol details, compatibility constraints, or non-obvious decisions. Do not comment obvious code.

Do not translate or modify localized/generated content just to satisfy this rule, including:

* `.resx` UI strings;
* `ReleaseNotes.xml` `<ru>` entries;
* generated `Locals.Designer.cs` content.