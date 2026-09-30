---
id: stuck-coupon
title: Stuck coupon (evaluated but never settled)
---
# Stuck coupon

## Symptoms
- `settlement.stuck-coupon` event for a coupon, or a coupon whose `SettlementPending` flag has stayed set for longer than the reconciler's stuck threshold (default 60 seconds).
- Punter-facing: a bet whose fixture has a result, but whose history row still shows `open`.

## Evidence to collect
- The coupon's settlement state: `LegCount`, each leg's latest evaluated result version, the list of settlements, and the Redis progress count (`RedisResolvedLegs`).
- Whether the `leg-evaluated` events for its legs were published (recent events for the coupon id).
- Whether the settler has been dropping or failing messages (consumer failure metrics, fault points armed on settlement).

## Diagnosis
- **All legs evaluated in SQL, Redis progress below the leg count:** Redis progress was lost or never recorded (key eviction, a dropped message, or an armed `settlement.settler.drop` fault). SQL is the source of truth; the coupon is safe to settle from it.
- **Legs not evaluated in SQL although their fixture has a result:** the result landed while the coupon was being indexed. The reconciler's unevaluated-legs pass repairs this within one interval.
- **Settlement exists but the coupon still shows pending:** the pending flag was not cleared after a no-op settle; harmless, cleared by the next refresh.

## Remediation
- Propose `refresh_coupon` for the coupon. It rebuilds the Redis progress from SQL and settles, and is idempotent: running it twice cannot pay twice, because payout moves only the delta for a new settlement version.
- If a settlement drop fault is armed, disarm it first; otherwise new coupons will keep getting stuck.

## Verification
- The coupon shows a settlement version and `SettlementPending = false`; `RedisResolvedLegs` equals `LegCount`.
- Payout records a payment for that settlement version.
