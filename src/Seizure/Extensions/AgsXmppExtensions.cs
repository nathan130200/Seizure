using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using agsXMPP.protocol.client;
using agsXMPP.Xml.Dom;
using Jid = agsXMPP.Jid;
using Namespaces = agsXMPP.Uri;
using StreamElement = agsXMPP.protocol.Stream;
using StreamErrorCondition = agsXMPP.protocol.StreamErrorCondition;

namespace Seizure.Extensions
{
    public static class AgsXmppExtensions
    {
        public static readonly NumberFormatInfo DEFAULT_NUMBER_FORMAT = new NumberFormatInfo
        {
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ".",
            NumberDecimalSeparator = ".",
            NumberGroupSeparator = ".",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ".",
        };

        public static Element C(this Element parent, string name, string text = null, string ns = null)
        {
            var child = new Element(name, text, ns);
            parent.AddChild(child);
            return child;
        }

        public static Element Q(this Element element, string name, Action<Element> builder)
        {
            var query = new Element("query", null, "urn:cryonline:k01");
            var tag = new Element(name);
            builder(tag);
            return element;
        }

        public static string StartTag(this Element element)
        {
            var sw = new StringWriter();
            var xw = new XmlTextWriter(sw);

            if (string.IsNullOrEmpty(element.Prefix))
            {
                xw.WriteStartElement(element.TagName);
            }
            else
            {
                xw.WriteStartElement($"{element.Prefix}:{element.TagName}");
            }

            if (!string.IsNullOrEmpty(element.Namespace))
            {
                if (!string.IsNullOrEmpty(element.Prefix))
                {
                    xw.WriteAttributeString($"xmlns:{element.Prefix}", element.Namespace);
                }
                else
                {
                    xw.WriteAttributeString("xmlns", element.Namespace);
                }
            }

            foreach(DictionaryEntry entry in element.Attributes)
            {
                xw.WriteAttributeString(entry.Key.ToString(), entry.Value.ToString());
            }

            xw.Flush();
            xw.Close();

            return sw.ToString().Replace("/>", ">");
        }

        public static string EndTag(this Element element)
        {
            if (string.IsNullOrEmpty(element.Prefix))
            {
                return $"</{element.TagName}>";
            }
            else
            {
                return $"</{element.Prefix}:{element.TagName}>";
            }
        }

        public static IQ ToError(this IQ iq, ErrorType type = ErrorType.cancel, ErrorCondition condition = ErrorCondition.FeatureNotImplemented, string text = "Custom query error.")
        {
            iq.SwitchDirection();
            iq.Type = IqType.error;

            var tag = "";

            switch (condition)
            {
                case ErrorCondition.BadRequest: tag = "bad-request"; break;
                case ErrorCondition.Conflict: tag = "conflict"; break;
                case ErrorCondition.FeatureNotImplemented: tag = "feature-not-implemented"; break;
                case ErrorCondition.Forbidden: tag = "forbidden"; break;
                case ErrorCondition.Gone: tag = "gone"; break;
                case ErrorCondition.InternalServerError: tag = "internal-server-error"; break;
                case ErrorCondition.ItemNotFound: tag = "item-not-found"; break;
                case ErrorCondition.JidMalformed: tag = "jid-malformed"; break;
                case ErrorCondition.NotAcceptable: tag = "not-acceptable"; break;
                case ErrorCondition.NotAllowed: tag = "not-allowed"; break;
                case ErrorCondition.NotAuthorized: tag = "not-authorized"; break;
                case ErrorCondition.NotModified: tag = "not-modified"; break;
                case ErrorCondition.PaymentRequired: tag = "payment-required"; break;
                case ErrorCondition.RecipientUnavailable: tag = "recipient-unavailable"; break;
                case ErrorCondition.Redirect: tag = "redirect"; break;
                case ErrorCondition.RegistrationRequired: tag = "registration-required"; break;
                case ErrorCondition.RemoteServerNotFound: tag = "remote-server-not-found"; break;
                case ErrorCondition.RemoteServerTimeout: tag = "remote-server-timeout"; break;
                case ErrorCondition.ResourceConstraint: tag = "resource-constraint"; break;
                case ErrorCondition.ServiceUnavailable: tag = "service-unavailable"; break;
                case ErrorCondition.SubscriptionRequired: tag = "subscription-required"; break;
                case ErrorCondition.UndefinedCondition: tag = "undefined-condition"; break;
                case ErrorCondition.UnexpectedRequest: tag = "unexpected-request"; break;
                default: tag = "undefined-condition"; break;
            }

            iq.C("error")
                .A("type", type.ToString())
                    .C(tag, ns: Namespaces.STANZAS);

            return iq;
        }

        public static IQ ToCustomError(this IQ iq, ErrorType type = ErrorType.@continue, int code = 8, int custom_code = -1, string text = "Custom query error.")
        {
            iq.SwitchDirection();
            iq.Type = IqType.error;

            iq.C("error")
                .A("type", type.ToString())
                .A("code", code)
                .A("custom_code", custom_code)
                    .C("text", text, Namespaces.STANZAS);

            return iq;
        }

        public static StreamElement ToError(this StreamElement stream, StreamErrorCondition condition = StreamErrorCondition.UndefinedCondition, string text = null)
        {
            var tag = "";

            switch (condition)
            {
                case StreamErrorCondition.BadFormat: tag = "bad-format"; break;
                case StreamErrorCondition.BadNamespacePrefix: tag = "bad-namespace-prefix"; break;
                case StreamErrorCondition.Conflict: tag = "conflict"; break;
                case StreamErrorCondition.ConnectionTimeout: tag = "connection-timeout"; break;
                case StreamErrorCondition.HostGone: tag = "host-gone"; break;
                case StreamErrorCondition.HostUnknown: tag = "host-unknown"; break;
                case StreamErrorCondition.ImproperAddressing: tag = "improper-addressing"; break;
                case StreamErrorCondition.InternalServerError: tag = "internal-server-error"; break;
                case StreamErrorCondition.InvalidFrom: tag = "invalid-from"; break;
                case StreamErrorCondition.InvalidId: tag = "invalid-id"; break;
                case StreamErrorCondition.InvalidNamespace: tag = "invalid-namespace"; break;
                case StreamErrorCondition.InvalidXml: tag = "invalid-xml"; break;
                case StreamErrorCondition.NotAuthorized: tag = "not-authorized"; break;
                case StreamErrorCondition.PolicyViolation: tag = "policy-violation"; break;
                case StreamErrorCondition.RemoteConnectionFailed: tag = "remote-connection-failed"; break;
                case StreamErrorCondition.ResourceConstraint: tag = "resource-constraint"; break;
                case StreamErrorCondition.RestrictedXml: tag = "restricted-xml"; break;
                case StreamErrorCondition.SeeOtherHost: tag = "see-other-host"; break;
                case StreamErrorCondition.SystemShutdown: tag = "system-shutdown"; break;
                case StreamErrorCondition.UnknownCondition: tag = "unknown-condition"; break;
                case StreamErrorCondition.UnsupportedEncoding: tag = "unsupported-encoding"; break;
                case StreamErrorCondition.UnsupportedStanzaType: tag = "unsupported-stanza-type"; break;
                case StreamErrorCondition.UnsupportedVersion: tag = "unsupported-version"; break;
                case StreamErrorCondition.XmlNotWellFormed: tag = "xml-not-well-formed"; break;
                case StreamErrorCondition.UndefinedCondition:
                default: tag = "undefined-condition"; break;
            }

            stream.C(tag, ns: Namespaces.STANZAS);

            if (!string.IsNullOrEmpty(text))
                stream.C("text", text, Namespaces.STANZAS);

            return stream;
        }

        public static Element C(this Element element, Element child)
        {
            element.AddChild(child);
            return child;
        }

        public static Element A(this Element element, string name, string value)
        {
            element.SetAttribute(name, value);
            return element;
        }

        public static Element A(this Element element, string name, bool value, bool number = true)
        {
            element.SetAttribute(name, number ? (value ? "1" : "0") : (value ? "true" : "false"));
            return element;
        }

        public static Element A(this Element element, string name, double value, IFormatProvider provider = null)
        {
            if (provider == null)
                provider = DEFAULT_NUMBER_FORMAT;

            element.SetAttribute(name, value, provider);
            return element;
        }

        public static Element A(this Element element, string name, long value)
        {
            element.SetAttribute(name, value);
            return element;
        }

        public static Element A(this Element element, string name, Jid value)
        {
            element.SetAttribute(name, value);
            return element;
        }

        public static Element P(this Element element)
        {
            var field = typeof(Element)
                .GetField("Parent", BindingFlags.NonPublic | BindingFlags.Instance);

            var obj = field.GetValue(element);

            if (obj == null)
                return element;

            return (Element)obj;
        }

        public static Element R(this Element element)
        {
            object parent = null;
            object instance = element;

            var field = typeof(Element)
                .GetField("Parent", BindingFlags.NonPublic | BindingFlags.Instance);

            while ((instance = field.GetValue(instance)) != null)
                parent = instance;

            return (Element)parent;
        }
    }
}
