SELECT incident_id AS IncidentId, kind AS Kind, subject AS Subject, summary AS Summary, status AS Status, opened_at AS OpenedAt FROM steward.incidents WHERE incident_id = @IncidentId;
SELECT report::text AS Report, problems::text AS Problems FROM steward.reports WHERE incident_id = @IncidentId;
SELECT tool_call_id AS ToolCallId, name AS Name, input::text AS InputJson, output AS OutputJson FROM steward.tool_calls WHERE incident_id = @IncidentId ORDER BY called_at;
SELECT action_id AS ActionId, incident_id AS IncidentId, type AS Type, target AS Target, rationale AS Rationale, status AS Status, decided_by AS DecidedBy, decided_at AS DecidedAt, outcome AS Outcome
FROM steward.actions WHERE incident_id = @IncidentId;
