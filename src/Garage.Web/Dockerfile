FROM node:25-alpine@sha256:26ded7f450a0ad37241d2ae97ea521a59cb551a1785c8a950f74b0a291ad3aea AS build

WORKDIR /app

COPY package.json package.json
COPY package-lock.json package-lock.json

RUN npm install

COPY . .

RUN npm run build

FROM nginx:1.29.5-alpine@sha256:05fcc6f6c80dc65c0f241f250677aad26e0962d118abaa65a513796862ff915d

# Install curl for healthcheck (optional)
RUN apk add --no-cache curl

COPY --from=build /app/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html

# Expose the default nginx port
EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
