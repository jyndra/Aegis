# Aegis UI Guidelines

## 1. Design goals

The UI should feel:
- calm,
- clear,
- non-judgmental,
- minimal,
- trustworthy.

It should support long-term use without feeling like a punishment screen.

## 2. Visual hierarchy

Primary view:
- protection status,
- lock countdown,
- component health,
- action buttons,
- recent events.

Secondary views:
- rules,
- logs,
- unlock flow,
- diagnostics,
- settings.

## 3. Tone

UI copy should avoid shame and coercion.
Use language like:
- Protection active
- Protection degraded
- Repair required
- Lock active
- Unlock pending
- Browser extension missing

Avoid:
- scary red overload,
- moralizing language,
- hostile messaging.

## 4. Dashboard cards

Recommended cards:
- Overall status
- Lock timer
- DNS health
- Extension health
- Service health
- Proxy health
- Recent blocks
- Recent tamper events

## 5. Block page

When content is blocked:
- show a short reason,
- show the policy category,
- provide no content details that would help bypass,
- optionally offer a safe reminder.

## 6. Unlock screens

Unlock screens should be:
- deliberate,
- step-based,
- timed,
- informative.

They should clearly show:
- why the request is pending,
- how long remains,
- what will happen next.

## 7. Settings screens

Settings should be grouped by:
- filtering,
- integrity,
- lock policy,
- logging,
- diagnostics,
- appearance.

## 8. Accessibility

- high contrast support,
- keyboard navigation,
- readable font sizes,
- clear state labels,
- responsive layouts.

## 9. Empty states

When no events exist or no blocks have occurred:
- show helpful placeholder content,
- do not make the UI look broken.

## 10. Status language

Use these states consistently:
- Protected
- Locked
- Degraded
- Recovery
- Unlock Pending
- Disabled
