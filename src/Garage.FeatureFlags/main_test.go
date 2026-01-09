package main

import (
	"bytes"
	"context"
	"io"
	"net/http"
	"testing"

	"golang.org/x/sync/errgroup"
)

func TestRun(t *testing.T) {
	const (
		port    = "34387"
		baseURL = "http://localhost:" + port
	)
	t.Setenv("PORT", port)
	t.Setenv("OTEL_SDK_DISABLED", "true")
	ctx, cancel := context.WithCancel(t.Context())
	t.Cleanup(cancel)

	eg, ctx := errgroup.WithContext(ctx)

	eg.Go(func() error {
		return run(ctx)
	})

	t.Run("homepage", func(t *testing.T) {
		const expectedBody = "Hello from Go Feature Flags API!"
		code, body := fetch(t, baseURL, http.MethodGet, nil)

		if code != http.StatusOK {
			t.Errorf("status code: got %d, want %d", code, http.StatusOK)
		}

		if body != expectedBody {
			t.Errorf("response body: got %q, want %q", body, expectedBody)
		}
	})

	t.Run("get flags", func(t *testing.T) {
		const expectedBody = "{}\n"
		code, body := fetch(t, baseURL+"/flags/user1", http.MethodGet, nil)

		if code != http.StatusOK {
			t.Errorf("status code: got %d, want %d", code, http.StatusOK)
		}

		if body != expectedBody {
			t.Errorf("response body: got %q, want %q", body, expectedBody)
		}
	})

	t.Run("set flags", func(t *testing.T) {
		const expectedBody = "Flag updates are disabled: enable-preview-mode is empty or off\n"
		code, body := fetch(t, baseURL+"/flags/user1", http.MethodPost, bytes.NewReader([]byte(``)))

		if code != http.StatusForbidden {
			t.Errorf("status code: got %d, want %d", code, http.StatusForbidden)
		}

		if body != expectedBody {
			t.Errorf("response body: got %q, want %q", body, expectedBody)
		}
	})

	cancel()
	err := eg.Wait()
	if err != nil {
		t.Errorf("no error wanted, got: %v ", err)
	}
}

func fetch(t *testing.T, url string, method string, body io.Reader) (int, string) {
	t.Helper()

	req, err := http.NewRequestWithContext(t.Context(), method, url, body)
	if err != nil {
		t.Fatalf("creating request: %v", err)
	}

	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		t.Fatalf("making request: %v", err)
	}

	data, err := io.ReadAll(resp.Body)
	if err != nil {
		t.Fatalf("reading response body: %v", err)
	}

	if err != resp.Body.Close() {
		t.Fatalf("making request: %v", err)
	}

	return resp.StatusCode, string(data)
}
