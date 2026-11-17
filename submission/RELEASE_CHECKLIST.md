# Release checklist (what you need to do)

Deadline: **18 Nov 2026, 12:00 PM PT = 19 Nov 2026, 01:30 IST**. Nothing may change after it, including the APK in the
channel. Keep the backend running unchanged until winners are announced (about 11 Dec).

Steps marked **(you)** need your accounts and cannot be automated from this repository.

## 1. Accounts (you)
- [ ] Meta developer account: sign in at https://developers.meta.com/vr/manage/ with your Meta account and complete
      the free registration (it includes verifying your account).
- [ ] Developer team (organization): create one when the Dashboard asks, and accept the **Developer Distribution
      Agreement** for it. Uploads are refused until both exist.
- [ ] **Meta VR Start** membership approved before you submit (Rules §2(d)): https://developers.meta.com/vr/discover/programs/start/
      Approved members see a "Start Member" flair on the developer forum.
- [ ] Devpost account registered for the competition.

## 2. App and "Competition" channel (you, Developer Dashboard)
1. [ ] https://developers.meta.com/vr/manage/ → **My Apps** → **Create New App** → name "Cursivis Spatial", Meta Quest
       (Meta Horizon Store) app.
2. [ ] Note the **App ID**: your app → **Development → API**.
3. [ ] **Distribution → Release Channels → Create Release Channel**: Name exactly **`Competition`**, Channel Access
       **Private** → Submit. Channel names can't be changed later.
4. [ ] **App Submission → App Metadata**: short description (first paragraph of `submission/DEVPOST_DESCRIPTION.md`)
       and a header image (`submission/media/01-hero-suggestions.png`). The invite page shows them. You don't have to
       submit the app to save them.

## 3. Upload the APK (about 5 minutes)
The release APK is `builds/android/CursivisSpatial.apk` (also on GitHub release v1.0.1): `com.cursivis.spatial`,
version 1.0.1 (code 2), APK Signature Scheme v2, release keystore (section 8). Meta's upload tool is already installed
at `%APPDATA%\metavr\tools\platform-utils\ovr-platform-util.exe` (v211).

1. [ ] Get an upload token: Dashboard → **Account → Account settings → Get Oculus platform utility token → Generate
       token**. Treat it like a password.
2. [ ] In a PowerShell window in the repository folder, run (the token is asked with a hidden prompt):
       ```powershell
       powershell -ExecutionPolicy Bypass -File tools/upload-competition.ps1 -AppId <APP_ID>
       ```
       It runs `ovr-platform-util upload-quest-build --age-group TEENS_AND_ADULTS --channel Competition ...`.
       Age groups are TEENS_AND_ADULTS, MIXED_AGES or CHILDREN; TEENS_AND_ADULTS (13+) fits this app. Private-channel
       invitees must be 18+ anyway.
- If the upload says the version code already exists, rebuild with a higher code:
  `powershell -File tools/build-apk.ps1 -VersionCode 3`, then upload again.

## 4. Invite URL (you)
1. [ ] **Distribution → Release Channels** → click **Competition** → **Channel Settings** tab.
2. [ ] In **Channel Invite**, tick **Grant access to users by URL**, choose **Automatic approval**, click **Save Changes**.
3. [ ] Copy the generated link (copy button next to it) and paste it into the Devpost field. Do not submit an APK link.
4. [ ] Open the link in a private browser window to check it shows the app page. Better: ask someone else to open it.
5. [ ] Optional but strongly recommended: someone with a Quest 3/3S joins through the URL, installs, and runs the
       10-minute device check in `docs/TEST_PLAN.md` §4. Everything on device is still UNVERIFIED.
- The invite page closes 90 days after the latest upload to the channel and holds 200 members by default; both are
  fine for judging (until about 11 Dec).
## 5. Video (you)
1. [ ] Use `submission/media/cursivis-spatial-demo.mp4` (1:59, simulator footage, labelled on screen; also on GitHub release v1.0.1) or re-record with
       `docs/DEMO_SCRIPT.md`. It must be **under 3:00**.
2. [ ] Upload to YouTube (or Vimeo) as **Public**. Title suggestion: "Cursivis Spatial: hands-first AI for Meta Quest
       (Meta XR Simulator footage)". Put "Recorded in Meta XR Simulator; no headset was available" in the description.

## 6. Devpost submission (you)
Fill the form with the files in `submission/`:

| Field | Source |
|---|---|
| Name, tagline | `TAGLINE.md` |
| Track / Division | Productivity / Adapted (Significantly Updated Experience) |
| Special award | Best Agentic Interaction |
| Description (≤500 words) | `DEVPOST_DESCRIPTION.md` |
| New features since original release (Adapted) | `CHANGES_FROM_ORIGINAL.md`, "Summary for the Devpost form" |
| Hand interactions (optional question) | `DEVPOST_DESCRIPTION.md`, "How hands work" |
| Video URL | your YouTube/Vimeo link |
| Build access | the **invite URL** from step 4 (not an APK link: "APKs will not be considered") |
| Repository | https://github.com/UnknownGod2011/Cursivis-spatial |
| Images | `submission/media/*.png` (simulator frames) |

- [ ] Press **Submit** and confirm the project shows **Submitted**, not Draft.

## 7. Keep it running (until about 11 Dec)
- Backend: https://cursivis-spatial.vercel.app/v1/health should return `"ok":true` and `keysAvailable` > 0.
- Do not redeploy the backend, rotate tokens or change models after the deadline.
- Free-tier Gemini keys: 6 of the 10 supplied keys work (4 return "free tier is not available for this project"). The
  pool skips dead keys automatically.

## 8. Signing key (keep it safe)
- Keystore: `C:\Users\Admin\cursivis-ref\keystore\cursivis-release.keystore` (alias `cursivis`), outside the repo.
- Its password is stored only DPAPI-encrypted in `keystore-pass.dpapi` next to it, readable only by this Windows user.
  To see it (for a backup in your password manager):
  ```powershell
  $s = Get-Content C:\Users\Admin\cursivis-ref\keystore\keystore-pass.dpapi | ConvertTo-SecureString
  [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s))
  ```
- Every future upload of this app must use the same keystore. Back up both files.
