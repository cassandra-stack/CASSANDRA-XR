using UnityEngine.Networking;
using UnityEngine.Scripting;

[Preserve]
class CertsHandler : CertificateHandler
{
    [Preserve] protected override bool ValidateCertificate(byte[] certData) => true;
}
