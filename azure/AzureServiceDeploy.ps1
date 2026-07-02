<#
.SYNOPSIS
    Deploys a Didakt service to Azure Container Apps, wiring up Postgres
    and/or Redis and JWT secrets via Key Vault as needed.

.DESCRIPTION
    Assumes the shared infrastructure already exists: resource group, ACR,
    Container Apps environment, Key Vault, and (if used) the Postgres
    server / Redis cluster. This script only creates/updates resources
    specific to the one service being deployed.

    Secrets that are meant to be shared across services (currently just
    jwt-secret) are reused if they already exist in Key Vault rather than
    regenerated, so multiple services validating the same tokens actually
    share the same signing key.

.PARAMETER ServiceName
    Short name used for the Container App and image, e.g. "didakt-auth".

.PARAMETER ProjectDir
    Path to the service's project directory (contains the Dockerfile, and
    the .csproj if -UsePostgres is set and migrations need to run).

.PARAMETER UsePostgres
    Wires up the Postgres connection string as a Key Vault secret, opens
    firewall rules, and runs EF Core migrations from ProjectDir.

.PARAMETER UseRedis
    Prompts for a Redis connection string and wires it up as a Key Vault
    secret. (Connection details must be fetched separately — see notes.)

.PARAMETER UseJwt
    Wires up Jwt__Secret / Jwt__Issuer / Jwt__Audience. Reuses the existing
    jwt-secret in Key Vault if one exists.

.EXAMPLE
    .\deploy-service.ps1 -ServiceName didakt-auth -ProjectDir Didakt.Api.Auth -UsePostgres -UseJwt

.EXAMPLE
    .\deploy-service.ps1 -ServiceName didakt-leaderboard -ProjectDir Didakt.Api.Leaderboard -UseRedis -UseJwt
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ServiceName,

    [Parameter(Mandatory = $true)]
    [string]$ProjectDir,

    [switch]$UsePostgres,
    [switch]$UseRedis,
    [switch]$UseJwt,

    [string]$ResourceGroup = "didakt-rg",
    [string]$AcrName = "didaktacr",
    [string]$Environment = "didakt-env",
    [string]$KeyVaultName = "didakt-kv",
    [int]$TargetPort = 8080,

    # Only used if -UsePostgres
    [string]$PostgresServer = "didakt-postgres",
    [string]$PostgresDatabase = "didakt",
    [string]$PostgresAdminUser = "didakt_admin",

    # Jwt config values (only used if -UseJwt)
    [string]$JwtIssuer = "didakt-api",
    [string]$JwtAudience = "didakt-client",
    [string]$JwtExpiryMinutes = "15",
    [string]$JwtRefreshExpiryDays = "7"
)

$ErrorActionPreference = "Stop"

$subscriptionId = az account show --query id -o tsv
$vaultScope = "/subscriptions/$subscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.KeyVault/vaults/$KeyVaultName"

function Get-OrCreateSecureString {
    param([string]$Prompt)
    $secure = Read-Host $Prompt -AsSecureString
    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    $plain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    return $plain
}

function Wait-ForRoleAssignmentPropagation {
    Write-Host "Waiting for role assignment to propagate..."
    Start-Sleep -Seconds 30
}

function Grant-KeyVaultAccess {
    param([string]$PrincipalId, [string]$Role)
    az role assignment create `
        --role $Role `
        --assignee $PrincipalId `
        --scope $vaultScope | Out-Null
}

function Get-OrCreateVaultSecret {
    param([string]$SecretName, [scriptblock]$GenerateValue)

    $existing = az keyvault secret show --vault-name $KeyVaultName --name $SecretName --query value -o tsv 2>$null
    if ($existing) {
        Write-Host "Reusing existing Key Vault secret '$SecretName'."
        return $existing
    }

    Write-Host "Creating new Key Vault secret '$SecretName'."
    $value = & $GenerateValue
    az keyvault secret set --vault-name $KeyVaultName --name $SecretName --value $value | Out-Null
    return $value
}

# ==== Ensure Key Vault access for current user (needed to read/write secrets below) ====

$myObjectId = az ad signed-in-user show --query id -o tsv
$myRoleCheck = az role assignment list --assignee $myObjectId --scope $vaultScope --query "[?roleDefinitionName=='Key Vault Secrets Officer']" -o tsv
if (-not $myRoleCheck) {
    Write-Host "Granting yourself Key Vault Secrets Officer on $KeyVaultName..."
    Grant-KeyVaultAccess -PrincipalId $myObjectId -Role "Key Vault Secrets Officer"
    Wait-ForRoleAssignmentPropagation
}

# ==== ACR credentials ====

az acr update -n $AcrName --admin-enabled true | Out-Null
$acrCreds = az acr credential show --name $AcrName | ConvertFrom-Json
$acrUsername = $acrCreds.username
$acrPassword = $acrCreds.passwords[0].value

# ==== Build and push image ====

$imageTag = "$AcrName.azurecr.io/$ServiceName`:latest"

az acr login --name $AcrName
docker build -t $imageTag -f "$ProjectDir/Dockerfile" $ProjectDir
docker push $imageTag

# ==== Postgres setup ====

$pgSecretRef = $null
if ($UsePostgres) {
    Write-Host "`n=== Postgres setup ==="

    $myIp = (Resolve-DnsName myip.opendns.com -Server resolver1.opendns.com -Type A).IPAddress

    az postgres flexible-server firewall-rule create `
        --resource-group $ResourceGroup `
        --server-name $PostgresServer `
        --name AllowAllAzureServices `
        --start-ip-address 0.0.0.0 `
        --end-ip-address 0.0.0.0 2>$null | Out-Null

    az postgres flexible-server firewall-rule create `
        --resource-group $ResourceGroup `
        --server-name $PostgresServer `
        --name "AllowMyIP-$($env:COMPUTERNAME)" `
        --start-ip-address $myIp `
        --end-ip-address $myIp 2>$null | Out-Null

    $pgPassword = Get-OrCreateSecureString -Prompt "Enter Postgres admin password"
    $pgConnectionString = "Host=$PostgresServer.postgres.database.azure.com;Database=$PostgresDatabase;Username=$PostgresAdminUser;Password=$pgPassword;Ssl Mode=Require"

    Write-Host "Running EF Core migrations against Azure Postgres..."
    Push-Location $ProjectDir
    $env:ConnectionStrings__Postgres = $pgConnectionString
    dotnet ef database update
    Remove-Item Env:\ConnectionStrings__Postgres
    Pop-Location

    az keyvault secret set --vault-name $KeyVaultName --name "$ServiceName-pg-connection" --value $pgConnectionString | Out-Null
    $pgSecretRef = "$ServiceName-pg-connection"
}

# ==== Redis setup ====

$redisSecretRef = $null
if ($UseRedis) {
    Write-Host "`n=== Redis setup ==="
    Write-Host "Fetch the correct hostname/port and key with:"
    Write-Host "  az redisenterprise database show --cluster-name <cluster> --resource-group $ResourceGroup"
    Write-Host "  az redisenterprise database list-keys --cluster-name <cluster> --resource-group $ResourceGroup"
    Write-Host "(Do not guess the hostname pattern — use what these commands return.)"

    $redisConnectionString = Read-Host "Paste the full Redis connection string (host:port,password=...,ssl=True,abortConnect=False)"

    az keyvault secret set --vault-name $KeyVaultName --name "$ServiceName-redis-connection" --value $redisConnectionString | Out-Null
    $redisSecretRef = "$ServiceName-redis-connection"
}

# ==== JWT setup ====

$jwtSecretRef = $null
if ($UseJwt) {
    Write-Host "`n=== JWT setup ==="
    Get-OrCreateVaultSecret -SecretName "jwt-secret" -GenerateValue {
        [Convert]::ToBase64String((1..32 | ForEach-Object { [byte](Get-Random -Max 256) }))
    } | Out-Null
    $jwtSecretRef = "jwt-secret"
}

# ==== Create the Container App (bare — no secrets yet) ====

Write-Host "`n=== Creating Container App '$ServiceName' ==="

$envVarArgs = @()
if ($UseJwt) {
    $envVarArgs += "Jwt__Issuer=$JwtIssuer"
    $envVarArgs += "Jwt__Audience=$JwtAudience"
    $envVarArgs += "Jwt__ExpiryMinutes=$JwtExpiryMinutes"
    $envVarArgs += "Jwt__RefreshExpiryDays=$JwtRefreshExpiryDays"
}

az containerapp create `
    --name $ServiceName `
    --resource-group $ResourceGroup `
    --environment $Environment `
    --image $imageTag `
    --registry-server "$AcrName.azurecr.io" `
    --registry-username $acrUsername `
    --registry-password $acrPassword `
    --target-port $TargetPort `
    --ingress external `
    --env-vars $envVarArgs `
    --system-assigned

# ==== Grant the app's managed identity read access to Key Vault ====

$appPrincipalId = az containerapp identity show --name $ServiceName --resource-group $ResourceGroup --query principalId -o tsv
Grant-KeyVaultAccess -PrincipalId $appPrincipalId -Role "Key Vault Secrets User"
Wait-ForRoleAssignmentPropagation

# ==== Wire up secrets and env vars ====

$secretArgs = @()
$secretEnvVarArgs = @()

if ($pgSecretRef) {
    $secretArgs += "pg-connection=keyvaultref:https://$KeyVaultName.vault.azure.net/secrets/$pgSecretRef,identityref:system"
    $secretEnvVarArgs += "ConnectionStrings__Postgres=secretref:pg-connection"
}
if ($redisSecretRef) {
    $secretArgs += "redis-connection=keyvaultref:https://$KeyVaultName.vault.azure.net/secrets/$redisSecretRef,identityref:system"
    $secretEnvVarArgs += "ConnectionStrings__Redis=secretref:redis-connection"
}
if ($jwtSecretRef) {
    $secretArgs += "jwt-secret=keyvaultref:https://$KeyVaultName.vault.azure.net/secrets/$jwtSecretRef,identityref:system"
    $secretEnvVarArgs += "Jwt__Secret=secretref:jwt-secret"
}

if ($secretArgs.Count -gt 0) {
    az containerapp secret set `
        --name $ServiceName `
        --resource-group $ResourceGroup `
        --secrets $secretArgs

    az containerapp update `
        --name $ServiceName `
        --resource-group $ResourceGroup `
        --set-env-vars $secretEnvVarArgs
}

# ==== Verify ====

$fqdn = az containerapp show --name $ServiceName --resource-group $ResourceGroup --query properties.configuration.ingress.fqdn -o tsv
Write-Host "`n=== $ServiceName is live at: https://$fqdn ==="

Start-Sleep -Seconds 5
az containerapp logs show --name $ServiceName --resource-group $ResourceGroup --tail 30
