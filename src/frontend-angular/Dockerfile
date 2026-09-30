FROM node:24-alpine AS build
ARG LIFEOS_BUILD_ID=local
ENV LIFEOS_BUILD_ID=${LIFEOS_BUILD_ID}
WORKDIR /src

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

RUN npm run build

FROM nginx:1.27-alpine AS final
ARG LIFEOS_BUILD_ID=local
COPY --from=build /src/dist/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --chmod=755 docker-entrypoint.d/ /docker-entrypoint.d/

ENV LIFEOS_BUILD_ID=${LIFEOS_BUILD_ID}
EXPOSE 80
