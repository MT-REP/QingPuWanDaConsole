using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MainControl.MT_NET
{
    class PJLinkControl
    {
        public enum POWER_STATE:byte
        {
            POWER_OFF,
            POWER_ON
        }
        Int32 _serverPort = 4352;
        public TcpClient[,] client = new TcpClient[4, 2];
        public NetworkStream[,] stream = new NetworkStream[4, 2];
        private byte[] _projectPowerOff = new byte[] { 0x25,0x31, 0x50, 0x4F, 0x57, 0x52, 0x20, 0x30, 0x0D };
        private byte[] _projectPowerOn = new byte[] { 0x25, 0x31, 0x50, 0x4F, 0x57, 0x52, 0x20, 0x31, 0x0D };
        
        public void ProjectorPower(byte platformNumber, byte projectorNumber,POWER_STATE powerState)
        {
            Thread thread = new Thread(new ThreadStart(() =>
            {
                try
                {
                    // Create a TcpClient.
                    // Note, for this client to work you need to have a TcpServer 
                    // connected to the same address as specified by the server, port
                    // combination.
                    AppSettingsReader ar = new AppSettingsReader();
                    client[platformNumber, projectorNumber] = new TcpClient((string)ar.GetValue("Projector_IP_Dev" + (platformNumber + 1) + "Num" + (projectorNumber + 1), typeof(string)), (int)ar.GetValue("Projector_Port_Dev" + (platformNumber + 1) + "Num" + (projectorNumber + 1), typeof(int)));
                    stream[platformNumber, projectorNumber] = client[platformNumber, projectorNumber].GetStream();

                    if (POWER_STATE.POWER_OFF == powerState)
                    {
                        stream[platformNumber, projectorNumber].Write(_projectPowerOff, 0, _projectPowerOn.Length);
                    }
                    else
                    {
                        stream[platformNumber, projectorNumber].Write(_projectPowerOn, 0, _projectPowerOn.Length);
                    }
                    Byte[] data = new Byte[256];
                    stream[platformNumber, projectorNumber].Read(data, 0, data.Length);
                    stream[platformNumber, projectorNumber].Close();
                    client[platformNumber, projectorNumber].Close();
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
                    MessageBox.Show(e.Message, "SocketException:", MessageBoxButton.OK, MessageBoxImage.Error);
                    //Console.WriteLine("ArgumentNullException: {0}", e);
                }
                catch (SocketException e)
                {
                    MessageBox.Show("投影仪未供电或异常！\r\n" + e.Message, "SocketException:", MessageBoxButton.OK, MessageBoxImage.Error);
                    //Console.WriteLine("SocketException: {0}", e);
                }
                System.Windows.Threading.Dispatcher.Run();

            }));
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            
        }
    }


}
