# Release checklist (what you need to do)

Deadline: **18 Nov 2026, 12:00 PM PT = 19 Nov 2026, 01:30 IST**. Nothing may change after it, including the APK in the
channel. Keep the backend running unchanged until winners are announced (about 11 Dec).

Steps marked **(you)** need your accounts and cannot be automated from this repository.

## 1. Accounts (you)
- [ ] Meta account with Developer access, and membership in the **Meta VR Start** program approved (Rules §2(d)).
- [ ] Devpost account registered for the competition.

## 2. Meta Horizon Developer Dashboard (you)
1. [ ] https://developers.meta.com/horizon/manage/ → **Create New App** → name "Cursivis Spatial", platform **Meta Quest**.
2. [ ] Note the **App ID**. Under **Development → API** copy the **App Secret** (never commit it).
3. [ ] **Distribution → Release Channels → Create channel** named exactly **`Competition`**.
4. [ ] **App Submission → App Metadata**: name, short description (use the first paragraph of
       `submission/DEVPOST_DESCRIPTION.md`), and a header image (`submission/media/` has simulator frames; label them as
       simulator footage). The invite page shows these.

## 3. Upload the APK (you, about 5 minutes)
The release APK is `builds/android/CursivisSpatial.apk` (also attached to the GitHub release). It is signed with the
release keystore described in section 8, package `com.cursivis.spatial`, version 1.0.1 (code 2).

```powershell
# Meta's upload tool (Windows): https://developers.meta.com/horizon/resources/publish-reference-platform-command-line-utility/
ovr-platform-util.exe upload-quest-build --app-id <APP_ID> --app-secret <APP_SECRET> `
  --apk builds\android\CursivisSpatial.apk --channel Competition --age-group TEENS_AND_ADULTS
```
- `--age-group` is required for new apps. `TEENS_AND_ADULTS` matches the app (no mature content, safety-filtered AI);
  choose `ADULTS_ONLY` if you prefer the conservative option.
- If the upload says the version code already exists, rebuild with a higher code:
  `powershell -File tools/build-apk.ps1 -VersionCode 3`.

## 4. Invite URL (you)
1. [ ] Release channel **Competition** → **Users** → turn on **Grant access to users by URL**.
2. [ ] Turn on **Automatic approval** ("Users with this URL will be automatically added to the release channel up to the max limit").
3. [ ] Copy the invite URL exactly as the Dashboard shows it. Open it in a private window to confirm it works.
4. [ ] Optional but strongly recommended: ask someone with a Quest 3/3S to join through the URL, install, and run the
       10-minute device check in `docs/TEST_PLAN.md` §4. Everything on device is still UNVERIFIED.

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
