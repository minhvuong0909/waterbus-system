# syntax=docker/dockerfile:1
# ==============================================================================
# Smart Waterbus Web (Vite + React) — build tĩnh rồi phục vụ bằng nginx
# Build context: thư mục waterbus-fe/
# ==============================================================================

FROM node:20-alpine AS build
WORKDIR /app
RUN npm install -g pnpm@12.11.2
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY . .
RUN pnpm build

FROM nginx:1.27-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
