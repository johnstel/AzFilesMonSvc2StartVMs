# Azure File Share Monitor Service

## Overview

This service monitors an Azure File Share and manages corresponding Azure VMs based on the contents of specific folders. It ensures that VMs are started when necessary and logs relevant information.

## Features

- Polls Azure File Share at a configurable interval.
- Maps folders to Azure VMs.
- Starts VMs if they are stopped or deallocated.
- Logs VM states and file counts.
- Centralized logging compatible with CMTrace.exe.
- Secure interactions using Managed Identity and Azure Key Vault.
- Resilient to transient faults using Polly.
- Adheres to best practices for security and performance.

## Setup and Configuration

### Prerequisites

- .NET Core SDK installed.
- Azure subscription with necessary permissions.
- Azure resources: File Share, VMs, Key Vault.
- Managed Identity set up for the hosting environment (e.g., Azure VM, App Service).

### Configuration

1. **Azure Key Vault**: Store sensitive configurations in Key Vault.

   - Use the Azure CLI or Azure Portal to add secrets.
   - Secrets to store:

     - `AzureSettings--StorageAccountName`: `yourstorageaccount`
     - `AzureSettings--FileShareName`: `yourfileshare`
     - `FolderMappings--0--FolderName`: `Folder1`
     - `FolderMappings--0--VMName`: `VM1`
     - `FolderMappings--0--ResourceGroupName`: `ResourceGroup1`
     - `FolderMappings--0--SubscriptionId`: `your-subscription-id`

   - For additional folder mappings, increment the index:

     - `FolderMappings--1--FolderName`, etc.

2. **appsettings.json**: Update the configuration file.

   - Only include non-sensitive information.
   - Specify the Key Vault name.

     ```json
     {
       "PollingSettings": {
         "IntervalInSeconds": 300
       },
       "AzureSettings": {
         "KeyVaultName": "yourkeyvault"
       },
       "Logging": {
         "LogFilePath": "Logs\\AzureFileShareMonitorService.log"
       }
     }
     ```

3. **Managed Identity Permissions**:

   - Assign `get` and `list` permissions for secrets in Key Vault to the Managed Identity.
   - Assign necessary Azure RBAC permissions for managing VMs (e.g., `Virtual Machine Contributor` role on the target subscriptions).

### Build and Run

```bash
dotnet build
dotnet run
