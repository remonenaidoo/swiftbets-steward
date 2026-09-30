---
id: outbox-backlog
title: Outbox backlog growing
---
# Outbox backlog

## Symptoms
- `swiftbets_outbox_pending` rises for a service; downstream sees events late.

## Evidence to collect
- Outbox pending per service, relay publish failures, Kafka health.

## Diagnosis
- **Publish failures rising:** Kafka is unreachable or a topic is missing (topics are never auto-created; check provisioning).
- **No failures, pending rising:** relay throughput is below the write rate. The relay publishes keys in parallel; check its CPU.
- Rows are never dropped, so nothing is lost while the backlog exists.

## Remediation
- Restore Kafka or provision the missing topic. Propose `no_action` for business data.

## Verification
- Pending returns to near zero.
