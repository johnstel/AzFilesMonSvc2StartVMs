namespace AzureFileShareMonitorService.Models
{
    public class AzureSettings
    {
        public string StorageAccountName { get; set; } = string.Empty;
        public string FileShareName { get; set; } = string.Empty;
        public string KeyVaultName { get; set; } = string.Empty;
    }
}
