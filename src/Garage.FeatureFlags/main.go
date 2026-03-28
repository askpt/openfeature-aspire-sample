package main

import (
	"cmp"
	"context"
	"errors"
	"fmt"
	"log/slog"
	"net"
	"net/http"
	"os"
	"os/signal"
	"time"

	"github.com/open-feature/go-sdk/openfeature"
	"go.opentelemetry.io/contrib/instrumentation/net/http/otelhttp"
	"go.opentelemetry.io/otel"
	"golang.org/x/sync/errgroup"
)

var (
	featureClient *openfeature.Client
	tracer        = otel.Tracer("flags-api")
)

// openFeatureEndpoint returns the OFREP endpoint from the environment.
func openFeatureEndpoint() string {
	return os.Getenv("OFREP_ENDPOINT")
}

func init() {
	flagsFilePath = os.Getenv("FLAGS_FILE_PATH")
	if flagsFilePath == "" {
		flagsFilePath = "../Garage.AppHost/flags/flagd.json"
	}
	backend = newBackend()
}

func main() {
	if err := run(context.Background()); err != nil {
		slog.Error("Failed to run", slog.Any("error", err))
		os.Exit(1)
	}
}

func run(ctx context.Context) error {
	ctx, stop := signal.NotifyContext(ctx, os.Interrupt)
	defer stop()

	port := cmp.Or(os.Getenv("PORT"), "8080")
	// Listen addrs
	var lc net.ListenConfig
	ln, err := lc.Listen(ctx, "tcp", ":"+port)
	if err != nil {
		return fmt.Errorf("failed to start listener: %w", err)
	}

	defer func() {
		if err := ln.Close(); err != nil && !errors.Is(err, net.ErrClosed) {
			slog.Error("Failed to stop listener", "error", err)
		}
	}()

	// Initialize OpenTelemetry
	shutdown, err := initOtel(ctx)
	if err != nil {
		slog.Warn("Failed to initialize OpenTelemetry", "error", err)
	} else {
		slog.Info("OpenTelemetry initialized successfully")
	}

	// Initialize OpenFeature with OFREP provider
	if err := initOpenFeature(ctx); err != nil {
		slog.Warn("Failed to initialize OpenFeature", "error", err)
		slog.Info("Flag updates require preview mode to be configured")
	} else {
		slog.Info("OpenFeature initialized successfully with OFREP provider")
	}

	// Start the server
	server := newServer()
	eg, ctx := errgroup.WithContext(ctx)

	eg.Go(func() error {
		slog.Info("Server listening", "port", port, "flagsFilePath", flagsFilePath)
		if err := server.Serve(ln); !errors.Is(err, http.ErrServerClosed) {
			return err
		}
		return nil
	})

	// wait for interrupt signal
	<-ctx.Done()

	slog.Info("Server is starting to exit...")
	shutdownCtx, cancel := context.WithTimeout(context.Background(), 15*time.Second)
	defer cancel()

	if err := server.Shutdown(shutdownCtx); err != nil {
		slog.Error("Server forced to shutdown", "error", err)
	}

	// wait for server shutdown
	err = eg.Wait()
	if err != nil {
		slog.Error("Server failed", "error", err)
	}

	slog.Info("Server is exited")

	if err := openfeature.ShutdownWithContext(shutdownCtx); err != nil {
		slog.Error("Error shutting down openfeature", "error", err)
	}

	if shutdown != nil {
		if err := shutdown(shutdownCtx); err != nil {
			slog.Error("Error shutting down telemetry", "error", err)
		}
	}

	return nil
}

func newServer() *http.Server {
	mux := http.NewServeMux()

	mux.HandleFunc("GET /flags/{userId}", handleGetFlags)
	mux.HandleFunc("POST /flags/{userId}", handleUpdateFlagTargeting)
	mux.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
		_, _ = w.Write([]byte("Hello from Go Feature Flags API!"))
	})

	return &http.Server{Handler: otelhttp.NewHandler(mux, "flags-api")}
}
