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
namespace MainControl
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MtUdp m_ConsoleUdp = new MtUdp();
        private static Timer timer;
        public MainWindow()
        {
            InitializeComponent();
            //网络初始化
            m_ConsoleUdp.UdpInit(11000);
            //发送数据多媒体定时器
            // Create an AutoResetEvent to signal the timeout threshold in the
            // timer callback has been reached.
            var autoEvent = new AutoResetEvent(false);
            timer = new Timer(new TimerCallback(TimerTask), autoEvent, 0,10);
        }
        private void TimerTask(object timerState)
        {
            m_ConsoleUdp.DofUpToMedian(m_ConsoleUdp.m_UdpReceiveData.Result.RemoteEndPoint);
        }
    }
}
