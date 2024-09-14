namespace AzureFileShareMonitorService.Models
{
    public class FolderMapping
    {
        public string FolderName { get; set; }
        public string VMName { get; set; }
        public string ResourceGroupName { get; set; }
        public string SubscriptionId { get; set; }
    }
}
