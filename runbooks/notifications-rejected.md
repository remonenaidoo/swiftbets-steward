---
id: notifications-rejected
title: Notifications rejected or not sending (customers get no email)
---
# Notifications rejected or not sending

## Symptoms
- `NotificationsRejected`: `swiftbets_notifications_deliveries_total{status="Rejected"}` rises for one template.
- `NotificationsNotSending`: email requests are recorded as `Skipped` and nothing is `Sent` for 30 minutes.
- Customers report missing verification or password-reset emails; identity itself still returns success.

## Evidence to collect
- Rejected deliveries by template and reason from `GET /admin/users/{id}/notifications` for an affected user.
- The sender's recent deploys: which service published the `NotificationRequestedV1` and with which template and data keys.
- Notifications SMTP settings (`Smtp:Host`) and whether the provider answers.

## Diagnosis
- `Rejected` means the request itself is bad: an unknown template, a missing data key or an invalid address. It is a sender bug, usually a sender deployed ahead of notifications or with a renamed key.
- `Skipped` means the request was fine but no email provider is configured or reachable; nothing leaves.
- `Suppressed` is expected (marketing to an account that may not receive it) and never alerts.
- Requests are deduplicated by notification id; rejected and skipped requests are recorded, not retried.

## Remediation
- Rejected: roll back or fix the sender, or deploy the notifications version that knows the template. Propose `no_action`; the customer can request a new verification or reset email once fixed.
- Not sending: restore the provider settings or connectivity, then propose `no_action`. Do not replay requests: the links they carry may already have expired, and customers can re-request.

## Verification
- `Rejected` and `Skipped` stop increasing and `Sent` resumes for the affected template.
- A fresh registration on staging receives its verification email.
