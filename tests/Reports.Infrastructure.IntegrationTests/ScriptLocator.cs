namespace Reports.Infrastructure.IntegrationTests;

/// <summary>
/// Localiza a pasta versionada <c>src/Reports.Infrastructure/Persistence/Database</c>
/// subindo a partir do diretório de execução até achar a solução.
/// </summary>
internal static class ScriptLocator
{
    public static IReadOnlyList<string> GetOrderedScripts()
    {
        var databaseDir = Path.Combine(FindRepositoryRoot(), "src", "Reports.Infrastructure", "Persistence", "Database");

        return Directory
            .GetFiles(databaseDir, "*.sql", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (dir.EnumerateFiles("Reports.slnx").Any())
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Não foi possível localizar a raiz do repositório (Reports.slnx).");
    }
}
