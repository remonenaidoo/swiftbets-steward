---
id: payment-drift
title: Payments reconciliation drift (the provider and the ledger disagree)
---
# Payment drift

## Symptoms
- Incident `PaymentDrift` with subject `<provider>:<day>`, opened from `payments.drift-detected`.
- Alert `PaymentsReconciliationDrift`: the last daily run found drifts.
- Alert `PaymentsReconciliationStale`: no run has completed in over 26 hours, so drift would go unnoticed.
- Customer-facing: a deposit the customer paid that never reached their balance, or a withdrawal shown as paid that never arrived.

## Evidence to collect
- The run: `GET /admin/payments/reconciliation/{provider}/latest` on payments (operator token with `payments.read`). Each drift has its kind, our reference (`dep_…` or `wd_…`), the provider's amount, the ledger's amount and the currency.
- For each reference: the payment's row (`payments.Deposits` or `payments.Withdrawals`) and its status history in the outbox (`payments.*` events keyed by the user).
- The provider's own record of the reference (simulator `GET /v1/deposits/{reference}` or `/v1/transfers/{reference}`; Paystack `transaction/verify/{reference}`).
- The wallet's postings with `Reference = <reference>` (`wallet.Postings`), which show whether the money moved in the ledger.
- Webhook receipts for the reference (`payments.WebhookEvents`) and the payments logs around it: a webhook rejected for its signature (`payments_webhooks_rejected_total`) or answered 503.

## Diagnosis
- **`MissingInLedger` (provider settled, we did not):**
  - A deposit whose success webhook was lost, rejected or never applied, and that the sweep has not finished yet. The deposit is still Pending.
  - A deposit the wallet refused after payment whose refund failed. The deposit is Failed with "refused by the wallet"; the provider still shows it settled.
  - A movement the provider attributes to us that we never started (a reference we do not recognise), for example a test settlement injected at the simulator.
- **`MissingAtProvider` (we recorded success, the provider did not):** a deposit credited on a forged or misattributed webhook, or a provider-side reversal or chargeback after we credited it.
- **`AmountMismatch`:** the provider settled a different amount or currency than the intent. Payments never credits a mismatched success; check whether the provider later adjusted the amount.
- **Stale or failing runs:** the provider is unreachable (`ProviderUnavailableException` in the logs), or the worker is disabled (`Payments:RunWorkers=false`).

## Remediation
- There is no automated remediation, and Steward must not propose one. Money is involved and a person decides which side is wrong.
- A Pending deposit the provider settled: wait one sweep (about a minute). If it stays Pending, run the sweep's check by hand. Never credit the wallet directly; the deposit's wallet key must be the one used.
- A refused deposit whose refund failed: refund it at the provider, with the incident in the note.
- A credited deposit the provider reversed: freeze the customer's account (wallet blacklist), then post a correcting debit with the incident as reference, approved by a second operator.
- An unknown reference at the provider: raise it with the provider; nothing changes in the ledger.
- Rerun the reconciliation for the day (`POST /admin/payments/reconciliation/{provider}/run?day=`) and close the incident once it finds no drift.
