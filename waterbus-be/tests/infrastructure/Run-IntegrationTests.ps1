param(
    [string]$SolutionPath = (Join-Path $PSScriptRoot '../../WaterbusSystem.sln'),
    [switch]$StartOnly,
    [switch]$AllTests
)

$ErrorActionPreference = 'Stop'
$solution = (Resolve-Path -LiteralPath $SolutionPath).Path
$backendRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$credentialsPath = Join-Path $backendRoot '.integration-tests.local.json'
$composePath = Join-Path $PSScriptRoot 'compose.yaml'

& docker info --format '{{.ServerVersion}}'
if ($LASTEXITCODE -ne 0) { throw 'Docker Engine is unavailable. Start Docker Desktop first.' }

# *.local.json is ignored by waterbus-be/.gitignore; never print the password.
if (!(Test-Path -LiteralPath $credentialsPath)) {
    $password = 'Wb!9' + [guid]::NewGuid().ToString('N')
    @{ SqlPassword = $password } | ConvertTo-Json | Set-Content -LiteralPath $credentialsPath -Encoding utf8
}
$credentials = Get-Content -LiteralPath $credentialsPath -Raw | ConvertFrom-Json
$savedPassword = $env:WATERBUS_INTEGRATION_SQL_PASSWORD
$savedSql = $env:WATERBUS_TEST_SQLSERVER
$savedRedis = $env:WATERBUS_TEST_REDIS
try {
    $env:WATERBUS_INTEGRATION_SQL_PASSWORD = $credentials.SqlPassword
    & docker compose --progress plain -f $composePath up -d --wait --wait-timeout 180
    if ($LASTEXITCODE -ne 0) { throw 'Test containers did not become healthy.' }
    if ($StartOnly) { return }

    # Docker binds IPv4 loopback; avoid localhost resolving to IPv6 ::1 on Windows.
    $env:WATERBUS_TEST_SQLSERVER = "Server=tcp:127.0.0.1,11433;User Id=sa;Password=$($credentials.SqlPassword);TrustServerCertificate=True;MultipleActiveResultSets=true"
    $env:WATERBUS_TEST_REDIS = '127.0.0.1:16379;127.0.0.1:16380;127.0.0.1:16381'
    $testArguments = @('test', $solution, '--verbosity', 'minimal')
    if (!$AllTests) {
        $testArguments += @('--filter', 'FullyQualifiedName~DomainPersistenceTests|FullyQualifiedName~CommercialSqlTests|FullyQualifiedName~RedisCommercialTests')
    }
    & dotnet @testArguments
    if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed; inspect test output.' }
}
finally {
    $env:WATERBUS_INTEGRATION_SQL_PASSWORD = $savedPassword
    $env:WATERBUS_TEST_SQLSERVER = $savedSql
    $env:WATERBUS_TEST_REDIS = $savedRedis
}
