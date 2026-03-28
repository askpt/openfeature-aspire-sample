package main

import (
	"encoding/json"
	"log/slog"
	"net/http"
	"slices"

	"go.opentelemetry.io/otel/codes"
)

// TargetingRequest represents the request body for targeting updates
type TargetingRequest struct {
	Enabled bool   `json:"enabled"`
	FlagKey string `json:"flagKey"`
}

// handleGetFlags handles GET /flags/{userId} - returns current flag states for a user
func handleGetFlags(w http.ResponseWriter, r *http.Request) {
	ctx, span := tracer.Start(r.Context(), "handleGetFlags")
	defer span.End()

	userID := r.PathValue("userId")
	if userID == "" {
		http.Error(w, "userId path parameter is required", http.StatusBadRequest)
		return
	}

	// Evaluate the feature flag before acquiring the file lock: getPreviewModeFlags
	// makes an OFREP network call and does not touch the flags file, so holding the
	// read-lock during that call would unnecessarily delay concurrent writes.
	flagList := getPreviewModeFlags(ctx)

	fileMutex.RLock()
	defer fileMutex.RUnlock()

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

	data, err := json.Marshal(flagStates)
	if err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		slog.ErrorContext(ctx, "failed to encode flagStates response", "error", err)
		http.Error(w, "Failed to encode response", http.StatusInternalServerError)
		return
	}
	w.Header().Set("Content-Type", "application/json")
	if _, err := w.Write(data); err != nil {
		slog.ErrorContext(ctx, "failed to write flagStates response", "error", err)
	}
}

// handleUpdateFlagTargeting handles POST /flags/{userId} - updates targeting for allowed flags
func handleUpdateFlagTargeting(w http.ResponseWriter, r *http.Request) {
	ctx, span := tracer.Start(r.Context(), "handleUpdateFlagTargeting")
	defer span.End()

	// Get the list of allowed flags from enable-preview-mode
	allowedFlags := getPreviewModeFlags(ctx)
	if len(allowedFlags) == 0 {
		http.Error(w, "Flag updates are disabled: enable-preview-mode is empty or off", http.StatusForbidden)
		return
	}

	userID := r.PathValue("userId")
	if userID == "" {
		http.Error(w, "userId is required", http.StatusBadRequest)
		return
	}

	var req TargetingRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		http.Error(w, "Invalid request body", http.StatusBadRequest)
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
		found := slices.Contains(userIDs, userID)
		if !found {
			userIDs = append(userIDs, userID)
		}
	} else {
		// Remove userId if present
		userIDs = slices.DeleteFunc(userIDs, func(id string) bool {
			return id == userID
		})
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

	slog.InfoContext(ctx, "Flag targeting updated", "flagKey", req.FlagKey, "userId", userID, "enabled", req.Enabled)

	respBody := map[string]any{
		"success": true,
		"userId":  userID,
		"enabled": req.Enabled,
		"userIds": userIDs,
	}
	data, err := json.Marshal(respBody)
	if err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		slog.ErrorContext(ctx, "failed to encode response", "error", err)
		http.Error(w, "Failed to encode response", http.StatusInternalServerError)
		return
	}
	w.Header().Set("Content-Type", "application/json")
	if _, err := w.Write(data); err != nil {
		slog.ErrorContext(ctx, "failed to write response", "error", err)
	}
}
