using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace Unity.Services.Multiplay
{
    /// <summary>
    /// The server configuration for the current session.
    /// </summary>
    [Preserve]
    [DataContract(Name = "server.json")]
    public class ServerConfig
    {
        /// <summary>
        /// Creates an instance of ServerConfig.
        /// Note that you should not manually create one of these except for testing.
        /// Instead, use MultiplayService.ServerConfig to access the server configuration.
        /// </summary>
        /// <param name="serverId">The Server ID.</param>
        /// <param name="allocationId">The Allocation ID for the server.</param>
        /// <param name="queryPort">The Query Port.</param>
        /// <param name="port">The Game Session Port.</param>
        /// <param name="ip">The Game Session IP Address.</param>
        /// <param name="serverLogDirectory">The Server Log Directory.</param>
        [Preserve]
        public ServerConfig(long serverId, string allocationId, ushort queryPort, ushort port, string ip, string serverLogDirectory)
        {
            ServerId = serverId;
            AllocationId = allocationId;
            QueryPort = queryPort;
            Port = port;
            IpAddress = ip;
            ServerLogDirectory = serverLogDirectory;
        }

        /// <summary>
        /// The server ID.
        /// </summary>
        [Preserve]
        [DataMember(Name = "serverID", IsRequired = true, EmitDefaultValue = false)]
        public long ServerId { get; }

        /// <summary>
        /// The allocation ID.
        /// </summary>
        [Preserve]
        [DataMember(Name = "allocatedUUID", IsRequired = true, EmitDefaultValue = false)]
        public string AllocationId { get; }

        /// <summary>
        /// The Server Query Protocol Port.
        /// </summary>
        [Preserve]
        [DataMember(Name = "queryPort", IsRequired = true, EmitDefaultValue = false)]
        public ushort QueryPort { get; }

        /// <summary>
        /// The connection port for the session.
        /// </summary>
        [Preserve]
        [DataMember(Name = "port", IsRequired = true, EmitDefaultValue = false)]
        public ushort Port { get; }

        /// <summary>
        /// The connection ip for the session.
        /// </summary>
        [Preserve]
        [DataMember(Name = "ip", IsRequired = true, EmitDefaultValue = false)]
        public string IpAddress { get; }

        /// <summary>
        /// The directory logs will be written to.
        /// </summary>
        [Preserve]
        [DataMember(Name = "serverLogDir", IsRequired = true, EmitDefaultValue = false)]
        public string ServerLogDirectory { get; }
    }
}
