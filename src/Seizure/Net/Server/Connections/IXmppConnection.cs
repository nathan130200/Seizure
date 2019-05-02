using System.Security.Cryptography.X509Certificates;
using agsXMPP.Xml.Dom;
using Seizure.Net.Server.Session;

namespace Seizure.Net.Server.Connections
{
    public interface IXmppConnection
    {
        TcpXmppSession Session { get; }
        void Open();
        void Send(string xml);
        void Send(Element element);
        void Reset();
        void Close();
        void StartTls(X509Certificate2 certificate);
    }
}