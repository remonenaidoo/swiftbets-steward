INSERT INTO steward.audit (incident_id, action_id, actor, what, detail, at) VALUES (@IncidentId, @ActionId, @Actor, @What, CAST(@Detail AS jsonb), now());
