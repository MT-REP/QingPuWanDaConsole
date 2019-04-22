using MainControl.MT_UDP;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MainControl
{
    public class DofInf : INotifyPropertyChanged
    {
        private DataToHost _dataToHost = new DataToHost();
        private float _motorCode0;
        private float _motorCode1;
        private float _motorCode2;
        private float _motorCode3;
        private float _motorCode4;
        private float _motorCode5;

        private float _para0;
        private float _para1;
        private float _para2;
        private float _para3;
        private float _para4;
        private float _para5;
        public DofInf()
        {
            //_dataToHost.attitude = new float[6];
            //_dataToHost.motor_code = new float[6];
            //_dataToHost.para = new float[6];
            _motorCode0=0.0f;
            _motorCode1=0.0f;
            _motorCode2=0.0f;
            _motorCode3=0.0f;
            _motorCode4=0.0f;
            _motorCode5=0.0f;
        }

        public float MotorCode0 { get => _motorCode0; set { _motorCode0 = value; OnPropertyChanged("MotorCode0"); } }
        public float MotorCode1 { get => _motorCode1; set { _motorCode1 = value; OnPropertyChanged("MotorCode1"); } }
        public float MotorCode2 { get => _motorCode2; set { _motorCode2 = value; OnPropertyChanged("MotorCode2"); } }
        public float MotorCode3 { get => _motorCode3; set { _motorCode3 = value; OnPropertyChanged("MotorCode3"); } }
        public float MotorCode4 { get => _motorCode4; set { _motorCode4 = value; OnPropertyChanged("MotorCode4"); } }
        public float MotorCode5 { get => _motorCode5; set { _motorCode5 = value; OnPropertyChanged("MotorCode5"); } }

        public float Para0 { get => _para0; set { _para0 = value; OnPropertyChanged("Para0"); } }
        public float Para1 { get => _para1; set { _para1 = value; OnPropertyChanged("Para1"); } }
        public float Para2 { get => _para2; set { _para2 = value; OnPropertyChanged("Para2"); } }
        public float Para3 { get => _para3; set { _para3 = value; OnPropertyChanged("Para3"); } }
        public float Para4 { get => _para4; set { _para4 = value; OnPropertyChanged("Para4"); } }
        public float Para5 { get => _para5; set { _para5 = value; OnPropertyChanged("Para5"); } }

        public DataToHost DataToHostData
        {
            get => _dataToHost;
            set
            {
                _dataToHost = value;
                MotorCode0 = _dataToHost.motor_code[0];
                MotorCode1 = _dataToHost.motor_code[1];
                MotorCode2 = _dataToHost.motor_code[2];
                MotorCode3 = _dataToHost.motor_code[3];
                MotorCode4 = _dataToHost.motor_code[4];
                MotorCode5 = _dataToHost.motor_code[5];

                //Para0 = _dataToHost.para[0];
                //Para1 = _dataToHost.para[1];
                //Para2 = _dataToHost.para[2];
                //Para3 = _dataToHost.para[3];
                //Para4 = _dataToHost.para[4];
                //Para5 = _dataToHost.para[5];
                //OnPropertyChanged("DataToHostData");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string v)
        {
            var handler = PropertyChanged;
            handler?.Invoke(this,new PropertyChangedEventArgs(v));
            //throw new NotImplementedException();
        }

    }
}
