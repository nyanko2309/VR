VR Mirror Therapy

A VR-based physical rehabilitation system for Meta Quest that applies mirror therapy techniques to a real-time, motion-tracked exercise game. Grant-funded project built for clinical use.

What it does

The patient's stronger limb — leg or hand — is tracked in real time, and its movement is mirrored onto the affected limb's avatar — the classic mirror-box therapy concept, adapted to VR. Exercises are delivered through a rhythm-based, procedurally generated session (calibration → main set → cool-down) that adapts difficulty to the player's range of motion, and logs progress across sessions so patients can see recovery over time instead of a single score.

Features


Real-time mirror tracking — user selects their stronger limb; its motion drives the mirrored avatar limb on the affected side
Leg and hand tracking — supports mirror therapy exercises for both lower-limb (ankle/leg) and hand movement, using Quest hand tracking alongside body/controller tracking
Procedural session structure — warm-up calibration, 4–6 adaptive exercise phrases, and a cool-down, so difficulty ramps up and back down within a session
Range-of-motion (ROM) progress tracking — session results and long-term progress saved to Firebase, visualized as a growing arc rather than a raw score
Passthrough / mixed-reality support — optional passthrough background via OVRPassthroughLayer for a less isolating in-clinic experience
Cosmetic-only progression — unlockable environments/effects tied to consistency, without gamifying therapeutic compliance
Custom native tracking layer — sensor/tracking components written in C++/C for low-latency motion capture, integrated into Unity via a native plugin


Tech stack


Engine: Unity (C#)
Native layer: C++ / C (custom tracking/sensor integration)
Backend: Firebase (Firestore + session data)
Platform: Meta Quest (OpenXR / OVR)
Input: Unity XR Input System, tracked controllers/body trackers, Quest hand tracking


How it works


A calibration phase reads the patient's available range and timing at low intensity.
The procedural choreographer generates a session of short exercise phrases (currently scoped to 3 reps per phase for the demo build).
The PhysioBallGenerator spawns movement targets that drive the mirrored exercise; the stronger limb's tracked motion (leg or hand) is mirrored onto the affected limb's avatar in real time.
Session data (reps completed, ROM, consistency) is written to Firebase at the end of each session.
Return visits unlock cosmetic variations and show cumulative recovery progress.


Getting started


Clone the repo and open it in Unity (matching the project's Unity version).
Add your own google-services.json to Assets/ (get this from your Firebase console — package name must match Edit → Project Settings → Player → Android → Package Name).
Ensure XR Plug-in Management is configured for Android/OpenXR under Project Settings.
Build and deploy to a Meta Quest device, or run in the Unity Editor with an XR simulator.


Status

Demo / prototype build — session phases are intentionally short (3 reps) for demonstration purposes. Grant-funded for clinical physical therapy use.

