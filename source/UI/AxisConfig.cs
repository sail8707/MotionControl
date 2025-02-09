﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MotionControl;

namespace MotionControl
{
    internal partial class AxisConfig : Form
    {
        private Axis axis = new Axis();
        internal AxisConfig(int i)
        {
            InitializeComponent();
            axis = Motion.Axis(i);
            this.Text = string.Format("轴{0}: {1}", axis.id, axis.name);
            this.textBox_CardNo.Text = axis.cardId.ToString();
            this.textBox_Id.Text = axis.id.ToString();
            this.textBox_Name.Text = axis.name;
            this.textBox_Pulse.Text = axis.plsPerMm.ToString();
            
            this.comboBox_Dir.SelectedIndex = axis.homePara.direction;
            this.textBox_MaxSearch.Text = axis.homePara.maxSearchDistance.ToString("F3");


            this.textBox_HomeAcc.Text = axis.homePara.acc.ToString("F3");
            this.textBox_HomeVel.Text = axis.homePara.vel.ToString("F3");
            this.textBox_HomeTimeout.Text = axis.homePara.maxSeconds.ToString("F1");

            this.textBox_Acc.Text = axis.motionPara.acc.ToString("F3");
            this.textBox_Vel.Text = axis.motionPara.maxVel.ToString("F3");
            this.textBox_Timeout.Text = axis.motionPara.maxSeconds.ToString("F1");            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                axis.cardId = (short)Convert.ToInt32(this.textBox_CardNo.Text);
                axis.id = (short)Convert.ToInt32(this.textBox_Id.Text);
                axis.name = this.textBox_Name.Text;
                axis.plsPerMm = Convert.ToDouble(this.textBox_Pulse.Text);
                axis.homePara.direction = this.comboBox_Dir.SelectedIndex;
                axis.homePara.maxSearchDistance = Convert.ToDouble(this.textBox_MaxSearch.Text);
                axis.homePara.acc = Convert.ToDouble(this.textBox_HomeAcc.Text);
                axis.homePara.vel = Convert.ToDouble(this.textBox_HomeVel.Text);
                axis.homePara.maxSeconds = Convert.ToDouble(this.textBox_HomeTimeout.Text);

                axis.motionPara.acc = Convert.ToDouble(this.textBox_Acc.Text);
                axis.motionPara.maxVel = Convert.ToDouble(this.textBox_Vel.Text);
                axis.motionPara.maxSeconds = Convert.ToDouble(this.textBox_Timeout.Text);
                Motion.SaveAxesPara();
                this.Close();
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
    }
}
