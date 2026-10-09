using Opc.Ua;

namespace OpcUA_Server;

internal static class ServerConfiguration
{
    public static readonly ITelemetryContext Telemetry = DefaultTelemetry.Create(_ => { });

    public static ApplicationConfiguration Create(string name, string address, string certificateRoot) => new(Telemetry)
    {
        ApplicationName = $"Demo OPC UA {name}",
        ApplicationUri = $"urn:{Utils.GetHostName()}:OpcUaServer:{name}:{Environment.ProcessId}",
        ProductUri = "urn:demo:OpcUaServer",
        ApplicationType = ApplicationType.Server,
        TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
        ServerConfiguration = new Opc.Ua.ServerConfiguration
        {
            BaseAddresses = [address],
            SecurityPolicies =
            [
                new ServerSecurityPolicy { SecurityMode = MessageSecurityMode.None, SecurityPolicyUri = SecurityPolicies.None },
                new ServerSecurityPolicy { SecurityMode = MessageSecurityMode.SignAndEncrypt, SecurityPolicyUri = SecurityPolicies.Basic256Sha256 },
                new ServerSecurityPolicy { SecurityMode = MessageSecurityMode.SignAndEncrypt, SecurityPolicyUri = SecurityPolicies.Aes256_Sha256_RsaPss }
            ],
            UserTokenPolicies = [new UserTokenPolicy { PolicyId = "anonymous", TokenType = UserTokenType.Anonymous, SecurityPolicyUri = SecurityPolicies.None }]
        },
        SecurityConfiguration = new SecurityConfiguration
        {
            ApplicationCertificate = new CertificateIdentifier { StoreType = "Directory", StorePath = Path.Combine(certificateRoot, "own"), SubjectName = $"CN=Demo {name} OPC UA Server" },
            TrustedPeerCertificates = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(certificateRoot, "trusted") },
            TrustedIssuerCertificates = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(certificateRoot, "issuers") },
            RejectedCertificateStore = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(certificateRoot, "rejected") },
            AutoAcceptUntrustedCertificates = false,
            AddAppCertToTrustedStore = true
        }
    };
}
