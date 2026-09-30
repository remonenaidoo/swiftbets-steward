UPDATE steward.actions SET status = @Status, decided_by = @DecidedBy, decided_at = @DecidedAt, outcome = @Outcome
WHERE action_id = @ActionId AND status = @Expected;
