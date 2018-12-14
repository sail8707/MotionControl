﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using System.Threading;
using lhdevice;
using System.Runtime.InteropServices;

namespace MotionControl
{
    /// <summary>
    /// 错误信息列表
    /// </summary>
    public enum Error
    {
        /// <summary>
        /// 无错误
        /// </summary>
        NoError = 0,
        /// <summary>
        /// 文件不存在
        /// </summary>
        FileNotExist = -1000,
        /// <summary>
        /// Id越界
        /// </summary>
        IdOutOfRange = -1001,
        /// <summary>
        /// 运动超时
        /// </summary>
        TimeOut = -1002,
        /// <summary>
        /// 运动到正限位停止
        /// </summary>
        NEL = -1003,
        /// <summary>
        /// 运动到负限位停止
        /// </summary>
        PEL = -1004,
        /// <summary>
        /// 轴不在同一张卡上
        /// </summary>
        AxesNotInOneCard = -1005
    }
    /// <summary>
    /// 回零方式
    /// </summary>
    public enum HomeMode
    {
        /// <summary>
        /// 未配置
        /// </summary>
        NONE = 0,
        /// <summary>
        /// 原点回零
        /// </summary>
        ORG = 1,
        /// <summary>
        /// EZ/Index信号回零
        /// </summary>
        INDEX = 2,
        /// <summary>
        /// 探针回零
        /// </summary>
        PROBE = 3,
        /// <summary>
        /// 原点+EZ/index回零
        /// </summary>
        ORG_INDEX = 4
    }
    /// <summary>
    /// 回零参数
    /// </summary>
    public struct HomePara
    {
        /// <summary>
        /// 回零模式
        /// </summary>
        public HomeMode mode;
        /// <summary>
        /// 加速度m/s^2
        /// </summary>
        public double acc;
        /// <summary>
        /// 速度m/s
        /// </summary>
        public double vel;
        /// <summary>
        /// 偏移量mm
        /// </summary>
        public double offset;
        /// <summary>
        /// 超时时间，单位s
        /// </summary>
        public double maxSeconds;
    }
    /// <summary>
    /// 运动参数
    /// </summary>
    public struct MotionPara
    {
        /// <summary>
        /// 加速度（默认减速度与之相同）
        /// </summary>
        public double acc;
        /// <summary>
        /// 最大速度
        /// </summary>
        public double maxVel;
        /// <summary>
        /// 超时时间，单位s
        /// </summary>
        public double maxSeconds;
    }
    /// <summary>
    /// MotionIO状态
    /// </summary>
    public struct MotionIO
    {
        /// <summary>
        /// 正限位信号
        /// </summary>
        public bool PEL;
        /// <summary>
        /// 负限位信号
        /// </summary>
        public bool NEL;
        /// <summary>
        /// 励磁信号
        /// </summary>
        public bool Enabled;
        /// <summary>
        /// 是否在运动中
        /// </summary>
        public bool Moving;
        /// <summary>
        /// 报警信号
        /// </summary>
        public bool Alarm;
    }
    /// <summary>
    /// 轴类，包含轴信息和运动接口
    /// </summary>
	[Serializable]
    public class Axis
    {
        /// <summary>
        /// 卡号，0-based
        /// </summary>
        public short cardId;
        /// <summary>
        /// 轴ID 1-based
        /// </summary>    
        public short id;
        /// <summary>
        /// 轴名字
        /// </summary>
        public string name;
        /// <summary>
        /// 脉冲当量，pulse/mm
        /// </summary>
        public double plsPerMm;
        /// <summary>
        /// 回零参数
        /// </summary>
        public HomePara homePara = new HomePara();
        /// <summary>
        /// 运动参数
        /// </summary>
        public MotionPara motionPara = new MotionPara();
        internal MotionIO mio_ = new MotionIO();
        internal bool online_ = false;
        private double cmdPos_ = 0;
        private double fbkPos_ = 0;
        private static short pulseWidth_ = 100;   //us
        /// <summary>
        /// MIO信号，每次使用会从板卡读取轴状态
        /// </summary>
        public MotionIO MIO
        {
            get
            {
                if (online_)
                {
                    int sts_ = 0;
                    lhmtc.LH_GetSts(id, out sts_, 1, false);
                    mio_.PEL = ((sts_ & 0x20) != 0);
                    mio_.NEL = ((sts_ & 0x40) != 0);
                    mio_.Moving = ((sts_ & 0x400) != 0);
                    mio_.Enabled = ((sts_ & 0x200) != 0);
                    mio_.Alarm = ((sts_ & 0x2) != 0);
                }
                return mio_;
            }
        }
        /// <summary>
        /// 指令位置，单位mm, 每次使用会从板卡读取轴指令位置
        /// </summary>
        public double CmdPos
        {
            get
            {
                if (online_)
                {
                    double cmd_ = 0;
                    lhmtc.LH_GetPrfPos(id, out cmd_, 1, false);
                    cmdPos_ = cmd_ / (plsPerMm <= 0 ? 1000 : plsPerMm);
                }
                return cmdPos_;
            }
        }
        /// <summary>
        /// 反馈位置，单位mm, 每次使用会从板卡读取轴反馈位置
        /// </summary>
        public double FbkPos
        {
            get
            {
                if (online_)
                {
                    double fbk_ = 0;
                    lhmtc.LH_GetEncPos(id, out fbk_, 1, false);
                    fbkPos_ = fbk_ / (plsPerMm <= 0 ? 1000 : plsPerMm);
                }
                return fbkPos_;
            }
        }
        /// <summary>
        /// 励磁使能
        /// </summary>
        /// <param name="enabled">true表示上电使能，false表示取消使能</param>
        /// <returns>0表示成功，负值表示错误码</returns>
        public int SetEnabled(bool enabled)
        {
            if (!online_)
            {
                mio_.Enabled = enabled;
                return 0;
            }
            if (enabled)
            {
                return lhmtc.LH_AxisOn(id, false);
            }
            else
            {
                return lhmtc.LH_AxisOff(id, false);
            }
        }

        /// <summary>
        /// 回零动作，阻塞等待完成
        /// </summary>
        /// <returns></returns>
        public int Home()
        {
            int err_ = 0;
            if (!online_)
            {
                cmdPos_ = fbkPos_ = 0;
                return 0;
            }
            int t_ = Environment.TickCount;
            if ((err_ = lhmtc.LH_Home(id, Int32.MaxValue / 2, _vel2pls(homePara.vel), _acc2pls(homePara.acc), _mm2pls(homePara.offset), false)) < 0)
            {
                return err_;
            }
            do
            {
                Thread.Sleep(100);
                if (Environment.TickCount - t_ > homePara.maxSeconds * 1000)
                {
                    return (int)Error.TimeOut;
                }
            } while (MIO.Moving);


            if (!mio_.PEL)
            {
                return 0;
            }
            //如果触发了正限位，则执行第二次反向回零
            if ((err_ = this.Reset()) != 0)
            {
                return err_;
            }
            t_ = Environment.TickCount;
            if ((err_ = lhmtc.LH_Home(id, Int32.MinValue / 2, _vel2pls(homePara.vel), _acc2pls(homePara.acc), _mm2pls(homePara.offset), false)) != 0)
            {
                return err_;
            }
            ushort pHomests = 0;
            do
            {
                Thread.Sleep(100);
                if (Environment.TickCount - t_ > homePara.maxSeconds * 1000)
                {
                    return (int)Error.TimeOut;
                }
                if ((err_ = this.Reset()) != 0)
                {
                    return err_;
                }
                if ((err_ = lhmtc.LH_HomeSts(id, out pHomests, false)) != 0)
                {
                    return err_;
                }
                if (pHomests == 1)//成功
                {
                    Thread.Sleep(100);
                    if ((err_ = lhmtc.LH_ZeroPos(id, 1, false)) != 0)
                    {
                        return err_;
                    }
                }
                else if (pHomests == 0)
                {
                    continue;
                }
                else
                {
                    return 1; //失败
                }

            } while (pHomests == 0);

            if (MIO.NEL)
            {
                return (int)Error.NEL;
            }
            return err_;
        }
        /// <summary>
        /// 当前位置设为原点(谨慎使用，设为原点后，新的运动坐标系将以该点为起点)
        /// </summary>
        /// <returns>0表示指令发送成功，负值表示错误码</returns></returns>
        public int ZeroPos()
        {
            int err_ = 0;
            if (online_)
            {
                if ((err_ = lhmtc.LH_ZeroPos(id, 1, false)) != 0)
                {
                    return err_;
                }
            }
            else
            {
                cmdPos_ = fbkPos_ = 0;
            }
            return 0;
        }
        /// <summary>
        /// 绝对运动指令，不检测完成
        /// </summary>
        /// <param name="pos">目标位置，单位mm</param>
        /// <param name="spd_ratio">运动速率，正数可以超过1.0</param>
        /// <returns>0表示指令发送成功，负值表示错误码</returns>
        public int AbsMove(double pos, double spd_ratio = 1.0)
        {
            var mp_ = new MotionPara
            {
                acc = motionPara.acc * spd_ratio * spd_ratio,
                maxVel = motionPara.maxVel * Math.Abs(spd_ratio),
            };
            return Move(pos, true, ref mp_);
        }
        /// <summary>
        /// 轴绝对运动，阻塞检测轴运动是否完成
        /// </summary>
        /// <param name="pos">目标位置, 单位mm</param>
        /// <param name="spd_ratio">运动速率，正数，可以大于1</param>
        /// <returns>0表示成功，非零表示错误</returns>
        public int AbsMoveOver(double pos, double spd_ratio = 1.0)
        {
            int err = 0;
            if ((err = AbsMove(pos, spd_ratio)) != 0)
            {
                return err;
            }
            return (err = MotionDone());
        }
        /// <summary>
        /// 相对运动指令（为减少累积误差，尽量使用绝对运动方式），不检测完成
        /// </summary>
        /// <param name="distance">运动距离, 单位mm</param>
        /// <param name="spd_ratio">运动速率，正数可以超过1.0</param>
        /// <returns>0表示指令发送成功，负值表示错误码</returns>
        public int RelMove(double distance, double spd_ratio = 1.0)
        {
            var mp_ = new MotionPara
            {
                acc = motionPara.acc * spd_ratio * spd_ratio,
                maxVel = motionPara.maxVel * Math.Abs(spd_ratio),
            };
            return Move(distance, false, ref mp_);
        }
        /// <summary>
        /// 轴相对运动，阻塞检测轴运动是否完成
        /// </summary>
        /// <param name="distance">运动距离，单位mm</param>
        /// <param name="spd_ratio">运动速率，正数，可以大于1</param>
        /// <returns>0表示成功，非零表示错误</returns>
        public int RelMoveOver(double distance, double spd_ratio = 1.0)
        {
            int err = 0;
            if ((err = RelMove(distance, spd_ratio)) != 0)
            {
                return err;
            }
            return (err = MotionDone());
        }
        /// <summary>
        /// 单轴点到点运动指令，不检测完成
        /// </summary>
        /// <param name="pos">目标位置</param>
        /// <param name="abs">true表示绝对运动，false表示相对运动</param>
        /// <param name="mp">运动参数，包含加速度mm/ms^2、速度mm/ms(m/s)</param>
        /// <returns>0表示指令发送成功，负值表示错误码</returns>
        public int Move(double pos, bool abs, ref MotionPara mp)
        {
            int err_ = 0;
            double pos_ = pos;
            if (online_)
            {
                var p_ = new lhmtc.TrapPrfPrm
                {
                    acc = _acc2pls(mp.acc),
                    dec = _acc2pls(mp.acc),
                    velStart = 0,
                    smoothTime = 10
                };
                if ((err_ = lhmtc.LH_PrfTrap(id, false)) != 0)
                {
                    return err_;
                }

                if ((err_ = lhmtc.LH_SetTrapPrm(id, ref p_, false)) != 0)
                {
                    return err_;
                }
                if (!abs)
                {
                    pos_ += FbkPos;
                }
                if ((err_ = lhmtc.LH_SetPos(id, _mm2pls(pos_), false)) != 0)
                {
                    return err_;
                }
                if ((err_ = lhmtc.LH_SetVel(id, _vel2pls(mp.maxVel), false)) != 0)
                {
                    return err_;
                }
                if ((err_ = lhmtc.LH_Update(1 << (id - 1), false)) != 0)
                {
                    return err_;
                }
            }
            else
            {
                cmdPos_ = fbkPos_ = pos;
            }
            return 0;
        }
        /// <summary>
        /// 设置触发位置并启动位置监控
        /// </summary>
        /// <param name="channel0_1">通道，0或1</param>
        /// <param name="posList">相对位置列表，单位mm, 注意是当前位置的相对距离，如向负方向运动，则可以设置-1,-3,-5.8, ...等</param>
        /// <returns>0表示成功，非零表示错误</returns>
        public int TriggerData(int channel0_1, List<double> posList)
        {
            if (!online_)
            {
                return 0;
            }
            int err_ = 0;
            short ch_ = (short)channel0_1;

            //1. 清除
            if ((err_ = lhmtc.LH_2DCompareStop(ch_)) != 0)
            {
                return err_;
            }
            if ((err_ = lhmtc.LH_2DCompareClear(ch_)) != 0)
            {
                return err_;
            }

            //2. 设置为1维比较    返回值最好判断是否为0
            if ((err_ = lhmtc.LH_2DCompareMode(ch_, 0)) != 0)
            {
                return err_;
            }

            //3. 设置比较参数
            lhmtc.T2DComparePrm prm_ = new lhmtc.T2DComparePrm
            {
                encx = id,
                ency = (short)(5 - id), //需要不同于encx
                source = 1, //编码器
                outputType = 0, //脉冲
                time = pulseWidth_,
                startLevel = 1,
                maxerr = 300,
                threshold = 10,
                pluseCount = 1,
                spacetime = 200
            };
            if ((err_ = lhmtc.LH_2DCompareSetPrm(ch_, ref prm_)) != 0)
            {
                return err_;
            }
            //4.设置数据
            int num_ = posList.Count;
            lhmtc.T2DCompareData[] data_ = new lhmtc.T2DCompareData[num_];
            //     double crt_ = 0;
            //    lhmtc.LH_GetEncPos(id, out crt_, 1, false);
            for (int i = 0; i < num_; i++)
            {
                data_[i].px = _mm2pls(posList[i]);
                data_[i].py = 0;
            }
            //IntPtr ptr_ = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(lhmtc.T2DCompareData)) * num_);
            if ((err_ = lhmtc.LH_2DCompareData(ch_, (short)num_, ref data_[0], 0)) != 0)
            {
                return err_;
            }
            int cnt_ = 0;
            short bufCnt_ = 0, sts_ = 0, fifo_ = 0, fifoCnt_ = 0;
            if ((err_ = lhmtc.LH_2DCompareStatus((short)channel0_1, ref sts_, ref cnt_, ref fifo_, ref fifoCnt_, ref bufCnt_)) != 0)
            {
                return err_;
            }
            //5. 启动比较功能            
            return (err_ = lhmtc.LH_2DCompareStart(ch_));
        }
        /// <summary>
        /// 设置线性触发位置，并启动位置监控
        /// </summary>
        /// <param name="channel0_1">通道，0或1</param>
        /// <param name="startPos">起始点，绝对位置，单位mm</param>
        /// <param name="interval">间距，单位mm，可以为负值</param>
        /// <param name="trigCnt">触发次数</param>
        /// <returns>0表示成功，非零表示错误</returns>
        public int TriggerLinear(int channel0_1, double startPos, double interval, int trigCnt)
        {
            if (!online_)
            {
                return 0;
            }
            int err_ = lhmtc.LH_CompareLinear(id, (short)channel0_1, _mm2pls(startPos), trigCnt, _mm2pls(interval), pulseWidth_, 1, false);
            return err_;
        }
        /// <summary>
        /// 停止位置出发指令
        /// </summary>
        /// /// <param name="channel0_1">通道号,取值0或1</param>
        /// <returns>0表示成功，负值表示错误码</returns>
        public int TriggerStop(int channel0_1)
        {
            if (!online_)
            {
                return 0;
            }
            return lhmtc.LH_2DCompareStop((short)channel0_1); ;
        }
        /// <summary>
        /// 软触发
        /// </summary>
        /// <param name="channel0_1">通道号,取值0或1</param>
        /// <param name="pulseCnt">输出个数，默认1</param>
        /// <param name="interval_us">每个脉冲间隔时间，单位us,仅在pulseCnt>1时有效</param>
        /// <returns>0表示成功，负值表示错误码</returns>
        public int SoftwareTrigger(int channel0_1, int pulseCnt = 1, int interval_us = 200)
        {
            if (!online_)
            {
                return 0;
            }
            int err_ = 0, cnt_ = 0;
            short bufCnt_ = 0, sts_ = 0, fifo_ = 0, fifoCnt_ = 0;
            //先停止之前的触发
            if ((err_ = TriggerStop(channel0_1)) < 0)
            {
                return err_;
            }
            if ((err_ = lhmtc.LH_2DCompareStatus((short)channel0_1, ref sts_, ref cnt_, ref fifo_, ref fifoCnt_, ref bufCnt_)) != 0)
            {
                return err_;
            }
            return lhmtc.LH_2DComparePulse((short)channel0_1, 1, 0, pulseWidth_, pulseCnt, (short)interval_us);
        }
        /// <summary>
        /// 清除轴报警
        /// </summary>
        /// <returns>0表示成功，负值表示错误码</returns>
        public int ClearAlarm()
        {
            if (online_)
            {
                return lhmtc.LH_ClrSts(id, 1, false);
            }
            mio_.Alarm = false;
            return 0;
        }
        /// <summary>
        /// 阻塞检测运动完成
        /// </summary>
        /// <returns>0表示成功，非0值表示错误码</returns>
        public int MotionDone()
        {
            var t_ = Environment.TickCount;
            do
            {
                if (Environment.TickCount - t_ > motionPara.maxSeconds * 1000)
                {
                    return (int)Error.TimeOut;
                }
                Thread.Sleep(5);
            } while (MIO.Moving);
            return 0;
        }
        /// <summary>
        /// 轴复位，包括清除状态、报警。
        /// </summary>
        /// <returns>0表示成功，负值表示错误码</returns>
        public int Reset()
        {
            int ret_ = 0;
            if (!online_)
            {
                return 0;
            }
            if ((ret_ = lhmtc.LH_ClrSts(id, 1, false)) != 0)
            {
                return ret_;
            }
            if ((ret_ = lhmtc.LH_AxisReset(id, 1, false)) != 0)//下降沿或者上升沿有效
            {
                return ret_;
            }
            if ((ret_ = lhmtc.LH_AxisReset(id, 0, false)) != 0)
            {
                return ret_;
            }
            return ret_;
        }

        // m/s^2 -> pulse/ms^2
        internal double _acc2pls(double mpss)
        {
            return mpss * plsPerMm / 1000.0;
        }
        // mm -> pulse
        internal int _mm2pls(double mm)
        {
            return (int)(mm * this.plsPerMm);
        }
        // m/s -> pulse/ms
        internal double _vel2pls(double mps)
        {
            return mps * this.plsPerMm;
        }

    }
    /// <summary>
    /// XY两坐标系
    /// </summary>
    public struct Point2d
    {
        public double x;
        public double y;
        public Point2d(double x_, double y_)
        {
            x = x_;
            y = y_;
        }
    }
    /// <summary>
    /// XYZ三坐标系
    /// </summary>
    public struct Point3d
    {
        public double x;
        public double y;
        public double z;
        /// <summary>
        /// 创建新的实例，并初始化
        /// </summary>
        /// <param name="x_">x值</param>
        /// <param name="y_">y值</param>
        /// <param name="z_">z值</param>
        public Point3d(double x_, double y_, double z_)
        {
            x = x_;
            y = y_;
            z = z_;
        }
    }
    /// <summary>
    /// 数字IO类，包含信息和方法
    /// </summary>
    public class DIO
    {
        /// <summary>
        /// IO名字
        /// </summary>
        public string name;
        /// <summary>
        /// 卡号
        /// </summary>
        public short cardId;
        /// <summary>
        /// 位号
        /// </summary>
        public short bitNo;
        /// <summary>
        /// 是否输出, true表示为输出，false表示为输入
        /// </summary>
        public bool ouput;
        [NonSerialized]
        private bool bit_;
        internal bool online_;
        /// <summary>
        /// 获取或设置IO状态
        /// </summary>      
        [System.Xml.Serialization.XmlIgnore]
        public bool Bit
        {
            get
            {
                if (online_)
                {
                    int v = 0;
                    if (this.ouput)
                    {
                        lhmtc.LH_GetDo(out v, false);
                    }
                    else
                    {
                        lhmtc.LH_GetDi(4, out v, false);
                    }
                    bit_ = ((v & (1 << bitNo)) != 0);
                }
                return bit_;
            }
            set
            {
                if (online_)
                {
                    if (this.ouput)
                    {
                        lhmtc.LH_SetDoBit(bitNo, (short)(value ? 1 : 0), false);
                    }
                }
                else
                {
                    bit_ = value;
                }
            }
        }
    }

    /// <summary>
    /// 运动类，包含板卡、轴、IO信息及列表
    /// </summary>
    public class Motion
    {
        private static string configFileFolder_ = string.Empty;
        private static List<Axis> axisList_ = new List<Axis>();
        private static List<DIO> dioList_ = new List<DIO>();
        private static short crdSys_ = 1;//默认用坐标系1
        private static short fifo_ = 0; //默认用fifo0
        private static bool _isIdNormal(int id)
        {
            return (id >= 0 && id < AxisNum);
        }
        private static bool online_ = false;
        #region 轴
        /// <summary>
        /// 根据index获取轴信息
        /// </summary>
        /// <param name="i">轴号，0-based</param>
        /// <returns>返回获取到的轴号</returns>
        public static Axis Axis(int i)
        {
            // return axisList_.Find(t=>t.id==i);
            return axisList_[i];
        }
        /// <summary>轴数</summary>
        public static int AxisNum { get { return axisList_.Count; } }
        /// <summary>
        /// 系统初始化，包括加载参数，打开板卡等
        /// </summary>
        /// <param name="configFileFolder">参数文件夹</param>
        /// <param name="onlineMode">true在线模式，false脱机模式</param>
        /// <returns>0表示成功，负值表示错误码</returns>
        public static int InitSystem(string configFileFolder, bool onlineMode)
        {
            int err_ = 0;
            string file_ = string.Empty;
            online_ = onlineMode;
            configFileFolder_ = configFileFolder;
            //0. 加载各轴的默认基础参数
            file_ = System.IO.Path.Combine(configFileFolder, "axis.xml");
            if (!System.IO.File.Exists(file_))
            {
                return (int)Error.FileNotExist;
            }
            axisList_.Clear();
            axisList_ = _xmlDeserialFromFile<List<Axis>>(file_);

            file_ = System.IO.Path.Combine(configFileFolder, "dio.xml");
            if (!System.IO.File.Exists(file_))
            {
                return (int)Error.FileNotExist;
            }
            dioList_.Clear();
            dioList_ = _xmlDeserialFromFile<List<DIO>>(file_);
            //1. 扫描板卡数量
            if (online_)
            {
                short card_num_ = 0;
                lhmtc.LH_EnumDevice(out card_num_);

                //2. 打开板卡
                for (short i = 0; i < card_num_; i++)
                {
                    if ((err_ = lhmtc.LH_Open(i, 1)) != 0)
                    {
                        return err_;
                    }
                }
                if ((err_ = lhmtc.LH_Reset()) != 0)
                {
                    return err_;
                }
                if ((err_ = lhmtc.LH_HomeInit(false)) != 0)
                {
                    return err_;
                }
                //3. 设置各个板卡所连轴的系统参数
                for (short i = 0; i < card_num_; i++)
                {
                    file_ = System.IO.Path.Combine(configFileFolder, i.ToString() + ".cfg");
                    if (!System.IO.File.Exists(file_))
                    {
                        return (int)Error.FileNotExist;
                    }
                    if ((err_ = lhmtc.LH_LoadConfig(file_)) != 0)
                    {
                        return err_;
                    }
                }
            }
            //4. 复位各个轴
            foreach (var a in axisList_)
            {
                a.online_ = online_;
                if ((err_ = a.Reset()) != 0)
                {
                    return err_;
                }
            }
            foreach(var i in dioList_)
            {
                i.online_ = online_;
            }
            return 0;
        }
        /// <summary>
        /// 轴绝对位置移动指令，不检测完成，调用Axis类中AbsMove方法
        /// </summary>
        /// <param name="axisId">轴号，0-based</param>
        /// <param name="pos">目标位置，单位mm</param>
        /// <param name="spd_ratio">运动速率，正数，可以大于1</param>
        /// <returns>0表示成功，非0值表示错误码</returns>
        public static int AbsMove(int axisId, double pos, double spd_ratio = 1.0)
        {
            return Axis(axisId).AbsMove(pos, spd_ratio);
        }
        /// <summary>
        /// 两轴绝对位置移动指令，不检测完成，调用Axis类中AbsMove方法
        /// </summary>
        /// <param name="axis1">轴号1，0-based</param>
        /// <param name="axis2">轴号2，0-based</param>
        /// <param name="pos">目标位置，单位mm</param>
        /// <param name="spd_ratio">运动速率，正数，可以大于1</param>
        /// <returns>0表示成功，非0值表示错误码</returns>
        public static int AbsMove(int axis1, int axis2, ref Point2d pos, double spd_ratio = 1.0)
        {
            var axis_ = new List<int>() { axis1, axis2 };
            var pos_ = new List<double>() { pos.x, pos.y };
            return AbsMove(axis_, pos_, spd_ratio);
        }
        /// <summary>
        /// 三轴绝对位置移动指令，不检测完成，调用Axis类中AbsMove方法
        /// </summary>
        /// <param name="axis1">轴号1，0-based</param>
        /// <param name="axis2">轴号2，0-based</param>
        /// <param name="axis3">轴号3，0-based</param>
        /// <param name="pos">目标位置，单位mm</param>
        /// <param name="spd_ratio">运动速率，正数，可以大于1</param>
        /// <returns>0表示成功，非0值表示错误码</returns>
        public static int AbsMove(int axis1, int axis2, int axis3, ref Point3d pos, double spd_ratio)
        {
            var axis_ = new List<int>() { axis1, axis2, axis3 };
            var pos_ = new List<double>() { pos.x, pos.y, pos.z };
            return AbsMove(axis_, pos_, spd_ratio);
        }
        /// <summary>
        /// 多轴绝对位置移动指令，不检测完成，调用Axis类中AbsMove方法
        /// </summary>
        /// <param name="axisList">轴号列表，0-based</param>
        /// <param name="posList">目标位置列表，单位mm</param>
        /// <param name="spd_ratio">运动速率，正数，可以大于1</param>
        /// <returns>0表示成功，非0值表示错误码</returns>
        public static int AbsMove(List<int> axisList, List<double> posList, double spd_ratio)
        {
            int err_ = 0;
            for (int i = 0; i < axisList.Count; i++)
            {
                if ((err_ = Axis(axisList[i]).AbsMove(posList[i], spd_ratio)) != 0)
                {
                    return err_;
                }
            }
            return err_;
        }
        /// <summary>
        /// 单轴运动+阻塞检测完成，调用Axis类中的AbsMoveOver方法
        /// </summary>
        /// <param name="axisId">轴号，0-based</param>
        /// <param name="pos">目标位置，单位mm</param>
        /// <param name="spd_ratio">运动速率，正数，可以大于1</param>
        /// <returns>0表示成功，非0值表示错误码</returns>
        public static int AbsMoveOver(int axisId, double pos, double spd_ratio = 1.0)
        {
            return Axis(axisId).AbsMove(pos, spd_ratio);
        }
        /// <summary>
        /// 两轴绝对运动+阻塞检测完成，调用Axis类中的AbsMove + Done方法
        /// </summary>
        /// <param name="axis1">轴号1，0-based</param>
        /// <param name="axis2">轴号2，0-based</param>
        /// <param name="pos">目标位置，单位mm</param>
        /// <param name="spd_ratio">运动速率，正数，可以大于1</param>
        /// <returns>0表示成功，非0值表示错误码</returns>
        public static int AbsMoveOver(int axis1, int axis2, ref Point2d pos, double spd_ratio = 1.0)
        {
            var axis_ = new List<int>() { axis1, axis2 };
            var pos_ = new List<double>() { pos.x, pos.y };
            return AbsMoveOver(axis_, pos_, spd_ratio);
        }
        /// <summary>
        /// 三轴绝对位置移动指令，阻塞检测完成，调用Axis类中AbsMove+Done方法
        /// </summary>
        /// <param name="axis1">轴号1，0-based</param>
        /// <param name="axis2">轴号2，0-based</param>
        /// <param name="axis3">轴号3，0-based</param>
        /// <param name="pos">目标位置，单位mm</param>
        /// <param name="spd_ratio">运动速率，正数，可以大于1</param>
        /// <returns>0表示成功，非0值表示错误码</returns>
        public static int AbsMoveOver(int axis1, int axis2, int axis3, ref Point3d pos, double spd_ratio)
        {
            var axis_ = new List<int>() { axis1, axis2, axis3 };
            var pos_ = new List<double>() { pos.x, pos.y, pos.z };
            return AbsMoveOver(axis_, pos_, spd_ratio);
        }
        /// <summary>
        /// 多轴绝对位置移动指令，阻塞检测完成，调用Axis类中AbsMove+Done方法
        /// </summary>
        /// <param name="axisList">轴号列表，0-based</param>
        /// <param name="posList">目标位置列表，单位mm</param>
        /// <param name="spd_ratio">运动速率，正数，可以大于1</param>
        /// <returns>0表示成功，非0值表示错误码</returns>
        public static int AbsMoveOver(List<int> axisList, List<double> posList, double spd_ratio)
        {
            int err_ = 0;
            if ((err_ = AbsMove(axisList, posList, spd_ratio)) != 0)
            {
                return err_;
            }
            Thread.Sleep(100);
            return (err_ = MotionDone(axisList));
        }
        /// <summary>
        /// 单轴阻塞检测运动完成函数，调用Axis类中的MotionDone函数
        /// </summary>
        /// <param name="axis">轴号，0based</param>
        /// <returns>0表示成功，非0值表示错误码</returns>
        public static int MotionDone(int axis)
        {
            return Axis(axis).MotionDone();
        }
        /// <summary>
        /// 两轴阻塞检测运动完成函数，调用Axis类中的MotionDone函数
        /// </summary>
        /// <param name="axis1">轴号1，0-based</param>
        /// <param name="axis2">轴号2，0-based</param>
        /// <returns>0表示成功，非0值表示错误码</returns>
        public static int MotionDone(int axis1, int axis2)
        {
            var axis_ = new List<int>() { axis1, axis2 };
            return MotionDone(axis_);
        }
        /// <summary>
        /// 三轴阻塞检测运动完成函数，调用Axis类中的MotionDone函数
        /// </summary>
        /// <param name="axis1">轴号1，0-based</param>
        /// <param name="axis2">轴号2，0-based</param>
        /// <param name="axis3">轴号3，0-based</param>
        /// <returns>0表示成功，非0值表示错误码</returns>
        public static int MotionDone(int axis1, int axis2, int axis3)
        {
            var axis_ = new List<int>() { axis1, axis2, axis3 };
            return MotionDone(axis_);
        }
        /// <summary>
        /// 多轴阻塞检测运动完成函数，调用Axis类中的MotionDone函数
        /// </summary>
        /// <param name="axisList">轴号列表，0-based</param>
        /// <returns>0表示成功，非0值表示错误码</returns>
        public static int MotionDone(List<int> axisList)
        {
            int err_ = 0;
            for (int i = 0; i < axisList.Count; i++)
            {
                if ((err_ = Axis(axisList[i]).MotionDone()) != 0)
                {
                    return err_;
                }
            }
            return err_;
        }

        /// <summary>
        /// 两轴直线插补运动指令，不检测完成
        /// </summary>
        /// <param name="id1">轴1ID, 0-based</param>
        /// <param name="id2">轴2ID, 0-based</param>
        /// <param name="pos">目标位置</param>
        /// <param name="resultantVel">运动合速度，单位m/s</param>
        /// <param name="resultantAcc">运动合加速度，单位m/s^2</param>
        /// <returns>0表示成功，负值表示错误码</returns>
        public static int Line(int id1, int id2, ref Point2d pos, double resultantVel, double resultantAcc)
        {
            int err_ = 0;
            if (!online_)
            {
                return 0;
            }
            if (!_isIdNormal(id1) || !_isIdNormal(id2))
            {
                return (int)(Error.IdOutOfRange);
            }
            if (axisList_[id1].cardId != axisList_[id2].cardId)
            {
                return (int)(Error.AxesNotInOneCard);
            }
            lhmtc.CrdCfg crdCfg_ = new lhmtc.CrdCfg();
            crdCfg_.dimension = 2;
            crdCfg_.profile = new short[8] { axisList_[id1].id, axisList_[id2].id, 0, 0, 0, 0, 0, 0 };
            crdCfg_.orignPos = new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
            crdCfg_.setOriginFlag = 1;
            crdCfg_.evenTime = 10;
            crdCfg_.synAccMax = 720;  //[0-750]
            crdCfg_.synVelMax = 720;   //[0-750]
            crdCfg_.synDecSmooth = 1;
            crdCfg_.synDecAbrupt = 500;

            if ((err_ = lhmtc.LH_SetCrdPrm(crdSys_, ref crdCfg_, false)) != 0)
            {
                return err_;
            }
            if ((err_ = lhmtc.LH_CrdClear(crdSys_, 1, fifo_, false)) != 0)
            {
                return err_;
            }
            if ((err_ = lhmtc.LH_LnXY(crdSys_,
                axisList_[id1]._mm2pls(pos.x), axisList_[id2]._mm2pls(pos.y),
                axisList_[id1]._vel2pls(resultantVel), axisList_[id1]._acc2pls(resultantAcc),
                0, fifo_)) != 0)
            {
                return err_;
            }
            if ((err_ = lhmtc.LH_CrdStart((short)(1 << (crdSys_ - 1)), (short)(1 << (fifo_ - 1)), false)) != 0)
            {
                return err_;
            }
            return 0;
        }
        /// <summary>
        /// 三轴直线插补运动指令，不检测完成
        /// </summary>
        /// <param name="id1">轴1ID, 0-based</param>
        /// <param name="id2">轴2ID, 0-based</param>
        /// <param name="id3">轴3ID, 0-based</param>
        /// <param name="pos">目标位置</param>
        /// <param name="resultantVel">运动合速度，单位m/s</param>
        /// <param name="resultantAcc">运动合加速度，单位m/s^2</param>
        /// <returns>0表示成功，非零值表示错误码</returns>
        public static int Line(int id1, int id2, int id3, ref Point3d pos, double resultantVel, double resultantAcc)
        {
            int err_ = 0;
            if (!online_)
            {
                return 0;
            }
            if (!_isIdNormal(id1) || !_isIdNormal(id2) || !_isIdNormal(id3))
            {
                return (int)(Error.IdOutOfRange);
            }
            if (axisList_[id1].cardId != axisList_[id2].cardId ||
                axisList_[id1].cardId != axisList_[id3].cardId ||
                axisList_[id2].cardId != axisList_[id3].cardId)
            {
                return (int)(Error.AxesNotInOneCard);
            }
            lhmtc.CrdCfg crdCfg_ = new lhmtc.CrdCfg();
            crdCfg_.dimension = 3;
            crdCfg_.profile = new short[8] { axisList_[id1].id, axisList_[id2].id, axisList_[id3].id, 0, 0, 0, 0, 0 };
            crdCfg_.orignPos = new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
            crdCfg_.setOriginFlag = 1;
            crdCfg_.evenTime = 10;
            crdCfg_.synAccMax = 500;  //[0-750]
            crdCfg_.synVelMax = 10;   //[0-750]
            crdCfg_.synDecSmooth = 1;
            crdCfg_.synDecAbrupt = 500;

            if ((err_ = lhmtc.LH_SetCrdPrm(crdSys_, ref crdCfg_, false)) != 0)
            {
                return err_;
            }
            if ((err_ = lhmtc.LH_CrdClear(crdSys_, 1, fifo_, false)) != 0)
            {
                return err_;
            }

            if ((err_ = lhmtc.LH_LnXYZ(crdSys_,
                axisList_[id1]._mm2pls(pos.x), axisList_[id2]._mm2pls(pos.y), axisList_[id3]._mm2pls(pos.z),
                axisList_[id1]._vel2pls(resultantVel), axisList_[id1]._acc2pls(resultantAcc),
                0, fifo_)) != 0)
            {
                return err_;
            }
            if ((err_ = lhmtc.LH_CrdStart(crdSys_, fifo_, false)) != 0)
            {
                return err_;
            }
            return 0;
        }
        /// <summary>
        /// 阻塞检测插补运动完成
        /// </summary>
        /// <param name="maxSeconds">超时时间，单位s</param>
        /// <returns>0表示成功，负值表示错误码</returns>
        public static int LineDone(double maxSeconds = 20)
        {
            int err_ = 0;
            if (!online_)
            {
                return 0;
            }
            short sts_ = 1;
            short cmdNum_ = 0;
            int space_ = 0;

            var t_ = Environment.TickCount;
            while (true)
            {
                if (Environment.TickCount - t_ > maxSeconds * 1000)
                {
                    return (int)(Error.TimeOut);
                }
                Thread.Sleep(10);
                if ((err_ = lhmtc.LH_CrdStatus(crdSys_, out sts_, out cmdNum_, out space_, fifo_, false)) != 0)
                {
                    continue;
                }
                if (sts_ != 1)
                {
                    break;
                }
            }
            return 0;
        }

        /// <summary>
        /// 三轴直线插补运动，发指令后，阻塞检测完成。
        /// </summary>
        /// <param name="id1">轴1，0-based</param>
        /// <param name="id2">轴2，0-based</param>
        /// <param name="id3">轴3，0-based</param>
        /// <param name="pos">目标位置</param>
        /// <param name="resultantVel">运动合速度，单位m/s</param>
        /// <param name="resultantAcc">运动合加速度，单位m/s^2</param>
        /// <param name="maxSeconds">最大检测完成时间，超时时间，单位秒</param>
        /// <returns>0表示成功，非零值表示错误码</returns>
        public static int LineOver(int id1, int id2, int id3, ref Point3d pos, double resultantVel, double resultantAcc, double maxSeconds = 20)
        {
            int err_ = 0;
            if ((err_ = Line(id1, id2, id3, ref pos, resultantVel, resultantAcc)) != 0)
            {
                return err_;
            }
            Thread.Sleep(100);
            return (err_ = LineDone(maxSeconds));
        }
        /// <summary>
        /// 两轴直线插补运动，发指令后，阻塞检测完成。
        /// </summary>
        /// <param name="id1">轴1，0-based</param>
        /// <param name="id2">轴2，0-based</param>
        /// <param name="pos">目标位置</param>
        /// <param name="resultantVel">运动合速度，单位m/s</param>
        /// <param name="resultantAcc">运动合加速度，单位m/s^2</param>
        /// <param name="maxSeconds">最大检测完成时间，超时时间，单位秒</param>
        /// <returns>0表示成功，非零值表示错误码</returns>
        public static int LineOver(int id1, int id2, ref Point2d pos, double resultantVel, double resultantAcc, double maxSeconds = 20)
        {
            int err_ = 0;
            if ((err_ = Line(id1, id2, ref pos, resultantVel, resultantAcc)) != 0)
            {
                return err_;
            }
            Thread.Sleep(100);
            return (err_ = LineDone(maxSeconds));
        }
        #endregion
        #region DIO
        /// <summary>
        /// 根据序号获取IO实例
        /// </summary>
        /// <param name="ioIdx">io号，0-based</param>
        /// <returns>返回实例</returns>
        public static DIO Dio(int ioIdx) { return dioList_[ioIdx]; }
        /// <summary>
        /// IO数量
        /// </summary>
        public static int DioNum { get => dioList_.Count; }
        
        #endregion
        /// <summary>
        /// 释放系统资源，包括轴使能关闭，办卡关闭等
        /// </summary>
        /// <returns></returns>
        public static int DiscardSystem()
        {
            int err_ = 0;
            if (!online_)
            {
                return 0;
            }
            foreach (var a in axisList_)
            {
                a.SetEnabled(false);
            }
            if ((err_ = lhmtc.LH_Close()) < 0)
            {
                return err_;
            }
            return err_;
        }
        /// <summary>
        /// 保存所有轴轴默认参数到文件
        /// </summary>
        /// <param name="paraFile">文件全名(xml格式)</param>
        public static void SaveAxesPara(string paraFile = null)
        {
            string file_ = paraFile;
            if (string.IsNullOrEmpty(paraFile))
            {
                file_ = Path.Combine(configFileFolder_, "axis.xml");
            }            
            _xmlSerialToFile(axisList_, file_);
        }
        /// <summary>
        /// 保存所有DIO信息
        /// </summary>
        /// <param name="paraFile">文件全名(xml格式)</param>
        public static void SaveDioPara(string paraFile = null)
        {
            string file_ = paraFile;
            if (string.IsNullOrEmpty(paraFile))
            {
                file_ = Path.Combine(configFileFolder_, "dio.xml");
            }            
            _xmlSerialToFile(dioList_, file_);
        }
        #region 参数序列化读写操作
        private static void _xmlSerialToFile<T>(T obj, string filePath)
        {
            XmlWriter writer = null;    //声明一个xml编写器
            XmlWriterSettings writerSetting = new XmlWriterSettings //声明编写器设置
            {
                Indent = true,//定义xml格式，自动创建新的行
                Encoding = UTF8Encoding.UTF8,//编码格式
            };

            //创建一个保存数据到xml文档的流
            writer = XmlWriter.Create(filePath, writerSetting);
            XmlSerializer xser = new XmlSerializer(typeof(T));  //实例化序列化对象

            xser.Serialize(writer, obj);  //序列化对象到xml文档
            writer.Close();
        }

        private static T _xmlDeserialFromFile<T>(string filePath)
        {
            string xmlString = File.ReadAllText(filePath);
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(xmlString)))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                return (T)serializer.Deserialize(stream);
            }
        }
        #endregion
    }
}