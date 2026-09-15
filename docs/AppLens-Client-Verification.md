# AppLens client verification — COP-239

Verified locally on 2026-09-13, Windows 11 Home build 26200, x64.
This closes the client/table scope of COP-239. Final installed release acceptance
remains COP-241; this is not Store certification or a clean-PC result.

Evidence root (local only, excluded from Git):
`artifacts/cop239-client-26922a8cb41d482db372f2590b9c386e/`.
These local artifacts may contain workstation details; they are not public Store screenshots.

## Changes and observed results

| Check | Result and evidence |
| --- | --- |
| Silver client, disk summary, table and secondary panels | Live scan displayed 433 entries, with 158 ordinary apps in the default view. Latest completion was 0.52 s on this developer PC, not a performance guarantee. See `final-client.png` and `zero-visible-before-export.png`. |
| Duplicate names, suites and unknown sizes | Real Copilot entries remained separate by installation/type/scope. Largest-first placed the 2.31 GiB desktop entry ahead of unknown Store sizes; the suite entry remained distinct. See `duplicate-names.png`, `largest-unknown-last.png`. Presentation tests cover stable ordering, both size directions and combined publisher/scope/type/removal filters without mutating inventory. |
| Keyboard navigation | Tab/Shift+Tab reached controls and row actions; sorting/scope selection worked by keyboard. Alt+S scanned; Alt+D opened the picker; Enter opened the owned fixture confirmation; Escape cancelled. Focused off-screen actions scrolled into view. See `keyboard-row-action.png`, `night-sky225-confirmation.png`, `keyboard-cancel-verification.json`. |
| Text scaling and resizing | At 225% text size, opening details originally consumed the table viewport. A bounded virtualized list and scrollable page now keep results and both panels reachable. Wrapped headers use the same column widths as rows. Live retest included 1280×880 and 960×760 windows, expanded details/history, horizontal and vertical scrolling. Before/after evidence: `text225-*.png`. |
| High contrast | Windows Night sky and Desert themes tested at 225%, including selection, focus, scrolling and cancellation. System contrast resources remained readable. See `night-sky225-*.png`, `desert225-selection.png`. |
| Accessible names | Live UI Automation originally exposed the class name for a row. Rows now expose app name, version, publisher, scope, type, size and status; action names also distinguish version/scope/type. A regression failed before the fix. See `final-accessibility-tree.txt` and the 12 passing presentation tests. Audio with an actual screen reader remains a release check. |
| Download and cancellation | Picker opens in the current user's Downloads folder. Escape retained results. Enter saved a new JSON report with zero visible rows; all 433 entries and seven actions remained in the report, matching the ledger, with user/computer identifiers redacted. See `picker-downloads-after-fix.png`, `picker-cancel-results-retained.png`, `report-saved-zero-visible.png`, `final-report-verification.json`. |
| Restoration and cleanup | Original Custom theme restored, high contrast off, text size 100%. The owned registry-only fixture was checked present after cancellation and then removed; it had no payload. No working app was removed during this review. See `display-restored.json`, `ui-fixture-cleanup.json`. |

The picker uses the installed App SDK 1.8's
[SuggestedFolder](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.windows.storage.pickers.filesavepicker.suggestedfolder?view=windows-app-sdk-1.8)
with [UserDataPaths.Downloads](https://learn.microsoft.com/en-us/uwp/api/windows.storage.userdatapaths.downloads?view=winrt-26100).
The location suggestion alone retained Windows' previously visited folder during
testing; the explicit known-folder path opened Downloads. No SDK upgrade was needed.

## Build and report evidence

- Final x64 and ARM64 desktop builds: zero warnings/errors. ARM64 was compiled,
  not run on ARM64 hardware.
- Core tests: 94 passed, comprising 82 backend and 12 presentation tests.
  Separate TRX files are in `final-tests/`; earlier red-test evidence remains in `tests/`.
- Saved report: `C:\Users\codyl\Downloads\AppLens-20260913-174908-731.json`.
  Evidence copy: `client-export-zero-visible.json`.
- SHA-256: `97D83ADEEC9B505041910DECEFBE10F1C7DA9BA62D609F352948A2164044F4B4`.
- Seven recorded outcomes: four Removed, three Cancelled. The extra cancellation
  came from this review's disposable fixture. Earlier records were not rewritten.
- Earlier COP-240 evidence covers real packaged Store/MSI/vendor flows, native
  UAC denial/approval, independent absence verification and history reload.

## Later computer-use checks — COP-241

Computer use initially stopped at the user's request, then resumed with explicit
permission on 2026-09-14. The [release validation](AppLens-Release-Validation.md)
records the new lifecycle checks and current package-installation boundary.
Do not repeat completed checks unless a new package, changed behavior or a
failure warrants it.

1. On the final installed candidate, verify scan → filter → exact-target removal
   → refresh → download/read report on a clean standard-user Windows PC, including
   offline operation. Downloads now passed in the fresh installed development
   package on 2026-09-14; retain a final-candidate spot check.
2. Use an actual screen reader to check announcements, row/action names and modal
   focus recovery. Recheck text/contrast/layout on the final package as needed.
3. A genuine managed-policy denial remains. NoRemove protection, real failed/slow
   uninstallers and app closure/reopening passed on 2026-09-14 with owned fixtures.
   Native administrator prompts require the user.
4. Run the claimed Windows 10 and ARM64 configurations on appropriate systems,
   measure installation/launch/footprint, and perform final certification checks.

See the [reviewed continuation handoff](AppLens-v1-Handoff.md) for release inputs,
authorization boundaries, commands and the complete next-step order.
