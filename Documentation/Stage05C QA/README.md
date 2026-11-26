# Stage 05C Play Mode captures

**WIP.** The presentation pass is ready for iteration. Browser smoke testing, mix review in dense combat and a Play Mode review of the updated door pictograms are pending.

Open Assets/Scenes/Security Route Stage 05B.unity to play. The captures use Unity 6000.3.13f1 and the Direct3D 12 renderer at 1440 by 810.

| Check | Captures |
|---|---|
| First-person posture | [Directive](Directive.png), [Directive ADS](Directive%20ADS.png), [Compliance](Compliance%20Hip.png), [Compliance ADS](Compliance%20ADS.png), [Termination](Termination%20Hip.png), [Termination ADS](Termination%20ADS.png), [Deadline](Deadline%20Hip.png), [Deadline optic](Deadline%20ADS.png) |
| Threat silhouettes | [Security Unit](Guard.png), [Surveillance Drone](Drone.png), [Laser Auditor](Auditor.png), [Compliance Enforcer](Enforcer.png), [The Supervisor](Supervisor.png) |
| Feedback | [Termination discharge](Termination%20Discharge.png), [Confirmed damage](Confirmed%20Damage.png), [Security Unit destruction](Guard%20Destruction.png), [Bullet Time](Bullet%20Time.png) |
| Rewards | [Weapon comparison](Physical%20Loot.png), [Protocol selection](Protocol%20Selection.png) |

The F8 QA panel in the editor and development builds exposes weapon setup, rarity, compatible modifiers, room jumps, Supervisor attacks and field cue playback. Maximum Isolation in Containment Chamber is the dense combat scenario.

The Play Mode run passed 150 of 150 cases covering Stage 05C, Stage 05B, Stage 05A.2, the two combat and temporal playgrounds, weapon loadout and the capture harness. Edit Mode passed 8 of 8 route generation cases. The main editor reported zero current Console errors and warnings during validation.

A WebGL build of the playable scene completed with zero build errors in the validation copy. The Play Mode and build runs used the local Unity configuration.
