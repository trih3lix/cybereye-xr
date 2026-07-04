# CyberEye — Privacy Policy

_Last updated: 2026-07-03_

CyberEye is a cyberpunk augmented-reality experience for XREAL One Pro glasses on the XREAL Beam Pro.

## What we access
- **Camera (XREAL Eye):** used **solely, on-device,** to detect the presence and screen position of common objects (people, dogs, birds, cats) so the app can draw stylized overlays. Camera frames are processed in memory by an on-device neural network (Unity Inference Engine) and are **never recorded, stored, or transmitted**.
- **Microphone permission** is declared only because the XREAL capture SDK requires it; CyberEye does **not** record or use audio input.

## What we do NOT do
- **No data leaves your device.** No network upload, no cloud, no analytics, no telemetry.
- **No personal data is collected or stored.** No images, no video, no biometrics, no accounts.
- The on-screen **"dossier" data (names, addresses, DOB, scores, facts) is entirely FICTIONAL** — randomly generated from a fixed seed per on-screen target. It is **not** real, not looked up, and not derived from the person in view beyond their approximate on-screen position.

## Permissions
| Permission | Why |
|---|---|
| `CAMERA` | On-device object detection (the AR effect). Frames never leave the device. |
| `RECORD_AUDIO` | Declared for the XREAL capture SDK; not used to record. |
| `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_MEDIA_PROJECTION` | Declared for XREAL camera/capture APIs. |

## Contact
jslade@horizon-controls.com
