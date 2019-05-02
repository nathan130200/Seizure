using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using agsXMPP;
using Seizure.Net.Server.Connections;
using Jid = agsXMPP.Jid;

namespace Seizure.Net.Server.Session
{
    public class TcpXmppSession : IDisposable, IEquatable<TcpXmppSession>
    {
        public TcpXmppSession(string id)
        {
            this.Id = id;
            this.IsAuthenticated = false;
            this.IsTlsStarted = false;
            this.IsBinded = false;
            this.Variables = new ConcurrentDictionary<string, object>();
            this.Jid = null;
        }

        public string Id { get; }
        public bool IsAuthenticated { get; private set; }
        public bool IsTlsStarted { get; set; }
        public bool IsBinded { get; private set; }
        public Jid Jid { get; private set; }
        public ConcurrentDictionary<string, object> Variables { get; private set; }

        public void Dispose()
        {
            this.IsAuthenticated = false;
            this.IsTlsStarted = false;
            this.IsBinded = false;
            this.Jid = null;
            this.Variables.Clear();
            this.Variables = null;
        }

        public TValue GetVariable<TValue>(string name)
        {
            if (this.Variables.TryGetValue(name, out var value))
                return (TValue)value;

            return default;
        }

        public object GetVariable(string name)
        {
            if (this.Variables.TryGetValue(name, out var value))
                return value;

            return default;
        }

        public object SetVariable(string name, object value)
        {
            return this.Variables.AddOrUpdate(name, value, (key, old) => value);
        }

        public void Authenticate(string username)
        {
            if (this.IsAuthenticated)
                return;

            this.IsAuthenticated = true;
            this.Jid = new Jid(username, "warface", string.Empty);
        }

        public void Bind(string resource)
        {
            if (this.IsBinded)
                return;

            this.IsBinded = true;
            this.Jid.Resource = resource;
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as TcpXmppSession);
        }

        public bool Equals(TcpXmppSession other)
        {
            return other != null && this.Id == other.Id;
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }

        public static bool operator ==(TcpXmppSession left, TcpXmppSession right)
        {
            return EqualityComparer<TcpXmppSession>.Default.Equals(left, right);
        }

        public static bool operator !=(TcpXmppSession left, TcpXmppSession right)
        {
            return !(left == right);
        }
    }
}
