# Demo video script (under 3 minutes)

Rules that shape this script (see `docs/RESEARCH.md` §7):
- Under 3:00, public on YouTube or Vimeo, English.
- "Make your hands-first design clear in the first 30 seconds."
- Show real footage that honestly represents the app. Simulator footage is allowed, so every simulator shot carries the
  on-screen label **"Meta XR Simulator footage · live AI responses"**.
- No identifiable people and no brand logos in frame (the simulator living room and the workspace documents have none).

Target length 2:45. Times are approximate.

| Time | Shot | What happens on screen | Caption |
|---|---|---|---|
| 0:00-0:08 | Room view, orb idle at lower right, workspace documents in front | Title fades in | **Cursivis Spatial**. Selection is Context. Gesture is Intent. Cursivis is Action. |
| 0:08-0:30 | Simulated right hand ray on the research abstract | Pinch: the paragraph highlights and the chip "Whole paragraph selected · Ask Cursivis" pops up. Pinch Ask Cursivis: the orb glides beside the paragraph and four chips fly out of it while they shimmer, then fill with "Key Findings", "Simplify Text", "Explain Terms", "Research Questions"; the Talk chip glows | Hands only: pinch to select, pinch to ask. Four AI actions + Talk. |
| 0:30-0:50 | Same | Pinch Key Findings: the card slides in with a loading shimmer, then the answer reveals word by word with headings and bullets | One pinch to act. About 2 s with the free-tier Gemini backend. |
| 0:50-1:05 | Card | Pinch Read aloud: the orb switches to speaking and the answer plays from the orb's position | Read aloud from the orb. |
| 1:05-1:20 | Card | Follow-up: "Explain the main finding like I'm 12" → new answer in the same card | Follow-ups remember the conversation. |
| 1:20-1:45 | Photo document | Pinch the photo → suggestions ("Describe Photo", "Write Caption", ...) → Write Caption → Redo gives a different caption | Works on images too. |
| 1:45-2:20 | Room | Tap Snip: the camera frame overlay appears. Pinch-drag a box around the curtains and lamp. The crop freezes as a preview, suggestions arrive ("Describe Scene", ...), then Talk: "What is this? Describe what you can see." | Snip the real world through the Passthrough Camera API. |
| 2:20-2:35 | Orb | With nothing selected, "What can you do?" → general answer; theme cross-fades from dark to light and back | General conversation and theme. |
| 2:35-2:50 | End card over the room | Text only | Quest 3/3S · Unity 6 + Meta XR SDK 209 (ISDK hands, MRUK Passthrough Camera API) · Gemini via a keyless-client backend · github.com/UnknownGod2011/Cursivis-spatial |

## How the footage was recorded (simulator)

The repository's tools reproduce the recording on a Windows PC without a headset:

1. Build the simulator player (Development build so the test autopilot is available):
   ```powershell
   $env:CURSIVIS_BACKEND_URL = "https://cursivis-spatial.vercel.app"; $env:CURSIVIS_DEVELOPMENT = "1"
   powershell -File tools/unity-batch.ps1 -Method Cursivis.Spatial.EditorTools.CursivisBuild.BuildWindows -Target StandaloneWindows64 -Name build-win
   ```
2. Start the app in the simulator, choose the **Living Room** environment, switch the inputs to hands:
   ```powershell
   powershell -File tools/run-sim.ps1 -LogName demo -Autopilot all -PlayerArgs "--cursivis-autopilot-delay=60,--cursivis-autopilot-pace=1.8"
   metavr xrsim input source --hand right --source hand
   ```
3. Record the simulator viewport with ffmpeg (the simulator build has no native MP4 recorder):
   `powershell -File tools/record-sim.ps1 -Out test-results/local/video/demo-raw.mp4 -Seconds 200`
4. Add the captions and the simulator label: `powershell -File tools/label-video.ps1`.

What is genuine and what is scripted, as stated in the video description:
- **Genuine:** every AI response (live Gemini through the production backend), the Passthrough Camera API frames
  (simulated camera of the synthetic room), the UI, animations and timings.
- **Simulated hands:** the hand pinches shown on the workspace come from the simulator's hand input (pinch key, ray
  aimed by turning the simulated head).
- **Scripted:** some later steps are triggered by the built-in test autopilot (`--cursivis-autopilot`) that calls the
  same app methods as the buttons, because aiming a simulated hand at every chip is slow. Those segments are captioned
  "steps triggered by the test autopilot".

## If you can borrow a Quest 3/3S

Record the same shot list on the device with the headset's own recorder (Quest menu → Camera → Record video). Real
hands and the real room make the strongest video. Then replace the simulator label with nothing; do not reuse the
simulator footage for device claims.
