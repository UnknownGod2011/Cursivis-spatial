# Preflight audit (2026-11-13, IST)

| Item | Finding | Action taken |
|---|---|---|
| OS / hardware | Windows 11 (build 26200), HP Pavilion x360 14, Intel i5-10210U (4C/8T), **Intel UHD 620 iGPU**, **7.8 GB RAM**, ~13 GB free disk | Heavy jobs run one at a time; Unity Hub GUI closed during builds |
| Git / GitHub | git 2.52, gh 2.93 logged in as `UnknownGod2011` (repo, workflow scopes). Source repo `MX--Cursivis` public; destination `Cursivis-spatial` empty, admin access | Source cloned read-only to `C:\Users\Admin\cursivis-ref\MX--cursivis` (never modified). Destination pushed on `main` |
| Unity | Unity Hub + **Unity 6000.4.6f1** (no Android module) | Installed Android Build Support + OpenJDK 17 + SDK/NDK r27c via Hub CLI (needed the Hub GUI closed) |
| Android SDK | Separate SDK in `%LOCALAPPDATA%\Android\Sdk` (platforms 28–36, NDK 25/28) | Using Unity's bundled SDK/NDK r27c instead (version Unity expects) |
| Java | System Java 1.8 (too old) | Unity's bundled OpenJDK 17 is used |
| Meta SDKs | None installed | `com.meta.xr.sdk.core`, `com.meta.xr.sdk.interaction(.ovr)`, `com.meta.xr.mrutilitykit` **209.0.0** + `com.unity.xr.openxr` 1.19.0 via `Packages/manifest.json` |
| Meta XR Simulator | Not installed | Official 207.1 MSI (Meta-signed). Per-machine MSI needs admin, so it was extracted unprivileged (`msiexec /a`) to `C:\Users\Admin\MetaXRSimulator`, registered with an HKCU `xrsim://` handler, plus the bundled Windows App Runtime 1.5 DDLM MSIX (per-user). Runs; OpenXR session reaches FOCUSED |
| Meta VR CLI / MCP | Not installed | Not needed so far: Core SDK 209 ships the Meta XR Operator OpenXR layer + MCP server (port 8720) used directly via `tools/xrop.mjs` |
| Kiro MCP | No MCP servers configured | Operator is driven over HTTP MCP by a small script instead of a registered MCP server |
| Quest hardware | **No headset** (adb lists no devices) | All on-device items are marked UNVERIFIED; simulator used for development and footage |
| Camera preview | No headset; simulator 207.1 + MRUK 209 advertise `XR_METAX1_passthrough_camera_data` | Passthrough Camera API path tested in the simulator (see TEST_PLAN results) |
| AI credentials | No keys in the environment. User supplied 10 Gemini keys | Stored only in gitignored `backend/.env` and Vercel encrypted env vars. 6 of 10 work; 4 return 403 "free tier not available" (pool disables them) |
| Deployment | Vercel CLI 50 logged in (`unknowngod2011`); Docker present | Backend deployed to Vercel Hobby (free): `https://cursivis-spatial.vercel.app` |
| Paid services | None used | — |

## Human-only items

| Item | Why it can't be automated |
|---|---|
| Testing the APK on a Quest 3/3S | No headset available |
| Meta Horizon Developer Dashboard: create app, release channel `Competition`, upload, enable URL invite | Requires the developer's Meta account login |
| Devpost submission + public video upload | Requires the developer's accounts |
