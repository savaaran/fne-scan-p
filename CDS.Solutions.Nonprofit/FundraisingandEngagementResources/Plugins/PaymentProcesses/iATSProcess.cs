using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Plugins.PaymentProcesses
{
    public class iATSProcess
    {
        public static XmlDocument CreateCreditCardCustomerCode(CreateCreditCardCustomerCode obj)
        {
            XmlDocument result = null;

            try
            {
                string serviceURL = "https://www.iatspayments.com/netgate/CustomerLinkv2.asmx";

                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(serviceURL);
                WebHeaderCollection headers = new WebHeaderCollection();
                headers.Add("SOAPAction", "https://www.iatspayments.com/NetGate/CreateCreditCardCustomerCode");
                httpWebRequest.Headers = headers;

                httpWebRequest.KeepAlive = false;
                httpWebRequest.ContentType = "text/xml";
                httpWebRequest.MediaType = "application/xml";
                httpWebRequest.Accept = "application/xml";
                httpWebRequest.Method = "POST";

                string serialize = string.Empty;
                //Avoid adding <xml node at first line
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.OmitXmlDeclaration = true;
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(CreateCreditCardCustomerCode), "https://www.iatspayments.com/NetGate/");

                using (var ms = new MemoryStream())
                {
                    using (var writer = XmlWriter.Create(ms, settings))
                    {
                        // removes namespace
                        var xmlns = new XmlSerializerNamespaces();
                        xmlns.Add(string.Empty, string.Empty);

                        xmlSerializer.Serialize(writer, obj, xmlns);
                        serialize = Encoding.UTF8.GetString(ms.ToArray());
                    }
                }

                XmlDocument soapEnvelopeXml = new XmlDocument();
                soapEnvelopeXml.LoadXml(string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
                <soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
                  <soap:Body>
                    {0}
                  </soap:Body>
                </soap:Envelope>", serialize));

                using (Stream stream = httpWebRequest.GetRequestStream())
                {
                    soapEnvelopeXml.Save(stream);
                }

                var httpResponse = httpWebRequest.GetResponse();
                XmlDocument _xmlDoc = new XmlDocument();
                XmlReader xReader = XmlReader.Create(httpResponse.GetResponseStream());
                _xmlDoc.Load(xReader);

                result = _xmlDoc;
            }
            catch (Exception ex)
            {
                //result = ex.Message;
                result = null;
            }

            return result;
        }

        public static XmlDocument GetCustomerCodeDetail(GetCustomerCodeDetail obj)
        {
            XmlDocument result = null;

            try
            {
                string serviceURL = "https://www.iatspayments.com/netgate/CustomerLinkv2.asmx";

                HttpWebRequest httpWebRequest = (HttpWebRequest)System.Net.WebRequest.Create(serviceURL);
                WebHeaderCollection headers = new WebHeaderCollection();
                headers.Add("SOAPAction", "https://www.iatspayments.com/NetGate/GetCustomerCodeDetail");
                httpWebRequest.Headers = headers;

                httpWebRequest.KeepAlive = false;
                httpWebRequest.ContentType = "text/xml";
                httpWebRequest.MediaType = "application/xml";
                httpWebRequest.Accept = "application/xml";
                httpWebRequest.Method = "POST";

                string serialize = string.Empty;
                //Avoid adding <xml node at first line
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.OmitXmlDeclaration = true;
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(GetCustomerCodeDetail), "https://www.iatspayments.com/NetGate/");

                using (var ms = new MemoryStream())
                {
                    using (var writer = XmlWriter.Create(ms, settings))
                    {
                        // removes namespace
                        var xmlns = new XmlSerializerNamespaces();
                        xmlns.Add(string.Empty, string.Empty);

                        xmlSerializer.Serialize(writer, obj, xmlns);
                        serialize = Encoding.UTF8.GetString(ms.ToArray());
                    }
                }

                XmlDocument soapEnvelopeXml = new XmlDocument();
                soapEnvelopeXml.LoadXml(string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
                <soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
                  <soap:Body>
                    {0}
                  </soap:Body>
                </soap:Envelope>", serialize));

                using (Stream stream = httpWebRequest.GetRequestStream())
                {
                    soapEnvelopeXml.Save(stream);
                }

                var httpResponse = httpWebRequest.GetResponse();
                XmlDocument _xmlDoc = new XmlDocument();
                XmlReader xReader = XmlReader.Create(httpResponse.GetResponseStream());
                _xmlDoc.Load(xReader);

                result = _xmlDoc;
            }
            catch (Exception ex)
            {
                //result = ex.Message;
                result = null;
            }

            return result;
        }

        public static XmlDocument ProcessCreditCardWithCustomerCode(ProcessCreditCardWithCustomerCode obj)
        {
            XmlDocument result = null;

            try
            {
                string serviceURL = "https://www.iatspayments.com/netgate/ProcessLinkv2.asmx";

                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(serviceURL);
                WebHeaderCollection headers = new WebHeaderCollection();
                headers.Add("SOAPAction", "https://www.iatspayments.com/NetGate/ProcessCreditCardWithCustomerCode");
                httpWebRequest.Headers = headers;

                httpWebRequest.KeepAlive = false;
                httpWebRequest.ContentType = "text/xml";
                httpWebRequest.MediaType = "application/xml";
                httpWebRequest.Accept = "application/xml";
                httpWebRequest.Method = "POST";

                string serialize = string.Empty;
                //Avoid adding <xml node at first line
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.OmitXmlDeclaration = true;
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(ProcessCreditCardWithCustomerCode), "https://www.iatspayments.com/NetGate/");

                using (var ms = new MemoryStream())
                {
                    using (var writer = XmlWriter.Create(ms, settings))
                    {
                        // removes namespace
                        var xmlns = new XmlSerializerNamespaces();
                        xmlns.Add(string.Empty, string.Empty);

                        xmlSerializer.Serialize(writer, obj, xmlns);
                        serialize = Encoding.UTF8.GetString(ms.ToArray());
                    }
                }

                XmlDocument soapEnvelopeXml = new XmlDocument();
                soapEnvelopeXml.LoadXml(string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
                <soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
                  <soap:Body>
                    {0}
                  </soap:Body>
                </soap:Envelope>", serialize));

                using (Stream stream = httpWebRequest.GetRequestStream())
                {
                    soapEnvelopeXml.Save(stream);
                }

                var httpResponse = httpWebRequest.GetResponse();
                XmlDocument _xmlDoc = new XmlDocument();
                XmlReader xReader = XmlReader.Create(httpResponse.GetResponseStream());
                _xmlDoc.Load(xReader);

                result = _xmlDoc;

                
            }
            catch (Exception ex)
            {
                //result = ex.Message;
                result = null;
            }

            return result;
        }

        public static XmlDocument CreateACHEFTCustomerCode(CreateACHEFTCustomerCode obj)
        {
            XmlDocument result = null;

            try
            {
                string serviceURL = "https://www.iatspayments.com/netgate/CustomerLinkv2.asmx";

                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(serviceURL);
                WebHeaderCollection headers = new WebHeaderCollection();
                headers.Add("SOAPAction", "https://www.iatspayments.com/NetGate/CreateACHEFTCustomerCode");
                httpWebRequest.Headers = headers;

                httpWebRequest.KeepAlive = false;
                httpWebRequest.ContentType = "text/xml";
                httpWebRequest.MediaType = "application/xml";
                httpWebRequest.Accept = "application/xml";
                httpWebRequest.Method = "POST";

                string serialize = string.Empty;
                //Avoid adding <xml node at first line
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.OmitXmlDeclaration = true;
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(CreateACHEFTCustomerCode), "https://www.iatspayments.com/NetGate/");

                using (var ms = new MemoryStream())
                {
                    using (var writer = XmlWriter.Create(ms, settings))
                    {
                        // removes namespace
                        var xmlns = new XmlSerializerNamespaces();
                        xmlns.Add(string.Empty, string.Empty);

                        xmlSerializer.Serialize(writer, obj, xmlns);
                        serialize = Encoding.UTF8.GetString(ms.ToArray());
                    }
                }

                XmlDocument soapEnvelopeXml = new XmlDocument();
                soapEnvelopeXml.LoadXml(string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
                <soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
                  <soap:Body>
                    {0}
                  </soap:Body>
                </soap:Envelope>", serialize));

                using (Stream stream = httpWebRequest.GetRequestStream())
                {
                    soapEnvelopeXml.Save(stream);
                }

                var httpResponse = httpWebRequest.GetResponse();
                XmlDocument _xmlDoc = new XmlDocument();
                XmlReader xReader = XmlReader.Create(httpResponse.GetResponseStream());
                _xmlDoc.Load(xReader);

                result = _xmlDoc;

                //XmlNodeList xnList = _xmlDoc.GetElementsByTagName("AUTHORIZATIONRESULT");//You can get any Name here.
                //foreach (XmlNode item in xnList)
                //{
                //    string authResult = item.InnerText;
                //    if (authResult.Contains("OK"))
                //    {
                //        result = _xmlDoc.GetElementsByTagName("CUSTOMERCODE")[0].InnerText;
                //    }
                //    else
                //    {
                //        result = authResult;
                //    }
                //}
            }
            catch (Exception ex)
            {
                //result = ex.Message;
                result = null;
            }

            return result;
        }

        public static XmlDocument ProcessACHEFTWithCustomerCode(ProcessACHEFTWithCustomerCode obj)
        {
            XmlDocument result = null;

            try
            {
                string serviceURL = "https://www.iatspayments.com/netgate/ProcessLinkv2.asmx";

                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(serviceURL);
                WebHeaderCollection headers = new WebHeaderCollection();
                headers.Add("SOAPAction", "https://www.iatspayments.com/NetGate/ProcessACHEFTWithCustomerCode");
                httpWebRequest.Headers = headers;

                httpWebRequest.KeepAlive = false;
                httpWebRequest.ContentType = "text/xml";
                httpWebRequest.MediaType = "application/xml";
                httpWebRequest.Accept = "application/xml";
                httpWebRequest.Method = "POST";

                string serialize = string.Empty;
                //Avoid adding <xml node at first line
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.OmitXmlDeclaration = true;
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(ProcessACHEFTWithCustomerCode), "https://www.iatspayments.com/NetGate/");

                using (var ms = new MemoryStream())
                {
                    using (var writer = XmlWriter.Create(ms, settings))
                    {
                        // removes namespace
                        var xmlns = new XmlSerializerNamespaces();
                        xmlns.Add(string.Empty, string.Empty);

                        xmlSerializer.Serialize(writer, obj, xmlns);
                        serialize = Encoding.UTF8.GetString(ms.ToArray());
                    }
                }

                XmlDocument soapEnvelopeXml = new XmlDocument();
                soapEnvelopeXml.LoadXml(string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
                <soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
                  <soap:Body>
                    {0}
                  </soap:Body>
                </soap:Envelope>", serialize));

                using (Stream stream = httpWebRequest.GetRequestStream())
                {
                    soapEnvelopeXml.Save(stream);
                }

                var httpResponse = httpWebRequest.GetResponse();
                XmlDocument _xmlDoc = new XmlDocument();
                XmlReader xReader = XmlReader.Create(httpResponse.GetResponseStream());
                _xmlDoc.Load(xReader);

                result = _xmlDoc;

                //XmlNodeList xnList = _xmlDoc.GetElementsByTagName("AUTHORIZATIONRESULT");//You can get any Name here.
                //foreach (XmlNode item in xnList)
                //{
                //    string authResult = item.InnerText;
                //    if (authResult.Contains("OK"))
                //    {
                //        result = _xmlDoc.GetElementsByTagName("TRANSACTIONID")[0].InnerText;
                //    }
                //    else
                //    {
                //        result = authResult;
                //    }
                //}
            }
            catch (Exception ex)
            {
                //result = ex.Message;
                result = null;
            }

            return result;
        }

        public static XmlDocument UpdateCreditCardCustomerCode(UpdateCreditCardCustomerCode obj)
        {
            XmlDocument result = null;

            try
            {
                string serviceURL = "https://www.iatspayments.com/netgate/CustomerLinkv2.asmx";

                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(serviceURL);
                WebHeaderCollection headers = new WebHeaderCollection();
                headers.Add("SOAPAction", "https://www.iatspayments.com/NetGate/UpdateCreditCardCustomerCode");
                httpWebRequest.Headers = headers;

                httpWebRequest.KeepAlive = false;
                httpWebRequest.ContentType = "text/xml";
                httpWebRequest.MediaType = "application/xml";
                httpWebRequest.Accept = "application/xml";
                httpWebRequest.Method = "POST";

                string serialize = string.Empty;
                //Avoid adding <xml node at first line
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.OmitXmlDeclaration = true;
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(UpdateCreditCardCustomerCode), "https://www.iatspayments.com/NetGate/");

                using (var ms = new MemoryStream())
                {
                    using (var writer = XmlWriter.Create(ms, settings))
                    {
                        // removes namespace
                        var xmlns = new XmlSerializerNamespaces();
                        xmlns.Add(string.Empty, string.Empty);

                        xmlSerializer.Serialize(writer, obj, xmlns);
                        serialize = Encoding.UTF8.GetString(ms.ToArray());
                    }
                }

                XmlDocument soapEnvelopeXml = new XmlDocument();
                soapEnvelopeXml.LoadXml(string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
                <soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
                  <soap:Body>
                    {0}
                  </soap:Body>
                </soap:Envelope>", serialize));

                using (Stream stream = httpWebRequest.GetRequestStream())
                {
                    soapEnvelopeXml.Save(stream);
                }

                var httpResponse = httpWebRequest.GetResponse();
                XmlDocument _xmlDoc = new XmlDocument();
                XmlReader xReader = XmlReader.Create(httpResponse.GetResponseStream());
                _xmlDoc.Load(xReader);

                result = _xmlDoc;
            }
            catch (Exception ex)
            {
                //result = ex.Message;
                result = null;
            }
            return result;
        }

        public static XmlDocument UpdateACHEFTCustomerCode(UpdateACHEFTCustomerCode obj)
        {
            XmlDocument result = null;

            try
            {
                string serviceURL = "https://www.iatspayments.com/netgate/CustomerLinkv2.asmx";

                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(serviceURL);
                WebHeaderCollection headers = new WebHeaderCollection();
                headers.Add("SOAPAction", "https://www.iatspayments.com/NetGate/UpdateACHEFTCustomerCode");
                httpWebRequest.Headers = headers;

                httpWebRequest.KeepAlive = false;
                httpWebRequest.ContentType = "text/xml";
                httpWebRequest.MediaType = "application/xml";
                httpWebRequest.Accept = "application/xml";
                httpWebRequest.Method = "POST";

                string serialize = string.Empty;
                //Avoid adding <xml node at first line
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.OmitXmlDeclaration = true;
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(UpdateACHEFTCustomerCode), "https://www.iatspayments.com/NetGate/");

                using (var ms = new MemoryStream())
                {
                    using (var writer = XmlWriter.Create(ms, settings))
                    {
                        // removes namespace
                        var xmlns = new XmlSerializerNamespaces();
                        xmlns.Add(string.Empty, string.Empty);

                        xmlSerializer.Serialize(writer, obj, xmlns);
                        serialize = Encoding.UTF8.GetString(ms.ToArray());
                    }
                }

                XmlDocument soapEnvelopeXml = new XmlDocument();
                soapEnvelopeXml.LoadXml(string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
                <soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
                  <soap:Body>
                    {0}
                  </soap:Body>
                </soap:Envelope>", serialize));

                using (Stream stream = httpWebRequest.GetRequestStream())
                {
                    soapEnvelopeXml.Save(stream);
                }

                var httpResponse = httpWebRequest.GetResponse();
                XmlDocument _xmlDoc = new XmlDocument();
                XmlReader xReader = XmlReader.Create(httpResponse.GetResponseStream());
                _xmlDoc.Load(xReader);

                result = _xmlDoc;
            }
            catch (Exception ex)
            {
                //result = ex.Message;
                result = null;
            }

            return result;
        }

        public static XmlDocument ProcessCreditCardRefundWithTransactionId(ProcessCreditCardRefundWithTransactionId obj)
        {
            XmlDocument result = null;

            try
            {
                string serviceURL = "https://www.iatspayments.com/netgate/ProcessLinkv2.asmx";

                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(serviceURL);
                WebHeaderCollection headers = new WebHeaderCollection();
                headers.Add("SOAPAction", "https://www.iatspayments.com/NetGate/ProcessCreditCardRefundWithTransactionId");
                httpWebRequest.Headers = headers;

                httpWebRequest.KeepAlive = false;
                httpWebRequest.ContentType = "text/xml";
                httpWebRequest.MediaType = "application/xml";
                httpWebRequest.Accept = "application/xml";
                httpWebRequest.Method = "POST";

                string serialize = string.Empty;
                //Avoid adding <xml node at first line
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.OmitXmlDeclaration = true;
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(ProcessCreditCardRefundWithTransactionId), "https://www.iatspayments.com/NetGate/");

                using (var ms = new MemoryStream())
                {
                    using (var writer = XmlWriter.Create(ms, settings))
                    {
                        // removes namespace
                        var xmlns = new XmlSerializerNamespaces();
                        xmlns.Add(string.Empty, string.Empty);

                        xmlSerializer.Serialize(writer, obj, xmlns);
                        serialize = Encoding.UTF8.GetString(ms.ToArray());
                    }
                }

                XmlDocument soapEnvelopeXml = new XmlDocument();
                soapEnvelopeXml.LoadXml(string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
                <soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
                  <soap:Body>
                    {0}
                  </soap:Body>
                </soap:Envelope>", serialize));

                using (Stream stream = httpWebRequest.GetRequestStream())
                {
                    soapEnvelopeXml.Save(stream);
                }

                var httpResponse = httpWebRequest.GetResponse();
                XmlDocument _xmlDoc = new XmlDocument();
                XmlReader xReader = XmlReader.Create(httpResponse.GetResponseStream());
                _xmlDoc.Load(xReader);

                result = _xmlDoc;
                
            }
            catch (Exception ex)
            {
                //result = ex.Message;
                result = null;
            }

            return result;
        }

        public static XmlDocument ProcessACHEFTRefundWithTransactionId(ProcessACHEFTRefundWithTransactionId obj)
        {
            XmlDocument result = null;

            try
            {
                string serviceURL = "https://www.iatspayments.com/netgate/ProcessLinkv2.asmx";

                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(serviceURL);
                WebHeaderCollection headers = new WebHeaderCollection();
                headers.Add("SOAPAction", "https://www.iatspayments.com/NetGate/ProcessACHEFTRefundWithTransactionId");
                httpWebRequest.Headers = headers;

                httpWebRequest.KeepAlive = false;
                httpWebRequest.ContentType = "text/xml";
                httpWebRequest.MediaType = "application/xml";
                httpWebRequest.Accept = "application/xml";
                httpWebRequest.Method = "POST";

                string serialize = string.Empty;
                //Avoid adding <xml node at first line
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.OmitXmlDeclaration = true;
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(ProcessACHEFTRefundWithTransactionId), "https://www.iatspayments.com/NetGate/");

                using (var ms = new MemoryStream())
                {
                    using (var writer = XmlWriter.Create(ms, settings))
                    {
                        // removes namespace
                        var xmlns = new XmlSerializerNamespaces();
                        xmlns.Add(string.Empty, string.Empty);

                        xmlSerializer.Serialize(writer, obj, xmlns);
                        serialize = Encoding.UTF8.GetString(ms.ToArray());
                    }
                }

                XmlDocument soapEnvelopeXml = new XmlDocument();
                soapEnvelopeXml.LoadXml(string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
                <soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
                  <soap:Body>
                    {0}
                  </soap:Body>
                </soap:Envelope>", serialize));

                using (Stream stream = httpWebRequest.GetRequestStream())
                {
                    soapEnvelopeXml.Save(stream);
                }

                var httpResponse = httpWebRequest.GetResponse();
                XmlDocument _xmlDoc = new XmlDocument();
                XmlReader xReader = XmlReader.Create(httpResponse.GetResponseStream());
                _xmlDoc.Load(xReader);

                result = _xmlDoc;

            }
            catch (Exception ex)
            {
                //result = ex.Message;
                result = null;
            }

            return result;
        }
    }
}
