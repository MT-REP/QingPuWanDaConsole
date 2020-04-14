using MainControl.MT_UDP;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MainControl
{
    /// <summary>
    /// AdminLoginWindow.xaml 的交互逻辑
    /// </summary>
    ///

    public partial class AdminLoginWindow : Window
    {
        #region //变量
        private bool[] carDoorShieldCheckFlag = new bool[4] { false, false, false, false };
        private bool[] ladderShieldCheckFlag = new bool[5] { false, false, false, false, false };
        private bool gameStartUpBtnDisplayFlag = false;
        public string[] ForceActiveBtnContent = new string[2] { "强制激活主窗口按钮", "取消强制激活按钮" };
        //public DofInfCollection dofInfCollection=new DofInfCollection();
        #endregion
        public AdminLoginWindow()
        {
            InitializeComponent();
            //dofInfCollection = (DofInfCollection)this.FindResource("DofInfCollectionKey");
        }

        public bool[] LadderShieldCheckFlag { get => ladderShieldCheckFlag; set => ladderShieldCheckFlag = value; }
        public bool[] CarDoorShieldCheckFlag { get => carDoorShieldCheckFlag; set => carDoorShieldCheckFlag = value; }
        public bool GameStartUpBtnDisplayFlag { get => gameStartUpBtnDisplayFlag; set => gameStartUpBtnDisplayFlag = value; }

        private void DefiniteNoCheck_Click(object sender, RoutedEventArgs e)
        {
            if(true==CbNum0CarDoorShieldCheck.IsChecked)
            {
                CarDoorShieldCheckFlag[0] = true;
            }
            else
            {
                CarDoorShieldCheckFlag[0] = false;
            }

            if (true == CbNum1CarDoorShieldCheck.IsChecked)
            {
                CarDoorShieldCheckFlag[1] = true;
            }
            else
            {
                CarDoorShieldCheckFlag[1] = false;
            }

            if (true == CbNum2CarDoorShieldCheck.IsChecked)
            {
                CarDoorShieldCheckFlag[2] = true;
            }
            else
            {
                CarDoorShieldCheckFlag[2] = false;
            }

            if (true == CbNum3CarDoorShieldCheck.IsChecked)
            {
                CarDoorShieldCheckFlag[3] = true;
            }
            else
            {
                CarDoorShieldCheckFlag[3] = false;
            }

            if(true==CbNum0LadderShieldCheck.IsChecked)
            {
                LadderShieldCheckFlag[0] = true;
            }
            else
            {
                LadderShieldCheckFlag[0] = false;
            }

            if(true==CbNum1LadderShieldCheck.IsChecked)
            {
                LadderShieldCheckFlag[1] = true;
            }
            else
            {
                LadderShieldCheckFlag[1] = false;
            }

            if(true==CbNum2LadderShieldCheck.IsChecked)
            {
                LadderShieldCheckFlag[2] = true;
            }
            else
            {
                LadderShieldCheckFlag[2] = false;
            }

            if(true==CbNum3LadderShieldCheck.IsChecked)
            {
                LadderShieldCheckFlag[3] = true;
            }
            else
            {
                LadderShieldCheckFlag[3] = false;
            }

            if(true==CbNum4LadderShieldCheck.IsChecked)
            {
                LadderShieldCheckFlag[4] = true;
            }
            else
            {
                LadderShieldCheckFlag[4] = false;
            }
        }
        protected override void OnClosing(CancelEventArgs e)
        {
            Hide();
            e.Cancel = true;
        }

        private void BtnDisplayGameStartupBtn_Click(object sender, RoutedEventArgs e)
        {
            GameStartUpBtnDisplayFlag = true;
        }
        /// <summary>
        /// 基于平台返回数据，在管理员窗口显示错误代码
        /// </summary>
        /// <param name="dataToHost"></param>
        public void DisplayErrorCode(DataToHost[] dataToHost)
        {
            for (int i = 0; i < dataToHost.Length; i++)
            {
                ((DofInfCollection)FindResource("DofInfCollectionKey")).DofInfs[i].Para0 = dataToHost[i].para[0];
                ((DofInfCollection)FindResource("DofInfCollectionKey")).DofInfs[i].Para1 = dataToHost[i].para[1];
                ((DofInfCollection)FindResource("DofInfCollectionKey")).DofInfs[i].Para2 = dataToHost[i].para[2];
                ((DofInfCollection)FindResource("DofInfCollectionKey")).DofInfs[i].Para3 = dataToHost[i].para[3];
                ((DofInfCollection)FindResource("DofInfCollectionKey")).DofInfs[i].Para4 = dataToHost[i].para[4];
                ((DofInfCollection)FindResource("DofInfCollectionKey")).DofInfs[i].Para5 = dataToHost[i].para[5];
            }
            
        }
    }
}
