---
id: provider-drift
title: Casino provider reconciliation drift (the game provider and our ledger disagree)
---
# Casino provider drift

## Symptoms
- Incident `ProviderDrift` with subject `<provider>:<day>`, opened when the casino's daily reconciliation with a game provider reports drift.
- Transactions the provider reports that we never recorded (missing on our side), or ones we recorded that the provider does not report (missing at the provider), and the net difference between the two.

## Evidence to collect
- The provider's recent reconciliation runs: our net, the provider's net, the drift and the missing counts on each side.
- Recent events for the subject, to see whether the casino gateway was refusing or failing wallet calls that day.
- How many customers played with the provider that day: the customer impact for the provider.

## Diagnosis
- **Missing on our side:** a bet or win the provider applied but our wallet call failed or was never received. The provider believes money moved that our ledger does not show.
- **Missing at the provider:** a transaction we applied that the provider rolled back or never committed, or a report that omitted it.
- A difference that matches one rolled-back bet usually means the rollback arrived after the report was cut, and will clear on the next day's run.

## Remediation
- There is no automated remediation, and Steward must not propose one. Money is involved and finance decides with the provider which side is right. Propose `no_action`.
- Finance raises each missing transaction with the provider, then posts any correction as a reviewed, balanced adjustment.

## Verification
- The next reconciliation for the provider and day reports `matched`, and the missing counts are zero.
