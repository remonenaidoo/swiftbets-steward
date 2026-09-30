SELECT action_id AS ActionId, incident_id AS IncidentId, type AS Type, target AS Target, rationale AS Rationale, status AS Status, decided_by AS DecidedBy, decided_at AS DecidedAt, outcome AS Outcome
FROM steward.actions WHERE action_id = @ActionId;
