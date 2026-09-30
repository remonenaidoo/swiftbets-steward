---
id: consumer-lag
title: Consumer lag growing
---
# Consumer lag

## Symptoms
- A consumer group's lag grows steadily; events arrive late (settlements and payouts delayed).

## Evidence to collect
- Consumer outcome rates (`handled`, `transient_failure`, `deferred`), handler duration, and the CPU of the owning service.

## Diagnosis
- **Many `transient_failure` outcomes:** a dependency is failing and the consumer is backing off (see wallet-outage or the database health checks).
- **Handler duration rising with CPU saturated:** under-provisioned; containers throttled at their CPU limit produce long tails.
- **`deferred` on ladder topics:** normal; retry-rung partitions are paused until attempts are due.

## Remediation
- Fix the failing dependency, or scale the consumer (more partitions and replicas) when CPU-bound. Propose `no_action` when lag is on retry rungs only.

## Verification
- Lag trends back to near zero.
