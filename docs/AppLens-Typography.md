# AppLens Typography

AppLens uses a two-font system across desktop UI, exported reports, and design artifacts.

## Standard

- UI text: `Inter`, with `Segoe UI`, `system-ui`, and `sans-serif` fallbacks.
- Technical text: `JetBrains Mono`, with `Cascadia Mono`, `Consolas`, and `monospace` fallbacks.

Use Inter for navigation, labels, cards, tables, buttons, report prose, and dashboard summaries. Use JetBrains Mono for paths, ledger IDs, correlation IDs, hashes, code-like values, and other local evidence strings where character distinction matters.

## Implementation Notes

WinUI references the font families by name and falls back to native Windows fonts when Inter or JetBrains Mono are not installed. Package font assets later if AppLens needs exact typography on every machine.

HTML reports and design prototypes should keep the shared CSS tokens:

```css
:root {
  --font-ui: "Inter", "Segoe UI", system-ui, sans-serif;
  --font-mono: "JetBrains Mono", "Cascadia Mono", Consolas, monospace;
}
```
