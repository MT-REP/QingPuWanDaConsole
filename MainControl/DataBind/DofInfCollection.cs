// // Copyright (c) Microsoft. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Timers;

namespace MainControl
{
    public class DofInfCollection : ObservableCollection<DofInf>
    {
        //private readonly DofInf _item1 = new DofInf();
        //private readonly DofInf _item2 = new DofInf();
        //private readonly DofInf _item3 = new DofInf();
        private DofInf[] _dofInfs = new DofInf[4];

        public DofInf[] DofInfs { get => _dofInfs; set => _dofInfs = value; }

        public DofInfCollection()
        {
            //Add(_item1);
            //Add(_item2);
            //Add(_item3);
            for (int i = 0; i < _dofInfs.Length; i++)
            {
                _dofInfs[i] = new DofInf();
                Add(_dofInfs[i]);
            }
            //CreateTimer();
        }

        private void Timer1_Elapsed(object sender, ElapsedEventArgs e)
        {
            //_dofInfs[0].Para1 += (float)1.25;
            //_dofInfs[1].Para1 += (float)2.45;
            //_dofInfs[2].Para1 += (float)10.55;

            //_dofInfs[0].Para2 += 1.0f + (float)1.25;
            //_dofInfs[1].Para2 += 1.0f + (float)2.45;
            //_dofInfs[2].Para2 += 1.0f + (float)10.55;
            //_dofInfs[0].MotorCode1 += (float) 1.25;
            //_dofInfs[1].MotorCode1 += (float) 2.45;
            //_dofInfs[2].MotorCode1 += (float) 10.55;

            //_dofInfs[0].MotorCode2 += 1.0f+(float)1.25;
            //_dofInfs[1].MotorCode2 += 1.0f+(float)2.45;
            //_dofInfs[2].MotorCode2 += 1.0f+(float)10.55;
        }

        private void CreateTimer()
        {
            var timer1 = new Timer
            {
                Enabled = true,
                Interval = 2000
            };
            timer1.Elapsed += Timer1_Elapsed;
        }
    }
}