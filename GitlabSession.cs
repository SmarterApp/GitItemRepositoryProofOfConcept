using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.XPath;

// System.Runtime.Serialization.Json parses JSON as if it were XML. This turns out
// to be really convenient because you can load it into an XPathDocument and query
// the results using XPath. But the code looks unusual.

namespace GitItemRepositoryProofOfConcept
{
    class GitLabSession
    {
        static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        string _hostUrl;
        string _privateToken;

        public GitLabSession(string hostUrl)
        {
            _hostUrl = hostUrl;
        }

        public void Login(string userId, string password)
        {
            XPathDocument doc = CallHttp("/api/v3/session", Method.POST, "login", userId, "email", userId, "password", password);
            XPathNavigator nav = doc.CreateNavigator();
            _privateToken = Eval(nav, "//private_token");
        }

        public void CreateProject(string projectName, string groupname)
        {
            int groupId = GetGroupId(groupname);
            XPathDocument doc = CallHttp("/api/v3/projects", Method.POST, "name", projectName, "namespace_id", groupId);
        }

        public enum ProjectStatus
        {
            NonExistent = 0,
            Empty = 1,
            HasContents = 2
        }

        public ProjectStatus GetProjectStatus(string projectName, string groupName)
        {
            string url = string.Format("{0}/api/v3/projects/{1}%2F{2}", _hostUrl, groupName, projectName);
            //Console.WriteLine("GET {0}", url);

            // TODO: Improve error handling so we can use CallHttp here.
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Headers.Add("PRIVATE-TOKEN", _privateToken);

            try
            {
                XPathDocument xml;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    using (XmlReader reader = JsonReaderWriterFactory.CreateJsonReader(response.GetResponseStream(), new XmlDictionaryReaderQuotas()))
                    {
                        xml = new XPathDocument(reader);
                    }
                }

                //DumpDoc(xml);
                string defaultBranch = Eval(xml.CreateNavigator(), "root/default_branch");
                return string.IsNullOrEmpty(defaultBranch) ? ProjectStatus.Empty : ProjectStatus.HasContents;
            }
            catch (WebException err)
            {
                using (HttpWebResponse response = (HttpWebResponse)err.Response)
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return ProjectStatus.NonExistent;
                    }
                    else
                    {
                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        {
                            string responseText = reader.ReadToEnd();
                            throw new ApplicationException(string.Format("HTTP Error {0} {1}\r\n{2}", (int)response.StatusCode, response.StatusCode, responseText));
                        }
                    }
                }
            }
        }

        public int GetGroupId(string groupName)
        {
            XPathDocument doc = CallHttp("/api/v3/groups", Method.GET, "search", groupName);
            string id = Eval(doc.CreateNavigator(), string.Format("/root/item[name='{0}']/id", groupName));
            if (id == null) throw new ArgumentException(string.Format("Group '{0}' not found.", groupName));
            return Int16.Parse(id);
        }

        private XPathDocument CallHttp(string path, Method method, params object[] parameters)
        {
            try
            {
                if (parameters.Length % 2 != 0)
                {
                    throw new ArgumentException("Parameters must be names alternating with values. Non-even count.");
                }

                // Build the query string (if necessary)
                string queryString = null;
                if (parameters.Length > 0)
                {
                    StringBuilder rq = new StringBuilder();
                    for (int i = 0; i < parameters.Length; i += 2)
                    {
                        if (i != 0) rq.Append('&');
                        if (!(parameters[i] is string)) throw new ArgumentException("Parameter name is not string!");
                        rq.Append((string)parameters[i]);
                        rq.Append('=');
                        rq.Append(Uri.EscapeDataString(parameters[i + 1].ToString()));
                    }
                    queryString = rq.ToString();
                }

                string url = string.Concat(_hostUrl, path);

                if (method == Method.GET && queryString != null)
                {
                    url = string.Concat(url, "?", queryString);
                }
                else if (method == Method.POST)
                {
                    if (queryString == null) throw new InvalidOperationException("Must specify parameters for method POST");
                }

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = method.ToString();
                if (_privateToken != null) request.Headers.Add("PRIVATE-TOKEN", _privateToken);

                if (method == Method.POST)
                {
                    byte[] requestBody = Utf8.GetBytes(queryString);
                    request.ContentType = "application/x-www-form-urlencoded";
                    request.ContentLength = requestBody.Length;
                    using (Stream dataOut = request.GetRequestStream())
                    {
                        dataOut.Write(requestBody, 0, requestBody.Length);
                    }
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    using (XmlReader reader = JsonReaderWriterFactory.CreateJsonReader(response.GetResponseStream(), new XmlDictionaryReaderQuotas()))
                    {
                        return new XPathDocument(reader);
                    }
                }
            }
            catch (WebException err)
            {
                using (StreamReader reader = new StreamReader(err.Response.GetResponseStream()))
                {
                    string httpMsg = reader.ReadToEnd();
                    throw new ApplicationException(string.Concat(err.Message, ":", httpMsg), err);
                }
            }
        }

        static void DumpDoc(XPathDocument doc)
        {
            Console.WriteLine("-----");
            using (XmlTextWriter writer = new XmlTextWriter(Console.Out))
            {
                writer.Formatting = Formatting.Indented;
                doc.CreateNavigator().WriteSubtree(writer);
            }
            Console.WriteLine();
            Console.WriteLine("-----");
        }

        static void DumpResponse(HttpWebResponse response)
        {
            Console.WriteLine("-----");
            Console.WriteLine("{0} {1}", (int)response.StatusCode, response.StatusDescription);
            using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8, true))
            {
                char[] buf = new char[256];
                int len;
                do
                {
                    len = reader.Read(buf, 0, 256);
                    Console.Write(buf, 0, len);
                } while (len > 0);
            }
        }

        private static string Eval(XPathNavigator nav, string xpath)
        {
            XPathNavigator node = nav.SelectSingleNode(xpath);
            return (node != null) ? node.InnerXml : null;
        }

        private enum Method
        {
            GET = 0,
            POST = 1
        }

    }
}
