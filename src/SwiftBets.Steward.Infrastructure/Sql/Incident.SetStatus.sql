UPDATE steward.incidents SET status = @Status, updated_at = now() WHERE incident_id = @IncidentId;
