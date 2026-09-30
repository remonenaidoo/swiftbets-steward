---
id: result-correction
title: Result corrected after settlement
---
# Result correction

## Symptoms
- A `Correction` result at a higher version for a fixture that already settled; coupons get a second settlement version.

## Evidence to collect
- Both result versions, the coupon's settlements, and its payments per version.

## Diagnosis
- Expected behaviour: settlement writes a new version when the outcome or payout changes, and payout moves only the delta: `RESETTLE_CREDIT` when the correction raises the payout, `RESETTLE_DEBIT` when it lowers it.

## Remediation
- Propose `no_action` when the deltas were applied. If a debit was refused because the punter's balance was too low, the payout is parked for an operator.

## Verification
- For each coupon, `PaidToDate` equals the latest settlement's payout.
