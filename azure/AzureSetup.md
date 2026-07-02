# Azure First Time Setup

## Azure CLI
`winget install Microsoft.AzureCLI`

Restart terminal

`az login` 

## Resource Providers
```
az provider register --namespace Microsoft.ContainerRegistry   # Container Registry
az provider register --namespace Microsoft.DBforPostgreSQL     # Postgres
az provider register --namespace Microsoft.Cache               # Redis
az provider register --namespace Microsoft.App                 # Container Apps
az provider register --namespace Microsoft.OperationalInsights # Log Analytics
az provider register --namespace Microsoft.KeyVault            # Key Vault
```

Register in background. Confirm Registered before continuing.

Check status with:

`az provider show --namespace <resourceProvider> --query registrationState`
 
## Resource group
`az group create --name didakt-rg --location centralus`
 
## Azure Container Registry
`az acr create --resource-group didakt-rg --name didaktacr --sku Basic`
 
## Postgres Flexible Server
```
az postgres flexible-server create `
  --resource-group didakt-rg `
  --name didakt-postgres `
  --location centralus `
  --admin-user didakt_admin `
  --admin-password <"your-password"> `
  --sku-name Standard_B1ms `
  --tier Burstable `
  --storage-size 32 `
  --version 17 `
  --public-access 0.0.0.0
  ```
 
## Database
`az postgres flexible-server db create --resource-group didakt-rg --server-name didakt-postgres --name didakt`
 
## Redis Enterprise CLI Extension
`az extension add --name redisenterprise`
 
## Azure Managed Redis
```
az redisenterprise create `
  --name didakt-redis `
  --resource-group didakt-rg `
  --location centralus `
  --sku Balanced_B1 `
  --public-network-access Enabled
```
 
## Container App CLI Extension
`az extension add --name containerapp --upgrade`
 
## Container Apps Environment
`az containerapp env create --name didakt-env --resource-group didakt-rg --location centralus`
