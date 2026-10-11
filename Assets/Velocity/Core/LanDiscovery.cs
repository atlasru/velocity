using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Velocity.Core
{
    public sealed class DiscoveredServer
    {
        public string Address,Name; public int Port,Players; public byte Map; public double LastSeen;
    }
    public sealed class LanDiscovery : IDisposable
    {
        public const int DiscoveryPort=27028;
        private readonly UdpTransport transport;
        private readonly LanServer host;
        private readonly string nonce=LanServer.RandomToken().ToString("x16");
        private readonly List<IPEndPoint> broadcasts=new List<IPEndPoint>();
        private readonly Dictionary<string,double> replies=new Dictionary<string,double>();
        public readonly Dictionary<string,DiscoveredServer> Servers=new Dictionary<string,DiscoveredServer>();
        private double now,lastScan=-2;
        public LanDiscovery(LanServer server=null)
        {
            host=server; transport=new UdpTransport(server==null?0:DiscoveryPort,true);
            broadcasts.Add(new IPEndPoint(IPAddress.Broadcast,DiscoveryPort));
            if(server!=null) return;
            try
            {
                foreach(NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if(adapter.OperationalStatus!=OperationalStatus.Up) continue;
                    foreach(UnicastIPAddressInformation ip in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if(ip.Address.AddressFamily!=AddressFamily.InterNetwork||ip.IPv4Mask==null) continue;
                        byte[] address=ip.Address.GetAddressBytes(),mask=ip.IPv4Mask.GetAddressBytes();
                        for(int i=0;i<4;i++) address[i]|=(byte)~mask[i];
                        broadcasts.Add(new IPEndPoint(new IPAddress(address),DiscoveryPort));
                    }
                }
            }
            catch(NetworkInformationException) { }
        }
        public void Pump(double time)
        {
            now=time; transport.Poll(Receive);
            if(host==null&&now-lastScan>1)
            {
                byte[] request=Encoding.ASCII.GetBytes("VLOC1?"+nonce);
                foreach(IPEndPoint endpoint in broadcasts) transport.Send(request,endpoint);
                lastScan=now;
                var expired=new List<string>(); foreach(var s in Servers) if(now-s.Value.LastSeen>3) expired.Add(s.Key); foreach(string key in expired) Servers.Remove(key);
            }
        }
        private void Receive(byte[] data,int size,IPEndPoint endpoint)
        {
            if(size<6||size>160) return;
            string message=Encoding.UTF8.GetString(data,0,size);
            if(host!=null)
            {
                if(size!=22||!message.StartsWith("VLOC1?",StringComparison.Ordinal)) return;
                string key=endpoint.ToString();
                if(replies.TryGetValue(key,out double previous)&&now-previous<.5) return;
                if(replies.Count>64) replies.Clear(); replies[key]=now;
                string name=host.Simulation.Players[1].Name.Replace("|","");
                string response="VLOC1|"+message.Substring(6)+"|"+host.Transport.Port+"|"+host.Simulation.Map.Id+"|"+host.Simulation.Players.Count+"|"+name;
                transport.Send(Encoding.UTF8.GetBytes(response),endpoint); return;
            }
            string[] parts=message.Split('|');
            if(parts.Length!=6||parts[0]!="VLOC1"||parts[1]!=nonce||!int.TryParse(parts[2],out int port)||port<1||port>65535||!byte.TryParse(parts[3],out byte map)||map>2||!int.TryParse(parts[4],out int players)||players<1||players>8) return;
            Servers[endpoint.Address+":"+port]=new DiscoveredServer{Address=endpoint.Address.ToString(),Port=port,Map=map,Players=players,Name=GameSimulation.CleanName(parts[5]),LastSeen=now};
        }
        public void Query(IPEndPoint endpoint) => transport.Send(Encoding.ASCII.GetBytes("VLOC1?"+nonce),endpoint);
        public void Dispose() => transport.Dispose();
    }
}
