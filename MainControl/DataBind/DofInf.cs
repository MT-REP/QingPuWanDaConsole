// // Copyright (c) Microsoft. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MainControl.MT_UDP;
using System.ComponentModel;

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

        public DofInf()
        {
            _motorCode0 = 0.0f;
            _motorCode1 = 0.0f;
            _motorCode2 = 0.0f;
            _motorCode3 = 0.0f;
            _motorCode4 = 0.0f;
            _motorCode5 = 0.0f;
        }
        public float MotorCode0 { get => _motorCode0; set { _motorCode0 = value; OnPropertyChanged("MotorCode0"); } }
        public float MotorCode1 { get => _motorCode1; set { _motorCode1 = value; OnPropertyChanged("MotorCode1"); } }
        public float MotorCode2 { get => _motorCode2; set { _motorCode2 = value; OnPropertyChanged("MotorCode2"); } }
        public float MotorCode3 { get => _motorCode3; set { _motorCode3 = value; OnPropertyChanged("MotorCode3"); } }
        public float MotorCode4 { get => _motorCode4; set { _motorCode4 = value; OnPropertyChanged("MotorCode4"); } }
        public float MotorCode5 { get => _motorCode5; set { _motorCode5 = value; OnPropertyChanged("MotorCode5"); } }
        //public float MotorCode0
        //{
        //    get { return _motorCode0; }
        //    set
        //    {
        //        if (_motorCode0.Equals(value) == false)
        //        {
        //            _motorCode0 = value;
        //            // Call OnPropertyChanged whenever the property is updated
        //            OnPropertyChanged("MotorCode0");
        //        }
        //    }
        //}

        //public float MotorCode1
        //{
        //    get { return _motorCode1; }
        //    set
        //    {
        //        if (_motorCode1.Equals(value) == false)
        //        {
        //            _motorCode1 = value;
        //            // Call OnPropertyChanged whenever the property is updated
        //            OnPropertyChanged("MotorCode1");
        //        }
        //    }
        //}
        //public float MotorCode2
        //{
        //    get { return _motorCode2; }
        //    set
        //    {
        //        if (_motorCode2.Equals(value) == false)
        //        {
        //            _motorCode2 = value;
        //            // Call OnPropertyChanged whenever the property is updated
        //            OnPropertyChanged("MotorCode2");
        //        }
        //    }
        //}

        //public float MotorCode3
        //{
        //    get { return _motorCode3; }
        //    set
        //    {
        //        if (_motorCode3.Equals(value) == false)
        //        {
        //            _motorCode3 = value;
        //            // Call OnPropertyChanged whenever the property is updated
        //            OnPropertyChanged("MotorCode3");
        //        }
        //    }
        //}

        //public float MotorCode4
        //{
        //    get { return _motorCode4; }
        //    set
        //    {
        //        if (_motorCode4.Equals(value) == false)
        //        {
        //            _motorCode4 = value;
        //            // Call OnPropertyChanged whenever the property is updated
        //            OnPropertyChanged("MotorCode4");
        //        }
        //    }
        //}
        //public float MotorCode5
        //{
        //    get { return _motorCode5; }
        //    set
        //    {
        //        if (_motorCode5.Equals(value) == false)
        //        {
        //            _motorCode5 = value;
        //            // Call OnPropertyChanged whenever the property is updated
        //            OnPropertyChanged("MotorCode5");
        //        }
        //    }
        //}

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

        // Declare event
        public event PropertyChangedEventHandler PropertyChanged;
        // OnPropertyChanged to update property value in binding
        private void OnPropertyChanged(string propName)
        {
            var handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}