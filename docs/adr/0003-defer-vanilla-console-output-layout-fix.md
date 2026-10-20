# Defer the vanilla console output layout fix

status: accepted

Manual A/B testing with `CheatConsoleExtended` removed reproduced the console output area's upward-looking edge clipping, so this behavior belongs to the vanilla `ConsoleWidget` layout rather than being a regression owned by configurable-font support. The current branch therefore keeps font selection, persistence, and the corrected output-row sizing, does not add compensating offsets, and defers any viewport/content layout correction to a separate, explicitly scoped change; mod-specific layout code is removed or restored only when a parity review shows that it changes vanilla behavior.

The agreed cleanup plan is deliberately narrow: remove only the explicit `verticalNormalizedPosition = 0f` assignment from `RefreshLayout`, while retaining canvas/layout rebuilding needed for immediate font reflow. Do not restore the superseded pre-`42d05a1` row-sizing logic. Acceptance for that cleanup is same-scene A/B parity between the modded and vanilla console; any real-game test still requires explicit user approval.

The implementation specification is [Issue #21](https://github.com/EltonZhang777/Blasphemous.CheatConsoleExtended/issues/21). No additional ticket breakdown is needed because the work has one existing seam and no blocking dependency.
