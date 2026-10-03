---
id: ledger-drift
title: Wallet ledger drift (a balance or posting disagrees with the ledger)
---
# Wallet ledger drift

## Symptoms
- Alert `WalletLedgerDrift`: the latest wallet reconciliation found one or more drifts, labelled by kind.
- Alert `WalletReconciliationStale`: no reconciliation has completed in over 26 hours, so drift would go unnoticed.
- Alert `WalletReconciliationFailing`: a reconciliation run threw before completing.
- Customer-facing: a balance that does not match the customer's own statement of bets, wins and top-ups.

## Evidence to collect
- The latest run: `GET /reconciliation/latest` on the wallet (operator token). It lists every drift with its kind, account, posting, expected (what the ledger says) and actual (what is stored).
- For an account drift: the account's postings in order (`wallet.Postings` by `AccountId`, then their `wallet.LedgerEntries`) and its reservations (`wallet.Reservations`).
- Whether anything wrote to the wallet database outside the service: database audit, recent manual scripts, a restore or migration that ran around the time the account last reconciled clean.
- The wallet's own logs for the run id: each drift is logged once at error level.

## Diagnosis
- **`AvailableBalance` or `ReservedBalance`:** the stored balance column moved without a posting, or a posting's entries were changed after the fact. The ledger entries are the record; the balance column is a projection of them. A difference of exactly one posting's amount usually means a balance was edited by hand.
- **`HeldReservations`:** a reservation's state changed without the matching posting (for example set to released or captured directly), or a reserve posting landed without its reservation row. Compare the reservation's state with the postings that reference its `ReservationId`.
- **`UnbalancedPosting`:** a posting whose entries do not sum to zero, or that has fewer than two entries. The wallet never writes these; this is an out-of-band edit or a failed partial restore.
- **`LedgerTotal`:** the whole ledger does not sum to zero. It always accompanies an unbalanced posting; fix that and this clears.
- **Stale or failing runs:** the reconciler cannot reach the database, a check timed out (600 s per check), or the worker is disabled (`Reconciliation:Enabled=false`). Check the wallet's readiness and logs.

## Remediation
- Propose `engage_kill_switch` with target `placement`: it stops new bets, so no stake or payout lands on a balance that may be wrong, until a person approves and later lifts it in Settings. Propose nothing else: money is involved and a person decides which side is wrong.
- Freeze the affected account first: blacklist it (`PUT /accounts/{id}/blacklist`) so no credit lands on a wrong balance while it is investigated.
- If the ledger is right and the balance is wrong, correct the balance projection to the ledger sum, in one audited change approved by a second operator.
- If the ledger is wrong (a missing or one-sided posting), add a correcting balanced posting with a reference to the incident; never edit or delete existing entries, which are append-only.
- Re-run reconciliation on demand: `POST /reconciliation/runs`.

## Verification
- `POST /reconciliation/runs` returns `isClean: true`, and `swiftbets_wallet_reconciliation_drifts` is zero for every kind.
- The account is taken off the blacklist only after the clean run, and any payouts dead-lettered while it was frozen are replayed.
