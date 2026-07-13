# Infrastructure/Persistence/Database/deploy-local.ps1
# Uso:  .\deploy-local.ps1                          (instância padrão, auth do Windows)
#       .\deploy-local.ps1 -Server "localhost\SQLEXPRESS"
#       .\deploy-local.ps1 -Server "localhost,1433" -User sa -Password "SuaSenha"
param(
    [string]$Server   = "localhost",
    [string]$User     = "",
    [string]$Password = ""
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

$authArgs = if ($User) { @("-U", $User, "-P", $Password) } else { @("-E") }

# Pastas em ordem alfabética => ordem de execução garantida pela numeração
$scripts = Get-ChildItem -Path $root -Recurse -Filter *.sql | Sort-Object FullName

foreach ($script in $scripts) {
    Write-Host "Aplicando $($script.FullName.Substring($root.Length + 1))..." -ForegroundColor Cyan
    & sqlcmd -S $Server @authArgs -b -i $script.FullName
    if ($LASTEXITCODE -ne 0) { throw "Falha ao aplicar $($script.Name)" }
}

Write-Host "`nBanco ReportsDb atualizado com sucesso." -ForegroundColor Green
