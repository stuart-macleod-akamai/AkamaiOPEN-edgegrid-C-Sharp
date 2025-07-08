// Copyright 2014 Akamai Technologies http://developer.akamai.com.
//
// Licensed under the Apache License, KitVersion 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// Author: colinb@akamai.com  (Colin Bendell)
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Akamai.EdgeGrid.Auth;

namespace Akamai.EdgeGrid
{
    /// <summary>
    /// Command Line sample application to demonstrate the utilization of the {Open} APIs.
    /// This can be used for both command line invocation or reference on how to leverage the
    /// Api. All supported commands are implemented in this sample for convience.
    ///
    /// Author: colinb@akamai.com  (Colin Bendell)
    /// </summary>
    class EdgeGridConsole
    {
        static void Main(string[] args)
        {
            string edgeRCFile = "";
            string section = "";
            string ask = "";
            string path = "";
            List<string> headers = [];
            string method = "GET";
            string contentType = "application/json";

            string outputfile = "";
            string uploadfile = "";
            string data = "";

            bool verbose = false;

            string? firstarg = null;
            foreach (string arg in args)
            {
                if (firstarg != null)
                {
                    switch (firstarg)
                    {
                        case "-p":
                            path = arg;
                            break;
                        case "-e":
                            edgeRCFile = arg;
                            break;
                        case "-s":
                            section = arg;
                            break;
                        case "-a":
                            ask = arg;
                            break;
                        case "-d":
                            if (method == "GET")
                                method = "POST";
                            data = arg;
                            break;
                        case "-f":
                            if (method == "GET")
                                method = "PUT";
                            uploadfile = arg;
                            break;
                        case "-H":
                            headers.Add(arg);
                            break;
                        case "-o":
                            outputfile = arg;
                            break;
                        case "-T":
                            contentType = arg;
                            break;
                        case "-X":
                            method = arg;
                            break;
                    }
                    firstarg = null;
                }
                else if (arg == "-h" || arg == "--help" || arg == "/?")
                {
                    Help();
                    return;
                }
                else if (arg == "-v" || arg == "-vv")
                    verbose = true;
                else if (!arg.StartsWith("-"))
                    path = arg;
                else
                    firstarg = arg;
            }

            if (verbose)
            {
                Console.WriteLine("{0} {1}", method, path);
                Console.WriteLine("EdgeRCFile: {0}", edgeRCFile);
                Console.WriteLine("Section: {0}", section);
                if (data != null)
                    Console.WriteLine("Data: [{0}]", data);
                if (uploadfile != null)
                    Console.WriteLine("UploadFile: {0}", uploadfile);
                if (outputfile != null)
                    Console.WriteLine("OutputFile: {0}", outputfile);
                foreach (string header in headers)
                    Console.WriteLine("{0}", header);
                Console.WriteLine("Content-Type: {0}", contentType);
            }

            Execute(
                method: method,
                path: path,
                headers: headers,
                edgeRCFile: edgeRCFile,
                section: section,
                accountSwitchKey: ask,
                data: data,
                uploadfile: uploadfile,
                outputfile: outputfile,
                contentType: contentType,
                verbose: verbose
            );
        }

        static void Execute(
            string method,
            string path,
            List<string> headers,
            string edgeRCFile,
            string section,
            string accountSwitchKey,
            string? data,
            string? uploadfile,
            string? outputfile,
            string contentType,
            bool verbose = false
        )
        {
            if (path == null)
            {
                Help();
                return;
            }

            EdgeGridV2Signer Signer = new();
            EdgeGridCredentials Credentials = new(edgeRCFile, section);

            // Add Account Switch Key to path if provided
            string AccountSwitchKey = "";
            if (!string.IsNullOrEmpty(accountSwitchKey))
            {
                AccountSwitchKey = accountSwitchKey;
            }
            else if(Credentials.AccountKey != null && Credentials.AccountKey != "")
            {
                AccountSwitchKey = Credentials.AccountKey;
            }

            if (AccountSwitchKey != "")
            {
                if (path.Contains("?"))
                {
                    path += "&accountSwitchKey=" + AccountSwitchKey;
                }
                else
                {
                    path += "?accountSwitchKey=" + AccountSwitchKey;
                }
            }

            Uri uri = new Uri($"https://{Credentials.Host}{path}");
            HttpRequestMessage Request = new HttpRequestMessage(new HttpMethod(method), uri);

            if (uploadfile != null && uploadfile != "")
            {
                FileStream stream = File.OpenRead(uploadfile);
                Request.Content = new StreamContent(stream);
            }
            else if (data != null && data != "")
            {
                HttpContent content = new StringContent(data, Encoding.UTF8, "application/json");
                Request.Content = content;
            }

            foreach (string header in headers)
            {
                var components = header.Split(':', 2);
                Request.Headers.Add(components[0], components[1]);
            }

            // Default Headers
            if (Request.Headers.Accept == null || !Request.Headers.Accept.Any())
            {
                Request.Headers.Add("accept", "application/json");
            }
            if (Request.Headers.UserAgent == null || !Request.Headers.UserAgent.Any())
            {
                Request.Headers.TryAddWithoutValidation("user-agent", "EdgeGridConsoleV2");
            }

            // Sign request
            Signer.Sign(Request, Credentials);
            Console.WriteLine("Authorization: {0}", Request.Headers.Authorization);
            Console.WriteLine();

            // Make request
            HttpClient Client = new HttpClient();
            HttpResponseMessage Response = Client.Send(Request);

            Console.WriteLine(
                "{0} {1}",
                (int)Response.StatusCode,
                Response.ReasonPhrase.ToString()
            );
            Console.WriteLine(Response.Headers.ToString());
            string responseBody = Response.Content.ReadAsStringAsync().Result;
            Console.WriteLine(responseBody);
        }

        static void Help()
        {
            Console.Error.WriteLine(
                @"
Usage: EdgeGridConeols <-e edgerc-file> <-s section> <-a account-switch-key>
           [-d data] [-f srcfile]
           [-o outfile]
           [-m max-size]
           [-X method]
           [-H header-line]
           [-T content-type]
           <url>

Where:
    -o outfile      local file name to use to save response from the API
    -d data         string of data to PUT to the API
    -f srcfile      local file used as source when action=upload
    -H header-line  Http Header 'Name: value'
    -X method       force HTTP PUT,POST,DELETE 
    -T content-type the HTTP content type (default = application/json)
    url             fully qualified api url such as https://akab-1234.luna.akamaiapis.net/diagnostic-tools/v1/locations       

"
            );
        }
    }
}
