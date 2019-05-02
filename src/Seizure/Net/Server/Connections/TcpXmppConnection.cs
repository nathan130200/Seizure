using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using X509Certificate2 = System.Security.Cryptography.X509Certificates.X509Certificate2;
using SslProtocols = System.Security.Authentication.SslProtocols;
using System.Threading;

using Jid = agsXMPP.Jid;
using agsXMPP.Xml.Dom;

using Logger = NLog.Logger;
using LogManager = NLog.LogManager;

using Seizure.Net.Server.Session;
using Seizure.Extensions;

using StreamParser = agsXMPP.Xml.StreamParser;
using StreamElement = agsXMPP.protocol.Stream;
using StreamFeatures = agsXMPP.protocol.stream.Features;

using Iq = agsXMPP.protocol.client.IQ;
using IqType = agsXMPP.protocol.client.IqType;

using TlsStart = agsXMPP.protocol.tls.StartTls;
using TlsProceed = agsXMPP.protocol.tls.Proceed;
using TlsFailure = agsXMPP.protocol.tls.Failure;

using SaslAuth = agsXMPP.protocol.sasl.Auth;
using SaslSuccess = agsXMPP.protocol.sasl.Success;
using SaslFailure = agsXMPP.protocol.sasl.Failure;
using SaslFailureCondition = agsXMPP.protocol.sasl.FailureCondition;

using Namespaces = agsXMPP.Uri;
using System.Text;
using System.Xml;

namespace Seizure.Net.Server.Connections
{
    public class TcpXmppConnection : IXmppConnection
    {
        public event Action<TcpXmppConnection> OnOpen;
        public event Action<TcpXmppConnection> OnClose;

        protected volatile bool _closed;
        protected volatile bool _can_read = true;
        protected volatile bool _can_write = true;

        protected IXmppListener _listener;
        protected Logger _log;
        protected Stream _stream;
        protected TcpClient _client;
        protected StreamParser _parser;

        public TcpXmppSession Session { get; private set; }

        public TcpXmppConnection(IXmppListener listener, TcpClient client)
        {
            this._listener = listener;
            this._client = client;
            this._stream = this._client.GetStream();

            var id = Guid.NewGuid().ToString("D");
            this.Session = new TcpXmppSession(id);
            this._log = LogManager.GetLogger($"{GetType().Name}/{id}");
        }

        #region << Connection Management

        public void Open()
        {
            this._parser = new StreamParser();
            this._parser.OnStreamStart += this.OnStreamStart;
            this._parser.OnStreamElement += this.OnStreamElement;
            this._parser.OnStreamEnd += this.OnStreamEnd;
            this._parser.OnStreamError += this.OnStreamError;
            this._parser.OnError += this.OnStreamError;

            var data = new byte[short.MaxValue];
            this._stream.BeginRead(data, 0, data.Length, ReadCallback, data);
            this.OnOpen?.Invoke(this);
        }

        public void Close()
        {
            if (this._closed)
                return;

            this._closed = true;
            this._log.Debug("close.");

            this.Send("</stream:stream>");

            this._can_read = false;
            this._can_write = false;

            if(this._stream != null)
            {
                this._stream.Dispose();
                this._stream = null;
            }

            if (this._client != null)
            {
                this._client.Dispose();
                this._client = null;
            }

            if(this._parser != null)
            {
                this._parser.OnStreamStart -= this.OnStreamStart;
                this._parser.OnStreamElement -= this.OnStreamElement;
                this._parser.OnStreamEnd -= this.OnStreamEnd;
                this._parser.OnStreamError -= this.OnStreamError;
                this._parser.OnError -= this.OnStreamError;
                this._parser = null;
            }

            OnClose?.Invoke(this);

            if (this.Session != null)
            {
                this.Session.Dispose();
                this.Session = null;
            }
        }



        public void Reset()
        {
            if (this._closed)
                return;

            this._parser.Reset();
            this._log.Debug("reset stream.");
        }

        public void Send(Element element)
        {
            if (this._closed)
                return;

            this.AcquireWrite();

            var data = Encoding.UTF8.GetBytes(element.ToString());

            this._log.Debug("send >>:\n{0}\n", element.ToString(Formatting.Indented));
            this._stream.BeginWrite(data, 0, data.Length, WriteCallback, data.Length);
        }

        public void Send(string raw)
        {
            if (this._closed)
                return;

            this.AcquireWrite();

            var data = Encoding.UTF8.GetBytes(raw);

            this._log.Debug("send >>:\n{0}\n", raw);
            this._stream.BeginWrite(data, 0, data.Length, WriteCallback, data.Length);
        }

        public void StartTls(X509Certificate2 certificate)
        {
            this._log.Warn("StartTls(): begin start tls...");

            try
            {
                this.AcquireWrite();

                this._can_write = false;
                this._can_read = false;

                this._stream.Flush();
                this._stream = new SslStream(this._stream);
                ((SslStream)this._stream).AuthenticateAsServer(certificate, false, SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, true);
                this._log.Info("StartTls(): start tls success!");

                this.Session.IsTlsStarted = true;

                this._can_write = true;
                this._can_read = true;

                this.Reset();
            }
            catch (Exception ex)
            {
                this._log.Error(ex, "StartTls(): start tls failure!\n");
                this.Send(new TlsFailure());
                this.Close();
            }
        }

        #endregion

        #region << Lockers >>

        void AcquireWrite()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            this._log.Debug("AcquireWrite(): acquire write stream.");
            while (!this._can_write) ;
            watch.Stop();

            this._log.Debug("AcquireWrite(): write state acquired in {0}ms.", watch.ElapsedMilliseconds);
            this._can_write = false;
        }

        #endregion


        #region << IAsync Result Handlers >>

        void WriteCallback(IAsyncResult ar)
        {
            try
            {
                if (this._closed)
                {
                    this._log.Warn("WriteCallback(): connection is closed, skip write.");
                    return;
                }

                var size = (int)ar.AsyncState;
                this._stream.EndWrite(ar);
                this._can_write = true;
                this._log.Debug("WriteCallback(): write {0} bytes.", size);
            }
            catch(Exception ex)
            {
                this._log.Error(ex, "WriteCallback(): write failed.\n");
                this.Close();
            }
        }

        void ReadCallback(IAsyncResult ar)
        {
            if (this._closed)
            {
                this._log.Warn("ReadCallback(): connection is closed, skip read.");
                return;
            }

            try
            {
                var data = (byte[])ar.AsyncState;
                var size = this._stream.EndRead(ar);
                if(size > 0)
                {
                    this._parser.Push(data, 0, size);
                    this._log.Debug("ReadCallback(): read {0} bytes", size);

                    if (this._can_read)
                        this._stream.BeginRead(data, 0, data.Length, ReadCallback, data);
                }
                else
                {
                    this._log.Warn("ReadCallback(): no data received, closing connection.");
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                this._log.Error(ex, "ReadCallback(): read failed.\n");
                this.Close();
            }
        }

        #endregion

        #region << Xmpp Stream Events >>

        void OnStreamStart(object sender, Node node)
        {
            var e = (StreamElement)node;

            this._log.Debug("recv <<:\n{0}\n", e.StartTag());

            var stream = new StreamElement
            {
                Id = this.Session.Id,
                From = "warface",
                Version = "1.0",
                Prefix = "stream",
                Namespace = Namespaces.STREAM
            };

            this.Send(stream.StartTag().ToString().Replace("/>", ">"));

            if (e.To.Server != "warface")
            {
                var error = new Element("error", null, Namespaces.STREAM) { Prefix = "stream" };
                error.C("host-unknown", ns: Namespaces.STREAMS);
                this.Send(error);
                return;
            }

            var features = new StreamFeatures { Prefix = "stream" };
            {
                if (!this.Session.IsAuthenticated)
                {
                    if (!this.Session.IsTlsStarted)
                    {
                        features.C("starttls", ns: Namespaces.TLS)
                            .C("required");
                    }

                    features.C("mechanisms", ns: Namespaces.SASL)
                        .C("mechanism", "PLAIN");
                }
                else
                {
                    features.C("bind", Namespaces.BIND);
                    features.C("session", Namespaces.SESSION);
                }
            }

            this.Send(features);
        }

        void OnStreamElement(object sender, Node node)
        {
            this._log.Debug("recv <<:\n{0}\n", node.ToString(Formatting.Indented));

            if (this._closed)
                return;

            if(node is TlsStart)
            {
                Send(new TlsProceed());
                this.StartTls(Program.GetTlsCertificate());
            }
            else if(node is SaslAuth)
            {
                ProcesAuth((SaslAuth)node);
            }
            else if(node is Iq)
            {
                ProcessIq(node as Iq);
            }
        }

        void OnStreamEnd(object sender, Node node)
        {
            var st = (StreamElement)node;
            this._log.Debug("recv <<:\n{0}\n", st.EndTag());

            if (this._closed)
                return;

            this.Close();
        }

        void OnStreamError(object sender, Exception error)
        {
            this._log.Fatal(error, "stream parser failure!\n");

            if (this._closed)
                return;

            this.Close();
        }

        #endregion

        #region << XMPP: SASL >>

        void ProcesAuth(SaslAuth auth)
        {
            if(auth.GetAttribute("mechanism") != "PLAIN")
            {
                Send(new SaslFailure(SaslFailureCondition.invalid_mechanism));
                return;
            }

            var sasl = Encoding.UTF8.GetString(Convert.FromBase64String(auth.Value)).Split((char)0);

            string username = "",
                password = "";

            if(sasl.Length == 3)
            {
                username = sasl[1];
                password = sasl[2];
            }
            else if(sasl.Length == 2)
            {
                username = sasl[0];
                password = sasl[1];
            }
            else
            {
                Send(new SaslFailure(SaslFailureCondition.incorrect_encoding));
                return;
            }

            if(string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                Send(new SaslFailure(SaslFailureCondition.not_authorized));
                return;
            }

            Send(new SaslSuccess());
            this.Session.Authenticate(username);
            this.Reset();
        }

        #endregion

        void ProcessIq(Iq iq)
        {
            var query = iq.Query ?? iq.FirstChild;

            if (query != null)
            {
                if(query.TagName == "bind" && query.Namespace == Namespaces.BIND)
                {
                    this.ProcessBind(iq, query);
                }
                else if(query.TagName == "session" && query.Namespace == Namespaces.SESSION)
                {
                    this.ProcessSession(iq);
                }
            }
        }

        #region << XMPP> Bind >>

        void ProcessBind(Iq iq, Element bind)
        {
            var resource = bind.SelectSingleElement("resource").Value;
            var search = new Jid(this.Session.Jid.Bare + $"/{resource}");
            var conflict = this._listener.GetConnection(search) != null;

            if (conflict)
            {
                this.Send(iq.ToError(agsXMPP.protocol.client.ErrorType.modify, agsXMPP.protocol.client.ErrorCondition.Conflict));
                return;
            }

            this.Session.Bind(resource);

            iq.SwitchDirection();
            iq.Type = IqType.result;
            iq.RemoveAllChildNodes();

            iq.C("bind", ns: Namespaces.BIND)
                .C("jid", this.Session.Jid);

            this.Send(iq);
        }

        #endregion

        #region << XMPP: Session >>

        void ProcessSession(Iq iq)
        {
            iq.SwitchDirection();
            iq.Type = IqType.result;
            this.Send(iq);
        }

        #endregion
    }
}
