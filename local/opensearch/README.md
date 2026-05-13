# OpenSearch (logs + retention)

The stack uses **daily indices** `logs-{yyyy.MM.dd}` from Serilog (`Api/appsettings.Development.json` and optional Worker overrides). The **Service** field distinguishes `api` vs `worker`.

## Apply 5-day retention (ISM)

After OpenSearch is up (`docker compose up -d` from `local/`):

```bash
curl -sS -X PUT "http://localhost:9200/_plugins/_ism/policies/logs-retention-5d" \
  -H "Content-Type: application/json" \
  --data-binary "@local/opensearch/ism-policy-logs-retention.json"
```

Then attach the policy to the index template (or existing indices) per your OpenSearch version UI, or use Dev Tools to add existing indices to the policy if they were created before the template applied.

For **fresh indices** matching `logs-*`, the `ism_template` in the JSON above should attach automatically once the policy is created (OpenSearch 2.x ISM).

## UI

OpenSearch Dashboards: http://localhost:5601 (dev image has security plugin disabled to match the node).

## Production note

Do **not** ship `DISABLE_SECURITY_PLUGIN=true`. Use proper TLS, fine-grained access control, and private networking; point Serilog at an HTTPS endpoint with credentials via environment variables, not committed config.
