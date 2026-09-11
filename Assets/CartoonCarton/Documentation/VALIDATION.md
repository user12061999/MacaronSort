# Validation — 2026-09-11

Environment: PA_CandyBlast, Unity 6000.0.80f1, Built-in Renderer.

## Editor / deterministic animation

`CartonDeliveryValidation.Run()` completed **25 checks**:

- Play state and correct docking pose.
- All four cakes stay on the tray until arrival completes.
- Each cake lands inside the carton.
- Closing and departure complete; packed/completed events fire once.
- Subsequent clock steps do not fire duplicate events.
- Reset restores carton and cake transforms/scales.
- Restart during arrival.
- Equivalent positions with a single time step and 120 frame steps.
- Null/duplicate cake inputs, empty tray.
- Two alternative carton dimensions.
- No asset dependencies outside Assets/CartoonCarton.

Five Unity camera snapshots were rendered and visually inspected at arrival, dock, cake jump, closed, departure. A 60-frame animated preview is generated separately under Docs/Carton/delivery.gif in the source project.

## Play Mode

The isolated delivery demo ran in actual Play Mode using Unity's normal Update clock:

- Observed arrival motion.
- Disabled controller during arrival; sequence stopped and restored the initial pose.
- Re-enabled and replayed.
- Full sequence completed with closed lids.
- Packed and completed callbacks each fired once.

Result: `CARTON_RUNTIME_PASS`.

The temporary Play Mode startup scene was restored afterwards. The user's already-dirty original carton demo remained dirty and was not saved. The pre-existing HalloweenConcept3.unity modification was left intact.

## Limits

No clean-project import test or target build was run. No playable-ad export/runtime compatibility claim is made. Standard materials target Built-in rendering; convert materials for other pipelines.
