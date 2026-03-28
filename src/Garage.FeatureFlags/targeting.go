package main

import "errors"

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
