using System.Security.Cryptography;
using System.Text.Json;

namespace SwyfInstaller;

internal static class CompatibilityInstaller
{
    private sealed class InstallState
    {
        public bool HadOriginal { get; set; }
        public string InstalledSha256 { get; set; } = "";
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string StateDirectory(string gameDir)
    {
        string normalized = Path.GetFullPath(gameDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string key = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized.ToUpperInvariant())));
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SwyfInstaller", "compatibility-backups", key);
    }

    public static void Install(string gameDir, Action<string> report)
    {
        string source = Path.Combine(AppContext.BaseDirectory, "compatibility", "Plugins", "x86_64", "steam_api64.dll");
        if (!File.Exists(source))
            throw new FileNotFoundException("The bundled low-end CPU compatibility DLL is missing.", source);

        string target = Path.Combine(gameDir, "Plugins", "x86_64", "steam_api64.dll");
        string stateDir = StateDirectory(gameDir);
        string backup = Path.Combine(stateDir, "steam_api64.dll.original");
        string statePath = Path.Combine(stateDir, "state.json");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        Directory.CreateDirectory(stateDir);

        InstallState state;
        if (File.Exists(statePath))
        {
            state = JsonSerializer.Deserialize<InstallState>(File.ReadAllText(statePath))
                ?? throw new InvalidDataException("Compatibility backup state is invalid: " + statePath);
        }
        else
        {
            bool hadOriginal = File.Exists(target);
            if (hadOriginal)
                File.Copy(target, backup, overwrite: false);
            state = new InstallState { HadOriginal = hadOriginal };
        }

        string sourceHash = HashFile(source);
        File.Copy(source, target, overwrite: true);
        state.InstalledSha256 = sourceHash;
        File.WriteAllText(statePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        report("Installed low-end CPU compatibility DLL to Plugins\\x86_64\\steam_api64.dll.");
        report("Original DLL backup: " + (state.HadOriginal ? backup : "(no original DLL was present)"));
    }

    public static void Restore(string gameDir, Action<string> report)
    {
        string target = Path.Combine(gameDir, "Plugins", "x86_64", "steam_api64.dll");
        string stateDir = StateDirectory(gameDir);
        string backup = Path.Combine(stateDir, "steam_api64.dll.original");
        string statePath = Path.Combine(stateDir, "state.json");

        if (!File.Exists(statePath))
        {
            report("No compatibility DLL backup state found; nothing to restore.");
            return;
        }

        var state = JsonSerializer.Deserialize<InstallState>(File.ReadAllText(statePath))
            ?? throw new InvalidDataException("Compatibility backup state is invalid: " + statePath);

        if (state.HadOriginal)
        {
            if (!File.Exists(backup))
                throw new FileNotFoundException("The original Steam API DLL backup is missing; leaving the installed DLL untouched.", backup);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(backup, target, overwrite: true);
            report("Restored the original Steam API DLL.");
        }
        else if (File.Exists(target) && string.Equals(HashFile(target), state.InstalledSha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(target);
            report("Removed the compatibility DLL (there was no original DLL to restore).");
        }
        else
        {
            report("The installed DLL was changed after installation; leaving it untouched.");
        }

        try { Directory.Delete(stateDir, recursive: true); } catch { }
    }
}
