package main

import (
	"context"
	"encoding/json"
	"os"
	"path/filepath"
	"testing"
)

// setFlagsFilePath temporarily overrides the global flagsFilePath and restores it on cleanup.
func setFlagsFilePath(t *testing.T, path string) {
	t.Helper()
	old := flagsFilePath
	flagsFilePath = path
	t.Cleanup(func() { flagsFilePath = old })
}

// writeTempFlagsFile creates a temporary flags JSON file and returns its path.
func writeTempFlagsFile(t *testing.T, content any) string {
	t.Helper()
	data, err := json.Marshal(content)
	if err != nil {
		t.Fatalf("failed to marshal test fixture: %v", err)
	}
	path := filepath.Join(t.TempDir(), "flagd.json")
	if err := os.WriteFile(path, data, 0o600); err != nil {
		t.Fatalf("failed to write test fixture: %v", err)
	}
	return path
}

func TestReadFlagsFile_ValidFile(t *testing.T) {
	fixture := FlagFile{
		Schema: "https://flagd.dev/schema/v0/flags.json",
		Flags: map[string]Flag{
			"test-flag": {
				State:          "ENABLED",
				DefaultVariant: "on",
				Variants:       map[string]any{"on": true, "off": false},
			},
		},
	}
	setFlagsFilePath(t, writeTempFlagsFile(t, fixture))

	got, err := readFlagsFile(context.Background())
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	if got.Schema != fixture.Schema {
		t.Errorf("schema: got %q, want %q", got.Schema, fixture.Schema)
	}
	if len(got.Flags) != 1 {
		t.Errorf("flags count: got %d, want 1", len(got.Flags))
	}
	if _, ok := got.Flags["test-flag"]; !ok {
		t.Error("test-flag: not found in flags map")
	}
}

func TestReadFlagsFile_EmptyFlags(t *testing.T) {
	fixture := FlagFile{
		Schema: "https://flagd.dev/schema/v0/flags.json",
		Flags:  map[string]Flag{},
	}
	setFlagsFilePath(t, writeTempFlagsFile(t, fixture))

	got, err := readFlagsFile(context.Background())
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if len(got.Flags) != 0 {
		t.Errorf("flags count: got %d, want 0", len(got.Flags))
	}
}

func TestReadFlagsFile_FileNotFound(t *testing.T) {
	setFlagsFilePath(t, filepath.Join(t.TempDir(), "nonexistent.json"))

	_, err := readFlagsFile(context.Background())
	if err == nil {
		t.Error("expected error for missing file, got nil")
	}
}

func TestReadFlagsFile_InvalidJSON(t *testing.T) {
	path := filepath.Join(t.TempDir(), "flagd.json")
	if err := os.WriteFile(path, []byte("{not-valid-json"), 0o600); err != nil {
		t.Fatalf("failed to write test fixture: %v", err)
	}
	setFlagsFilePath(t, path)

	_, err := readFlagsFile(context.Background())
	if err == nil {
		t.Error("expected error for invalid JSON, got nil")
	}
}

func TestWriteFlagsFile_Success(t *testing.T) {
	path := filepath.Join(t.TempDir(), "flagd.json")
	setFlagsFilePath(t, path)

	flagFile := &FlagFile{
		Schema: "https://flagd.dev/schema/v0/flags.json",
		Flags: map[string]Flag{
			"my-flag": {
				State:          "ENABLED",
				DefaultVariant: "off",
				Variants:       map[string]any{"on": true, "off": false},
			},
		},
	}

	if err := writeFlagsFile(context.Background(), flagFile); err != nil {
		t.Fatalf("unexpected write error: %v", err)
	}

	data, err := os.ReadFile(path)
	if err != nil {
		t.Fatalf("failed to read written file: %v", err)
	}

	var got FlagFile
	if err := json.Unmarshal(data, &got); err != nil {
		t.Fatalf("failed to unmarshal written content: %v", err)
	}

	if got.Schema != flagFile.Schema {
		t.Errorf("schema: got %q, want %q", got.Schema, flagFile.Schema)
	}
	if len(got.Flags) != 1 {
		t.Errorf("flags count: got %d, want 1", len(got.Flags))
	}
}

func TestWriteFlagsFile_ProducesValidJSON(t *testing.T) {
	path := filepath.Join(t.TempDir(), "flagd.json")
	setFlagsFilePath(t, path)

	flagFile := &FlagFile{
		Flags: map[string]Flag{
			"flag-with-targeting": {
				State:          "ENABLED",
				DefaultVariant: "off",
				Variants:       map[string]any{"on": true, "off": false},
				Targeting:      makeTargeting([]any{"alice", "bob"}),
			},
		},
	}

	if err := writeFlagsFile(context.Background(), flagFile); err != nil {
		t.Fatalf("unexpected write error: %v", err)
	}

	data, err := os.ReadFile(path)
	if err != nil {
		t.Fatalf("failed to read written file: %v", err)
	}

	// Verify the output is valid, indented JSON
	var raw map[string]any
	if err := json.Unmarshal(data, &raw); err != nil {
		t.Fatalf("written file is not valid JSON: %v", err)
	}
}

func TestReadWriteFlagsFile_Roundtrip(t *testing.T) {
	path := filepath.Join(t.TempDir(), "flagd.json")
	setFlagsFilePath(t, path)

	original := &FlagFile{
		Schema: "https://flagd.dev/schema/v0/flags.json",
		Flags: map[string]Flag{
			"feature-a": {
				State:          "ENABLED",
				DefaultVariant: "off",
				Variants:       map[string]any{"on": true, "off": false},
				Targeting:      makeTargeting([]any{"alice", "bob"}),
			},
			"feature-b": {
				State:          "DISABLED",
				DefaultVariant: "off",
				Variants:       map[string]any{"on": true, "off": false},
			},
		},
	}

	if err := writeFlagsFile(context.Background(), original); err != nil {
		t.Fatalf("write failed: %v", err)
	}

	got, err := readFlagsFile(context.Background())
	if err != nil {
		t.Fatalf("read failed: %v", err)
	}

	if len(got.Flags) != len(original.Flags) {
		t.Errorf("flags count: got %d, want %d", len(got.Flags), len(original.Flags))
	}

	for name, want := range original.Flags {
		gotFlag, ok := got.Flags[name]
		if !ok {
			t.Errorf("flag %q: not found in read result", name)
			continue
		}
		if gotFlag.State != want.State {
			t.Errorf("flag %q state: got %q, want %q", name, gotFlag.State, want.State)
		}
		if gotFlag.DefaultVariant != want.DefaultVariant {
			t.Errorf("flag %q defaultVariant: got %q, want %q", name, gotFlag.DefaultVariant, want.DefaultVariant)
		}
	}
}

func TestReadWriteFlagsFile_TargetingPreserved(t *testing.T) {
	path := filepath.Join(t.TempDir(), "flagd.json")
	setFlagsFilePath(t, path)

	original := &FlagFile{
		Flags: map[string]Flag{
			"chatbot": {
				State:          "ENABLED",
				DefaultVariant: "off",
				Variants:       map[string]any{"on": true, "off": false},
				Targeting:      makeTargeting([]any{"alice", "bob", "carol"}),
			},
		},
	}

	if err := writeFlagsFile(context.Background(), original); err != nil {
		t.Fatalf("write failed: %v", err)
	}

	got, err := readFlagsFile(context.Background())
	if err != nil {
		t.Fatalf("read failed: %v", err)
	}

	chatbot := got.Flags["chatbot"]
	if chatbot.Targeting == nil {
		t.Fatal("targeting: expected non-nil, got nil")
	}

	userIDs, err := getUserIDsFromTargeting(chatbot.Targeting)
	if err != nil {
		t.Fatalf("getUserIDsFromTargeting failed: %v", err)
	}

	want := []string{"alice", "bob", "carol"}
	if len(userIDs) != len(want) {
		t.Fatalf("userIDs count: got %d, want %d", len(userIDs), len(want))
	}
	for i, id := range want {
		if userIDs[i] != id {
			t.Errorf("userIDs[%d]: got %q, want %q", i, userIDs[i], id)
		}
	}
}
