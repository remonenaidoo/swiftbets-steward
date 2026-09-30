---
id: wallet-outage
title: Wallet unavailable (payouts retrying on the ladder)
---
# Wallet outage

## Symptoms
- The payout retry-ladder rate rises: attempts scheduled on `payout.retry-5s` and `payout.retry-1m`.
- Placement returns `503 wallet_unavailable` for new coupons.
- Payout logs `scheduled at step CreditWallet ... wallet unavailable`.

## Evidence to collect
- Ladder retry counts per rung over the last minutes (service metrics).
- Wallet readiness and whether the `wallet.unavailable` fault point is armed.
- Payout state for an affected coupon: `PaidToDate` still behind the settled target, no dead letter yet.

## Diagnosis
- The ladder is doing its job: nothing is lost and nothing is paid twice, because every retry reuses the same versioned idempotency key.
- Retries go 5 s, 1 min, 15 min, then dead-letter. An outage shorter than about 16 minutes drains by itself after recovery.

## Remediation
- Restore the wallet (restart it, or disarm the `wallet.unavailable` fault). Propose `no_action` for payouts: do not replay or re-credit manually while the ladder still holds the attempts, because manual credits would bypass the keys.
- If attempts reached the dead-letter (outage longer than the ladder), propose `replay_dead_letter` for each parked coupon once the wallet is healthy.

## Verification
- Ladder retry rate falls to zero; payments catch up; owed equals paid for the affected coupons.
- The wallet ledger still sums to zero and there are no duplicate idempotency keys.
