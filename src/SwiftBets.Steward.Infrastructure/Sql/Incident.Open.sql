INSERT INTO steward.incidents (incident_id, kind, subject, summary, status, fingerprint, opened_at, updated_at)
VALUES (@IncidentId, @Kind, @Subject, @Summary, @Status, @Fingerprint, @OpenedAt, @OpenedAt)
ON CONFLICT (fingerprint) WHERE status NOT IN ('Resolved', 'DiagnosisFailed') DO NOTHING
RETURNING incident_id;
