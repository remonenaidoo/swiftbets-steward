INSERT INTO steward.spend (incident_id, model, input_tokens, output_tokens, cache_read_tokens, cache_write_tokens, cost_usd, at)
VALUES (@IncidentId, @Model, @InputTokens, @OutputTokens, @CacheReadTokens, @CacheWriteTokens, @CostUsd, now());
