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
using System.Runtime.InteropServices;

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
    public enum DoorCtrlBtnStatus:byte
    {
        SETIDLE=0,
        SETLOCKED,
        SETUNLOCKED
    }
    public enum CarDoorStatus:byte
    {
        CLOSED=0,
        OPENED=1
    }
    public enum CarDoorLockStatus : byte
    {
        UNLOCKED = 0,
        LOCKED = 1,
        ACTIONING=100
    }
    public enum LadderCtrlBtnStatus : byte
    {
        SETIDLE = 0,
        SETCLOSE,           //靠近平台
        SETAWAY             //远离平台
    }
    public enum LadderStatus : byte
    {
        CLOSED=0,           //靠近平台
        AWAY=1,             //远离平台
        MOVING=2,
        ERROR=100
    }

    #endregion/* Exported types ------------------------------------------------------------*/
     
    public partial class MainWindow : Window
    {
        #region //DLL
        [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
        private static extern int FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", EntryPoint = "SetForegroundWindow", SetLastError = true)]
        private static extern bool SetForegroundWindow(int hWnd);
        #endregion
        #region //private const variables
        private const int LADDER_AMOUNT = 5;
        #endregion
        #region /* Private variables ---------------------------------------------------------*/
        private MtUdp m_ConsoleUdp = new MtUdp();
        public AdminLoginWindow adminLoginWindow = new AdminLoginWindow();

        private static Timer timer;
        private static readonly int m_LocalUdpPort = 10000;
        private bool InitOrWaitPassengerBtnClickFlag=false;
        private bool ExperienceBeginBtnClickFlag = false;
        private bool PF_InitOverFlag = false;
        private bool PF_BottomStatusFlag = false;
        private bool PF_EnableRunStatusFlag = false;
        private bool[] pf_IsCheckedFlag = new bool[4]{ false, false, false, false };
        public bool[] Pf_IsCheckedFlag { get => pf_IsCheckedFlag; set => pf_IsCheckedFlag = value; }
        private const byte LadderControllerMounter = 5;
        private const byte DoorMounter = 4;
        private CarDoorLockStatus[] m_CarDoorCurLockStatus = new CarDoorLockStatus[4] { CarDoorLockStatus.ACTIONING, CarDoorLockStatus.ACTIONING, CarDoorLockStatus.ACTIONING, CarDoorLockStatus.ACTIONING };
        private DoorCtrlBtnStatus[] m_DoorCtrlBtnStatus = new DoorCtrlBtnStatus[4] { DoorCtrlBtnStatus.SETIDLE, DoorCtrlBtnStatus.SETIDLE, DoorCtrlBtnStatus.SETIDLE, DoorCtrlBtnStatus.SETIDLE};
        private LadderStatus[] m_LadderCurStatus = new LadderStatus[5] { LadderStatus.MOVING, LadderStatus.MOVING, LadderStatus.MOVING, LadderStatus.MOVING, LadderStatus.MOVING };
        private LadderCtrlBtnStatus[] m_LadderCtrlBtnStatus = new LadderCtrlBtnStatus[5] { LadderCtrlBtnStatus.SETIDLE, LadderCtrlBtnStatus.SETIDLE, LadderCtrlBtnStatus.SETIDLE, LadderCtrlBtnStatus.SETIDLE, LadderCtrlBtnStatus.SETIDLE };
        private readonly string[] m_PfNetConnectDisplayContent = new string[3] { "连接", "断开","出错" };
        private readonly string[] m_CarDoorCtrlBtnContent = new string[2] { "解锁", "锁定" };
        private readonly string[] m_LadderStatusContent = new string[4] { "靠近", "远离","移动中","出错" };
        #endregion
        public MainWindow()
        {
            InitializeComponent();
            Title = "motus";//临时使用，为下面将已经启动的进程置前做准备
            bool ret;
            Mutex mutex = new System.Threading.Mutex(true, "MOTUS", out ret);
            
            if (!ret)
            {
                MessageBox.Show("已有一个程序实例运行");
                SetForegroundWindow(FindWindow(null,"MOTUS"));
                Environment.Exit(0);
            }
            Title = "MOTUS";
            //本地网络初始化
            m_ConsoleUdp.UdpInit(m_LocalUdpPort);

            //发送数据多媒体定时器
            // Create an AutoResetEvent to signal the timeout threshold in the
            // timer callback has been reached.
            var autoEvent = new AutoResetEvent(false);
            timer = new Timer(new TimerCallback(TimerTask), autoEvent, 0,10);
        }
        #region //定时器主线程
        private void TimerTask(object timerState)
        {
            this.Dispatcher.Invoke(
                new System.Action(
                    delegate
                    {
                        CarDoorsRelativeHandle();
                        CheckPfSelectedStatus();
                        for(byte i=0;i<LADDER_AMOUNT;i++)
                        {
                            m_LadderCurStatus[i] = GetLadderStatus(i, m_ConsoleUdp.m_DataFromPlc);
                        }
                        UpdateUiLadderStatus();
                        if(true==adminLoginWindow.GameStartUpBtnDisplayFlag)
                        {
                            BtnStartUpPF1Game.Visibility = Visibility.Visible;
                            BtnStartUpPF2Game.Visibility = Visibility.Visible;
                            BtnStartUpPF3Game.Visibility = Visibility.Visible;
                            BtnStartUpPF4Game.Visibility = Visibility.Visible;
                            adminLoginWindow.GameStartUpBtnDisplayFlag = false;
                        }
                        //如果按下初始化按钮
                        if ((InitOrWaitPassengerBtnClickFlag == true))         //(PF_InitOverFlag==false)&&
                        {
                            //如果不是所有梯子都远离，且平台也不在底位时，让梯子远离      判断为!=时，屏蔽时返回false;判断为==时，屏蔽时返回true;
                            if (((adminLoginWindow.LadderShieldCheckFlag[0] ? false : (LadderStatus.AWAY != GetLadderStatus(0, m_ConsoleUdp.m_DataFromPlc)))
                             || (adminLoginWindow.LadderShieldCheckFlag[1] ? false : (LadderStatus.AWAY != GetLadderStatus(1, m_ConsoleUdp.m_DataFromPlc)))
                             || (adminLoginWindow.LadderShieldCheckFlag[2] ? false : (LadderStatus.AWAY != GetLadderStatus(2, m_ConsoleUdp.m_DataFromPlc)))
                             || (adminLoginWindow.LadderShieldCheckFlag[3] ? false : (LadderStatus.AWAY != GetLadderStatus(3, m_ConsoleUdp.m_DataFromPlc)))
                             || (adminLoginWindow.LadderShieldCheckFlag[4] ? false : (LadderStatus.AWAY != GetLadderStatus(4, m_ConsoleUdp.m_DataFromPlc))))
                             && (PF_BottomStatusFlag == false)
                             )
                            {
                                for (byte i = 0; i < LadderControllerMounter; i++)
                                {
                                    SetLadderAway(i, m_ConsoleUdp.m_DataToPlc);
                                }
                                m_ConsoleUdp.SendDataToPlc(m_ConsoleUdp.m_DataToPlc, m_ConsoleUdp.m_DataToPlc.Length, m_ConsoleUdp.m_PlcIpEndpoint);
                            }
                            //如果梯子都远离成功，但平台不在底位
                            else if ((adminLoginWindow.LadderShieldCheckFlag[0] ? true : (LadderStatus.AWAY == GetLadderStatus(0, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[1] ? true : (LadderStatus.AWAY == GetLadderStatus(1, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[2] ? true : (LadderStatus.AWAY == GetLadderStatus(2, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[3] ? true : (LadderStatus.AWAY == GetLadderStatus(3, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[4] ? true : (LadderStatus.AWAY == GetLadderStatus(4, m_ConsoleUdp.m_DataFromPlc)))
                             && (PF_BottomStatusFlag == false))
                            {
                                //如果初始化未完成，且平台状态有不在中位时
                                if (((Pf_IsCheckedFlag[0] ? (m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus != (byte)(DOF_state.dof_neutral)) : false)
                                    || (Pf_IsCheckedFlag[1] ? (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus != (byte)(DOF_state.dof_neutral)) : false)
                                    || (Pf_IsCheckedFlag[2] ? (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus != (byte)(DOF_state.dof_neutral)) : false)
                                    || (Pf_IsCheckedFlag[3] ? (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus != (byte)(DOF_state.dof_neutral)) : false))
                                    && (PF_InitOverFlag == false)
                                    )
                                {
                                    for (int i = 0; i < MtUdp.DeviceAmount; i++)
                                    {
                                        if (true == Pf_IsCheckedFlag[i])
                                        {
                                            m_ConsoleUdp.DofUpToMedian(m_ConsoleUdp.m_RemoteIpEndpoint[i]);
                                        }

                                    }
                                }
                                //如果初始化未完成，且平台都在中位时
                                else if ((Pf_IsCheckedFlag[0] ? (m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus == (byte)(DOF_state.dof_neutral)) : true)
                                    && (Pf_IsCheckedFlag[1] ? (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus == (byte)(DOF_state.dof_neutral)) : true)
                                    && (Pf_IsCheckedFlag[2] ? (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus == (byte)(DOF_state.dof_neutral)) : true)
                                    && (Pf_IsCheckedFlag[3] ? (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus == (byte)(DOF_state.dof_neutral)) : true)
                                    && (PF_InitOverFlag == false)
                                    )
                                {
                                    PF_InitOverFlag = true;
                                }
                                //如果初始化完成，且平台都不在底位时,需要降到底位使乘客上座
                                else if (((Pf_IsCheckedFlag[0] ? (m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus != (byte)(DOF_state.dof_check_id)) : false)
                                    || (Pf_IsCheckedFlag[1] ? (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus != (byte)(DOF_state.dof_check_id)) : false)
                                    || (Pf_IsCheckedFlag[2] ? (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus != (byte)(DOF_state.dof_check_id)) : false)
                                    || (Pf_IsCheckedFlag[3] ? (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus != (byte)(DOF_state.dof_check_id)) : false))
                                    && (PF_InitOverFlag == true)
                                    )
                                {
                                    for (int i = 0; i < MtUdp.DeviceAmount; i++)
                                    {
                                        if (true == Pf_IsCheckedFlag[i])
                                        {
                                            m_ConsoleUdp.DofToBottom(m_ConsoleUdp.m_RemoteIpEndpoint[i]);
                                        }
                                    }
                                }
                                //如果初始化完成，且平台在底位时
                                else if ((Pf_IsCheckedFlag[0] ? (m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                                    && (Pf_IsCheckedFlag[1] ? (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                                    && (Pf_IsCheckedFlag[2] ? (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                                    && (Pf_IsCheckedFlag[3] ? (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                                    && (PF_InitOverFlag == true)
                                    )
                                {
                                    PF_BottomStatusFlag = true;
                                }

                            }
                            //如果平台在底位，但是梯子未靠近时，让梯子靠近
                            else if ((Pf_IsCheckedFlag[0] ? (m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                                && (Pf_IsCheckedFlag[1] ? (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                                && (Pf_IsCheckedFlag[2] ? (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                                && (Pf_IsCheckedFlag[3] ? (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                                && ((adminLoginWindow.LadderShieldCheckFlag[0] ? false : (LadderStatus.CLOSED != GetLadderStatus(0, m_ConsoleUdp.m_DataFromPlc)))
                                || (adminLoginWindow.LadderShieldCheckFlag[1] ? false : (LadderStatus.CLOSED != GetLadderStatus(1, m_ConsoleUdp.m_DataFromPlc)))
                                || (adminLoginWindow.LadderShieldCheckFlag[2] ? false : (LadderStatus.CLOSED != GetLadderStatus(2, m_ConsoleUdp.m_DataFromPlc)))
                                || (adminLoginWindow.LadderShieldCheckFlag[3] ? false : (LadderStatus.CLOSED != GetLadderStatus(3, m_ConsoleUdp.m_DataFromPlc)))
                                || (adminLoginWindow.LadderShieldCheckFlag[4] ? false : (LadderStatus.CLOSED != GetLadderStatus(4, m_ConsoleUdp.m_DataFromPlc))))
                                && (PF_BottomStatusFlag == true)
                                )
                            {
                                for (byte i = 0; i < LadderControllerMounter; i++)
                                {
                                    SetLadderClose(i, m_ConsoleUdp.m_DataToPlc);
                                }
                                m_ConsoleUdp.SendDataToPlc(m_ConsoleUdp.m_DataToPlc, m_ConsoleUdp.m_DataToPlc.Length, m_ConsoleUdp.m_PlcIpEndpoint);
                            }
                            //如果平台在底位，且梯子都靠近，表示平台可以上人，且可以进行后续 运动；
                            else if ((Pf_IsCheckedFlag[0] ? (m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                                && (Pf_IsCheckedFlag[1] ? (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                                && (Pf_IsCheckedFlag[2] ? (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                                && (Pf_IsCheckedFlag[3] ? (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                                && (adminLoginWindow.LadderShieldCheckFlag[0] ? true : (LadderStatus.CLOSED == GetLadderStatus(0, m_ConsoleUdp.m_DataFromPlc)))
                                && (adminLoginWindow.LadderShieldCheckFlag[1] ? true : (LadderStatus.CLOSED == GetLadderStatus(1, m_ConsoleUdp.m_DataFromPlc)))
                                && (adminLoginWindow.LadderShieldCheckFlag[2] ? true : (LadderStatus.CLOSED == GetLadderStatus(2, m_ConsoleUdp.m_DataFromPlc)))
                                && (adminLoginWindow.LadderShieldCheckFlag[3] ? true : (LadderStatus.CLOSED == GetLadderStatus(3, m_ConsoleUdp.m_DataFromPlc)))
                                && (adminLoginWindow.LadderShieldCheckFlag[4] ? true : (LadderStatus.CLOSED == GetLadderStatus(4, m_ConsoleUdp.m_DataFromPlc)))
                                && (PF_BottomStatusFlag == true)
                                )
                            {
                                //此状态可以上下客
                                for (byte i = 0; i < MtUdp.DeviceAmount; i++)
                                {
                                    if ((m_CarDoorCurLockStatus[i] != CarDoorLockStatus.UNLOCKED) && (CarDoorLockStatus.UNLOCKED == SetCarDoorOpened(i, m_ConsoleUdp.m_DataToPlc)))
                                    {
                                        m_CarDoorCurLockStatus[i] = CarDoorLockStatus.UNLOCKED;
                                        SetCarDoorBtnStatus(i, m_CarDoorCurLockStatus[i]);
                                    }
                                }
                                m_ConsoleUdp.SendDataToPlc(m_ConsoleUdp.m_DataToPlc, m_ConsoleUdp.m_DataToPlc.Length, m_ConsoleUdp.m_PlcIpEndpoint);
                                //激活运动按钮
                                if ((adminLoginWindow.CarDoorShieldCheckFlag[0] ? true : (CarDoorLockStatus.UNLOCKED == m_CarDoorCurLockStatus[0]))
                                && (adminLoginWindow.CarDoorShieldCheckFlag[1] ? true : (CarDoorLockStatus.UNLOCKED == m_CarDoorCurLockStatus[1]))
                                && (adminLoginWindow.CarDoorShieldCheckFlag[2] ? true : (CarDoorLockStatus.UNLOCKED == m_CarDoorCurLockStatus[2]))
                                && (adminLoginWindow.CarDoorShieldCheckFlag[3] ? true : (CarDoorLockStatus.UNLOCKED == m_CarDoorCurLockStatus[3]))
                                )
                                {
                                    InitOrWaitPassengerBtnClickFlag = false;
                                }
                            }
                        }
                        //如果按下体验开始按钮
                        else if (ExperienceBeginBtnClickFlag == true)
                        {
                            if (PF_EnableRunStatusFlag == true)
                            {
                                for (int i = 0; i < MtUdp.DeviceAmount; i++)
                                {
                                    if (true == Pf_IsCheckedFlag[i])
                                    {
                                        m_ConsoleUdp.DofToRun(m_ConsoleUdp.m_RemoteIpEndpoint[i]);
                                    }
                                }
                            }
                            //如果安全门在靠近状态
                            else if (((adminLoginWindow.LadderShieldCheckFlag[0] ? false : (LadderStatus.AWAY != GetLadderStatus(0, m_ConsoleUdp.m_DataFromPlc)))
                             || (adminLoginWindow.LadderShieldCheckFlag[1] ? false : (LadderStatus.AWAY != GetLadderStatus(1, m_ConsoleUdp.m_DataFromPlc)))
                             || (adminLoginWindow.LadderShieldCheckFlag[2] ? false : (LadderStatus.AWAY != GetLadderStatus(2, m_ConsoleUdp.m_DataFromPlc)))
                             || (adminLoginWindow.LadderShieldCheckFlag[3] ? false : (LadderStatus.AWAY != GetLadderStatus(3, m_ConsoleUdp.m_DataFromPlc)))
                             || (adminLoginWindow.LadderShieldCheckFlag[4] ? false : (LadderStatus.AWAY != GetLadderStatus(4, m_ConsoleUdp.m_DataFromPlc))))
                             && (Pf_IsCheckedFlag[0] ? (m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                            && (Pf_IsCheckedFlag[1] ? (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                            && (Pf_IsCheckedFlag[2] ? (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                            && (Pf_IsCheckedFlag[3] ? (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                             )
                            {
                                for (byte i = 0; i < LadderControllerMounter; i++)
                                {
                                    SetLadderAway(i, m_ConsoleUdp.m_DataToPlc);
                                }
                                m_ConsoleUdp.SendDataToPlc(m_ConsoleUdp.m_DataToPlc, m_ConsoleUdp.m_DataToPlc.Length, m_ConsoleUdp.m_PlcIpEndpoint);
                            }
                            //如果安全门都远离成功
                            else if ((adminLoginWindow.LadderShieldCheckFlag[0] ? true : (LadderStatus.AWAY == GetLadderStatus(0, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[1] ? true : (LadderStatus.AWAY == GetLadderStatus(1, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[2] ? true : (LadderStatus.AWAY == GetLadderStatus(2, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[3] ? true : (LadderStatus.AWAY == GetLadderStatus(3, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[4] ? true : (LadderStatus.AWAY == GetLadderStatus(4, m_ConsoleUdp.m_DataFromPlc)))
                             && (
                                    (Pf_IsCheckedFlag[0] ? (m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus != (byte)(DOF_state.dof_neutral)) : false)
                                    || (Pf_IsCheckedFlag[1] ? (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus != (byte)(DOF_state.dof_neutral)) : false)
                                    || (Pf_IsCheckedFlag[2] ? (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus != (byte)(DOF_state.dof_neutral)) : false)
                                    || (Pf_IsCheckedFlag[3] ? (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus != (byte)(DOF_state.dof_neutral)) : false)
                                 )
                            && (PF_EnableRunStatusFlag == false)
                             )
                            {
                                for (int i = 0; i < MtUdp.DeviceAmount; i++)
                                {
                                    if (true == Pf_IsCheckedFlag[i])
                                    {
                                        m_ConsoleUdp.DofUpToMedian(m_ConsoleUdp.m_RemoteIpEndpoint[i]);
                                    }

                                }

                            }
                            else if ((adminLoginWindow.LadderShieldCheckFlag[0] ? true : (LadderStatus.AWAY == GetLadderStatus(0, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[1] ? true : (LadderStatus.AWAY == GetLadderStatus(1, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[2] ? true : (LadderStatus.AWAY == GetLadderStatus(2, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[3] ? true : (LadderStatus.AWAY == GetLadderStatus(3, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[4] ? true : (LadderStatus.AWAY == GetLadderStatus(4, m_ConsoleUdp.m_DataFromPlc)))
                             && (Pf_IsCheckedFlag[0] ? (m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus == (byte)(DOF_state.dof_neutral)) : true)
                            && (Pf_IsCheckedFlag[0] ? (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus == (byte)(DOF_state.dof_neutral)) : true)
                            && (Pf_IsCheckedFlag[0] ? (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus == (byte)(DOF_state.dof_neutral)) : true)
                            && (Pf_IsCheckedFlag[0] ? (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus == (byte)(DOF_state.dof_neutral)) : true)
                            && (PF_EnableRunStatusFlag == false)
                             )
                            {
                                for (int i = 0; i < MtUdp.DeviceAmount; i++)
                                {
                                    if (true == Pf_IsCheckedFlag[i])
                                    {
                                        m_ConsoleUdp.DofUpToMedian(m_ConsoleUdp.m_RemoteIpEndpoint[i]);
                                    }
                                }
                                PF_EnableRunStatusFlag = true;
                            }

                        }
                        PlcDataHandler();
                        CarDoorsBtnHandler();
                        LadderBtnHandler();
                        //PlatformStatusIndicator();
                        //PlatformOperatorBtnHandler();
                    }
                    )
            );
        } 
        #endregion
        #region //核查平台勾选状态
        void CheckPfSelectedStatus()
        {
            //1号平台
            if(true==CbNum0Platform.IsChecked)
            {
                Pf_IsCheckedFlag[0] = true;
            }
            else
            {
                Pf_IsCheckedFlag[0] = false;
            }

            if (true == CbNum1Platform.IsChecked)
            {
                Pf_IsCheckedFlag[1] = true;
            }
            else
            {
                Pf_IsCheckedFlag[1] = false;
            }

            if (true == CbNum2Platform.IsChecked)
            {
                Pf_IsCheckedFlag[2] = true;
            }
            else
            {
                Pf_IsCheckedFlag[2] = false;
            }

            if (true == CbNum3Platform.IsChecked)
            {
                Pf_IsCheckedFlag[3] = true;
            }
            else
            {
                Pf_IsCheckedFlag[3] = false;
            }
        }
        #endregion
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
        #region //指示平台网络连接状态
        private void PlatformNetStatusIndicator()
        {
            for (int i = 0; i < MtUdp.DeviceAmount; i++)
            {
                if (true == m_ConsoleUdp.m_DeviceConnectState[i])
                {
                    switch (i)
                    {
                        case 0:
                            CbNum0Platform.IsEnabled = true;
                            CbNum0Platform.Background = Brushes.White;
                            PF0State.Content = m_PfNetConnectDisplayContent[0];
                            break;
                        case 1:
                            CbNum1Platform.IsEnabled = true;
                            CbNum1Platform.Background = Brushes.White;
                            PF1State.Content = m_PfNetConnectDisplayContent[0];
                            break;
                        case 2:
                            CbNum2Platform.IsEnabled = true;
                            CbNum2Platform.Background = Brushes.White;
                            PF2State.Content = m_PfNetConnectDisplayContent[0];
                            break;
                        case 3:
                            CbNum3Platform.IsEnabled = true;
                            CbNum3Platform.Background = Brushes.White;
                            PF3State.Content = m_PfNetConnectDisplayContent[0];
                            break;
                    }

                }
                else if (false == m_ConsoleUdp.m_DeviceConnectState[i])
                {
                    switch (i)
                    {
                        case 0:
                            CbNum0Platform.IsEnabled = false;
                            CbNum0Platform.Background = Brushes.Red;
                            PF0State.Content = m_PfNetConnectDisplayContent[1];
                            break;
                        case 1:
                            CbNum1Platform.IsEnabled = false;
                            CbNum1Platform.Background = Brushes.Red;
                            PF1State.Content = m_PfNetConnectDisplayContent[1];
                            break;
                        case 2:
                            CbNum2Platform.IsEnabled = false;
                            CbNum2Platform.Background = Brushes.Red;
                            PF2State.Content = m_PfNetConnectDisplayContent[1];
                            break;
                        case 3:
                            CbNum3Platform.IsEnabled = false;
                            CbNum3Platform.Background = Brushes.Red;
                            PF3State.Content = m_PfNetConnectDisplayContent[1];
                            break;
                    }
                }
                else if(119==m_ConsoleUdp.m_sToHostBuf[i].nDOFStatus)
                {
                    switch (i)
                    {
                        case 0:
                            CbNum0Platform.IsEnabled = false;
                            CbNum0Platform.Background = Brushes.Red;
                            PF0State.Content = m_PfNetConnectDisplayContent[2];
                            break;
                        case 1:
                            CbNum1Platform.IsEnabled = false;
                            CbNum1Platform.Background = Brushes.Red;
                            PF1State.Content = m_PfNetConnectDisplayContent[2];
                            break;
                        case 2:
                            CbNum2Platform.IsEnabled = false;
                            CbNum2Platform.Background = Brushes.Red;
                            PF2State.Content = m_PfNetConnectDisplayContent[2];
                            break;
                        case 3:
                            CbNum3Platform.IsEnabled = false;
                            CbNum3Platform.Background = Brushes.Red;
                            PF3State.Content = m_PfNetConnectDisplayContent[2];
                            break;
                    }
                }
            }
        } 
        #endregion

        #region //设置车门控制按钮状态
        private void SetCarDoorBtnStatus(byte index,CarDoorLockStatus carDoorLockStatus)
        {
            switch(index)
            {
                case 0:
                    if(CarDoorLockStatus.LOCKED== carDoorLockStatus)
                    {
                        BtnNum1CarDoorControl.Content = m_CarDoorCtrlBtnContent[0];
                    }
                    else
                    {
                        BtnNum1CarDoorControl.Content = m_CarDoorCtrlBtnContent[1];
                    }
                    break;
                case 1:
                    if (CarDoorLockStatus.LOCKED == carDoorLockStatus)
                    {
                        BtnNum2CarDoorControl.Content = m_CarDoorCtrlBtnContent[0];
                    }
                    else
                    {
                        BtnNum2CarDoorControl.Content = m_CarDoorCtrlBtnContent[1];
                    }
                    break;
                case 2:
                    if (CarDoorLockStatus.LOCKED == carDoorLockStatus)
                    {
                        BtnNum3CarDoorControl.Content = m_CarDoorCtrlBtnContent[0];
                    }
                    else
                    {
                        BtnNum3CarDoorControl.Content = m_CarDoorCtrlBtnContent[1];
                    }
                    break;
                case 3:
                    if (CarDoorLockStatus.LOCKED == carDoorLockStatus)
                    {
                        BtnNum4CarDoorControl.Content = m_CarDoorCtrlBtnContent[0];
                    }
                    else
                    {
                        BtnNum4CarDoorControl.Content = m_CarDoorCtrlBtnContent[1];
                    }
                    break;

            }
        }
        #endregion
        #region //车门控制按钮处理函数
        private void CarDoorsBtnHandler()
        {
            if((DoorCtrlBtnStatus.SETIDLE!= m_DoorCtrlBtnStatus[0])
                || (DoorCtrlBtnStatus.SETIDLE != m_DoorCtrlBtnStatus[1])
                || (DoorCtrlBtnStatus.SETIDLE != m_DoorCtrlBtnStatus[2])
                || (DoorCtrlBtnStatus.SETIDLE != m_DoorCtrlBtnStatus[3])
                )
            {
                for (byte i = 0; i < MtUdp.DeviceAmount; i++)
                {
                    if (DoorCtrlBtnStatus.SETLOCKED == m_DoorCtrlBtnStatus[i])
                    {
                        if ((m_CarDoorCurLockStatus[i] != CarDoorLockStatus.LOCKED) && (CarDoorLockStatus.LOCKED == SetCarDoorClosed(i, m_ConsoleUdp.m_DataToPlc)))
                        {
                            m_CarDoorCurLockStatus[i] = CarDoorLockStatus.LOCKED;
                            SetCarDoorBtnStatus(i, m_CarDoorCurLockStatus[i]);
                            m_DoorCtrlBtnStatus[i] = DoorCtrlBtnStatus.SETIDLE;
                        }

                    }
                    else if (DoorCtrlBtnStatus.SETUNLOCKED == m_DoorCtrlBtnStatus[i])
                    {
                        if ((m_CarDoorCurLockStatus[i] != CarDoorLockStatus.UNLOCKED) && (CarDoorLockStatus.UNLOCKED == SetCarDoorOpened(i, m_ConsoleUdp.m_DataToPlc)))
                        {
                            m_CarDoorCurLockStatus[i] = CarDoorLockStatus.UNLOCKED;
                            SetCarDoorBtnStatus(i, m_CarDoorCurLockStatus[i]);
                            m_DoorCtrlBtnStatus[i] = DoorCtrlBtnStatus.SETIDLE;
                        }
                    }
                }
                m_ConsoleUdp.SendDataToPlc(m_ConsoleUdp.m_DataToPlc, m_ConsoleUdp.m_DataToPlc.Length, m_ConsoleUdp.m_PlcIpEndpoint);
            }
            
        }
        #endregion
        #region //车门状态相关处理函数
        private void CarDoorsRelativeHandle()
        {
            if((CarDoorStatus.CLOSED== GetCarDoorStatus(0,m_ConsoleUdp.m_DataFromPlc))
                && (CarDoorStatus.CLOSED == GetCarDoorStatus(1, m_ConsoleUdp.m_DataFromPlc))
                && (CarDoorStatus.CLOSED == GetCarDoorStatus(2, m_ConsoleUdp.m_DataFromPlc))
                && (CarDoorStatus.CLOSED == GetCarDoorStatus(3, m_ConsoleUdp.m_DataFromPlc))
                )
            {
                InitOrWaitPassenger.IsEnabled = true;
                //ExperienceBegin.IsEnabled = true;
            }
            else
            {
                InitOrWaitPassenger.IsEnabled = false;
                ExperienceBegin.IsEnabled = false;
            }

        }
        #endregion
        #region //车门状态获取
        private CarDoorStatus GetCarDoorStatus(byte index,byte[] allStatus)
        {
            switch(index)
            {
                case 0:
                    if((1==((allStatus[2]>>0)&0x01))||(true==adminLoginWindow.CarDoorShieldCheckFlag[0]))
                    {
                        Car1DoorState.Content = m_CarDoorCtrlBtnContent[1];
                        return CarDoorStatus.CLOSED;
                    }
                    else
                    {
                        Car1DoorState.Content = m_CarDoorCtrlBtnContent[0];
                        return CarDoorStatus.OPENED;
                    }
                case 1:
                    if ((1 == ((allStatus[2] >> 1) & 0x01))|| (true == adminLoginWindow.CarDoorShieldCheckFlag[1]))
                    {
                        Car2DoorState.Content = m_CarDoorCtrlBtnContent[1];
                        return CarDoorStatus.CLOSED;
                    }
                    else
                    {
                        Car2DoorState.Content = m_CarDoorCtrlBtnContent[0];
                        return CarDoorStatus.OPENED;
                    }
                case 2:
                    if ((1 == ((allStatus[2] >> 2) & 0x01))|| (true == adminLoginWindow.CarDoorShieldCheckFlag[2]))
                    {
                        Car3DoorState.Content = m_CarDoorCtrlBtnContent[1];
                        return CarDoorStatus.CLOSED;
                    }
                    else
                    {
                        Car3DoorState.Content = m_CarDoorCtrlBtnContent[0];
                        return CarDoorStatus.OPENED;
                    }
                case 3:
                    if ((1 == ((allStatus[2] >> 3) & 0x01))|| (true == adminLoginWindow.CarDoorShieldCheckFlag[3]))
                    {
                        Car4DoorState.Content = m_CarDoorCtrlBtnContent[1];
                        return CarDoorStatus.CLOSED;
                    }
                    else
                    {
                        Car4DoorState.Content = m_CarDoorCtrlBtnContent[0];
                        return CarDoorStatus.OPENED;
                    }
            }
            return CarDoorStatus.CLOSED;
        }
        #endregion
        #region //车门锁定
        const UInt32 DoorCloseDelayMaxCounter = 50;
        UInt32[] DoorCloseDelayCounter = new UInt32[4];
        private CarDoorLockStatus SetCarDoorClosed(byte index,byte[] status)
        {
            switch(index)
            {
                case 0:
                    if (DoorCloseDelayCounter[index] <= DoorCloseDelayMaxCounter)
                    {
                        status[2] |= 0x01 << 0;
                        DoorCloseDelayCounter[index]++;
                        return CarDoorLockStatus.ACTIONING;
                    }
                    else
                    {
                        status[2] &= (0x01 << 0)^0xFF;
                        DoorCloseDelayCounter[index] = 0;
                        return CarDoorLockStatus.LOCKED;

                    }
                case 1:
                    if (DoorCloseDelayCounter[index] <= DoorCloseDelayMaxCounter)
                    {
                        status[2] |= 0x01 << 1;
                        DoorCloseDelayCounter[index]++;
                        return CarDoorLockStatus.ACTIONING;
                    }
                    else
                    {
                        status[2] &= (0x01 << 1)^0xFF;
                        DoorCloseDelayCounter[index] = 0;
                        return CarDoorLockStatus.LOCKED;
                    }
                case 2:
                    if (DoorCloseDelayCounter[index] <= DoorCloseDelayMaxCounter)
                    {
                        status[2] |= 0x01 << 2;
                        DoorCloseDelayCounter[index]++;
                        return CarDoorLockStatus.ACTIONING;
                    }
                    else
                    {
                        status[2] &= (0x01 << 2)^0xFF;
                        DoorCloseDelayCounter[index] = 0;
                        return CarDoorLockStatus.LOCKED;
                    }
                case 3:
                    if (DoorCloseDelayCounter[index] <= DoorCloseDelayMaxCounter)
                    {
                        status[2] |= 0x01 << 3;
                        DoorCloseDelayCounter[index]++;
                        return CarDoorLockStatus.ACTIONING;
                    }
                    else
                    {
                        status[2] &= (0x01 << 3)^0xFF;
                        DoorCloseDelayCounter[index] = 0;
                        return CarDoorLockStatus.LOCKED;
                    }
            }
            return CarDoorLockStatus.ACTIONING;
        }
        #endregion
        #region //车门打开
        const UInt32 DoorOpenDelayMaxCounter = 50;
        UInt32[] DoorOpenDelayCounter = new UInt32[4];
        private CarDoorLockStatus SetCarDoorOpened(byte index, byte[] status)
        {
            switch (index)
            {
                case 0:
                    if (DoorOpenDelayCounter[index] <= DoorOpenDelayMaxCounter)
                    {
                        status[2] |= 0x01 << 0;
                        DoorOpenDelayCounter[index]++;
                        return CarDoorLockStatus.ACTIONING;
                    }
                    else
                    {
                        status[2] &= (0x01 << 0) ^ 0xFF;
                        DoorOpenDelayCounter[index] = 0;
                        return CarDoorLockStatus.UNLOCKED;
                    }
                case 1:
                    if (DoorOpenDelayCounter[index] <= DoorOpenDelayMaxCounter)
                    {
                        DoorOpenDelayCounter[index]++;
                        status[2] |= 0x01 << 1;
                        return CarDoorLockStatus.ACTIONING;
                    }
                    else
                    {
                        status[2] &= (0x01 << 1) ^ 0xFF;
                        DoorOpenDelayCounter[index] = 0;
                        return CarDoorLockStatus.UNLOCKED;
                    }
                case 2:
                    if (DoorOpenDelayCounter[index] <= DoorOpenDelayMaxCounter)
                    {
                        DoorOpenDelayCounter[index]++;
                        status[2] |= 0x01 << 2;
                        return CarDoorLockStatus.ACTIONING;
                    }
                    else
                    {
                        status[2] &= (0x01 << 2) ^ 0xFF;
                        DoorOpenDelayCounter[index] = 0;
                        return CarDoorLockStatus.UNLOCKED;
                    }
                case 3:
                    if (DoorOpenDelayCounter[index] <= DoorOpenDelayMaxCounter)
                    {
                        DoorOpenDelayCounter[index]++;
                        status[2] |= 0x01 << 3;
                        return CarDoorLockStatus.ACTIONING;
                    }
                    else
                    {
                        status[2] &= (0x01 << 3) ^ 0xFF;
                        DoorOpenDelayCounter[index] = 0;
                        return CarDoorLockStatus.UNLOCKED;
                    }
            }
            return CarDoorLockStatus.ACTIONING;
        }
        #endregion

        #region //更新UI中梯子状态
        private void UpdateUiLadderStatus()
        {
            for(byte i=0;i<LadderControllerMounter;i++)
            {
                switch(i)
                {
                    case 0:
                        switch(GetLadderStatus(i, m_ConsoleUdp.m_DataFromPlc))
                        {
                            case LadderStatus.CLOSED:
                                Ladder1StatusDisplay.Content = m_LadderStatusContent[0];
                                ClearLadderClosedDelayCounter(i);
                                ClearLadderAwayDelayCounter(i);
                                Ladder1StatusDisplay.Background = Brushes.White;
                                break;
                            case LadderStatus.AWAY:
                                Ladder1StatusDisplay.Content = m_LadderStatusContent[1];
                                ClearLadderClosedDelayCounter(i);
                                ClearLadderAwayDelayCounter(i);
                                Ladder1StatusDisplay.Background = Brushes.White;
                                break;
                            case LadderStatus.MOVING:
                                Ladder1StatusDisplay.Content = m_LadderStatusContent[2];
                                Ladder1StatusDisplay.Background = Brushes.Green;
                                break;
                            case LadderStatus.ERROR:
                                Ladder1StatusDisplay.Content = m_LadderStatusContent[3];
                                Ladder1StatusDisplay.Background = Brushes.Red;
                                break;
                        }
                        break;
                    case 1:
                        switch (GetLadderStatus(i, m_ConsoleUdp.m_DataFromPlc))
                        {
                            case LadderStatus.CLOSED:
                                Ladder2StatusDisplay.Content = m_LadderStatusContent[0];
                                ClearLadderClosedDelayCounter(i);
                                ClearLadderAwayDelayCounter(i);
                                Ladder2StatusDisplay.Background = Brushes.White;
                                break;
                            case LadderStatus.AWAY:
                                Ladder2StatusDisplay.Content = m_LadderStatusContent[1];
                                ClearLadderClosedDelayCounter(i);
                                ClearLadderAwayDelayCounter(i);
                                Ladder2StatusDisplay.Background = Brushes.White;
                                break;
                            case LadderStatus.MOVING:
                                Ladder2StatusDisplay.Content = m_LadderStatusContent[2];
                                Ladder2StatusDisplay.Background = Brushes.Green;
                                break;
                            case LadderStatus.ERROR:
                                Ladder2StatusDisplay.Content = m_LadderStatusContent[3];
                                Ladder2StatusDisplay.Background = Brushes.Red;
                                break;
                        }
                        break;
                    case 2:
                        switch (GetLadderStatus(i, m_ConsoleUdp.m_DataFromPlc))
                        {
                            case LadderStatus.CLOSED:
                                Ladder3StatusDisplay.Content = m_LadderStatusContent[0];
                                Ladder3StatusDisplay.Background = Brushes.White;
                                ClearLadderClosedDelayCounter(i);
                                ClearLadderAwayDelayCounter(i);
                                break;
                            case LadderStatus.AWAY:
                                Ladder3StatusDisplay.Content = m_LadderStatusContent[1];
                                Ladder3StatusDisplay.Background = Brushes.White;
                                ClearLadderClosedDelayCounter(i);
                                ClearLadderAwayDelayCounter(i);
                                break;
                            case LadderStatus.MOVING:
                                Ladder3StatusDisplay.Content = m_LadderStatusContent[2];
                                Ladder3StatusDisplay.Background = Brushes.Green;
                                break;
                            case LadderStatus.ERROR:
                                Ladder3StatusDisplay.Content = m_LadderStatusContent[3];
                                Ladder3StatusDisplay.Background = Brushes.Red;
                                break;
                        }
                        break;
                    case 3:
                        switch (GetLadderStatus(i, m_ConsoleUdp.m_DataFromPlc))
                        {
                            case LadderStatus.CLOSED:
                                Ladder4StatusDisplay.Content = m_LadderStatusContent[0];
                                Ladder4StatusDisplay.Background = Brushes.White;
                                ClearLadderClosedDelayCounter(i);
                                ClearLadderAwayDelayCounter(i);
                                break;
                            case LadderStatus.AWAY:
                                Ladder4StatusDisplay.Content = m_LadderStatusContent[1];
                                Ladder4StatusDisplay.Background = Brushes.White;
                                ClearLadderClosedDelayCounter(i);
                                ClearLadderAwayDelayCounter(i);
                                break;
                            case LadderStatus.MOVING:
                                Ladder4StatusDisplay.Content = m_LadderStatusContent[2];
                                Ladder4StatusDisplay.Background = Brushes.Green;
                                break;
                            case LadderStatus.ERROR:
                                Ladder4StatusDisplay.Content = m_LadderStatusContent[3];
                                Ladder4StatusDisplay.Background = Brushes.Red;
                                break;
                        }
                        break;
                    case 4:
                        switch (GetLadderStatus(i, m_ConsoleUdp.m_DataFromPlc))
                        {
                            case LadderStatus.CLOSED:
                                Ladder5StatusDisplay.Content = m_LadderStatusContent[0];
                                Ladder5StatusDisplay.Background = Brushes.White;
                                ClearLadderClosedDelayCounter(i);
                                ClearLadderAwayDelayCounter(i);
                                break;
                            case LadderStatus.AWAY:
                                Ladder5StatusDisplay.Content = m_LadderStatusContent[1];
                                Ladder5StatusDisplay.Background = Brushes.White;
                                ClearLadderClosedDelayCounter(i);
                                ClearLadderAwayDelayCounter(i);
                                break;
                            case LadderStatus.MOVING:
                                Ladder5StatusDisplay.Content = m_LadderStatusContent[2];
                                Ladder5StatusDisplay.Background = Brushes.Green;
                                break;
                            case LadderStatus.ERROR:
                                Ladder5StatusDisplay.Content = m_LadderStatusContent[3];
                                Ladder5StatusDisplay.Background = Brushes.Red;
                                break;
                        }
                        break;

                }
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
                    else if((1 != ((allStatus[0] >> 4) & 0x01)) && (1 != ((allStatus[0] >> 5) & 0x01))
                        && (LadderAwayDelayCounter[index]<=LadderAwayMaxDelayCounter)
                        && (LadderClosedDelayCounter[index] <= LadderClosedMaxDelayCounter)
                        )
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
                    else if ((1 != ((allStatus[0] >> 6) & 0x01)) && (1 != ((allStatus[0] >> 7) & 0x01))
                        && (LadderAwayDelayCounter[index] <= LadderAwayMaxDelayCounter)
                        && (LadderClosedDelayCounter[index] <= LadderClosedMaxDelayCounter)
                        )
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
                    else if ((1 != ((allStatus[1] >> 0) & 0x01)) && (1 != ((allStatus[1] >> 1) & 0x01))
                        && (LadderAwayDelayCounter[index] <= LadderAwayMaxDelayCounter)
                        && (LadderClosedDelayCounter[index] <= LadderClosedMaxDelayCounter)
                        )
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
                    else if ((1 != ((allStatus[1] >> 2) & 0x01)) && (1 != ((allStatus[1] >> 3) & 0x01))
                        && (LadderAwayDelayCounter[index] <= LadderAwayMaxDelayCounter)
                        && (LadderClosedDelayCounter[index] <= LadderClosedMaxDelayCounter)
                        )
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
                    else if ((1 != ((allStatus[1] >> 4) & 0x01)) && (1 != ((allStatus[1] >> 5) & 0x01))
                        && (LadderAwayDelayCounter[index] <= LadderAwayMaxDelayCounter)
                        && (LadderClosedDelayCounter[index] <= LadderClosedMaxDelayCounter)
                        )
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
        #region //楼梯控制按钮处理函数
        private void LadderBtnHandler()
        {
            if ((LadderCtrlBtnStatus.SETIDLE != m_LadderCtrlBtnStatus[0])
                || (LadderCtrlBtnStatus.SETIDLE != m_LadderCtrlBtnStatus[1])
                || (LadderCtrlBtnStatus.SETIDLE != m_LadderCtrlBtnStatus[2])
                || (LadderCtrlBtnStatus.SETIDLE != m_LadderCtrlBtnStatus[3])
                || (LadderCtrlBtnStatus.SETIDLE != m_LadderCtrlBtnStatus[4])
                )
            {
                for (byte i = 0; i < LADDER_AMOUNT; i++)
                {
                    if (LadderCtrlBtnStatus.SETCLOSE == m_LadderCtrlBtnStatus[i])
                    {
                        if (m_LadderCurStatus[i] != LadderStatus.CLOSED)
                        {
                            SetLadderClose(i, m_ConsoleUdp.m_DataToPlc);
                        }
                        else
                        {
                            ClearLadderClosedDelayCounter(i);
                            m_LadderCtrlBtnStatus[i] = LadderCtrlBtnStatus.SETIDLE;
                        }
                    }
                    else if (LadderCtrlBtnStatus.SETAWAY == m_LadderCtrlBtnStatus[i])
                    {
                        if (m_LadderCurStatus[i] != LadderStatus.AWAY)
                        {
                            SetLadderAway(i, m_ConsoleUdp.m_DataToPlc);
                        }
                        else
                        {
                            ClearLadderAwayDelayCounter(i);
                            m_LadderCtrlBtnStatus[i] = LadderCtrlBtnStatus.SETIDLE;
                        }
                    }
                }
                m_ConsoleUdp.SendDataToPlc(m_ConsoleUdp.m_DataToPlc, m_ConsoleUdp.m_DataToPlc.Length, m_ConsoleUdp.m_PlcIpEndpoint);
            }
        }
        #endregion
        #region //楼梯远离平台
        private const UInt32 LadderAwayMaxDelayCounter = 1000;
        private UInt32[] LadderAwayDelayCounter = new UInt32[5] { 0,0,0,0,0};
        private void ClearLadderAwayDelayCounter(byte index)
        {
            LadderAwayDelayCounter[index] = 0;
        }
        private void SetLadderAway(byte index, byte[] status)
        {
            switch (index)
            {
                case 0:
                    //status[0] |= 0x01 << 5;
                    status[0] &= (0x01 << 5) ^ 0xff;
                    if (LadderAwayDelayCounter[index] > LadderAwayMaxDelayCounter)
                    {
                        //设备滑梯状态为出错
                        MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        m_LadderCtrlBtnStatus[index] = LadderCtrlBtnStatus.SETIDLE;
                    }
                    else
                    {
                        LadderAwayDelayCounter[index]++;
                    }
                    break;
                case 1:
                    //status[0] |= 0x01 << 6;
                    status[0] &= (0x01 << 6) ^ 0xff;
                    if (LadderAwayDelayCounter[index] > LadderAwayMaxDelayCounter)
                    {
                        //设备滑梯状态为出错
                        MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        m_LadderCtrlBtnStatus[index] = LadderCtrlBtnStatus.SETIDLE;
                    }
                    else
                    {
                        LadderAwayDelayCounter[index]++;
                    }
                    break;
                case 2:
                    //status[0] |= 0x01 << 7;
                    status[0] &= (0x01 << 7) ^ 0xff;
                    if (LadderAwayDelayCounter[index] > LadderAwayMaxDelayCounter)
                    {
                        //设备滑梯状态为出错
                        MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        m_LadderCtrlBtnStatus[index] = LadderCtrlBtnStatus.SETIDLE;
                    }
                    else
                    {
                        LadderAwayDelayCounter[index]++;
                    }
                    break;
                case 3:
                    //status[1] |= 0x01 << 0;
                    status[1] &= (0x01 << 0) ^ 0xff;
                    if (LadderAwayDelayCounter[index] > LadderAwayMaxDelayCounter)
                    {
                        //设备滑梯状态为出错
                        MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        m_LadderCtrlBtnStatus[index] = LadderCtrlBtnStatus.SETIDLE;
                    }
                    else
                    {
                        LadderAwayDelayCounter[index]++;
                    }
                    break;
                case 4:
                    //status[1] |= 0x01 << 1;
                    status[1] &= (0x01 << 1) ^ 0xff;
                    if (LadderAwayDelayCounter[index] > LadderAwayMaxDelayCounter)
                    {
                        //设备滑梯状态为出错
                        MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        m_LadderCtrlBtnStatus[index] = LadderCtrlBtnStatus.SETIDLE;
                    }
                    else
                    {
                        LadderAwayDelayCounter[index]++;
                    }
                    break;
            }
        }
        #endregion
        #region //出错弹窗
        void MotusMessageBox(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            timer.Change(Timeout.Infinite, 10);
            //MessageBox.Show(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            MessageBox.Show(messageBoxText, caption, button, icon);
            timer.Change(0, 10);
        }
        #endregion
        #region //楼梯靠近平台
        const UInt32 LadderClosedMaxDelayCounter = 1000;
        UInt32[] LadderClosedDelayCounter = new UInt32[5] { 0,0,0,0,0};
        private void ClearLadderClosedDelayCounter(byte index)
        {
            LadderClosedDelayCounter[index] = 0;
        }
        private void SetLadderClose(byte index, byte[] status)
        {
            switch (index)
            {
                case 0:
                    //status[0] &= (0x01 << 5)^0xff;
                    status[0] |= 0x01 << 5;
                    if (LadderClosedDelayCounter[index] >= LadderClosedMaxDelayCounter)
                    {
                        MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        m_LadderCtrlBtnStatus[index] = LadderCtrlBtnStatus.SETIDLE;
                    }
                    else
                    {
                        LadderClosedDelayCounter[index]++;
                    }
                    
                    break;
                case 1:
                    //status[0] &= (0x01 << 6) ^ 0xff;
                    status[0] |= 0x01 << 6;
                    if (LadderClosedDelayCounter[index] >= LadderClosedMaxDelayCounter)
                    {
                        MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        m_LadderCtrlBtnStatus[index] = LadderCtrlBtnStatus.SETIDLE;
                    }
                    else
                    {
                        LadderClosedDelayCounter[index]++;
                    }
                    break;
                case 2:
                    //status[0] &= (0x01 << 7) ^ 0xff;
                    status[0] |= 0x01 << 7;
                    if (LadderClosedDelayCounter[index] >= LadderClosedMaxDelayCounter)
                    {
                        MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        m_LadderCtrlBtnStatus[index] = LadderCtrlBtnStatus.SETIDLE;
                    }
                    else
                    {
                        LadderClosedDelayCounter[index]++;
                    }
                    break;
                case 3:
                    //status[1] &= (0x01 << 0) ^ 0xff;
                    status[1] |= 0x01 << 0;
                    if (LadderClosedDelayCounter[index] >= LadderClosedMaxDelayCounter)
                    {
                        MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        m_LadderCtrlBtnStatus[index] = LadderCtrlBtnStatus.SETIDLE;
                    }
                    else
                    {
                        LadderClosedDelayCounter[index]++;
                    }
                    break;
                case 4:
                    //status[1] &= (0x01 << 1) ^ 0xff;
                    status[1] |= 0x01 << 1;
                    if (LadderClosedDelayCounter[index] >= LadderClosedMaxDelayCounter)
                    {
                        MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        m_LadderCtrlBtnStatus[index] = LadderCtrlBtnStatus.SETIDLE;
                    }
                    else
                    {
                        LadderClosedDelayCounter[index]++;
                    }
                    break;
            }
        }
        #endregion

        #region //初始化按钮事件
        private void InitOrWaitPassenger_Click(object sender, RoutedEventArgs e)
        {
            InitOrWaitPassengerBtnClickFlag = true;
            ExperienceBeginBtnClickFlag = false;
        }
        #endregion
        #region //体验开始按钮事件
        private void ExperienceBegin_Click(object sender, RoutedEventArgs e)
        {
            InitOrWaitPassengerBtnClickFlag = false;
            ExperienceBeginBtnClickFlag = true;
        }
        #endregion
        #region //管理员模式按钮事件
        private void AdminMode_Click(object sender, RoutedEventArgs e)
        {
            adminLoginWindow.ShowDialog();
        }
        #endregion
        #region //车门控制按钮事件
        #region //1号车门控制按钮事件
        private void BtnNum1CarDoorControl_Click(object sender, RoutedEventArgs e)
        {
            if (BtnNum1CarDoorControl.Content.Equals(m_CarDoorCtrlBtnContent[0]))
            {
                m_DoorCtrlBtnStatus[0] = DoorCtrlBtnStatus.SETUNLOCKED;
                BtnNum1CarDoorControl.Content = m_CarDoorCtrlBtnContent[1];
            }
            else
            {
                m_DoorCtrlBtnStatus[0] = DoorCtrlBtnStatus.SETLOCKED;
                BtnNum1CarDoorControl.Content = m_CarDoorCtrlBtnContent[0];
            }
        }
        #endregion

        #region //2号车门控制按钮事件
        private void BtnNum2CarDoorControl_Click(object sender, RoutedEventArgs e)
        {
            if (BtnNum2CarDoorControl.Content.Equals(m_CarDoorCtrlBtnContent[0]))
            {
                m_DoorCtrlBtnStatus[1] = DoorCtrlBtnStatus.SETUNLOCKED;
                BtnNum2CarDoorControl.Content = m_CarDoorCtrlBtnContent[1];
            }
            else
            {
                m_DoorCtrlBtnStatus[1] = DoorCtrlBtnStatus.SETLOCKED;
                BtnNum2CarDoorControl.Content = m_CarDoorCtrlBtnContent[0];
            }
        }
        #endregion

        #region //3号车门控制按钮事件
        private void BtnNum3CarDoorControl_Click(object sender, RoutedEventArgs e)
        {
            if (BtnNum3CarDoorControl.Content.Equals(m_CarDoorCtrlBtnContent[0]))
            {
                m_DoorCtrlBtnStatus[2] = DoorCtrlBtnStatus.SETUNLOCKED;
                BtnNum3CarDoorControl.Content = m_CarDoorCtrlBtnContent[1];
            }
            else
            {
                m_DoorCtrlBtnStatus[2] = DoorCtrlBtnStatus.SETLOCKED;
                BtnNum3CarDoorControl.Content = m_CarDoorCtrlBtnContent[0];
            }
        }
        #endregion

        #region //4号车门控制按钮事件
        private void BtnNum4CarDoorControl_Click(object sender, RoutedEventArgs e)
        {
            if (BtnNum4CarDoorControl.Content.Equals(m_CarDoorCtrlBtnContent[0]))
            {
                m_DoorCtrlBtnStatus[3] = DoorCtrlBtnStatus.SETUNLOCKED;
                BtnNum4CarDoorControl.Content = m_CarDoorCtrlBtnContent[1];
            }
            else
            {
                m_DoorCtrlBtnStatus[3] = DoorCtrlBtnStatus.SETLOCKED;
                BtnNum4CarDoorControl.Content = m_CarDoorCtrlBtnContent[0];
            }
        }
        #endregion
        #endregion
        #region //楼梯控制按钮事件
        private void BtnNum1LadderControl_Click(object sender, RoutedEventArgs e)
        {
            if (BtnNum1LadderControl.Content.Equals(m_LadderStatusContent[0]))  //当前按钮显示内容为“靠近”平台时
            {
                m_LadderCtrlBtnStatus[0] = LadderCtrlBtnStatus.SETCLOSE;
                BtnNum1LadderControl.Content = m_LadderStatusContent[1];
                ClearLadderClosedDelayCounter(0);

            }
            else
            {
                m_LadderCtrlBtnStatus[0] = LadderCtrlBtnStatus.SETAWAY;
                BtnNum1LadderControl.Content = m_LadderStatusContent[0];
                ClearLadderAwayDelayCounter(0);
            }
        }

        private void BtnNum2LadderControl_Click(object sender, RoutedEventArgs e)
        {
            if (BtnNum2LadderControl.Content.Equals(m_LadderStatusContent[0]))  //当前按钮显示内容为“靠近”平台时
            {
                m_LadderCtrlBtnStatus[1] = LadderCtrlBtnStatus.SETCLOSE;
                BtnNum2LadderControl.Content = m_LadderStatusContent[1];
                ClearLadderClosedDelayCounter(1);
            }
            else
            {
                m_LadderCtrlBtnStatus[1] = LadderCtrlBtnStatus.SETAWAY;
                BtnNum2LadderControl.Content = m_LadderStatusContent[0];
                ClearLadderAwayDelayCounter(1);
            }
        }

        private void BtnNum3LadderControl_Click(object sender, RoutedEventArgs e)
        {
            if (BtnNum3LadderControl.Content.Equals(m_LadderStatusContent[0]))  //当前按钮显示内容为“靠近”平台时
            {
                m_LadderCtrlBtnStatus[2] = LadderCtrlBtnStatus.SETCLOSE;
                BtnNum3LadderControl.Content = m_LadderStatusContent[1];
                ClearLadderClosedDelayCounter(2);
            }
            else
            {
                m_LadderCtrlBtnStatus[2] = LadderCtrlBtnStatus.SETAWAY;
                BtnNum3LadderControl.Content = m_LadderStatusContent[0];
                ClearLadderAwayDelayCounter(2);
            }
        }

        private void BtnNum4LadderControl_Click(object sender, RoutedEventArgs e)
        {
            if (BtnNum4LadderControl.Content.Equals(m_LadderStatusContent[0]))  //当前按钮显示内容为“靠近”平台时
            {
                m_LadderCtrlBtnStatus[3] = LadderCtrlBtnStatus.SETCLOSE;
                BtnNum4LadderControl.Content = m_LadderStatusContent[1];
                ClearLadderClosedDelayCounter(3);
            }
            else
            {
                m_LadderCtrlBtnStatus[3] = LadderCtrlBtnStatus.SETAWAY;
                BtnNum4LadderControl.Content = m_LadderStatusContent[0];
                ClearLadderAwayDelayCounter(3);
            }
        }

        private void BtnNum5LadderControl_Click(object sender, RoutedEventArgs e)
        {
            if (BtnNum5LadderControl.Content.Equals(m_LadderStatusContent[0]))  //当前按钮显示内容为“靠近”平台时
            {
                m_LadderCtrlBtnStatus[4] = LadderCtrlBtnStatus.SETCLOSE;
                BtnNum5LadderControl.Content = m_LadderStatusContent[1];
                ClearLadderClosedDelayCounter(4);
            }
            else
            {
                m_LadderCtrlBtnStatus[4] = LadderCtrlBtnStatus.SETAWAY;
                BtnNum5LadderControl.Content = m_LadderStatusContent[0];
                ClearLadderAwayDelayCounter(4);
            }
        }
        #endregion
        #region //游戏启动按钮（测试使用）
        private void BtnStartUpPF1Game_Click(object sender, RoutedEventArgs e)
        {
            DataToDOF tToDOFBuf = new DataToDOF
            {
                nCheckID = 55,
                nCmd = 120,
                DOFs = new float[6],
                Vxyz = new float[3],
                Axyz = new float[3]
            };
            m_ConsoleUdp.m_listener.Send(StructToBytes(tToDOFBuf, Marshal.SizeOf(tToDOFBuf)), Marshal.SizeOf(tToDOFBuf), m_ConsoleUdp.m_RemoteIpEndpoint[0]);
            BtnStartUpPF1Game.Visibility = Visibility.Hidden;
        }

        private void BtnStartUpPF2Game_Click(object sender, RoutedEventArgs e)
        {
            DataToDOF tToDOFBuf = new DataToDOF
            {
                nCheckID = 55,
                nCmd = 120,
                DOFs = new float[6],
                Vxyz = new float[3],
                Axyz = new float[3]
            };
            m_ConsoleUdp.m_listener.Send(StructToBytes(tToDOFBuf, Marshal.SizeOf(tToDOFBuf)), Marshal.SizeOf(tToDOFBuf), m_ConsoleUdp.m_RemoteIpEndpoint[1]);
            BtnStartUpPF2Game.Visibility = Visibility.Hidden;
        }

        private void BtnStartUpPF3Game_Click(object sender, RoutedEventArgs e)
        {
            DataToDOF tToDOFBuf = new DataToDOF
            {
                nCheckID = 55,
                nCmd = 120,
                DOFs = new float[6],
                Vxyz = new float[3],
                Axyz = new float[3]
            };
            m_ConsoleUdp.m_listener.Send(StructToBytes(tToDOFBuf, Marshal.SizeOf(tToDOFBuf)), Marshal.SizeOf(tToDOFBuf), m_ConsoleUdp.m_RemoteIpEndpoint[2]);
            BtnStartUpPF3Game.Visibility = Visibility.Hidden;
        }

        private void BtnStartUpPF4Game_Click(object sender, RoutedEventArgs e)
        {
            DataToDOF tToDOFBuf = new DataToDOF
            {
                nCheckID = 55,
                nCmd = 120,
                DOFs = new float[6],
                Vxyz = new float[3],
                Axyz = new float[3]
            };
            m_ConsoleUdp.m_listener.Send(StructToBytes(tToDOFBuf, Marshal.SizeOf(tToDOFBuf)), Marshal.SizeOf(tToDOFBuf), m_ConsoleUdp.m_RemoteIpEndpoint[3]);
            BtnStartUpPF4Game.Visibility = Visibility.Hidden;
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

        private void Window_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
