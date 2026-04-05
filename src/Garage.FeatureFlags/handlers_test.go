package main

import (
	"bytes"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"testing"
)

func TestHandleGetFlags_EmptyUserID(t *testing.T) {
	req := httptest.NewRequest(http.MethodGet, "/flags/", nil)
	// userId path value is not set, so PathValue("userId") returns ""
	w := httptest.NewRecorder()

	handleGetFlags(w, req)

	if w.Code != http.StatusBadRequest {
		t.Errorf("status: got %d, want %d", w.Code, http.StatusBadRequest)
	}
}

func TestHandleGetFlags_MissingFlagsFile(t *testing.T) {
	setFlagsFilePath(t, filepath.Join(t.TempDir(), "nonexistent.json"))

	req := httptest.NewRequest(http.MethodGet, "/flags/user1", nil)
	req.SetPathValue("userId", "user1")
	w := httptest.NewRecorder()

	handleGetFlags(w, req)

	if w.Code != http.StatusInternalServerError {
		t.Errorf("status: got %d, want %d", w.Code, http.StatusInternalServerError)
	}
}

func TestHandleGetFlags_ContentTypeAndEmptyObject(t *testing.T) {
	fixture := FlagFile{
		Schema: "https://flagd.dev/schema/v0/flags.json",
		Flags:  map[string]Flag{},
	}
	setFlagsFilePath(t, writeTempFlagsFile(t, fixture))

	req := httptest.NewRequest(http.MethodGet, "/flags/user1", nil)
	req.SetPathValue("userId", "user1")
	w := httptest.NewRecorder()

	handleGetFlags(w, req)

	if w.Code != http.StatusOK {
		t.Errorf("status: got %d, want %d", w.Code, http.StatusOK)
	}
	if ct := w.Header().Get("Content-Type"); ct != "application/json" {
		t.Errorf("Content-Type: got %q, want %q", ct, "application/json")
	}
	var result map[string]bool
	if err := json.NewDecoder(w.Body).Decode(&result); err != nil {
		t.Fatalf("failed to decode response body: %v", err)
	}
	if len(result) != 0 {
		t.Errorf("expected empty object, got %v", result)
	}
}

func TestHandleUpdateFlagTargeting_PreviewModeDisabled(t *testing.T) {
	// featureClient is nil in tests → getPreviewModeFlags returns [] → 403
	req := httptest.NewRequest(http.MethodPost, "/flags/user1",
		bytes.NewReader([]byte(`{"flagKey":"test","enabled":true}`)))
	req.Header.Set("Content-Type", "application/json")
	req.SetPathValue("userId", "user1")
	w := httptest.NewRecorder()

	handleUpdateFlagTargeting(w, req)

	if w.Code != http.StatusForbidden {
		t.Errorf("status: got %d, want %d", w.Code, http.StatusForbidden)
	}
}

func TestHandleUpdateFlagTargeting_EmptyBody_PreviewModeDisabled(t *testing.T) {
	// Even with an empty body, preview mode check comes first → 403
	req := httptest.NewRequest(http.MethodPost, "/flags/user1", bytes.NewReader([]byte{}))
	req.SetPathValue("userId", "user1")
	w := httptest.NewRecorder()

	handleUpdateFlagTargeting(w, req)

	if w.Code != http.StatusForbidden {
		t.Errorf("status: got %d, want %d", w.Code, http.StatusForbidden)
	}
}
