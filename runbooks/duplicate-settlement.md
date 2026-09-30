---
id: duplicate-settlement
title: Duplicate settlement event
---
# Duplicate settlement

## Symptoms
- The same coupon and settlement version appear twice on `settlement.coupon-settled` with different event ids (a re-publish, a relay retry after a lost acknowledgement, or a replayed partition).

## Evidence to collect
- Both settlement events for the coupon (recent events).
- The coupon's payout state: the payments recorded per settlement version and `PaidToDate`.
- The wallet credits for the coupon's idempotency keys.

## Diagnosis
- Payout guards on the settlement version: a repeated version is a no-op (`LastVersion` check), and the wallet refuses a second posting under the same idempotency key.
- **Exactly one payment for the version and `PaidToDate` equal to the target:** the duplicate was absorbed and money is correct.
- **More than one payment for one version:** a real double payment. Escalate immediately; this should be impossible and means an invariant broke.

## Remediation
- When absorbed, propose `no_action`, and find why the event was published twice (outbox relay retries are expected; a manual re-publish is not).
- Never debit manually. If a genuine overpayment exists, it is corrected with a new settlement version, which payout turns into a `RESETTLE_DEBIT` delta.

## Verification
- One payment per coupon and version; the wallet has no duplicate idempotency keys.
