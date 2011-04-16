using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using System.Xml.Linq;

namespace XNameDDNSClient
{
    /// <summary>
    /// A simple .NET DDNS client with minimal dependencies, that can 
    /// update an A record on XName's DNS servers.  
    /// 
    /// Author:         Brian Hodges
    /// Source:         https://github.com/bhodges/XNameDDNSClient
    /// License:        GPLv3 - A copy of the license can be acquired at: 
    ///                     http://www.gnu.org/licenses/gpl-3.0.txt
    /// Last Modified:  2011-03-09
    /// 
    /// Requires .NET 3.5 or higher.  Can be built with Visual Studio or from the command-line
    /// using csc:
    /// 
    /// C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe XNameDDNSClient.cs
    /// 
    /// XNameDDNSClient is also known to work on other operating systems 
    /// such as Linux with Mono. 
    /// 
    ///  1) Import trusted root certificates with the mozroots tool.
    ///         $ mozroots --import --sync 
    /// 
    ///  2) Compile xnameddnsclient.exe.
    ///         $ gmcs -r:System.Xml.Linq XNameDDNSClient.cs -out:xnameddnsclient.exe
    ///
    ///
    ///  To see a list of options, run xnameddnsclient.exe --help
    ///
    /// </summary>
    class Program
    {
        public const string URL = "https://www.xname.org/xmlrpc.php";
        public const string METHODNAME = "xname.updateArecord";
        public const string OLDADDRESS = "*";
        public const string TTL = "600";
        public const string VERSION = "1.0";
        public const string USERAGENT = ".NET XNameDDNSClient Version " + VERSION;
       
        static void Main(string[] args)
        {
            Dictionary<string, string> arguments = GetArguments(args);
            if (arguments["valid"] == "false")
            {
                Environment.Exit(1);
            }
            else
            {
                UpdateRecord(arguments);
            }
        }

        /// <summary>
        /// Parse command-line arguments.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static Dictionary<string, string> GetArguments(string[] args)
        {
            Dictionary<string, string> arguments = new Dictionary<string, string>();
            Regex reArg = new Regex(@"--(.*)=(.*)");
            arguments.Add("valid", "true");
            Regex reVersion = new Regex(@"--version");
            Regex reHelp = new Regex(@"--help");
            string argsString = String.Join(" ", args);

            if (reVersion.IsMatch(argsString))
            {
                Console.WriteLine("\n" + USERAGENT + "\n");
                arguments["valid"] = "false";
            }
            else if (reHelp.IsMatch(argsString))
            {
                Console.WriteLine("\n" + USERAGENT + "\n");
                PrintUsage();
                arguments["valid"] = "false";
            }
            else
            {
                foreach (string arg in args)
                {
                    if (reArg.IsMatch(arg))
                    {
                        Match m = reArg.Match(arg);
                        switch (m.Groups[1].Value)
                        {
                            case "zone":
                            case "name":
                            case "newaddress":
                            case "oldaddress":
                            case "ttl":
                            case "user":
                            case "password":
                                arguments.Add(m.Groups[1].Value, m.Groups[2].Value);
                                break;
                            default:
                                Console.WriteLine("Invalid option: --" + arg + "\n");
                                PrintUsage();
                                arguments["valid"] = "false";
                                break;
                        }
                        if (arguments["valid"] == "false")
                            break;  // Leave foreach loop                       
                    }
                }
            }
            // Check that all required arguments were passed in.
            if (arguments["valid"] == "true")
            {
                if ((!arguments.ContainsKey("zone")) || (!arguments.ContainsKey("name")) ||
                    (!arguments.ContainsKey("newaddress")) || (!arguments.ContainsKey("user")) || 
                    (!arguments.ContainsKey("password")))
                {
                    Console.WriteLine("Required argument(s) missing.\n");
                    PrintUsage();
                    arguments["valid"] = "false";
                }
            }

            // Set some defaults if options not specified.
            if (!arguments.ContainsKey("oldaddress"))       
                arguments.Add("oldaddress", OLDADDRESS);
            if (!arguments.ContainsKey("ttl"))
                arguments.Add("ttl", TTL);

            return arguments;
        }


        static void PrintUsage()
        {
            string usage = @"
Usage:  XNameDDNSClient --help
        XNameDDNSClient --version
        XNameDDNSClient --zone=example.com --name=www \ 
                        --newaddress=xxx.xxx.xxx.xxx \
                        --oldaddress=xxx.xxx.xxx.xxx \
                        --ttl=600 --user=username --password=myPassword

        --zone          - the zone to perform the update on
        --name          - the hostname record to update
        --newaddress    - the new IP address
        --oldaddress    - the previous IP address, but matches any by default
        --ttl           - the TTL in seconds, which defaults to 600
        --user          - the username that is authorized to update records 
                          for this zone
        --password      - the password for the account specified with --user
        --help          - prints this help text
        --version       - print the version number

        With the exception of --oldaddress and --ttl, all arguments are required.
        ";
            Console.WriteLine(usage);
        }

        /// <summary>
        /// Make the XMLRPC request.
        /// </summary>
        /// <param name="arguments"></param>
        private static void UpdateRecord(Dictionary<string, string> arguments)
        {
            WebRequest wr = WebRequest.Create(URL);
            ((HttpWebRequest)wr).UserAgent = USERAGENT;
            wr.Method = "POST";
            string xml = GetUpdateXML(arguments);
            wr.ContentLength = xml.Length;
            wr.ContentType = "text/xml";
            Stream s = wr.GetRequestStream();
            System.Text.UTF8Encoding  utf8 = new System.Text.UTF8Encoding();
            s.Write(utf8.GetBytes(xml), 0, utf8.GetBytes(xml).Length);
            s.Close();

            WebResponse response = ((HttpWebRequest)wr).GetResponse();
            string status = ((HttpWebResponse)response).StatusCode.ToString();
            Stream data = wr.GetRequestStream();            
        }

        /// <summary>
        /// Create a member node that will be part of the XML in the request.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private static XElement GetMember(string key, string value)
        {
            XElement member = new XElement("member", 
                                                new XElement("name", key),
                                                new XElement("value",
                                                    new XElement("string", value)
                                                )
                                            );
            return member;
        }

        /// <summary>
        /// Prepare XML that will be sent to the XName XMLRPC service.
        /// </summary>
        /// <returns></returns>
        private static string GetUpdateXML(Dictionary<string, string> arguments)
        {
            List<XElement> members = new List<XElement>();
            members.Add(GetMember("name", arguments["name"]));
            members.Add(GetMember("zone", arguments["zone"]));
            members.Add(GetMember("oldaddress", arguments["oldaddress"]));
            members.Add(GetMember("user", arguments["user"]));
            members.Add(GetMember("ttl", arguments["ttl"]));
            members.Add(GetMember("newaddress", arguments["newaddress"]));
            members.Add(GetMember("password", arguments["password"]));

            XElement updateXML = new XElement("methodcall",
                                    new XElement("methodname", METHODNAME),
                                    new XElement("params",
                                        new XElement("param", 
                                            new XElement("value", 
                                                new XElement("struct", members)
                                                )
                                            )
                                        )
                                    );
            return updateXML.ToString();
        }
    }
}
