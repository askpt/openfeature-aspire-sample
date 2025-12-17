package main

import (
	"context"
	"encoding/json"
	"fmt"
	"net/http"
	"os"
	"slices"
	"sync"

	"github.com/open-feature/go-sdk-contrib/providers/ofrep"
	"github.com/open-feature/go-sdk/openfeature"
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
)

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
		return fmt.Errorf("OFREP_ENDPOINT environment variable is not set")
	}

	// Create OFREP provider
	ofrepProvider := ofrep.NewProvider(ofrepEndpoint)

	// Register the provider
	if err := openfeature.SetProviderAndWait(ofrepProvider); err != nil {
		return fmt.Errorf("failed to set OpenFeature provider: %w", err)
	}

	// Create a client
	featureClient = openfeature.NewDefaultClient()

	return nil
}

// readFlagsFile reads and parses the flagd.json file
func readFlagsFile() (*FlagFile, error) {
	data, err := os.ReadFile(flagsFilePath)
	if err != nil {
		return nil, fmt.Errorf("failed to read flags file: %w", err)
	}

	var flagFile FlagFile
	if err := json.Unmarshal(data, &flagFile); err != nil {
		return nil, fmt.Errorf("failed to parse flags file: %w", err)
	}

	return &flagFile, nil
}

// writeFlagsFile writes the flag configuration back to flagd.json
func writeFlagsFile(flagFile *FlagFile) error {
	data, err := json.MarshalIndent(flagFile, "", "  ")
	if err != nil {
		return fmt.Errorf("failed to marshal flags: %w", err)
	}

	if err := os.WriteFile(flagsFilePath, data, 0644); err != nil {
		return fmt.Errorf("failed to write flags file: %w", err)
	}

	return nil
}

// getUserIDsFromTargeting extracts the userIds array from the targeting rule
func getUserIDsFromTargeting(targeting map[string]any) ([]string, error) {
	ifRule, ok := targeting["if"].([]any)
	if !ok || len(ifRule) < 2 {
		return nil, fmt.Errorf("invalid targeting structure: missing 'if' rule")
	}

	inRule, ok := ifRule[0].(map[string]any)
	if !ok {
		return nil, fmt.Errorf("invalid targeting structure: missing condition")
	}

	inArray, ok := inRule["in"].([]any)
	if !ok || len(inArray) < 2 {
		return nil, fmt.Errorf("invalid targeting structure: missing 'in' rule")
	}

	userIDsRaw, ok := inArray[1].([]any)
	if !ok {
		return nil, fmt.Errorf("invalid targeting structure: userIds is not an array")
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
		return fmt.Errorf("invalid targeting structure")
	}

	inRule, ok := ifRule[0].(map[string]any)
	if !ok {
		return fmt.Errorf("invalid targeting structure")
	}

	inArray, ok := inRule["in"].([]any)
	if !ok || len(inArray) < 2 {
		return fmt.Errorf("invalid targeting structure")
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
func isPreviewModeEnabled(ctx context.Context) bool {
	if featureClient == nil {
		// If OpenFeature is not initialized, allow updates (fallback)
		fmt.Println("Warning: OpenFeature client not initialized, allowing update")
		return true
	}

	value, err := featureClient.BooleanValue(ctx, "enable-preview-mode", false, openfeature.EvaluationContext{})
	if err != nil {
		fmt.Printf("Warning: Failed to evaluate enable-preview-mode flag: %v\n", err)
		return false
	}

	return value
}

// handleGetFlags handles GET /flags/ - returns current flag states for a user
func handleGetFlags(w http.ResponseWriter, r *http.Request) {
	userID := r.URL.Query().Get("userId")
	if userID == "" {
		http.Error(w, "userId query parameter is required", http.StatusBadRequest)
		return
	}

	// list of flags for the user
	flagList := []string{"enable-demo"}

	fileMutex.Lock()
	defer fileMutex.Unlock()

	flagFile, err := readFlagsFile()
	if err != nil {
		http.Error(w, fmt.Sprintf("Failed to read flags: %v", err), http.StatusInternalServerError)
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
	json.NewEncoder(w).Encode(flagStates)
}

// handleEnableDemoTargeting handles POST /flags/
func handleEnableDemoTargeting(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	// Check if preview mode is enabled before allowing updates
	if !isPreviewModeEnabled(r.Context()) {
		http.Error(w, "Flag updates are disabled: enable-preview-mode is off", http.StatusForbidden)
		return
	}

	var req TargetingRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		http.Error(w, fmt.Sprintf("Invalid request body: %v", err), http.StatusBadRequest)
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

	fileMutex.Lock()
	defer fileMutex.Unlock()

	flagFile, err := readFlagsFile()
	if err != nil {
		http.Error(w, fmt.Sprintf("Failed to read flags: %v", err), http.StatusInternalServerError)
		return
	}

	flag, ok := flagFile.Flags[req.FlagKey]
	if !ok {
		http.Error(w, fmt.Sprintf("Flag '%s' not found", req.FlagKey), http.StatusNotFound)
		return
	}

	userIDs, err := getUserIDsFromTargeting(flag.Targeting)
	if err != nil {
		http.Error(w, fmt.Sprintf("Failed to parse targeting: %v", err), http.StatusInternalServerError)
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
		http.Error(w, fmt.Sprintf("Failed to update targeting: %v", err), http.StatusInternalServerError)
		return
	}

	flagFile.Flags["enable-demo"] = flag

	if err := writeFlagsFile(flagFile); err != nil {
		http.Error(w, fmt.Sprintf("Failed to write flags: %v", err), http.StatusInternalServerError)
		return
	}

	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(map[string]any{
		"success": true,
		"userId":  req.UserID,
		"enabled": req.Enabled,
		"userIds": userIDs,
	})
}

func main() {
	port := os.Getenv("PORT")
	if port == "" {
		port = "8080"
	}

	// Initialize OpenFeature with OFREP provider
	if err := initOpenFeature(); err != nil {
		fmt.Printf("Warning: Failed to initialize OpenFeature: %v\n", err)
		fmt.Println("Flag updates will be allowed without preview mode check")
	} else {
		fmt.Println("OpenFeature initialized successfully with OFREP provider")
	}

	http.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
		fmt.Fprintf(w, "Hello from Go Feature Flags API!")
	})

	http.HandleFunc("/flags/", func(w http.ResponseWriter, r *http.Request) {
		switch r.Method {
		case http.MethodGet:
			handleGetFlags(w, r)
		case http.MethodPost:
			handleEnableDemoTargeting(w, r)
		default:
			http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		}
	})

	fmt.Printf("Server listening on port %s\n", port)
	fmt.Printf("Flags file path: %s\n", flagsFilePath)
	http.ListenAndServe(":"+port, nil)
}
