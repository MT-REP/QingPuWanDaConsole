using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MainControl.MT_NET
{
    class PJLlinkControl
    {
        enum POWER_STATE:byte
        {
            POWER_OFF,
            POWER_ON
        }
        Int32 _serverPort = 4352;

        public int ServerPort { get => _serverPort; set => _serverPort = value; }
        private byte[] _projectPowerOff = new byte[] { 0x25,0x31, 0x50, 0x4F, 0x57, 0x52, 0x20, 0x30, 0x0D };
        private byte[] _projectPowerOn = new byte[] { 0x25, 0x31, 0x50, 0x4F, 0x57, 0x52, 0x20, 0x31, 0x0D };
        private NetworkStream[,] stream = new NetworkStream[4, 2];
        void TcpClientConnectToProjecte()
        {
            try
            {
                // Create a TcpClient.
                // Note, for this client to work you need to have a TcpServer 
                // connected to the same address as specified by the server, port
                // combination.
                TcpClient[,] client = new TcpClient[4,2];
                for(int i =0;i< 4;i++)
                {
                    for(int j=0;j<2;j++)
                    {
                        client[i, j] = new TcpClient("192.168.0.18" + i * 2 + j, ServerPort);
                        stream[i, j]= client[i, j].GetStream();
                    }
                }

                //// Send the message to the connected TcpServer. 
                //stream[i, j].Write(_projectPowerOn, 0, _projectPowerOn.Length);

                //// Buffer to store the response bytes.
                //data = new Byte[256];

                //// String to store the response ASCII representation.
                //String responseData = String.Empty;

                //// Read the first batch of the TcpServer response bytes.
                //Int32 bytes = stream.Read(data, 0, data.Length);
                //responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);
                //Console.WriteLine("Received: {0}", responseData);

                //// Close everything.
                //stream.Close();
                //client.Close();
            }
            catch (ArgumentNullException e)
            {
                Console.WriteLine("ArgumentNullException: {0}", e);
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
        }
        void ProjecterPower(byte platformNumber, byte projecterNumber,POWER_STATE powerState)
        {
            if(POWER_STATE.POWER_OFF==powerState)
            {
                stream[platformNumber, projecterNumber].Write(_projectPowerOff, 0, _projectPowerOn.Length);
            }
            else
            {
                stream[platformNumber, projecterNumber].Write(_projectPowerOn, 0, _projectPowerOn.Length);
            }
        }
    }


}
