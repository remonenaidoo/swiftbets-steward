---
id: payments-degraded
title: Payment webhooks rejected or the open-payments sweep failing
---
# Payments degraded

## Symptoms
- Incident `PaymentsDegraded` with subject `<provider>`, opened from alert `PaymentsWebhooksRejected` or `PaymentsSweepFailing`.
- `PaymentsWebhooksRejected`: more than five provider webhooks refused in 15 minutes, usually for a bad signature after a secret rotation, or a provider sending a new payload shape.
- `PaymentsSweepFailing`: the sweep that asks providers about open payments keeps failing, so deposits whose webhooks were lost stay pending.
- Customer-facing: a paid deposit that has not reached the balance, or a withdrawal that stays "processing".

## Evidence to collect
- The metrics `payments_webhooks_rejected_last_15m` and `payments_sweep_failures_last_30m`: how many webhooks were refused, and whether the sweep is failing.
- Recent events for the provider: deposits started and succeeded, to see whether successes stopped arriving.
- How many customers are waiting: the customer impact for the provider.

## Diagnosis
- **Signature failures:** the webhook secret at the provider and ours differ (a rotation on one side only). The webhooks are refused, so the money is safe but not yet credited.
- **Sweep failing:** the provider's status API is unreachable or answering errors; once it is back, the sweep finishes the open payments itself.
- Either way nothing is lost: the provider still holds the outcome of every payment, and asking it again is safe because outcomes apply once.

## Remediation
- Fix the cause first: align the webhook secret, or wait for the provider's status API to recover.
- Then propose `replay_payment_webhooks` with the provider as the target: it asks the provider about every open payment now and applies each outcome as its webhook would have. It is idempotent, so a payment already finished is unchanged.

## Verification
- Open deposits for the provider fall back to normal, and the customers who waited see their balance.
- `PaymentsWebhooksRejected` and `PaymentsSweepFailing` resolve.
