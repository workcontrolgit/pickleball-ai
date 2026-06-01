# Cerebrum

> OpenWolf's learning memory. Updated automatically as the AI learns from interactions.
> Do not edit manually unless correcting an error.
> Last updated: 2026-05-30

## User Preferences

<!-- How the user likes things done. Code style, tools, patterns, communication. -->

## Key Learnings

- **Project:** PickleIQ
- **MudBlazor 9.5.0:** `MudBottomNavigation` and `MudBottomNavigationItem` do NOT exist in v9 — removed completely. Use a fixed bottom `MudAppBar` with `Bottom="true"` as replacement.
- **MudBlazor 9.5.0:** `Color.Default` removed — use `Color.Inherit` or `Color.Surface`. `Color.Primary`, `Color.Inherit` confirmed working.
- **MudBlazor 9.5.0:** `MudDialogProvider` works without options parameter.
- **MudBlazor 9.5.0 MudFileUpload:** Use `CustomContent` (not `ActivatorContent`) as the RenderFragment parameter. The context is the `MudFileUpload` instance — call `context.OpenFilePickerAsync()` to open the OS file picker. `FilesChanged` is `EventCallback<IBrowserFile>` for single-file (`T="IBrowserFile"`). Accept attribute uses comma-separated extensions (e.g., `.mp4,.MP4,.mov,.MOV`).

## Do-Not-Repeat

<!-- Mistakes made and corrected. Each entry prevents the same mistake recurring. -->
<!-- Format: [YYYY-MM-DD] Description of what went wrong and what to do instead. -->
- [2026-05-31] Do NOT use hidden `InputFile` + `JS.InvokeVoidAsync("eval", "document.getElementById(...).click()")` for file picking. In MudBlazor 9.5.0, use `MudFileUpload<IBrowserFile>` with `CustomContent` RenderFragment (context = the component instance) and call `context.OpenFilePickerAsync()` on the button's `OnClick`. `FilesChanged` takes `IBrowserFile` (single file). `IJSRuntime` injection is not needed.

## Decision Log

<!-- Significant technical decisions with rationale. Why X was chosen over Y. -->
