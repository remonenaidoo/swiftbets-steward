---
id: saga-orphans
title: Placement sagas left unfinished
---
# Saga orphans

## Symptoms
- Placement saga intents in `Started`, `Reserved` or `Compensating` past their deadline; punters see `coupon_not_placed` or funds briefly held.

## Evidence to collect
- Sweeper activity (compensated and completed counts), wallet reservations held under the saga's reserve key.

## Diagnosis
- The sweeper releases holds for sagas that never persisted a coupon, and completes capture for sagas that did. It depends on the wallet being reachable.
- A captured reservation without a persisted coupon is logged as critical and needs a human.

## Remediation
- Keep the wallet healthy; the sweeper converges by itself. Propose `no_action` unless a captured-without-coupon case is logged.

## Verification
- No intents past their deadline in non-terminal states; no reservations left Held for them.
