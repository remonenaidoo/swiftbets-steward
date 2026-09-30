INSERT INTO steward.runbook_chunks (runbook_id, section, title, content, content_hash, embedding)
VALUES (@RunbookId, @Section, @Title, @Content, @ContentHash, CAST(@Embedding AS vector))
ON CONFLICT (runbook_id, section) DO UPDATE
SET title = EXCLUDED.title, content = EXCLUDED.content, content_hash = EXCLUDED.content_hash, embedding = EXCLUDED.embedding;
