INSERT INTO steward.reports (incident_id, report, problems, created_at)
VALUES (@IncidentId, CAST(@Report AS jsonb), CAST(@Problems AS jsonb), now())
ON CONFLICT (incident_id) DO UPDATE SET report = EXCLUDED.report, problems = EXCLUDED.problems, created_at = EXCLUDED.created_at;
