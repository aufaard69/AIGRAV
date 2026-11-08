using AIGRAV.Contracts;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
namespace AIGRAV.RevitBridge;

/// <summary>
/// Loads AIGRAV external skills from the Skills directory using collectible
/// AssemblyLoadContexts and a per-load shadow-copy runtime directory.
///
/// Development workflow:
///   Build source DLL -> replace source DLL in Skills -> aigrav_reload_skills
///
/// Revit never loads the source DLL directly, so the source file can be
/// overwritten by Visual Studio even while the previous skill version is active.
/// </summary>
public static class AIGRAVSkillLoader
{
    private sealed class LoadedSkillAssembly
    {
        public string SourceAssemblyPath { get; init; } = string.Empty;
        public string RuntimeAssemblyPath { get; init; } = string.Empty;
        public string RuntimeDirectory { get; init; } = string.Empty;
        public SkillLoadContext LoadContext { get; init; } = null!;
        public List<string> SkillNames { get; } = new();
    }

    private sealed class SkillLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly string _hostAssemblyName;
        private readonly string _contractsAssemblyName;

        public SkillLoadContext(string mainAssemblyPath)
            : base(
                $"AIGRAVSkill_{Path.GetFileNameWithoutExtension(mainAssemblyPath)}_{Guid.NewGuid():N}",
                isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
            _hostAssemblyName =
                typeof(AIGRAVSkillLoader).Assembly.GetName().Name ?? string.Empty;
            _contractsAssemblyName =
                typeof(IAIGRAVSkill).Assembly.GetName().Name ?? string.Empty;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string requestedName = assemblyName.Name ?? string.Empty;

            if (string.Equals(
                    requestedName,
                    _hostAssemblyName,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    requestedName,
                    _contractsAssemblyName,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    requestedName,
                    "RevitAPI",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    requestedName,
                    "RevitAPIUI",
                    StringComparison.OrdinalIgnoreCase))
            {
                return AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(assembly =>
                        string.Equals(
                            assembly.GetName().Name,
                            requestedName,
                            StringComparison.OrdinalIgnoreCase));
            }

            string? resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);

            if (!string.IsNullOrWhiteSpace(resolvedPath)
                && File.Exists(resolvedPath))
            {
                return LoadFromAssemblyPath(resolvedPath);
            }

            return null;
        }
    }

    private static readonly object Sync = new();

    private static readonly List<string> DiscoveredSkillFiles = new();
    private static readonly List<LoadedSkillAssembly> LoadedAssemblies = new();

    private static string RuntimeRootDirectory { get; set; } = string.Empty;

    public static string SkillsDirectory { get; private set; } = string.Empty;

    public static void Initialize()
    {
        lock (Sync)
        {
            if (string.IsNullOrWhiteSpace(SkillsDirectory))
            {
                string assemblyDirectory =
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                    ?? AppContext.BaseDirectory;

                SkillsDirectory = Path.Combine(assemblyDirectory, "Skills");

                RuntimeRootDirectory = Path.Combine(
                    Path.GetTempPath(),
                    "AIGRAV",
                    "SkillRuntime");

                Directory.CreateDirectory(SkillsDirectory);
                Directory.CreateDirectory(RuntimeRootDirectory);
            }
        }

        Discover();
        LoadDiscoveredSkills();
    }

    public static IReadOnlyList<string> Discover()
    {
        lock (Sync)
        {
            DiscoveredSkillFiles.Clear();

            if (!Directory.Exists(SkillsDirectory))
            {
                return DiscoveredSkillFiles.AsReadOnly();
            }

            foreach (string file in Directory.GetFiles(
                         SkillsDirectory,
                         "*.dll",
                         SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(
                        Path.GetFileName(file),
                        Path.GetFileName(Assembly.GetExecutingAssembly().Location),
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                DiscoveredSkillFiles.Add(file);
            }

            DiscoveredSkillFiles.Sort(StringComparer.OrdinalIgnoreCase);

            return DiscoveredSkillFiles.AsReadOnly();
        }
    }

    public static string Reload()
    {
        try
        {
            AIGRAVSkillRegistry.RemoveExternalSkills();

            List<LoadedSkillAssembly> oldAssemblies;

            lock (Sync)
            {
                oldAssemblies = LoadedAssemblies.ToList();
                LoadedAssemblies.Clear();
            }

            foreach (LoadedSkillAssembly loaded in oldAssemblies)
            {
                try
                {
                    loaded.LoadContext.Unload();
                }
                catch
                {
                    // Continue unloading the remaining skill contexts.
                }
            }

            // Release collectible AssemblyLoadContexts before loading the next
            // version. The old runtime directory is cleaned up afterwards.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            foreach (LoadedSkillAssembly loaded in oldAssemblies)
            {
                TryDeleteDirectory(loaded.RuntimeDirectory);
            }

            Discover();
            LoadDiscoveredSkills();

            return
                "AIGRAV Skill Reload completed.\\n\\n" +
                GetStatus();
        }
        catch (Exception ex)
        {
            return
                "AIGRAV Skill Reload failed:\\n\\n" +
                ex;
        }
    }

    private static void LoadDiscoveredSkills()
    {
        string[] files;

        lock (Sync)
        {
            files = DiscoveredSkillFiles.ToArray();
        }

        foreach (string file in files)
        {
            LoadSkillAssembly(file);
        }
    }

    private static void LoadSkillAssembly(string sourceAssemblyPath)
    {
        SkillLoadContext? loadContext = null;
        string runtimeDirectory = string.Empty;

        try
        {
            if (!File.Exists(sourceAssemblyPath))
            {
                throw new FileNotFoundException(
                    "External skill DLL was not found.",
                    sourceAssemblyPath);
            }

            string skillFolderName =
                SanitizePathPart(
                    Path.GetFileNameWithoutExtension(sourceAssemblyPath));

            runtimeDirectory = Path.Combine(
                RuntimeRootDirectory,
                skillFolderName,
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(runtimeDirectory);

            string runtimeAssemblyPath =
                Path.Combine(
                    runtimeDirectory,
                    Path.GetFileName(sourceAssemblyPath));

            // Shadow-copy the source DLL. Revit loads only this runtime copy.
            File.Copy(
                sourceAssemblyPath,
                runtimeAssemblyPath,
                overwrite: false);

            loadContext = new SkillLoadContext(runtimeAssemblyPath);

            Assembly assembly =
                loadContext.LoadFromAssemblyPath(runtimeAssemblyPath);

            Type[] types = assembly.GetTypes();
            List<string> registeredNames = new();

            foreach (Type type in types)
            {
                if (!typeof(IAIGRAVSkill).IsAssignableFrom(type)
                    || type.IsAbstract
                    || type.IsInterface
                    || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                if (Activator.CreateInstance(type) is not IAIGRAVSkill skill)
                {
                    continue;
                }

                AIGRAVSkillRegistry.Register(skill);
                registeredNames.Add(skill.Info.Name);
            }

            if (registeredNames.Count == 0)
            {
                loadContext.Unload();
                TryDeleteDirectory(runtimeDirectory);
                return;
            }

            LoadedSkillAssembly loaded = new()
            {
                SourceAssemblyPath = sourceAssemblyPath,
                RuntimeAssemblyPath = runtimeAssemblyPath,
                RuntimeDirectory = runtimeDirectory,
                LoadContext = loadContext
            };

            loaded.SkillNames.AddRange(registeredNames);

            lock (Sync)
            {
                LoadedAssemblies.Add(loaded);
            }
        }
        catch (Exception ex)
        {
            loadContext?.Unload();

            if (!string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                TryDeleteDirectory(runtimeDirectory);
            }

            AIGRAVSkillLoadErrors.Add(
                $"{Path.GetFileName(sourceAssemblyPath)}: {ex.Message}");
        }
    }

    private static string SanitizePathPart(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(value)
            ? "Skill"
            : value;
    }

    private static void TryDeleteDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)
            || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // A stale runtime copy is harmless. It can be cleaned on a later
            // reload or manually from the user's temp directory.
        }
    }

    private static class AIGRAVSkillLoadErrors
    {
        private static readonly List<string> Errors = new();

        public static void Add(string message)
        {
            lock (Errors)
            {
                Errors.Add(message);
            }
        }

        public static IReadOnlyList<string> GetAll()
        {
            lock (Errors)
            {
                return Errors.ToList();
            }
        }

        public static void Clear()
        {
            lock (Errors)
            {
                Errors.Clear();
            }
        }
    }

    public static IReadOnlyList<string> GetDiscoveredSkills()
    {
        lock (Sync)
        {
            return DiscoveredSkillFiles.AsReadOnly();
        }
    }

    public static string GetDiscoveryReport()
    {
        IReadOnlyList<string> discovered = GetDiscoveredSkills();
        IReadOnlyList<string> errors = AIGRAVSkillLoadErrors.GetAll();

        string discoveredText = discovered.Count == 0
            ? "- No external skill DLL found."
            : string.Join(
                Environment.NewLine,
                discovered.Select(file =>
                    $"- {Path.GetRelativePath(SkillsDirectory, file)}"));

        string errorText = errors.Count == 0
            ? "None"
            : string.Join(
                Environment.NewLine,
                errors.Select(error => $"- {error}"));

        return
            $"Skill Directory: {SkillsDirectory}\\n" +
            $"Runtime Cache: {RuntimeRootDirectory}\\n\\n" +
            $"Discovered DLLs: {discovered.Count}\\n" +
            discoveredText +
            "\\n\\nLoad Errors:\\n" +
            errorText;
    }

    public static string GetStatus()
    {
        if (string.IsNullOrWhiteSpace(SkillsDirectory))
        {
            return "Skill loader is not initialized.";
        }

        return
            $"Skill Directory: {SkillsDirectory}\\n" +
            $"Runtime Cache: {RuntimeRootDirectory}\\n" +
            $"Discovered external DLLs: {DiscoveredSkillFiles.Count}\\n" +
            $"Loaded external assemblies: {LoadedAssemblies.Count}\\n\\n" +
            AIGRAVSkillRegistry.GetStatus();
    }
}
