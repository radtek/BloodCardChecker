﻿using GxIAPINET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using SKABO.Camera.Enums;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using BloodCardChecker;
using System.IO;
using System.Linq;
using System.Threading;

namespace SKABO.Camera.DaHeng
{
    public class DaHengCamera
    {
        public static readonly object img_lock = new object();
        bool m_bIsOpen = false;                  ///<设备打开状态
        bool m_bIsSnap = false;                  ///<发送开采命令标识
        IGXFactory m_objIGXFactory = null;                   ///<Factory对像
        public IGXDevice m_objIGXDevice = null;                   ///<设备对像
        public IGXStream m_objIGXStream = null;                   ///<流对像
        public IGXFeatureControl m_objIGXFeatureControl = null;                   ///<远端设备属性控制器对像
        CStatistics m_objStatistic = new CStatistics();      ///<数据统计类对象用于处理统计时间
        CStopWatch m_objStopTime = new CStopWatch();       ///<定义时间差类对象
        public GxBitmap m_objGxBitmap = null;
        public int last_exposuretime = 0;
        public int camer_id = 0;
        ///<图像显示类对象
        /// <summary>
        /// 关闭流
        /// </summary>
        public DaHengCamera()
        {
            try
            {
                m_objIGXFactory = IGXFactory.GetInstance();
                m_objIGXFactory.Init();
            }catch(Exception e)
            {
                
            }
        }
         ~DaHengCamera()
        {
            Close();
        }
        private void __CloseStream()
        {
            try
            {
                //关闭流
                if (null != m_objIGXStream)
                {
                    m_objIGXStream.Close();
                    m_objIGXStream = null;
                }
            }
            catch (Exception)
            {
            }
        }
        /// <summary>
        /// 关闭设备
        /// </summary>
        private void __CloseDevice()
        {
            try
            {
                //关闭设备
                if (null != m_objIGXDevice)
                {
                    m_objIGXDevice.Close();
                    m_objIGXDevice = null;
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 停止采集关闭设备、关闭流
        /// </summary>
        private void __CloseAll()
        {
            try
            {
                // 如果未停采则先停止采集
                if (m_bIsSnap)
                {
                    if (null != m_objIGXFeatureControl)
                    {
                        m_objIGXFeatureControl.GetCommandFeature("AcquisitionStop").Execute();
                        m_objIGXFeatureControl = null;
                    }
                }
            }
            catch (Exception)
            {
            }
            m_bIsSnap = false;
            try
            {
                //停止流通道和关闭流
                if (null != m_objIGXStream)
                {
                    m_objIGXStream.StopGrab();
                    m_objIGXStream.Close();
                    m_objIGXStream = null;
                }
            }
            catch (Exception)
            {
            }

            //关闭设备
            __CloseDevice();
            m_bIsOpen = false;
        }
        /// <summary>
        /// 相机初始化
        /// </summary>
        private void __InitDevice()
        {
            if (null != m_objIGXFeatureControl)
            {
                //设置采集模式连续采集
                m_objIGXFeatureControl.GetEnumFeature("AcquisitionMode").SetValue("Continuous");
                //设置触发模式为开
                m_objIGXFeatureControl.GetEnumFeature("TriggerMode").SetValue("On");
                //m_objIGXFeatureControl.GetEnumFeature("TriggerMode").SetValue("Off");
                //选择触发源为软触发
                m_objIGXFeatureControl.GetEnumFeature("TriggerSource").SetValue("Software");

            }
        }
        public bool Close()
        {
            bool result = false;
            try
            {
                m_objStatistic.Reset();
                // 停止采集关闭设备、关闭流
                __CloseAll();
                result = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            return result;
        }


        public bool Open(int index)
        {
            camer_id = index;
            if (m_bIsOpen)
                return m_bIsOpen;
            bool result = false;
            try
            {
                //关闭流
                __CloseStream();
                __CloseDevice();
                List<IGXDeviceInfo> listGXDeviceInfo = new List<IGXDeviceInfo>();
                m_objIGXFactory.UpdateDeviceList(2000, listGXDeviceInfo);
                // 判断当前连接设备个数
                if (listGXDeviceInfo.Count <= 0)
                {
                    throw new Exception("没有发现设备");
                }
                //打开列表第一个设备
                m_objIGXDevice = m_objIGXFactory.OpenDeviceBySN(listGXDeviceInfo[index].GetSN(), GX_ACCESS_MODE.GX_ACCESS_EXCLUSIVE);
                m_objIGXFeatureControl = m_objIGXDevice.GetRemoteFeatureControl();
                //打开流
                if (null != m_objIGXDevice)
                {
                    m_objIGXStream = m_objIGXDevice.OpenStream(0);
                }


                __InitDevice();
                m_objGxBitmap = new GxBitmap(m_objIGXDevice);

                // 更新设备打开标识
                m_bIsOpen = true;


            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                result = m_bIsOpen;
            }
            return result;
        }
        /// <summary>
        /// 回调函数,用于获取图像信息和显示图像
        /// </summary>
        /// <param name="obj">用户自定义传入参数</param>
        /// <param name="objIFrameData">图像信息对象</param>
        private void __CaptureCallbackPro(object objUserParam, IFrameData objIFrameData)
        {

        }
        public void Play()
        {
            if (m_bIsSnap)
                return;
            try
            {
                //开启采集流通道
                if (null != m_objIGXStream)
                {
                    //RegisterCaptureCallback第一个参数属于用户自定参数(类型必须为引用
                    //类型)，若用户想用这个参数可以在委托函数中进行使用
                    //m_objIGXStream.RegisterCaptureCallback(this, __CaptureCallbackPro);
                    m_objIGXStream.UnregisterCaptureCallback();
                    m_objIGXStream.StartGrab();
                }

                //发送开采命令
                if (null != m_objIGXFeatureControl)
                {
                    m_objIGXFeatureControl.GetCommandFeature("AcquisitionStart").Execute();
                }
                m_bIsSnap = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        
        public void Stop()
        {
            try
            {
                //发送停采命令
                if (null != m_objIGXFeatureControl)
                {
                    m_objIGXFeatureControl.GetCommandFeature("AcquisitionStop").Execute();
                }

                //关闭采集流通道
                if (null != m_objIGXStream)
                {
                    m_objIGXStream.StopGrab();
                    //注销采集回调函数
                    m_objIGXStream.UnregisterCaptureCallback();
                }

                m_bIsSnap = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        public IImageData GetImage(uint time)
        {
            IImageData objIImageData = null;
            try
            {
                lock(img_lock)
                {
                    objIImageData = m_objIGXStream.GetImage(time);
                    //Thread.Sleep(100);
                }
            }
            catch
            {
                //Close();
                //Open(camer_id);
                Console.WriteLine("GetImage Faile");
            }
            return objIImageData;
        }
        public Bitmap GetBitmap()
        {
            m_objIGXStream.FlushQueue();
            m_objIGXFeatureControl.GetCommandFeature("TriggerSoftware").Execute();
            IImageData objIImageData = GetImage(500);
            var bm = m_objGxBitmap.GetBitmap(objIImageData);
            objIImageData.Destroy();
            return (Bitmap)bm.Clone();
        }

        /// <summary>
        /// bitmap转换为bitmapsource 以适应wpf的image
        /// </summary>
        /// <param name="pic"></param>
        /// <returns></returns>
        [System.Runtime.InteropServices.DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);
        public BitmapSource GetBitmapSource()
        {
            Bitmap pic = GetBitmap();
            if (pic == null) return null;
            IntPtr ip = pic.GetHbitmap();
            BitmapSource bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                ip, IntPtr.Zero, Int32Rect.Empty,
                System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            DeleteObject(ip);
            return bitmapSource;
        }

        public void SetExposureTime(int ExposureTime)
        {
            if (last_exposuretime != ExposureTime)
            {
                m_objIGXFeatureControl.GetFloatFeature("ExposureTime").SetValue(ExposureTime);
                last_exposuretime = ExposureTime;
            }
        }
        void GetAllFiles(string dir, List<string> allFiles)
        {
            DirectoryInfo di = new DirectoryInfo(dir);
            if (!di.Exists) return;//如果目录不存在,退出
            var currentDirFiles = di.GetFiles().Select(p => p.Name);//获取当前目录所有文件
            allFiles.AddRange(currentDirFiles);//将当前目录文件放到baiallFiles中
            var currentDirSubDirs = di.GetDirectories().ToList();//获取子目录
            currentDirSubDirs.ForEach(p => GetAllFiles(p.FullName, allFiles));//将子目录中的文件放入allFiles中
        }

        public Mat GetOpenCvMat(int ExposureTime)
        {
            SetExposureTime(ExposureTime);
            m_objIGXStream.FlushQueue();
            m_objIGXFeatureControl.GetCommandFeature("TriggerSoftware").Execute();
            IImageData objIImageData = null;
            objIImageData = GetImage(50000);
            if (objIImageData == null) return null;
            var bm = m_objGxBitmap.GetBitmap(objIImageData);
            var img = OpenCvSharp.Extensions.BitmapConverter.ToMat(bm);
            objIImageData.Destroy();
            return img;
        }


        public Bitmap GetOpenCvBitmap(int ExposureTime)
        {
            SetExposureTime(ExposureTime);
            m_objIGXStream.FlushQueue();
            m_objIGXFeatureControl.GetCommandFeature("TriggerSoftware").Execute();
            IImageData objIImageData = null;
            objIImageData = GetImage(500);
            if (objIImageData == null) return null;
            var bm = m_objGxBitmap.GetBitmap(objIImageData);
            objIImageData.Destroy();
            return bm;
        }

        public void SetExposureTime(double time)
        {
            m_objIGXFeatureControl.GetFloatFeature("ExposureTime").SetValue(time);
        }

        public double GetMinExposureTime()
        {
            if (null != m_objIGXFeatureControl)
            {
                return m_objIGXFeatureControl.GetFloatFeature("ExposureTime").GetMin();
            }
            return 10;
        }

        public double GetMaxExposureTime()
        {
            if (null != m_objIGXFeatureControl)
            {
                return m_objIGXFeatureControl.GetFloatFeature("ExposureTime").GetMax();
            }
            return 5000;
        }
        public void SetGain(double gain)
        {

            m_objIGXFeatureControl.GetFloatFeature("Gain").SetValue(gain);
        }

        public double GetMinGain()
        {
            
                return m_objIGXFeatureControl.GetFloatFeature("Gain").GetMin();
            
        }

        public double GetMaxGain()
        {
                return m_objIGXFeatureControl.GetFloatFeature("Gain").GetMax();
        }

        public double GetExposureTime()
        {
            return m_objIGXFeatureControl.GetFloatFeature("ExposureTime").GetValue(); 
        }

        public double GetGain()
        {
            return  m_objIGXFeatureControl.GetFloatFeature("Gain").GetValue();
        }

        private void SetBalanceWhiteChanel(BalanceWhiteChanelEnum balanceWhiteChanelEnum)
        {
            String val = "";
            if (balanceWhiteChanelEnum == BalanceWhiteChanelEnum.BALANCE_RATIO_SELECTOR_BLUE)
            {
                val = "Blue";
            }
            else if (balanceWhiteChanelEnum == BalanceWhiteChanelEnum.BALANCE_RATIO_SELECTOR_GREEN)
            {
                val = "Green";
            }
            else if (balanceWhiteChanelEnum == BalanceWhiteChanelEnum.BALANCE_RATIO_SELECTOR_RED)
            {
                val = "Red";
            }
            m_objIGXFeatureControl.GetEnumFeature("BalanceRatioSelector").SetValue(val);
        }

        public void SetBalanceWhiteAuto(BalanceWhiteAutoStatusEnum balanceWhiteAutoStatusEnum)
        {
            String val = "Off";
            if (balanceWhiteAutoStatusEnum == BalanceWhiteAutoStatusEnum.BALANCE_WHITE_AUTO_OFF)
            {
                val = "Off";
            }
            else if (balanceWhiteAutoStatusEnum == BalanceWhiteAutoStatusEnum.BALANCE_WHITE_AUTO_CONTINUOUS)
            {
                val = "Continuous";
            }
            else if (balanceWhiteAutoStatusEnum == BalanceWhiteAutoStatusEnum.BALANCE_WHITE_AUTO_ONCE)
            {
                val = "Once";
            }
                m_objIGXFeatureControl.GetEnumFeature("BalanceWhiteAuto").SetValue(val);
        }

        public void SetBalanceRatio(BalanceWhiteChanelEnum balanceWhiteChanelEnum, double val)
        {
            SetBalanceWhiteChanel(balanceWhiteChanelEnum);
            m_objIGXFeatureControl.GetFloatFeature("BalanceRatio").SetValue(val);
        }

        public double GetBalanceRatio(BalanceWhiteChanelEnum balanceWhiteChanelEnum)
        {
            SetBalanceWhiteChanel(balanceWhiteChanelEnum);
            return m_objIGXFeatureControl.GetFloatFeature("BalanceRatio").GetValue();
        }

        public double GetMinBalanceRatio(BalanceWhiteChanelEnum balanceWhiteChanelEnum)
        {
            SetBalanceWhiteChanel(balanceWhiteChanelEnum);
            return m_objIGXFeatureControl.GetFloatFeature("BalanceRatio").GetMin();
        }

        public double GetMaxBalanceRatio(BalanceWhiteChanelEnum balanceWhiteChanelEnum)
        {
            SetBalanceWhiteChanel(balanceWhiteChanelEnum);
            return m_objIGXFeatureControl.GetFloatFeature("BalanceRatio").GetMax();
        }

        public string[] SupportResolution()
        {
            return new String[] { "2048(H) x 1536(V)" };
        }
    }
}
