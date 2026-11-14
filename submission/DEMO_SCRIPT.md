# Demo video: what to show and how to record it (under 3:00)

Two versions: **A** uses the recording already made in the Meta XR Simulator (`submission/media/cursivis-spatial-demo.mp4`,
labelled as simulator footage). **B** is the shot list to record on a Quest 3/3S if you can borrow one; real hands
and a real room make a much stronger video. The full production notes are in `docs/DEMO_SCRIPT.md`.

Rules: under 3 minutes, public on YouTube or Vimeo, English, hands-first visible in the first 30 seconds, no
identifiable people, no brand logos, no footage of features that don't exist.

## B. Headset recording (Quest 3/3S), 2:45

Before you start: sit at a table with a printed page of text (any non-branded document, for example a printed recipe or
a page you write yourself), a plain object such as a mug or a plant, and good light. Remove branded packaging and
anything with a logo from view. Start recording from the Quest menu (Camera → Record video).

| Time | Do this | Say (voice-over or on-screen text) |
|---|---|---|
| 0:00-0:08 | Launch Cursivis Spatial. The orb and the four workspace documents appear. | "Cursivis Spatial. Selection is context, gesture is intent, Cursivis is action." |
| 0:08-0:30 | Put controllers down. Point at the research abstract and pinch. The paragraph highlights and "Ask Cursivis" appears. Pinch it: the orb moves beside the paragraph and four suggestions fly out. | "Hands only. Pinch to select, pinch to ask. Cursivis reads the selection and suggests four next steps." |
| 0:30-0:50 | Pinch "Key Findings". The answer card slides in and the text reveals. Scroll with a pinch-drag. | "One pinch to act." |
| 0:50-1:05 | Pinch the speaker icon in the card header: the orb reads the answer aloud. | "It can read the answer to you." |
| 1:05-1:15 | Pinch a follow-up chip (for example "Explain like I'm 12"). | "Follow-ups keep the context." |
| 1:15-1:55 | Close the card. Pinch **Snip** on the dock, allow the headset camera, then pinch-drag a box around the printed page. The crop freezes; pinch Ask Cursivis; choose "Translate" or "Summarize". | "Now the real world: Snip anything in the room through the Passthrough Camera API." |
| 1:55-2:15 | Hold **Talk** and ask "What is the main point of this, in one sentence?" Release. | "Or just ask about it." |
| 2:15-2:30 | Pinch the photo document, then "Write Caption", then Redo. Toggle the theme on the dock. | "Images, captions, light and dark." |
| 2:30-2:45 | End card. | "Select anything. Cursivis understands what you might want to do next." |

## A. Simulator recording (already made)

`submission/media/cursivis-spatial-demo.mp4` was recorded with `tools/demo-run.ps1` and labelled with
`tools/label-video.ps1`. It shows the real app build in the Meta XR Simulator with live AI responses. One step at the
start is a simulated-hand pinch; the following steps are triggered by the app's built-in test autopilot, which calls the
same functions as the buttons (aiming the simulator's hand ray at small chips is unreliable). The video's caption panel
and end card say this. Upload it as is, or re-record with B and replace it.

To re-record in the simulator:
```powershell
# 1. Release Windows build for the simulator (about 2 minutes)
$env:CURSIVIS_BACKEND_URL = "https://cursivis-spatial.vercel.app"
powershell -File tools/unity-batch.ps1 -Method Cursivis.Spatial.EditorTools.CursivisBuild.BuildWindows -Target StandaloneWindows64 -Name build-win
# 2. Record (keep the PC idle; the simulator window is pinned on top while recording)
powershell -File tools/demo-run.ps1 -Scenario "code,workspace,photo,snip,general" -Delay 50 -Pace 1.4
# 3. Cut and caption (edit submission/media/demo-segments.tsv with the times from the log, then)
powershell -File tools/label-video.ps1 -Raw test-results/local/video/demo-final.mkv -Segments submission/media/demo-segments.tsv
```

## YouTube upload text

Title: **Cursivis Spatial: hands-first AI for Meta Quest (Meta XR Simulator footage)**

Description:
> Cursivis Spatial for Meta Quest 3/3S: select anything in your room or workspace, pinch Ask Cursivis, and pick one of
> four AI-suggested actions or just Talk. Recorded in the Meta XR Simulator (Quest 3 profile) with the real app build
> and live AI responses; no headset was available during development. Some steps are triggered by the app's test
> autopilot. Built for the Meta Horizon Start Developer Competition 2026.
> Code: https://github.com/UnknownGod2011/Cursivis-spatial
