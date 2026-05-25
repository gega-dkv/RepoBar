using System.Xml.Linq;
using Xunit;

namespace RepoBar.Tests;

public sealed class PackagingTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void PackageEnvironmentDefinesStableIdentityAndRoutes()
    {
        Dictionary<string, string> values = ReadEnv(Path.Combine(Root, "Windows", "Packaging", "package.env"));

        Assert.Equal("com.openclaw.repobar.windows", values["REPOBAR_WINDOWS_APP_ID"]);
        Assert.Equal("6C6D9F17-4A23-47F4-8E52-92AE57CF18C2", values["REPOBAR_WINDOWS_UPGRADE_CODE"]);
        Assert.Equal("wix-msi", values["REPOBAR_WINDOWS_PACKAGE_ROUTE"]);
        Assert.Equal("github-release-msi-upgrade", values["REPOBAR_WINDOWS_UPDATE_ROUTE"]);
    }

    [Fact]
    public void WixPackageUsesMajorUpgradePerUserInstallAndDoesNotRemoveUserData()
    {
        string path = Path.Combine(Root, "Windows", "Packaging", "Wix", "Package.wxs");
        XDocument document = XDocument.Load(path);
        XNamespace wix = "http://wixtoolset.org/schemas/v4/wxs";
        XElement package = document.Root!.Element(wix + "Package")!;

        Assert.Equal("perUser", package.Attribute("Scope")?.Value);
        Assert.NotNull(package.Element(wix + "MajorUpgrade"));
        Assert.Contains(document.Descendants(wix + "RegistryValue"), value => value.Attribute("Name")?.Value == "AppId");
        Assert.DoesNotContain(document.Descendants(wix + "RemoveFolder"), folder => folder.Attribute("Directory")?.Value?.Contains("AppData", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void PackagingScriptsContainPublishSigningValidationAndWixBuildSteps()
    {
        string publish = File.ReadAllText(Path.Combine(Root, "Windows", "Packaging", "Scripts", "publish-windows.ps1"));
        string package = File.ReadAllText(Path.Combine(Root, "Windows", "Packaging", "Scripts", "package-windows.ps1"));
        string sign = File.ReadAllText(Path.Combine(Root, "Windows", "Packaging", "Scripts", "sign-windows-artifacts.ps1"));
        string validate = File.ReadAllText(Path.Combine(Root, "Windows", "Packaging", "Scripts", "validate-windows-package.ps1"));

        Assert.Contains("dotnet publish", publish, StringComparison.Ordinal);
        Assert.Contains("--self-contained true", publish, StringComparison.Ordinal);
        Assert.Contains("PublishReadyToRun=true", publish, StringComparison.Ordinal);
        Assert.Contains("wix build", package, StringComparison.Ordinal);
        Assert.Contains("Compress-Archive", package, StringComparison.Ordinal);
        Assert.Contains("REPOBAR_SIGN_CERT_SHA1", sign, StringComparison.Ordinal);
        Assert.Contains("RepoBar.Desktop.exe", validate, StringComparison.Ordinal);
        Assert.Contains("RepoBar.Cli.exe", validate, StringComparison.Ordinal);
    }

    [Fact]
    public void WixFileListGeneratorPreservesNestedPublishDirectories()
    {
        string generator = File.ReadAllText(Path.Combine(Root, "Windows", "Packaging", "Scripts", "write-wix-file-list.ps1"));

        Assert.Contains("Get-ChildItem -Path $publishPath -Directory -Recurse", generator, StringComparison.Ordinal);
        Assert.Contains("<DirectoryRef Id=`\"$parentId`\">", generator, StringComparison.Ordinal);
        Assert.Contains("Directory=`\"$directoryId`\"", generator, StringComparison.Ordinal);
        Assert.Contains("$(var.PublishDir)\\", generator, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsReleaseDocsRequireManualRuntimeVerificationAndPreserveMacReleaseFlow()
    {
        string packagingReadme = File.ReadAllText(Path.Combine(Root, "Windows", "Packaging", "README.md"));
        string checklist = File.ReadAllText(Path.Combine(Root, "Windows", "Packaging", "windows-release-checklist.md"));
        string release = File.ReadAllText(Path.Combine(Root, "docs", "release.md"));

        Assert.Contains("MSIX remains deferred", packagingReadme, StringComparison.Ordinal);
        Assert.Contains("Windows Credential Manager", checklist, StringComparison.Ordinal);
        Assert.Contains("%AppData%\\RepoBar", checklist, StringComparison.Ordinal);
        Assert.Contains("%LocalAppData%\\RepoBar", checklist, StringComparison.Ordinal);
        Assert.Contains("without changing the macOS Sparkle/appcast flow", release, StringComparison.Ordinal);
        Assert.Contains("Do not port Sparkle to Windows", release, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> ReadEnv(string path) =>
        File.ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
            .Select(line => line.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "windows-implementation-phases.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
