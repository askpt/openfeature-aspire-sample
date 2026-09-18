FROM node:26-alpine@sha256:d1afba4b9c2edf2dcc56684c90e6c670309eecbc558b9d4bc7f530efb9934b43 AS build

WORKDIR /app

COPY package.json package.json
COPY package-lock.json package-lock.json

RUN npm install

COPY . .

RUN npm run build

FROM nginx:1.31.6-alpine@sha256:35c6bab452e41fc906bfa367bccff2e92f88ba6723d07479e7a18e1c6a3f5b52

# Install curl for healthcheck (optional)
RUN apk add --no-cache curl

COPY --from=build /app/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html

# Expose the default nginx port
EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
