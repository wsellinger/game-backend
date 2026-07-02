# ==== Azure CLI check/install ====

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Host "Azure CLI not found. Installing via winget..."
    winget install --exact --id Microsoft.AzureCLI
    Write-Host "Azure CLI installed. Please close and reopen your terminal, then re-run this script."
    exit
}

# ==== Azure login check ====

$account = az account show 2>$null
if (-not $account) {
    Write-Host "Not logged in to Azure. Opening browser to log in..."
    az login
} else {
    $accountInfo = $account | ConvertFrom-Json
    Write-Host "Already logged in as $($accountInfo.user.name) (subscription: $($accountInfo.name))."
}

# ==== Postgres admin password (prompted, not hardcoded) ====

$securePassword = Read-Host "Enter Postgres admin password" -AsSecureString
$bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
$pgPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
[System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)

# ==== Resource Providers ====

$providers = @(
    "Microsoft.ContainerRegistry",
    "Microsoft.DBforPostgreSQL",
    "Microsoft.Cache",
    "Microsoft.App",
    "Microsoft.OperationalInsights",
    "Microsoft.KeyVault"
)

foreach ($provider in $providers) {
    Write-Host "Registering $provider..."
    az provider register --namespace $provider | Out-Null
}

foreach ($provider in $providers) {
    do {
        $state = az provider show --namespace $provider --query registrationState -o tsv
        if ($state -ne "Registered") {
            Write-Host "Waiting on $provider (current: $state)..."
            Start-Sleep -Seconds 10
        }
    } while ($state -ne "Registered")
    Write-Host "$provider Registered."
}

# ==== Resource Group ====

az group create --name didakt-rg --location centralus

# ==== Azure Container Registry ====

az acr create --resource-group didakt-rg --name didaktacr --sku Basic

# ==== Postgres Flexible Server ====

az postgres flexible-server create `
  --resource-group didakt-rg `
  --name didakt-postgres `
  --location centralus `
  --admin-user didakt_admin `
  --admin-password $pgPassword `
  --sku-name Standard_B1ms `
  --tier Burstable `
  --storage-size 32 `
  --version 17 `
  --public-access 0.0.0.0

# ==== Database ====

az postgres flexible-server db create --resource-group didakt-rg --server-name didakt-postgres --name didakt

# ==== Redis Enterprise CLI Extension ====

az extension add --name redisenterprise

# ==== Azure Managed Redis ====

az redisenterprise create `
  --name didakt-redis `
  --resource-group didakt-rg `
  --location centralus `
  --sku Balanced_B1 `
  --public-network-access Enabled

# ==== Container App CLI Extension ====

az extension add --name containerapp --upgrade

# ==== Container Apps Environment ====

az containerapp env create --name didakt-env --resource-group didakt-rg --location centralus

Write-Host "All resources created."