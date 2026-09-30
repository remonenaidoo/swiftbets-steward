SELECT runbook_id AS RunbookId, section AS Section, ts_rank_cd(tsv, query) AS Rank
FROM steward.runbook_chunks, websearch_to_tsquery('english', @Query) AS query
WHERE tsv @@ query ORDER BY Rank DESC LIMIT @Limit;
