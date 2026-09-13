# Android Smoke Test

## Frame pacing policy

The game targets a fixed 60 FPS. `GameBootstrapper.Awake` runs once before scene flow and sets `QualitySettings.vSyncCount = 0` and `Application.targetFrameRate = 60`, so 90/120 Hz devices must not run above 60 FPS. Every smoke-test build must be checked against this policy.

1. Install `Builds/Android/BorderValley.apk` on an Android 8.0 or newer device.
2. Launch the game and confirm it enters landscape mode.
3. Confirm Boot transitions to Main Menu within 5 seconds.
4. Tap New Game and confirm WorldPlaceholder logs the foundation message.
5. Background the app for 30 seconds and resume it without a crash.
6. Run for 10 minutes and record average FPS, peak memory, and visible corruption. Average FPS must stay at or near 60 and must not exceed 60.
7. On a 120 Hz device, repeat steps 2-6 and confirm the frame rate stays at the 60 FPS cap instead of reaching 90/120 FPS.
