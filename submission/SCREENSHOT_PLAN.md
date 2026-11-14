# Screenshot and image plan

All images below are frames from the **Meta XR Simulator** (Quest 3 profile, synthetic Living Room, left-eye view)
with live AI responses. Each one is labelled "Meta XR Simulator" in its caption on Devpost. None is a mock-up and none
was edited beyond cropping. Replace them with headset captures if a Quest 3/3S becomes available.

| # | File (`submission/media/`) | Shows | Caption for Devpost |
|---|---|---|---|
| 1 | `01-hero-suggestions.png` | The orb beside the selected research paragraph with four AI chips and Talk around it | Pinch to select, pinch to ask: four AI actions plus Talk. (Meta XR Simulator) |
| 2 | `02-result-card.png` | Result card with the "Key Findings" answer, evidence and follow-up chips | One pinch to act. The answer card stays next to what you selected. (Meta XR Simulator) |
| 3 | `03-room-snip.png` | Snip overlay over the passthrough room with a framed region | Snip anything in your room through the Passthrough Camera API. (Meta XR Simulator) |
| 4 | `04-snip-answer.png` | Answer about the snipped region (curtains, lamp) | Ask about the real world. (Meta XR Simulator) |
| 5 | `05-hand-pinch.png` | Simulated hand ray selecting a workspace document | Hands-first: hand ray and pinch, no controllers. (Meta XR Simulator) |
| 6 | `06-photo-caption.png` | Photo document with the generated caption | Works on images too. (Meta XR Simulator) |
| 7 | `07-light-theme.png` | The same UI in the light theme | Light and dark themes with a soft cross-fade. (Meta XR Simulator) |

Rules to respect (Rules §4.17, §4.19): no identifiable people, no brand names or logos in frame. The simulator room and
the workspace documents contain neither. Use the Devpost image gallery for 1-4 at minimum; the header image on the
Meta Dashboard can reuse #1.

How to capture: run the simulator as in `docs/DEMO_SCRIPT.md`, then
`powershell -File tools/screenshot.ps1 -Out submission/media/<file>.png -Crop "838,142,779,815" -MaxWidth 1600`
(the crop is the left-eye viewport of the maximized simulator window on a 1920×1080 screen).
