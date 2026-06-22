package main

import (
	"context"
	"crypto/sha1"
	"encoding/hex"
	"fmt"
	"html"
	"io"
	"net/http"
	"net/url"
	"os"
	"path/filepath"
	"regexp"
	"sort"
	"strconv"
	"strings"
	"time"
)

const (
	parserEndpoint                = "https://store.rg-adguard.net/api/GetFiles"
	gamingServicesProductID       = "9MWPM2CQNLHN"
	xboxIdentityProviderProductID = "9WZDNCRD1HKW"
	maxDownloadAttempts           = 5
)

type storeFileEntry struct {
	FileName    string
	DownloadURL string
	SHA1        string
	Size        string
}

type downloadProgress struct {
	Received int64
	Total    int64
}

func resolveRequiredPackages(ctx context.Context, logger *Logger) ([]RemotePackageInfo, error) {
	logger.Write("开始连接离线包解析服务。")
	gamingEntries, err := resolveProductEntries(ctx, gamingServicesProductID)
	if err != nil {
		return nil, err
	}
	xboxIdentityEntries, err := resolveProductEntries(ctx, xboxIdentityProviderProductID)
	if err != nil {
		return nil, err
	}

	packages := []RemotePackageInfo{
		selectNewest(gamingEntries, "Microsoft.VCLibs.x64.appx", func(name string) bool {
			lower := strings.ToLower(name)
			return strings.HasPrefix(lower, "microsoft.vclibs.140.00_") &&
				strings.Contains(lower, "_x64__") &&
				strings.HasSuffix(lower, ".appx")
		}),
		selectNewest(gamingEntries, "Microsoft.NET.Native.Framework.x64.appx", func(name string) bool {
			lower := strings.ToLower(name)
			return strings.HasPrefix(lower, "microsoft.net.native.framework.2.2_") &&
				strings.Contains(lower, "_x64__") &&
				strings.HasSuffix(lower, ".appx")
		}),
		selectNewest(gamingEntries, "Microsoft.NET.Native.Runtime.x64.appx", func(name string) bool {
			lower := strings.ToLower(name)
			return strings.HasPrefix(lower, "microsoft.net.native.runtime.2.2_") &&
				strings.Contains(lower, "_x64__") &&
				strings.HasSuffix(lower, ".appx")
		}),
		selectNewest(xboxIdentityEntries, "XboxIdentityProvider.appxbundle", func(name string) bool {
			lower := strings.ToLower(name)
			return strings.HasPrefix(lower, "microsoft.xboxidentityprovider_") &&
				strings.HasSuffix(lower, ".appxbundle")
		}),
		selectNewest(gamingEntries, "GamingServices.msixbundle", func(name string) bool {
			lower := strings.ToLower(name)
			return strings.HasPrefix(lower, "microsoft.gamingservices_") &&
				(strings.HasSuffix(lower, ".appxbundle") || strings.HasSuffix(lower, ".msixbundle"))
		}),
	}

	for _, packageInfo := range packages {
		if packageInfo.SourceFileName == "" {
			return nil, fmt.Errorf("未找到所需商店包：%s", packageInfo.TargetFileName)
		}
	}
	return packages, nil
}

func resolveProductEntries(ctx context.Context, productID string) ([]storeFileEntry, error) {
	form := url.Values{}
	form.Set("type", "ProductId")
	form.Set("url", productID)
	form.Set("ring", "Retail")
	form.Set("lang", "en-US")

	request, err := http.NewRequestWithContext(ctx, http.MethodPost, parserEndpoint, strings.NewReader(form.Encode()))
	if err != nil {
		return nil, err
	}
	request.Header.Set("Content-Type", "application/x-www-form-urlencoded")
	request.Header.Set("Referer", "https://store.rg-adguard.net/")
	request.Header.Set("User-Agent", "Mozilla/5.0 HorizonEnvironmentAssistant/1.0")

	client := &http.Client{Timeout: 20 * time.Minute}
	response, err := client.Do(request)
	if err != nil {
		return nil, err
	}
	defer response.Body.Close()

	if response.StatusCode < 200 || response.StatusCode >= 300 {
		return nil, fmt.Errorf("解析产品 %s 失败：HTTP %d", productID, response.StatusCode)
	}

	body, err := io.ReadAll(response.Body)
	if err != nil {
		return nil, err
	}

	entries := parseStoreEntries(string(body))
	if len(entries) == 0 {
		return nil, fmt.Errorf("未解析到产品 %s 的商店文件", productID)
	}
	return entries, nil
}

func parseStoreEntries(markup string) []storeFileEntry {
	rowRegex := regexp.MustCompile(`(?is)<tr[^>]*>(.*?)</tr>`)
	linkRegex := regexp.MustCompile(`(?is)<a\s+href="([^"]+)"[^>]*>([^<]+)</a>`)
	cellRegex := regexp.MustCompile(`(?is)<td[^>]*>(.*?)</td>`)
	tagRegex := regexp.MustCompile(`(?is)<.*?>`)

	rows := rowRegex.FindAllStringSubmatch(markup, -1)
	entries := make([]storeFileEntry, 0, len(rows))
	for _, row := range rows {
		link := linkRegex.FindStringSubmatch(row[1])
		if len(link) < 3 {
			continue
		}
		cells := cellRegex.FindAllStringSubmatch(row[1], -1)
		if len(cells) < 4 {
			continue
		}

		strip := func(value string) string {
			return strings.TrimSpace(html.UnescapeString(tagRegex.ReplaceAllString(value, "")))
		}

		entries = append(entries, storeFileEntry{
			FileName:    strings.TrimSpace(html.UnescapeString(link[2])),
			DownloadURL: strings.TrimSpace(html.UnescapeString(link[1])),
			SHA1:        strip(cells[2][1]),
			Size:        strip(cells[3][1]),
		})
	}
	return entries
}

func selectNewest(entries []storeFileEntry, targetFileName string, predicate func(string) bool) RemotePackageInfo {
	var candidates []storeFileEntry
	for _, entry := range entries {
		if predicate(entry.FileName) {
			candidates = append(candidates, entry)
		}
	}

	sort.Slice(candidates, func(i, j int) bool {
		return compareVersions(extractVersion(candidates[i].FileName), extractVersion(candidates[j].FileName)) > 0
	})

	if len(candidates) == 0 {
		return RemotePackageInfo{TargetFileName: targetFileName}
	}

	selected := candidates[0]
	return RemotePackageInfo{
		TargetFileName: targetFileName,
		SourceFileName: selected.FileName,
		DownloadURL:    selected.DownloadURL,
		SHA1:           selected.SHA1,
		Size:           selected.Size,
	}
}

func extractVersion(fileName string) []int {
	versionRegex := regexp.MustCompile(`_(\d+(?:\.\d+){1,3})_`)
	match := versionRegex.FindStringSubmatch(fileName)
	if len(match) < 2 {
		return []int{0}
	}
	parts := strings.Split(match[1], ".")
	version := make([]int, 0, len(parts))
	for _, part := range parts {
		value, err := strconv.Atoi(part)
		if err != nil {
			value = 0
		}
		version = append(version, value)
	}
	return version
}

func compareVersions(left []int, right []int) int {
	maxLen := len(left)
	if len(right) > maxLen {
		maxLen = len(right)
	}
	for i := 0; i < maxLen; i++ {
		leftValue := 0
		rightValue := 0
		if i < len(left) {
			leftValue = left[i]
		}
		if i < len(right) {
			rightValue = right[i]
		}
		if leftValue > rightValue {
			return 1
		}
		if leftValue < rightValue {
			return -1
		}
	}
	return 0
}

func buildDownloadStates(packages []RemotePackageInfo, manifest *PackageManifest) []DownloadState {
	manifestLookup := make(map[string]PackageManifestEntry)
	if manifest != nil {
		for _, entry := range manifest.Packages {
			manifestLookup[strings.ToLower(entry.TargetFileName)] = entry
		}
	}

	states := make([]DownloadState, 0, len(packages))
	for _, packageInfo := range packages {
		state := DownloadState{
			FileName: packageInfo.TargetFileName,
			Package:  packageInfo,
			Status:   StatusNormal,
			Progress: "",
		}

		localPath := filepath.Join(offlinePackagesDir(), packageInfo.TargetFileName)
		if _, err := os.Stat(localPath); err != nil {
			state.Status = StatusAbnormal
			state.Description = "文件不存在，需要下载。"
			state.RequiresDownload = true
			states = append(states, state)
			continue
		}

		entry, ok := manifestLookup[strings.ToLower(packageInfo.TargetFileName)]
		if !ok {
			state.Status = StatusStopped
			state.Description = "缺少版本记录，需要更新。"
			state.RequiresDownload = true
			states = append(states, state)
			continue
		}

		if !packageMatchesManifest(entry, packageInfo) {
			state.Status = StatusStopped
			state.Description = "线上版本已变化，需要更新。"
			state.RequiresDownload = true
			states = append(states, state)
			continue
		}

		state.Description = "已是最新。"
		states = append(states, state)
	}
	return states
}

func downloadPackage(ctx context.Context, logger *Logger, packageInfo RemotePackageInfo, destinationPath string, progress func(downloadProgress)) error {
	if packageInfo.DownloadURL == "" {
		return fmt.Errorf("缺少下载地址：%s", packageInfo.TargetFileName)
	}

	tempPath := destinationPath + ".download"
	defer os.Remove(tempPath)

	var lastErr error
	currentPackage := packageInfo
	for attempt := 1; attempt <= maxDownloadAttempts; attempt++ {
		if attempt > 1 {
			logger.Printf("准备重试下载：%s，第 %d 次。", currentPackage.TargetFileName, attempt)
			refreshed, err := refreshPackageInfo(ctx, logger, currentPackage.TargetFileName)
			if err == nil {
				currentPackage = refreshed
			}
			time.Sleep(2 * time.Second)
		}

		err := downloadPackageChunk(ctx, currentPackage.DownloadURL, tempPath, progress)
		if err != nil {
			lastErr = err
			logger.Printf("下载中断：%s：%v", currentPackage.TargetFileName, err)
			continue
		}

		sha1Value, err := computeSHA1(tempPath)
		if err != nil {
			lastErr = err
			continue
		}
		if !strings.EqualFold(sha1Value, currentPackage.SHA1) {
			lastErr = fmt.Errorf("下载校验失败：%s", currentPackage.TargetFileName)
			_ = os.Remove(tempPath)
			continue
		}

		_ = os.Remove(destinationPath)
		if err := os.Rename(tempPath, destinationPath); err != nil {
			return err
		}
		return nil
	}

	return lastErr
}

func refreshPackageInfo(ctx context.Context, logger *Logger, targetFileName string) (RemotePackageInfo, error) {
	packages, err := resolveRequiredPackages(ctx, logger)
	if err != nil {
		return RemotePackageInfo{}, err
	}
	for _, packageInfo := range packages {
		if strings.EqualFold(packageInfo.TargetFileName, targetFileName) {
			return packageInfo, nil
		}
	}
	return RemotePackageInfo{}, fmt.Errorf("刷新下载地址失败：%s", targetFileName)
}

func downloadPackageChunk(ctx context.Context, downloadURL string, tempPath string, progress func(downloadProgress)) error {
	existingLength := int64(0)
	if stat, err := os.Stat(tempPath); err == nil {
		existingLength = stat.Size()
	}

	request, err := http.NewRequestWithContext(ctx, http.MethodGet, downloadURL, nil)
	if err != nil {
		return err
	}
	if existingLength > 0 {
		request.Header.Set("Range", fmt.Sprintf("bytes=%d-", existingLength))
	}
	request.Header.Set("User-Agent", "Mozilla/5.0 HorizonEnvironmentAssistant/1.0")

	client := &http.Client{Timeout: 20 * time.Minute}
	response, err := client.Do(request)
	if err != nil {
		return err
	}
	defer response.Body.Close()

	if existingLength > 0 && response.StatusCode == http.StatusOK {
		_ = os.Remove(tempPath)
		existingLength = 0
	}
	if response.StatusCode < 200 || response.StatusCode >= 300 {
		return fmt.Errorf("HTTP %d", response.StatusCode)
	}

	fileMode := os.O_CREATE | os.O_WRONLY
	if existingLength > 0 && response.StatusCode == http.StatusPartialContent {
		fileMode |= os.O_APPEND
	} else {
		fileMode |= os.O_TRUNC
	}

	file, err := os.OpenFile(tempPath, fileMode, 0644)
	if err != nil {
		return err
	}
	defer file.Close()

	total := int64(0)
	if response.ContentLength > 0 {
		total = response.ContentLength
		if existingLength > 0 && response.StatusCode == http.StatusPartialContent {
			total += existingLength
		}
	}
	transferred := existingLength
	buffer := make([]byte, 128*1024)
	for {
		n, readErr := response.Body.Read(buffer)
		if n > 0 {
			if _, err := file.Write(buffer[:n]); err != nil {
				return err
			}
			transferred += int64(n)
			if progress != nil {
				progress(downloadProgress{Received: transferred, Total: total})
			}
		}
		if readErr == io.EOF {
			return nil
		}
		if readErr != nil {
			return readErr
		}
	}
}

func computeSHA1(path string) (string, error) {
	file, err := os.Open(path)
	if err != nil {
		return "", err
	}
	defer file.Close()

	hash := sha1.New()
	if _, err := io.Copy(hash, file); err != nil {
		return "", err
	}
	return hex.EncodeToString(hash.Sum(nil)), nil
}
