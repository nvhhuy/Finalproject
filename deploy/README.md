# Deploy (remote / production)

This folder is reserved for **future** production-oriented assets, for example:

- A multi-stage **`Dockerfile`** for the Api (often kept under `deploy/` or `src/Backend/` once you add it)
- `docker-compose.prod.yml` or Kubernetes manifests
- CI/CD notes (registry tags, secrets management)

The **Api** image build is not wired in this repo yet; add a Dockerfile when you connect a registry or Kubernetes.

For day-to-day development, use repo root **`local/docker-compose.yml`** (Postgres, Redis, MinIO, OpenSearch, Dashboards).
