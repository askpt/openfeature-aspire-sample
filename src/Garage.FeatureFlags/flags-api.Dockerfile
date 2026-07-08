FROM golang:1.27rc2-alpine@sha256:7870fdc211100210e7380f487953c4188fcbeac99646a56926a973161a3eedcd AS builder

WORKDIR /src

COPY go.mod go.sum ./
RUN go mod download

COPY *.go ./
RUN CGO_ENABLED=0 go build -o /app .

FROM alpine:3.24@sha256:28bd5fe8b56d1bd048e5babf5b10710ebe0bae67db86916198a6eec434943f8b
RUN apk --no-cache add ca-certificates
COPY --from=builder /app /app

ENTRYPOINT ["/app"]
