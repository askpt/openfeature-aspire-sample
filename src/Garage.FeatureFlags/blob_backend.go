package main

import (
	"context"
	"io"
	"os"

	"gocloud.dev/blob"
	_ "gocloud.dev/blob/azureblob"
)

// flagsBackend abstracts read/write access to the flagd.json configuration.
type flagsBackend interface {
	Read(ctx context.Context) ([]byte, error)
	Write(ctx context.Context, data []byte) error
}

// fileBackend reads/writes flagd.json from the local filesystem.
type fileBackend struct {
	path string
}

func (fb *fileBackend) Read(_ context.Context) ([]byte, error) {
	return os.ReadFile(fb.path)
}

func (fb *fileBackend) Write(_ context.Context, data []byte) error {
	return os.WriteFile(fb.path, data, 0o600)
}

// blobBackend reads/writes flagd.json from a cloud blob store (e.g. Azure Blob Storage).
type blobBackend struct {
	bucketURL string
	blobName  string
}

func (bb *blobBackend) Read(ctx context.Context) ([]byte, error) {
	bucket, err := blob.OpenBucket(ctx, bb.bucketURL)
	if err != nil {
		return nil, err
	}
	defer bucket.Close()

	reader, err := bucket.NewReader(ctx, bb.blobName, nil)
	if err != nil {
		return nil, err
	}
	defer reader.Close()

	return io.ReadAll(reader)
}

func (bb *blobBackend) Write(ctx context.Context, data []byte) error {
	bucket, err := blob.OpenBucket(ctx, bb.bucketURL)
	if err != nil {
		return err
	}
	defer bucket.Close()

	writer, err := bucket.NewWriter(ctx, bb.blobName, nil)
	if err != nil {
		return err
	}

	if _, err := writer.Write(data); err != nil {
		_ = writer.Close()
		return err
	}

	return writer.Close()
}

// newBackend returns the appropriate backend based on environment variables.
// If FLAGS_BLOB_CONTAINER is set, it uses Azure Blob Storage via gocloud.dev;
// otherwise it falls back to the local filesystem.
func newBackend() flagsBackend {
	container := os.Getenv("FLAGS_BLOB_CONTAINER")
	if container != "" {
		blobName := os.Getenv("FLAGS_BLOB_NAME")
		if blobName == "" {
			blobName = "flagd.json"
		}
		return &blobBackend{
			bucketURL: "azblob://" + container,
			blobName:  blobName,
		}
	}

	return &fileBackend{path: flagsFilePath}
}
