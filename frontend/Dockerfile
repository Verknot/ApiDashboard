# Build from frontend/:
#   docker build -t api-workbench-web .

# bookworm-slim: Alpine npm often dies with "Exit handler never called" on large trees.
FROM node:22-bookworm-slim AS build
WORKDIR /app

ENV NPM_CONFIG_UPDATE_NOTIFIER=false \
    NPM_CONFIG_FUND=false \
    NPM_CONFIG_AUDIT=false \
    NODE_OPTIONS=--max-old-space-size=4096

COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS final

# Official nginx image runs envsubst on /etc/nginx/templates/*.template
ENV API_UPSTREAM=api:8080

COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
