using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Jid = agsXMPP.Jid;

namespace Seizure.Net.Server.Connections
{
    public class TcpXmppListener : IXmppListener
    {
        protected Logger _log = LogManager.GetCurrentClassLogger();
        protected volatile bool _closed;
        protected List<IXmppConnection> _connections;
        protected TcpListener _listener;

        public TcpXmppListener()
        {
            this._connections = new List<IXmppConnection>();

            this._listener = new TcpListener(IPAddress.Any, 5222);
            this._listener.ExclusiveAddressUse = true;
        }

        public void StartListen()
        {
            this._listener.Start(10);
            this._listener.BeginAcceptTcpClient(AcceptCallback, null);
            this._log.Info("start listen.");
        }

        public void StopListen()
        {
            if (this._closed)
                return;

            this._closed = true;

            foreach(var connection in this.GetConnections())
            {
                connection.Close();
            }

            if(this._listener != null)
            {
                this._listener.Stop();
                this._listener = null;
            }

            this._log.Info("stop listen.");
        }

        public IXmppConnection GetConnection(Jid jid)
        {
            IXmppConnection[] temp;

            lock (this._connections)
            {
                temp = this._connections.ToArray();
            }

            return temp.Where(x => x.Session.Jid.ToString() == jid.ToString()).FirstOrDefault();
        }

        public IEnumerable<IXmppConnection> GetConnections(Jid jid)
        {
            IXmppConnection[] temp;

            lock (this._connections)
            {
                temp = this._connections.ToArray();
            }

            return temp.Where(x => x.Session.Jid.Bare == jid.Bare);
        }

        public IEnumerable<IXmppConnection> GetConnections()
        {
            IXmppConnection[] temp;

            lock (this._connections)
            {
                temp = this._connections.ToArray();
            }

            return temp;
        }

        void AcceptCallback(IAsyncResult ar)
        {
            var result = true;

            try
            {
                var client = this._listener.EndAcceptTcpClient(ar);
                if (client != null)
                {
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            var connection = new TcpXmppConnection(this, client);
                            connection.OnOpen += this.Connection_OnOpen;
                            connection.OnClose += this.Connection_OnClose;
                            connection.Open();
                        }
                        catch(Exception ex)
                        {
                            _log.Error(ex, "AcceptCallback(): connection error.\n");
                        }
                    });
                }
            }
            catch (ObjectDisposedException ex)
            {
                result = false;
                this._log.Fatal(ex);
            }
            catch (Exception ex)
            {
                this._log.Warn(ex);
            }
            finally
            {
                if (!this._closed && result)
                {
                    this._listener.BeginAcceptTcpClient(AcceptCallback, null);
                }
            }
        }

        void Connection_OnOpen(TcpXmppConnection e)
        {
            lock (this._connections)
            {
                this._connections.Add(e);
            }
        }

        void Connection_OnClose(TcpXmppConnection e)
        {
            lock (this._connections)
            {
                this._connections.Remove(e);
            }
        }
    }
}