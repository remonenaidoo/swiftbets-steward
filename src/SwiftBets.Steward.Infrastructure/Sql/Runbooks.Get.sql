SELECT r.runbook_id AS RunbookId, r.title AS Title, r.markdown AS Markdown,
       ARRAY(SELECT c.section FROM steward.runbook_chunks c WHERE c.runbook_id = r.runbook_id ORDER BY c.section) AS Sections
FROM steward.runbooks r WHERE r.runbook_id = ANY(@RunbookIds);
