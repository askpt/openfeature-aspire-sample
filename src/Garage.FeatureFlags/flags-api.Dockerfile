FROM golang:1.25-alpine AS builder

WORKDIR /src

COPY go.mod go.sum ./
RUN go mod download

COPY *.go ./
RUN CGO_ENABLED=0 go build -o /app .

FROM alpine:3.23
RUN apk --no-cache add ca-certificates
COPY --from=builder /app /app

ENTRYPOINT ["/app"]
