# Cursivis Spatial — Interaction Specification

Hands-first, seated. Every core operation works with hands only (no controllers). Controllers and the Meta XR Simulator's
mouse/keyboard input drive the same Interaction SDK interactors, so they work as fallbacks, not as requirements.

Implementation: `unity/CursivisSpatial/Assets/Cursivis/Scripts/App/CursivisApp.cs` (orchestration),
`Core/FlowStateMachine.cs` (explicit transition table), `Selection/SelectionInput.cs` (selection gestures),
`UI/OrbHud.cs`, `UI/ResultCard.cs`, `Voice/Voice.cs`.

## 1. User journey

1. **Idle.** The orb floats low-right in the user's view (0.58 m away, 0.20 m right, 0.19 m below the eyes) with a dock:
   **Snip · Workspace · Theme · Help**. It lazily re-centres when the user turns more than 30° or moves 35 cm.
   The Cursivis workspace (four documents: research abstract, Spanish notice, photo, code) sits in a 2×2 arc 1 m away.
2. **Select.**
   - *Workspace:* pinch a paragraph or the photo (whole item), or pinch-drag across words / across part of the photo (range or region).
   - *Room:* pinch **Snip**. The camera starts, the workspace hides, a transparent selection surface appears at the depth
     of what the user is looking at (Depth API raycast, else 0.85 m), with a dashed frame showing what the passthrough
     camera can actually see. Pinch-drag a box around the object; release to capture.
3. **Selection confirmed.** The selection is highlighted (text mark / region border / frozen camera crop shown in place),
   the orb glows, and an **Ask Cursivis** chip appears under the selection with **Retake/Clear** and **×**.
   Nothing has been sent to the cloud yet.
4. **Trigger.** Pinch **Ask Cursivis**. The orb flies next to the selection and expands; four "..." placeholder chips and
   a **Talk** chip appear immediately (local feedback, no network wait); the orb shows the thinking arc and
   "Understanding…".
5. **Suggestions.** Four AI-predicted actions replace the placeholders (labels 1–3 words, icon per intent), with the
   interpreted selection title above the orb. **Talk** is always present and never AI-generated.
6. **Act or Talk.** Pinch a suggestion → result card opens in "loading" state with the action name → answer.
   Or hold **Talk** and speak ("Is this vegan?"), release to send; a quick tap starts hands-free listening that stops
   at the first pause.
7. **Result.** Read, scroll (pinch-drag the body), move (grab the handle bar), read aloud, ask a follow-up (voice or
   AI follow-up chips), **More options** (reopen suggestions), **Redo** (regenerate), **Copy**, pin/unpin, expand, theme.
8. **Dismiss.** **×** on the card returns to Idle; the orb flies home. General conversation: pinch-and-hold the orb in
   Idle (no selection) and speak.

## 2. States and transitions

`FlowStateMachine` rejects any transition not in this table (logged as `[CursivisFlow] rejected`).

| From | Allowed to |
|---|---|
| Idle | SelectionReady (Snip), SelectionConfirmed (workspace pick), VoiceListening (orb hold, general) |
| SelectionReady | Selecting (pinch down), SelectionConfirmed, Idle (cancel / camera unavailable) |
| Selecting | SelectionConfirmed (capture ok), SelectionReady (too small / capture failed / tracking lost), Idle |
| SelectionConfirmed | Understanding (Ask Cursivis), SelectionReady (Retake), SelectionConfirmed (new pick), VoiceListening, Idle |
| Understanding | SuggestionsVisible (ok or error with Talk still available), SelectionConfirmed, VoiceListening, Idle, SelectionReady |
| SuggestionsVisible | ActionSelected, VoiceListening, SelectionConfirmed, SelectionReady, Understanding (reload), Idle |
| ActionSelected | Processing, SuggestionsVisible, Idle |
| VoiceListening | Processing (sent), SuggestionsVisible / ResultVisible / SelectionConfirmed / Idle (cancelled or nothing heard) |
| Processing | ResultVisible (result or error card), SuggestionsVisible, SelectionConfirmed, Idle, SelectionReady |
| ResultVisible | FollowUp, VoiceListening, Understanding, SuggestionsVisible, Processing, Dismissed, SelectionReady, SelectionConfirmed, Idle |
| FollowUp | ResultVisible, Idle |
| Dismissed | Idle, SelectionReady, SelectionConfirmed |

Stale-response protection: every request carries the selection id; a response is applied only if
`response.selectionId == current selection id` (dropped responses are counted as `stale_response_dropped`).
Starting a new selection cancels the in-flight request (`CancellationTokenSource`).

## 3. Gesture table

| User intention | Gesture / action | Interaction SDK component | Recognition conditions | UI feedback | Cancellation | Fallback input | Tracking loss | Possible conflicts and how they are avoided |
|---|---|---|---|---|---|---|---|---|
| Arm a room snip | Pinch the **Snip** dock button | RayInteractor + PointableCanvas (dock canvas), PokeInteractable when within reach | Ray hover + index pinch select→release on the button | Button hover border, press scale 0.965, click sound; orb "Snip"; camera frame appears | **×** chip or any dock action; app pause stops the camera | Controller trigger; simulator mouse | Snip stays armed; nothing captured | Dock hidden outside Idle, so it can't fire mid-flow |
| Frame something in the room | Pinch-drag on the snip surface, release to capture | RayInteractor → PointableCanvasModule drag events on `SnipSurface` (1.6 × 1.1 m canvas, scaled with depth) | Pinch down starts the box at the ray hit; drag updates; release captures. Minimum 30 × 30 mm | Rubber-band box (brand cyan, 3 mm stroke) with corner handles; hint text | Release over a too-small box rejects it with a toast; **Retake** re-arms | Controller trigger-drag; simulator mouse drag | If hands are lost >1.2 s mid-drag the box is cleared and the state returns to SelectionReady with a toast | Drag threshold disabled on this surface only; only the pointer that started the drag can finish it (pointer id check) |
| Select workspace text | Pinch a paragraph (whole) or pinch-drag across words (range) | RayInteractor/PokeInteractor + PointableCanvas on workspace canvases; `WorkspaceText` uses TMP word geometry | Down/up on the same pointer id; word under the ray found from TMP `wordInfo` | Live highlight (TMP mark) while dragging; chip appears on release | **Clear** / **×** on the chip; selecting another item replaces it | Controller; simulator mouse | Highlight remains; no selection created until release | A new selection cancels in-flight requests |
| Select a photo / region | Pinch the photo (whole) or pinch-drag a region | Same as above; `WorkspaceImage` | Region counts if > 8 % of width and height, else whole photo | Region border; chip on release | **Clear** / **×** | Controller; mouse | As above | — |
| Trigger Cursivis | Pinch **Ask Cursivis** on the selection chip | Ray/Poke on chip canvas | Click (select + release) | Orb glow → flies to selection, expands; placeholders + Talk appear in the same frame | **×** chip before triggering; after triggering, select something else or dismiss | Controller; mouse | Nothing happens until pinch completes | Explicit chip (not auto-trigger) so nothing is sent to the cloud by accident |
| Choose a suggestion | Pinch a suggestion chip | Ray/Poke on orb canvas | Click; ignored while loading | Chip selected style, orb "Working…", card opens with loading skeleton | **×** on the card cancels and dismisses; selecting again replaces the request | Controller; mouse | — | Chips are inert while loading ("..."), so a late tap can't run a stale suggestion |
| Talk about the selection | Hold **Talk**, speak, release | Ray/Poke press/release events on the Talk chip (push-to-talk) | Press starts the mic; release after ≥ 0.45 s sends; shorter tap = hands-free mode that stops after 1.2 s of silence (max 15 s) | Orb "Listening…" with a cyan ring that follows the mic level; card's follow-up button shows "Listening…" | Release with no speech → "I didn't catch anything" toast, back to previous state | Tap mode if holding is uncomfortable; controller trigger hold | If hands are lost while holding, the recording is sent as if released | Talk only starts in states where it is allowed; other buttons don't record |
| General conversation | Pinch-and-hold the orb in Idle, speak, release | Same press/release on the orb hit target | As Talk; no selection attached | As Talk | As Talk | Tap mode; controller | As Talk | Orb hold is only a Talk gesture in Idle/Dismissed |
| Read aloud | Pinch the speaker icon in the card header | Ray/Poke | Click toggles start/stop | Icon becomes stop; orb "Speaking…"; audio comes from the orb | Pinch again; any new action stops speech | Voice: "read that out loud" (clientAction) | Unaffected | Starting Talk stops speech first |
| Scroll a long result | Pinch-drag up/down on the card body | PointableCanvasModule drag → uGUI ScrollRect (elastic, inertia) | Vertical drag | Content moves; thin scroll thumb | Release | Voice follow-up "scroll down" is answered as text (no scroll command) — UNVERIFIED as a feature; controller thumbstick drag | Scroll stops | Body drag area only; header/footer buttons are separate targets |
| Move the result card | Grab the handle bar under the card (ray-grab from afar or hand-grab up close) | Grabbable (target = ResultRoot) + RayGrab + HandGrab interactables on `GrabHandle` | Pinch/grab on the handle | Card follows the hand; kinematic while held | Release drops it in place (pinned) | Controller grip | Card stays where it was | Grab collider sits outside the canvas so it never steals UI clicks |
| Pin / follow | Pinch the pin icon | Ray/Poke | Click toggles | Toast: pinned here / follows your view | Toggle again | — | — | — |
| Expand | Pinch expand icon | Ray/Poke | Click toggles 1.0× ↔ 1.28× | Card grows (larger text and targets) | Toggle again | — | — | — |
| Theme | Pinch sun/moon (dock or card) | Ray/Poke | Click | Instant theme switch (as in the original) | Toggle again | Voice: "switch to dark mode" (clientAction) | — | — |
| Dismiss | Pinch **×** on the card | Ray/Poke | Click | Card closes (135 ms), orb flies home | — | — | — | Cancels requests, voice and speech |

## 4. Accessibility and comfort

- Seated: all content within ±30° horizontally and −30°…+10° vertically of the initial gaze; nothing behind the user.
- Targets: chips 176 × 50 mm and buttons ≥ 34 mm tall at 0.6–0.8 m (≈ 2.5–4.5°); body text 14.9 mm at 0.78 m (≈ 1.1°).
- Either hand works for every gesture (the Interaction SDK rig provides interactors for both hands).
- Voice is optional: every voice feature has a hand alternative, and every hand feature except selection drawing has
  a voice alternative via `clientAction` (read aloud, theme, more options, clear, new conversation).
- Audible click on press and a confirmation tone on capture/trigger (hand tracking has no haptics).

## 5. Permissions

| Permission | When requested | If denied |
|---|---|---|
| `horizonos.permission.HEADSET_CAMERA` (+ Scene for depth) | First time the user pinches **Snip** | Toast explains; workspace and Talk keep working; Snip asks again next time |
| `android.permission.RECORD_AUDIO` | First time the user holds **Talk** / the orb | Toast explains; suggestions still work without voice |

## 6. Error and edge handling

| Situation | Behaviour |
|---|---|
| No internet / backend down | Orb shows "Offline", toast, health retried every 12 s; failed requests show a card error with **Retry** |
| Timeout / rate limit / AI unavailable | Card error with the backend's safe message and **Retry**; suggestions error keeps Talk available |
| Camera unavailable (no PCA on device/simulator) | Toast with the reason, back to Idle |
| Empty / too small selection | Rejected locally with a toast before any request |
| Black or empty camera frame | Capture rejected ("The camera frame was empty") |
| New selection while a request runs | Old request cancelled; late responses dropped by selection id |
| App paused (headset off, system menu) | Camera, mic, speech and requests stop; in-flight work shows "Paused before the answer arrived. Tap Retry." |
| Hand tracking lost | Drag cancelled (SelectionReady); held Talk is sent; toast "Hands not visible" |
