SELECT incident_id AS IncidentId, kind AS Kind, subject AS Subject, summary AS Summary, status AS Status, opened_at AS OpenedAt
FROM steward.incidents ORDER BY opened_at DESC LIMIT @Limit;
