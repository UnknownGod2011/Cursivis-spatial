# Cursivis Spatial

**Selection is Context. Gesture is Intent. Cursivis is Action.**

## Inspiration
On the desktop, Cursivis let you select anything and press one button to get the right AI action. In a headset the
selection is the world around you: a nutrition label, a page of notes, a sign in another language. We wanted the same
one-gesture loop on Quest, hands only.

## What it does
- **Select.** Tap Snip and pinch-drag a box around anything in your room; Cursivis captures just that region through the
  Passthrough Camera API. Or pinch text and images on floating workspace documents.
- **Ask.** Pinch *Ask Cursivis*. The orb reads the selection and offers four specific actions, such as *Check Allergens*,
  *Key Findings* or *Describe Room*, plus **Talk**, which is always there and never AI-generated.
- **Act.** The answer appears on a spatial card beside the selection: formatted text that reveals word by word,
  evidence and uncertainty notes, follow-up chips, read-aloud from the orb, regenerate, pin and grab.
- **Talk.** Hold Talk, or tap and speak, to ask anything about the selection. With nothing selected, pinch the orb for a
  general conversation. Voice also drives the app: "read that out loud", "show me the options", "start over".

## How we built it
Unity 6 with OpenXR and Meta XR Core, Interaction SDK and MR Utility Kit 209; scene, rig and settings are generated
from code. `PassthroughCameraAccess` runs only while you snip: the selection's world corners are projected into the
camera frame with its own pose, cropped on the GPU and sent as a JPEG of at most 1024 px. A Node 22 backend on Vercel
calls Gemini: one structured call returns the content type and four suggestions; actions, Talk (audio in, transcript
and answer out) and text-to-speech are single calls. A rotating key pool, model fallback and hedged requests keep
answers at about 2 seconds. No API key ships in the APK.

## How hands work
Everything is hands-first; controllers are optional. Far targets use Interaction SDK hand rays with pinch to select,
near targets use poke. Pinch-tap selects a whole block, pinch-drag selects a word range or frames a region. Talk is
push-to-talk on a held pinch, or tap-to-talk that stops when you pause. The result card has a grab handle. If tracking
is lost mid-drag, the drag is cancelled and Cursivis tells you to raise your hand.

## Challenges
We have no headset. Everything was built and tested in the Meta XR Simulator on an integrated laptop GPU, which meant
Vulkan instead of D3D11, a JPEG passthrough path and a seated rig with locomotion removed. Free-tier latency was the
other fight: measuring every model and reordering the chain cut action time from 7.7 s to about 2 s.

## What's next
On-device tuning on Quest 3 and 3S, live streaming voice, anchoring cards to real objects across sessions, and
multi-selection comparisons.

*Honesty note: the video is Meta XR Simulator footage with live AI responses; on-device behaviour has not been tested.*
