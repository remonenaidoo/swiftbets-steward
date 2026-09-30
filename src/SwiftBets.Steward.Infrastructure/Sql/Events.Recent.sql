SELECT topic AS Topic, key AS Key, event_type AS EventType, event_id AS EventId, occurred_at AS OccurredAt, payload::text AS PayloadJson
FROM steward.events WHERE key = @Subject OR topic = @Subject ORDER BY event_seq DESC LIMIT @Limit;
