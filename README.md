# swiftbets-steward

[![ci](https://github.com/remonenaidoo/swiftbets-steward/actions/workflows/ci.yml/badge.svg)](https://github.com/remonenaidoo/swiftbets-steward/actions/workflows/ci.yml)

Steward, the SwiftBets operations copilot: rule-based detectors over the event stream and metrics, a tool-calling agent that diagnoses incidents with evidence it must cite, hybrid runbook retrieval in pgvector, incident reports, and approval-gated remediation with an audit trail. Also orchestrates fault injection.

## Hosts

- `SwiftBets.Steward.Api`: incidents, approvals, faults.
- `SwiftBets.Steward.Migrator`: one-shot DbUp migrator for Postgres (enables pgvector).

## Data and events

- **Owns:** Postgres `sb_steward` (runbooks, chunks, vectors, incidents, tool calls, reports, actions, audit).
- **Events:** Consumes the platform event stream and DLQs; produces `steward.incident-raised`, `steward.incident-updated`, `steward.remediation-executed`.

## Layout

Clean Architecture, enforced by project references and `*.ArchitectureTests`:

```
src/*.Domain          pure domain, no references
src/*.Application     use cases and ports; depends on Domain and contracts only
src/*.Infrastructure  adapters (Dapper + embedded .sql, Kafka, Redis); implements Application ports
src/*.Api | *.Worker  composition root: observability, error envelope, health, metrics
src/*.Migrator        DbUp scripts under Migrations/, run once before the host starts
```

Every host exposes `/health/live`, `/health/ready` (checks its real dependencies), `/metrics` (Prometheus), logs compact JSON with correlation ids, and exports traces over OTLP.

## Build and test

```bash
../swiftbets-platform/scripts/fetch-shared-packages.sh .   # or pack-local.sh for unreleased shared changes
dotnet test SwiftBets.Steward.slnx
```

Integration tests use Testcontainers and need Docker. The whole platform runs from `swiftbets-platform` with `make up`.

## Images

Multi-arch (amd64 + arm64), non-root, chiseled runtime:

- `ghcr.io/remonenaidoo/swiftbets-steward`
- `ghcr.io/remonenaidoo/swiftbets-steward-migrator`

## License

MIT
