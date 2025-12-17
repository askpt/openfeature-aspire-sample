package main

import (
	"context"
	"encoding/json"
	"errors"
	"log/slog"
	"net/http"
	"os"
	"slices"
	"strings"
	"sync"

	"github.com/open-feature/go-sdk-contrib/providers/ofrep"
	"github.com/open-feature/go-sdk/openfeature"
	"go.opentelemetry.io/contrib/bridges/otelslog"
	"go.opentelemetry.io/contrib/exporters/autoexport"
	"go.opentelemetry.io/contrib/instrumentation/net/http/otelhttp"
	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/codes"
	"go.opentelemetry.io/otel/propagation"
	"go.opentelemetry.io/otel/sdk/log"
	"go.opentelemetry.io/otel/sdk/metric"
	"go.opentelemetry.io/otel/sdk/resource"
	"go.opentelemetry.io/otel/sdk/trace"
	semconv "go.opentelemetry.io/otel/semconv/v1.27.0"
)

// Flag represents a feature flag configuration
type Flag struct {
	State          string         `json:"state"`
	Variants       map[string]any `json:"variants"`
	DefaultVariant string         `json:"defaultVariant"`
	Targeting      map[string]any `json:"targeting,omitempty"`
}

// FlagFile represents the entire flagd.json structure
type FlagFile struct {
	Schema string          `json:"$schema"`
	Flags  map[string]Flag `json:"flags"`
}

// TargetingRequest represents the request body for targeting updates
type TargetingRequest struct {
	UserID  string `json:"userId"`
	Enabled bool   `json:"enabled"`
	FlagKey string `json:"flagKey"`
}

var (
	flagsFilePath string
	fileMutex     sync.Mutex

	featureClient *openfeature.Client
	tracer        = otel.Tracer("flags-api")
	logger        *slog.Logger
)

// initOtel initializes OpenTelemetry with autoexport for traces, metrics, and logs
func initOtel(ctx context.Context) (func(context.Context) error, error) {
	serviceName := os.Getenv("OTEL_SERVICE_NAME")
	if serviceName == "" {
		serviceName = "flags-api"
	}

	res, err := resource.New(ctx,
		resource.WithAttributes(semconv.ServiceName(serviceName)),
		resource.WithFromEnv(),
		resource.WithTelemetrySDK(),
		resource.WithHost(),
	)
	if err != nil {
		return nil, err
	}

	// Create span exporter using autoexport
	spanExporter, err := autoexport.NewSpanExporter(ctx)
	if err != nil {
		return nil, err
	}

	tracerProvider := trace.NewTracerProvider(
		trace.WithBatcher(spanExporter),
		trace.WithResource(res),
	)
	otel.SetTracerProvider(tracerProvider)

	// Create metric reader using autoexport
	metricReader, err := autoexport.NewMetricReader(ctx)
	if err != nil {
		return nil, err
	}

	meterProvider := metric.NewMeterProvider(
		metric.WithReader(metricReader),
		metric.WithResource(res),
	)
	otel.SetMeterProvider(meterProvider)

	// Create log exporter using autoexport
	logExporter, err := autoexport.NewLogExporter(ctx)
	if err != nil {
		return nil, err
	}

	loggerProvider := log.NewLoggerProvider(
		log.WithProcessor(log.NewBatchProcessor(logExporter)),
		log.WithResource(res),
	)

	// Initialize slog with OTEL handler
	logger = slog.New(otelslog.NewHandler(serviceName, otelslog.WithLoggerProvider(loggerProvider)))
	slog.SetDefault(logger)

	// Set global propagator
	otel.SetTextMapPropagator(propagation.NewCompositeTextMapPropagator(
		propagation.TraceContext{},
		propagation.Baggage{},
	))

	return func(ctx context.Context) error {
		err := tracerProvider.Shutdown(ctx)
		if err2 := meterProvider.Shutdown(ctx); err2 != nil && err == nil {
			err = err2
		}
		if err2 := loggerProvider.Shutdown(ctx); err2 != nil && err == nil {
			err = err2
		}
		return err
	}, nil
}

func init() {
	flagsFilePath = os.Getenv("FLAGS_FILE_PATH")
	if flagsFilePath == "" {
		flagsFilePath = "../Garage.AppHost/flags/flagd.json"
	}
}

// initOpenFeature initializes the OpenFeature client with OFREP provider
func initOpenFeature() error {
	ofrepEndpoint := os.Getenv("OFREP_ENDPOINT")
	if ofrepEndpoint == "" {
		return errors.New("OFREP_ENDPOINT environment variable is not set")
	}

	// Create OFREP provider
	ofrepProvider := ofrep.NewProvider(ofrepEndpoint)

	// Register the provider
	if err := openfeature.SetProviderAndWait(ofrepProvider); err != nil {
		return err
	}

	// Create a client
	featureClient = openfeature.NewDefaultClient()

	return nil
}

// readFlagsFile reads and parses the flagd.json file
func readFlagsFile(ctx context.Context) (*FlagFile, error) {
	ctx, span := tracer.Start(ctx, "readFlagsFile")
	defer span.End()

	data, err := os.ReadFile(flagsFilePath)
	if err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		return nil, err
	}

	var flagFile FlagFile
	if err := json.Unmarshal(data, &flagFile); err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		return nil, err
	}

	return &flagFile, nil
}

// writeFlagsFile writes the flag configuration back to flagd.json
func writeFlagsFile(ctx context.Context, flagFile *FlagFile) error {
	ctx, span := tracer.Start(ctx, "writeFlagsFile")
	defer span.End()
	_ = ctx // ctx available for future use

	data, err := json.MarshalIndent(flagFile, "", "  ")
	if err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		return err
	}

	if err := os.WriteFile(flagsFilePath, data, 0600); err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		return err
	}

	return nil
}

// getUserIDsFromTargeting extracts the userIds array from the targeting rule
func getUserIDsFromTargeting(targeting map[string]any) ([]string, error) {
	ifRule, ok := targeting["if"].([]any)
	if !ok || len(ifRule) < 2 {
		return nil, errors.New("invalid targeting structure: missing 'if' rule")
	}

	inRule, ok := ifRule[0].(map[string]any)
	if !ok {
		return nil, errors.New("invalid targeting structure: missing condition")
	}

	inArray, ok := inRule["in"].([]any)
	if !ok || len(inArray) < 2 {
		return nil, errors.New("invalid targeting structure: missing 'in' rule")
	}

	userIDsRaw, ok := inArray[1].([]any)
	if !ok {
		return nil, errors.New("invalid targeting structure: userIds is not an array")
	}

	userIDs := make([]string, 0, len(userIDsRaw))
	for _, id := range userIDsRaw {
		if strID, ok := id.(string); ok {
			userIDs = append(userIDs, strID)
		}
	}

	return userIDs, nil
}

// setUserIDsInTargeting updates the userIds array in the targeting rule
func setUserIDsInTargeting(targeting map[string]any, userIDs []string) error {
	ifRule, ok := targeting["if"].([]any)
	if !ok || len(ifRule) < 2 {
		return errors.New("invalid targeting structure")
	}

	inRule, ok := ifRule[0].(map[string]any)
	if !ok {
		return errors.New("invalid targeting structure")
	}

	inArray, ok := inRule["in"].([]any)
	if !ok || len(inArray) < 2 {
		return errors.New("invalid targeting structure")
	}

	// Convert []string to []any for JSON compatibility
	userIDsAny := make([]any, len(userIDs))
	for i, id := range userIDs {
		userIDsAny[i] = id
	}

	inArray[1] = userIDsAny
	return nil
}

// isPreviewModeEnabled checks if the enable-preview-mode flag is enabled
// Returns the list of allowed flag keys (comma-separated) or empty if disabled
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

// handleGetFlags handles GET /flags/ - returns current flag states for a user
func handleGetFlags(w http.ResponseWriter, r *http.Request) {
	ctx, span := tracer.Start(r.Context(), "handleGetFlags")
	defer span.End()

	userID := r.URL.Query().Get("userId")
	if userID == "" {
		http.Error(w, "userId query parameter is required", http.StatusBadRequest)
		return
	}

	fileMutex.Lock()
	defer fileMutex.Unlock()

	// Get the list of editable flags from enable-preview-mode
	flagList := getPreviewModeFlags(ctx)

	flagFile, err := readFlagsFile(ctx)
	if err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		http.Error(w, "Failed to read flags", http.StatusInternalServerError)
		return
	}

	flagStates := make(map[string]bool)

	// Check if flags are enabled for this user
	for _, flagKey := range flagList {
		flag, ok := flagFile.Flags[flagKey]

		if ok && flag.Targeting != nil {
			userIDs, err := getUserIDsFromTargeting(flag.Targeting)
			if err == nil {
				enabled := slices.Contains(userIDs, userID)

				// collect flag states
				flagStates[flagKey] = enabled
			}
		}
	}

	w.Header().Set("Content-Type", "application/json")
	if err := json.NewEncoder(w).Encode(flagStates); err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		slog.ErrorContext(ctx, "failed to encode flagStates response", "error", err)
		http.Error(w, "Failed to encode response", http.StatusInternalServerError)
		return
	}
}

// handleUpdateFlagTargeting handles POST /flags/ - updates targeting for allowed flags
func handleUpdateFlagTargeting(w http.ResponseWriter, r *http.Request) {
	ctx, span := tracer.Start(r.Context(), "handleUpdateFlagTargeting")
	defer span.End()

	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	// Get the list of allowed flags from enable-preview-mode
	allowedFlags := getPreviewModeFlags(ctx)
	if len(allowedFlags) == 0 {
		http.Error(w, "Flag updates are disabled: enable-preview-mode is empty or off", http.StatusForbidden)
		return
	}

	var req TargetingRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		http.Error(w, "Invalid request body", http.StatusBadRequest)
		return
	}

	if req.UserID == "" {
		http.Error(w, "userId is required", http.StatusBadRequest)
		return
	}

	if req.FlagKey == "" {
		http.Error(w, "flagKey is required", http.StatusBadRequest)
		return
	}

	// Check if the requested flag is in the allowed list
	if !slices.Contains(allowedFlags, req.FlagKey) {
		http.Error(w, "Flag is not allowed for updates", http.StatusForbidden)
		return
	}

	fileMutex.Lock()
	defer fileMutex.Unlock()

	flagFile, err := readFlagsFile(ctx)
	if err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		http.Error(w, "Failed to read flags", http.StatusInternalServerError)
		return
	}

	flag, ok := flagFile.Flags[req.FlagKey]
	if !ok {
		http.Error(w, "Flag not found", http.StatusNotFound)
		return
	}

	userIDs, err := getUserIDsFromTargeting(flag.Targeting)
	if err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		http.Error(w, "Failed to parse targeting", http.StatusInternalServerError)
		return
	}

	if req.Enabled {
		// Add userId if not already present
		found := slices.Contains(userIDs, req.UserID)
		if !found {
			userIDs = append(userIDs, req.UserID)
		}
	} else {
		// Remove userId if present
		newUserIDs := make([]string, 0, len(userIDs))
		for _, id := range userIDs {
			if id != req.UserID {
				newUserIDs = append(newUserIDs, id)
			}
		}
		userIDs = newUserIDs
	}

	if err := setUserIDsInTargeting(flag.Targeting, userIDs); err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		http.Error(w, "Failed to update targeting", http.StatusInternalServerError)
		return
	}

	flagFile.Flags[req.FlagKey] = flag

	if err := writeFlagsFile(ctx, flagFile); err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		http.Error(w, "Failed to write flags", http.StatusInternalServerError)
		return
	}

	slog.InfoContext(ctx, "Flag targeting updated", "flagKey", req.FlagKey, "userId", req.UserID, "enabled", req.Enabled)

	w.Header().Set("Content-Type", "application/json")
	if err := json.NewEncoder(w).Encode(map[string]any{
		"success": true,
		"userId":  req.UserID,
		"enabled": req.Enabled,
		"userIds": userIDs,
	}); err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		slog.ErrorContext(ctx, "failed to encode response", "error", err)
		http.Error(w, "Failed to encode response", http.StatusInternalServerError)
		return
	}
}

func main() {
	ctx := context.Background()

	// Initialize OpenTelemetry
	shutdown, err := initOtel(ctx)
	if err != nil {
		slog.Warn("Failed to initialize OpenTelemetry", "error", err)
	} else {
		defer func() {
			if err := shutdown(ctx); err != nil {
				slog.Error("Error shutting down telemetry", "error", err)
			}
		}()
		slog.Info("OpenTelemetry initialized successfully")
	}

	port := os.Getenv("PORT")
	if port == "" {
		port = "8080"
	}

	// Initialize OpenFeature with OFREP provider
	if err := initOpenFeature(); err != nil {
		slog.Warn("Failed to initialize OpenFeature", "error", err)
		slog.Info("Flag updates require preview mode to be configured")
	} else {
		slog.Info("OpenFeature initialized successfully with OFREP provider")
	}

	mux := http.NewServeMux()

	mux.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
		w.Write([]byte("Hello from Go Feature Flags API!"))
	})

	mux.HandleFunc("/flags/", func(w http.ResponseWriter, r *http.Request) {
		switch r.Method {
		case http.MethodGet:
			handleGetFlags(w, r)
		case http.MethodPost:
			handleUpdateFlagTargeting(w, r)
		default:
			http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		}
	})

	handler := otelhttp.NewHandler(mux, "flags-api")

	slog.Info("Server listening", "port", port, "flagsFilePath", flagsFilePath)
	if err := http.ListenAndServe(":"+port, handler); err != nil {
		slog.Error("HTTP server failed", "error", err)
		os.Exit(1)
	}
}
