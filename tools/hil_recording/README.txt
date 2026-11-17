Existing Unity HIL workflows: passive V2.2 recording

No separate test-start UI or terminal step is required. HilSimulationUi invokes
service.py attach only after the existing HIL CLI returns the real run_id.
The recorder's own user unit survives Unity closing. It only attaches existing
read-only observers and archives the finished app-owned logs; it never calls
queue_start, stop, flash, authorize, publishers, CAN send or real-vehicle deploy.

The observer uses the same app-owned ROS environment and domains as the selected
profile. It does not replace localization with truth. ROS header simulation
stamps are used for pose/path measurements. Large maps/models are not duplicated.

After mission completion the ordinary Gazebo runtime remains running. Wait for
the existing 5-simulation-second terminal observations, then use the existing
stop button. The physical USB-CAN mux writes its final report only on shutdown.
The recorder waits for that normal shutdown (or the next startup's confirmed
previous-session cleanup), collects logs, seals their hashes, and audits them.
It never stops HIL merely to obtain a log. If stopped too early, incomplete
evidence is preserved and does not become a pass.

.local/hil/recordings/runs/RUN holds each immutable captured attempt and status.
.local/hil/recordings/campaigns/ID holds index.json, cases.json, cases.csv and a
Chinese 30-item record. Latest attempts are explicit; failed attempts remain.
A finished five-profile round or a new local calendar date opens a new campaign.
The desktop entry is only a symlink to campaigns, not another model/log copy.

requirements.json is V2.2's source of truth. It intentionally narrows acceptance
to measurements available in the five ordinary workflows, replacing V2.1's
special-injection cases; old frozen scripts, criteria and historical evidence
are unchanged. All 30 criteria are explicit and mirrored in the generated Word
documents. Missing evidence/failures do not get hidden by past successful runs.
This is not AEF/ISOBUS certification, real agronomic performance or a third-party
signature. Screenshots must be supplied by the actual test operator.

Offline tests: tools/tests/test_hil_gui_recording.py and Unity HilRecordingUiTests.
They use temporary directories and stub processes only; not physical retests.
