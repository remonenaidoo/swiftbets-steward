INSERT INTO steward.tool_calls (incident_id, tool_call_id, name, input, output, called_at)
VALUES (@IncidentId, @ToolCallId, @Name, CAST(@Input AS jsonb), @Output, now())
ON CONFLICT DO NOTHING;
