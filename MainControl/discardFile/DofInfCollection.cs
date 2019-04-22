using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace MainControl
{
    public class DofInfCollection: ObservableCollection<DofInf>
    {
        private readonly DofInf dofInfs0 = new DofInf();
        private readonly DofInf dofInfs1 = new DofInf();
        public DofInfCollection()
        {

            //for (int i=0;i<dofInfs.Length;i++)
            //{
            //    dofInfs[i] = new DofInf();
            //    Add(dofInfs[i]);
            //}
            Add(dofInfs0);
            Add(dofInfs1);
            CreateTimer();
        }
        private void Timer1_Elapsed(object sender, ElapsedEventArgs e)
        {
            //for(int i=0;i<4;i++)
            //{
            //    dofInfs[i].MotorCode0 += (float)1.25+i;
            //    dofInfs[i].MotorCode1 += (float)1.25+i;
            //    dofInfs[i].MotorCode2 += (float)1.25+i;
            //    dofInfs[i].MotorCode4 += (float)1.25+i;
            //    dofInfs[i].MotorCode5 += (float)1.25+i;
            //    dofInfs[i].MotorCode3 += (float)1.25+i;
            //}
            dofInfs0.MotorCode0 += (float)1.25 + 0;
            dofInfs0.MotorCode1 += (float)1.25 + 0;
            dofInfs0.MotorCode2 += (float)1.25 + 0;
            dofInfs0.MotorCode4 += (float)1.25 + 0;
            dofInfs0.MotorCode5 += (float)1.25 + 0;
            dofInfs0.MotorCode3 += (float)1.25 + 0;
            dofInfs1.MotorCode0 += (float)1.25 + 1;
            dofInfs1.MotorCode1 += (float)1.25 + 1;
            dofInfs1.MotorCode2 += (float)1.25 + 1;
            dofInfs1.MotorCode4 += (float)1.25 + 1;
            dofInfs1.MotorCode5 += (float)1.25 + 1;
            dofInfs1.MotorCode3 += (float)1.25 + 1;

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
