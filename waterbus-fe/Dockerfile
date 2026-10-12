# syntax=docker/dockerfile:1
# ==============================================================================
# Smart Waterbus Web (Vite + React) — build tĩnh rồi phục vụ bằng nginx
# Build context: thư mục waterbus-fe/
# ==============================================================================

FROM node:20-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
