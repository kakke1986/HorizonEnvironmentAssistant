using System.Text.Json;

namespace CafeGameEnvironmentAssistant.Core;

public static class FirewallHelper
{
    public static async Task<IReadOnlyList<FirewallProfileState>> GetProfileStatesAsync()
    {
        const string command = "Get-NetFirewallProfile | Select-Object Name, Enabled | ConvertTo-Json -Compress";
        var result = await CommandRunner.RunPowerShellAsync(command);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            LogHelper.Write($"读取防火墙状态失败：{GetCommandMessage(result)}");
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            var states = new List<FirewallProfileState>();

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in root.EnumerateArray())
                {
                    states.Add(ReadProfile(element));
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                states.Add(ReadProfile(root));
            }

            return states;
        }
        catch (JsonException ex)
        {
            LogHelper.Write($"解析防火墙状态失败：{ex.Message}");
            return [];
        }
    }

    public static async Task DisableAllProfilesAsync()
    {
        var result = await CommandRunner.RunAsync("netsh.exe", "advfirewall set allprofiles state off");
        if (result.Succeeded)
        {
            LogHelper.Write("已执行：netsh advfirewall set allprofiles state off");
            return;
        }

        LogHelper.Write($"关闭防火墙配置文件失败：{GetCommandMessage(result)}");
    }

    private static string GetCommandMessage(CommandResult result)
    {
        return string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
    }

    private static FirewallProfileState ReadProfile(JsonElement element)
    {
        var name = element.GetProperty("Name").GetString() ?? "Unknown";
        var enabled = ReadEnabledValue(element.GetProperty("Enabled"));
        return new FirewallProfileState(name, enabled);
    }

    private static bool ReadEnabledValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt32(out var number) => number != 0,
            JsonValueKind.String when bool.TryParse(element.GetString(), out var boolean) => boolean,
            JsonValueKind.String when int.TryParse(element.GetString(), out var number) => number != 0,
            _ => throw new JsonException($"Unsupported firewall Enabled value: {element.GetRawText()}")
        };
    }
}

public sealed record FirewallProfileState(string Name, bool Enabled);
