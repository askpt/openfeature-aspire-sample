package main

import (
	"slices"
	"testing"
)

// makeTargeting constructs a targeting map matching the flagd JSON structure:
//
//	{ "if": [ { "in": [ <context-key>, <userIds> ] }, "on", "off" ] }
func makeTargeting(userIDs []any) map[string]any {
	return map[string]any{
		"if": []any{
			map[string]any{
				"in": []any{"$targeting_key", userIDs},
			},
			"on",
			"off",
		},
	}
}

func TestGetUserIDsFromTargeting(t *testing.T) {
	t.Run("returns user IDs from valid targeting", func(t *testing.T) {
		targeting := makeTargeting([]any{"alice", "bob"})
		got, err := getUserIDsFromTargeting(targeting)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if !slices.Equal(got, []string{"alice", "bob"}) {
			t.Errorf("got %v, want [alice bob]", got)
		}
	})

	t.Run("returns empty slice for empty userIds array", func(t *testing.T) {
		targeting := makeTargeting([]any{})
		got, err := getUserIDsFromTargeting(targeting)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if len(got) != 0 {
			t.Errorf("got %v, want []", got)
		}
	})

	t.Run("skips non-string entries", func(t *testing.T) {
		targeting := makeTargeting([]any{"alice", 42, true, "bob"})
		got, err := getUserIDsFromTargeting(targeting)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if !slices.Equal(got, []string{"alice", "bob"}) {
			t.Errorf("got %v, want [alice bob]", got)
		}
	})

	t.Run("error when targeting is nil", func(t *testing.T) {
		_, err := getUserIDsFromTargeting(nil)
		if err == nil {
			t.Error("expected error for nil targeting, got nil")
		}
	})

	t.Run("error when if rule is missing", func(t *testing.T) {
		targeting := map[string]any{"other": "value"}
		_, err := getUserIDsFromTargeting(targeting)
		if err == nil {
			t.Error("expected error when 'if' key is missing, got nil")
		}
	})

	t.Run("error when if rule is too short", func(t *testing.T) {
		targeting := map[string]any{
			"if": []any{},
		}
		_, err := getUserIDsFromTargeting(targeting)
		if err == nil {
			t.Error("expected error for empty 'if' rule, got nil")
		}
	})

	t.Run("error when in rule is missing", func(t *testing.T) {
		targeting := map[string]any{
			"if": []any{
				map[string]any{"other": "value"},
				"on",
			},
		}
		_, err := getUserIDsFromTargeting(targeting)
		if err == nil {
			t.Error("expected error when 'in' key is missing, got nil")
		}
	})

	t.Run("error when userIds is not an array", func(t *testing.T) {
		targeting := map[string]any{
			"if": []any{
				map[string]any{
					"in": []any{"$targeting_key", "not-an-array"},
				},
				"on",
			},
		}
		_, err := getUserIDsFromTargeting(targeting)
		if err == nil {
			t.Error("expected error when userIds is not an array, got nil")
		}
	})
}

func TestSetUserIDsInTargeting(t *testing.T) {
	t.Run("sets user IDs in valid targeting", func(t *testing.T) {
		targeting := makeTargeting([]any{"alice"})
		err := setUserIDsInTargeting(targeting, []string{"alice", "bob", "carol"})
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		got, err := getUserIDsFromTargeting(targeting)
		if err != nil {
			t.Fatalf("read back failed: %v", err)
		}
		if !slices.Equal(got, []string{"alice", "bob", "carol"}) {
			t.Errorf("got %v, want [alice bob carol]", got)
		}
	})

	t.Run("clears user IDs with empty slice", func(t *testing.T) {
		targeting := makeTargeting([]any{"alice", "bob"})
		if err := setUserIDsInTargeting(targeting, []string{}); err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		got, err := getUserIDsFromTargeting(targeting)
		if err != nil {
			t.Fatalf("read back failed: %v", err)
		}
		if len(got) != 0 {
			t.Errorf("got %v, want []", got)
		}
	})

	t.Run("replaces existing user IDs", func(t *testing.T) {
		targeting := makeTargeting([]any{"old1", "old2"})
		if err := setUserIDsInTargeting(targeting, []string{"new1"}); err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		got, err := getUserIDsFromTargeting(targeting)
		if err != nil {
			t.Fatalf("read back failed: %v", err)
		}
		if !slices.Equal(got, []string{"new1"}) {
			t.Errorf("got %v, want [new1]", got)
		}
	})

	t.Run("error when targeting structure is invalid", func(t *testing.T) {
		targeting := map[string]any{"other": "value"}
		err := setUserIDsInTargeting(targeting, []string{"alice"})
		if err == nil {
			t.Error("expected error for invalid targeting, got nil")
		}
	})

	t.Run("roundtrip: set then get returns same IDs", func(t *testing.T) {
		targeting := makeTargeting([]any{})
		want := []string{"user1", "user2", "user3"}
		if err := setUserIDsInTargeting(targeting, want); err != nil {
			t.Fatalf("set failed: %v", err)
		}
		got, err := getUserIDsFromTargeting(targeting)
		if err != nil {
			t.Fatalf("get failed: %v", err)
		}
		if !slices.Equal(got, want) {
			t.Errorf("roundtrip: got %v, want %v", got, want)
		}
	})
}
