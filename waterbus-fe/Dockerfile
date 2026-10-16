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
# Rỗng = FE gọi đường dẫn tương đối /api, /hubs cùng origin; nginx proxy sang API trong mạng compose.
# Không để rơi về mặc định http://localhost:5000 trong src/constant/env.ts (cổng API không public khi deploy).
ARG VITE_API_BASE_URL=""
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
RUN pnpm build

FROM nginx:1.27-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
