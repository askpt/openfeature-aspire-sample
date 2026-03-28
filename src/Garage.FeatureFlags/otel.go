package main

import (
	"cmp"
	"context"
	"log/slog"
	"os"

	"go.opentelemetry.io/contrib/bridges/otelslog"
	"go.opentelemetry.io/contrib/exporters/autoexport"
	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/propagation"
	"go.opentelemetry.io/otel/sdk/log"
	"go.opentelemetry.io/otel/sdk/metric"
	"go.opentelemetry.io/otel/sdk/resource"
	"go.opentelemetry.io/otel/sdk/trace"
	semconv "go.opentelemetry.io/otel/semconv/v1.27.0"
)

// initOtel initializes OpenTelemetry with autoexport for traces, metrics, and logs
func initOtel(ctx context.Context) (func(context.Context) error, error) {
	serviceName := cmp.Or(os.Getenv("OTEL_SERVICE_NAME"), "flagsapi")

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
	logger := slog.New(otelslog.NewHandler(serviceName, otelslog.WithLoggerProvider(loggerProvider)))
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
