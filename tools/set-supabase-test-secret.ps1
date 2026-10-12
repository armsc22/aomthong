$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot '..\GoldShopWeb.csproj'
$securePassword = Read-Host 'Supabase database password (hidden)' -AsSecureString
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
try {
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    if ([string]::IsNullOrWhiteSpace($plainPassword)) { throw 'Password must not be empty.' }
    $connectionBuilder = New-Object System.Data.Common.DbConnectionStringBuilder
    $connectionBuilder['Host'] = 'aws-0-ap-northeast-1.pooler.supabase.com'
    $connectionBuilder['Port'] = 6543
    $connectionBuilder['Database'] = 'postgres'
    $connectionBuilder['Username'] = 'postgres.gallmxfsroubhdxpkvld'
    $connectionBuilder['Password'] = $plainPassword
    $connectionBuilder['SSL Mode'] = 'VerifyFull'
    $certificatePath = Join-Path $PSScriptRoot '..\App_Data\certificates\supabase-ca.crt'
    if (Test-Path -LiteralPath $certificatePath) {
        $connectionBuilder['Root Certificate'] = (Resolve-Path -LiteralPath $certificatePath).Path
    }
    $connectionBuilder['Timeout'] = 15
    $connectionBuilder['Max Auto Prepare'] = 0
    @{ 'ConnectionStrings:SupabaseTest' = $connectionBuilder.ConnectionString } |
        ConvertTo-Json -Compress | dotnet user-secrets set --project $projectPath
    if ($LASTEXITCODE -ne 0) { throw 'Could not save the connection settings.' }
    Write-Host 'Saved SupabaseTest connection settings. The current SQLite database is unchanged.'
    Write-Host 'Connection and PostgreSQL migration still need to be tested.'
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    $plainPassword = $null
    $connectionBuilder = $null
    $securePassword.Dispose()
}
