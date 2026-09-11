# Portal to the Tomorrow That Never Was

**A virtual portal you can walk into from the real world.**

Wearing a Meta Quest 3, the viewer sees a virtual portal standing in the physical space around them (MR). Stepping through it, they leave reality behind and enter a future that was promised but never arrived, built on the Frutiger Aero aesthetic. Meanwhile, a nearby display (a vintage CRT monitor in this exhibition) shows the viewer's body tracking data in real time.

- Documentation video: https://youtu.be/v39DtJl0XCw
- Author: Yiho Li (MFA Computational Arts, Goldsmiths, University of London)
- Background and implementation details: see "Technical Notes" and "Concept" below

---

## System Architecture

The piece runs on two devices with two Unity scenes, communicating over OSC on the same local network.

```
┌───────────────────────────┐         OSC          ┌──────────────────────────┐
│  Meta Quest 3 (standalone)│ ─── body data ───▶   │  PC → CRT monitor        │
│  Portal_FrutigerAero      │                      │  PC_Portal               │
│  MR portal + chimeras     │                      │  live body visualisation │
└───────────────────────────┘                      └──────────────────────────┘
            │                                                  │
        OSC Transmitter                                    OSC Receiver
        Remote Host: PC IP                                 Local Host: PC IP
        Local Host:  Quest 3 IP
```

| Scene | Target platform | Contents |
| --- | --- | --- |
| `Portal_FrutigerAero` | Meta Quest 3 (Android, built as APK) | The MR portal experience, Frutiger Aero virtual scene, algorithm chimeras, OSC Transmitter |
| `PC_Portal` | Windows PC (built as EXE, output to the CRT monitor) | Receives body tracking data from the Quest 3 and visualises it in real time, OSC Receiver |

---

## Requirements

| Item | Version / notes |
| --- | --- |
| Unity | **6000.0.69f1** (Unity 6 LTS). Open with this exact version; upgrading across versions can break the URP and XR settings. |
| Meta XR SDK | See `Packages/manifest.json`. Building Blocks used: Camera Rig, Effect Mesh, Passthrough Window, Character Retargeter, Eye Gaze |
| Mixed Reality Utility Kit (MRUK) | Installed with the Meta XR SDK; used to position where the portal appears |
| extOSC | **Not included in this repo** — import it yourself from the Unity Asset Store (see step 1) |
| Hardware | Meta Quest 3 (an external battery pack is recommended to extend running time), a Windows PC, a display (a CRT monitor was used in this exhibition), a wireless router |

The project contains a large amount of assets (~25 GB) — check your disk space before cloning.

---

## Setup and Usage

### 1. Open the project and add missing dependencies

1. Open the project with Unity **6000.0.69f1**.
2. Import [extOSC: Open Sound Control](https://assetstore.unity.com/packages/tools/input-management/extosc-open-sound-control-72005) (free) from the Unity Asset Store. Licensing prevents it from being redistributed here, so both scenes will show compile errors until it is imported.
3. Confirm the Android Player Settings meet Quest build requirements (cross-check against the project's actual values):
   - Scripting Backend: IL2CPP
   - Target Architectures: ARM64
   - Texture Compression: ASTC
   - XR Plug-in Management → Android: Oculus enabled
   - Minimum API Level: as required by Horizon OS on Quest 3

### 2. Prepare the Quest 3 (required)

**Skip this and the portal will not appear in the right place.**

1. Set up the **user boundary (Guardian)** in the headset.
2. Run **Space Setup** to scan the actual room the piece will run in. The project's MRUK relies on this room data to position where the portal appears.
3. Re-scan every time the venue changes.
4. On first launch, grant the Passthrough and Spatial Data permissions.

### 3. Configure IPs and OSC

1. Connect both the **PC** and the **Quest 3** to the same router.
2. Note the local IP address of each device:
   - Quest 3: `Settings → Wi-Fi → current network → Advanced`
   - Windows: run `ipconfig` in Command Prompt and read the IPv4 address
3. Enter the addresses in Unity:

   | Scene | Object | Field | Value |
   | --- | --- | --- | --- |
   | `Portal_FrutigerAero` | OSC Transmitter | Remote Host | PC IP |
   | | | Local Host | Quest 3 IP |
   | `PC_Portal` | OSC Receiver | Local Host | PC IP |

   The port must match on both ends (use the value already set in the scenes).
4. **Turn off the PC's firewall** (or open the corresponding UDP port for `PC_Portal.exe`). Windows Firewall blocks OSC packets by default, and the CRT side will receive nothing at all.

### 4. Build and install the Quest 3 side (APK)

1. Switch the build target to **Android**, open `Portal_FrutigerAero`, and confirm the scene is in `File → Build Settings`.
2. Build the `.apk`.
3. Install it on the Quest 3 by either route:
   - **SideQuest**: install SideQuest desktop, connect the Quest 3 over USB with developer mode enabled, and drag the APK into the SideQuest window.
   - **adb**: `adb install -r Portal_FrutigerAero.apk`
4. On the headset, go to `App Library → filter menu (top right) → Unknown Sources` to find the app.

### 5. Build and run the PC side (EXE)

1. Switch the build target to **Windows**, open `PC_Portal`, and build the `.exe`.
2. Run the EXE directly on the machine connected to the CRT/display.

### 6. Run the experience

Start the PC application first, then launch the app on the Quest 3. Once the headset is on, the virtual MR portal appears in the physical space; walking through it enters the virtual world, and the viewer's body movement appears on the CRT in sync.

The experience is designed to need **no controller and no recalibration**: the portal stays in the correct position even after the headset is put down and woken again, or when the viewer faces a different direction — so visitors can be handed over quickly in a busy exhibition.

---

## Troubleshooting

| Symptom | Likely cause and fix |
| --- | --- |
| Portal does not appear, or appears in the wrong place | Space Setup was not completed on the Quest 3, or the venue changed without a re-scan. Redo the room scan. |
| Nothing shows on the CRT side | 1) The PC firewall is on and UDP packets are blocked; 2) IPs or ports do not match; 3) the two devices are not on the same network. |
| OSC stops working after a network change | Changing IPs requires rebuilding the APK (see Known Limitations). |
| Many compile errors on opening the project | extOSC has not been imported. |
| APK will not install, or the screen is black on launch | Check Player Settings: IL2CPP / ARM64 / Oculus XR Plug-in. |
| The app is not visible in the headset | Switch the App Library filter to Unknown Sources. |

---

## Known Limitations

- **OSC is tied to fixed IPs**: changing an IP address means rebuilding the APK. Allow time for this when the network environment changes on site.
- **The firewall has to be off**: there is currently no finer-grained permission setup, so the exhibition PC needs its firewall disabled.
- **Depth API and Scene API cannot be used together**: an earlier version had an algorithmic worm read real room data and crawl across object surfaces (Scene API), but combining this with the Depth API caused severe visual overlap and technical conflicts. That direction was dropped; only the chimera design was kept.
- **Transparent materials**: they cause visual artefacts and crashes inside the portal world, so everything is now opaque.

---

## Technical Notes

### A portal you can actually walk into

Stencil-based portals in game engines are usually just visual illusions: you can look through them, but you cannot go inside. The goal here was a real, bodily transition.

The first instinct was to spawn a virtual room or space inside the real world. After several attempts, the logic was **reversed** instead: the whole scene is built as a virtual environment, with an MR cube placed inside it and its rendering normals flipped outward. That cube becomes the actual boundary the viewer moves around in — standing inside it, they see the real world through passthrough; the moment they physically step across its boundary, they seamlessly enter the surrounding virtual scene.

### LBE XR (Location-Based Experience) spatial design

- The exhibition space was small (roughly 3×3 m²), so the viewer's path was set **diagonally** to maximise usable distance while keeping the CRT monitor within their line of sight as part of the experience.
- The real walls, tables and other elements were measured on site and rebuilt at **1:1 scale** in the virtual environment, so the virtual and physical spaces line up more naturally.
- Several friends tested the piece while their movement and gaze were observed; the layout was adjusted from that feedback.

---

## Concept

Every technological medium carries its own imagination of the future. Placing a Quest 3 and a vintage CRT monitor side by side sets up a dialogue across time, suggesting that today's technologies may eventually become the nostalgic artifacts of another imagined future.

The visual reference is **Frutiger Aero**, a 2000s–2010s aesthetic built on a techno-utopian vision of harmony between nature and technology. Looking back from 2026, that future never happened; it has instead become a nostalgic style consumed by today's internet subcultures. The CRT reinforces this, recalling a time when the screen itself functioned as a portal into a newly imagined digital world.

For a fuller discussion of the "portal" concept, see the article *What Passes Through: The Portal from Sight to Body*, published in the Computational Arts publication **f0rm1ess**.

---

## References and Credits

- Nancy Baker Cahill, *CENTO* — https://whitney.org/exhibitions/cento
- Karl Sims — https://www.youtube.com/watch?v=Yi6k067plAs
- Frutiger Aero Archive — https://frutigeraeroarchive.org/
- OSC template: Becky's VR class, Goldsmiths — https://learn.gold.ac.uk/mod/page/view.php?id=1771048
- VRChat Wiki, *Portals* — https://wiki.vrchat.com/wiki/Portals
- Meta Horizon OS Developers: [Depth API](https://developers.meta.com/horizon/documentation/unreal/unreal-depthapi-overview/), [Scene API](https://developers.meta.com/horizon/documentation/unreal/unreal-build-using-scene/), [MRUK](https://developers.meta.com/horizon/documentation/unity/unity-mr-utility-kit-overview/)
