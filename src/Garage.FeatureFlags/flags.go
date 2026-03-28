package main

import (
	"context"
	"encoding/json"
	"sync"

	"go.opentelemetry.io/otel/codes"
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

var (
	flagsFilePath string
	fileMutex     sync.RWMutex
	backend       flagsBackend
)

// readFlagsFile reads and parses the flagd.json configuration via the backend.
func readFlagsFile(ctx context.Context) (*FlagFile, error) {
	_, span := tracer.Start(ctx, "readFlagsFile")
	defer span.End()

	data, err := backend.Read(ctx)
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

// writeFlagsFile writes the flag configuration back via the backend.
func writeFlagsFile(ctx context.Context, flagFile *FlagFile) error {
	_, span := tracer.Start(ctx, "writeFlagsFile")
	defer span.End()

	data, err := json.MarshalIndent(flagFile, "", "  ")
	if err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		return err
	}

	if err := backend.Write(ctx, data); err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		return err
	}

	return nil
}
