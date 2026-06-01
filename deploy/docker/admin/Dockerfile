# Build context: repository root
FROM node:24-alpine AS build
WORKDIR /app
COPY src/Admin/package.json src/Admin/package-lock.json ./
RUN npm ci
COPY src/Admin/ .
# Same-origin /api via Traefik — do not bake a public API URL into the bundle.
RUN npm run build

FROM nginx:1.27-alpine AS final
RUN apk add --no-cache curl \
    && addgroup --system --gid 1001 nginxgroup \
    && adduser --system --uid 1001 --ingroup nginxgroup nginxuser \
    && chown -R nginxuser:nginxgroup /var/cache/nginx /var/run /var/log/nginx
COPY deploy/docker/admin/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build --chown=nginxuser:nginxgroup /app/dist /usr/share/nginx/html
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD curl -fsS http://127.0.0.1:80/ >/dev/null || exit 1
USER nginxuser
