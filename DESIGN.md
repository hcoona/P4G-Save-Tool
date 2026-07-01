---
version: 1
name: "P4G Save Tool WinUI Design System"
description: "A Windows-native, trust-first design system for the P4G Save Tool desktop save editor."
colors:
  strategy: "Use WinUI semantic theme resources and default control brushes; do not define a custom brand palette until implemented."
  background: "{ThemeResource ApplicationPageBackgroundThemeBrush}"
  text: "Default WinUI text foreground theme resources through common controls and text styles."
  accent: "{ThemeResource AccentFillColorDefaultBrush}; use sparingly for primary selection and confirmation affordances."
  diagnostics: "Pair severity color with code, text, icon, or grouping; never communicate status by color alone."
  highContrast: "Use SystemColor theme resources and explicit HighContrast dictionaries for any custom resources."
typography:
  family: "Segoe UI Variable, inherited from WinUI common controls"
  weights:
    body: "Regular"
    emphasis: "Semibold"
  casing: "Sentence case for labels, commands, headings, and diagnostics."
rounded: "Use WinUI default control corner radius and shape resources; avoid custom pills unless a native control cannot express the state."
spacing:
  unit: "4 effective pixels"
  relatedControls: "8 epx"
  labelToContent: "12 epx"
  surfaceEdge: "16 epx"
components:
  shell: "MenuBar plus visible command surface for file commands; retain keyboard and menu access."
  feedback: "InfoBar for visible non-blocking state, ContentDialog for blocking decisions, ProgressBar or ProgressRing for busy work."
  collections: "ListView/GridView/TreeView; use ListView with a Grid ItemTemplate and header Grid for tabular display."
---

# P4G Save Tool WinUI Design System

## Overview

P4G Save Tool is a Windows desktop save editor. Its interface must feel native,
calm, inspectable, and safe. Users are editing small but high-value game save
files, so the design priority is trust over novelty: make the current file,
dirty state, write eligibility, validation diagnostics, and destructive
consequences visible before the user commits changes.

This document is the living design source of truth for humans and AI agents.
The YAML tokens above are normative where they are specific; the prose explains
how to choose native WinUI controls, composition patterns, and safety behavior
when exact tokens do not cover a future UI change.

### Product personality

- Windows-native: use Fluent/WinUI controls first, and let platform styles carry
  most visual identity.
- Trust-first: every editing screen should answer "What file am I editing?",
  "Are my edits applied?", "Can this be written?", and "What will be lost?"
- Calm and dense enough for desktop editing: avoid playful decoration, noisy
  color, and web-dashboard visuals.
- Transparent with domain uncertainty: unknown or unsupported IDs should stay
  visible and diagnosable where current projections support them.
- Complete and coherent: save editing, diagnostics, and state feedback are part
  of the same workflow, not secondary debug UI.

### Source basis

| Basis | Citation |
| --- | --- |
| DESIGN.md files are a self-contained design system source of truth with optional normative YAML tokens and ordered Markdown sections. | <https://github.com/google-labs-code/design.md/blob/2a19f5dd97ab887971b417ebdf1e7e8fda0c7f79/docs/spec.md#L4-L8>, <https://github.com/google-labs-code/design.md/blob/2a19f5dd97ab887971b417ebdf1e7e8fda0c7f79/docs/spec.md#L17-L58>, <https://github.com/google-labs-code/design.md/blob/2a19f5dd97ab887971b417ebdf1e7e8fda0c7f79/docs/spec.md#L88-L109>, <https://github.com/google-labs-code/design.md/blob/2a19f5dd97ab887971b417ebdf1e7e8fda0c7f79/docs/spec.md#L354-L365> |
| Fluent 2 and Windows principles emphasize native fit, focus, accessibility, calm, familiarity, completeness, and coherence. | <https://fluent2.microsoft.design/design-principles>, <https://learn.microsoft.com/en-us/windows/apps/design/design-principles> |
| Windows provides many polished, accessible, responsive WinUI controls; staying current is easiest when custom styles/templates are avoided. | <https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/>, <https://learn.microsoft.com/en-us/windows/apps/develop/platform/xaml/xaml-styles> |
| The current modern app is a WinUI desktop app targeting Windows with self-contained/NativeAOT publishing and references only Application, Contracts, and Presentation from the UI layer. | `src\P4G.SaveTool.WinUI\P4G.SaveTool.WinUI.csproj:3-24`, `src\P4G.SaveTool.WinUI\P4G.SaveTool.WinUI.csproj:38-42` |
| The current shell is a single main window with MenuBar, visible Open/Apply/Save/Save as/About buttons, jump buttons, and a scrollable two-column editor. | `src\P4G.SaveTool.WinUI\MainWindow.xaml:20-56` |
| Current sections cover Basic/Stats, Calendar/Social, Social Links, Party/Persona, Equipment, Compendium, Inventory, and Diagnostics/State. | `src\P4G.SaveTool.WinUI\MainWindow.xaml:43-50`, `src\P4G.SaveTool.WinUI\MainWindow.xaml:62-120`, `src\P4G.SaveTool.WinUI\MainWindow.xaml:390-430` |
| The app already uses XamlControlsResources, ApplicationPageBackgroundThemeBrush, SubtitleTextBlockStyle, and BodyStrongTextBlockStyle rather than a custom palette. | `src\P4G.SaveTool.WinUI\App.xaml:6-10`, `src\P4G.SaveTool.WinUI\MainWindow.xaml:12`, `src\P4G.SaveTool.WinUI\MainWindow.xaml:63-71`, `src\P4G.SaveTool.WinUI\MainWindow.xaml:106-110` |
| Open and save flows are file-centric: open accepts local `.bin` paths and drag/drop; save applies editor fields, writes bytes, and replaces the target file. | `src\P4G.SaveTool.WinUI\MainWindow.xaml.cs:319-356`, `src\P4G.SaveTool.WinUI\MainWindow.xaml.cs:568-638`, `src\P4G.SaveTool.WinUI\MainWindow.xaml.cs:452-510` |
| Shell state gates editing and saving based on save availability, busy state, and writability. | `src\P4G.SaveTool.WinUI\MainWindow.xaml.cs:1701-1775` |
| Diagnostics are first-class and code-driven. | `src\P4G.SaveTool.Contracts\SaveDiagnostic.cs:3-7`, `src\P4G.SaveTool.WinUI\ShellStateFormatter.cs:22-35`, `src\P4G.SaveTool.WinUI\MainWindow.xaml:390-410` |
| Runtime `{Binding}` inside templates is intentional for NativeAOT preservation; do not rewrite templates to typed `x:Bind` without revisiting tests and preservation. | `src\P4G.SaveTool.WinUI\XamlBindingPreservation.cs:8-21`, `tests\P4G.SaveTool.WinUI.Tests\WinUIArchitectureTests.cs:650-665` |
| Some current destructive mutations act immediately in working state; future UI should add confirmation/undo affordances for these paths. | `src\P4G.SaveTool.WinUI\MainWindow.xaml.cs:2557-2628`, `src\P4G.SaveTool.WinUI\MainWindow.xaml.cs:3242-3283` |
| Existing persistence writes a temp file and uses File.Replace without a backup file. | `src\P4G.SaveTool.WinUI\SafeFilePersistence.cs:24-42`, `src\P4G.SaveTool.WinUI\SafeFilePersistence.cs:55-64` |
| A previous premature UI pass added CommandBar/InfoBar chrome to the old dense form but did not establish Fluent visual quality. Future work must design the shell, states, hierarchy, and layout before implementation. | Current redesign evidence summary, 2026-06-30 |
| The Windows UI Kit / Windows Design Kit for Figma is an official Microsoft design resource with UI components, navigation, dialog, layout patterns, styles, and tokens; direct community file access may require Figma authentication. | <https://learn.microsoft.com/en-us/windows/apps/design/downloads/#windows-ui-kit>, <https://www.figma.com/community/file/1440832812269040007> |

### Layered UX and UI workflow

Do not implement UI first. Frontend design for this app is layered and staged;
each layer must produce reviewable evidence before the next layer starts.

1. UX principles: restate the save-editing jobs, trust risks, safety goals, and
   Windows-native expectations for the specific change.
2. Information architecture: define the shell silhouette, navigation model,
   file/state visibility, diagnostics placement, and section grouping before
   choosing controls.
3. Interaction design: specify command hierarchy, enablement, keyboard path,
   busy/empty/error/dirty states, destructive confirmations, and recovery paths.
4. Visual system: apply Fluent quality through native typography, theme
   resources, 4/8/12/16 epx spacing, base/content layers, and meaningful cards
   only where grouping improves comprehension.
5. Component composition: map states and flows to WinUI controls and Windows UI
   Kit patterns before writing XAML. Avoid custom composition until native
   controls cannot express the required state.
6. Accessibility and safety: include keyboard, Narrator, text scaling, High
   Contrast, visible labels, diagnostics without color-only meaning, and safe
   save/destructive behavior in the design acceptance criteria.
7. Evidence and review: every later step requires independent GPT-5.5 review.
   Visual review must use evidence such as screenshots, Figma frames, annotated
   state matrices, or measured spacing; do not approve visuals from guesses.
8. Implementation staging: build the Figma shell and key states first, review
   them visually, then implement XAML. Do not start XAML implementation until
   the shell, state surfaces, and accessibility/safety evidence are accepted.

## Colors

### Color strategy

Use Windows and WinUI semantic theme resources as the color system. Do not add
hard-coded hex colors, fixed "light" or "dark" brushes, or a game-themed brand
palette unless the app implements and tests those tokens in Light, Dark, and
High Contrast.

Native theme resources are the app brand for now. The current app already
merges `XamlControlsResources` and uses `ApplicationPageBackgroundThemeBrush`,
which is the right baseline for a desktop utility that should feel at home on
Windows.

Official basis:

- Common controls use theme brushes, and custom templates should use theme
  brushes instead of hard-coded colors:
  <https://learn.microsoft.com/en-us/windows/apps/develop/ui/theming>,
  <https://learn.microsoft.com/en-us/windows/apps/develop/platform/xaml/xaml-theme-resources>
- Use accent colors sparingly:
  <https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/color>
- Do not use color as the only way to communicate:
  <https://fluent2.microsoft.design/color>
- In High Contrast, use system color resources and test all contrast themes:
  <https://learn.microsoft.com/en-us/windows/apps/design/accessibility/high-contrast-themes>

### Semantic color roles

- App background: `ApplicationPageBackgroundThemeBrush`.
- Text: inherited TextBlock/control foreground resources and WinUI text styles.
- Accent: selected item, default action, or a single primary apply/save
  affordance. Prefer brush resources such as `AccentFillColorDefaultBrush`
  over raw `SystemAccentColor` unless an API requires a `Color`. Do not tint
  every section header or field.
- Focus: preserve built-in WinUI focus visuals. If custom focus visuals are
  unavoidable, use focus-specific theme resources such as
  `FocusStrokeColorOuterBrush` and `FocusStrokeColorInnerBrush`; test keyboard
  and High Contrast.
- Diagnostics:
  - Error: pair severity text/code with an icon or grouping, not color alone.
  - Warning: show the affected target and the action needed.
  - Information: prefer neutral tone and inline placement.
- Danger/destructive: use purpose-named resources if custom styling is needed
  (`DangerTextBrush`, `DestructiveButtonForegroundBrush`, etc.). Do not name
  resources by hue.

### High Contrast rules

- Every custom brush must have Light, Dark, and HighContrast definitions.
- In High Contrast, use `SystemColor*` resources instead of fixed colors.
- Never set `HighContrastAdjustment="None"` unless the entire visual subtree is
  manually backed by system-aware resources.
- Validation, selection, and destructive affordances must remain understandable
  with color removed.

## Typography

Use Segoe UI Variable through WinUI common controls. Typography should support
fast scanning of dense save data, not create a marketing page.

Official basis: WinUI common controls default to Segoe UI Variable; use Regular
and Semibold, sentence case, and minimums of 14 px Semibold or 12 px Regular:
<https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/typography>

### Text scaling

- Support Windows text scaling up to 225%.
- Prefer built-in WinUI controls because they respond to text scaling.
- Avoid fixed control heights. Use `Auto` and `*` sizing, wrapping, scrolling,
  or tooltips anywhere text may clip.
- Test at increased Windows Text size settings before shipping broad UI
  changes.

Official basis:
<https://learn.microsoft.com/en-us/windows/apps/develop/input/text-scaling>

### Type roles

- Window title: native window title text. Keep the file name in the title when a
  save is open, matching the existing shell formatter.
- Section heading: `SubtitleTextBlockStyle` for major editor groups.
- Subsection heading: `BodyStrongTextBlockStyle` for local group labels such as
  "Main character" or "Diagnostics".
- Body/value text: default TextBlock or control typography.
- Diagnostics: severity, code, optional target, and message. The code is part of
  the visual contract because diagnostics are traceable and support bug reports.

### Writing style

- Use sentence case: "Open save...", "Apply edits", "Save as...", "Family name".
- Prefer verbs for commands and nouns for fields.
- Use visible labels/headers, not placeholder-only labeling. Many WinUI
  controls have a built-in `Header`; use it before adding separate labels.
- State constraints plainly: "Select a Persona 4 Golden .bin save file."
- Avoid abbreviations unless they are domain-standard and already familiar in
  the UI, such as "LV" and "XP".

Official basis for visible labels and headers:
<https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/labels>,
<https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/forms>

## Layout

### Layout model

Design in effective pixels and multiples of 4 epx. Use native layout controls,
not web layout metaphors. The current app is a single main surface with a menu,
visible command strip, section jump strip, and two-column editor content. Future
work should preserve that direct editor feel unless a larger information
architecture change justifies navigation.

Official basis: design in effective pixels, use multiples of 4 epx, and apply
Windows responsive breakpoints Small < 640, Medium 641-1007, Large 1008+:
<https://learn.microsoft.com/en-us/windows/apps/design/layout/screen-sizes-and-breakpoints-for-responsive-design>

### Spacing

Use the frontmatter spacing tokens:

- 4 epx: base increment for alignment.
- 8 epx: related controls, toolbar items, jump buttons, compact field groups.
- 12 epx: label-to-content or control-to-explanation spacing.
- 16 epx: outer window padding and text inset from surfaces.

Official basis: Windows content spacing examples include 8 epx between related
buttons/control-header, 12 epx between control-label/content, and 16 epx from a
surface edge to text:
<https://learn.microsoft.com/en-us/windows/apps/design/basics/content-basics>

### Window and page structure

- Keep the main window centered on file editing. Avoid a marketing landing page.
- Design the shell silhouette before details: title/menu area, command surface,
  state/diagnostics surface, navigation or jump surface, and editor content must
  be understandable at a glance.
- Keep the open file path and state near the top of the content or status area.
- Preserve visible access to Open, Apply edits, Save, Save as, and About through
  a menu and a visible command surface. If replaced with `CommandBar`, keep menu
  or accelerator access.
- Keep command hierarchy clear. Primary file and apply/save actions should not
  compete visually with section navigation, filters, or diagnostic affordances.
- The existing two-column editor is acceptable for Large widths. If a future
  responsive pass targets Medium or Small widths, collapse to one column and
  keep section navigation reachable without horizontal scrolling.
- Use jump buttons, `SelectorBar`, or a compact `NavigationView` only when they
  serve the current editor structure. Do not add `NavigationView` just because a
  desktop app has sections.
- Use `TabView` only if the product gains multiple save sessions/documents or
  independent editor tabs.

### Collection layout

- `ListView` and `GridView` provide their own scrolling. Do not wrap them in an
  extra vertical `ScrollViewer`; constrain height instead.
- Tabular data should be `ListView` with a Grid-based `ItemTemplate` and a
  header Grid. WinUI has no built-in `DataGrid`; do not add a grid dependency
  for simple tables without a separate design and accessibility review.
- Keep list item templates readable by keyboard and screen reader users. Expose
  item identity in text, not only in icons or columns.

### Draft preservation

Refreshing data must not erase user drafts or selection unexpectedly. The
current app explicitly preserves inventory quantity drafts, social link drafts,
and compendium drafts across refreshes. Future layout changes must keep that
behavior visible and reliable.

Repository basis: `src\P4G.SaveTool.WinUI\MainWindow.xaml.cs:1510-1562`

## Elevation & Depth

The app should be mostly flat. Use depth to separate window, transient, and
blocking surfaces, not to decorate sections.

### Materials

- Treat the app as base layers plus content layers. The base window establishes
  calm Windows-native context; content layers group related editing tasks,
  diagnostics, or blocking decisions.
- Mica is appropriate for long-lived app/window/titlebar surfaces if the app
  adopts system backdrops.
- Acrylic is appropriate only for transient light-dismiss surfaces such as
  flyouts, not for permanent editor panels.
- Do not tint or layer materials in ways that reduce contrast or make the save
  editor feel like a web dashboard.
- Use card-like surfaces only when they add meaningful grouping, state, or
  scanning value. Do not wrap every field group in cards to simulate Fluent.

Official basis:
<https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops>,
<https://learn.microsoft.com/en-us/windows/apps/design/style/mica>,
<https://learn.microsoft.com/en-us/windows/apps/design/style/acrylic>

### Dialog and flyout depth

- Use `ContentDialog` for blocking confirmation, approval, overwrite, discard,
  clear, and destructive decisions.
- Use `Flyout` or `MenuFlyout` for contextual lightweight actions.
- Use `InfoBar` for highly visible, non-blocking app status such as parse
  warnings, write completion, or partial edit restrictions.
- Use `ProgressBar` or `ProgressRing` while opening, applying, or saving if the
  operation can take perceptible time.

Official basis:
<https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/infobar>,
<https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/dialogs-and-flyouts/dialogs>,
<https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/progress-controls>

### Motion

Use built-in WinUI motion. Motion should be reactive, direct, and context
preserving. Do not add decorative delay, animated validation noise, or page
transitions that imply a web app.

Official basis:
<https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/motion>,
<https://learn.microsoft.com/en-us/windows/apps/develop/motion/page-transitions>,
<https://learn.microsoft.com/en-us/windows/apps/develop/motion/connected-animation>

## Shapes

Use native WinUI shapes and default corner radius values. Shape should signal
platform consistency and interaction state, not custom branding.

### Shape rules

- Prefer default Button, TextBox, ComboBox, Slider, ListView, MenuBar, and
  ContentDialog shapes.
- Avoid hand-built pill buttons, custom segmented controls, and bespoke cards
  when a native control expresses the same state.
- If a custom surface is necessary, name shape and brush resources by purpose,
  support Light/Dark/HighContrast, and test keyboard focus, hover, pressed,
  disabled, selected, and error states.
- Section groups should be separated by spacing, headings, and alignment before
  borders or card chrome.

### Icon shape and identity

- Use icons only when they are immediately clear or paired with text.
- Icon-only buttons must have an accessible name.
- Prefer Segoe Fluent Icons through `SymbolIcon`, `FontIcon`, or `IconSource`
  where native controls expose icon slots.

Official basis:
<https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/icons>,
<https://learn.microsoft.com/en-us/windows/apps/design/accessibility/basic-accessibility-information>

## Components

### Design-to-implementation gates

- A control inventory is not a visual design. Adding CommandBar, InfoBar, or
  individual controls to the existing dense form is insufficient unless the
  shell silhouette, hierarchy, spacing, states, and accessibility are designed
  together.
- Use Windows UI Kit / Windows Design Kit Figma patterns as the design starting
  point when creating shell, navigation, dialog, layout, component, style, or
  token evidence.
- Figma work must show the main shell plus representative empty, loaded, dirty,
  busy, warning/error, destructive-confirmation, and save-complete states before
  XAML implementation starts.
- Each stage needs independent GPT-5.5 review. Visual review must cite concrete
  evidence, such as Figma frames or screenshots, and should call out measured
  spacing, command hierarchy, state visibility, and accessibility risks.

### Architecture boundaries

- WinUI code must depend only on Application, Contracts, and Presentation.
  Catalog, Domain, and SaveFormat details must be exposed through those layers
  rather than referenced directly from WinUI. After UI architecture changes,
  run `WinUIArchitectureTests`.

Repository basis: the WinUI project references only Application, Contracts, and
Presentation in `src\P4G.SaveTool.WinUI\P4G.SaveTool.WinUI.csproj:38-42`;
architecture tests forbid Domain, SaveFormat, and Catalog references in
`tests\P4G.SaveTool.WinUI.Tests\WinUIArchitectureTests.cs:13-18` and
`tests\P4G.SaveTool.WinUI.Tests\WinUIArchitectureTests.cs:41-70`.

### Shell commands

Current command set:

- Open save...
- Apply edits
- Save
- Save as...
- About

Design rules:

- Frequent/core commands stay on the canvas or command surface; less frequent
  commands can live in menus.
- Order primary commands by workflow importance: Open, Apply, Save, Save as.
- Keep Save disabled unless the app can write safely; keep editing disabled
  while no save is open or while a busy operation is running.
- Every command must be reachable by keyboard. Add keyboard accelerators and
  access keys as the command surface matures.
- Do not hide required commands at smaller widths without an overflow, menu, or
  alternate route.

Official basis:
<https://learn.microsoft.com/en-us/windows/apps/design/basics/commanding-basics>,
<https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/command-bar>

### File open, save, and overwrite

- File open should continue to use the Windows file picker and drag/drop for
  local `.bin` save files.
- Reject unsupported paths with a clear diagnostic and a blocking message only
  when the operation cannot continue.
- Before writing, make the target file path and dirty/can-write state visible.
- Because current persistence replaces the target without a backup file, future
  UI work around Save should consider an explicit first-save warning, a backup
  option, or a clear "Save as..." escape path.
- Raw byte preservation is a SaveFormat-layer concern: write paths should use
  snapshots and codec patching rather than UI-level byte handling.
- UI must preserve and expose unknown choices where current application and
  presentation projections expose them, and surface unsupported edits as
  diagnostics.
- Current canonicalization/normalization exceptions include unsupported party
  members canonicalizing to Blank and inventory writes filtering non-writable
  stacks. Changing these requires application/presentation tests and an
  explicit parity decision.

Repository basis:

- File picker and `.bin` validation: `src\P4G.SaveTool.WinUI\MainWindow.xaml.cs:319-356`
- Drag/drop open: `src\P4G.SaveTool.WinUI\MainWindow.xaml.cs:568-638`
- Save applies fields and replaces file: `src\P4G.SaveTool.WinUI\MainWindow.xaml.cs:452-510`
- Replace without backup: `src\P4G.SaveTool.WinUI\SafeFilePersistence.cs:55-64`
- SaveFormat copies original bytes and applies field patches:
  `src\P4G.SaveTool.SaveFormat\P4GSaveCodec.cs:55-73`,
  `src\P4G.SaveTool.SaveFormat\SaveSnapshot.cs:12-73`
- Current party and inventory normalization:
  `src\P4G.SaveTool.Presentation\PartyConfigurationProjection.cs:22-35`,
  `tests\P4G.SaveTool.Presentation.Tests\SaveEditorViewModelTests.cs:125-142`,
  `src\P4G.SaveTool.Application\SaveApplicationService.cs:520-523`,
  `src\P4G.SaveTool.Application\SaveApplicationService.cs:777-782`,
  `src\P4G.SaveTool.Application\SaveApplicationService.cs:858-864`

### Forms and editors

- Use WinUI `TextBox`, `NumberBox`, `ComboBox`, `Slider`, `ToggleSwitch`,
  `RadioButtons`, and `CalendarDatePicker` according to the data type.
- For numeric fields, prefer `NumberBox` in new UI unless draft text, exact
  parsing, or legacy validation requires `TextBox` plus diagnostics.
- Use `ComboBox` for 4 or more choices. Keep `IsTextSearchEnabled` for long
  catalogs.
- Use visible `Header` values for field labels.
- Preserve blank/default values when they are legitimate domain states. Do not
  "clean up" empty-looking values without domain validation.
- Apply edits explicitly where the app needs a review step; if a future field
  commits immediately, show how that affects dirty state.

### Diagnostics and state

- Diagnostics are a primary component, not a debug afterthought.
- Display severity, code, target, and message. Keep codes copyable or easy to
  transcribe.
- Use `InfoBar` for a current high-level problem or success; use a `ListView`
  or details panel for multiple diagnostics.
- Do not rely on red/yellow/green alone. Pair color with text and code.
- Keep "Has save", "Dirty", and "Can write" visible or easily discoverable in
  the shell.

Repository basis: `src\P4G.SaveTool.Contracts\SaveDiagnostic.cs:3-7`,
`src\P4G.SaveTool.WinUI\ShellStateFormatter.cs:22-35`,
`src\P4G.SaveTool.WinUI\MainWindow.xaml:390-410`

### Destructive actions

Destructive save-editor actions include deleting inventory entries, deleting a
social link, removing a compendium item, clearing the compendium, discarding
unapplied drafts, and overwriting a save file.

Rules:

- Use `ContentDialog` before destructive actions unless undo is available.
- Dialog title should name the action: "Delete inventory item?", "Clear
  compendium?", "Overwrite save file?"
- Dialog body should identify the affected item, count, file, or section.
- Primary button should be the destructive verb: "Delete", "Clear",
  "Overwrite". Secondary button should be "Cancel".
- After completion, show non-blocking confirmation or updated diagnostics.

Repository basis: current social link delete, compendium remove/clear, and
inventory delete mutate working state immediately in
`src\P4G.SaveTool.WinUI\MainWindow.xaml:179-183`,
`src\P4G.SaveTool.WinUI\MainWindow.xaml.cs:2385-2422`,
`src\P4G.SaveTool.WinUI\MainWindow.xaml.cs:2557-2628`, and
`src\P4G.SaveTool.WinUI\MainWindow.xaml.cs:3242-3283`.

### Unknown and unsupported domain data

- Unknown IDs should remain visible if they are present in the save and the
  current projection exposes them.
- Unsupported edit targets should be rejected with diagnostics unless they are a
  documented current canonicalization/normalization exception.
- UI should distinguish "unknown but preserved" from "unsupported edit".
- Avoid controls that drop values just because a value is not in the current
  catalog list.
- Do not describe all unknown or unsupported data as universally raw-preserved:
  raw byte preservation belongs to SaveFormat snapshots/codecs, while UI
  preservation follows current application and presentation projections.
- Known current exceptions are unsupported party members canonicalizing to Blank
  and inventory writes filtering non-writable stacks; changing either behavior
  needs application/presentation tests and a parity decision.

Repository basis: unsupported inventory and persona edits produce diagnostics
in `src\P4G.SaveTool.Application\SaveApplicationService.cs:270-279` and
`src\P4G.SaveTool.Application\SaveApplicationService.cs:976-984`; current
projection and normalization behavior is shown in
`src\P4G.SaveTool.Presentation\PartyConfigurationProjection.cs:22-35`,
`tests\P4G.SaveTool.Presentation.Tests\SaveEditorViewModelTests.cs:125-142`,
`src\P4G.SaveTool.Application\SaveApplicationService.cs:520-523`,
`src\P4G.SaveTool.Application\SaveApplicationService.cs:777-782`, and
`src\P4G.SaveTool.Application\SaveApplicationService.cs:858-864`.

### Accessibility and keyboard

- Keyboard is a primary interaction model. Maintain logical tab order, visible
  focus, Enter/Space activation, and Esc dismissal.
- Add `AutomationProperties.Name` for icon-only controls, ambiguous text, and
  custom templates.
- Preserve and expand existing AutomationProperties coverage on jump buttons,
  basic fields, state, and diagnostics.
- Test with keyboard only, Narrator, Accessibility Insights, and all Windows
  contrast themes before shipping broad UI changes.

Official basis:
<https://learn.microsoft.com/en-us/windows/apps/design/accessibility/keyboard-accessibility>,
<https://learn.microsoft.com/en-us/windows/apps/develop/input/keyboard-interactions>,
<https://learn.microsoft.com/en-us/windows/apps/develop/input/keyboard-accelerators>,
<https://learn.microsoft.com/en-us/windows/apps/develop/input/access-keys>,
<https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-overview>,
<https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-testing>

Repository basis: current jump buttons and key fields include automation names
in `src\P4G.SaveTool.WinUI\MainWindow.xaml:43-50`,
`src\P4G.SaveTool.WinUI\MainWindow.xaml:68-71`, and
`src\P4G.SaveTool.WinUI\MainWindow.xaml:398-404`.

### Localization and globalization

- Move user-facing strings to `.resw` resources as localization work begins.
- Use `x:Uid` and localize `AutomationProperties.Name`.
- Format numbers, dates, and diagnostics with culture-aware rules where domain
  data is not byte-exact.
- Support `FlowDirection` and RTL layout if the app is localized.

Official basis:
<https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/mrtcore/localize-strings>,
<https://learn.microsoft.com/en-us/windows/apps/design/globalizing/globalizing-portal>,
<https://learn.microsoft.com/en-us/windows/apps/design/globalizing/adjust-layout-and-fonts--and-support-rtl>

### XAML and NativeAOT composition constraints

- Do not convert template runtime `{Binding}` to typed `x:Bind` as a visual
  refactor. NativeAOT preservation and tests currently depend on runtime
  bindings that call `ToString()`.
- Avoid custom `ControlTemplate` work unless a native control cannot express
  the state and the change includes accessibility, theme, High Contrast, and
  keyboard coverage.
- Use `{ThemeResource}` at usage sites so theme changes update live.
- If a binding or template change affects generated code, run the relevant
  WinUI architecture tests.

Repository basis:
`src\P4G.SaveTool.WinUI\XamlBindingPreservation.cs:8-21`,
`tests\P4G.SaveTool.WinUI.Tests\WinUIArchitectureTests.cs:650-665`

## Do's and Don'ts

### Do

- Do choose native WinUI controls before custom UI.
- Do keep the current file path, dirty state, can-write state, and diagnostics
  visible.
- Do use `ThemeResource` brushes and default WinUI typography.
- Do use visible labels and control headers.
- Do preserve user drafts and expose unknown choices where current projections
  expose them.
- Do confirm destructive actions or provide undo.
- Do make diagnostics readable without color.
- Do keep keyboard, screen reader, and High Contrast behavior in scope for UI
  changes.
- Do use `InfoBar` for non-blocking status and `ContentDialog` for blocking
  decisions.
- Do stage UX, information architecture, interaction design, visual evidence,
  accessibility/safety review, and implementation in that order.
- Do require independent GPT-5.5 review for each later design/implementation
  stage, with visual review based on screenshots, Figma frames, or equivalent
  evidence.
- Do cite this document and update it when a future UI decision changes the
  design system.

### Don't

- Don't implement UI first or treat XAML changes as the design source of truth.
- Don't turn this into a web app design system with CSS-like palettes, cards for
  everything, or dashboard chrome.
- Don't approve Fluent visual quality from guesses; review the Figma shell,
  state surfaces, and spacing evidence before implementing XAML.
- Don't invent brand hex colors or hard-code Light/Dark colors.
- Don't replace standard control templates for visual novelty.
- Don't add `NavigationView`, `TabView`, or custom tabs unless the app shape
  actually needs them.
- Don't wrap `ListView` or `GridView` in an extra vertical `ScrollViewer`.
- Don't use placeholder text as the only label.
- Don't communicate errors, warnings, rarity, or dirty state by color alone.
- Don't silently save over a target without showing the target and write state.
- Don't delete, clear, discard, or overwrite without confirmation unless undo is
  implemented.
- Don't convert runtime bindings to `x:Bind` without preserving NativeAOT
  behavior and updating tests.
