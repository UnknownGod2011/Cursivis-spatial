# Demo video script (under 3 minutes)

Final video: `submission/media/cursivis-spatial-demo.mp4`, **1:59**, 1920×1080, silent, every frame labelled
"Meta XR Simulator footage (Quest 3 profile) · Real app build, live AI responses". Also attached to GitHub release v1.0.1.

Rules that shape it (see `docs/RESEARCH.md` §7): under 3:00, public on YouTube or Vimeo, English, hands-first visible in
the first 30 seconds, honest footage, no identifiable people, no brand logos (the energy drink label is original
artwork without a brand).

## The cut (from `submission/media/demo-segments.tsv`)

| Video time | Raw time | Shows | Caption |
|---|---|---|---|
| 0:00-0:03 | title card | Name and tagline | Cursivis Spatial. Selection is Context. Gesture is Intent. Cursivis is Action. |
| 0:03-0:11 | 29.0-36.5 s | Simulated right hand points at the code card and pinches; the card is selected, Ask Cursivis chip appears | Pinch to select |
| 0:11-0:19 | 50.5-58.3 s | Ask Cursivis: orb moves next to the code, "Understanding…" (about 7 s of real latency, kept) | Ask Cursivis |
| 0:19-0:21 | hold at 58.3 s | Four chips + Talk (frame held 2.5 s, captioned as held) | Four actions + Talk |
| 0:21-0:32 | 58.3-69.5 s | Find Bugs → off-by-one error in the loop | Find Bugs |
| 0:32-0:38 | 71.3-77.0 s | Spanish building notice selected → Translate Notice first | A notice in Spanish |
| 0:38-0:50 | 77.0-89.0 s | Translation (water cut on Tuesday 3 December, 9:00-14:00), scroll | Translate Notice |
| 0:50-0:58 | 90.5-98.0 s | Back of an energy drink can, region over the ingredients → Check Allergens, List Ingredients, Explain Additives, Health Warnings | Energy drink label |
| 0:58-1:08 | 98.0-108.0 s | Talk "Is this vegan?" → "Not vegan: contains E120 (carmine) … vitamin D3 often from lanolin" | Talk: "Is this vegan?" |
| 1:08-1:26 | 108.0-126.0 s | Read aloud (orb speaking), follow-up "How much caffeine is in one can?" → 80 mg per can, ~400 mg/day guidance | Read aloud + follow-up |
| 1:26-1:38 | 127.7-139.5 s | Room Snip through the Passthrough Camera API → "a window with dark curtains next to a floor lamp" | Snip the room |
| 1:38-1:52 | 140.2-155.0 s | Research abstract → Key Findings, cross-fade to the light theme | Key Findings + theme |
| 1:52-1:59 | end card | Tech line, repo, honesty note | |

Cuts only remove idle time (before the first action and between scenarios). Waits between an action and its AI
answer are kept.

## What is genuine and what is scripted (also stated in the video and its description)
- **Genuine:** the app build, every AI answer (live Gemini through the production backend), the passthrough camera
  frames of the simulator room, the UI, animations and latency.
- **Simulated hand:** the first selection is a real hand-ray pinch in the simulator (point-and-click mode).
- **Scripted:** later steps are triggered by the built-in test autopilot (`--cursivis-autopilot`), which calls the same
  functions as the buttons; questions are typed by it because the simulator has no microphone. The room snip's drag
  is simulated on the snip surface.
- **Not captured:** audio (the read-aloud is visible as the orb's speaking state only).

## How to reproduce it
```powershell
# 1. Release simulator build (about 2 minutes)
$env:CURSIVIS_BACKEND_URL = "https://cursivis-spatial.vercel.app"
powershell -File tools/unity-batch.ps1 -Method Cursivis.Spatial.EditorTools.CursivisBuild.BuildWindows -Target StandaloneWindows64 -Name build-win
# 2. Record: starts the simulator and the app, pins the window on top, records with ffmpeg, pinches the code card,
#    then lets the paced autopilot run code, notice, label, snip and research (keep the PC idle for ~5 minutes)
powershell -File tools/demo-run.ps1
# 3. Cut and caption: read the event times from the log (the autopilot prints at=HH:mm:ss.fff for every step),
#    edit submission/media/demo-segments.tsv, then
powershell -File tools/label-video.ps1 -Raw test-results/local/video/demo-final.mkv -Segments submission/media/demo-segments.tsv
```

## With a Quest 3/3S
Use the same order with real hands, a real printed page and a real (unbranded) package; record with the headset's
own recorder. See `submission/DEMO_SCRIPT.md`, section B.
