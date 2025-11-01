using UnityEngine.Networking;

class CertsHandler : CertificateHandler {
    protected override bool ValidateCertificate(byte[] certData) => true; // Bypass cert
}
