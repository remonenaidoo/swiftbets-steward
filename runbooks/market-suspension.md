---
id: market-suspension
title: Suspending a market
---
# Market suspension

## Symptoms
- A price is known to be wrong, a fixture's status is uncertain, or liability on one fixture is climbing unusually fast.

## Evidence to collect
- The fixture's offer (prices, version), recent placements on it, and its running liability.

## Diagnosis
- A suspended market refuses new coupons immediately (`market_suspended`); bets already placed are unaffected and settle normally.

## Remediation
- Propose `suspend_market` with the fixture and market id. It bumps the offer version, so any in-flight coupon priced earlier is refused.

## Verification
- New placements on the market are refused with `market_suspended`.
