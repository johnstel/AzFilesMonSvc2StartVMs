namespace AzureFileShareMonitorService.Models
{
    public class FolderMapping
    {
        public string FolderName { get; set; } = string.Empty;
        public string VMName { get; set; } = string.Empty;
        public string ResourceGroupName { get; set; } = string.Empty;
        public string SubscriptionId { get; set; } = string.Empty;
    }
}
