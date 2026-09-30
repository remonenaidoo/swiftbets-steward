SELECT EXISTS (
    SELECT 1 FROM steward.events
    WHERE event_type = @EventType AND key = @Key AND discriminator = @Discriminator AND event_id <> @EventId);
