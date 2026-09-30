CREATE TABLE steward.incidents
(
    incident_id  uuid        PRIMARY KEY,
    kind         text        NOT NULL,
    subject      text        NOT NULL,
    summary      text        NOT NULL,
    status       text        NOT NULL,
    fingerprint  text        NOT NULL,
    opened_at    timestamptz NOT NULL,
    updated_at   timestamptz NOT NULL
);
-- One unresolved incident per fingerprint: repeated signals collapse into it.
CREATE UNIQUE INDEX ux_incidents_open_fingerprint ON steward.incidents (fingerprint) WHERE status NOT IN ('Resolved', 'DiagnosisFailed');
CREATE INDEX ix_incidents_opened ON steward.incidents (opened_at DESC);

CREATE TABLE steward.tool_calls
(
    incident_id  uuid        NOT NULL REFERENCES steward.incidents (incident_id),
    tool_call_id text        NOT NULL,
    name         text        NOT NULL,
    input        jsonb       NOT NULL,
    output       text        NOT NULL,
    called_at    timestamptz NOT NULL,
    PRIMARY KEY (incident_id, tool_call_id)
);

CREATE TABLE steward.reports
(
    incident_id  uuid        PRIMARY KEY REFERENCES steward.incidents (incident_id),
    report       jsonb       NULL,
    problems     jsonb       NOT NULL,
    created_at   timestamptz NOT NULL
);

CREATE TABLE steward.actions
(
    action_id    uuid        PRIMARY KEY,
    incident_id  uuid        NOT NULL REFERENCES steward.incidents (incident_id),
    type         text        NOT NULL,
    target       text        NOT NULL,
    rationale    text        NOT NULL,
    status       text        NOT NULL,
    decided_by   text        NULL,
    decided_at   timestamptz NULL,
    outcome      text        NULL
);
CREATE INDEX ix_actions_incident ON steward.actions (incident_id);

CREATE TABLE steward.audit
(
    audit_id     bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    incident_id  uuid        NOT NULL,
    action_id    uuid        NULL,
    actor        text        NOT NULL,
    what         text        NOT NULL,
    detail       jsonb       NOT NULL,
    at           timestamptz NOT NULL
);
CREATE INDEX ix_audit_incident ON steward.audit (incident_id, at);

CREATE TABLE steward.events
(
    event_seq     bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    topic         text        NOT NULL,
    key           text        NOT NULL,
    event_type    text        NOT NULL,
    event_id      uuid        NULL,
    discriminator text        NULL,
    occurred_at   timestamptz NOT NULL,
    payload       jsonb       NOT NULL
);
CREATE INDEX ix_events_key ON steward.events (key, event_seq DESC);
CREATE INDEX ix_events_topic ON steward.events (topic, event_seq DESC);
CREATE INDEX ix_events_occurred ON steward.events (occurred_at);

CREATE TABLE steward.spend
(
    spend_id     bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    incident_id  uuid        NOT NULL,
    model        text        NOT NULL,
    input_tokens bigint      NOT NULL,
    output_tokens bigint     NOT NULL,
    cache_read_tokens bigint NOT NULL,
    cache_write_tokens bigint NOT NULL,
    cost_usd     numeric(12, 6) NOT NULL,
    at           timestamptz NOT NULL
);
CREATE INDEX ix_spend_at ON steward.spend (at);

CREATE TABLE steward.runbook_chunks
(
    runbook_id   text        NOT NULL,
    section      text        NOT NULL,
    title        text        NOT NULL,
    content      text        NOT NULL,
    content_hash text        NOT NULL,
    embedding    vector(768) NOT NULL,
    tsv          tsvector    GENERATED ALWAYS AS (to_tsvector('english', title || ' ' || section || ' ' || content)) STORED,
    PRIMARY KEY (runbook_id, section)
);
CREATE INDEX ix_runbook_chunks_tsv ON steward.runbook_chunks USING gin (tsv);

CREATE TABLE steward.runbooks
(
    runbook_id   text PRIMARY KEY,
    title        text NOT NULL,
    markdown     text NOT NULL
);
