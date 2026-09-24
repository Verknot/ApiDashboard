# Build from frontend/:
#   docker build -t api-workbench-web .

FROM node:22-bookworm-slim AS build
WORKDIR /app

# "Exit handler never called" usually means the npm process was killed (OOM / SIGKILL).
# Pin npm 10 (more stable in containers than npm 11 shipped with some images) and
# fall back to npm install if npm ci still dies.
ENV NPM_CONFIG_UPDATE_NOTIFIER=false \
    NPM_CONFIG_FUND=false \
    NPM_CONFIG_AUDIT=false \
    NPM_CONFIG_PROGRESS=false \
    NPM_CONFIG_FETCH_RETRIES=5 \
    NPM_CONFIG_FETCH_RETRY_MINTIMEOUT=20000 \
    NPM_CONFIG_FETCH_RETRY_MAXTIMEOUT=120000 \
    NODE_OPTIONS=--max-old-space-size=2048

RUN npm install -g npm@10.9.2

COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund \
  || (echo "npm ci failed, falling back to npm install" \
      && rm -rf node_modules \
      && npm install --no-audit --no-fund)

COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS final

# Official nginx image runs envsubst on /etc/nginx/templates/*.template
ENV API_UPSTREAM=api:8080

COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
