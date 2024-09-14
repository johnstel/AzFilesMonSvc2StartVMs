# Azure File Share Monitor Service

## Overview

The Azure File Share Monitor Service is a C# .NET Core application designed to automate the monitoring of Azure File Shares and manage Azure Virtual Machines (VMs) based on specific criteria. It ensures that VMs are started when necessary and logs relevant information while adhering to best practices for security, performance, and reliability.

## Features

- **File Share Monitoring**: Polls specified Azure File Share folders at a configurable interval (default every 5 minutes, minimum 30 seconds).
- **VM State Management**: Automatically starts VMs if they are stopped or deallocated; logs VM states and file counts.
- **Centralized Logging**: Logs all operations to a single file compatible with CMTrace.exe, with thread-safe logging mechanisms.
- **Security**: Uses Azure Key Vault for secure configuration management, Managed Identity for authentication, and supports Azure Private Link.
- **Resiliency**: Implements retry policies using Polly for transient faults and uses cancellation tokens for graceful shutdowns.
- **Configurability**: Configurations are managed through `appsettings.json` and Azure Key Vault, allowing easy adjustments without code changes.
- **Documentation and Best Practices**: Includes thorough inline comments, XML documentation, and follows SOLID principles.

## Setup and Configuration

### Prerequisites

- **.NET Core SDK**: Install the latest .NET Core SDK.
- **Azure Subscription**: Ensure you have an Azure subscription with necessary permissions.
- **Azure Resources**:
  - Azure File Share
  - Azure Virtual Machines
  - Azure Key Vault
  - Managed Identity set up for the hosting environment (e.g., Azure VM, App Service)

### Configuration

1. **Azure Key Vault**: Store sensitive configurations in Key Vault.

   - Use the Azure CLI or Azure Portal to add secrets.
   - **Secrets to store**:

     ```
     AzureSettings--StorageAccountName: yourstorageaccount
     AzureSettings--FileShareName: yourfileshare
     FolderMappings--0--FolderName: Folder1
     FolderMappings--0--VMName: VM1
     FolderMappings--0--ResourceGroupName: ResourceGroup1
     FolderMappings--0--SubscriptionId: your-subscription-id
     ```

   - For additional folder mappings, increment the index:

     ```
     FolderMappings--1--FolderName: Folder2
     // and so on
     ```

   - **Note**: Use double hyphens `--` to represent nested configuration sections and array indices.

2. **appsettings.json**: Update the configuration file.

   - Include non-sensitive settings and specify the Key Vault name.

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

4. **Network Configuration**:

   - Configure Azure Private Link and Private Endpoints for:
     - Azure Key Vault
     - Azure Storage Account (File Share)
     - Azure Resource Manager (for VM management)
   - Ensure the hosting environment is within a Virtual Network that can access these Private Endpoints.

### Build and Run

```bash
dotnet build
dotnet run
