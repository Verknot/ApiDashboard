# Build from frontend/:
#   docker build -t api-workbench-web .

FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS final

# Official nginx image runs envsubst on /etc/nginx/templates/*.template
ENV API_UPSTREAM=api:8080

COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
