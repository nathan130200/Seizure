using System;
using System.Collections.Generic;
using System.Text;

using Jid = agsXMPP.Jid;

namespace Seizure.Net.Server.Connections
{
    public interface IXmppListener
    {
        IXmppConnection GetConnection(Jid jid);
        IEnumerable<IXmppConnection> GetConnections(Jid jid);
        IEnumerable<IXmppConnection> GetConnections();

        void StartListen();
        void StopListen();
    }
}
