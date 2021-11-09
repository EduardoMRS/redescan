using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.Net.NetworkInformation;
using System.Net;
using System.Diagnostics;
using CefSharp.WinForms;
using CefSharp;

namespace WEBTester___Analise_de_Redes
{
    public partial class Main : Form
    {
        public Main()
        {
            InitializeComponent();

            

        }

        Ping oPing = new Ping();
        PingReply resposta;
        ProcessStartInfo ps = new ProcessStartInfo();

        
        private void Main_Load(object sender, EventArgs e)
        {
            btIPBusc.PerformClick();
            bwsVelTest.Load("https://fast.com/pt/");
            bwsWebAcess.Load(lbIPgat.Text);
        }


        private void ipBuscBut_Click(object sender, EventArgs e)
        {
            IPHostEntry host;
            string localIP = "?";
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress miip in host.AddressList)
            {
                if (miip.AddressFamily.ToString() == "InterNetwork")
                {
                    localIP = miip.ToString();
                    lbIPatua.Text = localIP.ToString();
                }
            }
            //scanner de Gateway IP 
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in adapters)
            {
                IPInterfaceProperties adapterProperties = adapter.GetIPProperties();
                GatewayIPAddressInformationCollection addresses = adapterProperties.GatewayAddresses;
                if (addresses.Count > 0)
                {
                    foreach (GatewayIPAddressInformation address in addresses)
                    {
                        lbIPgat.Text = address.Address.ToString() + "  ";
                    }
                }
            }
        }

        private void pingBut_Click(object sender, EventArgs e)
        {
            pingstor.Clear();
            pingstor.AppendText("Ping em: " + pingList.Text + Environment.NewLine);
            timePing.Enabled = true;
        }

        private void btIr_Click(object sender, EventArgs e)
        {
            bwsWebAcess.Load(txPGAtua.Text);
        }

        private void pagAtua_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
            {
                btIr.PerformClick();
            }
        }

        private void btYT_Click(object sender, EventArgs e)
        {
            bwsWebAcess.Load("https://www.youtube.com/");
        }

        private void tPLinkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bwsWebAcess.Load("192.168.0.1");
        }

        private void mercusyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bwsWebAcess.Load("192.168.1.1");
        }

        private void intelbrasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bwsWebAcess.Load("10.0.0.1");
        }

        private void novaResetadaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bwsWebAcess.Load("192.168.1.20");
        }

        private void configuradaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bwsWebAcess.Load("192.168.1.254:8090");
        }

        private void configuradaBugadaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bwsWebAcess.Load("192.168.1.254");
        }

        private void btWathsapp_Click(object sender, EventArgs e)
        {
            bwsWebAcess.Load("bwsWebAcess.Load");
        }

        private void timePing_Tick(object sender, EventArgs e)
        {
            try
            {
                resposta = oPing.Send(pingList.Text);
                pingstor.AppendText("IP: " + resposta.Address.ToString() + "Bytes: " + resposta.Buffer.Length.ToString() + "Tempo:" + resposta.RoundtripTime.ToString() + "ms TTL: " + resposta.Options.Ttl.ToString() + Environment.NewLine);
            }
            catch (Exception ex)
            {
                pingstor.AppendText("IP Inacessivel!\n");
            }
        }

        private void btStopPing_Click(object sender, EventArgs e)
        {
            pingstor.AppendText("\nPing Finalizado");
            timePing.Enabled = false;
        }
    }
}
