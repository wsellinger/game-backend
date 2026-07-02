# Azure Deploy Service

Instructions to deploy a service to Azure for the first time after infrastructure setup is complete.

## Login

```
az login
```

## Admin Credentials 

Enable admin credentials and then retrieve them

```
az acr update -n didaktacr --admin-enabled true
az acr credential show --name didaktacr
```

## Link Docker Auth to Azure

Link local Docker CLI to Azure login to reduce need for credentials 

```
az acr login --name didaktacr
```

## Build and Push Service Image

```
docker build -t didaktacr.azurecr.io/<serviceImageTag>:latest -f Didakt.Api.Auth/Dockerfile <serviceDirectory>
docker push didaktacr.azurecr.io/<serviceImageTag>:latest
```

## Postgres

### Open Firewall 

#### Azure-internal (Container Apps)

```
az postgres flexible-server firewall-rule create `
  --resource-group didakt-rg `
  --server-name didakt-postgres `
  --name AllowAllAzureServices `
  --start-ip-address 0.0.0.0 `
  --end-ip-address 0.0.0.0
```

#### Get Public IP

```
curl.exe -s ifconfig.me
```

#### Local Machine

```
az postgres flexible-server firewall-rule create `
  --resource-group didakt-rg `
  --server-name didakt-postgres `
  --name AllowMyIP `
  --start-ip-address <your-public-ip> `
  --end-ip-address <your-public-ip>
```

### Migration

```
cd Didakt.Api.Auth
$env:ConnectionStrings__Postgres = "Host=didakt-postgres.postgres.database.azure.com;Database=didakt;Username=didakt_admin;Password=<password>;Ssl Mode=Require"
dotnet ef database update
```

## Container App

```
az containerapp create `
  --name $appName `
  --resource-group $rg `
  --environment $envName `
  --image "$acrName.azurecr.io/didakt-auth:latest" `
  --registry-server "$acrName.azurecr.io" `
  --registry-username $acrUsername `
  --registry-password $acrPassword `
  --target-port 8080 `
  --ingress external `
  --env-vars `
    "Jwt__Issuer=didakt-api" `
    "Jwt__Audience=didakt-client" `
    "Jwt__ExpiryMinutes=15" `
    "Jwt__RefreshExpiryDays=7" `
  --system-assigned
```

### Secrets

#### Generate JWT Secret

```
openssl rand -base64 32
```

#### Key Vault

```
az provider register --namespace Microsoft.KeyVault
az keyvault create --name didakt-kv --resource-group didakt-rg --location centralus
```

#### Get Ids

```
az ad signed-in-user show --query id -o tsv
az account show --query id -o tsv
```

#### Assign Secrets Roles

```
az role assignment create `
  --role "Key Vault Secrets Officer" `
  --assignee <your-object-id> `
  --scope /subscriptions/<sub-id>/resourceGroups/didakt-rg/providers/Microsoft.KeyVault/vaults/didakt-kv
  
az role assignment create `
  --role "Key Vault Secrets User" `
  --assignee <app-principal-id> `
  --scope "/subscriptions/$((az account show --query id -o tsv))/resourceGroups/$rg/providers/Microsoft.KeyVault/vaults/$kvName"
```

Role assignments may take 30-60s to propagate

#### Get Redis Keys

```
az redisenterprise database update `
   --cluster-name didakt-redis `
   --resource-group didakt-rg `
   --access-keys-authentication Enabled

az redisenterprise database list-keys `
   --cluster-name didakt-redis `
   --resource-group didakt-rg
```

#### Set Secrets

```
# Jwt
az keyvault secret set --vault-name didakt-kv --name jwt-secret --value "<jwt-secret>"

# Postgres
az keyvault secret set --vault-name didakt-kv --name pg-connection `
   --value "Host=didakt-postgres.postgres.database.azure.com;Database=didakt;Username=didakt_admin;Password=<pg-password>;Ssl Mode=Require"

# Redis
az keyvault secret set --vault-name didakt-kv --name redis-connection `
   --value "didakt-redis.centralus.redisenterprise.cache.azure.net:10000,password=<primary-key>=,ssl=True,abortConnect=False"
```

#### Set App Secrets and Env Vars

```
az containerapp secret set `
  --name $appName `
  --resource-group $rg `
  --secrets `
    "jwt-secret=keyvaultref:https://$kvName.vault.azure.net/secrets/jwt-secret,identityref:system" `
    "pg-connection=keyvaultref:https://$kvName.vault.azure.net/secrets/pg-connection,identityref:system" `
    "redis-connection=keyvaultref:https://didakt-kv.vault.azure.net/secrets/redis-connection,identityref:system"

az containerapp update `
  --name $appName `
  --resource-group $rg `
  --set-env-vars `
    "Jwt__Secret=secretref:jwt-secret" `
    "ConnectionStrings__Postgres=secretref:pg-connection" `
    "ConnectionStrings__Redis=secretref:redis-connection"
```