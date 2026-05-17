using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml;

namespace Next.Hazel.UPnP;

/// <summary>
///     Status of the UPnP capabilities
/// </summary>
public enum UPnPStatus
{
    /// <summary>
    ///     Still discovering UPnP capabilities
    /// </summary>
    Discovering,

    /// <summary>
    ///     UPnP is not available
    /// </summary>
    NotAvailable,

    /// <summary>
    ///     UPnP is available and ready to use
    /// </summary>
    Available
}

public class UPnPHelper : IDisposable
{
    private const int DiscoveryTimeOutMs = 1000;
    private readonly byte[] buffer;

    private readonly ManualResetEvent discoveryComplete = new(false);

    private readonly ILogger logger;
    private readonly Socket socket;

    private DateTime discoveryResponseDeadline;

    private EndPoint ep;
    private string serviceName = "";

    private string serviceUrl;

    public UPnPHelper(ILogger logger)
    {
        this.logger = logger;

        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.EnableBroadcast = true;
        socket.MulticastLoopback = false;

        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));

        ep = new IPEndPoint(IPAddress.Any, 1900);
        buffer = new byte[ushort.MaxValue];

        ListenForUPnP();

        Discover();
    }

    /// <summary>
    ///     Status of the UPnP capabilities of this NetPeer
    /// </summary>
    public UPnPStatus Status { get; private set; }

    public void Dispose()
    {
        discoveryComplete.Dispose();
        try
        {
            socket.Shutdown(SocketShutdown.Both);
        }
        catch
        {
            // ignored
        }

        socket.Dispose();
    }

    private void ListenForUPnP()
    {
        try
        {
            socket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref ep, HandleMessage, null);
        }
        catch (Exception e)
        {
            logger.WriteInfo("Exception listening for UPnP: " + e.Message);
        }
    }

    private void HandleMessage(IAsyncResult ar)
    {
        int len;
        try
        {
            len = socket.EndReceiveFrom(ar, ref ep);
        }
        catch
        {
            return;
        }

        var resp = Encoding.UTF8.GetString(buffer, 0, len);
        if (resp.Contains("upnp:rootdevice") || resp.Contains("UPnP/1.0"))
        {
            var locationStart = resp.IndexOf("location:", StringComparison.OrdinalIgnoreCase);
            if (locationStart >= 0)
            {
                locationStart += 10;
                var locationEnd = resp.IndexOf('\r', locationStart);

                resp = resp.Substring(locationStart, locationEnd - locationStart);
                if (!ExtractServiceUrl(resp)) ListenForUPnP();
            }
            else
            {
                ListenForUPnP();
            }
        }
        else
        {
            ListenForUPnP();
        }
    }

    internal void Discover()
    {
        var str =
            "M-SEARCH * HTTP/1.1\r\n" +
            "HOST: 239.255.255.250:1900\r\n" +
            "ST:upnp:rootdevice\r\n" +
            "MAN:\"ssdp:discover\"\r\n" +
            "MX:3\r\n\r\n";

        discoveryResponseDeadline = DateTime.UtcNow.AddSeconds(6);
        Status = UPnPStatus.Discovering;

        var bytes = Encoding.UTF8.GetBytes(str);

        logger.WriteInfo("Attempting UPnP discovery");

        socket.SendTo(bytes, new IPEndPoint(NetUtility.GetBroadcastAddress(), 1900));
    }

    internal bool ExtractServiceUrl(string resp)
    {
        try
        {
            var desc = new XmlDocument();
            using var client = new HttpClient();
            using var response = client.GetStreamAsync(resp).Result;
            desc.Load(response);

            var nsMgr = new XmlNamespaceManager(desc.NameTable);
            nsMgr.AddNamespace("tns", "urn:schemas-upnp-org:device-1-0");
            var typeNode = desc.SelectSingleNode("//tns:device/tns:deviceType/text()", nsMgr);
            if (typeNode?.Value == null)
                return false;
            
            if (!typeNode.Value.Contains("InternetGatewayDevice"))
                return false;

            serviceName = "WANIPConnection";
            var node = desc.SelectSingleNode(
                "//tns:service[tns:serviceType=\"urn:schemas-upnp-org:service:" + serviceName +
                ":1\"]/tns:controlURL/text()", nsMgr);
            if (node == null)
            {
                //try another service name
                serviceName = "WANPPPConnection";
                node = desc.SelectSingleNode(
                    "//tns:service[tns:serviceType=\"urn:schemas-upnp-org:service:" + serviceName +
                    ":1\"]/tns:controlURL/text()", nsMgr);
                if (node == null)
                    return false;
            }

            serviceUrl = CombineUrls(resp, node.Value);
            logger.WriteInfo("UPnP service ready");
            Status = UPnPStatus.Available;
            discoveryComplete.Set();
            return true;
        }
        catch (Exception e)
        {
            logger.WriteError("Exception while parsing UPnP Service URL: " + e.Message);
            return false;
        }
    }

    private static string CombineUrls(string gatewayURL, string subURL)
    {
        // Is Control URL an absolute URL?
        if (subURL.Contains("http:") || subURL.Contains("."))
            return subURL;

        gatewayURL = gatewayURL.Replace("http://", ""); // strip any protocol
        var n = gatewayURL.IndexOf("/", StringComparison.Ordinal);
        if (n >= 0) gatewayURL = gatewayURL[..n]; // Use first portion of URL

        return "http://" + gatewayURL + subURL;
    }

    private bool CheckAvailability()
    {
        switch (Status)
        {
            case UPnPStatus.NotAvailable:
                break;
            case UPnPStatus.Available:
                return true;
            case UPnPStatus.Discovering:
                while (!discoveryComplete.WaitOne(DiscoveryTimeOutMs))
                    if (DateTime.UtcNow > discoveryResponseDeadline)
                    {
                        Status = UPnPStatus.NotAvailable;
                        return false;
                    }

                return true;
        }

        return false;
    }

    /// <summary>
    ///     Add a forwarding rule to the router using UPnP
    /// </summary>
    /// <param name="externalPort">The external, WAN facing, port</param>
    /// <param name="description">A description for the port forwarding rule</param>
    /// <param name="internalPort">The port on the client machine to send traffic to</param>
    /// <param name="durationSeconds">The lease duration on the port forwarding rule, in seconds. 0 for indefinite.</param>
    public bool ForwardPort(int externalPort, string description, int internalPort = 0, int durationSeconds = 0)
    {
        if (!CheckAvailability())
            return false;

        if (internalPort == 0)
            internalPort = externalPort;

        try
        {
            var client = NetUtility.GetMyAddress(out _);
            if (client == null)
                return false;

            SOAPRequest(serviceUrl,
                $"<u:AddPortMapping xmlns:u=\"urn:schemas-upnp-org:service:{serviceName}:1\">" +
                "<NewRemoteHost></NewRemoteHost>" +
                $"<NewExternalPort>{externalPort}</NewExternalPort>" +
                "<NewProtocol>UDP</NewProtocol>" +
                $"<NewInternalPort>{internalPort}</NewInternalPort>" +
                $"<NewInternalClient>{client}</NewInternalClient>" +
                "<NewEnabled>1</NewEnabled>" +
                $"<NewPortMappingDescription>{description}</NewPortMappingDescription>" +
                $"<NewLeaseDuration>{durationSeconds}</NewLeaseDuration>" +
                "</u:AddPortMapping>",
                "AddPortMapping");

            logger.WriteInfo("Sent UPnP port forward request.");
            return true;
        }
        catch (Exception ex)
        {
            logger.WriteError("UPnP port forward failed: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    ///     Delete a forwarding rule from the router using UPnP
    /// </summary>
    /// <param name="externalPort">The external, 'internet facing', port</param>
    public bool DeleteForwardingRule(int externalPort)
    {
        if (!CheckAvailability())
            return false;

        try
        {
            SOAPRequest(serviceUrl,
                $"<u:DeletePortMapping xmlns:u=\"urn:schemas-upnp-org:service:{serviceName}:1\">" +
                "<NewRemoteHost></NewRemoteHost>" +
                $"<NewExternalPort>{externalPort}</NewExternalPort>" +
                "<NewProtocol>UDP</NewProtocol>" +
                "</u:DeletePortMapping>", "DeletePortMapping");
            return true;
        }
        catch (Exception)
        {
            // m_peer.LogWarning("UPnP delete forwarding rule failed: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    ///     Retrieve the extern ip using UPnP
    /// </summary>
    public IPAddress GetExternalIP()
    {
        if (!CheckAvailability())
            return null;
        try
        {
            var xdoc = SOAPRequest(serviceUrl, "<u:GetExternalIPAddress xmlns:u=\"urn:schemas-upnp-org:service:" +
                                               serviceName + ":1\">" +
                                               "</u:GetExternalIPAddress>", "GetExternalIPAddress");
            var nsMgr = new XmlNamespaceManager(xdoc.NameTable);
            nsMgr.AddNamespace("tns", "urn:schemas-upnp-org:device-1-0");
            var IP = xdoc.SelectSingleNode("//NewExternalIPAddress/text()", nsMgr).Value;
            return IPAddress.Parse(IP);
        }
        catch (Exception)
        {
            // m_peer.LogWarning("Failed to get external IP: " + ex.Message);
            return null;
        }
    }

    private XmlDocument SOAPRequest(string url, string soap, string function)
    {
        var req =
            "<?xml version=\"1.0\"?>" +
            "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
            $"<s:Body>{soap}</s:Body>" +
            "</s:Envelope>";

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(req, Encoding.UTF8, "text/xml")
        };
        request.Headers.Add("SOAPACTION", $"\"urn:schemas-upnp-org:service:{serviceName}:1#{function}\"");

        using var client = new HttpClient();

        using var response = client.SendAsync(request).Result;

        var xmlDoc = new XmlDocument();
        xmlDoc.Load(response.Content.ReadAsStream());

        return xmlDoc;
    }
}