namespace AzureFileShareMonitorService.Models
{
    /// <summary>
    /// Represents the possible states of an Azure Virtual Machine.
    /// </summary>
    public enum VMState
    {
        /// <summary>
        /// The VM state is unknown.
        /// </summary>
        Unknown,

        /// <summary>
        /// The VM is currently running.
        /// </summary>
        Running,

        /// <summary>
        /// The VM is starting.
        /// </summary>
        Starting,

        /// <summary>
        /// The VM is stopping.
        /// </summary>
        Stopping,

        /// <summary>
        /// The VM is stopped.
        /// </summary>
        Stopped,

        /// <summary>
        /// The VM is deallocating.
        /// </summary>
        Deallocating,

        /// <summary>
        /// The VM is deallocated.
        /// </summary>
        Deallocated
    }
}
