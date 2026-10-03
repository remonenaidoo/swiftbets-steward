-- Any of the question's words may match (an operator describes symptoms, not a runbook's exact wording); the rank orders them.
SELECT runbook_id AS RunbookId, section AS Section, ts_rank_cd(tsv, query) AS Rank
FROM steward.runbook_chunks,
     CAST(COALESCE(NULLIF(replace(CAST(plainto_tsquery('english', @Query) AS text), ' & ', ' | '), ''), '') AS tsquery) AS query
WHERE tsv @@ query ORDER BY Rank DESC LIMIT @Limit;
