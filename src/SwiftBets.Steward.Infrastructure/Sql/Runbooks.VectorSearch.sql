SELECT runbook_id AS RunbookId, section AS Section, 1 - (embedding <=> CAST(@Embedding AS vector)) AS Similarity
FROM steward.runbook_chunks ORDER BY embedding <=> CAST(@Embedding AS vector) LIMIT @Limit;
