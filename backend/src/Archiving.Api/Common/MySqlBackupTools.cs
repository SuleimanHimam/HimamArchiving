namespace Archiving.Api.Common;

/// <summary>Locates the local mysqldump/mysql CLIs and builds a credential-safe options file.
/// Shared between the on-demand backup endpoint and the scheduled auto-backup job.</summary>
public static class MySqlBackupTools
{
    public static Dictionary<string, string> ParseConnectionString(string cs) =>
        cs.Split(';', StringSplitOptions.RemoveEmptyEntries)
          .Select(p => p.Split('=', 2))
          .Where(p => p.Length == 2)
          .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

    public static string? FindTool(string name)
    {
        var onPath = Environment
            .GetEnvironmentVariable("PATH")?
            .Split(Path.PathSeparator)
            .Select(d => Path.Combine(d, name + (OperatingSystem.IsWindows() ? ".exe" : "")))
            .FirstOrDefault(File.Exists);
        if (onPath is not null) return onPath;

        string[] candidates =
        [
            $@"C:\Program Files\MySQL\MySQL Server 8.4\bin\{name}.exe",
            $@"C:\Program Files\MySQL\MySQL Server 8.0\bin\{name}.exe",
            $@"C:\Program Files\MySQL\MySQL Server 5.7\bin\{name}.exe",
            $@"C:\xampp\mysql\bin\{name}.exe",
            $@"C:\wamp64\bin\mysql\mysql8.0.31\bin\{name}.exe",
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    // Write a temporary MySQL option file so credentials never appear in process args.
    public static string WriteTempOptionFile(Dictionary<string, string> p)
    {
        p.TryGetValue("server", out var host);   host ??= "localhost";
        p.TryGetValue("port",   out var port);   port ??= "3306";
        p.TryGetValue("uid",    out var user);   user ??= "root";
        p.TryGetValue("pwd",    out var pwd);    pwd  ??= "";

        var path = Path.Combine(Path.GetTempPath(), $"arch_my_{Guid.NewGuid():N}.cnf");
        File.WriteAllText(path,
            $"[client]\nhost={host}\nport={port}\nuser={user}\npassword={pwd}\n");
        return path;
    }
}
