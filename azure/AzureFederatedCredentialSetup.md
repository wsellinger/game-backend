# Azure OIDC Federated Credential Setup

## Create App Registration
 
```bash
az ad app create --display-name "didakt-github-actions"
```
 
Output `appId` == Client ID
 
## Create Service Principal
 
```bash
az ad sp create --id <client-id>
```
 
This links a service principal to the app registration. 

Output `id` == Service Principal Object ID
 
## Grant Contributor Role
 
```bash
# Get Subscription Id
az account show --query id -o tsv

# Create Role
az role assignment create `
  --role "Contributor" `
  --assignee-object-id <service-principal-object-id> `
  --assignee-principal-type ServicePrincipal `
  --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group>
```
 
`Contributor` = can create/modify/delete resources within the group, but
cannot manage access control or subscription-level resources.
 
## Create Federated Credential
 
Write parameters to temp file to avoid PowerShell JSON quoting
issues:
 
```powershell
@'
{
  "name": "didakt-api-main-branch",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<github-org>/<repo-name>:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}
'@ | Out-File -FilePath "$env:TEMP\federated-credential.json" -Encoding ascii
 
az ad app federated-credential create `
  --id <appId> `
  --parameters "$env:TEMP\federated-credential.json"
```

## Add Identifiers as GitHub Repo Secrets
 
Settings > Secrets and Variables > Actions > New Repository Secret
 
| Secret name | Value | From |
|---|---|---|
| `AZURE_CLIENT_ID` | Client ID | `az ad app list --display-name "didakt-github-actions" --query "[0].appId" -o tsv` |
| `AZURE_TENANT_ID` | Tenant ID | `az account show --query tenantId -o tsv` |
| `AZURE_SUBSCRIPTION_ID` | Subscription ID | `az account show --query id -o tsv` |