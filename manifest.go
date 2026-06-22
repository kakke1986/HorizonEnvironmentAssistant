package main

import (
	"encoding/json"
	"os"
	"time"
)

func loadPackageManifest(logger *Logger) *PackageManifest {
	file, err := os.Open(manifestPath())
	if err != nil {
		return nil
	}
	defer file.Close()

	var manifest PackageManifest
	if err := json.NewDecoder(file).Decode(&manifest); err != nil {
		if logger != nil {
			logger.Printf("读取 packages-manifest.json 失败：%v", err)
		}
		return nil
	}
	return &manifest
}

func savePackageManifest(packages []RemotePackageInfo) error {
	entries := make([]PackageManifestEntry, 0, len(packages))
	for _, packageInfo := range packages {
		entries = append(entries, PackageManifestEntry{
			TargetFileName: packageInfo.TargetFileName,
			SourceFileName: packageInfo.SourceFileName,
			SHA1:           packageInfo.SHA1,
			Size:           packageInfo.Size,
		})
	}

	manifest := PackageManifest{
		DownloadedAtUtc: time.Now().UTC().Format(time.RFC3339),
		Packages:        entries,
	}

	if err := os.MkdirAll(offlinePackagesDir(), 0755); err != nil {
		return err
	}

	tempPath := manifestPath() + ".tmp"
	file, err := os.Create(tempPath)
	if err != nil {
		return err
	}
	encoder := json.NewEncoder(file)
	encoder.SetIndent("", "  ")
	encodeErr := encoder.Encode(manifest)
	closeErr := file.Close()
	if encodeErr != nil {
		_ = os.Remove(tempPath)
		return encodeErr
	}
	if closeErr != nil {
		_ = os.Remove(tempPath)
		return closeErr
	}
	_ = os.Remove(manifestPath())
	return os.Rename(tempPath, manifestPath())
}

func packageMatchesManifest(entry PackageManifestEntry, packageInfo RemotePackageInfo) bool {
	return equalFold(entry.SourceFileName, packageInfo.SourceFileName) &&
		equalFold(entry.SHA1, packageInfo.SHA1) &&
		entry.Size == packageInfo.Size
}

func equalFold(left string, right string) bool {
	if len(left) != len(right) {
		return false
	}
	for i := range left {
		l := left[i]
		r := right[i]
		if 'A' <= l && l <= 'Z' {
			l += 'a' - 'A'
		}
		if 'A' <= r && r <= 'Z' {
			r += 'a' - 'A'
		}
		if l != r {
			return false
		}
	}
	return true
}
