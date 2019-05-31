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
using MainControl.MT_NET;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using static MainControl.MT_NET.PJLinkControl;
using System.Configuration;
using MainControl.Properties;
using System.IO;
using System.Windows.Interop;

namespace MainControl
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    /// 

    #region /* Exported types ------------------------------------------------------------*/

    public enum LightState:byte
    {
        OFF,
        ON
        
    }
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
        SETUNLOCKED,
        SET_CHANGE          //车门锁改为一个IO控制
    }
    public enum CarDoorStatus:byte
    {
        OPENED= 0,
        CLOSED= 1,
        UNKNOWN=2
    }
    public enum CarDoorLockStatus : byte
    {
        UNLOCKED = 0,
        LOCKED = 1,
        ACTIONING=2,
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
        ERROR = 3,
        UNKNOWN =4,          //PLC网络处于断开状态
    }
    public enum RaceState:byte
    {
            RACESTATE_INVALID,
            RACESTATE_NOT_STARTED,
            RACESTATE_RACING,
            RACESTATE_FINISHED,
            RACESTATE_DISQUALIFIED,
            RACESTATE_RETIRED,
            RACESTATE_DNF,
            //-------------
            RACESTATE_MAX
        };
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
        #region //Private variables
        private const int WM_QUERYENDSESSION = 0x0011;

        private readonly DateTime m_EndTime = new DateTime(2099, 5, 25, 0, 0, 0);
        private MtUdp m_ConsoleUdp = new MtUdp();
        private PJLinkControl pjLinkControl = new PJLinkControl();          //投影仪使用PJLink协议操作类
        public AdminLoginWindow adminLoginWindow = new AdminLoginWindow();

        private static Timer timer;
        private static readonly int m_LocalUdpPort = 10000;
        private bool PF_InitOverFlag = false;
        private bool[] PF_SingleInitOverFlag = new bool[4] { false, false, false, false };
        private bool PF_BottomStatusFlag = false;
        private bool PF_EnableRunStatusFlag = false;
        private bool[] pf_IsCheckedFlag = new bool[4] { false, false, false, false };
        public bool[] Pf_IsCheckedFlag { get => pf_IsCheckedFlag; set => pf_IsCheckedFlag = value; }
        private const byte LadderControllerMounter = 5;
        private const byte DoorMounter = 4;
        private CarDoorLockStatus[] m_CarDoorCurLockStatus = new CarDoorLockStatus[4] { CarDoorLockStatus.ACTIONING, CarDoorLockStatus.ACTIONING, CarDoorLockStatus.ACTIONING, CarDoorLockStatus.ACTIONING };
        private DoorCtrlBtnStatus[] m_DoorCtrlBtnStatus = new DoorCtrlBtnStatus[4] { DoorCtrlBtnStatus.SETIDLE, DoorCtrlBtnStatus.SETIDLE, DoorCtrlBtnStatus.SETIDLE, DoorCtrlBtnStatus.SETIDLE };
        private LadderStatus[] m_LadderCurStatus = new LadderStatus[5] { LadderStatus.MOVING, LadderStatus.MOVING, LadderStatus.MOVING, LadderStatus.MOVING, LadderStatus.MOVING };
        private LadderCtrlBtnStatus[] m_LadderCtrlBtnStatus = new LadderCtrlBtnStatus[5] { LadderCtrlBtnStatus.SETIDLE, LadderCtrlBtnStatus.SETIDLE, LadderCtrlBtnStatus.SETIDLE, LadderCtrlBtnStatus.SETIDLE, LadderCtrlBtnStatus.SETIDLE };
        private readonly string[] m_PfNetConnectDisplayContent = new string[11] { "连接", "断开", "出错", "待初始化", "寻底中", "回中位", "中位", "运行中", "底位", "M网断", "未勾选" };
        private readonly string[] m_CarDoorLabelDisplayContent = new string[3] { "打开", "关闭", "未知" };
        private readonly string[] m_LadderStatusContent = new string[5] { "靠近", "远离", "移动中", "出错", "未知" };
        private static string[] BtnStartOrEndContent = new string[4] { "   启动\r\n游戏体验", " 体验\r\n开始中", "   结束\r\n游戏体验", " 体验\r\n结束中" };
        private static string[] BtnResetContent = new string[2] { "   复位", "复位中" };
        private static string[] m_PJControlButtonContent = new string[2] { "关投影仪", "开投影仪" };
        private static string[] m_InitBtnContent = new string[4] { " 启动\r\n初始化", "初始化\r\n过程中", "初始化\r\n完成", "初始化\r\n出错" };

        private bool m_RaceFinishedFlag = false;
        private bool m_ActiveAutoEnd = false;

        StreamWriter m_LogWriter;
        string m_LogLastEventContent="";
        string m_LogLastButtonEventContent = "";
        string[] m_LogLastPfErrorContent = new string[4] { "", "", "", "" };
        string[] m_LogLastCarDoorState = new string[4] { "", "", "", "" };
        string[] m_LogLastGameState = new string[4] { "", "", "", "" };
        #endregion
        public MainWindow()
        {
            InitializeComponent();
            //进程锁
            EnsureOnlyOneProgressRun();

            m_LogWriter = new StreamWriter(System.AppDomain.CurrentDomain.BaseDirectory+"//Log//"+DateTime.Now.ToString("yyyy_MM_dd_hh_mm_ss")+".log");
            m_LogWriter.AutoFlush = true;
            //试用设备时间是否结束
            DateTime curTime = DateTime.Now;
            if(curTime> new DateTime((long)(new AppSettingsReader()).GetValue("SNC", typeof(long))))
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings["SNC"].Value = curTime.Ticks.ToString();
                config.Save();
                if (curTime>m_EndTime)
                {
                    MessageBox.Show("程序内部逻辑出现问题，请联系供应商");
                    Application.Current.Shutdown();
                }
            }
            else
            {
                MessageBox.Show("程序内部逻辑出现问题，请联系供应商");
                Application.Current.Shutdown();
            }

            //打开投影仪
            if (true == (bool)(new AppSettingsReader()).GetValue("PjAutoControlIsEnable", typeof(bool)))
            {
                CheckedProjectorOperate(POWER_STATE.POWER_ON);
            }
            //滑梯初始屏蔽？
            for(byte i=0;i<LADDER_AMOUNT;i++)
            {
                if (true == (bool)(new AppSettingsReader()).GetValue("Ladder"+(i+1)+"ShieldEnable", typeof(bool)))
                {
                    (adminLoginWindow.FindName("CbNum" + i + "LadderShieldCheck") as CheckBox).IsChecked = true;
                    adminLoginWindow.LadderShieldCheckFlag[i] = true;
                }
                else
                {
                    (adminLoginWindow.FindName("CbNum" + i + "LadderShieldCheck") as CheckBox).IsChecked = false;
                    adminLoginWindow.LadderShieldCheckFlag[i] = false;
                }
            }
            //本地网络初始化
            m_ConsoleUdp.UdpInit(m_LocalUdpPort);
            //发送数据多媒体定时器
            // Create an AutoResetEvent to signal the timeout threshold in the
            // timer callback has been reached.
            var autoEvent = new AutoResetEvent(false);
            timer = new Timer(new TimerCallback(TimerTask), autoEvent, 0, 10);
        }
        #region //定时器主线程
        private void TimerTask(object timerState)
        {
            this.Dispatcher.Invoke(
                new System.Action(
                    delegate
                    {
                        adminLoginWindow.DisplayErrorCode(m_ConsoleUdp.m_sToHostBuf);
                        GameStateUiUpdate();
                        PlatformNetStatusIndicator();
                        CheckPfSelectedStatus();
                        PfBottomStateCheck();
                        DevicesCheckAndBtnEnable();
                        #region //如果按下初始化按钮,即初始化按钮内容为初始化中；
                        if ((BtnInitOrWaitPassenger.Content.Equals(m_InitBtnContent[1])))         //(PF_InitOverFlag==false)&&
                        {
                            PlatformAllCheckedEnableControl(false);
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
                                    if (((Label)FindName("Ladder" + (i + 1) + "StatusDisplay")).Content.Equals(m_LadderStatusContent[3])) //如果有滑梯出错
                                    {
                                        MotusMessageBox((i + 1) + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                        BtnInitOrWaitPassenger.Content = m_InitBtnContent[3];          //告知使用者初始化出错；
                                    }
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
                                            if (-1 == m_ConsoleUdp.DofUpToMedian(m_ConsoleUdp.m_RemoteIpEndpoint[i]))
                                            {
                                                MotusMessageBox("PLC断开连接，请检查！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                            }
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
                                            if (-1 == m_ConsoleUdp.DofToBottom(m_ConsoleUdp.m_RemoteIpEndpoint[i]))
                                            {
                                                MotusMessageBox("PLC断开连接，请检查！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                            }
                                        }
                                    }
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
                                    if (true == adminLoginWindow.LadderShieldCheckFlag[i])
                                    {
                                        SetLadderAway(i, m_ConsoleUdp.m_DataToPlc);
                                    }
                                    else
                                    {
                                        SetLadderClose(i, m_ConsoleUdp.m_DataToPlc);
                                    }
                                    if (((Label)FindName("Ladder" + (i + 1) + "StatusDisplay")).Content.Equals(m_LadderStatusContent[3])) //如果有滑梯出错
                                    {
                                        MotusMessageBox((i + 1) + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                        BtnInitOrWaitPassenger.Content = m_InitBtnContent[3];          //告知使用者初始化出错；
                                    }
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
                                    if ((m_CarDoorCurLockStatus[i] != CarDoorLockStatus.UNLOCKED) && (CarDoorLockStatus.UNLOCKED == SetCarDoorUnlocked(i, m_ConsoleUdp.m_DataToPlc)))
                                    {
                                        m_CarDoorCurLockStatus[i] = CarDoorLockStatus.UNLOCKED;
                                        //SetCarDoorBtnDisplayContent(i, m_CarDoorCurLockStatus[i]);
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
                                    BtnInitOrWaitPassenger.Content = m_InitBtnContent[2];//初始化完成
                                    SetWaitLight(LightState.ON);
                                    for (byte i = 0; i < MtUdp.DeviceAmount; i++)
                                    {
                                        if (true == Pf_IsCheckedFlag[i])
                                        {
                                            PF_SingleInitOverFlag[i] = true;
                                            PlatformCheckedEnableControl(i, true);

                                        }
                                        else
                                        {
                                            PF_SingleInitOverFlag[i] = false;
                                            PlatformCheckedEnableControl(i, false);
                                        }
                                    }
                                }
                            }
                        }
                        #endregion
                        #region //如果按下体验开始按钮
                        else if (BtnStart.Content.Equals(BtnStartOrEndContent[1]))
                        {
                            PlatformAllCheckedEnableControl(false);
                            SetRunLight(LightState.ON);
                            //判断车门是否锁定，如未锁定，则锁定车门，
                            if ((m_CarDoorCurLockStatus[0] != CarDoorLockStatus.LOCKED)
                            || (m_CarDoorCurLockStatus[1] != CarDoorLockStatus.LOCKED)
                            || (m_CarDoorCurLockStatus[2] != CarDoorLockStatus.LOCKED)
                            || (m_CarDoorCurLockStatus[3] != CarDoorLockStatus.LOCKED)
                                )
                            {
                                TextBoxOperateInstruction.Text = "锁定车门中";
                                for (byte i = 0; i < MtUdp.DeviceAmount; i++)
                                {
                                    if ((m_CarDoorCurLockStatus[i] != CarDoorLockStatus.LOCKED) && (CarDoorLockStatus.LOCKED == SetCarDoorLocked(i, m_ConsoleUdp.m_DataToPlc)))
                                    {
                                        m_CarDoorCurLockStatus[i] = CarDoorLockStatus.LOCKED;
                                        //SetCarDoorBtnDisplayContent(i, m_CarDoorCurLockStatus[i]);
                                    }
                                }
                                m_ConsoleUdp.SendDataToPlc(m_ConsoleUdp.m_DataToPlc, m_ConsoleUdp.m_DataToPlc.Length, m_ConsoleUdp.m_PlcIpEndpoint);
                            }
                            else if ((PF_EnableRunStatusFlag == true)
                            && (Pf_IsCheckedFlag[0] ? (m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus == (byte)(DOF_state.dof_working)) : true)
                            && (Pf_IsCheckedFlag[1] ? (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus == (byte)(DOF_state.dof_working)) : true)
                            && (Pf_IsCheckedFlag[2] ? (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus == (byte)(DOF_state.dof_working)) : true)
                            && (Pf_IsCheckedFlag[3] ? (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus == (byte)(DOF_state.dof_working)) : true)
                            )
                            {
                                PF_EnableRunStatusFlag = false;//清零；
                                BtnStart.Content = BtnStartOrEndContent[0];
                                BtnEnd.IsHitTestVisible = true;
                                TextBoxOperateInstruction.Text = "体验已开始，待体验结束后，点击“体验结束按钮”";
                            }
                            else if ((PF_EnableRunStatusFlag == true)
                            && (
                                (Pf_IsCheckedFlag[0] ? (m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus != (byte)(DOF_state.dof_working)) : false)
                                || (Pf_IsCheckedFlag[1] ? (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus != (byte)(DOF_state.dof_working)) : false)
                                || (Pf_IsCheckedFlag[2] ? (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus != (byte)(DOF_state.dof_working)) : false)
                                || (Pf_IsCheckedFlag[3] ? (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus != (byte)(DOF_state.dof_working)) : false)
                                )
                            )
                            {
                                for (int i = 0; i < MtUdp.DeviceAmount; i++)
                                {
                                    if (true == Pf_IsCheckedFlag[i])
                                    {
                                        if (-1 == m_ConsoleUdp.DofToRun(m_ConsoleUdp.m_RemoteIpEndpoint[i]))
                                        {
                                            MotusMessageBox("PLC断开连接，请检查！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                            BtnStart.Content = BtnStartOrEndContent[0];
                                            BtnStart.IsHitTestVisible = true;
                                        }
                                    }
                                }
                            }
                            //如果有楼梯在靠近状态，且有设备被勾选
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
                                TextBoxOperateInstruction.Text = "等待安全门远离";
                                for (byte i = 0; i < LadderControllerMounter; i++)
                                {
                                    SetLadderAway(i, m_ConsoleUdp.m_DataToPlc);
                                    if (((Label)FindName("Ladder" + (i + 1) + "StatusDisplay")).Content.Equals(m_LadderStatusContent[3])) //如果有滑梯出错
                                    {
                                        MotusMessageBox((i + 1) + "号楼梯运动超时，请检查设备或通过管理员模式屏蔽相应楼梯！并重新点击“体验开始”", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                        BtnStart.Content = BtnStartOrEndContent[0];
                                        BtnStart.IsHitTestVisible = true;
                                    }
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
                                        if (-1 == m_ConsoleUdp.DofUpToMedian(m_ConsoleUdp.m_RemoteIpEndpoint[i]))
                                        {
                                            MotusMessageBox("PLC断开连接，请检查！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                            BtnStart.Content = BtnStartOrEndContent[0];
                                            BtnStart.IsHitTestVisible = true;
                                        }
                                    }
                                }
                            }
                            //如果滑梯都远离平台，且勾选的平台都在中位
                            else if ((adminLoginWindow.LadderShieldCheckFlag[0] ? true : (LadderStatus.AWAY == GetLadderStatus(0, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[1] ? true : (LadderStatus.AWAY == GetLadderStatus(1, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[2] ? true : (LadderStatus.AWAY == GetLadderStatus(2, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[3] ? true : (LadderStatus.AWAY == GetLadderStatus(3, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[4] ? true : (LadderStatus.AWAY == GetLadderStatus(4, m_ConsoleUdp.m_DataFromPlc)))
                             && (Pf_IsCheckedFlag[0] ? (m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus == (byte)(DOF_state.dof_neutral)) : true)
                            && (Pf_IsCheckedFlag[1] ? (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus == (byte)(DOF_state.dof_neutral)) : true)
                            && (Pf_IsCheckedFlag[2] ? (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus == (byte)(DOF_state.dof_neutral)) : true)
                            && (Pf_IsCheckedFlag[3] ? (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus == (byte)(DOF_state.dof_neutral)) : true)
                            && (PF_EnableRunStatusFlag == false)
                             )
                            {
                                for (int i = 0; i < MtUdp.DeviceAmount; i++)
                                {
                                    if (true == Pf_IsCheckedFlag[i])
                                    {
                                        if (-1 == m_ConsoleUdp.DofToRun(m_ConsoleUdp.m_RemoteIpEndpoint[i]))
                                        {
                                            MotusMessageBox("PLC断开连接，请检查！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                            BtnStart.Content = BtnStartOrEndContent[0];
                                            BtnStart.IsHitTestVisible = true;
                                        }
                                    }
                                }
                                PF_EnableRunStatusFlag = true;
                            }

                        }
                        #endregion
                        #region //如果按下体验结束按钮
                        else if (BtnEnd.Content.Equals(BtnStartOrEndContent[3])||(BtnReset.Content.Equals(BtnResetContent[1])))
                        {
                            if(BtnEnd.Content.Equals(BtnStartOrEndContent[3]))
                            {
                                SetStopLight(LightState.ON);
                            }
                            else
                            {
                                SetResetLight(LightState.ON);
                            }
                            PlatformAllCheckedEnableControl(false);
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
                                    if (((Label)FindName("Ladder" + (i + 1) + "StatusDisplay")).Content.Equals(m_LadderStatusContent[3])) //如果有滑梯出错
                                    {
                                        MotusMessageBox((i + 1) + "号楼梯状态出错，请检查设备或通过管理员模式屏蔽相应楼梯，再点击“体验结束”", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                        if (BtnEnd.Content.Equals(BtnStartOrEndContent[3]))
                                        {
                                            BtnEnd.Content = BtnStartOrEndContent[2];
                                            BtnEnd.IsHitTestVisible = true;
                                        }
                                        else
                                        {
                                            BtnReset.Content = BtnResetContent[0];
                                            BtnReset.IsHitTestVisible = true;
                                        }
                                    }
                                }
                                m_ConsoleUdp.SendDataToPlc(m_ConsoleUdp.m_DataToPlc, m_ConsoleUdp.m_DataToPlc.Length, m_ConsoleUdp.m_PlcIpEndpoint);
                            }
                            //如果梯子都远离成功，但平台不在底位,让平台回底。
                            else if ((adminLoginWindow.LadderShieldCheckFlag[0] ? true : (LadderStatus.AWAY == GetLadderStatus(0, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[1] ? true : (LadderStatus.AWAY == GetLadderStatus(1, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[2] ? true : (LadderStatus.AWAY == GetLadderStatus(2, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[3] ? true : (LadderStatus.AWAY == GetLadderStatus(3, m_ConsoleUdp.m_DataFromPlc)))
                             && (adminLoginWindow.LadderShieldCheckFlag[4] ? true : (LadderStatus.AWAY == GetLadderStatus(4, m_ConsoleUdp.m_DataFromPlc)))
                             && (PF_BottomStatusFlag == false))
                            {
                                for (int i = 0; i < MtUdp.DeviceAmount; i++)
                                {
                                    //if (true == Pf_IsCheckedFlag[i])
                                    //{
                                        if (-1 == m_ConsoleUdp.DofToBottom(m_ConsoleUdp.m_RemoteIpEndpoint[i]))
                                        {
                                            MotusMessageBox("PLC断开连接，请检查！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                            if (BtnEnd.Content.Equals(BtnStartOrEndContent[3]))
                                            {
                                                BtnEnd.Content = BtnStartOrEndContent[2];
                                            }
                                            else
                                            {
                                                BtnReset.Content = BtnResetContent[0];
                                            }
                                        }
                                    //}
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
                                    if (true == adminLoginWindow.LadderShieldCheckFlag[i])
                                    {
                                        SetLadderAway(i, m_ConsoleUdp.m_DataToPlc);
                                    }
                                    else
                                    {
                                        SetLadderClose(i, m_ConsoleUdp.m_DataToPlc);
                                    }
                                    if (((Label)FindName("Ladder" + (i + 1) + "StatusDisplay")).Content.Equals(m_LadderStatusContent[3])) //如果有滑梯出错
                                    {
                                        MotusMessageBox((i + 1) + "号楼梯状态出错，请检查设备或通过管理员模式屏蔽相应楼梯，再点击“体验结束”或单个关闭楼梯", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                        if (BtnEnd.Content.Equals(BtnStartOrEndContent[3]))
                                        {
                                            BtnEnd.Content = BtnStartOrEndContent[2];
                                        }
                                        else
                                        {
                                            BtnReset.Content = BtnResetContent[0];
                                        }
                                    }
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
                                //此状态可以上下客,解锁车辆
                                for (byte i = 0; i < MtUdp.DeviceAmount; i++)
                                {
                                    if ((m_CarDoorCurLockStatus[i] != CarDoorLockStatus.UNLOCKED) && (CarDoorLockStatus.UNLOCKED == SetCarDoorUnlocked(i, m_ConsoleUdp.m_DataToPlc)))
                                    {
                                        m_CarDoorCurLockStatus[i] = CarDoorLockStatus.UNLOCKED;
                                        //SetCarDoorBtnDisplayContent(i, m_CarDoorCurLockStatus[i]);
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
                                    if (BtnEnd.Content.Equals(BtnStartOrEndContent[3]))
                                    {
                                        BtnEnd.Content = BtnStartOrEndContent[2];
                                    }
                                    else
                                    {
                                        BtnReset.Content = BtnResetContent[0];
                                        BtnReset.IsHitTestVisible = true;
                                    }
                                    EnableBtnStart(true);
                                    SetWaitLight(LightState.ON);
                                    for (byte i = 0; i < MtUdp.DeviceAmount; i++)
                                    {
                                        if (true == Pf_IsCheckedFlag[i])
                                        {
                                            PlatformCheckedEnableControl(i, true);

                                        }
                                        else
                                        {
                                            PlatformCheckedEnableControl(i, false);
                                        }
                                    }
                                }
                            }
                            #endregion
                        }
                    }
                    )
            );
        }
        #endregion
        #region //平台勾选使能控制
        /// <summary>
        /// 
        /// </summary>
        /// <param name="state">true/false</param>
        void PlatformAllCheckedEnableControl(bool state)
        {
            for (byte i = 0; i < MtUdp.DeviceAmount; i++)
            {
                PlatformCheckedEnableControl(i, state);
            }
        }
        /// <summary>
        /// 平台勾选框使能操作；在某些特定条件下不允许点击
        /// </summary>
        /// <param name="index">0~3</param>
        /// <param name="state">true/false</param>
        void PlatformCheckedEnableControl(byte index, bool state)
        {
            if (true == state)
            {
                ((CheckBox)FindName("CbNum" + index + "Platform")).ToolTip = "单击即可选定平台";
            }
            else
            {
                ((CheckBox)FindName("CbNum" + index + "Platform")).ToolTip = "平台不可被勾选，原因可能如下：\r\n1. 平台通信故障；\r\n2. 平台运行中；\r\n";//
            }
            ((CheckBox)FindName("CbNum" + index + "Platform")).IsEnabled = state;
        }
        #endregion
        #region //各状态检测及相关提示
        void DevicesCheckAndBtnEnable()
        {
            //如果PLC未连接，或无平台可用，或未勾选设备，不使能开始/结束按钮
            if((Brushes.Red==GbPfsAndPlcStates.BorderBrush)||(Brushes.Red==GbDevicesSelect.BorderBrush))
            {
                EnableBtnStart(false);
                EnableBtnEnd(false);
            }
            PlcState.Content = m_ConsoleUdp.m_PLCConnectState;
            if (m_ConsoleUdp.m_PLCConnectState.Equals(MtUdp.PlcConnectStateContent[0]))
            {
                PlcState.Background = Brushes.Red;
                SetGbBorderColorToRed(GbPfsAndPlcStates);
                SetBtnEnableStateRelatedToPlc(false);
                SetLabelContentWhenPlcDisconnect();
                TextBoxOperateInstruction.Text = "PLC网络断开，主控按钮不可被点击，请检修";
                WriteEventContentToLog(TextBoxOperateInstruction.Text);
            }
            else
            {
                PlcState.Background = Brushes.White;
                LadderStateAndBtnStateUpdate();
                CarDoorUiUpdate();
                CarDoorsBtnHandler();
                LadderBtnHandler();
                PlcDataHandler();
                HardwareErrorCheckAndErrorLightCtrl();
                if (0 == ((m_ConsoleUdp.m_DataFromPlc[0] >> 0) & (0x01)))                             //判断急停按钮
                {
                    TextBoxOperateInstruction.Text = "急停按钮被按下";
                    WriteEventContentToLog(TextBoxOperateInstruction.Text);
                    //仅使用硬件按钮，软件按钮仅作为指示灯
                    //发送急停指令，但平台操作程序接收急停指令后，并不进行数据发送保持在当前位置
                    for (int i = 0; i < MtUdp.DeviceAmount; i++)
                    {
                        m_ConsoleUdp.DofToEmergency(m_ConsoleUdp.m_RemoteIpEndpoint[i]);
                    }
                    BtnEmerge.Content = "急停\r\n按下";
                    BtnEmerge.Background = Brushes.Red;
                    DataClearWhenEmergBtnPressed();
                }
                else
                {
                    BtnEmerge.Content = "急停\r\n松开";
                    BtnEmerge.Background = Brushes.White;
                    for(byte i=0;i<MtUdp.DeviceAmount;i++)
                    {
                        if((Brushes.Red == (FindName("PF"+i+"State") as Label).Background)
                            &&(BtnStart.Content.Equals(BtnStartOrEndContent[0]))
                            && (BtnEnd.Content.Equals(BtnStartOrEndContent[2]))
                            && (BtnReset.Content.Equals(BtnResetContent[0]))
                            )
                        {
                            m_ConsoleUdp.DofToEmergency(m_ConsoleUdp.m_RemoteIpEndpoint[i]);
                        }
                    }
                    if ((Brushes.Red == PF0State.Background)
                        && (Brushes.Red == PF1State.Background)
                        && (Brushes.Red == PF2State.Background)
                        && (Brushes.Red == PF3State.Background)
                        )
                    {
                        SetGbBorderColorToRed(GbPfsAndPlcStates);
                        TextBoxOperateInstruction.Text = "无可正常使用平台，请查看设备状态并检修";
                    }
                    else
                    {
                        RecoveryGbBorderBrushToDefault(GbPfsAndPlcStates);
                        if ((true != Pf_IsCheckedFlag[0])
                            && (true != Pf_IsCheckedFlag[1])
                            && (true != Pf_IsCheckedFlag[2])
                            && (true != Pf_IsCheckedFlag[3])
                            )
                        {
                            SetGbBorderColorToRed(GbDevicesSelect);
                            TextBoxOperateInstruction.Text = "请勾选需要使用的设备";
                        }
                        else
                        {
                            RecoveryGbBorderBrushToDefault(GbDevicesSelect);
                            //如果有车门未关闭;则提醒操作员；
                            if ((CarDoorStatus.CLOSED != GetCarDoorStatus(0))
                                || (CarDoorStatus.CLOSED != GetCarDoorStatus(1))
                                || (CarDoorStatus.CLOSED != GetCarDoorStatus(2))
                                || (CarDoorStatus.CLOSED != GetCarDoorStatus(3))
                                )
                            {
                                EnableBtnStart(false);
                                BtnStart.Content = BtnStartOrEndContent[0];
                                GbCarDoors.BorderBrush = Brushes.Red;
                                string tString = "请关闭";
                                for (byte i = 0; i < MtUdp.DeviceAmount; i++)
                                {
                                    if (CarDoorStatus.CLOSED != GetCarDoorStatus(i))
                                    {
                                        m_ConsoleUdp.DofToEmergency(m_ConsoleUdp.m_RemoteIpEndpoint[i]);
                                        tString += (i + 1) + "号设备  ";
                                    }
                                }
                                tString += "车门";
                                TextBoxOperateInstruction.Text = tString;
                            }
                            else
                            {
                                RecoveryGbBorderBrushToDefault(GbCarDoors);
                                IsActiveAutoEnd();  //判断是否激活自动结束体验
                                if (BtnStart.Content.Equals(BtnStartOrEndContent[0])
                                    && (true == PF_BottomStatusFlag)
                                    )
                                {
                                    TextBoxOperateInstruction.Text = "请点击\"启动游戏体验\"按钮,开始游戏体验";
                                    EnableBtnStart(true);
                                }
                                else
                                {
                                    EnableBtnStart(false);
                                }

                                if (BtnEnd.Content.Equals(BtnStartOrEndContent[2])
                                    //&&(true==BtnEnd.IsHitTestVisible)
                                    && (false == PF_BottomStatusFlag)
                                    )
                                {
                                    TextBoxOperateInstruction.Text = "点击\"结束游戏体验\"按钮,设备恢复至待客状态";
                                    EnableBtnEnd(true);
                                }
                                else
                                {
                                    EnableBtnEnd(false);
                                }
                            }
                        }
                    }
                }
            }
        }
        #endregion
        #region //核查平台勾选状态
        void CheckPfSelectedStatus()
        {
            for (int i = 0; i < MtUdp.DeviceAmount; i++)
            {
                if (true == ((CheckBox)FindName("CbNum" + i + "Platform")).IsChecked)
                {
                    Pf_IsCheckedFlag[i] = true;
                }
                else
                {
                    Pf_IsCheckedFlag[i] = false;
                }
            }
            //1号平台
            //if (true == CbNum0Platform.IsChecked)
            //{
            //    Pf_IsCheckedFlag[0] = true;
            //}
            //else
            //{
            //    Pf_IsCheckedFlag[0] = false;
            //}

            //if (true == CbNum1Platform.IsChecked)
            //{
            //    Pf_IsCheckedFlag[1] = true;
            //}
            //else
            //{
            //    Pf_IsCheckedFlag[1] = false;
            //}

            //if (true == CbNum2Platform.IsChecked)
            //{
            //    Pf_IsCheckedFlag[2] = true;
            //}
            //else
            //{
            //    Pf_IsCheckedFlag[2] = false;
            //}

            //if (true == CbNum3Platform.IsChecked)
            //{
            //    Pf_IsCheckedFlag[3] = true;
            //}
            //else
            //{
            //    Pf_IsCheckedFlag[3] = false;
            //}
        }
        #endregion
        #region //PLC数据处理
        private void PlcDataHandler()
        {
            m_ConsoleUdp.PlcNetDelayCounter();
            if ((1 == ((m_ConsoleUdp.m_DataFromPlc[0] >> 1) & (0x01)))&&(BtnStart.Content.Equals(BtnStartOrEndContent[0])))                       //判断运行按钮
            {
                if (false == BtnStart.IsHitTestVisible)
                {
                    MotusMessageBox("操作错误！请查看操作指引\r\n开始按钮：" + ((m_ConsoleUdp.m_DataFromPlc[0] >> 1) & (0x01)).ToString()
                        + "\r\nBtnStart.IsHitTestVisible："+ BtnStart.IsHitTestVisible.ToString()
                        , "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    //使软件按钮与硬件保持一致
                    BtnStartOrEnd_Click(BtnStart, null);
                }
            }
            if (((1 == ((m_ConsoleUdp.m_DataFromPlc[0] >> 2) & (0x01)))||(true==m_ActiveAutoEnd)) && (BtnEnd.Content.Equals(BtnStartOrEndContent[2])))                 //判断关机按钮
            {
                m_ActiveAutoEnd = false;
                if (false == BtnEnd.IsHitTestVisible)
                {
                    MotusMessageBox("操作错误！请查看操作指引\r\n 停止按钮："+ ((m_ConsoleUdp.m_DataFromPlc[0] >> 2) & (0x01)).ToString()
                        + "\r\nm_ActiveAutoEnd："+ m_ActiveAutoEnd.ToString()
                        + "\r\nBtnEnd.IsHitTestVisible:"+ BtnEnd.IsHitTestVisible.ToString()
                        , "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    //使软件按钮与硬件保持一致
                    BtnStartOrEnd_Click(BtnEnd, null);
                }
            }
            if ((1 == ((m_ConsoleUdp.m_DataFromPlc[0] >> 3) & (0x01))) && (BtnReset.Content.Equals(BtnResetContent[0])))      //判断复位按钮
            {
                if (false == BtnReset.IsHitTestVisible)
                {
                    MotusMessageBox("操作错误！请查看操作指引\r\n复位按钮：" + ((m_ConsoleUdp.m_DataFromPlc[0] >> 3) & (0x01)).ToString()
                         + "\r\nBtnReset.IsHitTestVisible：" + BtnReset.IsHitTestVisible.ToString()
                        , "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    BtnReset_Click(BtnReset, null);
                }

            }
        }
        #endregion

        #region //指示平台网络连接状态
        private void PlatformNetStatusIndicator()
        {
            for (byte i = 0; i < MtUdp.DeviceAmount; i++)
            {
                if (true == m_ConsoleUdp.m_DeviceConnectState[i])
                {
                    m_ConsoleUdp.PfNetDelayCounter(i);
                    if (false == PF_InitOverFlag)
                    {
                        PlatformCheckedEnableControl(i, true);
                    }
                    if (118 == m_ConsoleUdp.m_sToHostBuf[i].nDOFStatus)
                    {
                        ((Label)FindName("PF" + i + "State")).Background = Brushes.Red;
                        ((Label)FindName("PF" + i + "State")).Content = m_PfNetConnectDisplayContent[9];
                        ((Label)FindName("PF" + i + "State")).ToolTip = (i + 1) + "号平台网络断开，请检查网络连接；";
                    }
                    else if ((119 == m_ConsoleUdp.m_sToHostBuf[i].nDOFStatus)
                        || (0.01 < m_ConsoleUdp.m_sToHostBuf[i].para[0])
                        || (0.01 < m_ConsoleUdp.m_sToHostBuf[i].para[1])
                        || (0.01 < m_ConsoleUdp.m_sToHostBuf[i].para[2])
                        || (0.01 < m_ConsoleUdp.m_sToHostBuf[i].para[3])
                        || (0.01 < m_ConsoleUdp.m_sToHostBuf[i].para[4])
                        || (0.01 < m_ConsoleUdp.m_sToHostBuf[i].para[5])
                        )
                    {
                        if(119 == m_ConsoleUdp.m_sToHostBuf[i].nDOFStatus)
                        {
                            ((Label)FindName("PF" + i + "State")).Content = m_PfNetConnectDisplayContent[2];
                        }
                        else
                        {
                            ((Label)FindName("PF" + i + "State")).Content = m_ConsoleUdp.m_sToHostBuf[i].nDOFStatus;
                        }
                        ((Label)FindName("PF" + i + "State")).Background = Brushes.Red;
                        ((Label)FindName("PF" + i + "State")).ToolTip = "设备中有驱动器报错；请点击管理员按钮查看对应设备的错误代码";
                    }
                    else if (55 == m_ConsoleUdp.m_sToHostBuf[i].nDOFStatus)
                    {
                        if (/*(true == PF_InitOverFlag) && */(true == ((CheckBox)FindName("CbNum" + i + "Platform")).IsChecked))
                        {
                            ((Label)FindName("PF" + i + "State")).Background = Brushes.Green;
                            ((Label)FindName("PF" + i + "State")).Content = m_PfNetConnectDisplayContent[8]; // 在底位
                        }
                        else
                        {
                            ((Label)FindName("PF" + i + "State")).Background = Brushes.Yellow;
                            ((Label)FindName("PF" + i + "State")).Content = m_PfNetConnectDisplayContent[10]; //未勾选
                        }
                    }
                    else if (0 == m_ConsoleUdp.m_sToHostBuf[i].nDOFStatus)
                    {

                        ((Label)FindName("PF" + i + "State")).Background = Brushes.White;
                        ((Label)FindName("PF" + i + "State")).Content = m_PfNetConnectDisplayContent[4]; // 在寻底状态

                    }
                    else if (1 == m_ConsoleUdp.m_sToHostBuf[i].nDOFStatus)
                    {

                        ((Label)FindName("PF" + i + "State")).Background = Brushes.White;
                        ((Label)FindName("PF" + i + "State")).Content = m_PfNetConnectDisplayContent[5]; // 在回中状态

                    }
                    else if (2 == m_ConsoleUdp.m_sToHostBuf[i].nDOFStatus)
                    {

                        ((Label)FindName("PF" + i + "State")).Background = Brushes.White;
                        ((Label)FindName("PF" + i + "State")).Content = m_PfNetConnectDisplayContent[6]; // 在中位状态

                    }
                    else if (3 == m_ConsoleUdp.m_sToHostBuf[i].nDOFStatus)
                    {

                        ((Label)FindName("PF" + i + "State")).Background = Brushes.Orange;
                        ((Label)FindName("PF" + i + "State")).Content = m_PfNetConnectDisplayContent[7]; // 在运行状态

                    }
                    else
                    {
                        ((Label)FindName("PF" + i + "State")).Background = Brushes.Red;
                        ((Label)FindName("PF" + i + "State")).Content = m_ConsoleUdp.m_sToHostBuf[i].nDOFStatus; // 在错误代码
                    }
                    if (Brushes.Red == ((Label)FindName("PF" + i + "State")).Background)
                    {
                        ((CheckBox)FindName("CbNum" + i + "Platform")).IsChecked = false;
                        PlatformCheckedEnableControl(i, false);
                        //((CheckBox)FindName("CbNum" + i + "Platform")).IsEnabled = false;
                    }
                }
                else if (false == m_ConsoleUdp.m_DeviceConnectState[i])
                {
                    //使平台不可勾选，且取消勾选；
                    ((CheckBox)FindName("CbNum" + i + "Platform")).IsChecked = false;
                    PlatformCheckedEnableControl(i, false);
                    m_ConsoleUdp.m_sToHostBuf[i].nRev1 = 0;

                    ((Label)FindName("PF" + i + "State")).Background = Brushes.Red;
                    ((Label)FindName("PF" + i + "State")).Content = m_PfNetConnectDisplayContent[1];
                }
                if ((119 == m_ConsoleUdp.m_sToHostBuf[i].nDOFStatus)
                        || (0.01 < m_ConsoleUdp.m_sToHostBuf[i].para[0])
                        || (0.01 < m_ConsoleUdp.m_sToHostBuf[i].para[1])
                        || (0.01 < m_ConsoleUdp.m_sToHostBuf[i].para[2])
                        || (0.01 < m_ConsoleUdp.m_sToHostBuf[i].para[3])
                        || (0.01 < m_ConsoleUdp.m_sToHostBuf[i].para[4])
                        || (0.01 < m_ConsoleUdp.m_sToHostBuf[i].para[5])
                        )
                {
                    WritePfErrorContentToLog(i,((Label)FindName("PF" + i + "State")).Content.ToString()+"，错误代码:"
                        + (int)m_ConsoleUdp.m_sToHostBuf[i].nDOFStatus + ";Para代码："
                        + (int)m_ConsoleUdp.m_sToHostBuf[i].para[0]+"," + (int)m_ConsoleUdp.m_sToHostBuf[i].para[1] + "," 
                        + (int)m_ConsoleUdp.m_sToHostBuf[i].para[2] + "," + (int)m_ConsoleUdp.m_sToHostBuf[i].para[3] + ","
                        + (int)m_ConsoleUdp.m_sToHostBuf[i].para[4] + "," + (int)m_ConsoleUdp.m_sToHostBuf[i].para[5]
                        );
                }
                else
                {
                    WritePfErrorContentToLog(i, ((Label)FindName("PF" + i + "State")).Content.ToString());
                }
            }
        }
        #endregion
        #region //判断平台是否在底位状态；
        /// <summary>
        /// 判断平台是否在底位状态；
        /// </summary>
        void PfBottomStateCheck()
        {
            if ((Pf_IsCheckedFlag[0] ? (m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                && (Pf_IsCheckedFlag[1] ? (m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                && (Pf_IsCheckedFlag[2] ? (m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                && (Pf_IsCheckedFlag[3] ? (m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus == (byte)(DOF_state.dof_check_id)) : true)
                && ((true == Pf_IsCheckedFlag[0]) || (true == Pf_IsCheckedFlag[1]) || (true == Pf_IsCheckedFlag[2]) || (true == Pf_IsCheckedFlag[3]))
                )
            {
                PF_BottomStatusFlag = true;
            }
            else
            {
                PF_BottomStatusFlag = false;
            }
        } 
        #endregion
        private void GameStateUiUpdate()
        {
            for(int i=0;i<MtUdp.DeviceAmount;i++)
            {
                switch(m_ConsoleUdp.m_sToHostBuf[i].nRev1)
                {
                    case 0:
                        ((Label)FindName("PF" + (i + 1) + "GameState")).Background = Brushes.Green;
                        ((Label)FindName("PF" + (i + 1) + "GameState")).Content = "未开始";
                        break;
                    case 1:
                    case 2:
                        ((Label)FindName("PF" + (i + 1) + "GameState")).Background = Brushes.Orange;
                        ((Label)FindName("PF" + (i + 1) + "GameState")).Content = "比赛中";
                        break;
                    case 3:
                        ((Label)FindName("PF" + (i + 1) + "GameState")).Background = Brushes.Red;
                        ((Label)FindName("PF" + (i + 1) + "GameState")).Content = "比赛结束";
                        break;
                    case 4:
                        ((Label)FindName("PF" + (i + 1) + "GameState")).Background = Brushes.Red;
                        ((Label)FindName("PF" + (i + 1) + "GameState")).Content = "丧失资格";
                        break;
                    case 5:
                        ((Label)FindName("PF" + (i + 1) + "GameState")).Background = Brushes.Red;
                        ((Label)FindName("PF" + (i + 1) + "GameState")).Content = "异常退出";
                        break;
                    case 6:
                        ((Label)FindName("PF" + (i + 1) + "GameState")).Background = Brushes.Red;
                        ((Label)FindName("PF" + (i + 1) + "GameState")).Content = "比赛未完成";
                        break;
                }
                WriteGameStateToLog(i, ((Label)FindName("PF" + (i + 1) + "GameState")).Content.ToString());
            }
        }
        private void IsActiveAutoEnd()
        {
            if ((Pf_IsCheckedFlag[0]|| Pf_IsCheckedFlag[1]|| Pf_IsCheckedFlag[2]|| Pf_IsCheckedFlag[3])
                &&(false== m_RaceFinishedFlag)
                &&(Pf_IsCheckedFlag[0] ?((((byte)(RaceState.RACESTATE_FINISHED)==m_ConsoleUdp.m_sToHostBuf[0].nRev1)|| ((byte)(RaceState.RACESTATE_DNF) == m_ConsoleUdp.m_sToHostBuf[0].nRev1))&&((byte)(DOF_state.dof_working)==m_ConsoleUdp.m_sToHostBuf[0].nDOFStatus)) : true)
                && (Pf_IsCheckedFlag[1] ? ((((byte)(RaceState.RACESTATE_FINISHED) == m_ConsoleUdp.m_sToHostBuf[1].nRev1) || ((byte)(RaceState.RACESTATE_DNF) == m_ConsoleUdp.m_sToHostBuf[0].nRev1))&&((byte)(DOF_state.dof_working) == m_ConsoleUdp.m_sToHostBuf[1].nDOFStatus)) : true)
                && (Pf_IsCheckedFlag[2] ? ((((byte)(RaceState.RACESTATE_FINISHED) == m_ConsoleUdp.m_sToHostBuf[2].nRev1) || ((byte)(RaceState.RACESTATE_DNF) == m_ConsoleUdp.m_sToHostBuf[1].nRev1))&&((byte)(DOF_state.dof_working) == m_ConsoleUdp.m_sToHostBuf[2].nDOFStatus)) : true)
                && (Pf_IsCheckedFlag[3] ? ((((byte)(RaceState.RACESTATE_FINISHED) == m_ConsoleUdp.m_sToHostBuf[3].nRev1) || ((byte)(RaceState.RACESTATE_DNF) == m_ConsoleUdp.m_sToHostBuf[2].nRev1))&&((byte)(DOF_state.dof_working) == m_ConsoleUdp.m_sToHostBuf[3].nDOFStatus)) : true)
                )
            {
                m_RaceFinishedFlag = true;
                m_ActiveAutoEnd = true;
            }
            else if((Pf_IsCheckedFlag[0] || Pf_IsCheckedFlag[1] || Pf_IsCheckedFlag[2] || Pf_IsCheckedFlag[3])
                && (true == m_RaceFinishedFlag)
                && (Pf_IsCheckedFlag[0] ? (((byte)(RaceState.RACESTATE_FINISHED) != m_ConsoleUdp.m_sToHostBuf[0].nRev1)&&((byte)(RaceState.RACESTATE_DNF) != m_ConsoleUdp.m_sToHostBuf[0].nRev1)) : true)
                && (Pf_IsCheckedFlag[1] ? (((byte)(RaceState.RACESTATE_FINISHED) != m_ConsoleUdp.m_sToHostBuf[1].nRev1) && ((byte)(RaceState.RACESTATE_DNF) != m_ConsoleUdp.m_sToHostBuf[1].nRev1)) : true)
                && (Pf_IsCheckedFlag[2] ? (((byte)(RaceState.RACESTATE_FINISHED) != m_ConsoleUdp.m_sToHostBuf[2].nRev1) && ((byte)(RaceState.RACESTATE_DNF) != m_ConsoleUdp.m_sToHostBuf[2].nRev1)) : true)
                && (Pf_IsCheckedFlag[3] ? (((byte)(RaceState.RACESTATE_FINISHED) != m_ConsoleUdp.m_sToHostBuf[3].nRev1) && ((byte)(RaceState.RACESTATE_DNF) != m_ConsoleUdp.m_sToHostBuf[3].nRev1)) : true)
                )
            {
                m_RaceFinishedFlag = false;
            }
        }

        #region //硬件指示灯
        void HardwareErrorCheckAndErrorLightCtrl()
        {
            if (
                //任意设备报错或网络连接失败
                (Brushes.Red == PF0State.Background)
                || (Brushes.Red == PF1State.Background)
                || (Brushes.Red == PF2State.Background)
                || (Brushes.Red == PF3State.Background)
                //任意滑梯报错
                || (LadderStatus.ERROR == m_LadderCurStatus[0])
                || (LadderStatus.ERROR == m_LadderCurStatus[1])
                || (LadderStatus.ERROR == m_LadderCurStatus[2])
                || (LadderStatus.ERROR == m_LadderCurStatus[3])
                || (LadderStatus.ERROR == m_LadderCurStatus[4])
                )
            {
                SetErrorLight(LightState.ON);
            }
            else
            {
                SetErrorLight(LightState.OFF);
            }
        }
        /// <summary>
        /// 设置错误指示灯状态
        /// </summary>
        /// <param name="lightState"></param>
        void SetErrorLight(LightState lightState)
        {
            if ((LightState.ON == lightState) && (0 == ((m_ConsoleUdp.m_DataFromPlc[4] >> 0) & 0x01)))
            {
                m_ConsoleUdp.m_DataToPlc[0] |= 0x01 << 0;
            }
            else if ((LightState.OFF == lightState) && (1 == ((m_ConsoleUdp.m_DataFromPlc[4] >> 0) & 0x01)))
            {
                m_ConsoleUdp.m_DataToPlc[0] &= (0x01 << 0) ^ 0xff;
            }
            m_ConsoleUdp.SendDataToPlc(m_ConsoleUdp.m_DataToPlc, m_ConsoleUdp.m_DataToPlc.Length, m_ConsoleUdp.m_PlcIpEndpoint);
        }
        /// <summary>
        /// 设置运行指示灯状态
        /// </summary>
        /// <param name="lightState"></param>
        void SetRunLight(LightState lightState)
        {
            if ((LightState.ON == lightState) && (0 == ((m_ConsoleUdp.m_DataFromPlc[4] >> 1) & 0x01)))
            {
                m_ConsoleUdp.m_DataToPlc[0] |= 0x01 << 1;   //运行灯开
                m_ConsoleUdp.m_DataToPlc[0] &= (0x01 << 2) ^ 0xff;  //停止灯关
                m_ConsoleUdp.m_DataToPlc[0] &= (0x01 << 4) ^ 0xff;  //待灯灯关
                m_ConsoleUdp.m_DataToPlc[0] &= (0x01 << 3) ^ 0xff;  //复位灯关

                BtnReset.Background = Brushes.White;

                BtnStart.Background = Brushes.Green;
                BtnEnd.Background = Brushes.White;
            }
            else if ((LightState.OFF == lightState) && (1 == ((m_ConsoleUdp.m_DataFromPlc[4] >> 1) & 0x01)))
            {
                m_ConsoleUdp.m_DataToPlc[0] &= (0x01 << 1) ^ 0xff;
                BtnStart.Background = Brushes.White;
            }
            m_ConsoleUdp.SendDataToPlc(m_ConsoleUdp.m_DataToPlc, m_ConsoleUdp.m_DataToPlc.Length, m_ConsoleUdp.m_PlcIpEndpoint);
        }
        /// <summary>
        /// 设置停止指示灯
        /// </summary>
        /// <param name="lightState"></param>
        void SetStopLight(LightState lightState)
        {
            if ((LightState.ON == lightState) && (0 == ((m_ConsoleUdp.m_DataFromPlc[4] >> 2) & 0x01)))
            {
                m_ConsoleUdp.m_DataToPlc[0] |= 0x01 << 2;   //停止灯开
                m_ConsoleUdp.m_DataToPlc[0] &= (0x01 << 1) ^ 0xff;  //运行灯关
                m_ConsoleUdp.m_DataToPlc[0] &= (0x01 << 3) ^ 0xff;  //复位灯关

                BtnReset.Background = Brushes.White;

                BtnStart.Background = Brushes.White;
                BtnEnd.Background = Brushes.Red;
            }
            else if ((LightState.OFF == lightState) && (1 == ((m_ConsoleUdp.m_DataFromPlc[4] >> 2) & 0x01)))
            {
                m_ConsoleUdp.m_DataToPlc[0] &= (0x01 << 2) ^ 0xff;
                BtnEnd.Background = Brushes.White;
            }
            m_ConsoleUdp.SendDataToPlc(m_ConsoleUdp.m_DataToPlc, m_ConsoleUdp.m_DataToPlc.Length, m_ConsoleUdp.m_PlcIpEndpoint);
        }
        /// <summary>
        /// 设置复位指示灯
        /// </summary>
        /// <param name="lightState"></param>
        void SetResetLight(LightState lightState)
        {
            if ((LightState.ON == lightState) && (0 == ((m_ConsoleUdp.m_DataFromPlc[4] >> 3) & 0x01)))
            {
                m_ConsoleUdp.m_DataToPlc[0] |= 0x01 << 3;
                m_ConsoleUdp.m_DataToPlc[0] &= (0x01 << 1) ^ 0xff;  //运行灯关
                m_ConsoleUdp.m_DataToPlc[0] &= (0x01 << 2) ^ 0xff;  //停止灯关
                m_ConsoleUdp.m_DataToPlc[0] &= (0x01 << 4) ^ 0xff; //关待客指示灯

                BtnReset.Background = Brushes.Yellow;

                BtnStart.Background = Brushes.White;
                BtnEnd.Background = Brushes.White;
            }
            else if ((LightState.OFF == lightState) && (1 == ((m_ConsoleUdp.m_DataFromPlc[4] >> 3) & 0x01)))
            {
                m_ConsoleUdp.m_DataToPlc[0] &= (0x01 << 3) ^ 0xff;
                BtnReset.Background = Brushes.White;
            }
            m_ConsoleUdp.SendDataToPlc(m_ConsoleUdp.m_DataToPlc, m_ConsoleUdp.m_DataToPlc.Length, m_ConsoleUdp.m_PlcIpEndpoint);
        }
        /// <summary>
        /// 设置待客指示灯
        /// </summary>
        /// <param name="lightState"></param>
        void SetWaitLight(LightState lightState)
        {
            if ((LightState.ON == lightState) && (0 == ((m_ConsoleUdp.m_DataFromPlc[4] >> 4) & 0x01)))
            {
                m_ConsoleUdp.m_DataToPlc[0] |= 0x01 << 4;           //待客灯亮
                m_ConsoleUdp.m_DataToPlc[0] &= (0x01 << 1) ^ 0xff;  //运行灯关
                m_ConsoleUdp.m_DataToPlc[0] &= (0x01 << 2) ^ 0xff;  //停止灯关
                m_ConsoleUdp.m_DataToPlc[0] &= (0x01 << 3) ^ 0xff;  //复位灯关

                BtnReset.Background = Brushes.White;

                BtnStart.Background = Brushes.White;
                BtnEnd.Background = Brushes.White;
            }
            else if ((LightState.OFF == lightState) && (1 == ((m_ConsoleUdp.m_DataFromPlc[4] >> 4) & 0x01)))
            {
                m_ConsoleUdp.m_DataToPlc[0] &= (0x01 << 4) ^ 0xff;
            }
            m_ConsoleUdp.SendDataToPlc(m_ConsoleUdp.m_DataToPlc, m_ConsoleUdp.m_DataToPlc.Length, m_ConsoleUdp.m_PlcIpEndpoint);
        }

        #endregion
        #region //设备与PLC相关的按钮使能状态；如果PLC网络断开需要将所有按钮使能关掉
        /// <summary>
        /// 设备与PLC相关的按钮使能状态；如果PLC网络断开需要将所有按钮使能关掉
        /// </summary>
        /// <param name="state"></param>
        private void SetBtnEnableStateRelatedToPlc(bool state)
        {
            //主操作按钮
            BtnStart.IsHitTestVisible = false;
            BtnEnd.IsHitTestVisible = false;
            BtnReset.IsHitTestVisible = false;
            //车门
            for (byte i = 0; i < MtUdp.DeviceAmount; i++)
            {
                SetCarLockBtnEnableState(i, state);
            }
            for (byte i = 0; i < LADDER_AMOUNT; i++)
            {
                SetLadderBtnEnableState(i, state);
            }
        }
        #endregion
        #region // 当PLC断开网络连接时，设置相关状态显示信息
        /// <summary>
        /// 当PLC断开网络连接时，设置相关状态显示信息
        /// </summary>
        private void SetLabelContentWhenPlcDisconnect()
        {
            //车门
            for (byte i = 0; i < MtUdp.DeviceAmount; i++)
            {
                SetCarDoorLabelDisplay(i, CarDoorStatus.UNKNOWN);
            }
            for (byte i = 0; i < LADDER_AMOUNT; i++)
            {
                SetLadderLabel(i, LadderStatus.UNKNOWN);
            }
        }
        #endregion
        #region // 设备车门状态显示信息
        /// <summary>
        /// 设备车门状态显示信息
        /// </summary>
        /// <param name="index"></param>
        /// <param name="carDoorStatus"></param>
        private void SetCarDoorLabelDisplay(byte index, CarDoorStatus carDoorStatus)
        {
            ((Label)FindName("Car" + (index + 1) + "DoorState")).Content = m_CarDoorLabelDisplayContent[(int)carDoorStatus];
            ((Label)FindName("Car" + (index + 1) + "DoorState")).Background = Brushes.Red;
        }
        #endregion
        #region // 设备车门锁按钮是否可以被单击控制
        /// <summary>
        /// 设备车门锁按钮是否可以被单击控制
        /// </summary>
        /// <param name="index"></param>
        /// <param name="state"></param>
        private void SetCarLockBtnEnableState(byte index, bool state)
        {
            ((Button)FindName("BtnNum" + (index + 1) + "CarDoorControl")).IsEnabled = state;
        } 
        #endregion
        #region //设置车门控制按钮状态
        private void SetCarDoorBtnDisplayContent(byte index, CarDoorLockStatus carDoorLockStatus)
        {
            if (CarDoorLockStatus.LOCKED == carDoorLockStatus)
            {
                ((Button)FindName("BtnNum" + (index + 1) + "CarDoorControl")).Content = m_CarDoorLabelDisplayContent[0];
            }
            else
            {
                ((Button)FindName("BtnNum" + (index + 1) + "CarDoorControl")).Content = m_CarDoorLabelDisplayContent[1];
            }
        }
        #endregion
        #region //车门控制按钮处理函数
        private void CarDoorsBtnHandler()
        {
            if ((DoorCtrlBtnStatus.SETIDLE != m_DoorCtrlBtnStatus[0])
                || (DoorCtrlBtnStatus.SETIDLE != m_DoorCtrlBtnStatus[1])
                || (DoorCtrlBtnStatus.SETIDLE != m_DoorCtrlBtnStatus[2])
                || (DoorCtrlBtnStatus.SETIDLE != m_DoorCtrlBtnStatus[3])
                )
            {
                for (byte i = 0; i < MtUdp.DeviceAmount; i++)
                {
                    if (DoorCtrlBtnStatus.SETLOCKED == m_DoorCtrlBtnStatus[i])
                    {
                        if ((m_CarDoorCurLockStatus[i] != CarDoorLockStatus.LOCKED) && (CarDoorLockStatus.LOCKED == SetCarDoorLocked(i, m_ConsoleUdp.m_DataToPlc)))
                        {
                            m_CarDoorCurLockStatus[i] = CarDoorLockStatus.LOCKED;
                            //SetCarDoorBtnDisplayContent(i, m_CarDoorCurLockStatus[i]);
                            m_DoorCtrlBtnStatus[i] = DoorCtrlBtnStatus.SETIDLE;
                        }

                    }
                    else if (DoorCtrlBtnStatus.SETUNLOCKED == m_DoorCtrlBtnStatus[i])
                    {
                        if ((m_CarDoorCurLockStatus[i] != CarDoorLockStatus.UNLOCKED) && (CarDoorLockStatus.UNLOCKED == SetCarDoorUnlocked(i, m_ConsoleUdp.m_DataToPlc)))
                        {
                            m_CarDoorCurLockStatus[i] = CarDoorLockStatus.UNLOCKED;
                            //SetCarDoorBtnDisplayContent(i, m_CarDoorCurLockStatus[i]);
                            m_DoorCtrlBtnStatus[i] = DoorCtrlBtnStatus.SETIDLE;
                        }
                    }
                    else if (DoorCtrlBtnStatus.SET_CHANGE == m_DoorCtrlBtnStatus[i])
                    {
                        if (CarDoorLockStatus.UNLOCKED == SetCarDoorUnlocked(i, m_ConsoleUdp.m_DataToPlc))
                        {
                            if (CarDoorLockStatus.UNLOCKED == m_CarDoorCurLockStatus[i])
                            {
                                m_CarDoorCurLockStatus[i] = CarDoorLockStatus.LOCKED;
                            }
                            else if (CarDoorLockStatus.LOCKED == m_CarDoorCurLockStatus[i])
                            {
                                m_CarDoorCurLockStatus[i] = CarDoorLockStatus.UNLOCKED;
                            }
                            m_DoorCtrlBtnStatus[i] = DoorCtrlBtnStatus.SETIDLE;
                        }
                    }
                }
                m_ConsoleUdp.SendDataToPlc(m_ConsoleUdp.m_DataToPlc, m_ConsoleUdp.m_DataToPlc.Length, m_ConsoleUdp.m_PlcIpEndpoint);
            }

        }
        #endregion
        #region // 车门相关UI更新
        /// <summary>
        /// 车门相关UI更新
        /// </summary>
        private void CarDoorUiUpdate()
        {
            for (byte i = 0; i < MtUdp.DeviceAmount; i++)
            {
                if (CarDoorStatus.CLOSED == GetCarDoorStatus(i))
                {
                    ((Label)FindName("Car" + (i + 1) + "DoorState")).Content = m_CarDoorLabelDisplayContent[1];
                    ((Label)FindName("Car" + (i + 1) + "DoorState")).Background = Brushes.White;
                    ((Button)FindName("BtnNum" + (i + 1) + "CarDoorControl")).IsEnabled = true;
                }
                else if (CarDoorStatus.OPENED == GetCarDoorStatus(i))
                {
                    ((Label)FindName("Car" + (i + 1) + "DoorState")).Content = m_CarDoorLabelDisplayContent[0];
                    ((Label)FindName("Car" + (i + 1) + "DoorState")).Background = Brushes.Yellow;
                    ((Button)FindName("BtnNum" + (i + 1) + "CarDoorControl")).IsEnabled = false;
                }
                WriteCarDoorStateToLog(i, ((Label)FindName("Car" + (i + 1) + "DoorState")).Content.ToString());
            }
        } 
        #endregion
        #region //车门状态获取
        private CarDoorStatus GetCarDoorStatus(byte index)
        {
            switch (index)
            {
                case 0:
                    if ((0 == ((m_ConsoleUdp.m_DataFromPlc[2] >> 0) & 0x01)) || (true == adminLoginWindow.CarDoorShieldCheckFlag[0]))
                    {
                        return CarDoorStatus.CLOSED;
                    }
                    else
                    {
                        return CarDoorStatus.OPENED;
                    }
                case 1:
                    if ((0 == ((m_ConsoleUdp.m_DataFromPlc[2] >> 1) & 0x01)) || (true == adminLoginWindow.CarDoorShieldCheckFlag[1]))
                    {
                        return CarDoorStatus.CLOSED;
                    }
                    else
                    {
                        return CarDoorStatus.OPENED;
                    }
                case 2:
                    if ((0 == ((m_ConsoleUdp.m_DataFromPlc[2] >> 2) & 0x01)) || (true == adminLoginWindow.CarDoorShieldCheckFlag[2]))
                    {
                        return CarDoorStatus.CLOSED;
                    }
                    else
                    {
                        return CarDoorStatus.OPENED;
                    }
                case 3:
                    if ((0 == ((m_ConsoleUdp.m_DataFromPlc[2] >> 3) & 0x01)) || (true == adminLoginWindow.CarDoorShieldCheckFlag[3]))
                    {
                        return CarDoorStatus.CLOSED;
                    }
                    else
                    {
                        return CarDoorStatus.OPENED;
                    }
            }
            return CarDoorStatus.CLOSED;
        }
        #endregion
        #region //车门锁定
        const UInt32 DoorLockDelayMaxCounter = 50;
        UInt32[] DoorLockDelayCounter = new UInt32[4];
        private CarDoorLockStatus SetCarDoorLocked(byte index, byte[] status)
        {
            switch (index)
            {
                case 0:
                    if (DoorLockDelayCounter[index] <= DoorLockDelayMaxCounter)
                    {
                        status[2] |= 0x01 << 0;
                        DoorLockDelayCounter[index]++;
                        return CarDoorLockStatus.ACTIONING;
                    }
                    else
                    {
                        status[2] &= (0x01 << 0) ^ 0xFF;
                        DoorLockDelayCounter[index] = 0;
                        return CarDoorLockStatus.LOCKED;

                    }
                case 1:
                    if (DoorLockDelayCounter[index] <= DoorLockDelayMaxCounter)
                    {
                        status[2] |= 0x01 << 1;
                        DoorLockDelayCounter[index]++;
                        return CarDoorLockStatus.ACTIONING;
                    }
                    else
                    {
                        status[2] &= (0x01 << 1) ^ 0xFF;
                        DoorLockDelayCounter[index] = 0;
                        return CarDoorLockStatus.LOCKED;
                    }
                case 2:
                    if (DoorLockDelayCounter[index] <= DoorLockDelayMaxCounter)
                    {
                        status[2] |= 0x01 << 2;
                        DoorLockDelayCounter[index]++;
                        return CarDoorLockStatus.ACTIONING;
                    }
                    else
                    {
                        status[2] &= (0x01 << 2) ^ 0xFF;
                        DoorLockDelayCounter[index] = 0;
                        return CarDoorLockStatus.LOCKED;
                    }
                case 3:
                    if (DoorLockDelayCounter[index] <= DoorLockDelayMaxCounter)
                    {
                        status[2] |= 0x01 << 3;
                        DoorLockDelayCounter[index]++;
                        return CarDoorLockStatus.ACTIONING;
                    }
                    else
                    {
                        status[2] &= (0x01 << 3) ^ 0xFF;
                        DoorLockDelayCounter[index] = 0;
                        return CarDoorLockStatus.LOCKED;
                    }
            }
            return CarDoorLockStatus.ACTIONING;
        }
        #endregion
        #region //车门打开
        const UInt32 DoorUnlockDelayMaxCounter = 50;
        UInt32[] DoorUnlockDelayCounter = new UInt32[4];
        private CarDoorLockStatus SetCarDoorUnlocked(byte index, byte[] status)
        {
            switch (index)
            {
                case 0:
                    if (DoorUnlockDelayCounter[index] <= DoorUnlockDelayMaxCounter)
                    {
                        status[2] |= 0x01 << 0;
                        DoorUnlockDelayCounter[index]++;
                        return CarDoorLockStatus.ACTIONING;
                    }
                    else
                    {
                        status[2] &= (0x01 << 0) ^ 0xFF;
                        DoorUnlockDelayCounter[index] = 0;
                        return CarDoorLockStatus.UNLOCKED;
                    }
                case 1:
                    if (DoorUnlockDelayCounter[index] <= DoorUnlockDelayMaxCounter)
                    {
                        DoorUnlockDelayCounter[index]++;
                        status[2] |= 0x01 << 1;
                        return CarDoorLockStatus.ACTIONING;
                    }
                    else
                    {
                        status[2] &= (0x01 << 1) ^ 0xFF;
                        DoorUnlockDelayCounter[index] = 0;
                        return CarDoorLockStatus.UNLOCKED;
                    }
                case 2:
                    if (DoorUnlockDelayCounter[index] <= DoorUnlockDelayMaxCounter)
                    {
                        DoorUnlockDelayCounter[index]++;
                        status[2] |= 0x01 << 2;
                        return CarDoorLockStatus.ACTIONING;
                    }
                    else
                    {
                        status[2] &= (0x01 << 2) ^ 0xFF;
                        DoorUnlockDelayCounter[index] = 0;
                        return CarDoorLockStatus.UNLOCKED;
                    }
                case 3:
                    if (DoorUnlockDelayCounter[index] <= DoorUnlockDelayMaxCounter)
                    {
                        DoorUnlockDelayCounter[index]++;
                        status[2] |= 0x01 << 3;
                        return CarDoorLockStatus.ACTIONING;
                    }
                    else
                    {
                        status[2] &= (0x01 << 3) ^ 0xFF;
                        DoorUnlockDelayCounter[index] = 0;
                        return CarDoorLockStatus.UNLOCKED;
                    }
            }
            return CarDoorLockStatus.ACTIONING;
        }
        #endregion

        #region //获取楼梯当前状态
        LadderStatus GetLadderStatus(byte index, byte[] allStatus)
        {
            switch (index)
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
                    else if ((1 != ((allStatus[0] >> 4) & 0x01)) && (1 != ((allStatus[0] >> 5) & 0x01))
                        && (LadderAwayDelayCounter[index] < LadderAwayMaxDelayCounter)
                        && (LadderClosedDelayCounter[index] < LadderClosedMaxDelayCounter)
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
                        && (LadderAwayDelayCounter[index] < LadderAwayMaxDelayCounter)
                        && (LadderClosedDelayCounter[index] < LadderClosedMaxDelayCounter)
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
                        && (LadderAwayDelayCounter[index] < LadderAwayMaxDelayCounter)
                        && (LadderClosedDelayCounter[index] < LadderClosedMaxDelayCounter)
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
                        && (LadderAwayDelayCounter[index] < LadderAwayMaxDelayCounter)
                        && (LadderClosedDelayCounter[index] < LadderClosedMaxDelayCounter)
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
                        && (LadderAwayDelayCounter[index] < LadderAwayMaxDelayCounter)
                        && (LadderClosedDelayCounter[index] < LadderClosedMaxDelayCounter)
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
        #region //设置楼梯状态显示
        /// <summary>
        /// 设置楼梯状态显示
        /// </summary>
        /// <param name="index"></param>
        /// <param name="ldderStatus"></param>
        private void SetLadderLabel(byte index, LadderStatus ldderStatus)
        {
            if (LadderStatus.CLOSED == ldderStatus)
            {
                ((Label)FindName("Ladder" + (index + 1) + "StatusDisplay")).Background = Brushes.Yellow;
                ClearLadderClosedDelayCounter(index);
                ClearLadderAwayDelayCounter(index);
            }
            else if (LadderStatus.AWAY == ldderStatus)
            {
                ((Label)FindName("Ladder" + (index + 1) + "StatusDisplay")).Background = Brushes.White;
                ClearLadderClosedDelayCounter(index);
                ClearLadderAwayDelayCounter(index);
            }
            else if (LadderStatus.MOVING == ldderStatus)
            {
                ((Label)FindName("Ladder" + (index + 1) + "StatusDisplay")).Background = Brushes.Green;
            }
            else if (LadderStatus.ERROR == ldderStatus)
            {
                ((Label)FindName("Ladder" + (index + 1) + "StatusDisplay")).Background = Brushes.Red;
            }
            else if (LadderStatus.UNKNOWN == ldderStatus)
            {
                ((Label)FindName("Ladder" + (index + 1) + "StatusDisplay")).Background = Brushes.Red;
            }
            ((Label)FindName("Ladder" + (index + 1) + "StatusDisplay")).Content = m_LadderStatusContent[(int)ldderStatus];
        }
        #endregion
        #region //设置楼梯控制按钮能否被单击
        /// <summary>
        /// 设置楼梯控制按钮能否被单击
        /// </summary>
        /// <param name="index"></param>
        /// <param name="state"></param>
        private void SetLadderBtnEnableState(byte index, bool state)
        {
            ((Button)FindName("BtnNum" + (index + 1) + "LadderControl")).IsEnabled = state;
        }
        #endregion
        #region //根据楼梯状态，确定楼梯按钮显示
        /// <summary>
        /// 设置滑梯按钮相关
        /// </summary>
        /// <param name="index"></param>
        /// <param name="ladderStatus"></param>
        private void SetLadderBtn(byte index, LadderStatus ladderStatus)
        {
            if (LadderStatus.MOVING == ladderStatus)
            {
                //((Button)FindName("BtnNum" + (index + 1) + "LadderControl")).Content = m_LadderStatusContent[(int)ladderStatus];
                //((Button)FindName("BtnNum" + (index + 1) + "LadderControl")).IsEnabled = false;
            }
            else
            {
                ((Button)FindName("BtnNum" + (index + 1) + "LadderControl")).IsEnabled = true;
                if (LadderStatus.CLOSED == ladderStatus)
                {
                    ((Button)FindName("BtnNum" + (index + 1) + "LadderControl")).Content = m_LadderStatusContent[1];
                }
                else if (LadderStatus.AWAY == ladderStatus)
                {
                    ((Button)FindName("BtnNum" + (index + 1) + "LadderControl")).Content = m_LadderStatusContent[0];
                }
            }
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
        private const UInt32 LadderAwayMaxDelayCounter = 1500;
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
                        //MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        //MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        //MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        //MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        //MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        //MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        //MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        //MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        //MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        //MotusMessageBox(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        #region //获取所有楼梯状态并设置按钮状态
        void LadderStateAndBtnStateUpdate()
        {
            for (byte i = 0; i < LADDER_AMOUNT; i++)
            {
                m_LadderCurStatus[i] = GetLadderStatus(i, m_ConsoleUdp.m_DataFromPlc);
                //更新UI中Ladder状态
                SetLadderLabel(i, m_LadderCurStatus[i]);
                SetLadderBtn(i, m_LadderCurStatus[i]);
            }
        }
        #endregion

        #region //初始化按钮事件
        private void InitOrWaitPassenger_Click(object sender, RoutedEventArgs e)
        {
            BtnInitOrWaitPassenger.Content = m_InitBtnContent[1];
            BtnInitOrWaitPassenger.IsHitTestVisible = false;
        }
        #endregion
        #region //判断强制启动主控按钮功能是否启用
        /// <summary>
        /// 判断是否启用强制启用按钮功能；
        /// </summary>
        void ForceActiveBtnHandle()
        {
            if (adminLoginWindow.ActiveMainWindowButton.Content.Equals(adminLoginWindow.ForceActiveBtnContent[1]))
            {
                BtnStart.IsHitTestVisible = true;
                BtnEnd.IsHitTestVisible = true;
            }
        } 
        #endregion
        #region //体验开始/结束按钮事件
        private void BtnStartOrEnd_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button).Content.Equals(BtnStartOrEndContent[0]))     //当前内容为“启动游戏体验”
            {
                WriteButtonEventContentToLog(BtnStartOrEndContent[0]);
                BtnEnd.Content = BtnStartOrEndContent[2];
                (sender as Button).Content = BtnStartOrEndContent[1];              //当前内容为“体验开始中”

            }
            else if ((sender as Button).Content.Equals(BtnStartOrEndContent[2])) //当前内容为“结束游戏体验”
            {
                WriteButtonEventContentToLog(BtnStartOrEndContent[2]);
                BtnStart.Content = BtnStartOrEndContent[0];
                (sender as Button).Content = BtnStartOrEndContent[3];            //当前内容为“体验结束中”
            }
            //EnableBtnStart(false);
            //if (true == PF_InitOverFlag)
            //{
            //    if (BtnStart.Content.Equals(BtnStartOrEndContent[0]))     //当前内容为“启动游戏体验”
            //    {
            //        BtnStart.Content = BtnStartOrEndContent[1];              //当前内容为“体验开始中”
            //    }
            //    else if (BtnEnd.Content.Equals(BtnStartOrEndContent[2])) //当前内容为“结束游戏体验”
            //    {
            //        BtnEnd.Content = BtnStartOrEndContent[3];            //当前内容为“体验结束中”
            //    }
            //    EnableBtnStart(false);
            //}
            //else
            //{
            //    MotusMessageBox("平台未初始化！\r\n请先点击初始化按钮！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            //}
            //if((sender as Button).Content.Equals(BtnStartOrEndContent[0]))     //当前内容为“启动游戏体验”
            //{
            //    (sender as Button).Content = BtnStartOrEndContent[1];              //当前内容为“体验开始中”
            //}
            //else if ((sender as Button).Content.Equals(BtnStartOrEndContent[2])) //当前内容为“结束游戏体验”
            //{
            //    (sender as Button).Content = BtnStartOrEndContent[3];            //当前内容为“体验结束中”
            //}
            //EnableBtnStart(false);//BtnStart.IsEnabled = false;
        }
        #endregion
        #region //开始按钮使能控制函数
        /// <summary>
        /// 开始按钮使能控制
        /// </summary>
        /// <param name="state"></param>
        void EnableBtnStart(bool state)
        {
            if (adminLoginWindow.ActiveMainWindowButton.Content.Equals(adminLoginWindow.ForceActiveBtnContent[0]))
            {
                BtnStart.IsHitTestVisible = state;
                //if(true==state)
                //{
                //    BtnEnd.IsHitTestVisible = false;
                //}
            }
            else
            {
                BtnStart.IsHitTestVisible = true;
            }
        }
        #endregion
        #region //结束按钮使能控制函数
        /// <summary>
        /// 结束按钮使能控制
        /// </summary>
        /// <param name="state"></param>
        void EnableBtnEnd(bool state)
        {
            if (adminLoginWindow.ActiveMainWindowButton.Content.Equals(adminLoginWindow.ForceActiveBtnContent[0]))
            {
                BtnEnd.IsHitTestVisible = state;
                if (true == state)
                {
                    BtnStart.IsHitTestVisible = false;
                }
            }
            else
            {
                BtnEnd.IsHitTestVisible = true;
            }
        } 
        #endregion
        #region //管理员模式按钮事件
        private void AdminMode_Click(object sender, RoutedEventArgs e)
        {
            //adminLoginWindow.ShowDialog();
            adminLoginWindow.Show();
        }
        #endregion
        #region //车门控制按钮事件
        #region //1号车门控制按钮事件
        private void BtnNum1CarDoorControl_Click(object sender, RoutedEventArgs e)
        {
            m_DoorCtrlBtnStatus[0] = DoorCtrlBtnStatus.SET_CHANGE; 
            //if (BtnNum1CarDoorControl.Content.Equals(m_CarDoorLabelDisplayContent[0]))
            //{
            //    m_DoorCtrlBtnStatus[0] = DoorCtrlBtnStatus.SETUNLOCKED;
            //    BtnNum1CarDoorControl.Content = m_CarDoorLabelDisplayContent[1];
            //}
            //else
            //{
            //    m_DoorCtrlBtnStatus[0] = DoorCtrlBtnStatus.SETLOCKED;
            //    BtnNum1CarDoorControl.Content = m_CarDoorLabelDisplayContent[0];
            //}
        }
        #endregion

        #region //2号车门控制按钮事件
        private void BtnNum2CarDoorControl_Click(object sender, RoutedEventArgs e)
        {
            m_DoorCtrlBtnStatus[1] = DoorCtrlBtnStatus.SET_CHANGE;
            //if (BtnNum2CarDoorControl.Content.Equals(m_CarDoorLabelDisplayContent[0]))
            //{
            //    m_DoorCtrlBtnStatus[1] = DoorCtrlBtnStatus.SETUNLOCKED;
            //    BtnNum2CarDoorControl.Content = m_CarDoorLabelDisplayContent[1];
            //}
            //else
            //{
            //    m_DoorCtrlBtnStatus[1] = DoorCtrlBtnStatus.SETLOCKED;
            //    BtnNum2CarDoorControl.Content = m_CarDoorLabelDisplayContent[0];
            //}
        }
        #endregion

        #region //3号车门控制按钮事件
        private void BtnNum3CarDoorControl_Click(object sender, RoutedEventArgs e)
        {
            m_DoorCtrlBtnStatus[1] = DoorCtrlBtnStatus.SET_CHANGE;
            //if (BtnNum3CarDoorControl.Content.Equals(m_CarDoorLabelDisplayContent[0]))
            //{
            //    m_DoorCtrlBtnStatus[2] = DoorCtrlBtnStatus.SETUNLOCKED;
            //    BtnNum3CarDoorControl.Content = m_CarDoorLabelDisplayContent[1];
            //}
            //else
            //{
            //    m_DoorCtrlBtnStatus[2] = DoorCtrlBtnStatus.SETLOCKED;
            //    BtnNum3CarDoorControl.Content = m_CarDoorLabelDisplayContent[0];
            //}
        }
        #endregion

        #region //4号车门控制按钮事件
        private void BtnNum4CarDoorControl_Click(object sender, RoutedEventArgs e)
        {
            m_DoorCtrlBtnStatus[3] = DoorCtrlBtnStatus.SET_CHANGE;
            //if (BtnNum4CarDoorControl.Content.Equals(m_CarDoorLabelDisplayContent[0]))
            //{
            //    m_DoorCtrlBtnStatus[3] = DoorCtrlBtnStatus.SETUNLOCKED;
            //    BtnNum4CarDoorControl.Content = m_CarDoorLabelDisplayContent[1];
            //}
            //else
            //{
            //    m_DoorCtrlBtnStatus[3] = DoorCtrlBtnStatus.SETLOCKED;
            //    BtnNum4CarDoorControl.Content = m_CarDoorLabelDisplayContent[0];
            //}
        }
        #endregion
        #endregion
        #region //楼梯控制按钮事件
        private void BtnNum1LadderControl_Click(object sender, RoutedEventArgs e)
        {
            if (BtnNum1LadderControl.Content.Equals(m_LadderStatusContent[0]))  //当前按钮显示内容为“靠近”平台时
            {
                WriteEventContentToLog("单击1号滑梯按钮："+m_LadderStatusContent[0]);
                m_LadderCtrlBtnStatus[0] = LadderCtrlBtnStatus.SETCLOSE;
                BtnNum1LadderControl.Content = m_LadderStatusContent[1];
                ClearLadderClosedDelayCounter(0);
            }
            else
            {
                WriteEventContentToLog("单击1号滑梯按钮：" + m_LadderStatusContent[1]);
                m_LadderCtrlBtnStatus[0] = LadderCtrlBtnStatus.SETAWAY;
                BtnNum1LadderControl.Content = m_LadderStatusContent[0];
                ClearLadderAwayDelayCounter(0);
            }
        }

        private void BtnNum2LadderControl_Click(object sender, RoutedEventArgs e)
        {
            if (BtnNum2LadderControl.Content.Equals(m_LadderStatusContent[0]))  //当前按钮显示内容为“靠近”平台时
            {
                WriteEventContentToLog("单击2号滑梯按钮：" + m_LadderStatusContent[0]);
                m_LadderCtrlBtnStatus[1] = LadderCtrlBtnStatus.SETCLOSE;
                BtnNum2LadderControl.Content = m_LadderStatusContent[1];
                ClearLadderClosedDelayCounter(1);
            }
            else
            {
                WriteEventContentToLog("单击2号滑梯按钮：" + m_LadderStatusContent[1]);
                m_LadderCtrlBtnStatus[1] = LadderCtrlBtnStatus.SETAWAY;
                BtnNum2LadderControl.Content = m_LadderStatusContent[0];
                ClearLadderAwayDelayCounter(1);
            }
        }

        private void BtnNum3LadderControl_Click(object sender, RoutedEventArgs e)
        {
            if (BtnNum3LadderControl.Content.Equals(m_LadderStatusContent[0]))  //当前按钮显示内容为“靠近”平台时
            {
                WriteEventContentToLog("单击3号滑梯按钮：" + m_LadderStatusContent[0]);
                m_LadderCtrlBtnStatus[2] = LadderCtrlBtnStatus.SETCLOSE;
                BtnNum3LadderControl.Content = m_LadderStatusContent[1];
                ClearLadderClosedDelayCounter(2);
            }
            else
            {
                WriteEventContentToLog("单击3号滑梯按钮：" + m_LadderStatusContent[1]);
                m_LadderCtrlBtnStatus[2] = LadderCtrlBtnStatus.SETAWAY;
                BtnNum3LadderControl.Content = m_LadderStatusContent[0];
                ClearLadderAwayDelayCounter(2);
            }
        }

        private void BtnNum4LadderControl_Click(object sender, RoutedEventArgs e)
        {
            if (BtnNum4LadderControl.Content.Equals(m_LadderStatusContent[0]))  //当前按钮显示内容为“靠近”平台时
            {
                WriteEventContentToLog("单击4号滑梯按钮：" + m_LadderStatusContent[0]);
                m_LadderCtrlBtnStatus[3] = LadderCtrlBtnStatus.SETCLOSE;
                BtnNum4LadderControl.Content = m_LadderStatusContent[1];
                ClearLadderClosedDelayCounter(3);
            }
            else
            {
                WriteEventContentToLog("单击4号滑梯按钮：" + m_LadderStatusContent[1]);
                m_LadderCtrlBtnStatus[3] = LadderCtrlBtnStatus.SETAWAY;
                BtnNum4LadderControl.Content = m_LadderStatusContent[0];
                ClearLadderAwayDelayCounter(3);
            }
        }

        private void BtnNum5LadderControl_Click(object sender, RoutedEventArgs e)
        {
            if (BtnNum5LadderControl.Content.Equals(m_LadderStatusContent[0]))  //当前按钮显示内容为“靠近”平台时
            {
                WriteEventContentToLog("单击5号滑梯按钮：" + m_LadderStatusContent[0]);
                m_LadderCtrlBtnStatus[4] = LadderCtrlBtnStatus.SETCLOSE;
                BtnNum5LadderControl.Content = m_LadderStatusContent[1];
                ClearLadderClosedDelayCounter(4);
            }
            else
            {
                WriteEventContentToLog("单击5号滑梯按钮：" + m_LadderStatusContent[1]);
                m_LadderCtrlBtnStatus[4] = LadderCtrlBtnStatus.SETAWAY;
                BtnNum5LadderControl.Content = m_LadderStatusContent[0];
                ClearLadderAwayDelayCounter(4);
            }
        }
        #endregion
        #region //投影仪控制按钮单击事件
        /// <summary>
        /// 投影仪控制按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnPJControl_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button).Content.Equals(m_PJControlButtonContent[0]))
            {
                (sender as Button).Content = m_PJControlButtonContent[1];
                for (byte i = 0; i < MtUdp.DeviceAmount; i++)
                {
                    for (byte j = 0; j < 2; j++)
                    {
                        if (true == (FindName("Dev" + (i + 1) + "Projector" + (j + 1)) as CheckBox).IsChecked)
                        {
                            pjLinkControl.ProjectorPower(i, j, POWER_STATE.POWER_OFF);
                        }
                    }
                }
            }
            else if ((sender as Button).Content.Equals(m_PJControlButtonContent[1]))
            {
                (sender as Button).Content = m_PJControlButtonContent[0];
                for (byte i = 0; i < MtUdp.DeviceAmount; i++)
                {
                    for (byte j = 0; j < 2; j++)
                    {
                        if (true == (FindName("Dev" + (i + 1) + "Projector" + (j + 1)) as CheckBox).IsChecked)
                        {
                            pjLinkControl.ProjectorPower(i, j, POWER_STATE.POWER_OFF);
                        }
                    }
                }
            }
        } 
        #endregion
        #region //游戏启动按钮（测试使用）
        /// <summary>
        /// 根据判断结果确定是否激活游戏启动按钮；（测试使用）
        /// </summary>
        private void ActiveGameStartupBtn()
        {
            //为正版ProjectCarII测试使用的按钮，此程序已经取消游戏启动按钮
            if (true == adminLoginWindow.GameStartUpBtnDisplayFlag)
            {
                BtnStartUpPF1Game.Visibility = Visibility.Visible;
                BtnStartUpPF2Game.Visibility = Visibility.Visible;
                BtnStartUpPF3Game.Visibility = Visibility.Visible;
                BtnStartUpPF4Game.Visibility = Visibility.Visible;
                adminLoginWindow.GameStartUpBtnDisplayFlag = false;
            }
        }
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
        #region //复位按钮响应函数
        /// <summary>
        /// 复位按钮响应函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button).Content.Equals(BtnResetContent[0]))
            {
                WriteButtonEventContentToLog(BtnStartOrEndContent[0]);
                BtnStart.Content = BtnStartOrEndContent[0];
                BtnStart.IsHitTestVisible = false;
                BtnEnd.Content = BtnStartOrEndContent[2];
                BtnEnd.IsHitTestVisible = false;
                (sender as Button).Content = BtnResetContent[1];

            }
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

        

        /// <summary>
        /// 确保单击程序右上角X按钮时程序可以正常退出
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closed(object sender, EventArgs e)
        {
            if(MessageBoxResult.Yes==MotusMessageBox("是否结束运营？\r\n是，则关闭投影仪，滑梯远离，退出程序\r\n否，则仅退出程序", "Question", MessageBoxButton.YesNo, MessageBoxImage.Question))
            {
                OperateEndHandle();
            }
            //m_LogWriter.Close();
            Thread.Sleep(100);
            Application.Current.Shutdown();
        }
        /// <summary>
        /// UI界面中投影仪开关机操作按钮单击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProjectorControl_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton curButton = sender as ToggleButton;
            if (true == curButton.IsChecked)
            {
                curButton.Content = "关闭";
                CheckedProjectorOperate(POWER_STATE.POWER_ON);
            }
            else
            {
                curButton.Content = "打开";
                CheckedProjectorOperate(POWER_STATE.POWER_OFF);
            }
            
        }
        /// <summary>
        /// 根据UI界面中勾选的选项进行投影仪开关机操作；
        /// </summary>
        /// <param name="powerState">投影仪开关机状态</param>
        private void CheckedProjectorOperate(POWER_STATE powerState)
        {
            for (byte i = 0; i < MtUdp.DeviceAmount; i++)
            {
                for (byte j = 0; j < 2; j++)
                {
                    if (true == (FindName("Dev" + (i + 1) + "Projector" + (j + 1)) as CheckBox).IsChecked)
                    {
                        pjLinkControl.ProjectorPower(i, j, powerState);
                    }
                }
            }
        }
        /// <summary>
        /// 确保仅有一个程序进行，如果已有程序运行，将之前程序界面置于窗口最前；
        /// </summary>
        private void EnsureOnlyOneProgressRun()
        {
            Title = "motus";//临时使用，为下面将已经启动的进程置前做准备
            bool ret;
            Mutex mutex = new System.Threading.Mutex(true, "MOTUS", out ret);

            if (!ret)
            {
                MessageBox.Show("已有一个程序实例运行");
                SetForegroundWindow(FindWindow(null, "MOTUS"));
                Environment.Exit(0);
            }
            Title = "穆特科技（武汉）股份有限公司";
        }
        /// <summary>
        /// 清空相关数据；
        /// </summary>
        private void DataClearWhenEmergBtnPressed()
        {
            BtnStart.Content = BtnStartOrEndContent[0];
            BtnEnd.Content = BtnStartOrEndContent[2];
            EnableBtnEnd(false);
            EnableBtnStart(false);
            BtnReset.IsHitTestVisible = true;
            BtnInitOrWaitPassenger.IsHitTestVisible = false;
        }
        /// <summary>
        /// 恢复GroupBox边框默认颜色
        /// </summary>
        /// <param name="groupBox"></param>
        private void RecoveryGbBorderBrushToDefault(GroupBox groupBox)
        {
            //将GroupBox边框颜色恢复为默认值
            groupBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD5DFE5"));
        }
        /// <summary>
        /// 设置GroupBox边框颜色，具备互锁功能，设置一个颜色后其他的全恢复至默认；
        /// </summary>
        /// <param name="groupBox"></param>
        private void SetGbBorderColorToRed(GroupBox groupBox)
        {
            if(groupBox==GbPfsAndPlcStates)
            {
                groupBox.BorderBrush = Brushes.Red;
                RecoveryGbBorderBrushToDefault(GbDevicesSelect);
            }
            else if(groupBox == GbDevicesSelect)
            {
                groupBox.BorderBrush = Brushes.Red;
                RecoveryGbBorderBrushToDefault(GbPfsAndPlcStates);
            }
        }
        #region //出错弹窗
        private MessageBoxResult MotusMessageBox(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            MessageBoxResult mbRet;
            timer.Change(Timeout.Infinite, 10);
            //MessageBox.Show(index + 1 + "号楼梯运动超时，请检查设备！", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            WriteEventContentToLog(messageBoxText);
            mbRet = MessageBox.Show(messageBoxText, caption, button, icon);
            WriteEventContentToLog(mbRet.ToString());
            timer.Change(0, 10);
            return mbRet;
        }
        #endregion
        private void OperateEndHandle()
        {
            //关闭投影仪
            for (byte i = 0; i < MtUdp.DeviceAmount; i++)
            {
                for (byte j = 0; j < 2; j++)
                {
                    pjLinkControl.ProjectorPower(i, j, POWER_STATE.POWER_OFF);
                }
            }
            //滑梯远离
            for (byte i = 0; i < LadderControllerMounter; i++)
            {
                SetLadderAway(i, m_ConsoleUdp.m_DataToPlc);
                m_ConsoleUdp.SendDataToPlc(m_ConsoleUdp.m_DataToPlc, m_ConsoleUdp.m_DataToPlc.Length, m_ConsoleUdp.m_PlcIpEndpoint);
            }
            m_ConsoleUdp.SendDataToPlc(m_ConsoleUdp.m_DataToPlc, m_ConsoleUdp.m_DataToPlc.Length, m_ConsoleUdp.m_PlcIpEndpoint);
            Thread.Sleep(5000);
        }

        private void WriteEventContentToLog(string logContent)
        {
            if(m_LogLastEventContent.Equals(logContent))
            {

            }
            else
            {
                m_LogLastEventContent= logContent;
                m_LogWriter.WriteLine(DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss:  ") + logContent);
            }
        }
        private void WriteButtonEventContentToLog(string logContent)
        {
            if (m_LogLastButtonEventContent.Equals(logContent))
            {

            }
            else
            {
                m_LogLastButtonEventContent = logContent;
                m_LogWriter.WriteLine(DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss:  ") + "单击  "+logContent);
            }
        }
        private void WritePfErrorContentToLog(int index,string logContent)
        {
            if (m_LogLastPfErrorContent[index].Equals(logContent))
            {

            }
            else
            {
                m_LogLastPfErrorContent[index] = logContent;
                m_LogWriter.WriteLine(DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss:  ") + (index+1)+"号设备-"+logContent);
            }
        }

        private void WriteCarDoorStateToLog(int index, string logContent)
        {
            if (m_LogLastCarDoorState[index].Equals(logContent))
            {

            }
            else
            {
                m_LogLastCarDoorState[index] = logContent;
                m_LogWriter.WriteLine(DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss:  ") + (index + 1) + "号车门-" + logContent);
            }
        }
        private void WriteGameStateToLog(int index, string logContent)
        {
            if (m_LogLastGameState[index].Equals(logContent))
            {

            }
            else
            {
                m_LogLastGameState[index] = logContent;
                m_LogWriter.WriteLine(DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss:  ") + (index + 1) + "号游戏状态-" + logContent);
            }
        }

        private void CbPlatformsSelect_Click(object sender, RoutedEventArgs e)
        {
            WriteEventContentToLog((sender as CheckBox).Name +"  勾选状态："+ (sender as CheckBox).IsChecked.ToString());
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_QUERYENDSESSION)
            {
                OperateEndHandle();
            }
            return IntPtr.Zero;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //// 获得窗口句柄
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            //// 在Win32窗口中显示WPF的内容////接收窗口消息的处理程序实现（基于 System.Windows.Interop.HwndSourceHook 委托）
            HwndSource.FromHwnd(hwnd).AddHook(new HwndSourceHook(WndProc));
        }
    }
}
