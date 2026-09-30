SELECT COALESCE(SUM(cost_usd), 0) FROM steward.spend WHERE at >= date_trunc('month', now());
