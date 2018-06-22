using System;
using System.Xml;

namespace XmlConfigHelper
{
    public class XmlHelper
    {
        private static XmlDocument Doc { get; } = new XmlDocument();

        public static string GetValueFromConfigByXPath(string docPath, string selectXPath = "//config/ipaddress[@name='main']/multicastaddress")
        {
            if(string.IsNullOrEmpty(docPath) || string.IsNullOrWhiteSpace(docPath))
                throw new ArgumentNullException($"Parameter {nameof(docPath)} can't be null or empty.");

            Doc.Load(docPath);

            //var xmlNodeList = Doc.SelectNodes("//config/ipadress[@name='main']/multicastaddress");
            var xNode = Doc.SelectSingleNode(selectXPath);

            //XmlElement root = Doc.DocumentElement;
            //XmlNodeList nodeList = root.GetElementsByTagName("multicastaddress");
            return xNode?.InnerText;
        }

        public static string GetTimeSpanMilisecondsByXPath(string docPath, string selectXPath = "//config/timing[@name='main']/timespanmiliseconds")
        {
            return GetValueFromConfigByXPath(docPath, selectXPath);
        }
    }
}
