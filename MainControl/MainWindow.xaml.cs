using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MainControl.MT_UDP;
using System.Net;
namespace MainControl
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    /// 

    #region /* Exported types ------------------------------------------------------------*/
    public enum DeviceId : byte
    {
        No_1 = 55,
        No_2 = 56,
        No_3 = 57,
        No_4 = 58
    }

    public enum LadderStatus : byte
    {
        CLOSED=0,
        AWAY=1,
        MOVING=2,
        ERROR=100
    }

    #endregion/* Exported types ------------------------------------------------------------*/
     
    public partial class MainWindow : Window
    {
        #region /* Private variables ---------------------------------------------------------*/
        private MtUdp m_ConsoleUdp = new MtUdp();
        private static Timer timer;
        private static readonly int m_LocalUdpPort = 10000;
        private bool InitOrWaitPassengerBtnClickFlag=false;
        private bool ExperienceBeginBtnClickFlag = false;
        private bool PF_InitOverFlag = false;
        private bool PF_BottomStatusFlag = false;
        private bool PF_EnableRunStatusFlag = false;
        private const byte LadderMounter = 5;
        private const byte DoorMounter = 4;
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            //本地网络初始化
            m_ConsoleUdp.UdpInit(m_LocalUdpPort);

            //发送数据多媒体定时器
            // Create an AutoResetEvent to signal the timeout threshold in the
            // timer callback has been reached.
            var autoEvent = new AutoResetEvent(false);
            timer = new Timer(new TimerCallback(TimerTask), autoEvent, 0,10);
        } 
        private void TimerTask(object timerState)
        {
            this.Dispatcher.Invoke(
                new System.Action(
                    delegate
                    {
                        //如果按下初始化按钮
                        if((InitOrWaitPassengerBtnClickFlag==true))         //(PF_InitOverFlag==false)&&
                        {
                            if (((LadderStatus.AWAY != GetLadderStatus(0, m_ConsoleUdp.m_DataFromPlc))
                             || (LadderStatus.AWAY != GetLadderStatus(1, m_ConsoleUdp.m_DataFromPlc))
                             || (LadderStatus.AWAY != GetLadderStatus(2, m_ConsoleUdp.m_DataFromPlc))
                             || (LadderStatus.AWAY != GetLadderStatus(3, m_ConsoleUdp.m_DataFromPlc))
                             || (LadderStatus.AWAY != GetLadderStatus(4, m_ConsoleUdp.m_DataFromPlc)))
                             && (PF_BottomStatusFlag == false)
                             )
                            {

                            }
                            //如果安全门都远离成功
                            else if((LadderStatus.AWAY ==GetLadderStatus(0,m_ConsoleUdp.m_DataFromPlc))
                             &&(LadderStatus.AWAY == GetLadderStatus(1,m_ConsoleUdp.m_DataFromPlc))
                             && (LadderStatus.AWAY == GetLadderStatus(2,m_ConsoleUdp.m_DataFromPlc))
                             && (LadderStatus.AWAY == GetLadderStatus(3,m_ConsoleUdp.m_DataFromPlc))
                             && (LadderStatus.AWAY == GetLadderStatus(4,m_ConsoleUdp.m_DataFromPlc))
                             && (PF_BottomStatusFlag == false))
                            {
                                //如果初始化未完成，且平台状态有不在中位时
                                if (((m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus != (byte)(DOF_state.dof_neutral))
                                    || (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus != (byte)(DOF_state.dof_neutral))
                                    || (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus != (byte)(DOF_state.dof_neutral))
                                    || (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus != (byte)(DOF_state.dof_neutral)))
                                    &&(PF_InitOverFlag == false)
                                    )
                                {
                                    for (int i = 0; i < MtUdp.DeviceAmount; i++)
                                    {
                                        m_ConsoleUdp.DofUpToMedian(m_ConsoleUdp.m_RemoteIpEndpoint[i]);
                                    }
                                }
                                    //如果初始化未完成，且平台都在中位时
                                else if ((m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus == (byte)(DOF_state.dof_neutral))
                                    && (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus == (byte)(DOF_state.dof_neutral))
                                    && (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus == (byte)(DOF_state.dof_neutral))
                                    && (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus == (byte)(DOF_state.dof_neutral))
                                    && (PF_InitOverFlag==false)
                                    )
                                {
                                    PF_InitOverFlag=true;
                                }
                                    //如果初始始化完成，且平台都不在底位时
                                else if(((m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus != (byte)(DOF_state.dof_check_id))
                                    || (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus != (byte)(DOF_state.dof_check_id))
                                    || (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus != (byte)(DOF_state.dof_check_id))
                                    || (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus != (byte)(DOF_state.dof_check_id)))
                                    &&(PF_InitOverFlag == true)
                                    )
                                {
                                    for (int i = 0; i < MtUdp.DeviceAmount; i++)
                                    {
                                        m_ConsoleUdp.DofToBottom(m_ConsoleUdp.m_RemoteIpEndpoint[i]);
                                    }
                                }
                                    //如果初始化完成，且平台在底位时
                                else if ((m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus == (byte)(DOF_state.dof_check_id))
                                    && (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus == (byte)(DOF_state.dof_check_id))
                                    && (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus == (byte)(DOF_state.dof_check_id))
                                    && (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus == (byte)(DOF_state.dof_check_id))
                                    && (PF_InitOverFlag==true)
                                    )
                                {
                                    PF_BottomStatusFlag = true;
                                }
                                
                            }
                            else if((m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus == (byte)(DOF_state.dof_check_id))
                                && (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus == (byte)(DOF_state.dof_check_id))
                                && (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus == (byte)(DOF_state.dof_check_id))
                                && (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus == (byte)(DOF_state.dof_check_id))
                                &&((LadderStatus.CLOSED !=GetLadderStatus(0,m_ConsoleUdp.m_DataFromPlc))
                                ||(LadderStatus.CLOSED != GetLadderStatus(1,m_ConsoleUdp.m_DataFromPlc))
                                || (LadderStatus.CLOSED != GetLadderStatus(2,m_ConsoleUdp.m_DataFromPlc))
                                || (LadderStatus.CLOSED != GetLadderStatus(3,m_ConsoleUdp.m_DataFromPlc))
                                || (LadderStatus.CLOSED != GetLadderStatus(4,m_ConsoleUdp.m_DataFromPlc)))
                                &&(PF_BottomStatusFlag==true)
                                )
                            {
                                for (byte i = 0; i < LadderMounter; i++)
                                {
                                    SetLadderClosed(i, m_ConsoleUdp.m_DataToPlc);
                                }
                                m_ConsoleUdp.SendDataToPlc(m_ConsoleUdp.m_DataToPlc, m_ConsoleUdp.m_DataToPlc.Length, m_ConsoleUdp.m_PlcIpEndpoint);
                            }
                            else if((m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus == (byte)(DOF_state.dof_check_id))
                                && (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus == (byte)(DOF_state.dof_check_id))
                                && (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus == (byte)(DOF_state.dof_check_id))
                                && (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus == (byte)(DOF_state.dof_check_id))
                                &&(LadderStatus.CLOSED ==GetLadderStatus(0,m_ConsoleUdp.m_DataFromPlc))
                                &&(LadderStatus.CLOSED == GetLadderStatus(1,m_ConsoleUdp.m_DataFromPlc))
                                && (LadderStatus.CLOSED == GetLadderStatus(2,m_ConsoleUdp.m_DataFromPlc))
                                && (LadderStatus.CLOSED == GetLadderStatus(3,m_ConsoleUdp.m_DataFromPlc))
                                && (LadderStatus.CLOSED == GetLadderStatus(4,m_ConsoleUdp.m_DataFromPlc))
                                &&(PF_BottomStatusFlag==true)
                                )
                            {
                                //此状态可以上下客
                                //激活运动按钮
                                InitOrWaitPassengerBtnClickFlag=false;
                            }
                        }
                        //如果按下体验开始按钮
                        else if(ExperienceBeginBtnClickFlag==true)
                        {
                            if (PF_EnableRunStatusFlag == true)
                            {
                                for (int i = 0; i < MtUdp.DeviceAmount; i++)
                                {
                                    m_ConsoleUdp.DofToRun(m_ConsoleUdp.m_RemoteIpEndpoint[i]);
                                }
                            }
                            //如果安全门在告诉状态
                            else if (((LadderStatus.AWAY != GetLadderStatus(0, m_ConsoleUdp.m_DataFromPlc))
                             || (LadderStatus.AWAY != GetLadderStatus(1, m_ConsoleUdp.m_DataFromPlc))
                             || (LadderStatus.AWAY != GetLadderStatus(2, m_ConsoleUdp.m_DataFromPlc))
                             || (LadderStatus.AWAY != GetLadderStatus(3, m_ConsoleUdp.m_DataFromPlc))
                             || (LadderStatus.AWAY != GetLadderStatus(4, m_ConsoleUdp.m_DataFromPlc)))
                             && (m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus == (byte)(DOF_state.dof_check_id))
                            && (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus == (byte)(DOF_state.dof_check_id))
                            && (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus == (byte)(DOF_state.dof_check_id))
                            && (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus == (byte)(DOF_state.dof_check_id))
                             )
                            {
                                for (byte i = 0; i < LadderMounter; i++)
                                {
                                    SetLadderAway(i, m_ConsoleUdp.m_DataToPlc);
                                }
                                m_ConsoleUdp.SendDataToPlc(m_ConsoleUdp.m_DataToPlc, m_ConsoleUdp.m_DataToPlc.Length, m_ConsoleUdp.m_PlcIpEndpoint);

                            }
                            //如果安全门都远离成功
                            else if ((LadderStatus.AWAY == GetLadderStatus(0, m_ConsoleUdp.m_DataFromPlc))
                             && (LadderStatus.AWAY == GetLadderStatus(1, m_ConsoleUdp.m_DataFromPlc))
                             && (LadderStatus.AWAY == GetLadderStatus(2, m_ConsoleUdp.m_DataFromPlc))
                             && (LadderStatus.AWAY == GetLadderStatus(3, m_ConsoleUdp.m_DataFromPlc))
                             && (LadderStatus.AWAY == GetLadderStatus(4, m_ConsoleUdp.m_DataFromPlc))
                             && (
                                    (m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus != (byte)(DOF_state.dof_neutral))
                                    || (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus != (byte)(DOF_state.dof_neutral))
                                    || (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus != (byte)(DOF_state.dof_neutral))
                                    || (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus != (byte)(DOF_state.dof_neutral))
                                 )
                            && (PF_EnableRunStatusFlag==false)
                             )
                            {
                                //如果初始化未完成，且平台状态有不在中位时
                                for (int i = 0; i < MtUdp.DeviceAmount; i++)
                                {
                                    m_ConsoleUdp.DofUpToMedian(m_ConsoleUdp.m_RemoteIpEndpoint[i]);
                                }

                            }
                            else if((LadderStatus.AWAY == GetLadderStatus(0, m_ConsoleUdp.m_DataFromPlc))
                             && (LadderStatus.AWAY == GetLadderStatus(1, m_ConsoleUdp.m_DataFromPlc))
                             && (LadderStatus.AWAY == GetLadderStatus(2, m_ConsoleUdp.m_DataFromPlc))
                             && (LadderStatus.AWAY == GetLadderStatus(3, m_ConsoleUdp.m_DataFromPlc))
                             && (LadderStatus.AWAY == GetLadderStatus(4, m_ConsoleUdp.m_DataFromPlc))
                             && (m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus == (byte)(DOF_state.dof_neutral))
                            && (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus == (byte)(DOF_state.dof_neutral))
                            && (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus == (byte)(DOF_state.dof_neutral))
                            && (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus == (byte)(DOF_state.dof_neutral))
                            && (PF_EnableRunStatusFlag == false)
                             )
                            {
                                for (int i = 0; i < MtUdp.DeviceAmount; i++)
                                {
                                    m_ConsoleUdp.DofUpToMedian(m_ConsoleUdp.m_RemoteIpEndpoint[i]);
                                }
                                PF_EnableRunStatusFlag = true;
                            }
                            
                        }
                        PlcDataHandler();
                        PlatformStatusIndicator();
                        //PlatformOperatorBtnHandler();
                    }
                    )
            );
        }
        #region //PLC数据处理
        private void PlcDataHandler()
        {
            #region //急停，运行，关机，复位按钮判断
            if (1 == ((m_ConsoleUdp.m_DataFromPlc[0] >> 0) & (0x01)))                             //判断急停按钮
            {
                //使软件按钮与硬件保持一致
                //待添加代码
                for (int i = 0; i < MtUdp.DeviceAmount; i++)
                {
                    m_ConsoleUdp.DofToEmergency(m_ConsoleUdp.m_RemoteIpEndpoint[i]);
                }
            }
            else if (1 == ((m_ConsoleUdp.m_DataFromPlc[0] >> 1) & (0x01)))                        //判断运行按钮
            {
                //使软件按钮与硬件保持一致
                //待添加代码
                for (int i = 0; i < MtUdp.DeviceAmount; i++)
                {
                    m_ConsoleUdp.DofToRun(m_ConsoleUdp.m_RemoteIpEndpoint[i]);
                }
            }
            else if (1 == ((m_ConsoleUdp.m_DataFromPlc[0] >> 2) & (0x01)))                 //判断关机按钮
            {
                //使软件按钮与硬件保持一致
                //待添加代码
            }
            else if (1 == ((m_ConsoleUdp.m_DataFromPlc[0] >> 3) & (0x01)))                 //判断复位按钮
            {
                //使软件按钮与硬件保持一致
                //待添加代码
                for (int i = 0; i < MtUdp.DeviceAmount; i++)
                {
                    m_ConsoleUdp.DofToBottom(m_ConsoleUdp.m_RemoteIpEndpoint[i]);
                }
                if(((byte)MT_UDP.DOF_state.dof_check_id)==m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus)
                {
                    //添加楼梯控制代码
                    //待添加代码
                    //m_ConsoleUdp.m_DataToPlc[0]=
                    //m_ConsoleUdp.SendDataToPlc()
                }
            } 
            #endregion

        }
        #endregion
        //指示平台网络连接状态
        private void PlatformStatusIndicator()
        {
            for (int i = 0; i < MtUdp.DeviceAmount; i++)
            {
                if (true == m_ConsoleUdp.m_DeviceConnectState[i])
                {
                    switch (i)
                    {
                        case 0:
                            CbNum1Platform.IsHitTestVisible = true;
                            CbNum1Platform.Background = Brushes.White;
                            break;
                        case 1:
                            CbNum2Platform.IsHitTestVisible = true;
                            CbNum2Platform.Background = Brushes.White;
                            break;
                        case 2:
                            CbNum3Platform.IsHitTestVisible = true;
                            CbNum3Platform.Background = Brushes.White;
                            break;
                        case 3:
                            CbNum4Platform.IsHitTestVisible = true;
                            CbNum4Platform.Background = Brushes.White;
                            break;
                    }

                }
                else
                {
                    switch (i)
                    {
                        case 0:
                            CbNum1Platform.IsHitTestVisible = false;
                            CbNum1Platform.Background = Brushes.Gray;
                            break;
                        case 1:
                            CbNum2Platform.IsHitTestVisible = false;
                            CbNum2Platform.Background = Brushes.Gray;
                            break;
                        case 2:
                            CbNum3Platform.IsHitTestVisible = false;
                            CbNum3Platform.Background = Brushes.Gray;
                            break;
                        case 3:
                            CbNum4Platform.IsHitTestVisible = false;
                            CbNum4Platform.Background = Brushes.Gray;
                            break;
                    }
                }
            }        
        }
        #region //车门锁定
        const UInt32 DoorCloseDelayMaxCounter = 50;
        UInt32[] DoorCloseDelayCounter = new UInt32[4];
        private void SetCarDoorClosed(byte index,byte[] status)
        {
            switch(index)
            {
                case 0:
                    if (DoorCloseDelayCounter[index] <= DoorCloseDelayMaxCounter)
                    {
                        status[2] |= 0x01 << 0;
                    }
                    else
                    {
                        status[2] &= (0x01 << 0)^0xFF;
                    }
                    DoorCloseDelayCounter[index]++;
                    break;
                case 1:
                    if (DoorCloseDelayCounter[index] <= DoorCloseDelayMaxCounter)
                    {
                        status[2] |= 0x01 << 1;
                    }
                    else
                    {
                        status[2] &= (0x01 << 1)^0xFF;
                    }
                    DoorCloseDelayCounter[index]++;
                    break;
                case 2:
                    if (DoorCloseDelayCounter[index] <= DoorCloseDelayMaxCounter)
                    {
                        status[2] |= 0x01 << 2;
                    }
                    else
                    {
                        status[2] &= (0x01 << 2)^0xFF;
                    }
                    DoorCloseDelayCounter[index]++;
                    break;
                case 3:
                    if (DoorCloseDelayCounter[index] <= DoorCloseDelayMaxCounter)
                    {
                        status[2] |= 0x01 << 3;
                    }
                    else
                    {
                        status[2] &= (0x01 << 3)^0xFF;
                    }
                    DoorCloseDelayCounter[index]++;
                    break;
            }
        }
        #endregion
        #region //车门打开
        const UInt32 DoorOpenDelayMaxCounter = 50;
        UInt32[] DoorOpenDelayCounter = new UInt32[4];
        private void SetCarDoorOpened(byte index, byte[] status)
        {
            switch (index)
            {
                case 0:
                    if (DoorOpenDelayCounter[index] <= DoorOpenDelayMaxCounter)
                    {
                        status[2] |= 0x01 << 4;
                    }
                    else
                    {
                        status[2] &= (0x01 << 4) ^ 0xFF;
                    }
                    DoorOpenDelayCounter[index]++;
                    break;
                case 1:
                    if (DoorOpenDelayCounter[index] <= DoorOpenDelayMaxCounter)
                    {
                        status[2] |= 0x01 << 5;
                    }
                    else
                    {
                        status[2] &= (0x01 << 5) ^ 0xFF;
                    }
                    DoorOpenDelayCounter[index]++;
                    break;
                case 2:
                    if (DoorOpenDelayCounter[index] <= DoorOpenDelayMaxCounter)
                    {
                        status[2] |= 0x01 << 6;
                    }
                    else
                    {
                        status[2] &= (0x01 << 6) ^ 0xFF;
                    }
                    DoorOpenDelayCounter[index]++;
                    break;
                case 3:
                    if (DoorOpenDelayCounter[index] <= DoorOpenDelayMaxCounter)
                    {
                        status[2] |= 0x01 << 7;
                    }
                    else
                    {
                        status[2] &= (0x01 << 7) ^ 0xFF;
                    }
                    DoorOpenDelayCounter[index]++;
                    break;
            }
        }
        #endregion
        #region //获取楼梯当前状态
        LadderStatus GetLadderStatus(byte index,byte[] allStatus)
        {
            switch(index)
            {
                case 0:
                    if ((1 == ((allStatus[0] >> 4) & 0x01)) && (1 != ((allStatus[0] >> 5) & 0x01)))
                    {
                        return LadderStatus.CLOSED;
                    }
                    else if ((1 != ((allStatus[0] >> 4) & 0x01)) && (1 == ((allStatus[0] >> 5) & 0x01)))
                    {
                        return LadderStatus.AWAY;
                    }
                    else if((1 != ((allStatus[0] >> 4) & 0x01)) && (1 != ((allStatus[0] >> 5) & 0x01)))
                    {
                        return LadderStatus.MOVING;
                    }
                    else
                    {
                        return LadderStatus.ERROR;
                    }
                case 1:
                    if ((1 == ((allStatus[0] >> 6) & 0x01)) && (1 != ((allStatus[0] >> 7) & 0x01)))
                    {
                        return LadderStatus.CLOSED;
                    }
                    else if ((1 != ((allStatus[0] >> 6) & 0x01)) && (1 == ((allStatus[0] >> 7) & 0x01)))
                    {
                        return LadderStatus.AWAY;
                    }
                    else if ((1 != ((allStatus[0] >> 6) & 0x01)) && (1 != ((allStatus[0] >> 7) & 0x01)))
                    {
                        return LadderStatus.MOVING;
                    }
                    else
                    {
                        return LadderStatus.ERROR;
                    }
                case 2:
                    if ((1 == ((allStatus[1] >> 0) & 0x01)) && (1 != ((allStatus[1] >> 1) & 0x01)))
                    {
                        return LadderStatus.CLOSED;
                    }
                    else if ((1 != ((allStatus[1] >> 0) & 0x01)) && (1 == ((allStatus[1] >> 1) & 0x01)))
                    {
                        return LadderStatus.AWAY;
                    }
                    else if ((1 != ((allStatus[1] >> 0) & 0x01)) && (1 != ((allStatus[1] >> 1) & 0x01)))
                    {
                        return LadderStatus.MOVING;
                    }
                    else
                    {
                        return LadderStatus.ERROR;
                    }
                case 3:
                    if ((1 == ((allStatus[1] >> 2) & 0x01)) && (1 != ((allStatus[1] >> 3) & 0x01)))
                    {
                        return LadderStatus.CLOSED;
                    }
                    else if ((1 != ((allStatus[1] >> 2) & 0x01)) && (1 == ((allStatus[1] >> 3) & 0x01)))
                    {
                        return LadderStatus.AWAY;
                    }
                    else if ((1 != ((allStatus[1] >> 2) & 0x01)) && (1 != ((allStatus[1] >> 3) & 0x01)))
                    {
                        return LadderStatus.MOVING;
                    }
                    else
                    {
                        return LadderStatus.ERROR;
                    }
                case 4:
                    if ((1 == ((allStatus[1] >> 4) & 0x01)) && (1 != ((allStatus[1] >> 5) & 0x01)))
                    {
                        return LadderStatus.CLOSED;
                    }
                    else if ((1 != ((allStatus[1] >> 4) & 0x01)) && (1 == ((allStatus[1] >> 5) & 0x01)))
                    {
                        return LadderStatus.AWAY;
                    }
                    else if ((1 != ((allStatus[1] >> 4) & 0x01)) && (1 != ((allStatus[1] >> 5) & 0x01)))
                    {
                        return LadderStatus.MOVING;
                    }
                    else
                    {
                        return LadderStatus.ERROR;
                    }
            }
            return LadderStatus.ERROR;
        }
        #endregion
        #region //楼梯远离
        const UInt32 LadderAwayMaxDelayCounter = 1000;
        UInt32[] LadderAwayDelayCounter = new UInt32[5];
        private void SetLadderAway(byte index, byte[] status)
        {
            switch (index)
            {
                case 0:
                    status[0] |= 0x01 << 5;
                    if (LadderAwayDelayCounter[index] >= LadderAwayMaxDelayCounter)
                    {
                        //设备滑梯状态为出错
                    }
                    else
                    {
                        LadderAwayDelayCounter[index]++;
                    }
                    break;
                case 1:
                    status[0] |= 0x01 << 6;
                    if (LadderAwayDelayCounter[index] >= LadderAwayMaxDelayCounter)
                    {
                        //设备滑梯状态为出错
                    }
                    else
                    {
                        LadderAwayDelayCounter[index]++;
                    }
                    break;
                case 2:
                    status[0] |= 0x01 << 7;
                    if (LadderAwayDelayCounter[index] >= LadderAwayMaxDelayCounter)
                    {
                        //设备滑梯状态为出错
                    }
                    else
                    {
                        LadderAwayDelayCounter[index]++;
                    }
                    break;
                case 3:
                    status[1] |= 0x01 << 0;
                    if (LadderAwayDelayCounter[index] >= LadderAwayMaxDelayCounter)
                    {
                        //设备滑梯状态为出错
                    }
                    else
                    {
                        LadderAwayDelayCounter[index]++;
                    }
                    break;
                case 4:
                    status[1] |= 0x01 << 1;
                    if (LadderAwayDelayCounter[index] >= LadderAwayMaxDelayCounter)
                    {
                        //设备滑梯状态为出错
                    }
                    else
                    {
                        LadderAwayDelayCounter[index]++;
                    }
                    break;
            }
        }
        #endregion
        #region //楼梯靠近
        const UInt32 LadderClosedMaxDelayCounter = 1000;
        UInt32[] LadderClosedDelayCounter = new UInt32[5];
        private void SetLadderClosed(byte index, byte[] status)
        {
            switch (index)
            {
                case 0:
                    status[0] &= (0x01 << 5)^0xff;
                    if (LadderClosedDelayCounter[index] >= LadderClosedMaxDelayCounter)
                    {

                    }
                    else
                    {
                        LadderClosedDelayCounter[index]++;
                    }
                    
                    break;
                case 1:
                    status[0] &= (0x01 << 6) ^ 0xff;
                    if (LadderClosedDelayCounter[index] >= LadderClosedMaxDelayCounter)
                    {

                    }
                    else
                    {
                        LadderClosedDelayCounter[index]++;
                    }
                    break;
                case 2:
                    status[0] &= (0x01 << 7) ^ 0xff;
                    if (LadderClosedDelayCounter[index] >= LadderClosedMaxDelayCounter)
                    {

                    }
                    else
                    {
                        LadderClosedDelayCounter[index]++;
                    }
                    break;
                case 3:
                    status[1] &= (0x01 << 0) ^ 0xff;
                    if (LadderClosedDelayCounter[index] >= LadderClosedMaxDelayCounter)
                    {

                    }
                    else
                    {
                        LadderClosedDelayCounter[index]++;
                    }
                    break;
                case 4:
                    status[1] &= (0x01 << 1) ^ 0xff;
                    if (LadderClosedDelayCounter[index] >= LadderClosedMaxDelayCounter)
                    {

                    }
                    else
                    {
                        LadderClosedDelayCounter[index]++;
                    }
                    break;
            }
        }
        #endregion

        private void InitOrWaitPassenger_Click(object sender, RoutedEventArgs e)
        {
            InitOrWaitPassengerBtnClickFlag = true;
            ExperienceBeginBtnClickFlag = false;
        }

        private void ExperienceBegin_Click(object sender, RoutedEventArgs e)
        {
            InitOrWaitPassengerBtnClickFlag = false;
            ExperienceBeginBtnClickFlag = true;
        }
    }
}
