package main

import (
	"context"
	"errors"
	"log/slog"
	"strings"

	"github.com/open-feature/go-sdk-contrib/providers/ofrep"
	"github.com/open-feature/go-sdk/openfeature"
	"go.opentelemetry.io/otel/codes"
)

// initOpenFeature initializes the OpenFeature client with OFREP provider
func initOpenFeature(ctx context.Context) error {
	ofrepEndpoint := openFeatureEndpoint()
	if ofrepEndpoint == "" {
		return errors.New("OFREP_ENDPOINT environment variable is not set")
	}

	// Create OFREP provider
	ofrepProvider := ofrep.NewProvider(ofrepEndpoint)

	// Register the provider
	if err := openfeature.SetProviderWithContextAndWait(ctx, ofrepProvider); err != nil {
		return err
	}

	// Create a client
	featureClient = openfeature.NewDefaultClient()

	return nil
}

// getPreviewModeFlags returns the list of flag keys allowed for preview-mode
// updates, or an empty slice if the feature is disabled.
func getPreviewModeFlags(ctx context.Context) []string {
	ctx, span := tracer.Start(ctx, "getPreviewModeFlags")
	defer span.End()

	if featureClient == nil {
		slog.WarnContext(ctx, "OpenFeature client not initialized")
		return []string{}
	}

	value, err := featureClient.StringValue(ctx, "enable-preview-mode", "", openfeature.EvaluationContext{})
	if err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		slog.WarnContext(ctx, "Failed to evaluate enable-preview-mode flag", "error", err)
		return []string{}
	}

	if value == "" {
		return []string{}
	}

	// Split comma-separated flag keys and trim whitespace
	flags := strings.Split(value, ",")
	result := make([]string, 0, len(flags))
	for _, f := range flags {
		trimmed := strings.TrimSpace(f)
		if trimmed != "" {
			result = append(result, trimmed)
		}
	}
	return result
}
