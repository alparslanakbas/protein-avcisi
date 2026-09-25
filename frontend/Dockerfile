FROM node:24.19.0-slim AS build
WORKDIR /src

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM node:24.19.0-slim AS final
WORKDIR /app
ENV NODE_ENV=production
COPY --from=build /src/dist/frontend ./dist/frontend
COPY --from=build /src/node_modules ./node_modules

ENV PORT=4000
EXPOSE 4000

# Root olmadan (node imajındaki `node`, UID 1000). SSR diske yazmıyor,
# dosyaları yalnızca okuyor; port 1024'ün üstünde, ayrıcalık gerekmiyor.
USER node

ENTRYPOINT ["node", "dist/frontend/server/server.mjs"]
