using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.CommandLine;
using BootstrapperShared;
using Spectre.Console;
using cbuild;
using System.Text;
using Semver;

class FileOperationWrapper(bool dryRun)
{

    public void CreateDirectory(string path)
    {
        if (dryRun)
        {
            AnsiConsole.MarkupLine($"[grey]Would create directory: {path}[/]");
        }
        else
        {
            Directory.CreateDirectory(path);
        }
    }

    public void WriteAllText(string path, string contents)
    {
        if (dryRun)
        {
            AnsiConsole.MarkupLine($"[grey]Would write file: {path}[/]");
        }
        else
        {
            File.WriteAllText(path, contents, encoding: Encoding.UTF8);
        }
    }

    public void CreateZip(string sourceDir, string destinationPath)
    {
        if (dryRun)
        {
            AnsiConsole.MarkupLine($"[grey]Would create zip file: {destinationPath}[/]");
        }
        else
        {
            ZipFile.CreateFromDirectory(sourceDir, destinationPath);
        }
    }

    public string CalculateFileHash(string filePath)
    {
        if (dryRun)
        {
            return $"SIMULATED_HASH_{Path.GetFileName(filePath)}";
        }
        else
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}

class Program
{
    static async Task<int> Main(string[] args)
    {
        var inputOption = new Option<string>(
            name: "--input",
            description: "Input directory containing the release files")
        { IsRequired = true };

        var branchOption = new Option<string>(
            name: "--branch",
            description: "Branch name (e.g., main, beta)")
        { IsRequired = true };

        var versionOption = new Option<string>(
            name: "--release-version",
            description: "Release version number (semver)")
        { IsRequired = true };

        var outputOption = new Option<string>(
            name: "--output",
            description: "Output directory for release artifacts and catalog")
        { IsRequired = true };

        var dryRunOption = new Option<bool>(
            name: "--dry-run",
            description: "Simulate the process without writing any files",
            getDefaultValue: () => false);

        var rootCommand = new RootCommand
        {
            inputOption,
            branchOption,
            versionOption,
            outputOption,
            dryRunOption
        };

        rootCommand.Description = "Generate a catalog and create release artifacts for app updates";

        rootCommand.SetHandler((string input, string branch, string version, string output, bool dryRun) =>
        {
            try
            {
                GenerateCatalogAndRelease(input, branch, version, output, dryRun);
                return Task.FromResult(0);
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Panel(new Text($"Error: {ex.Message}").Centered())
                    .Expand()
                    .BorderColor(Color.Red)
                    .Header("[red]Error Occurred[/]"));
                return Task.FromResult(1);
            }
        },
        inputOption, branchOption, versionOption, outputOption, dryRunOption);

        return await rootCommand.InvokeAsync(args);
    }

    static void GenerateCatalogAndRelease(string input, string branch, string version, string output, bool dryRun)
    {
        var fileOps = new FileOperationWrapper(dryRun);

        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("Catalog Generator").Centered().Color(Color.Cyan1));
        AnsiConsole.WriteLine();

        var settingsTable = new Table().Centered().BorderColor(Color.Grey);
        settingsTable.AddColumn("Setting", c => c.Centered());
        settingsTable.AddColumn("Value", c => c.Centered());
        settingsTable.AddRow("Input", input);
        settingsTable.AddRow("Branch", branch);
        settingsTable.AddRow("Version", version);
        settingsTable.AddRow("Output", output);
        settingsTable.AddRow("Dry Run", dryRun.ToString());
        AnsiConsole.Write(settingsTable);
        AnsiConsole.WriteLine();

        if (dryRun)
        {
            AnsiConsole.MarkupLine("[yellow]DRY RUN: No files will be written.[/]");
            AnsiConsole.WriteLine();
        }

        ValidateInputs(input, output);

        string catalogPath = Path.Combine(output, "catalog.json");
        var catalog = LoadOrCreateCatalog(catalogPath, fileOps);

        UpdateCatalog(catalog, branch, version);

        string releaseDir = Path.Combine(output, "releases", branch);
        fileOps.CreateDirectory(releaseDir);

        string zipPath = Path.Combine(releaseDir, $"v{version}.zip");
        string releasePath = $"/releases/{branch}/v{version}.zip";

        fileOps.CreateZip(input, zipPath);

        var files = HashFilesAndCheckSignatures(input, fileOps);

        var versionInfo = new VersionInfo(releasePath, fileOps.CalculateFileHash(zipPath), files, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
        catalog.Branches[branch].Versions[version] = versionInfo;
        catalog.Branches[branch] = catalog.Branches[branch] with { CurrentVersion = version };

        SaveCatalog(catalogPath, catalog, fileOps);

        DisplaySummary(catalog, branch, files.Count, dryRun);
    }

    static void ValidateInputs(string input, string output)
    {
        AnsiConsole.Status()
            .Start("Validating inputs...", ctx =>
            {
                if (string.IsNullOrEmpty(input))
                    throw new ArgumentException("Input directory is required.", nameof(input));
                if (!Directory.Exists(input))
                    throw new DirectoryNotFoundException($"Input directory not found: {input}");
                ctx.Status("Inputs validated successfully");
            });
    }

    static Catalog LoadOrCreateCatalog(string catalogPath, FileOperationWrapper fileOps)
    {
        return AnsiConsole.Status()
            .Start("Loading catalog...", ctx =>
            {
                if (File.Exists(catalogPath))
                {
                    try
                    {
                        var existingCatalog = File.ReadAllText(catalogPath);
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            Converters = { new CatalogJsonConverter() }
                        };
                        var catalog = JsonSerializer.Deserialize<Catalog>(existingCatalog, options);
                        if (catalog == null)
                            throw new JsonException("Deserialized catalog is null.");
                        ctx.Status("Existing catalog loaded successfully");
                        return catalog;
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine("[yellow]Warning: Failed to load existing catalog. Creating new one.[/]");
                        AnsiConsole.WriteException(ex);
                        return new Catalog(new Dictionary<string, BranchInfo>());
                    }
                }
                ctx.Status("Creating new catalog");
                return new Catalog(new Dictionary<string, BranchInfo>());
            });
    }

    static void UpdateCatalog(Catalog catalog, string branch, string version)
    {
        AnsiConsole.Status()
            .Start("Updating catalog...", ctx =>
            {
                if (!catalog.Branches.ContainsKey(branch))
                {
                    catalog.Branches[branch] = new BranchInfo(branch, version, new Dictionary<string, VersionInfo>());
                    ctx.Status($"Added new branch: {branch}");
                }

                var branchInfo = catalog.Branches[branch];

                if (branchInfo.Versions.ContainsKey(version))
                {
                    throw new InvalidOperationException($"Version {version} already exists for branch {branch}. Cannot overwrite existing version.");
                }

                ctx.Status("Catalog updated successfully");
            });
    }

    static List<CatalogFile> HashFilesAndCheckSignatures(string input, FileOperationWrapper fileOps)
    {
        var files = new List<CatalogFile>();
        var fileCount = Directory.GetFiles(input, "*", SearchOption.AllDirectories).Length;
        var unsignedExecutables = new List<string>();

        AnsiConsole.Progress()
            .Start(ctx =>
            {
                var task = ctx.AddTask("[green]Processing files[/]", maxValue: fileCount);

                foreach (var file in Directory.GetFiles(input, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(input, file);

                    if (PeNet.PeFile.IsPeFile(file))
                    {
                        bool isSigned = FileSignatureValidator.IsFileSigned(file);
                        if (!isSigned)
                        {
                            unsignedExecutables.Add(relativePath);
                        }
                    }

                    files.Add(new CatalogFile(relativePath, fileOps.CalculateFileHash(file)));
                    task.Increment(1);
                }
            });

        if (unsignedExecutables.Count > 0)
        {
            AnsiConsole.Write(new Rule("[yellow]Warning: Unsigned Executables Found[/]").RuleStyle("yellow"));
            var table = new Table().BorderColor(Color.Yellow);
            table.AddColumn("Unsigned File");
            foreach (var file in unsignedExecutables)
            {
                table.AddRow(file);
            }
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        return files;
    }

    // Add this method to your Program class
    static void SortVersionsInCatalog(Catalog catalog)
    {
        foreach (var branch in catalog.Branches.Values)
        {
            var sortedVersions = branch.Versions
                .OrderByDescending(kvp => SemVersion.Parse(kvp.Key, SemVersionStyles.Strict))
                .ToList();

            branch.Versions.Clear();
            foreach (var kvp in sortedVersions)
            {
                branch.Versions.Add(kvp.Key, kvp.Value);
            }
        }
    }

    static void SaveCatalog(string catalogPath, Catalog catalog, FileOperationWrapper fileOps)
    {
        AnsiConsole.Status()
            .Start("Saving catalog...", ctx =>
            {
                ctx.Status("Sorting versions...");
                SortVersionsInCatalog(catalog);
                try
                {
                    ctx.Status("Serializing catalog...");
                    var json = JsonSerializer.Serialize(catalog, SourceGenerationContext.Default.Catalog);
                    ctx.Status("Writing catalog to file...");
                    fileOps.WriteAllText(catalogPath, json);
                    ctx.Status("Catalog saved successfully");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to save catalog: {ex.Message}", ex);
                }
            });
    }

    static void DisplaySummary(Catalog catalog, string currentBranch, int fileCount, bool dryRun)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold cyan]Summary[/]").RuleStyle("grey").LeftJustified());

        var summaryTable = new Table().BorderColor(Color.Grey);
        summaryTable.AddColumn("Metric", c => c.PadRight(4));
        summaryTable.AddColumn("Value", c => c.PadLeft(4));

        summaryTable.AddRow("Total Files", fileCount.ToString());
        summaryTable.AddRow("Branches", catalog.Branches.Count.ToString());
        summaryTable.AddRow("Versions in Current Branch", catalog.Branches[currentBranch].Versions.Count.ToString());

        AnsiConsole.Write(summaryTable);

        AnsiConsole.WriteLine();
        if (dryRun)
        {
            AnsiConsole.Write(new Panel(new Text("Dry run completed successfully. No files were written.").Centered())
                .Expand()
                .BorderColor(Color.Yellow)
                .Header("[yellow]Dry Run Completed[/]"));
        }
        else
        {
            AnsiConsole.Write(new Panel(new Text("Catalog generation completed successfully!").Centered())
                .Expand()
                .BorderColor(Color.Green)
                .Header("[green]Success[/]"));
        }
    }
}