---
id: redis-sql-divergence
title: Redis progress diverged from SQL
---
# Redis and SQL divergence

## Symptoms
- The reconciler repairs coupons (a `stuck-coupon` event with `Repaired = true`), or Redis progress counts disagree with SQL evaluations.

## Evidence to collect
- Redis memory policy (must be `noeviction`), key TTLs, recent restarts of Redis.

## Diagnosis
- SQL holds the truth; Redis only gates when settlement runs. Divergence delays settlement but cannot change a payout.

## Remediation
- Propose `refresh_coupon` for affected coupons if the reconciler has not already repaired them. Fix eviction if memory pressure caused it.

## Verification
- Reconciler repair count returns to zero.
