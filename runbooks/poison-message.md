---
id: poison-message
title: Poison message parked on a dead-letter queue
---
# Poison message

## Symptoms
- A message appears on a `<topic>.<env>.dlq` topic, with headers `dlq-source-topic`, `dlq-source-partition`, `dlq-source-offset` and `dlq-reason`.
- Consumer metrics show a `dead_lettered` outcome.

## Evidence to collect
- The dead-lettered message's source topic, offset, key and reason.
- Whether later messages on the same partition were processed (the consumer group's committed offset moved past the poison offset).
- Whether several poison messages share a producer or a contract version.

## Diagnosis
- **Reason starts with `deserialization`:** the payload is not a valid envelope (malformed JSON or a producer bug).
- **Reason starts with `envelope`:** the event type or version does not match the topic's contract; a producer is publishing an unexpected version.
- **Reason starts with `validation`:** the payload failed the consumer's validator.
- The partition keeps flowing by design; one poison message never blocks the rest.

## Remediation
- Propose `no_action` for the platform itself: the message is quarantined and nothing downstream depends on it.
- Fix the producer. If the message carried real business data, it can be re-published once corrected; never replay it unchanged.

## Verification
- No new messages on the DLQ; the consumer's committed offset keeps advancing.
