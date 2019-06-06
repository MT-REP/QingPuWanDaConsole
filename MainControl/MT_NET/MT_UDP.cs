using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;



namespace MainControl.MT_UDP
{
    #region //DOF_state枚举
    public enum DOF_state:byte
    {
        dof_stop = 0,           //
        dof_sys_moving = 1,     //
        dof_neutral = 2,        //
        dof_working = 3,        //
        dof_setconf = 3,        //
        dof_select = 8,
        dof_check_id = 55,
        dof_closed = 253,
        dof_emg = 254,
        dof_err = 255
    };
    #endregion
    #region //M_nCmd枚举
    public enum M_nCmd:byte
    {
        S_CMD_RUN = 0,                                  //正常运行
        S_CMD_Check = 1,
        S_CMD_Back2MID = 2,                             // 底回中立位
        S_CMD_ToMerg = 3,                               //	紧急停机
        S_CMD_ToWork = 4,                               // 握手协议
        S_CMD_JOG = 5,                                  //单缸手动	
        S_CMD_Work = 6,                                 //由低位上升到中位
        S_CMD_Stop = 7,                                 //由中位回落到低位
        S_CMD_ChaConf = 8,                              //配置驱动器信息
        S_CMD_HOM = 9,
        S_CMD_JOYCTRL = 101,
        S_CMD_GAMECTRL = 102,
    };
    #endregion
    //GameControl枚举
    public enum GameCtrl:byte
    {
        CMD_DIY_START,
        CMD_RACE_START,
        CMD_RACE_END
    }
    #region //协议结构体定义
    [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct DataToDOF
    {
        public byte nCheckID;
        public byte nCmd;
        public byte nAct;
        public byte nReserved;                                 //保留
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public float[] DOFs;                                  //{横摇，纵倾，航向，前向，侧向，升降	}
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] Vxyz;                                  //[0] 代表发动机转速  [1] 代表时速。
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] Axyz;      //...
    };


    public struct DataToHost
    {
        public byte nCheckID;
        public byte nDOFStatus;
        public byte nRev0;                                     //需要使用
        public byte nRev1;                                     //需要使用
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public float[] attitude;                              //需要使用，RotorTrim，MixtureControl
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public float[] para;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public float[] motor_code;
    };
    #endregion
    public class MtUdp 
    {

        delegate double UdpReceiveHandler(IPEndPoint udpEndPoint);
        #region /*变量定义*/
        private const int deviceAmount = 4;
        public const int MESSAGE_SIZE = 6;
        public static int DeviceAmount
        {
            get { return MtUdp.deviceAmount; }
        }

        public bool[] m_DeviceConnectState = new bool[deviceAmount] { false, false, false, false };
        public int[] m_DeviceConnectCheckDelay = new int[deviceAmount] { 0, 0, 0, 0 };
        public const int NetMaxDelay =2000;
        public DataToDOF m_sToDOFBuf = new DataToDOF();
        public DataToHost[] m_sToHostBuf = new DataToHost[deviceAmount];                            //根据外部设备数量定义相应数据结构体
        public IPEndPoint m_CurRomateIpEndPoint = new IPEndPoint(0, 0);
        public IPEndPoint[] m_RemoteIpEndpoint = new IPEndPoint[deviceAmount];                      //根据外部设备数量定义地址数量
        public IPEndPoint m_PlcIpEndpoint = new IPEndPoint(IPAddress.Parse("192.168.0.120"), 5000);
        public IPEndPoint m_GmCtrlSoftwareIpEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8888);      //外方游戏控制软件地址及端口
        IPEndPoint m_LocalIPEndPoint = new IPEndPoint(IPAddress.Parse("192.168.0.130"), 10000);
        IPEndPoint m_AnyRemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
        public byte[] m_UdpReceiveBuf;
        public UdpClient m_listener;
        public Thread m_UdpDataProcess;

        public int m_PLCNetConnectCheckDelay = 0;
        public static string[] PlcConnectStateContent = new string[2] { "断开", "连接" };
        public string m_PLCConnectState = PlcConnectStateContent[0];
        public byte[] m_DataFromPlc = new byte[8] {0x51,0x15,0x0F,0x00,0x00,0x00,0x00,0x00};
        public byte[] m_DataToPlc = new byte[5] {0x00,0x00,0x00,0x00,0x00};
        public byte[] m_message=new byte[MESSAGE_SIZE] { (byte)'w', (byte)'a', (byte)'n', (byte)'d', (byte)'a', (byte)'?' };

        public bool m_EnableDiyCtrl = false;
        public bool m_EnableRaceCtrl = false;
        #endregion
        #region //Udp初始化
        public void UdpInit(int udpPort)
        {
            try
            {
                #region /*变量初始化*/
                AppSettingsReader ar = new AppSettingsReader();
                for (int i = 0; i < DeviceAmount; i++)
                {
                    m_RemoteIpEndpoint[i] = new IPEndPoint(IPAddress.Parse((string)ar.GetValue("GamePC_IP_Num" + (i + 1),typeof(string))), (int)ar.GetValue("GamePC_Port_Num" + (i+1),typeof(int)));
                }
                m_sToDOFBuf.DOFs = new float[6];
                m_sToDOFBuf.Vxyz = new float[3];
                m_sToDOFBuf.Axyz = new float[3];
                for (int i=0;i< DeviceAmount; i++)
                {
                    m_sToHostBuf[i].attitude = new float[6];
                    m_sToHostBuf[i].para = new float[6];
                    m_sToHostBuf[i].motor_code = new float[6];
                }
                #endregion
                //IPEndPoint localEP= new IPEndPoint(IPAddress.Parse("192.168.0.130"), udpPort);
                m_listener = new UdpClient(10000); //new UdpClient(localEP);
                m_listener.Client.Blocking = true;
                //解决UDP发送数据时，对方程序未打开时出现：System.Net.Sockets.SocketException:“远程主机强迫关闭了一个现有的连接。”
                uint IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                m_listener.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);

                m_UdpDataProcess = new Thread(UdpDataProcess);
                m_UdpDataProcess.IsBackground = true;
                m_UdpDataProcess.Start();
            }
            catch (Exception e)
            {
                if (MessageBox.Show(e.Message, "初始化套接字错误！", MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Environment.Exit(0);
                }
            }
        }
        #endregion
        
        #region //UDP接收数据处理
        public void UdpDataProcess()
        {
            while (true)
            {
                m_UdpReceiveBuf = m_listener.Receive(ref m_AnyRemoteIpEndPoint);
                for (int i = 0; i < 4; i++)
                {
                    if ((m_AnyRemoteIpEndPoint.Equals(m_RemoteIpEndpoint[i]))&&(m_UdpReceiveBuf.Length == 76))
                    {
                        if (null != ByteToStruct(m_UdpReceiveBuf, typeof(DataToHost)))
                        {
                            m_sToHostBuf[i] = (DataToHost)ByteToStruct(m_UdpReceiveBuf, typeof(DataToHost));
                            m_DeviceConnectCheckDelay[i] = 0;
                            m_DeviceConnectState[i] = true;
                        }

                    }
                    else
                    {
                        
                    }
                }
                if ((m_AnyRemoteIpEndPoint.Equals(m_PlcIpEndpoint))&&(m_UdpReceiveBuf.Length == m_DataFromPlc.Length))
                {
                    Array.Copy(m_UdpReceiveBuf, m_DataFromPlc, m_DataFromPlc.Length);
                    //Array.Reverse(m_DataFromPlc);
                    m_PLCNetConnectCheckDelay = 0;
                    //读取第一次PLC数据，保持相关状态，防止误动作；
                    if(m_PLCConnectState.Equals(PlcConnectStateContent[0]))
                    {
                        PlcDataInit(m_DataFromPlc);
                    }
                    m_PLCConnectState = PlcConnectStateContent[1];
                }
                else
                {
                    
                }
            }
        } 
        #endregion

        public void PfNetDelayCounter(byte index)
        {
            if (m_DeviceConnectCheckDelay[index] > NetMaxDelay)
            {
                m_DeviceConnectState[index] = false;
            }
            else
            {
                m_DeviceConnectCheckDelay[index]++;
            }
        }

        public void PlcNetDelayCounter()
        {
            if (m_PLCNetConnectCheckDelay > NetMaxDelay)
            {
                m_PLCConnectState = PlcConnectStateContent[0];
            }
            else
            {
                m_PLCNetConnectCheckDelay++;
            }
        }

        #region //寻底，上升至中位
        /// <summary>
        /// 
        /// </summary>
        /// <param name="endPoint"></param>
        public int DofUpToMedian(IPEndPoint endPoint)
        {
            m_sToDOFBuf.nCheckID = 55;
            m_sToDOFBuf.nCmd = (byte)M_nCmd.S_CMD_Work;
            m_sToDOFBuf.nAct = 0;
            m_sToDOFBuf.nReserved = 0;
            for (int i = 0; i < 3; i++)
            {
                m_sToDOFBuf.DOFs[i] = 0.0f;
                m_sToDOFBuf.DOFs[i+3] = 0.0f;
                m_sToDOFBuf.Vxyz[i] = 0.0f;
                m_sToDOFBuf.Axyz[i] = 0.0f;
            }
            return SendDataToDof(StructToBytes(m_sToDOFBuf,Marshal.SizeOf(m_sToDOFBuf)), Marshal.SizeOf(m_sToDOFBuf), endPoint);
        }
        #endregion
        #region //运行
        public int DofToRun(IPEndPoint endPoint)
        {
            m_sToDOFBuf.nCheckID = 55;
            m_sToDOFBuf.nCmd = (byte)M_nCmd.S_CMD_RUN;
            m_sToDOFBuf.nAct = 0;
            m_sToDOFBuf.nReserved = 0;
            for (int i = 0; i < 3; i++)
            {
                m_sToDOFBuf.DOFs[i] = 0.0f;
                m_sToDOFBuf.DOFs[i + 3] = 0.0f;
                m_sToDOFBuf.Vxyz[i] = 0.0f;
                m_sToDOFBuf.Axyz[i] = 0.0f;
            }
            return SendDataToDof(StructToBytes(m_sToDOFBuf, Marshal.SizeOf(m_sToDOFBuf)), Marshal.SizeOf(m_sToDOFBuf), endPoint);
        } 
        #endregion
        #region //任意姿态到中位

        public int DofToMedain(IPEndPoint endPoint)
        {
            m_sToDOFBuf.nCheckID = 55;
            m_sToDOFBuf.nCmd = (byte)M_nCmd.S_CMD_Back2MID;
            m_sToDOFBuf.nAct = 0;
            m_sToDOFBuf.nReserved = 0;
            for (int i = 0; i < 3; i++)
            {
                m_sToDOFBuf.DOFs[i] = 0.0f;
                m_sToDOFBuf.DOFs[i + 3] = 0.0f;
                m_sToDOFBuf.Vxyz[i] = 0.0f;
                m_sToDOFBuf.Axyz[i] = 0.0f;
            }
            return SendDataToDof(StructToBytes(m_sToDOFBuf, Marshal.SizeOf(m_sToDOFBuf)), Marshal.SizeOf(m_sToDOFBuf), endPoint);
        } 
        #endregion

        #region //到底位
        public int DofToBottom(IPEndPoint endPoint)
        {
            m_sToDOFBuf.nCheckID = 55;
            m_sToDOFBuf.nCmd = (byte)M_nCmd.S_CMD_Stop;
            m_sToDOFBuf.nAct = 0;
            m_sToDOFBuf.nReserved = 0;
            for (int i = 0; i < 3; i++)
            {
                m_sToDOFBuf.DOFs[i] = 0.0f;
                m_sToDOFBuf.DOFs[i + 3] = 0.0f;
                m_sToDOFBuf.Vxyz[i] = 0.0f;
                m_sToDOFBuf.Axyz[i] = 0.0f;
            }
            return SendDataToDof(StructToBytes(m_sToDOFBuf, Marshal.SizeOf(m_sToDOFBuf)), Marshal.SizeOf(m_sToDOFBuf), endPoint);
        } 
        #endregion

        #region //急停
        public int DofToEmergency(IPEndPoint endPoint)
        {
            m_sToDOFBuf.nCheckID = 55;
            m_sToDOFBuf.nCmd = 66;          
            m_sToDOFBuf.nAct = 0;
            m_sToDOFBuf.nReserved = 0;
            for (int i = 0; i < 3; i++)
            {
                m_sToDOFBuf.DOFs[i] = 0.0f;
                m_sToDOFBuf.DOFs[i + 3] = 0.0f;
                m_sToDOFBuf.Vxyz[i] = 0.0f;
                m_sToDOFBuf.Axyz[i] = 0.0f;
            }
            return SendDataToDof(StructToBytes(m_sToDOFBuf, Marshal.SizeOf(m_sToDOFBuf)), Marshal.SizeOf(m_sToDOFBuf), endPoint);
        }
        #endregion
        private void PlcDataInit(byte[] dataFromPlc)
        {
            if(1==((dataFromPlc[0]>>4)&0x01))           //1号梯为靠近状态
            {
                m_DataToPlc[0] |= 0x01 << 5;
            }
            if(1 == ((dataFromPlc[0] >> 6) & 0x01))     //2号梯为靠近状态
            {
                m_DataToPlc[0] |= 0x01 << 6;
            }
            if (1 == ((dataFromPlc[1] >> 0) & 0x01))    //3号梯为靠近状态
            {
                m_DataToPlc[0] |= 0x01 << 7;
            }
            if (1 == ((dataFromPlc[1] >> 2) & 0x01))    //4号梯为靠近状态
            {
                m_DataToPlc[1] |= 0x01 << 0;
            }
            if (1 == ((dataFromPlc[1] >> 4) & 0x01))    //5号梯为靠近状态
            {
                m_DataToPlc[1] |= 0x01 << 1;
            }
        }
        public int SendDataToDof(byte[] dgram, int bytes, IPEndPoint endPoint)
        {
            //任意状态下检测到PLC网络断开，则不给平台发数据；
            if(m_PLCConnectState.Equals(PlcConnectStateContent[1]))
            {
                return MtUdpSend(dgram, bytes, endPoint);
            }
            else
            {
                return -1;
            }
        }

        //游戏控制指令
        public void GmCtrlDiyStart()
        {
            if (true == m_EnableDiyCtrl)
            {
                m_message[5] = (byte)GameCtrl.CMD_DIY_START;
                MtUdpSend(m_message, m_message.Length, m_GmCtrlSoftwareIpEndpoint);
            }
        }
        public void GmCtrlRaceStart()
        {
            if(true==m_EnableRaceCtrl)
            {
                m_message[5] = (byte)GameCtrl.CMD_RACE_START;
                MtUdpSend(m_message, m_message.Length, m_GmCtrlSoftwareIpEndpoint);
            }
        }
        public void GmCtrlRaceEnd()
        {
            if (true == m_EnableRaceCtrl)
            {
                m_message[5] = (byte)GameCtrl.CMD_RACE_END;
                MtUdpSend(m_message, m_message.Length, m_GmCtrlSoftwareIpEndpoint);
            }
        }

        public int MtUdpSend(byte[] dgram, int bytes, IPEndPoint endPoint)
        {
            try
            {
                return m_listener.Send(dgram, bytes, endPoint);
            }
            catch (SocketException e)
            {
                return 0;
            }
            
        }

        #region //发送数据到PLC
        public void SendDataToPlc(byte[] dgram, int bytes, IPEndPoint endPoint)
        {
            MtUdpSend(dgram, bytes, endPoint);
        } 
        #endregion
        #region //结构体转字节数组
        public byte[] StructToBytes(object structObj, int size)
        {
            byte[] bytes = new byte[size];
            IntPtr structPtr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(structObj, structPtr, false);
            Marshal.Copy(structPtr, bytes, 0, size);
            Marshal.FreeHGlobal(structPtr);
            return bytes;
        } 
        #endregion

        #region //字节数组转结构体
        public object ByteToStruct(byte[] bytes, Type type)
        {
            int size = Marshal.SizeOf(type);
            if (size > bytes.Length)
            {
                return null;
            }
            IntPtr structPtr = Marshal.AllocHGlobal(size);
            Marshal.Copy(bytes, 0, structPtr, size);
            object obj = Marshal.PtrToStructure(structPtr, type);
            Marshal.FreeHGlobal(structPtr);
            return obj;
        } 
        #endregion
    }
}
