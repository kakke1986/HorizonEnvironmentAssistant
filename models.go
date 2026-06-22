package main

type Status string

const (
	StatusPending  Status = "等待"
	StatusChecking Status = "检测中"
	StatusNormal   Status = "正常"
	StatusStopped  Status = "停止"
	StatusAbnormal Status = "异常"
	StatusWorking  Status = "处理中"
)

type CheckItem struct {
	Type        string
	Item        string
	Status      Status
	Progress    string
	Description string
}

type RemotePackageInfo struct {
	TargetFileName string `json:"targetFileName"`
	SourceFileName string `json:"sourceFileName"`
	DownloadURL    string `json:"downloadUrl"`
	SHA1           string `json:"sha1"`
	Size           string `json:"size"`
}

type PackageManifest struct {
	DownloadedAtUtc string                 `json:"downloadedAtUtc"`
	Packages        []PackageManifestEntry `json:"packages"`
}

type PackageManifestEntry struct {
	TargetFileName string `json:"targetFileName"`
	SourceFileName string `json:"sourceFileName"`
	SHA1           string `json:"sha1"`
	Size           string `json:"size"`
}

type DownloadState struct {
	FileName         string
	Package          RemotePackageInfo
	Status           Status
	Description      string
	RequiresDownload bool
	Progress         string
}

var xboxServiceNames = []string{
	"GamingServices",
	"GamingServicesNet",
	"XblAuthManager",
	"XblGameSave",
	"XboxGipSvc",
	"XboxNetApiSvc",
}

var firewallServiceNames = []string{
	"BFE",
	"MpsSvc",
}

var offlinePackageNames = []string{
	"Microsoft.VCLibs.x64.appx",
	"Microsoft.NET.Native.Framework.x64.appx",
	"Microsoft.NET.Native.Runtime.x64.appx",
	"XboxIdentityProvider.appxbundle",
	"GamingServices.msixbundle",
}
