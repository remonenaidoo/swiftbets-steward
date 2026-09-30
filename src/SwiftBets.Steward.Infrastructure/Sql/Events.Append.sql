INSERT INTO steward.events (topic, key, event_type, event_id, discriminator, occurred_at, payload)
VALUES (@Topic, @Key, @EventType, @EventId, @Discriminator, @OccurredAt, CAST(@Payload AS jsonb));
