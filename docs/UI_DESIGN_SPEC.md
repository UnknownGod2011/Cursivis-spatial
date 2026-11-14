# Cursivis Spatial UI design spec

- Source of truth: `unity/CursivisSpatial/Assets/Cursivis/Scripts/UI/CursivisTheme.cs` and the component scripts named in each section. Paths below are relative to `unity/CursivisSpatial/Assets/Cursivis/`.
- Original values come from `C:\Users\Admin\cursivis-ref\notes\ui-audit.md` (the WPF app), converted to Unity `#RRGGBBAA`.
- Units:
  - Every canvas uses scale 0.001, so 1 canvas unit = 1 mm (`Editor/SceneBuilder.cs:165-183`).
  - Font sizes are TextMeshPro em sizes in mm.
  - Angles are full visual angles: `2·atan(size / 2d)`.
- Nothing here was checked in a headset. Colors, legibility and angular sizes are computed from code, not observed.

## 1. Identity

| Aspect | Spec |
|---|---|
| Look | Monochrome graphite glass in dark mode (the default) and white glass in light mode. This is the original Cursivis look. The original's brushes named Cyan, Magenta and Gold were actually silver greys, and the port keeps that monochrome palette. |
| Saturated color | Used only to carry meaning. Brand cyan `#49E3FF` means "Cursivis is engaged": Ask Cursivis, Talk, selection glow, listening ring, highlights. A cyan→magenta (`#FF5BD6`) arc means "thinking". Snip blue marks a room selection. Warning, error and success colors mark uncertainty and failures. |
| Shape | Nested rounded rectangles: card 22.9 → body 18 / header 14.7 → buttons up to 19.7 (pill). The orb is a glass disc with a hairline rim. |
| Motion | Kept from the original: orb breathing pulse, silver shine on status text and wordmark, 4-ring completion burst, panel rise, word-by-word reveal. Added: staggered chip reveal, a rotating thinking arc, press scale and click sounds. |
| Type | Inter Regular, SemiBold and Bold (Inter Latin Ext as fallback) for UI, IBM Plex Mono for code, Material Icons for glyphs. Text is 1.4× the original px × 0.817 mm. |
| Brand marks | "Cursivis" wordmark with a silver shine in the result-card header, "CURSIVIS" inside the orb, and the onboarding tagline "Selection is Context. Gesture is Intent. Cursivis is Action." (`Scripts/Core/CursivisVersion.cs:6`). No logo image is used in the UI. |

## 2. Color tokens

Theme state:
- `CursivisTheme.P` returns the active palette.
- `CursivisTheme.SetDark` / `Toggle` switch instantly, raise `Changed` and persist the choice in PlayerPrefs `cursivis.theme.dark` (`CursivisTheme.cs:107-126`).
- Every component re-applies its colors when `Changed` fires.

"Original" lists the dark / light values from `ui-audit.md` §12.2. "=" means unchanged.

### 2.1 Surfaces and text (`CursivisTheme.cs:61-105`)
| Token | Dark | Light | Original | Change and reason |
|---|---|---|---|---|
| ShellA (shell top) | `#21262DE6` | `#FFFFFFEB` | `#21262DD0` / `#FFFFFFD0` | α 0.82 → 0.90 (dark) / 0.92 (light). Stays legible over a bright or busy room. |
| ShellB (shell bottom) | `#1A1F25E6` | `#F3F4F5E6` | (shell was flat) | Adds a subtle top-to-bottom gradient. |
| ShellBorder | `#FFFFFF1A` | `#00000014` | `#FFFFFF00` / `#00000010` | Dark gets a 10% white hairline (the original window border) so the edge reads against passthrough. |
| Header | `#101418D0` | `#FFFFFFC8` | `#101418C8` / `#FFFFFFB8` | Slightly more opaque. |
| Surface (body) | `#101418D0` | `#FFFFFFC4` | `#101418C8` / `#FFFFFFB2` | Slightly more opaque. |
| SurfaceBorder | `#FFFFFF10` | `#00000012` | `#FFFFFF00` / = | Dark gets a 6% hairline. |
| ShadowAlpha | 0.28 | 0.16 | ≈0.10 effective (24 px, `#66000000` × 0.24) | Stronger, so panels separate from the room. |
| TextMain | `#F5F7FAFF` | `#111418FF` | = | |
| TextMuted | `#A9B0B8FF` | `#67707AFF` | = | |
| TextSoft | `#7A828CFF` | `#8C949CFF` | = | Low contrast, see §12.3. |
| Heading | `#F7FAFDFF` | `#0F1419FF` | = | |
| Bullet | `#D4DAE1FF` | `#5D6670FF` | = | |
| CodeFg | `#F2F5FAFF` | `#1B232DFF` | = | |
| CodeBg | `#1A1D2140` | `#E9ECEFFF` | `#1A1D2124` / `#F4F5F6FF` | More visible on both themes. |

### 2.2 Buttons and chips
| Token | Dark | Light | Original | Change and reason |
|---|---|---|---|---|
| BtnBg / BtnBorder / BtnText | `#FFFFFF1A` / `#FFFFFF26` / `#F7FAFFFF` | `#FFFFFFED` / `#00000018` / `#12161AFF` | = (ButtonPrimary) | In the original the dark header pills were near-black `#101317F0`. Here they use the same translucent primary as every other button. |
| BtnHoverBorder | `#FFFFFF52` | `#0000002C` | = (StrokeStrong) | |
| BtnPressedBg | `#171B20FF` | `#F3F4F5FF` | `#171B20FF` / `#FFFFFFFF` (SurfaceHigh) | Light pressed is grey, so a press shows on white buttons. |
| BtnSecondaryBg / Border / Text | `#0000000E` / `#FFFFFF18` / `#A9B0B8FF` | `#FFFFFFE7` / `#00000014` / `#6A727AFF` | = | |
| AccentBg / Border / Text | `#2C323AFF` / `#FFFFFF3A` / `#F7FAFFFF` | `#14141AFF` / `#14141AFF` / `#FFFFFFFF` | = | |
| ChipSelA → ChipSelB | `#13171CE6` → `#1C2127D8` | `#FFFFFFF5` → `#F5F6F7EC` | 3-stop diagonal: `#13171CD6`, `#1C2127C4`, `#111519D6` / `#FFFFFFF0`, `#F5F6F7E4`, `#FFFFFFF0` | The SDF shader supports a 2-stop vertical gradient only. More opaque. |
| ChipSelBorder / ChipSelText | `#F5F7FAFF` / `#FFFFFFFF` | `#1A1F24FF` / `#12161AFF` | = | |
| ChipA → ChipB | `#11151AC8` → `#14181DB8` | `#FFFFFFEC` → `#F4F5F6E2` | `#11151AA8`, `#14181D94`, `#0F1318A8` / `#FFFFFFE1`, `#F4F5F6D6`, `#FFFFFFE1` | α 0.58-0.66 → 0.72-0.78 (dark), 0.84-0.88 → 0.89-0.93 (light). The chips float over passthrough with no panel behind them. |
| ChipBorder | `#FFFFFF48` | `#20202030` | = / `#20202022` | Light border stronger. |
| ChipText | `#D9DEE4FF` | `#3A4048FF` | = / `#525861FF` | Light text darker for contrast. |

### 2.3 Orb, shine and accents
| Token | Dark | Light | Original | Change and reason |
|---|---|---|---|---|
| OrbCore0 / 1 / 2 (radial stops 0, 0.58, 1) | `#161A1FDC`, `#0F1317CC`, `#090D11BC` | `#FFFFFFEC`, `#FBFCFEE6`, `#F4F6F9DE` | = (idle palette) | The original's per-state core palettes (processing, listening, completed) are dropped. State is shown by the shader effects instead. |
| OrbInner0 / 1 / 2 | `#141D28F6`, `#0E151FEE`, `#090E16E6` | `#FFFFFFFC`, `#FCFDFEF6`, `#F5F7FAEE` | = | |
| OrbRim | `#FFFFFF60` | `#00000024` | = / `#00000014` | Light rim stronger. |
| OrbInnerStroke | `#FFFFFF22` | `#00000014` | = | |
| OrbShadow | `#08131F`, α 0.30 | `#000000`, α 0.10 | `#08131FCC` × 0.18 / `#00000033` × 0.05 | Stronger. `OrbView.cs:66` overrides the alpha. |
| OrbBrand | `#B7BFC8FF` | `#6B727AFF` | = | |
| OrbStatus | `#FFFFFFFF` | `#2A3037FF` | = | Defined but unused. The status label uses TextMain (`OrbView.cs:68`). |
| Burst ring even / odd | `#F5F7FAFF` / `#B6BEC6FF` | `#2A3037FF` / `#626973FF` | `#F5F7FA` / `#B6BEC6` in both themes | Light variant added (`OrbView.cs:69-71`). |
| TitleShineBase → TitleShine | `#C9D0D8FF` → `#FFFFFFFF` | `#5A6067FF` → `#D5DAE0FF` | = | |
| StatusShineBase → StatusShine | `#C0C8D0FF` → `#FFFFFFFF` | `#5F666EFF` → `#B9C0C8FF` | = (Processing). Listening base was `#F5F7FA` / `#2A3036`. | Listening reuses the Processing colors. |
| Highlight / HighlightBorder | `#49E3FF38` / `#49E3FFE6` | `#2F7DFF2E` / `#2F7DFFE6` | new | Text and image selection. Brand cyan in dark, the media site's blue `#2F7DFF` in light. |
| Snip / SnipFill | `#4FC3F7FF` / `#4FC3F720` | `#2F7DFFFF` / `#2F7DFF1C` | Lasso `#4FC3F7` / `#4FC3F73F`, no theme | Fill α 0.25 → 0.125, so the target stays visible. |
| Warning / Error / Success | `#FFC86BFF` / `#FF8A8AFF` / `#8BE3B5FF` | `#B26A00FF` / `#C23B3BFF` / `#1F8A57FF` | new | Uncertainty chips, error icon, toasts. |
| Brand (both themes) | navy `#121C2DFF`, cyan `#49E3FFFF`, magenta `#FF5BD6FF` | = | App icon colors | Cyan: Talk and Ask Cursivis style, orb glow and ring. Cyan → magenta: thinking arc (`CursivisOrb.shader:20-21`). |

## 3. Geometry tokens (`CursivisTheme.cs:33-39`)

| Token | mm | Original | Used by |
|---|---|---|---|
| RadiusCard | 22.9 | 28 px | Result card, workspace and onboarding shells |
| RadiusBody | 18.0 | 22 px | Result body, workspace body |
| RadiusHeader | 14.7 | 18 px | Result header |
| RadiusButton | 12.3 | 15 px | Button radius is `min(height/2, 1.6 × 12.3 = 19.7)`. Icon buttons are circles. |
| PadCard | 9.8 | 12 px | The constant is unused. The card uses a literal 9.8 inset. |
| Hairline | 1.0 | 1 px = 0.8 mm | The constant is unused. Panels draw 1.0 mm borders, buttons 1.2 mm (1.6 mm on hover). Snip rect 3, camera frame 2.2, image region 2.5. |
| Scale | 1 unit = 1 mm | 0.817 mm per WPF px | All canvases |

## 4. Type tokens (`CursivisTheme.cs:40-51`)

| Token | mm | Original px (×0.817 ×1.4) | Used by |
|---|---|---|---|
| TextH1 | 20.6 | 18 | Onboarding title. Markdown H1 computes the same value. |
| TextH2 / TextH3 | 19.4 / 18.3 | 17 / 16 | The constants are unused. `MarkdownTmp.cs:81` computes headings as `max(14, 19 − level) × 1.4 × 0.817`, giving 20.6 / 19.4 / 18.3 / 17.2 / 16.0 / 16.0. |
| TextTitle | 19.4 | 17 | Card wordmark "Cursivis" |
| TextBody | 14.9 | 13 | Answer text (lineSpacing 6, paragraphSpacing 4.9 TMP units), error message. Workspace prose uses 14.5, onboarding steps 13.9. |
| TextSubtitle | 13.7 | 12 | Card title, workspace panel titles |
| TextButton | 13.2 | 11.5 | Button labels. Auto-size runs from 0.78× to 1×. |
| TextMeta | 12.0 | 10.5 | Meta line, chips row, guided title, hints, captions. Workspace code uses 12.6 (IBM Plex Mono). Toast uses 13. |
| TextChip | 13.0 | 10 (would be 11.4) | Suggestion and Talk chips, orb status. Enlarged for ray targeting. |
| TextOrbBrand | 10.3 | 9 | "CURSIVIS" inside the orb, character spacing 18 |
| (literal) | 22 | 15 | Snip hint, white with a black outline (0.18, α 160) |

- Font assets are built by `Editor/SceneBuilder.cs:209-251`:
  - Dynamic SDF atlases of 1024×1024, SDFAA, sampling 72 pt (icons 64), padding 7.
  - Fallbacks: every Inter weight falls back to Inter-LatinExt-Regular; IBM Plex Mono falls back to Inter-Regular.
  - The TMP default font is Inter-Regular.
- The assets live in `Resources/Fonts & Materials/` so `<font="…">` tags resolve at runtime.

## 5. Motion tokens

| Motion | Spec | Original |
|---|---|---|
| Panel open (`PanelOpenSec`) | 0.28 s, ease-out cubic, alpha 0→1, rise from −14.7 mm to 0 (`UiKit.cs:293-300`) | 280 ms, 18 px rise |
| Panel close (`PanelCloseSec`) | 0.135 s, ease-in cubic, alpha →0, drop 0 → −8.2 mm, then inactive (`UiKit.cs:302-309`) | 135 ms, 10 px |
| Orb base scale (`OrbScaleSec`) | 0.22 s, ease-out cubic, scaled from the center. Idle 0.88, active 1.0, Completed and Error 0.98. | Same values. The original scaled from the top-left corner. |
| Orb pulse (`OrbPulseHalfSec`) | Ping-pong over 0.62 s, linear, from `max(scale, 0.96)` up by +0.07. In Listening the amplitude is ×`lerp(0.35, 1, micLevel)`. | Same, without the mic scaling |
| Completion burst | 4 rings, 80 ms stagger. Alpha 0 → 0.9 at 160 ms → 0 at 820 ms. Scale 0.72 → 1.24 + 0.05·i over 860 ms, ease-out cubic. | Same |
| Status shine | Period 2.35 s (Understanding, Processing, ProcessingVoice) or 2.0 s (Listening). Band ±0.22 of the text width, mix 0.9, left → right. | Same periods. Swept right → left with strength 0.64 dark / 0.28 light. |
| Wordmark shine | 2.65 s dark / 3.35 s light. Band ±0.18, strength 0.94 / 0.32, left → right. | Same periods and strengths |
| Guided ring fade (`ChipShowSec` / `ChipHideSec`) | Whole ring alpha, linear, in 0.18 s / out 0.22 s | Per chip, quad ease |
| Chip fly-out | Each suggestion chip flies from 18% of its offset (inside the orb) to its slot in 0.34 s, ease-out cubic, 45 ms stagger, scale 0.6 → 1; the Talk chip launches last (`OrbHud.cs:268-290`) | new |
| Chip shimmer while loading | Alpha 0.55-0.85, sine at 4.2 rad/s with a 0.9 rad phase step per chip; labels stay "…" until the real AI labels arrive (no fake results) | The original showed "..." placeholders |
| Chip landing pop | When the real labels arrive each chip pops 0.9 → 1 with ease-out-back over 0.3 s, 50 ms stagger | new |
| Talk glow | Soft glow behind Talk breathes at 2.6 rad/s while the ring is up; while listening it follows the mic level (alpha 0.35 + 0.45·level, 6.5 rad/s) | new |
| Body reveal | TMP `maxVisibleWords`. ≤110 words: 1 word per 20 ms. >110: 2 per 17 ms. >200: 3 per 14 ms. >320: 4 per 12 ms. | Same thresholds (the original counted regex tokens) |
| Follow-up chips | Appear after the reveal finishes: scale 0.85 → 1 with ease-out-back over 0.25 s, 70 ms stagger (`ResultCard.cs:388-390`) | new |
| Card loading shimmer | A soft highlight sweeps down the skeleton lines while the AI works (`ResultCard.cs:394`) | new |
| Button feedback | Critically damped spring (ω = 26/s, exact closed-form step so it stays stable at any frame rate): 1.03 on hover, 0.955 pressed, +1.2 velocity kick on click; colours ease at 24/s; a white border flash fades over 0.28 s on click (`UiKit.cs`) | Instant, no scale |
| Orb idle breathing | Scale ±1.2% on a 4.6 s sine (`OrbView.cs:122`) | new |
| Orb acknowledgement pop | +9% bump over 0.45 s when a selection is made (`OrbView.cs:123-124`) | new |
| Orb error shake | Horizontal shake, 3.2 mm amplitude decaying over 0.42 s, with the error tint (`OrbView.cs:128-129`) | Errors looked like success |
| Orb shader blends | Arc, ring and error tint approach at 8/s, glow at 5/s, mic level at 18/s. The arc spins at 2.4 rad/s (about 2.6 s per turn). | new |
| Orb follow | Position and rotation approach at 7/s | new |
| Dock fade / toast fade | 0.2 s / 0.18 s. Toasts stay 4 s. | new |
| Skeleton pulse | Sine at 1.6 rad/s, alpha 0.05-0.10 (+0.03 on the top stop), 0.55 rad phase step per bar | new |
| Theme switch | 0.24 s cross-fade of every palette colour, ease-out cubic (`ThemeFader`, `CursivisTheme.cs`) | Instant. A large panel flipping from dark to white at once is uncomfortable in a headset |

## 6. Orb (`Scripts/UI/OrbView.cs`, `Resources/Shaders/CursivisOrb.shader`, `Scripts/UI/OrbHud.cs:47-79`)

### 6.1 Geometry
- Core diameter D = 78 mm (`OrbHud.OrbDiameter`).
  - The original's 118 px would map to 96.4 mm.
  - The orb is drawn on one RawImage quad of 1.6 D = 124.8 mm. In shader UV the core radius is R = 0.3125.
- Layers, back to front:

| Layer | Spec |
|---|---|
| Shadow | `(1 − smoothstep(0.92 R, 1.45 R, r))²` × shadow alpha, centered (no offset) |
| Selection glow | Brand cyan, `0.35·g²` with `g = (1 − smoothstep(R, 1.55 R, r))·_Glow` |
| Listening ring | Brand cyan ring at radius `R·(1.12 + 0.18·level)` (43.7-50.7 mm), half-width about 0.05 R (2 mm), alpha `(0.45 + 0.5·level)·_Listen` |
| Core | 3-stop radial ramp (0, 0.58, 1) from focal point (0.30, 0.25), radius 0.70 |
| Inner disc | 0.898 D (70 mm), its own 3-stop ramp, plus an inner hairline |
| Rim | Hairline at the core edge. A sheen multiplier of 0.75-1.25 varies with height. |
| Thinking arc | Band at 1.07 R (41.7 mm), half-width about 0.035 R (1.4 mm). The segment covers about 8-42% of a turn with soft ends, colored cyan → magenta, alpha 0.9·`_Arc`. |
| Error tint | 35% wash of the core toward `#FF7373` |
| Dither | ±0.5/255 noise against banding |

- Burst rings: four RoundedRect rings of Ø 92.5 / 99.1 / 105.8 / 112.4 mm (1.186-1.441 D). Stroke 1.6 mm, the last 1.1 mm.
- Text:
  - "CURSIVIS" at 10.3 mm SemiBold in OrbBrand color, 2 mm below center.
  - Status label at 13 mm SemiBold, 57 mm below center, no wrap.
- Hit target: an invisible 78 mm circle button. Pressing it in Idle starts general Talk. Pinching it after a suggestion failure retries.

### 6.2 Moods (`OrbView.cs:74-94`)
| Mood | Scale | Pulse | Shader state | Status shine | Set when (status text) |
|---|---|---|---|---|---|
| Idle | 0.88 | – | All effects 0 | – | Start, cancel, not heard ("Offline" when offline) |
| Acknowledged | 1.0 | – | Glow to 1, then decays at 1.2/s and holds at 0.45 | – | Selection confirmed. Snip armed ("Snip"). |
| Understanding | 1.0 | yes | Arc 1, glow 0.35 | 2.35 s | Ask Cursivis ("Understanding…") |
| Listening | 1.0 | yes, scaled by mic level | Ring 1, radius and alpha follow the mic level | 2.0 s | Talk held ("Listening…") or tapped ("Listening… tap to stop") |
| ProcessingVoice | 1.0 | yes | Arc 1, glow 0.2 | 2.35 s | Voice sent ("Thinking about what you said…"), typed follow-up ("Working…") |
| Processing | 1.0 | yes | Arc 1, glow 0.2 | 2.35 s | Action running ("Working…") |
| Speaking | 1.0 | – | Ring 0.55 at a static radius (1.12 R) | – | Read aloud ("Speaking…") |
| Completed | 0.98 | – | All 0. Burst plays. | – | Suggestions ready, result shown |
| Error | 0.98 | – | Error tint 1 | – | "Couldn’t read it", "Couldn’t finish", "Couldn’t answer", "No AI backend configured" |

### 6.3 Placement (`App/CursivisApp.cs:859-961`)
- **Home:**
  - The orb sits at head + yaw·(0.21 m right, 0.235 m down, 0.58 m forward), about 20° right and 22° down, facing the head.
  - It re-centers when the head turns more than 30° or moves more than 35 cm. It does not move while Talk is listening.
- **Near a selection** (rewritten after simulator screenshots showed the ring and card at the edge of the view):
  - The orb hugs the edge of the selection that faces the centre of the view: yaw = selection yaw ∓ (half its angular
    width − 2°), clamped to ±24°; pitch = selection pitch − 2°, clamped to −24° … +6°.
  - Distance is `clamp(0.62 × selection distance, 0.5, 0.66)` m, so it floats in front of the workspace documents.
- **Layout follow:** if the head ends up more than 45 cm from where the layout was built (a recenter, the user moving,
  or a simulator pose jump), the workspace, orb, card, chip and onboarding panels move with it.

## 7. Guided ring and home dock (`Scripts/UI/OrbHud.cs`)

### 7.1 Layout
The ring root is 480×300 mm, centered on the orb, on the 500×330 OrbCanvas (ISDK ray and poke).

| Element | Size (mm) | Center (mm) | Style |
|---|---|---|---|
| Title chip | 330×30, r 15 | (0, +92) | ChipA → ChipB fill, ChipBorder 1 mm. Text 12 mm SemiBold TextMuted, no wrap. |
| Suggestion 0 / 1 | 176×50 | (−138, +34) / (+138, +34) | Chip style, 13 mm label, 17.6 mm icon |
| Suggestion 2 / 3 | 176×50 | (−138, −30) / (+138, −30) | Chip style |
| Talk | 150×50 | (0, −98) | Talk style (cyan), mic glyph. Never AI-generated. |
| Hint | 420×20 | (0, −136) | 12 mm Regular TextSoft, no panel behind it |

- Chips are numbered left to right, top to bottom.
- The original used five slots clockwise from the top, with chip centers at ±0.907 D / ±0.339 D. Here the 176 mm chips sit at ±1.77 D, +0.44 / −0.38 D, which leaves an 11 mm gap to the orb edge.
- The title replaces the original's top slot, and Talk takes the bottom.

### 7.2 States
| State | Title | Chips | Talk | Hint |
|---|---|---|---|---|
| Loading (`SetLoading` `:150-164`) | "Reading your snip…", "Reading “<title>”…" or "Reading your selection…" | "..." label, More glyph, disabled | Enabled | "Talk is ready while suggestions load" |
| Ready (`SetSuggestions` `:166-189`) | AI title. "· hard to read" is appended when quality is unreadable, "· basic options" when the suggestions are fallbacks. | Label plus icon from `Icons.ForSuggestion` (`CursivisTheme.cs:184-214`). Unused slots are hidden. | Enabled | "Pinch an action, or hold Talk and ask anything about it" |
| Chosen (`MarkChosen`) | – | The picked chip shows the Selected look | – | – |
| Error (`SetError` `:191-197`) | The error message | Disabled | Enabled | "Pinch the orb to retry, or hold Talk" |
| Listening | – | – | Pressed look while held | "Release to send · or tap and speak, I'll stop when you pause" |

The ring fades in or out as one group and blocks rays only while it is shown. Each time it opens, the chips replay the staggered reveal (§5).

### 7.3 Home dock
- Shown only in Idle and Dismissed. Fades over 0.2 s.
- Background: 404×56 mm pill (r 28) with Shell colors and a 10 mm shadow, centered at (0, −96) below the orb.
- Buttons:
  - Snip, 118×42 at x −133, Primary, crop glyph
  - Workspace, 150×42 at x 5, Primary
  - Theme, 42 mm circle at x 110, glyph of the theme you would switch to
  - Help, 42 mm circle at x 160, opens onboarding

## 8. Result card (`Scripts/UI/ResultCard.cs`)

### 8.1 Layout
The card is 520×404 mm, centered at (0, +8) on the 540×430 ResultCanvas, with ISDK ray and poke.

| Part | Size (mm) | Position | Spec |
|---|---|---|---|
| Shell | 520×404, r 22.9 | (0, 8) | ShellA → ShellB, ShellBorder 1 mm, shadow 18 mm at ShadowAlpha. Blocks rays from passing through. |
| Header | 500.4×46, r 14.7 | 9.8 mm top inset | Header fill, SurfaceBorder |
| ↳ Wordmark | 19.4 mm SemiBold | Left, 23.8 mm inset | "Cursivis" with the silver shine (§5) |
| ↳ Title | 13.7 mm Regular TextMuted | 170 mm box at x −54 | Result title or action label, no wrap |
| ↳ Icon buttons | 34 mm circles, 40 mm pitch | From the right: Close, Pin, Expand, Theme, Talk (mic), Read aloud (speaker) | Icon style. Pin shows Selected while pinned. Read aloud switches to a Stop glyph and Selected while speaking. Expand switches glyph. |
| Meta line | 12 mm | 70 mm below the shell top | Left: "Output · <action>", "You said “<transcript>”" for voice, or "Output · couldn’t finish". SemiBold TextMuted. Right: latency "x.x s", Regular TextSoft. |
| Body | 500.4×232, r 18 | Below the meta line | Surface fill, SurfaceBorder. Viewport insets 14 / 16 / 10 / 10 with RectMask2D. Vertical ScrollRect: elastic 0.12, inertia, deceleration 0.12, scroll sensitivity 18. |
| ↳ Answer | 14.9 mm Regular TextMain | Top of the scroll content | Markdown through `MarkdownTmp` (§8.2) |
| ↳ Follow-up chips | Up to 3, 34 mm high, width `clamp(36 + 7.4·chars, 110, 190)`, 8 mm gap | Below the answer | Chip style, 12 mm. A pinch sends the text as a typed follow-up. |
| ↳ Scroll thumb | 4 mm wide, height `max(19.6, viewport²/content)` | 7 mm from the body's right edge | TextMain at 28%. Shown only when the content overflows. |
| Uncertainty chip | 240×28, r 14 | Left, 8 mm below the body | medium: Warning, "Partly uncertain: <note>". high: Error, "Not enough evidence: <note>". low: Success, "Grounded in your selection". none: hidden. Fill is the color at 16% → 10% (success 14% → 8%), border at 80% (60%). |
| Evidence chip | 240×28, r 14 | Right | Eye glyph plus evidence text, ChipA → ChipB, TextMuted |
| Footer | 42 mm high, centered 30.8 mm above the shell bottom | x −174 / −24 / 104 / 204 | "Ask follow-up" 150 (Accent, mic; "Listening…" while recording), "More options" 140, "Redo" 96, "Copy" 92 (Primary) |
| Grab handle | 118×11 visual pill, r 5.5 | 14 mm below the shell | TextMain at 55% → 45%. The collider is a 140×26×12 mm box with ISDK ray grab and hand grab that moves `ResultRoot` (`SceneBuilder.cs:117-142`). |

### 8.2 Markdown (`Scripts/UI/MarkdownTmp.cs:47-125`)
- Supported elements:
  - Headings (see §4), `**bold**`, `__bold__`, `*italic*`, links (shown underlined).
  - Inline code: IBM Plex Mono in CodeFg on a CodeBg `<mark>`.
  - Fenced code: mono, 88% size, line height 118%, spaces kept.
  - Bullets: "•" with a hanging indent. Numbered lists: "N." with a hanging indent. Checklists: ☐ / ☑.
  - Tables: cells joined with a muted "│". Quotes: muted italic.
- Model text is escaped: `<` becomes `<` + zero-width space, so stray TMP tags do nothing.
- Read-aloud uses `ToSpeech` (`:130-147`). It strips Markdown and replaces code blocks with "(code omitted)".

### 8.3 States and motion
| State | Spec |
|---|---|
| Open / close | `PanelAnimator` (§5) on the shell. The whole canvas group fades. |
| Loading (`ShowLoading` `:233-247`) | Five skeleton bars, 12 mm high, r 6, 26 mm apart, left-aligned, widths 95/82/95/82/55% of 470 mm. They pulse (§5). Below them: "Cursivis is working on “<label>”…" (12 mm SemiBold TextMuted). Meta: "Output · <label>". Chips hidden. Footer, Read aloud and Talk disabled. |
| Result (`ShowResult` `:266-306`) | Word reveal (§5). Scroll resets to the top. Chips and follow-ups filled. Footer enabled. |
| Error (`ShowError` `:249-264`) | Warning glyph (30 mm, Warning color) above the message (14.9 mm, centered), plus a "Retry" Accent button (130×40) when retry is possible. Footer disabled except More options. |
| Listening for a follow-up | "Ask follow-up" and the header mic show Selected. The label reads "Listening…". |

### 8.4 Placement (`CursivisApp.cs:913-927`, `:947-957`)
- **Initial position:** `ResultRoot` goes 0.8 m from the head, 5 cm below eye height, facing the head.
  - Yaw: on the far side of the orb from the selection (orb yaw ∓ 21°), or 22° left of a home orb, clamped to ±18°.
    The card is about 38° wide at 0.8 m, so it stays inside a ±37° cone and never covers the selection's inner edge.
- **Pinned (default):** the card stays in place.
- **Unpinned:** when the card is more than 35° off the forward direction, it drifts to −18° yaw at 0.78 m.
- **Expand:** scales `ResultRoot` by 1.28.

## 9. Selection, snip, workspace, onboarding, toast

### 9.1 Selection chip (`Scripts/Selection/SelectionInput.cs:439-475`)
- Pill: 360×58 mm, r 29, Shell colors, 10 mm shadow, on the 380×110 ChipCanvas.
- Buttons:
  - "Ask Cursivis", 176×44, Talk style (cyan), sparkle glyph
  - "Retake" (camera) or "Clear" (workspace), 108×44, Secondary
  - Cancel ×, 44 mm circle
- Caption: 12 mm SemiBold TextMuted, 40 mm below. Texts: "Room snip · nothing is sent until you ask", "Whole paragraph selected", "N words selected", "Photo selected", "Photo region selected".
- Placement (`CursivisApp.cs:470-486`):
  - 4.5 cm below the bottom edge of the selection and 3 cm toward the head.
  - At most 0.9 m from the head and no lower than 0.45 m below eye height.
  - Showing it plays the confirm tone and sets the orb to Acknowledged.

### 9.2 Snip surface (`SelectionInput.cs:32-188`)
- Surface:
  - A 1600×1100 mm canvas with ISDK ray only (no poke). Its hit area is a full-size RoundedRect at alpha 0.001.
  - It is placed facing the user at the Depth API hit distance minus 1 cm, clamped to 0.35-2.5 m, or at 0.85 m without depth.
  - It scales with distance so its angular size matches 0.85 m (`CursivisApp.cs:320-342`).
- Camera-frame outline:
  - RoundedRect, r 18, 2.2 mm border in TextMain at 35%, no fill.
  - It marks the area the passthrough camera actually sees, from `TryGetCameraFrameOnPlane`, and refreshes every 10 frames.
- Rubber band:
  - Shown while pinch-dragging. RoundedRect, r 10, 3 mm Snip-color border, SnipFill.
  - The drag starts immediately (no drag threshold) and is clamped to the surface.
- Handles: four 16 mm discs at the corners, filled with the Snip color.
- Hint: 22 mm SemiBold white with a black outline, 220 mm above center. Texts: "Starting the camera…", "Pinch and drag to frame what you want Cursivis to see", "Capturing…".
- Rejections:
  - A side shorter than 30 mm in world size → toast "That selection is too small. Pinch and drag a larger box."
  - Losing hand tracking for 1.2 s cancels the drag. This check runs only on device; Editor and Windows builds skip it (`CursivisApp.cs:963-999`).

### 9.3 Workspace panels (`Scripts/App/AppPanels.cs:15-139`)
- Four 400×300 mm canvases ("Research abstract", "Aviso a los vecinos", "Photo · coastal footbridge", "reviews.js"), with ray and poke.
- Placement: 1.0 m from the head, 0.06 m below eye level, in a 2×2 grid at yaw ±13.5° and pitch +11.5° / −7°, each facing the head (rows sit high so the home orb and dock never cover the bottom-right card) (`CursivisApp.cs:872-892`).
- Shell: 394×294, r 22.9, 14 mm shadow.
- Header row:
  - 20 mm glyph in TextMuted.
  - Title at 13.7 mm SemiBold TextMain.
  - Hint at 12 mm TextSoft: "pinch or drag to select", "code · pinch or drag to select" or "pinch, or drag a region".
- Body: 374×234, r 18, Surface fill.
- Text: 14 / 14 / 12 / 12 mm insets. Prose at 14.5 mm Inter (lineSpacing 4). Code at 12.6 mm IBM Plex Mono (lineSpacing 2). Overflow is truncated.
- Text selection:
  - A pinch-tap selects the whole block. A pinch-drag selects a word range.
  - The highlight is a TMP `<mark>` in the Highlight color with padding 2,2,1,1.
- Photo:
  - Aspect-fit inside 354×194 mm.
  - A drag larger than 8% of the image in both directions selects a region. Anything smaller selects the whole photo.
  - Region rectangle: r 6, 2.5 mm HighlightBorder stroke, Highlight fill.

### 9.4 Onboarding (`AppPanels.cs:141-189`)
- 600×380 canvas. Shell 592×372, r 22.9, 18 mm shadow.
- Shown 0.78 m in front, 2 cm up. It appears on first run (PlayerPrefs `cursivis.onboarded`) and from Help.
- Content:
  - Title "Cursivis Spatial" at 20.6 mm SemiBold.
  - Tagline at 12 mm TextMuted.
  - Three steps (Select / Ask Cursivis / Act or Talk) at 13.9 mm, each with an inline Material Icons glyph.
  - A privacy line at 12 mm TextSoft: what is sent to Gemini, and that the camera runs only while Snip is active.
- Buttons: "Snip my room" 170×44 and "Use workspace" 180×44 (Primary), "Got it" 120×44 (Accent).

### 9.5 Toast (`AppPanels.cs:191-229`)
- A 460×54 pill (r 27) on a non-interactive canvas, 0.62 m forward and 8 cm below eye level.
- Shell colors and a 10 mm shadow.
- Border: Warning at 70% for warnings, ShellBorder for info.
- Content: a 20 mm Warning or Check glyph in the Warning or Success color, and text at 13 mm SemiBold TextMain.
- Fades in 0.18 s and stays 4 s.

## 10. Interaction states (`Scripts/UI/UiKit.cs:126-269`)

`CursivisButton` responds to uGUI pointer events. ISDK `PointableCanvasModule` produces those events for hand-ray pinch, poke and controllers.

| Style | Normal | Hover | Pressed | Selected |
|---|---|---|---|---|
| Primary, Icon | BtnBg, BtnBorder, BtnText | Border BtnHoverBorder | Bg BtnPressedBg | Border ChipSelBorder |
| Secondary | BtnSecondaryBg / Border / Text | Border BtnHoverBorder | Bg BtnPressedBg | – |
| Accent | AccentBg / Border / Text | Border BtnHoverBorder | (no change) | – |
| Chip | ChipA → ChipB, ChipBorder, ChipText | Border ChipSelBorder, text ChipSelText | Same as Selected | ChipSelA → ChipSelB, ChipSelBorder, ChipSelText |
| Talk | ChipA → ChipB, border cyan 65%, text TextMain, glyph cyan | Border cyan 100% | Fill cyan 30% → 18% (dark) or 22% → 14% (light), border cyan | Same as pressed |
| Danger (unused) | BtnBg, BtnBorder, text Error | Border Error | Bg BtnPressedBg | – |

Common rules:
- Hover thickens the border from 1.2 to 1.6 mm and scales the button to 1.02.
- Press scales to 0.965 and plays a click: 1850 Hz, 28 ms, volume 0.35, spatial blend 0.6. Confirmations (selection made, Ask Cursivis) play a 1320 → 1760 Hz, 75 ms tone (`UiKit.cs:90-122`). Hands give no haptics, so sound is the confirmation.
- Disabled multiplies all four colors' alpha by 0.42 and ignores press and click.
- Clicks within 250 ms of the previous one are ignored.
- Leaving the button while pressed fires Release. That is how push-to-talk ends when the ray slips off.
- Talk timing (`CursivisApp.cs:645-703`):
  - Holding for 0.45 s or longer records until release.
  - A shorter tap switches to hands-free listening, which stops after 1.2 s of silence or on another tap.
  - Recordings are capped at 15 s (`Voice.cs:97-98`).

## 11. Rendering approach and performance

- **SDF rounded rectangles** (`Scripts/UI/RoundedRect.cs`, `Resources/Shaders/CursivisRoundedRect.shader`):
  - Each panel, chip, button, ring and bar is one quad of 4 vertices.
  - Vertex data:
    - color: top fill
    - uv0: local position and half size
    - uv1: radius, border, shadow size, shadow alpha
    - uv2: bottom fill
    - uv3: border color
    - tangent.x: rim highlight
  - The fragment shader computes the fill gradient, border, rim sheen and outer shadow analytically.
  - All RoundedRects share one material, so uGUI can batch them. Canvases enable the extra shader channels (TexCoord1-3, Tangent, Normal).
- **Passthrough compositing:**
  - The camera clears to (0,0,0,0), and an `OVRPassthroughLayer` underlay shows the room (`SceneBuilder.cs:60-81`).
  - Both UI shaders blend `SrcAlpha OneMinusSrcAlpha` for color and `One OneMinusSrcAlpha` for alpha, so the eye buffer's alpha carries UI coverage to the compositor.
  - On-device appearance is UNVERIFIED.
- **No real-time blur.** The original used none either. Shadows are analytic distance falloffs. Both shaders add ±0.5/255 dither against banding in dark gradients.
- **Orb:** one quad with up to about 8 alpha-over composites per pixel. The `atan2` arc runs only while `_Arc > 0.001`. The orb owns one material instance.
- **Text:** TMP SDF with dynamic atlases. The wordmark and orb-status shine call `ForceMeshUpdate` and rewrite vertex colors every frame while visible.
- **Per-frame work to profile on device:**
  - Every `CursivisButton` writes `localScale` each frame.
  - The loading skeleton re-sets its bars each frame, which dirties the canvas mesh.
  - The snip hit area is a 1.6×1.1 m transparent quad, so it adds overdraw while snipping.
  - A result card stacks 4-5 transparent layers (shell, header, body, chips, text). The workspace adds 4 panels of 2 layers each.
- **Project settings:**
  - URP: MSAA 4×, no HDR, no depth or opaque textures, no shadows, no post-processing, render scale 1 (`Editor/ProjectSetup.cs:132-166`).
  - Vulkan, single-pass instanced on Android.
  - The Depth API is enabled only while placing the snip surface.
  - `ProjectSetup.cs` (`AndroidFeatureIds`) requests the `com.meta.openxr.feature.foveation` OpenXR feature, but no foveation level is set in code (effect UNVERIFIED).
- **Measurement:** `Scripts/Core/CursivisMetrics.cs` logs FPS, memory and latencies. Quest frame rate has not been measured, and simulator FPS says nothing about Quest FPS.

## 12. Accessibility

### 12.1 Sizes
| Element | Size (mm) | Typical distance | Angle there | At 0.7 m | At 1.0 m |
|---|---|---|---|---|---|
| Body text (em) | 14.9 | 0.78 m (card) | 1.09° | 1.22° | 0.85° |
| Meta / chip-row text | 12.0 | 0.78 m | 0.88° | 0.98° | 0.69° |
| Suggestion / Talk label | 13.0 | 0.5-0.72 m (ring) | 1.49-1.03° | 1.06° | 0.74° |
| Card wordmark | 19.4 | 0.78 m | 1.42° | 1.59° | 1.11° |
| Orb brand text | 10.3 | 0.58 m (home) | 1.02° | 0.84° | 0.59° |
| Workspace prose / code | 14.5 / 12.6 | 1.0 m | 0.83° / 0.72° | 1.19° / 1.03° | 0.83° / 0.72° |
| Suggestion / Talk chip height | 50 | 0.5-0.72 m | 5.7-4.0° | 4.09° | 2.86° |
| Footer buttons, dock buttons | 42 | 0.78 m / 0.58 m | 3.08° / 4.15° | 3.44° | 2.41° |
| Ask Cursivis, Cancel | 44 | up to 0.9 m | ≥ 2.80° | 3.60° | 2.52° |
| Header icons, follow-up chips | 34 | 0.78 m | 2.50° | 2.78° | 1.95° |
| Grab handle collider (height) | 26 | 0.78 m | 1.91° | 2.13° | 1.49° |
| Orb hit target | 78 | 0.58 m | 7.69° | 6.37° | 4.46° |

- Meta's target guidance is about 2.5-3° and at least 48 dp. The grab handle (1.9°) falls short at its own distance. The 34 mm header icons and follow-up chips sit at the 2.5° lower bound.
- Gaps below Meta's 12 mm spacing guidance:
  - card header icons: 6 mm
  - card footer: 5, 10 and 6 mm
  - home dock: 4, 9 and 8 mm
  - selection chip: 6 and 8 mm
  - follow-up chips: 8 mm
- Gaps that meet the guidance: guided ring 14-18 mm (100 mm between the left and right columns), onboarding 12 mm.
- Footprints:
  - The card spans about 37° × 29° at 0.78 m (46° × 37° expanded).
  - The guided ring spans 35-49° horizontally at 0.72-0.5 m.
  - The workspace grid spans about 50° × 36° at 1.0 m.
- Meta's px-based text guidance (at least 14 px, 18 px comfortable) depends on panel pixel density. Legibility at these angles must be checked in a headset.

### 12.2 Non-visual and alternative input
- Every flow works with hand ray or poke. Controllers work through the same uGUI events. No step needs typing or a system keyboard.
- Talk is a voice alternative to pinching suggestions. Voice can also switch theme, read aloud, show options, clear the selection or start a new conversation (`clientAction`, `CursivisApp.cs:790-800`).
- Read aloud speaks any result.
- Uncertainty is shown with a glyph, a text label and a color, never color alone.
- Every press plays an audible click.
- The light theme helps in bright rooms.

### 12.3 Contrast (computed)
Method:
- WCAG 2.x contrast ratio.
- Panel colors are alpha-composited over the worst-case passthrough: pure white behind the dark theme, pure black behind the light theme. Real rooms fall in between.
- Chip tints are ignored.

| Pair | Dark | Light |
|---|---|---|
| TextMain on body surface | 16.1:1 | 17.8:1 |
| TextMain on shell | 10.5:1 | 15.5:1 |
| TextMuted on shell | 5.1:1 | 4.2:1 |
| TextSoft on shell | 2.9:1 (4.1:1 over black) | 2.6:1 (3.1:1 over white) |
| ChipText on ChipA | 6.9:1 | 8.9:1 |
| Talk glyph (cyan) on ChipA | 6.1:1 | 1.3:1 |
| Warning / Error / Success text on shell | 7.4 / 5.0 / 7.4:1 | 3.6 / 4.4 / 3.6:1 |

Open issues:
- TextSoft is used for the orb hint (which has no panel behind it), card latency, workspace hints and the onboarding privacy text. It stays below 4.5:1 in both themes.
- In the light theme, the cyan Talk and Ask Cursivis glyph and border have very low contrast (1.3:1). Their labels use TextMain and stay readable.
- The light-theme status colors fall below 4.5:1.
- The orb status label and the hint have no backing panel or outline, so their contrast depends on the room. The snip hint has an outline.

## 13. Known deviations and open items (code compared with notes and comments)

1. **Orb highlight position.**
   - WPF `GradientOrigin (0.3, 0.25)` is measured with y pointing down, so the original orb's light spot is at the upper left.
   - The shader uses the same numbers in UV space, where y points up (`CursivisOrb.shader:102-104`). The light spot is therefore likely at the lower left.
   - The rim "top-left sheen" formula (`:121`) brightens the lower half and has no x term.
   - Visual check pending.
2. **Chip positions.** The `OrbHud.cs` header comment says the chips are at "±0.907 D, ±0.339 D". The code places them at ±138 / +34 / −30 mm with D = 78 mm (§7.1).
3. **Orb size.** D = 78 mm, smaller than the 96.4 mm that `ui-audit.md` §12.3 maps the original to.
4. **Shine.** It sweeps left → right, while the original swept right → left. Listening uses the Processing colors.
5. **Uncertainty chip on theme change.** `ResultCard.ApplyTheme` (`:194-215`) does not re-color it, so it keeps the old theme's colors until the next result. "none" hides the chip, while "low" shows "Grounded in your selection".
6. **Unused tokens and code:** `OrbStatus`, `Hairline`, `PadCard`, `TextH2`, `TextH3`, `BrandNavy` and `BrandMagenta` (C#; the shader has its own magenta default), `Ease.OutBack`, `ButtonStyle.Danger`.
7. **Low contrast and tight spacing** as listed in §12.1 and §12.3.
