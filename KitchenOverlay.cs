﻿using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using TShockAPI;

namespace TerrariaKitchen
{
    public class KitchenOverlay
    {
        private TcpListener? wsServer;

        private HttpListener? httpListener;

        private bool ShuttingDown = false;

        private List<NetworkStream> wsStreams;

        private KitchenConfig Config;

        internal Func<string, string>? WebhookResponse;

        public event EventHandler<NetworkStream>? NewConnection;

        public event EventHandler<string>? SetAPIKey;

        public KitchenOverlay(KitchenConfig config)
        {
            wsStreams = new List<NetworkStream>();
            Config = config;
        }

        public void StartServer(int httpPort, int wsPort)
        {
            if (httpPort < 1024 || httpPort > 49151 || wsPort < 1024 || wsPort > 49151)
            {
                Console.WriteLine("(Terraria Kitchen) Ports out of range, cannot start web server.");
                return;
            }
            httpListener?.Close();
            wsServer?.Stop();

            var httpUrl = $"http://localhost:{httpPort}/";

            httpListener = new HttpListener();
            httpListener.Prefixes.Add(httpUrl);
            httpListener.Start();
            HttpLoop(httpListener);

            wsServer = new TcpListener(IPAddress.Parse("127.0.0.1"), wsPort);
            wsServer.Start();
            wsServer.BeginAcceptTcpClient(AcceptClient, wsServer);
            Console.WriteLine($"(Terraria Kitchen) Servers started. Overlay address: {httpUrl} ws address: ws://127.0.0.1:{wsPort}");
        }

        private string MenuHtml()
        {
            return MenuGenerator.GenerateMenu(Config.Entries, Config.Events);
        }

        private void HttpLoop(HttpListener httpListener)
        {
            new Thread(async () =>
            {
                while (!ShuttingDown)
                {
                    try
                    {
                        HttpListenerContext ctx = await httpListener.GetContextAsync();

                        // Peel out the requests and response objects
                        HttpListenerRequest req = ctx.Request;
                        HttpListenerResponse resp = ctx.Response;

                        byte[] data = Encoding.UTF8.GetBytes(GetResponseData(req.Url));
                        resp.ContentType = "text/html";
                        resp.ContentEncoding = Encoding.UTF8;
                        resp.ContentLength64 = data.LongLength;

                        // Write out to the response stream (asynchronously), then close it
                        await resp.OutputStream.WriteAsync(data, 0, data.Length);
                        resp.Close();

                    }
                    catch (Exception ex)
                    {
                        if (!ShuttingDown)
                        {
                            TShock.Log.Error("(Terraria Kitchen Error) - " + ex.Message);
                            TShock.Log.Error(ex.StackTrace);
                            Console.WriteLine("(Terraria Kitchen) An error is detected with the HTTP server.");
                        }
                    }
                }
            })
            { IsBackground = true }.Start();
        }

        private string textBody(string title, string body)
        {
            return $"<html><head><title>{WebUtility.HtmlEncode(title)}</title></head><body>{WebUtility.HtmlEncode(body)}</body></html>";
        }

        private string GetResponseData(Uri? uri)
        {
            var queryText = uri?.Query;
            if (queryText?.StartsWith("?") == true)
            {
                queryText = queryText.Substring(1);
            }
            var queries = queryText?.Split("&").ToDictionary(s => WebUtility.UrlDecode(s.Split("=")[0]), s => WebUtility.UrlDecode(s.Substring(s.IndexOf('=') + 1)));
            switch (uri?.AbsolutePath)
            {
                case "/menu":
                    return MenuHtml();
                case "/connectTwitch":
                    return $@"<html><head><title>{WebUtility.HtmlEncode("Connect Terraria Kitchen with Twitch")}</title></head>
<body>
<div style='margin: auto; text-align: center;'>
    <h1>Connect with your twitch account to let this plugin read chat and response to commands!</h1>
    <a href='{GetAuthUrl()}' style='display: inline-block;
    padding: 0.375rem 0.375rem;
    font-size: 1rem;
    font-weight: 400;
    line-height: 1.5;
    color: white;
    text-decoration: none;
    vertical-align: middle;
    cursor: pointer;
    -webkit-user-select: none;
    -moz-user-select: none;
    user-select: none;
    border: 1px solid #0d6efd;
    border-radius: 0.375rem;
    background-color: purple;
    transition: color .15s ease-in-out,background-color .15s ease-in-out,border-color .15s ease-in-out,box-shadow .15s ease-in-out;'>Connect with Twitch</a>
</div>
</body>
</html>";
                case "/registerauth":
                    if (CSRFSecret == null || queries?.ContainsKey("state") != true || queries["state"] != CSRFSecret)
                    {
                        return textBody("Terraria Kitchen Auth", "CSRF mismatch, authorization ignored. If you are attempting to authorize Terraria Kitchen, please restart the server and use the new URL.");
                    }
                    CSRFSecret = null;
                    if (queries.TryGetValue("access_token", out var token))
                    {
                        SetAPIKey?.Invoke(this, token);
                        if (!File.Exists(KitchenPlugin.ApiPath))
                        {
                            File.WriteAllText(KitchenPlugin.ApiPath, token);
                        }
                        Console.WriteLine("");
                        return textBody("Terraria Kitchen Auth", "Authorization success, you may now close this window.");
                    }
                    return textBody("Terraria Kitchen Auth", "Failed to authorize, please restart the server and use the new URL.");
                case "/auth":
                    if (queries?.TryGetValue("error_description", out var errorDesc) == true)
                    {
                        return textBody("Terraria Kitchen Auth", $"Error: {errorDesc}");
                    }

                    return @"<html><body></body><script>
(function() {
if(window.location.hash) window.location = 'http://localhost:7770/registerauth?' + window.location.hash.substr(1);
})();
</script></html>";
                default:
                    break;
            }
            return KitchenOverlayHtml.GeneratePage($"{uri?.Host}:{(wsServer?.LocalEndpoint as IPEndPoint)?.Port ?? Config.OverlayWsPort}");
        }

        public enum EOpcodeType
        {
            /* Denotes a continuation code */
            Fragment = 0,

            /* Denotes a text code */
            Text = 1,

            /* Denotes a binary code */
            Binary = 2,

            /* Denotes a closed connection */
            ClosedConnection = 8,

            /* Denotes a ping*/
            Ping = 9,

            /* Denotes a pong */
            Pong = 10
        }

        public static byte[] GetFrameFromString(string Message, EOpcodeType Opcode = EOpcodeType.Text)
        {
            byte[] response;
            byte[] bytesRaw = Encoding.Default.GetBytes(Message);
            byte[] frame = new byte[10];

            int indexStartRawData = -1;
            int length = bytesRaw.Length;

            frame[0] = (byte)(128 + (int)Opcode);
            if (length <= 125)
            {
                frame[1] = (byte)length;
                indexStartRawData = 2;
            }
            else if (length >= 126 && length <= 65535)
            {
                frame[1] = (byte)126;
                frame[2] = (byte)((length >> 8) & 255);
                frame[3] = (byte)(length & 255);
                indexStartRawData = 4;
            }
            else
            {
                frame[1] = (byte)127;
                frame[2] = (byte)((length >> 56) & 255);
                frame[3] = (byte)((length >> 48) & 255);
                frame[4] = (byte)((length >> 40) & 255);
                frame[5] = (byte)((length >> 32) & 255);
                frame[6] = (byte)((length >> 24) & 255);
                frame[7] = (byte)((length >> 16) & 255);
                frame[8] = (byte)((length >> 8) & 255);
                frame[9] = (byte)(length & 255);

                indexStartRawData = 10;
            }

            response = new byte[indexStartRawData + length];

            int i, reponseIdx = 0;

            //Add the frame bytes to the reponse
            for (i = 0; i < indexStartRawData; i++)
            {
                response[reponseIdx] = frame[i];
                reponseIdx++;
            }

            //Add the data bytes to the response
            for (i = 0; i < length; i++)
            {
                response[reponseIdx] = bytesRaw[i];
                reponseIdx++;
            }

            return response;
        }

        private void AcceptClient(IAsyncResult ar)
        {
            var listener = (TcpListener?)ar.AsyncState;

            if (ShuttingDown || listener == null)
            {
                return;
            }

            new Thread(() =>
            {
                var client = listener.EndAcceptTcpClient(ar);
                //Console.WriteLine("(TEST WEBSOCKET) Got connection from {0}", (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "Address not found");
                using (var stream = client.GetStream())
                {
                    while (!ShuttingDown)
                    {
                        while (!stream.DataAvailable)
                        {
                            if (!client.Connected || ShuttingDown)
                            {
                                // Dispose if no longer connected
                                //Console.WriteLine("(TEST WEBSOCKET) {0} Disconnected", (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "Address not found");
                                lock (wsStreams)
                                {
                                    wsStreams.Remove(stream);
                                }
                                return;
                            }
                        }

                        while (client.Available < 3)
                        {
                            if (!client.Connected || ShuttingDown)
                            {
                                // Dispose if no longer connected
                                //Console.WriteLine("(TEST WEBSOCKET) {0} Disconnected", (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "Address not found");
                                lock (wsStreams)
                                {
                                    wsStreams.Remove(stream);
                                }
                                return;
                            }
                        }

                        byte[] bytes = new byte[client.Available];
                        stream.Read(bytes, 0, bytes.Length);
                        String data = Encoding.UTF8.GetString(bytes);

                        if (Regex.IsMatch(data, "^GET", RegexOptions.IgnoreCase))
                        {
                            string swk = Regex.Match(data, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
                            string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                            byte[] swkaSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
                            string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);

                            //Console.WriteLine("(TEST WEBSOCKET) GET Received - {0}", (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString()  ?? "Address not found");

                            // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
                            byte[] response = Encoding.UTF8.GetBytes(
                                "HTTP/1.1 101 Switching Protocols\r\n" +
                                "Connection: Upgrade\r\n" +
                                "Upgrade: websocket\r\n" +
                                "Sec-WebSocket-Accept: " + swkaSha1Base64 + "\r\n\r\n");

                            stream.Write(response, 0, response.Length);

                            NewConnection?.Invoke(client, stream);
                            lock (wsStreams)
                            {
                                wsStreams.Add(stream);
                            }
                        }
                        else
                        {
                            bool fin = (bytes[0] & 0b10000000) != 0,
                            mask = (bytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"
                            int opcode = bytes[0] & 0b00001111, // expecting 1 - text message
                                offset = 2;
                            ulong msglen = bytes[1] & (ulong)0b01111111;

                            if (msglen == 126)
                            {
                                msglen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
                                offset = 4;
                            }
                            else if (msglen == 127)
                            {
                                msglen = BitConverter.ToUInt64(new byte[] { bytes[9], bytes[8], bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2] }, 0);
                                offset = 10;
                            }

                            if (msglen == 0)
                            {
                                //Console.WriteLine("(TEST WEBSOCKET) - EMPTY MESSAGE");
                            }
                            else if (mask)
                            {
                                byte[] decoded = new byte[msglen];
                                byte[] masks = new byte[4] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
                                offset += 4;

                                for (ulong i = 0; i < msglen; ++i)
                                    decoded[i] = (byte)(bytes[(ulong) offset + i] ^ masks[i % 4]);

                                string text = Encoding.UTF8.GetString(decoded);

                                if (text.StartsWith("PING "))
                                {
                                    var response = GetFrameFromString("PONG " + text.Substring(5));
                                    stream.Write(response, 0, response.Length);
                                }
                                else
                                {
                                    var resText = WebhookResponse?.Invoke(text);
                                    if (!string.IsNullOrEmpty(resText))
                                    {
                                        var responseBytes = GetFrameFromString(resText);
                                        stream.Write(responseBytes, 0, responseBytes.Length);
                                    }
                                }
                            }
                        }
                    }

                    lock (wsStreams)
                    {
                        wsStreams.Remove(stream);
                    }
                }
            })
            { IsBackground = true }.Start();

            listener.BeginAcceptTcpClient(AcceptClient, listener);
        }

        public void SendPacket(object packet)
        {
            lock (wsStreams)
            {
                foreach (var stream in wsStreams)
                {
                    if (stream.CanWrite)
                    {
                        try
                        {
                            var response = GetFrameFromString(JsonConvert.SerializeObject(packet));
                            stream.Write(response, 0, response.Length);
                        }
                        catch
                        {

                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            ShuttingDown = true;
            httpListener?.Abort();
            wsServer?.Stop();
        }

        public string? CSRFSecret { get; private set; }

        private readonly string[] Scopes = new string[]
        {
            "channel:manage:predictions",
            "chat:read",
            "chat:edit",
            "moderator:read:chatters",
            "user:read:broadcast",
            "whispers:edit",
        };

        private string GetAuthUrl()
        {
            CSRFSecret = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            var queries = new[]
            {
                new KeyValuePair<string, string>("client_id", TwitchConnection.clientId),
                new KeyValuePair<string, string>("redirect_uri", "http://localhost:7770/auth"),
                new KeyValuePair<string, string>("response_type", "token"),
                new KeyValuePair<string, string>("scope", string.Join(" ", Scopes)),
                new KeyValuePair<string, string>("state", CSRFSecret),
            };


            return new Uri("https://id.twitch.tv/oauth2/authorize?" + string.Join("&", queries.Select(q => $"{WebUtility.UrlEncode(q.Key)}={WebUtility.UrlEncode(q.Value)}"))).ToString();
        }
    }
}
