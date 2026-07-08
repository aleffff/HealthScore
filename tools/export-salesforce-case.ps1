[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$CaseNumber,
    [string]$EnvFile = (Join-Path $PSScriptRoot "..\.env"),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\artifacts\salesforce")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Import-DotEnv {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { throw "Arquivo de ambiente nao encontrado: $Path" }

    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith("#")) { continue }
        $separator = $trimmed.IndexOf("=")
        if ($separator -lt 1) { continue }
        $name = $trimmed.Substring(0, $separator).Trim()
        $value = $trimmed.Substring($separator + 1).Trim()
        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or
            ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }
        if (-not [Environment]::GetEnvironmentVariable($name, "Process")) {
            [Environment]::SetEnvironmentVariable($name, $value, "Process")
        }
    }
}

function Get-RequiredEnvironmentVariable {
    param([Parameter(Mandatory = $true)][string]$Name)
    $value = [Environment]::GetEnvironmentVariable($Name, "Process")
    if ([string]::IsNullOrWhiteSpace($value)) { throw "Variavel obrigatoria nao configurada: $Name" }
    return $value
}

function Invoke-SalesforceGet {
    param([Parameter(Mandatory = $true)][string]$Url)
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{
        Authorization = "Bearer $script:AccessToken"
        Accept = "application/json"
    }
}

function Get-CaseFieldBatch {
    param(
        [Parameter(Mandatory = $true)][string[]]$Fields,
        [Parameter(Mandatory = $true)][string]$WhereValue
    )
    $soql = "SELECT $($Fields -join ',') FROM Case WHERE CaseNumber = '$WhereValue' LIMIT 1"
    $url = "$script:InstanceUrl/services/data/$script:ApiVersion/query?q=$([Uri]::EscapeDataString($soql))"
    try {
        $response = Invoke-SalesforceGet -Url $url
        if ($response.totalSize -eq 0) { throw "Chamado $CaseNumber nao encontrado ou nao acessivel." }
        return $response.records[0]
    }
    catch {
        if ($Fields.Count -gt 1) {
            $middle = [Math]::Floor($Fields.Count / 2)
            $left = Get-CaseFieldBatch -Fields $Fields[0..($middle - 1)] -WhereValue $WhereValue
            $right = Get-CaseFieldBatch -Fields $Fields[$middle..($Fields.Count - 1)] -WhereValue $WhereValue
            return @($left, $right)
        }
        $script:SkippedFields.Add([pscustomobject]@{ name = $Fields[0]; error = $_.Exception.Message }) | Out-Null
        return @()
    }
}

Import-DotEnv -Path (Resolve-Path -LiteralPath $EnvFile)
$loginUrl = (Get-RequiredEnvironmentVariable "SALESFORCE_LOGIN_URL").TrimEnd("/")
$clientId = Get-RequiredEnvironmentVariable "SALESFORCE_CLIENT_ID"
$clientSecret = Get-RequiredEnvironmentVariable "SALESFORCE_CLIENT_SECRET"
$script:ApiVersion = Get-RequiredEnvironmentVariable "SALESFORCE_API_VERSION"

Write-Host "Autenticando no Salesforce..."
$token = Invoke-RestMethod -Method Post -Uri "$loginUrl/services/oauth2/token" -ContentType "application/x-www-form-urlencoded" -Body @{
    grant_type = "client_credentials"
    client_id = $clientId
    client_secret = $clientSecret
}
$script:AccessToken = $token.access_token
$script:InstanceUrl = $token.instance_url.TrimEnd("/")

Write-Host "Obtendo metadados de Case..."
$describe = Invoke-SalesforceGet -Url "$script:InstanceUrl/services/data/$script:ApiVersion/sobjects/Case/describe"
$fieldNames = @($describe.fields | ForEach-Object { $_.name } | Sort-Object -Unique)
$escapedCaseNumber = $CaseNumber.Replace("\", "\\").Replace("'", "\'")
$script:SkippedFields = [System.Collections.Generic.List[object]]::new()
$record = [ordered]@{}

Write-Host "Consultando $($fieldNames.Count) campos em lotes..."
for ($offset = 0; $offset -lt $fieldNames.Count; $offset += 60) {
    $last = [Math]::Min($offset + 59, $fieldNames.Count - 1)
    $results = @(Get-CaseFieldBatch -Fields @($fieldNames[$offset..$last]) -WhereValue $escapedCaseNumber)
    foreach ($result in $results) {
        if ($null -eq $result) { continue }
        foreach ($property in $result.PSObject.Properties) {
            if ($property.Name -ne "attributes") { $record[$property.Name] = $property.Value }
        }
    }
}

if (-not $record.Contains("Id")) { throw "Nao foi possivel exportar o chamado $CaseNumber." }
$fieldMetadata = @($describe.fields | ForEach-Object {
    [ordered]@{
        name = $_.name
        label = $_.label
        type = $_.type
        calculated = $_.calculated
        custom = $_.custom
        value = if ($record.Contains($_.name)) { $record[$_.name] } else { $null }
    }
})

$safeCaseNumber = $CaseNumber -replace '[^A-Za-z0-9_-]', '_'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$recordPath = Join-Path $OutputDirectory "case-$safeCaseNumber.json"
$metadataPath = Join-Path $OutputDirectory "case-$safeCaseNumber-fields.json"
[ordered]@{
    exportedAtUtc = [DateTime]::UtcNow.ToString("o")
    object = "Case"
    caseNumber = $CaseNumber
    record = $record
    skippedFields = @($script:SkippedFields)
} | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $recordPath -Encoding utf8
[ordered]@{
    exportedAtUtc = [DateTime]::UtcNow.ToString("o")
    object = "Case"
    caseNumber = $CaseNumber
    fields = $fieldMetadata
} | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $metadataPath -Encoding utf8

Write-Host "Exportacao concluida."
Write-Host "Chamado:   $recordPath"
Write-Host "Campos:    $metadataPath"
Write-Host "Ignorados: $($script:SkippedFields.Count)"
Write-Host "Os arquivos podem conter dados de clientes e nao devem ser versionados."
