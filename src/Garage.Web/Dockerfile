FROM node:25-alpine@sha256:3f162243a45e30f6b20f80255779785789e142bec7b1d5d3b431f31d5b3610f6 AS build

WORKDIR /app

COPY package.json package.json
COPY package-lock.json package-lock.json

RUN npm install

COPY . .

RUN npm run build

FROM nginx:alpine

# Install curl for healthcheck (optional)
RUN apk add --no-cache curl

COPY --from=build /app/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/public/config.template.js /etc/nginx/templates/config.template.js
COPY --from=build /app/dist /usr/share/nginx/html

# Expose the default nginx port
EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
