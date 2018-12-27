using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MainControl.MT_UDP
{
    public enum DOF_state
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
    public enum M_nCmd
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
        public float[] Vxyz;                                  //{前向，侧向，升降}，向右为正，向下为正
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
    public class MtUdp 
    {
        delegate double UdpReceiveHandler(IPEndPoint udpEndPoint);
        public Task<UdpReceiveResult> m_UdpReceiveData;
        public DataToDOF m_sToDOFBuf = new DataToDOF();
        UdpClient m_listener;
        public Thread m_UdpDataProcess;
        public void UdpInit(int udpPort)
        {
            m_listener = new UdpClient(udpPort);
            m_UdpDataProcess = new Thread(UdpDataProcess);
            m_UdpDataProcess.IsBackground = true;
            m_UdpDataProcess.Start();
        }
        public void UdpDataProcess()
        {
            m_UdpReceiveData = m_listener.ReceiveAsync();
        }

        
        public void DofUpToMedian(IPEndPoint endPoint)
        {
            m_sToDOFBuf.nCheckID = 55;
            m_sToDOFBuf.nCmd = (byte)M_nCmd.S_CMD_Work;
            m_sToDOFBuf.nAct = 0;
            m_sToDOFBuf.nReserved = 0;
            for (int i = 0; i < 6; i++)
            {
                m_sToDOFBuf.DOFs[i] = 0.0f;
                m_sToDOFBuf.DOFs[i+3] = 0.0f;
                m_sToDOFBuf.Vxyz[i] = 0.0f;
                m_sToDOFBuf.Axyz[i] = 0.0f;
            }
            m_listener.Send(StructToBytes(m_sToDOFBuf,Marshal.SizeOf(m_sToDOFBuf)), Marshal.SizeOf(m_sToDOFBuf), endPoint);
        }
        public void DofToRun(IPEndPoint endPoint)
        {
            m_sToDOFBuf.nCheckID = 55;
            m_sToDOFBuf.nCmd = (byte)M_nCmd.S_CMD_RUN;
            m_sToDOFBuf.nAct = 0;
            m_sToDOFBuf.nReserved = 0;
            for (int i = 0; i < 6; i++)
            {
                m_sToDOFBuf.DOFs[i] = 0.0f;
                m_sToDOFBuf.DOFs[i + 3] = 0.0f;
                m_sToDOFBuf.Vxyz[i] = 0.0f;
                m_sToDOFBuf.Axyz[i] = 0.0f;
            }
            m_listener.Send(StructToBytes(m_sToDOFBuf, Marshal.SizeOf(m_sToDOFBuf)), Marshal.SizeOf(m_sToDOFBuf), endPoint);
        }
        public void DofToMedain(IPEndPoint endPoint)
        {
            m_sToDOFBuf.nCheckID = 55;
            m_sToDOFBuf.nCmd = (byte)M_nCmd.S_CMD_Back2MID;
            m_sToDOFBuf.nAct = 0;
            m_sToDOFBuf.nReserved = 0;
            for (int i = 0; i < 6; i++)
            {
                m_sToDOFBuf.DOFs[i] = 0.0f;
                m_sToDOFBuf.DOFs[i + 3] = 0.0f;
                m_sToDOFBuf.Vxyz[i] = 0.0f;
                m_sToDOFBuf.Axyz[i] = 0.0f;
            }
            m_listener.Send(StructToBytes(m_sToDOFBuf, Marshal.SizeOf(m_sToDOFBuf)), Marshal.SizeOf(m_sToDOFBuf), endPoint);
        }

        public void DofToBottom(IPEndPoint endPoint)
        {
            m_sToDOFBuf.nCheckID = 55;
            m_sToDOFBuf.nCmd = (byte)M_nCmd.S_CMD_Stop;
            m_sToDOFBuf.nAct = 0;
            m_sToDOFBuf.nReserved = 0;
            for (int i = 0; i < 6; i++)
            {
                m_sToDOFBuf.DOFs[i] = 0.0f;
                m_sToDOFBuf.DOFs[i + 3] = 0.0f;
                m_sToDOFBuf.Vxyz[i] = 0.0f;
                m_sToDOFBuf.Axyz[i] = 0.0f;
            }
            m_listener.Send(StructToBytes(m_sToDOFBuf, Marshal.SizeOf(m_sToDOFBuf)), Marshal.SizeOf(m_sToDOFBuf), endPoint);
        }
        public byte[] StructToBytes(object structObj, int size)
        {
            byte[] bytes = new byte[size];
            IntPtr structPtr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(structObj, structPtr, false);
            Marshal.Copy(structPtr, bytes, 0, size);
            Marshal.FreeHGlobal(structPtr);
            return bytes;
        }
    }
}
