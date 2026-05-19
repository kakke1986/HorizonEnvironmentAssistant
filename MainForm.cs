using System.Diagnostics;
using System.Drawing;
using CafeGameEnvironmentAssistant.Core;
using Microsoft.Win32;

namespace CafeGameEnvironmentAssistant;

public sealed class MainForm : Form
{
    private static readonly Color WindowBackground = Color.FromArgb(246, 248, 251);
    private static readonly Color Surface = Color.FromArgb(255, 255, 255);
    private static readonly Color SurfaceMuted = Color.FromArgb(241, 245, 249);
    private static readonly Color Border = Color.FromArgb(203, 213, 225);
    private static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
    private static readonly Color TextSecondary = Color.FromArgb(71, 85, 105);
    private static readonly Color Accent = Color.FromArgb(37, 99, 235);
    private static readonly Color AccentHover = Color.FromArgb(219, 234, 254);
    private static readonly Color AccentDown = Color.FromArgb(191, 219, 254);
    private static readonly Color StatusNormal = Color.FromArgb(22, 163, 74);
    private static readonly Color StatusStopped = Color.FromArgb(180, 83, 9);
    private static readonly Color StatusAbnormal = Color.FromArgb(220, 38, 38);

    private readonly DataGridView _grid = new();
    private readonly Button _healthCheckButton = new();
    private readonly Button _repairButton = new();
    private readonly Button _offlineRepairButton = new();
    private readonly Button _downloadPackagesButton = new();
    private readonly Button _firewallRepairButton = new();
    private readonly Label _windowsFamilyLabel = new();
    private readonly Label _windowsVersionLabel = new();
    private readonly string _offlinePackagesDirectory = AppPaths.OfflinePackagesDirectory;
    private static readonly TimeSpan DownloadGridRefreshInterval = TimeSpan.FromMilliseconds(120);
    private DateTime _lastDownloadGridRefreshUtc = DateTime.MinValue;

    public MainForm()
    {
        Text = "地平线环境助手";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 700);
        Size = new Size(1160, 780);
        BackColor = WindowBackground;
        ForeColor = TextPrimary;
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        InitializeLayout();
        Resize += (_, _) => FitRowsToVisibleArea();

        Shown += async (_, _) =>
        {
            await RunSafelyAsync(
                async () =>
                {
                    LogHelper.Write("程序启动，当前进程已具备管理员权限。");
                    await RunHealthCheckAsync();
                    await CheckForPackageUpdatesOnStartupAsync();
                },
                "启动初始化失败");
        };
    }

    private void InitializeLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16),
            BackColor = WindowBackground
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var headerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = WindowBackground
        };
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 264));

        var leftHeaderPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = WindowBackground
        };
        leftHeaderPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        leftHeaderPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var title = new Label
        {
            Dock = DockStyle.Fill,
            Text = "地平线环境助手",
            Font = new Font("Microsoft YaHei UI", 19F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = TextPrimary,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };

        var systemInfoPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Surface,
            Padding = new Padding(14),
            Margin = new Padding(8, 8, 16, 8)
        };
        systemInfoPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        systemInfoPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _windowsFamilyLabel.Dock = DockStyle.Fill;
        _windowsFamilyLabel.AutoEllipsis = false;
        _windowsFamilyLabel.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold, GraphicsUnit.Point);
        _windowsFamilyLabel.ForeColor = TextPrimary;
        _windowsFamilyLabel.TextAlign = ContentAlignment.MiddleRight;

        _windowsVersionLabel.Dock = DockStyle.Fill;
        _windowsVersionLabel.AutoEllipsis = false;
        _windowsVersionLabel.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
        _windowsVersionLabel.ForeColor = TextSecondary;
        _windowsVersionLabel.TextAlign = ContentAlignment.MiddleRight;

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 2, 0, 8),
            BackColor = WindowBackground
        };

        systemInfoPanel.Controls.Add(_windowsFamilyLabel, 0, 0);
        systemInfoPanel.Controls.Add(_windowsVersionLabel, 0, 1);
        leftHeaderPanel.Controls.Add(title, 0, 0);
        leftHeaderPanel.Controls.Add(buttonPanel, 0, 1);
        headerPanel.Controls.Add(leftHeaderPanel, 0, 0);
        headerPanel.Controls.Add(systemInfoPanel, 1, 0);

        ConfigureButton(_healthCheckButton, "刷新");
        ConfigureButton(_repairButton, "免重启修复");
        ConfigureButton(_offlineRepairButton, "离线修复");
        ConfigureButton(_downloadPackagesButton, "下载离线包");
        ConfigureButton(_firewallRepairButton, "防火墙兼容");

        _healthCheckButton.Click += async (_, _) => await RunExclusiveAsync(RunHealthCheckAsync);
        _repairButton.Click += async (_, _) => await RunExclusiveAsync(RunNoRestartRepairAsync);
        _offlineRepairButton.Click += async (_, _) => await RunExclusiveAsync(RunOfflineXboxRepairAsync);
        _downloadPackagesButton.Click += async (_, _) => await RunExclusiveAsync(RunDownloadOfflinePackagesAsync);
        _firewallRepairButton.Click += async (_, _) => await RunExclusiveAsync(RunFirewallCompatibilityRepairAsync);

        buttonPanel.Controls.AddRange(
        [
            _healthCheckButton,
            _repairButton,
            _offlineRepairButton,
            _downloadPackagesButton,
            _firewallRepairButton
        ]);

        ConfigureGrid();

        root.Controls.Add(headerPanel, 0, 0);
        root.Controls.Add(_grid, 0, 1);
        Controls.Add(root);
    }

    private void ConfigureButton(Button button, string text)
    {
        button.Text = text;
        button.Width = 148;
        button.Height = 38;
        button.Margin = new Padding(0, 0, 10, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = AccentHover;
        button.FlatAppearance.MouseDownBackColor = AccentDown;
        button.BackColor = Surface;
        button.ForeColor = TextPrimary;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.ScrollBars = ScrollBars.None;
        _grid.ReadOnly = true;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.BackgroundColor = Surface;
        _grid.BorderStyle = BorderStyle.FixedSingle;
        _grid.GridColor = Border;
        _grid.RowHeadersVisible = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceMuted;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SurfaceMuted;
        _grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextPrimary;
        _grid.DefaultCellStyle.BackColor = Surface;
        _grid.DefaultCellStyle.ForeColor = TextPrimary;
        _grid.DefaultCellStyle.SelectionBackColor = AccentHover;
        _grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        _grid.AlternatingRowsDefaultCellStyle.ForeColor = TextPrimary;
        _grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = AccentHover;
        _grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = TextPrimary;
        _grid.RowTemplate.Height = 24;

        _grid.Columns.Add("Type", "类型");
        _grid.Columns.Add("Item", "项目");
        _grid.Columns.Add("Status", "状态");
        _grid.Columns.Add("Progress", "进度");
        _grid.Columns.Add("Description", "说明");

        _grid.Columns["Type"]!.Width = 170;
        _grid.Columns["Item"]!.Width = 320;
        _grid.Columns["Status"]!.Width = 130;
        _grid.Columns["Progress"]!.Width = 160;
        _grid.Columns["Progress"]!.Visible = false;
        _grid.Columns["Description"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _grid.Columns["Description"]!.MinimumWidth = 420;
        _grid.CellPainting += GridOnCellPainting;
    }

    private async Task RunExclusiveAsync(Func<Task> action)
    {
        SetButtonsEnabled(false);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            LogHelper.Write($"操作失败：{ex}");
            ShowOwnedMessageBox(
                $"操作失败：{ex.Message}",
                "地平线环境助手",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            SetBusyCursor(false);
            SetButtonsEnabled(true);
        }
    }

    private async Task RunSafelyAsync(Func<Task> action, string operationName)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            LogHelper.Write($"{operationName}：{ex}");
            ShowOwnedMessageBox(
                $"{operationName}：{ex.Message}",
                "地平线环境助手",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _healthCheckButton.Enabled = enabled;
        _repairButton.Enabled = enabled;
        _offlineRepairButton.Enabled = enabled;
        _downloadPackagesButton.Enabled = enabled;
        _firewallRepairButton.Enabled = enabled;
    }

    private async Task RunHealthCheckAsync()
    {
        SetProgressColumnVisible(false);
        LogHelper.Write("开始执行刷新。");

        UpdateWindowsVersionLabels();
        var items = new List<DetectionItem>();

        items.AddRange(await DetectFirewallProfilesAsync());
        items.AddRange(DetectServices(
        [
            "BFE",
            "MpsSvc",
            "AppXSVC",
            "ClipSVC",
            "InstallService",
            "BITS",
            "wuauserv"
        ], "核心服务"));

        items.AddRange(DetectServices(
        [
            "GamingServices",
            "GamingServicesNet",
            "XblAuthManager",
            "XblGameSave",
            "XboxNetApiSvc",
            "XboxGipSvc"
        ], "Xbox 服务"));

        items.Add(DetectTrustedAppsPolicy());
        items.AddRange(DetectOfflinePackages());

        BindDetectionItems(items);
        LogHelper.Write("刷新完成。");
    }

    private async Task RunDownloadOfflinePackagesAsync()
    {
        try
        {
            SetProgressColumnVisible(true);
            if (!EnsureSupportedPackageEnvironment("下载离线包"))
            {
                SetProgressColumnVisible(false);
                return;
            }

            Directory.CreateDirectory(_offlinePackagesDirectory);
            LogHelper.Write("开始刷新离线包状态。");
            SetBusyCursor(true);
            BindDetectionItems(GetRefreshingOfflinePackageItems());
            await Task.Yield();

            var packageInfos = await StorePackageClient.ResolveRequiredPackagesAsync();
            var manifest = await PackageManifestStore.LoadAsync();
            var packageStates = BuildPackageDownloadStates(packageInfos, manifest);
            RefreshDownloadItems(packageStates, force: true);
            SetBusyCursor(false);

            var packagesToDownload = packageStates
                .Where(state => state.RequiresDownload)
                .ToList();
            if (packagesToDownload.Count == 0)
            {
                SetBusyCursor(false);
                MessageBox.Show(
                    "离线包已是最新。",
                    "下载离线包",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            SetBusyCursor(false);
            var downloadChoice = MessageBox.Show(
                "发现文件未下载或需要更新，是否开始下载更新？",
                "下载离线包",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (downloadChoice != DialogResult.Yes)
            {
                LogHelper.Write("用户取消了离线包下载更新。");
                await ShowOfflinePackageScanAsync();
                return;
            }

            for (var index = 0; index < packageStates.Count; index++)
            {
                var state = packageStates[index];
                if (!state.RequiresDownload)
                {
                    continue;
                }

                packageStates[index] = state with
                {
                    Status = DetectionStatus.Stopped,
                    Description = "正在下载。",
                    ProgressPercent = 0
                };
                RefreshDownloadItems(packageStates, force: true);

                var targetPath = Path.Combine(_offlinePackagesDirectory, state.Package.TargetFileName);
                await StorePackageClient.DownloadPackageAsync(
                    state.Package,
                    targetPath,
                    message =>
                    {
                        packageStates[index] = packageStates[index] with
                        {
                            Status = DetectionStatus.Stopped,
                            Description = message
                        };
                        RefreshDownloadItems(packageStates);
                    },
                    progress =>
                    {
                        packageStates[index] = packageStates[index] with
                        {
                            Status = DetectionStatus.Stopped,
                            Description = progress.Percent is int percent
                                ? $"正在下载 {percent}%"
                                : "正在下载。",
                            ProgressPercent = progress.Percent
                        };
                        RefreshDownloadItems(packageStates);
                    });

                packageStates[index] = state with
                {
                    Status = DetectionStatus.Normal,
                    Description = "已是最新。",
                    RequiresDownload = false,
                    ProgressPercent = 100
                };
                RefreshDownloadItems(packageStates, force: true);
                LogHelper.Write($"下载完成：{state.Package.TargetFileName}");
            }

            var manifestEntries = packageInfos
                .Select(PackageManifestEntry.FromRemotePackage)
                .ToList();
            await PackageManifestStore.SaveAsync(new PackageManifest(DateTimeOffset.UtcNow, manifestEntries));
            LogHelper.Write("已生成 packages-manifest.json。");

            MessageBox.Show(
                "离线包下载完成。",
                "下载离线包",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            await RunHealthCheckAsync();
        }
        catch (Exception ex)
        {
            SetBusyCursor(false);
            LogHelper.Write($"下载离线包失败：{ex.Message}");
            MessageBox.Show(
                $"下载离线包失败：{ex.Message}",
                "下载离线包",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            SetBusyCursor(false);
        }
    }

    private async Task CheckForPackageUpdatesOnStartupAsync()
    {
        try
        {
            var manifest = await PackageManifestStore.LoadAsync();
            if (manifest is null)
            {
                return;
            }

            var onlinePackages = await StorePackageClient.ResolveRequiredPackagesAsync();
            if (!PackageManifestStore.HasOnlineChanges(manifest, onlinePackages))
            {
                return;
            }

            ShowOwnedMessageBox(
                "文件需要更新",
                "离线包更新",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            RestoreAndActivate();
            LogHelper.Write("检测到线上离线包已变化。");
        }
        catch (Exception ex)
        {
            LogHelper.Write($"启动时检查离线包更新失败：{ex.Message}");
        }
    }

    private static IEnumerable<DetectionItem> ToDownloadDetectionItems(IEnumerable<DownloadState> states)
    {
        return states.Select(state =>
            new DetectionItem("离线包", state.FileName, state.Status, state.Description, state.ProgressPercent));
    }

    private static IEnumerable<DetectionItem> GetRefreshingOfflinePackageItems()
    {
        return GetOfflinePackageNames().Select(packageName =>
            new DetectionItem(
                "离线包",
                packageName,
                DetectionStatus.Stopped,
                "正在刷新线上信息。",
                null));
    }

    private List<DownloadState> BuildPackageDownloadStates(
        IReadOnlyList<RemotePackageInfo> packageInfos,
        PackageManifest? manifest)
    {
        var manifestLookup = manifest?.Packages.ToDictionary(
            package => package.TargetFileName,
            StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, PackageManifestEntry>(StringComparer.OrdinalIgnoreCase);

        return packageInfos
            .Select(package =>
            {
                var packagePath = Path.Combine(_offlinePackagesDirectory, package.TargetFileName);
                if (!File.Exists(packagePath))
                {
                    return new DownloadState(
                        package.TargetFileName,
                        package,
                        DetectionStatus.Abnormal,
                        "文件不存在。",
                        true,
                        0);
                }

                if (!manifestLookup.TryGetValue(package.TargetFileName, out var manifestEntry))
                {
                    return new DownloadState(
                        package.TargetFileName,
                        package,
                        DetectionStatus.Stopped,
                        "缺少版本记录，需要更新。",
                        true,
                        0);
                }

                var needsUpdate = !PackageManifestStore.Matches(manifestEntry, package);
                return new DownloadState(
                    package.TargetFileName,
                    package,
                    needsUpdate ? DetectionStatus.Stopped : DetectionStatus.Normal,
                    needsUpdate ? "文件需要更新。" : "已是最新。",
                    needsUpdate,
                    needsUpdate ? 0 : 100);
            })
            .ToList();
    }

    private void UpdateWindowsVersionLabels()
    {
        var info = GetWindowsVersionInfo();
        var architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86";
        _windowsFamilyLabel.Text = info.Family;
        _windowsVersionLabel.Text = $"{info.DisplayVersion} {architecture}";
    }

    private static WindowsVersionInfo GetWindowsVersionInfo()
    {
        const string currentVersionPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(currentVersionPath, writable: false);

        var productName = key?.GetValue("ProductName") as string;
        var displayVersion = key?.GetValue("DisplayVersion") as string
            ?? key?.GetValue("ReleaseId") as string
            ?? "未知版本";
        var buildText = key?.GetValue("CurrentBuildNumber") as string
            ?? key?.GetValue("CurrentBuild") as string;
        var ubr = key?.GetValue("UBR");

        if (!int.TryParse(buildText, out var buildNumber))
        {
            var fallbackVersion = Environment.OSVersion.Version;
            return new WindowsVersionInfo(
                "Windows",
                displayVersion,
                $"Windows {fallbackVersion}",
                fallbackVersion.Build,
                fallbackVersion.Major >= 10);
        }

        var family = buildNumber >= 22000 ? "Windows 11" : "Windows 10";
        var edition = GetEditionSuffix(productName);
        var buildDisplay = ubr is int updateBuild
            ? $"{buildNumber}.{updateBuild}"
            : buildNumber.ToString();
        var isSupported = buildNumber >= 10240;

        return new WindowsVersionInfo(
            family,
            displayVersion,
            $"{family}{edition} (Build {buildDisplay})",
            buildNumber,
            isSupported);
    }

    private static string GetEditionSuffix(string? productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return string.Empty;
        }

        if (productName.StartsWith("Windows 10", StringComparison.OrdinalIgnoreCase))
        {
            return productName["Windows 10".Length..];
        }

        if (productName.StartsWith("Windows 11", StringComparison.OrdinalIgnoreCase))
        {
            return productName["Windows 11".Length..];
        }

        return $" {productName}";
    }

    private async Task<IEnumerable<DetectionItem>> DetectFirewallProfilesAsync()
    {
        var profiles = await FirewallHelper.GetProfileStatesAsync();
        if (profiles.Count == 0)
        {
            return
            [
                new DetectionItem("防火墙", "配置文件状态", DetectionStatus.Abnormal, "未能读取防火墙配置文件状态。")
            ];
        }

        return profiles.Select(profile =>
            new DetectionItem(
                "防火墙",
                $"{profile.Name} 配置文件",
                profile.Enabled ? DetectionStatus.Normal : DetectionStatus.Stopped,
                profile.Enabled ? "当前为开启。" : "当前为关闭。"));
    }

    private static IEnumerable<DetectionItem> DetectServices(IEnumerable<string> serviceNames, string type)
    {
        foreach (var serviceName in serviceNames)
        {
            var info = ServiceHelper.GetServiceInfo(serviceName);
            if (!info.Exists)
            {
                yield return new DetectionItem(type, serviceName, DetectionStatus.Abnormal, "服务不存在。");
                continue;
            }

            var status = info.StartType == ServiceStartType.Disabled
                ? DetectionStatus.Abnormal
                : info.Status == ServiceProcessStatus.Running
                    ? DetectionStatus.Normal
                    : DetectionStatus.Stopped;

            yield return new DetectionItem(
                type,
                serviceName,
                status,
                $"当前状态：{ServiceHelper.TranslateStatus(info.Status)}；启动类型：{ServiceHelper.TranslateStartType(info.StartType)}。");
        }
    }

    private static DetectionItem DetectTrustedAppsPolicy()
    {
        const string path = @"SOFTWARE\Policies\Microsoft\Windows\Appx";
        using var key = Registry.LocalMachine.OpenSubKey(path, writable: false);
        var value = key?.GetValue("AllowAllTrustedApps");
        var enabled = value is int intValue && intValue == 1;

        return enabled
            ? new DetectionItem("注册表", "AllowAllTrustedApps", DetectionStatus.Normal, "当前值为 1。")
            : new DetectionItem("注册表", "AllowAllTrustedApps", DetectionStatus.Abnormal, "未设置为 1。");
    }

    private IEnumerable<DetectionItem> DetectOfflinePackages()
    {
        foreach (var packageName in GetOfflinePackageNames())
        {
            var fullPath = Path.Combine(_offlinePackagesDirectory, packageName);
            yield return File.Exists(fullPath)
                ? new DetectionItem("离线包", packageName, DetectionStatus.Normal, "文件存在。")
                : new DetectionItem("离线包", packageName, DetectionStatus.Abnormal, "文件不存在。");
        }
    }

    private async Task RunNoRestartRepairAsync()
    {
        if (!EnsureSupportedPackageEnvironment("免重启修复"))
        {
            return;
        }

        LogHelper.Write("开始执行免重启修复。");

        SetTrustedAppsPolicy();
        await ServiceHelper.ConfigureAndStartAsync("BFE", ServiceStartType.Auto);
        await ServiceHelper.ConfigureAndStartAsync("MpsSvc", ServiceStartType.Auto);
        await FirewallHelper.DisableAllProfilesAsync();
        await ServiceHelper.ConfigureAndStartAsync("AppXSVC", ServiceStartType.Demand);
        await ServiceHelper.ConfigureAndStartAsync("ClipSVC", ServiceStartType.Demand);
        await ServiceHelper.ConfigureAndStartAsync("InstallService", ServiceStartType.Demand);
        await ServiceHelper.ConfigureAndStartAsync("BITS", ServiceStartType.Demand);
        await ServiceHelper.ConfigureAndStartAsync("wuauserv", ServiceStartType.Demand);

        await ServiceHelper.TryStartIfExistsAsync("GamingServices");
        await ServiceHelper.TryStartIfExistsAsync("GamingServicesNet");

        LogHelper.Write("免重启修复完成，开始重新刷新。");
        await RunHealthCheckAsync();
    }

    private async Task RunOfflineXboxRepairAsync()
    {
        SetProgressColumnVisible(false);
        if (!EnsureSupportedPackageEnvironment("离线修复"))
        {
            return;
        }

        Directory.CreateDirectory(_offlinePackagesDirectory);

        var packageStates = await ShowOfflinePackageScanAsync();
        var packageNames = packageStates.Select(state => state.FileName).ToArray();

        var canInstall = packageStates.Any(state => state.Exists);

        if (!canInstall)
        {
            LogHelper.Write("未发现任何离线包，自动进入下载离线包。");
            await RunDownloadOfflinePackagesAsync();
            return;
        }

        var installChoice = MessageBox.Show(
            "已发现离线包，是否开始安装现有文件？",
            "离线修复",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (installChoice != DialogResult.Yes)
        {
            LogHelper.Write("用户取消了离线修复安装。");
            return;
        }

        LogHelper.Write("开始执行离线修复 Xbox。");
        await RunNoRestartRepairAsync();

        var failedPackages = new List<string>();
        foreach (var packageName in packageNames)
        {
            var fullPath = Path.Combine(_offlinePackagesDirectory, packageName);
            if (!File.Exists(fullPath))
            {
                LogHelper.Write($"离线包不存在，已跳过：{packageName}");
                continue;
            }

            var installResult = await AppxHelper.InstallPackageAsync(fullPath);
            if (!installResult.Succeeded)
            {
                failedPackages.Add(installResult.PackageFileName);
            }
        }

        foreach (var processName in new[]
                 {
                     "GamingServices",
                     "XboxPcApp",
                     "XboxAppServices",
                     "GameBar",
                     "GameBarFTServer"
                 })
        {
            KillProcessesByName(processName);
        }

        await ServiceHelper.TryStartIfExistsAsync("GamingServices");
        await ServiceHelper.TryStartIfExistsAsync("GamingServicesNet");

        LogHelper.Write("离线修复 Xbox 完成，开始重新刷新。");
        await RunHealthCheckAsync();

        if (failedPackages.Count > 0)
        {
            ShowOwnedMessageBox(
                "部分离线包安装失败，请查看 Logs\\latest.log。\r\n\r\n" + string.Join("\r\n", failedPackages),
                "离线修复",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private Task<List<OfflinePackageState>> ShowOfflinePackageScanAsync()
    {
        SetProgressColumnVisible(false);
        Directory.CreateDirectory(_offlinePackagesDirectory);

        var packageStates = GetOfflinePackageNames()
            .Select(packageName => new OfflinePackageState(
                packageName,
                File.Exists(Path.Combine(_offlinePackagesDirectory, packageName))))
            .ToList();

        BindDetectionItems(packageStates.Select(state =>
            new DetectionItem(
                "离线包",
                state.FileName,
                state.Exists ? DetectionStatus.Normal : DetectionStatus.Abnormal,
                state.Exists ? "文件存在。" : "文件不存在。")));

        return Task.FromResult(packageStates);
    }

    private async Task RunFirewallCompatibilityRepairAsync()
    {
        SetProgressColumnVisible(false);
        if (!EnsureSupportedWindowsVersion("防火墙兼容"))
        {
            return;
        }

        LogHelper.Write("开始执行防火墙兼容修复。");
        await ServiceHelper.ConfigureAndStartAsync("BFE", ServiceStartType.Auto);
        await ServiceHelper.ConfigureAndStartAsync("MpsSvc", ServiceStartType.Auto);
        await FirewallHelper.DisableAllProfilesAsync();
        LogHelper.Write("防火墙兼容修复完成：配置文件已关闭，BFE 与 MpsSvc 保持运行。");
        await RunHealthCheckAsync();
    }

    private static void SetTrustedAppsPolicy()
    {
        const string path = @"SOFTWARE\Policies\Microsoft\Windows\Appx";
        using var key = Registry.LocalMachine.CreateSubKey(path, writable: true);
        key?.SetValue("AllowAllTrustedApps", 1, RegistryValueKind.DWord);
        LogHelper.Write(@"已设置 HKLM\SOFTWARE\Policies\Microsoft\Windows\Appx\AllowAllTrustedApps=1。");
    }

    private bool EnsureSupportedPackageEnvironment(string operationName)
    {
        if (!EnsureSupportedWindowsVersion(operationName))
        {
            return false;
        }

        if (Environment.Is64BitOperatingSystem)
        {
            return true;
        }

        LogHelper.Write($"{operationName} 已取消：当前系统不是 64 位。");
        ShowOwnedMessageBox(
            "当前功能仅支持 Windows 10 / Windows 11 64 位系统。\r\n\r\n当前系统不是 64 位，已停止操作。",
            operationName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
    }

    private bool EnsureSupportedWindowsVersion(string operationName)
    {
        var info = GetWindowsVersionInfo();
        if (info.IsSupported)
        {
            return true;
        }

        LogHelper.Write($"{operationName} 已取消：不支持的系统版本，Build {info.BuildNumber}。");
        ShowOwnedMessageBox(
            "当前程序仅支持 Windows 10 / Windows 11。\r\n\r\n当前系统版本不在支持范围内，已停止操作。",
            operationName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
    }

    private static void KillProcessesByName(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        if (processes.Length == 0)
        {
            LogHelper.Write($"未发现进程：{processName}.exe");
            return;
        }

        foreach (var process in processes)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                LogHelper.Write($"已结束进程：{processName}.exe");
            }
            catch (Exception ex)
            {
                LogHelper.Write($"结束进程失败：{processName}.exe，{ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private void BindDetectionItems(IEnumerable<DetectionItem> items)
    {
        _grid.Rows.Clear();

        foreach (var item in items)
        {
            var rowIndex = _grid.Rows.Add(
                item.Type,
                item.Item,
                TranslateDetectionStatus(item.Status),
                item.ProgressPercent,
                item.Description);

            var row = _grid.Rows[rowIndex];
            ApplyStatusColor(row, item.Status);
        }

        FitRowsToVisibleArea();
    }

    private void RefreshDownloadItems(IReadOnlyList<DownloadState> states, bool force = false)
    {
        var now = DateTime.UtcNow;
        if (!force && now - _lastDownloadGridRefreshUtc < DownloadGridRefreshInterval)
        {
            return;
        }

        _lastDownloadGridRefreshUtc = now;
        var items = ToDownloadDetectionItems(states).ToList();

        if (!CanUpdateDownloadRowsInPlace(items))
        {
            BindDetectionItems(items);
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var row = _grid.Rows[index];
            row.Cells["Status"]!.Value = TranslateDetectionStatus(item.Status);
            row.Cells["Progress"]!.Value = item.ProgressPercent;
            row.Cells["Description"]!.Value = item.Description;
            ApplyStatusColor(row, item.Status);
        }

        _grid.InvalidateColumn(_grid.Columns["Progress"]!.Index);
    }

    private bool CanUpdateDownloadRowsInPlace(IReadOnlyList<DetectionItem> items)
    {
        if (_grid.Rows.Count != items.Count)
        {
            return false;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var row = _grid.Rows[index];
            if (!string.Equals(row.Cells["Type"]!.Value as string, items[index].Type, StringComparison.Ordinal)
                || !string.Equals(row.Cells["Item"]!.Value as string, items[index].Item, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void ApplyStatusColor(DataGridViewRow row, DetectionStatus status)
    {
        row.Cells["Status"]!.Style.ForeColor = status switch
        {
            DetectionStatus.Normal => StatusNormal,
            DetectionStatus.Stopped => StatusStopped,
            _ => StatusAbnormal
        };
    }

    private void SetProgressColumnVisible(bool visible)
    {
        _grid.Columns["Progress"]!.Visible = visible;
    }

    private void SetBusyCursor(bool isBusy)
    {
        UseWaitCursor = isBusy;
        Application.UseWaitCursor = isBusy;
        Cursor.Current = isBusy ? Cursors.WaitCursor : Cursors.Default;
    }

    private DialogResult ShowOwnedMessageBox(
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon)
    {
        return MessageBox.Show(this, text, caption, buttons, icon);
    }

    private void RestoreAndActivate()
    {
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        Show();
        BringToFront();
        Activate();
    }

    private void FitRowsToVisibleArea()
    {
        if (_grid.Rows.Count == 0)
        {
            return;
        }

        var availableHeight = _grid.ClientSize.Height - _grid.ColumnHeadersHeight - 2;
        var fittedHeight = Math.Max(20, availableHeight / _grid.Rows.Count);

        foreach (DataGridViewRow row in _grid.Rows)
        {
            row.Height = fittedHeight;
        }
    }

    private static string TranslateDetectionStatus(DetectionStatus status)
    {
        return status switch
        {
            DetectionStatus.Normal => "正常",
            DetectionStatus.Stopped => "停止",
            _ => "异常"
        };
    }

    private void GridOnCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "Progress")
        {
            return;
        }

        e.PaintBackground(e.CellBounds, true);

        var progress = e.Value is int value ? Math.Clamp(value, 0, 100) : 0;
        var barBounds = new Rectangle(
            e.CellBounds.X + 8,
            e.CellBounds.Y + 6,
            Math.Max(0, e.CellBounds.Width - 16),
            Math.Max(0, e.CellBounds.Height - 12));

        var graphics = e.Graphics;
        if (graphics is null)
        {
            return;
        }

        using var backgroundBrush = new SolidBrush(SurfaceMuted);
        graphics.FillRectangle(backgroundBrush, barBounds);

        if (progress > 0)
        {
            var fillBounds = new Rectangle(
                barBounds.X,
                barBounds.Y,
                Math.Max(1, barBounds.Width * progress / 100),
                barBounds.Height);
            using var fillBrush = new SolidBrush(Accent);
            graphics.FillRectangle(fillBrush, fillBounds);
        }

        var text = e.Value is int ? $"{progress}%" : string.Empty;
        TextRenderer.DrawText(
            graphics,
            text,
            _grid.Font,
            e.CellBounds,
            TextPrimary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        e.Handled = true;
    }

    private sealed record DetectionItem(
        string Type,
        string Item,
        DetectionStatus Status,
        string Description,
        int? ProgressPercent = null);
    private sealed record OfflinePackageState(string FileName, bool Exists);
    private sealed record DownloadState(
        string FileName,
        RemotePackageInfo Package,
        DetectionStatus Status,
        string Description,
        bool RequiresDownload,
        int? ProgressPercent);
    private sealed record WindowsVersionInfo(
        string Family,
        string DisplayVersion,
        string Description,
        int BuildNumber,
        bool IsSupported);

    private static string[] GetOfflinePackageNames()
    {
        return
        [
            "Microsoft.VCLibs.x64.appx",
            "Microsoft.NET.Native.Framework.x64.appx",
            "Microsoft.NET.Native.Runtime.x64.appx",
            "XboxIdentityProvider.appxbundle",
            "GamingServices.msixbundle"
        ];
    }

    private enum DetectionStatus
    {
        Normal,
        Abnormal,
        Stopped
    }
}
